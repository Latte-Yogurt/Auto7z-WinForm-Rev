using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;

namespace Auto7z_Rev
{
    public partial class Auto7zMainForm : Form
    {
        private Dictionary<string, Dictionary<string, string>> languageTexts;

        public static class Parameters
        {
            public readonly static string workPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            public readonly static string extractPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            public readonly static string xmlPath = Path.Combine(workPath, "Auto7z_Rev.xml");
            public readonly static string appConfigPath = Path.Combine(workPath, "Auto7z_Rev.exe.config");
            public readonly static string auto7zPath = Path.Combine(extractPath, "Auto7z_Rev");
            public readonly static string sevenZPath = Path.Combine(auto7zPath, "7z");
            public readonly static string md5CalculatorPath = Path.Combine(auto7zPath, "md5Calculator.exe");
            public readonly static string MD5CalculatorAppConfigPath = Path.Combine(Parameters.auto7zPath, "MD5Calculator.exe.config");
            public readonly static string sevenZExePath = Path.Combine(sevenZPath, "7z.exe");
            public readonly static string sevenZDllPath = Path.Combine(sevenZPath, "7z.dll");
            public readonly static string langPath = Path.Combine(sevenZPath, "Lang");
            public readonly static string zhCNPath = Path.Combine(langPath, "zh-cn.txt");
            public readonly static string zhTWPath = Path.Combine(langPath, "zh-tw.txt");
            public static bool isFormLoaded { get; set; } // 标识符意思为是否确定窗体已被加载 
            public static bool hasPermission { get; set; }
            public static bool packedOneFile { get; set; }
            public static bool isHandleSeparately { get; set; } // 标记符意思为是否将文件进行合并处理
            public static string currentLanguage { get; set; }
            public static float systemScale { get; set; }
            public static int newScreenWidth { get; set; }
            public static int newScreenHeight { get; set; }
            public static float newSystemScale { get; set; }
            public static string volume { get; set; }
            public static string format { get; set; }
            public static string password { get; set; }
            public static bool zstd { get; set; }
            public static bool disableVolume { get; set; }
            public static bool generateMd5 { get; set; }
            public static bool autoSave { get; set; }
            public static bool portable { get; set; }
            public static bool maxLevelCompress { get; set; }
            public static long sevenZUsageCount { get; set; }
            public static string filePath { get; set; }
            public static string fileName { get; set; }
            public static string directoryPath { get; set; }
            public static string[] filePaths { get; set; }
            public static long fileSize { get; set; }
            public static long folderSize { get; set; }
            public static long fileSizes { get; set; }
            public static long folderSizes { get; set; }
            public static long totalSize { get; set; }
            public static string newFolderPath { get; set; }
        }

        public static class LogSetting
        {
            private readonly static string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            private readonly static string logFileName = "Auto7z.log";
            public readonly static string logFilePath = Path.Combine(desktopPath, logFileName);
        }

        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            base.OnDpiChanged(e);

            // 获取新的 DPI 缩放因子
            float newScale = e.DeviceDpiNew / 96.0f;
            Parameters.systemScale = newScale;

            INITIALIZE_MAINFORM_SIZE(newScale);
            UPDATE_MENUSTRIP_LAYOUT(MenuStrip, newScale);

            // 强制重绘界面以适应新 DPI 下的字体和控件
            Invalidate();
            Update();
        }

        #region MainForm Function
        public Auto7zMainForm(string[] args)
        {
            InitializeComponent();

            Parameters.hasPermission = CHECK_PATH_READ_WRITE(Parameters.workPath, out Exception error);

            if (!Parameters.hasPermission)
            {
                ERROR_APPLICATION_NO_PREMISSION(Parameters.workPath, error);
                Close();
                return;
            }

            CHECK_XML_LEGAL(Parameters.xmlPath);
            Parameters.systemScale = GET_SCALE();
            Parameters.currentLanguage = GET_CURRENT_LANGUAGE(Parameters.xmlPath);
            Parameters.newScreenWidth = GET_SCREEN_WIDTH(Parameters.xmlPath);
            Parameters.newScreenHeight = GET_SCREEN_HEIGHT(Parameters.xmlPath);
            Parameters.newSystemScale = GET_SYSTEM_SCALE(Parameters.xmlPath);

            InitializeLanguageTexts();
            UpdateLanguage();
            InitializeParameters();

            if (!CHECK_AUTO7Z_EXIST())
            {
                CREATE_COMPONENTS(out Exception ex);
                if (ex != null)
                {
                    ERROR_CREATE_COMPONENTS_FAILED(ex);
                    Close();
                }
            }

            if (args != null && args.Length > 0)
            {
                Parameters.isFormLoaded = false;
                MAIN_TASK(args);
                Close();
            }
        }
        #endregion

        #region Main Task
        private void MAIN_TASK(string[] args)
        {
            if (args != null && args.Length > 0)
            {
                if (args.Length > 1)
                {
                    Parameters.packedOneFile=QUESTION_PACKED_IN_ONE_FILE();
                    Parameters.isHandleSeparately = !Parameters.packedOneFile;
                }

                else
                {
                    Parameters.isHandleSeparately = false;
                }
            }

            if (args != null && args.Length > 0 && args.ToList().TrueForAll(arg => !string.IsNullOrEmpty(arg)))
            {
                if (!Parameters.isHandleSeparately)
                {
                    string[] filePaths = !Parameters.packedOneFile ? new string[] { Path.GetFullPath(args[0]) } : args;
                    string[] newFilePaths = null;

                    Parameters.fileName = File.Exists(args[0]) ? Path.GetFileNameWithoutExtension(Path.GetFullPath(args[0])) : Path.GetFileName(Path.GetFullPath(args[0]));
                    Parameters.directoryPath = Path.GetDirectoryName(Path.GetFullPath(args[0]));

                    bool isFileInUse = IS_FILE_IN_USE(filePaths, out Exception ex);

                    if (isFileInUse)
                    {
                        ERROR_FILE_IN_USE(ex);
                        return;
                    }

                    bool getSizeSuccess = !Parameters.packedOneFile ? GET_SINGLE_FILE_SIZE(filePaths[0]) : GET_MULTIPLE_FILES_SIZE(filePaths, out newFilePaths);
                    Parameters.filePaths = !Parameters.packedOneFile ? filePaths : newFilePaths;

                    if (!getSizeSuccess)
                    {
                        DELETE_EXTRACT_RESOURCE();
                        return;
                    }

                    if (Parameters.format == "7z" || Parameters.format == "zip")
                    {
                        if (!CHECK_AUTO7Z_EXIST())
                        {
                            CREATE_COMPONENTS(out Exception error);
                            if (error != null)
                            {
                                ERROR_CREATE_COMPONENTS_FAILED(error);
                                return;
                            }
                        }

                        string command = GENERATE_COMMAND(!Parameters.packedOneFile ? filePaths : newFilePaths);

                        if (command == null)
                        {
                            DELETE_EXTRACT_RESOURCE();
                            return;
                        }

                        bool finished = EXECUTE_COMMAND_BOOL(command);

                        if (!finished)
                        {
                            DELETE_FILES_AND_FOLDER_WHILE_UNFINISHED();
                        }

                        if (Parameters.generateMd5 && Directory.Exists(Parameters.newFolderPath))
                        {
                            GENERATE_MD5(Parameters.md5CalculatorPath, Parameters.newFolderPath);
                        }

                        ADD_SEVENZ_USAGE_COUNT();
                        DELETE_EXTRACT_RESOURCE();
                        return;
                    }

                    if (Parameters.format == "tar")
                    {
                        if (!CHECK_AUTO7Z_EXIST())
                        {
                            CREATE_COMPONENTS(out Exception error);
                            if (error != null)
                            {
                                ERROR_CREATE_COMPONENTS_FAILED(error);
                                return;
                            }
                        }

                        string command = GENERATE_COMMAND(!Parameters.packedOneFile ? filePaths : newFilePaths);

                        if (command == null)
                        {
                            DELETE_EXTRACT_RESOURCE();
                            return;
                        }

                        bool tarFinished = EXECUTE_COMMAND_BOOL(command);

                        if (!tarFinished)
                        {
                            DELETE_FILES_AND_FOLDER_WHILE_UNFINISHED();
                        }

                        if (Parameters.zstd)
                        {
                            string zstdCommand = ZSTD_COMMAND();
                            bool zstdFinished = EXECUTE_COMMAND_BOOL(zstdCommand);

                            if (!zstdFinished)
                            {
                                DELETE_FILES_AND_FOLDER_WHILE_UNFINISHED();
                            }

                            else
                            {
                                DELETE_TEMP_TAR(Parameters.newFolderPath);
                            }
                        }

                        if (Parameters.generateMd5 && Directory.Exists(Parameters.newFolderPath))
                        {
                            GENERATE_MD5(Parameters.md5CalculatorPath, Parameters.newFolderPath);
                        }

                        ADD_SEVENZ_USAGE_COUNT();
                        DELETE_EXTRACT_RESOURCE();
                    }
                }

                else
                {
                    var processedFileNamesMap = new Dictionary<string, string>();
                    var fileNameCounts = new Dictionary<string, int>();

                    foreach (var file in args)
                    {
                        string fullPath = Path.GetFullPath(file);
                        string baseName = File.Exists(fullPath) ? Path.GetFileNameWithoutExtension(fullPath) : Path.GetFileName(fullPath);

                        string processingKey = baseName;
                        string finalBaseName = baseName;

                        if (fileNameCounts.ContainsKey(processingKey))
                        {
                            fileNameCounts[processingKey]++;
                        }
                        else
                        {
                            fileNameCounts.Add(processingKey, 1);
                        }

                        int currentCount = fileNameCounts[processingKey];

                        if (currentCount > 1)
                        {
                            finalBaseName = $"{baseName}({currentCount - 1})";
                        }

                        processedFileNamesMap.Add(fullPath, finalBaseName);
                    }

                    foreach (var singleFilePath in args)
                    {
                        Parameters.filePath = Path.GetFullPath(singleFilePath);
                        Parameters.fileName = processedFileNamesMap[Parameters.filePath];
                        Parameters.directoryPath = Path.GetDirectoryName(Parameters.filePath);

                        bool isFileInUse = IS_FILE_IN_USE(new string[] { Parameters.filePath }, out Exception ex);

                        if (isFileInUse)
                        {
                            WRANING_FILE_IN_USE(Parameters.fileName, ex);
                            continue;
                        }

                        bool getSizeSuccess = GET_SINGLE_FILE_SIZE(Parameters.filePath);

                        if (!getSizeSuccess)
                        {
                            continue;
                        }

                        if (Parameters.format == "7z" || Parameters.format == "zip")
                        {
                            if (!CHECK_AUTO7Z_EXIST())
                            {
                                CREATE_COMPONENTS(out Exception error);
                                if (error != null)
                                {
                                    ERROR_CREATE_COMPONENTS_FAILED(error);
                                    break;
                                }
                            }

                            string command = GENERATE_COMMAND(new string[] { Parameters.filePath });

                            bool finished = EXECUTE_COMMAND_BOOL(command);

                            if (!finished)
                            {
                                DELETE_FILES_AND_FOLDER_WHILE_UNFINISHED();
                            }

                            if (Parameters.generateMd5 && Directory.Exists(Parameters.newFolderPath))
                            {
                                GENERATE_MD5(Parameters.md5CalculatorPath, Parameters.newFolderPath);
                            }

                            ADD_SEVENZ_USAGE_COUNT();
                        }

                        if (Parameters.format == "tar")
                        {
                            if (!CHECK_AUTO7Z_EXIST())
                            {
                                CREATE_COMPONENTS(out Exception error);
                                if (error != null)
                                {
                                    ERROR_CREATE_COMPONENTS_FAILED(error);
                                    break;
                                }
                            }

                            string command = GENERATE_COMMAND(new string[] { Parameters.filePath });

                            bool tarFinished = EXECUTE_COMMAND_BOOL(command);

                            if (!tarFinished)
                            {
                                DELETE_FILES_AND_FOLDER_WHILE_UNFINISHED();
                            }

                            if (Parameters.zstd)
                            {
                                string zstdCommand = ZSTD_COMMAND();
                                bool zstdFinished = EXECUTE_COMMAND_BOOL(zstdCommand);

                                if (!zstdFinished)
                                {
                                    DELETE_FILES_AND_FOLDER_WHILE_UNFINISHED();
                                }
                                else
                                {
                                    DELETE_TEMP_TAR(Parameters.newFolderPath);
                                }
                            }

                            if (Parameters.generateMd5 && Directory.Exists(Parameters.newFolderPath))
                            {
                                GENERATE_MD5(Parameters.md5CalculatorPath, Parameters.newFolderPath);
                            }

                            ADD_SEVENZ_USAGE_COUNT();
                        }
                    }

                    DELETE_EXTRACT_RESOURCE();
                }
            }
        }
        #endregion

        #region Initialize Parameters
        private float GET_SCALE()
        {
            float dpi;
            using (Graphics g = CreateGraphics())
            {
                dpi = g.DpiX;
            }

            return dpi / 96.0f;
        }

        private void InitializeLanguageTexts()
        {
            languageTexts = new Dictionary<string, Dictionary<string, string>>
            {
                { "zh-CN", new Dictionary<string, string>
                    {
                        { "LanguageMenu","语言" },
                        { "LanguageMenuSelect","选择语言" },
                        { "OptionMenu","选项"},
                        { "OptionMenuDisableVolume","禁用分卷"},
                        { "OptionMenuGenerateMD5","压缩完成后生成MD5文件"},
                        { "AboutMenu","关于" },
                        { "AboutAuto7zRev","关于 Auto7z Rev" },
                        { "LabelSize","分卷大小:" },
                        { "LabelFormat","生成格式:" },
                        { "LabelPassword","添加密码:" },
                        { "CheckBoxAutoSave","程序关闭时自动保存配置" },
                        { "ButtonConfig","保存配置" },
                    }
                },
                { "zh-TW", new Dictionary<string, string>
                    {
                        { "LanguageMenu","語言" },
                        { "LanguageMenuSelect","選擇語言" },
                        { "OptionMenu","選項"},
                        { "OptionMenuDisableVolume","禁用分卷"},
                        { "OptionMenuGenerateMD5","壓縮完成后生成MD5文件"},
                        { "AboutMenu","關於" },
                        { "AboutAuto7zRev","關於 Auto7z Rev" },
                        { "LabelSize","分卷大小:" },
                        { "LabelFormat","生成格式:" },
                        { "LabelPassword","添加密碼:" },
                        { "CheckBoxAutoSave","程式關閉時自動保存配置" },
                        { "ButtonConfig","保存配置" },
                    }
                },
                { "en-US", new Dictionary<string, string>
                    {
                        { "LanguageMenu","Language" },
                        { "LanguageMenuSelect","Select Language" },
                        { "OptionMenu","Options"},
                        { "OptionMenuDisableVolume","Disable Volume"},
                        { "OptionMenuGenerateMD5","Generate MD5 file after compression completed"},
                        { "AboutMenu","About" },
                        { "AboutAuto7zRev","About Auto7z Rev" },
                        { "LabelSize","Volume Size:" },
                        { "LabelFormat","Generate Format:" },
                        { "LabelPassword","Add Password:" },
                        { "CheckBoxAutoSave","Save config on exit" },
                        { "ButtonConfig","Save config" },
                    }
                }
            };
        }

        private void UpdateLanguage()
        {
            LanguageMenu.Text = languageTexts[Parameters.currentLanguage]["LanguageMenu"];
            LanguageMenuSelect.Text = languageTexts[Parameters.currentLanguage]["LanguageMenuSelect"];
            OptionMenu.Text = languageTexts[Parameters.currentLanguage]["OptionMenu"];
            OptionMenuDisableVolume.Text = languageTexts[Parameters.currentLanguage]["OptionMenuDisableVolume"];
            OptionMenuGenerateMD5.Text = languageTexts[Parameters.currentLanguage]["OptionMenuGenerateMD5"];
            AboutMenu.Text = languageTexts[Parameters.currentLanguage]["AboutMenu"];
            AboutAuto7zRev.Text = languageTexts[Parameters.currentLanguage]["AboutAuto7zRev"];
            LabelSize.Text = languageTexts[Parameters.currentLanguage]["LabelSize"];
            LabelFormat.Text = languageTexts[Parameters.currentLanguage]["LabelFormat"];
            LabelPassword.Text = languageTexts[Parameters.currentLanguage]["LabelPassword"];
            CheckBoxAutoSave.Text = languageTexts[Parameters.currentLanguage]["CheckBoxAutoSave"];
            ButtonConfig.Text = languageTexts[Parameters.currentLanguage]["ButtonConfig"];
        }

        private void InitializeParameters()
        {
            Parameters.volume = GET_VOLUME(Parameters.xmlPath);
            Parameters.format = GET_FORMAT(Parameters.xmlPath);
            Parameters.password = GET_PASSWORD(Parameters.xmlPath);
            Parameters.zstd = GET_ZSTD(Parameters.xmlPath);
            Parameters.disableVolume = GET_DISABLE_VOLUME(Parameters.xmlPath);
            Parameters.generateMd5 = GET_GENERATE_MD5(Parameters.xmlPath);
            Parameters.autoSave = GET_AUTOSAVE(Parameters.xmlPath);
            Parameters.portable = GET_PORTABLE(Parameters.xmlPath);
            Parameters.maxLevelCompress = GET_MAXLEVEL_COMPRESS(Parameters.xmlPath);
            Parameters.sevenZUsageCount = GET_SEVENZ_USAGE_COUNT(Parameters.xmlPath);

            DEFAULT_OPTION_MENU_DISABLE_VOLUME();
            DEFAULT_OPTION_MENU_GENERATE_MD5();
            DEFAULT_VOLUME_TEXTBOX();
            DEFAULT_FORMAT_MENU();
            DEFAULT_ZSTD();
            DEFAULT_PASSWORD_TEXTBOX();
            DEFAULT_AUTOSAVE();
        }

        private void INITIALIZE_MAINFORM_SIZE(float scale)
        {
            // 设置自动缩放模式
            AutoScaleMode = AutoScaleMode.Dpi;

            MinimumSize = new Size(0, 0);
            MaximumSize = MinimumSize;
            UPDATE_MIN_MAX_SIZE(scale);

            float baseWidth = 320f;
            float baseHeight = 320f;
            Size = new Size((int)(baseWidth * scale), (int)(baseHeight * scale));

            INITIALIZE_TABLE_LAYOUT_PANEL_PIXEL();
            INITIALIZE_UI_FONT_SIZE();
        }

        private void UPDATE_MIN_MAX_SIZE(float scale)
        {
            int width = (int)(320 * scale);
            int height = (int)(320 * scale);
            MinimumSize = new Size(width, height);
            MaximumSize = MinimumSize;
        }

        private void INITIALIZE_TABLE_LAYOUT_PANEL_PIXEL()
        {
            // tableLayoutPanel的索引从0开始，即第一行或列的索引号为0
            // tableLayoutPanel的cell指定为先列后行，例如cell=0,1即为第一列第二行

            // 对列的定义
            SET_COLUMN_SIZE(tableLayoutPanel, 0, SizeType.Absolute, 10f);
            SET_COLUMN_SIZE(tableLayoutPanel, 1, SizeType.Percent, 12f);
            SET_COLUMN_SIZE(tableLayoutPanel, 2, SizeType.Percent, 25f);
            SET_COLUMN_SIZE(tableLayoutPanel, 3, SizeType.Percent, 12f);
            SET_COLUMN_SIZE(tableLayoutPanel, 4, SizeType.Percent, 12f);
            SET_COLUMN_SIZE(tableLayoutPanel, 5, SizeType.Percent, 12f);
            SET_COLUMN_SIZE(tableLayoutPanel, 6, SizeType.Percent, 22f);
            SET_COLUMN_SIZE(tableLayoutPanel, 7, SizeType.Percent, 5f);
            SET_COLUMN_SIZE(tableLayoutPanel, 8, SizeType.Absolute, 10f);

            // 对行的定义
            SET_ROW_SIZE(tableLayoutPanel, 0, SizeType.Percent, 10f);
            SET_ROW_SIZE(tableLayoutPanel, 1, SizeType.Absolute, 10f);
            SET_ROW_SIZE(tableLayoutPanel, 2, SizeType.Percent, 16f);
            SET_ROW_SIZE(tableLayoutPanel, 3, SizeType.Percent, 16f);
            SET_ROW_SIZE(tableLayoutPanel, 4, SizeType.Absolute, 20f);
            SET_ROW_SIZE(tableLayoutPanel, 5, SizeType.Percent, 16f);
            SET_ROW_SIZE(tableLayoutPanel, 6, SizeType.Absolute, 20f);
            SET_ROW_SIZE(tableLayoutPanel, 7, SizeType.Percent, 16f);
            SET_ROW_SIZE(tableLayoutPanel, 8, SizeType.Percent, 15f);
            SET_ROW_SIZE(tableLayoutPanel, 9, SizeType.Percent, 11f);
            SET_ROW_SIZE(tableLayoutPanel, 10, SizeType.Absolute, 5f);
        }

        private void SET_COLUMN_SIZE(TableLayoutPanel panel,int num,SizeType type,float fontSize)
        {
            panel.ColumnStyles[num].SizeType = type;
            panel.ColumnStyles[num].Width = fontSize;
        }

        private void SET_ROW_SIZE(TableLayoutPanel panel, int num, SizeType type, float fontSize)
        {
            panel.RowStyles[num].SizeType = type;
            panel.RowStyles[num].Height = fontSize;
        }

        private void INITIALIZE_UI_FONT_SIZE()
        {
            SET_FONT_SIZE(MenuStrip, Font.Size);
            SET_FONT_SIZE(LabelSize, Font.Size);
            SET_FONT_SIZE(TextBoxSize, Font.Size);
            SET_FONT_SIZE(LabelFormat, Font.Size);
            SET_FONT_SIZE(ComboBoxFormat, Font.Size);
            SET_FONT_SIZE(LabelUnit, Font.Size);
            SET_FONT_SIZE(CheckBoxZstd, Font.Size);
            SET_FONT_SIZE(LabelPassword, Font.Size);
            SET_FONT_SIZE(TextBoxPassword, Font.Size);
            SET_FONT_SIZE(CheckBoxAutoSave, Font.Size);
            SET_FONT_SIZE(ButtonConfig, Font.Size);
        }

        private void SET_FONT_SIZE(Control obj, float fontSize)// 使用dynamic或Control绕过编译时的类型检查，直到运行时才解析
        {
            obj.Font = new Font(obj.Font.FontFamily, fontSize, obj.Font.Style, GraphicsUnit.Point);
        }

        private void UPDATE_MENUSTRIP_LAYOUT(MenuStrip MenuStrip, float scale)
        {
            if (MenuStrip != null)
            {
                int iconSize = (int)(16 * scale);
                MenuStrip.ImageScalingSize = new Size(iconSize, iconSize);

                foreach (ToolStripItem item in MenuStrip.Items)
                {
                    UPDATE_TOOL_STRIP_ITEM(item);
                }
            }
        }

        private void UPDATE_TOOL_STRIP_ITEM(ToolStripItem item)
        {
            item.Font = new Font(item.Font.FontFamily, Font.Size, item.Font.Style);

            if (item is ToolStripMenuItem menuItem)
            {
                menuItem.ImageScaling = ToolStripItemImageScaling.SizeToFit;

                foreach (ToolStripItem subItem in menuItem.DropDownItems)
                {
                    // 递归调用自身
                    UPDATE_TOOL_STRIP_ITEM(subItem);
                }
            }
        }
        #endregion

        #region Check Parameters
        private bool CHECK_PATH_READ_WRITE(string path, out Exception error)
        {
            error = null;
            string checkFilePath = Path.Combine(
                path, "~testFile_" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var fs = new FileStream(checkFilePath, FileMode.CreateNew,
                                               FileAccess.Write, FileShare.None))
                {
                    fs.WriteByte(0);
                }
                using (var fs = new FileStream(checkFilePath, FileMode.Open,
                                               FileAccess.Read, FileShare.Read))
                {
                    fs.ReadByte();
                }
                return true;
            }
            catch (UnauthorizedAccessException ex) { error = ex; return false; }
            catch (Exception ex) { error = ex; return false; }
            finally
            {
                try { if (File.Exists(checkFilePath)) File.Delete(checkFilePath); }
                catch { /* 清理失败不影响判定 */ }
            }
        }

        private void CHECK_FILE_LEGAL(string filePath)
        {
            FileInfo file = new FileInfo(filePath);

            if (file.Exists && (file.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
            {
                file.Attributes = FileAttributes.Normal;
            }

            if (file.Exists && (file.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
            {
                file.Attributes = FileAttributes.Normal;
            }
        }

        private void CHECK_FOLDER_LEGAL(string directoryPath)
        {
            DirectoryInfo directory = new DirectoryInfo(directoryPath);

            if (directory.Exists && (directory.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
            {
                directory.Attributes = FileAttributes.Normal;
            }

            if (directory.Exists && (directory.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
            {
                directory.Attributes = FileAttributes.Normal;
            }
        }

        private void CHECK_XML_LEGAL(string configFilePath)
        {
            FileInfo configFile = new FileInfo(configFilePath);

            if (configFile.Exists && (configFile.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
            {
                configFile.Attributes = FileAttributes.Normal;
            }

            if (configFile.Exists && (configFile.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
            {
                configFile.Attributes = FileAttributes.Normal;
            }

            if (!configFile.Exists)
            {
                CREATE_DEFAULT_CONFIG(configFilePath);
            }
        }

        private void CHECK_LOG_LEGAL(string configFilePath)
        {
            FileInfo configFile = new FileInfo(configFilePath);

            if (configFile.Exists && (configFile.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
            {
                configFile.Attributes = FileAttributes.Normal;
            }

            if (configFile.Exists && (configFile.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
            {
                configFile.Attributes = FileAttributes.Normal;
            }
        }

        private bool CHECK_AUTO7Z_EXIST()
        {
            return Directory.Exists(Parameters.auto7zPath)
                && Directory.Exists(Parameters.sevenZPath)
                && File.Exists(Parameters.sevenZExePath)
                && File.Exists(Parameters.sevenZDllPath)
                && Directory.Exists(Parameters.langPath)
                && File.Exists(Parameters.zhCNPath)
                && File.Exists(Parameters.zhTWPath);
        }

        private bool IS_PROCESS_RUNNING(string processName)
        {
            Process[] processes = Process.GetProcessesByName(processName);

            foreach (Process p in processes)
            {
                try
                {
                    // 检查进程的可执行文件路径是否与组件路径匹配
                    if (p.MainModule.FileName.Equals(Parameters.sevenZExePath, StringComparison.OrdinalIgnoreCase))
                    {
                        return true; // 找到了一个正在运行的进程，并且路径正确
                    }
                }

                catch (Exception ex)
                {
                    ERROR_EXCEPTION_MESSAGE(ex);
                    return false;
                }
            }

            return false; // 没有找到路径正确的正在运行的进程
        }

        private string[] GET_DIRECTOR_CONTENTS(string dirPath)
        {
            List<string> paths = new List<string>();

            foreach (string file in Directory.GetFiles(dirPath, "*.*", SearchOption.AllDirectories))
            {
                paths.Add(file);
            }

            return paths.ToArray();
        }

        private bool IS_FILE_IN_USE(string[] filePaths, out Exception ex)
        {
            ex = null;
            try
            {
                foreach (var file in filePaths)
                {
                    if (File.Exists(file))
                    {
                        // 尝试以独占方式打开文件
                        using (FileStream stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None))
                        {
                            return false; // 文件未被占用
                        }
                    }

                    else
                    {
                        string[] paths = GET_DIRECTOR_CONTENTS(file);

                        foreach (var path in paths)
                        {
                            try
                            {
                                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
                                {
                                    // 如果能成功打开，不做任何操作
                                }
                            }
                            catch (IOException ioEx)
                            {
                                ex = ioEx;
                                // 如果抛出IOException，表示文件被占用，跳出循环并返回true
                                return true;
                            }
                        }
                    }
                }
            }
            catch (IOException ioEx)
            {
                ex = ioEx;
                // 如果抛出Exception，表示文件被占用，跳出循环并返回true
                return true;
            }

            return false; // 文件夹内部的所有文件均未被占用
        }
        #endregion

        #region Extract Resources
        private void CREATE_COMPONENTS(out Exception error)
        {
            error = null;

            try
            {
                if (!File.Exists(Parameters.appConfigPath))
                {
                    CREATE_APP_CONFIG();
                }

                if (!Directory.Exists(Parameters.auto7zPath))
                {
                    CREATE_RESOURCE_FOLDER(Parameters.auto7zPath);
                }

                if (Directory.Exists(Parameters.auto7zPath))
                {
                    CHECK_FOLDER_LEGAL(Parameters.auto7zPath);

                    if (!File.Exists(Parameters.md5CalculatorPath))
                    {
                        CREATE_MD5_CALCULATOR_EXE();
                    }

                    if (!File.Exists(Parameters.MD5CalculatorAppConfigPath))
                    {
                        CREATE_MD5CALCULATOR_APP_CONFIG();
                    }

                    if (!Directory.Exists(Parameters.sevenZPath))
                    {
                        CREATE_RESOURCE_FOLDER(Parameters.sevenZPath);
                    }

                    if (Directory.Exists(Parameters.sevenZPath))
                    {
                        CHECK_FOLDER_LEGAL(Parameters.sevenZPath);

                        if (!File.Exists(Parameters.sevenZExePath))
                        {
                            CREATE_SEVENZ_EXE();
                        }

                        if (!File.Exists(Parameters.sevenZDllPath))
                        {
                            CREATE_SEVENZ_DLL();
                        }

                        if (!Directory.Exists(Parameters.langPath))
                        {
                            CREATE_RESOURCE_FOLDER(Parameters.langPath);
                        }

                        if (Directory.Exists(Parameters.langPath))
                        {
                            CHECK_FOLDER_LEGAL(Parameters.langPath);

                            if (!File.Exists(Parameters.zhCNPath))
                            {
                                CREATE_ZHCN_TXT();
                            }

                            if (!File.Exists(Parameters.zhTWPath))
                            {
                                CREATE_ZHTW_TXT();
                            }
                        }
                    }
                }
            }

            catch (Exception ex)
            {
                error = ex;
            }
        }

        private void EXTRACT_RESOURCE(string resourceName, string outputPath)
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    ERROR_RESOURCE_EXIST(resourceName);
                    return;
                }

                using (FileStream fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
                {
                    stream.CopyTo(fileStream);
                }
            }
        }

        private void CREATE_RESOURCE_FOLDER(string newFolderPath)
        {
            if (!Directory.Exists(newFolderPath))
            {
                Directory.CreateDirectory(newFolderPath);
            }
        }

        private void CREATE_APP_CONFIG()
        {
            string resourceName = "Auto7z_Rev.Resources.Auto7z_Rev.exe.config";
            string outputFileName = resourceName.Replace("Auto7z_Rev.Resources.", "");
            string outputPath = Path.Combine(Parameters.workPath, outputFileName);
            EXTRACT_RESOURCE(resourceName, outputPath);
        }

        private void CREATE_MD5_CALCULATOR_EXE()
        {
            string resourceName = "Auto7z_Rev.Resources.MD5Calculator.exe";
            string outputFileName = resourceName.Replace("Auto7z_Rev.Resources.", "");
            string outputPath = Path.Combine(Parameters.auto7zPath, outputFileName);
            EXTRACT_RESOURCE(resourceName, outputPath);
        }

        private void CREATE_MD5CALCULATOR_APP_CONFIG()
        {
            string resourceName = "Auto7z_Rev.Resources.MD5Calculator.exe.config";
            string outputFileName = resourceName.Replace("Auto7z_Rev.Resources.", "");
            string outputPath = Path.Combine(Parameters.auto7zPath, outputFileName);
            EXTRACT_RESOURCE(resourceName, outputPath);
        }

        private void CREATE_SEVENZ_EXE()
        {
            string resourceName = "Auto7z_Rev.Resources.sevenz.7z.exe";
            string outputFileName = resourceName.Replace("Auto7z_Rev.Resources.sevenz.", "");//将原始嵌入资源的文件名中的命名空间前缀替换为空字符串
            string outputPath = Path.Combine(Parameters.sevenZPath, outputFileName);
            EXTRACT_RESOURCE(resourceName, outputPath);
        }

        private void CREATE_SEVENZ_DLL()
        {
            string resourceName = "Auto7z_Rev.Resources.sevenz.7z.dll";
            string outputFileName = resourceName.Replace("Auto7z_Rev.Resources.sevenz.", "");
            string outputPath = Path.Combine(Parameters.sevenZPath, outputFileName);
            EXTRACT_RESOURCE(resourceName, outputPath);
        }

        private void CREATE_ZHCN_TXT()
        {
            string resourceName = "Auto7z_Rev.Resources.sevenz.Lang.zh-cn.txt";
            string outputFileName = resourceName.Replace("Auto7z_Rev.Resources.sevenz.Lang.", "");
            string outputPath = Path.Combine(Parameters.langPath, outputFileName);
            EXTRACT_RESOURCE(resourceName, outputPath);
        }

        private void CREATE_ZHTW_TXT()
        {
            string resourceName = "Auto7z_Rev.Resources.sevenz.Lang.zh-tw.txt";
            string outputFileName = resourceName.Replace("Auto7z_Rev.Resources.sevenz.Lang.", "");
            string outputPath = Path.Combine(Parameters.langPath, outputFileName);
            EXTRACT_RESOURCE(resourceName, outputPath);
        }
        #endregion

        #region Generate Command
        private bool EXECUTE_COMMAND_BOOL(string command)
        {
            var process = new Process();
            process.StartInfo.FileName = $"\"{Parameters.sevenZExePath}\"";
            process.StartInfo.Arguments = command;
            process.StartInfo.UseShellExecute = false;

            try
            {
                process.Start();
                process.WaitForExit(); // 等待进程完成
                return process.ExitCode == 0; // 如果ExitCode为0，则返回true（表示成功）
            }

            catch (Exception ex)
            {
                ERROR_EXCEPTION_MESSAGE(ex);
                return false; // 发生异常时返回false
            }

            finally
            {
                process.Dispose(); // 清理资源
            }
        }

        private bool CREATE_NEW_FOLDER()
        {
            string folderCodeName = null;
            string nowTime = DateTime.Now.ToString("HH-mm-ss");

            switch (Parameters.currentLanguage)
            {
                case "zh-CN":
                    folderCodeName = $"压缩文件_{nowTime}";
                    break;
                case "zh-TW":
                    folderCodeName = $"壓縮檔_{nowTime}";
                    break;
                case "en-US":
                    folderCodeName = $"Compressed File_{nowTime}";
                    break;
            }

            Parameters.newFolderPath = $"{Parameters.directoryPath}\\{Parameters.fileName}_{folderCodeName}";

            if (!Directory.Exists(Parameters.newFolderPath))
            {
                try
                {
                    Directory.CreateDirectory(Parameters.newFolderPath);
                    return true;
                }

                catch (Exception ex)
                {
                    ERROR_CREATE_FOLDER_FAILED(ex);
                    return false;
                }
            }

            else
            {
                return true;
            }
        }

        private string GENERATE_COMMAND(string[] paths)
        {
            if (!CREATE_NEW_FOLDER())
            {
                return null;
            }

            if (!Parameters.packedOneFile)
            {
                return GENERATE_SINGLE_FILE_COMMAND(paths[0]);
            }

            else
            {
                return GENERATE_MULTIPLE_FILES_COMMAND(paths);
            }
        }

        private string GENERATE_SINGLE_FILE_COMMAND(string path)
        {
            string fullPath = Path.GetFullPath(path);

            if (!IS_VALID_PATH(fullPath))
            {
                return null;
            }

            long size = GET_FILE_SIZE_OR_FOLDER_SIZE(fullPath);
            string command = BUILD_COMMAND(fullPath, size);

            return command;
        }

        private string GENERATE_MULTIPLE_FILES_COMMAND(string[] paths)
        {
            foreach (var path in paths)
            {
                string fullPath = Path.GetFullPath(path);

                if (!IS_VALID_PATH(fullPath))
                {
                    return null;
                }
            }

            string command = BUILD_COMMAND_FOR_MULTIPLE_FILES(paths);

            return command;
        }

        private bool IS_VALID_PATH(string fullPath)
        {
            if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
            {
                if (fullPath.Length >= 260)
                {
                    ERROR_TOO_LONG_PATH(fullPath);
                }
                else
                {
                    ERROR_FILE_MAYBE_NOT_EXIST(fullPath, Parameters.newFolderPath);
                }

                return false;
            }

            return true;
        }

        private long GET_FILE_SIZE_OR_FOLDER_SIZE(string path)
        {
            bool isFile = File.Exists(path);
            return isFile ? Parameters.fileSize : Parameters.folderSize;
        }

        private string BUILD_COMMAND(string fullPath, long size)
        {
            bool isNumber = int.TryParse(Parameters.volume, out int targetSize);

            if (!isNumber)
            {
                targetSize = 0;
            }

            if (Parameters.format == "7z" || Parameters.format == "zip" || Parameters.format == "tar")
            {
                string command = $"a -aoa -t{Parameters.format} \"{Parameters.newFolderPath}\\{Parameters.fileName}.{Parameters.format}\" \"{fullPath}\"";

                if (ADD_VOLUME_CONDITION(size, targetSize))
                {
                    command += $" -v{targetSize}m";
                }

                if (!string.IsNullOrEmpty(Parameters.password) && Parameters.format != "tar")
                {
                    command += $" -p{Parameters.password}";
                }

                if (Parameters.format == "7z" && Parameters.maxLevelCompress)
                {
                    command += @" -m0=LZMA2 -mx=9 -ms=on";
                }

                return command;
            }

            return null;
        }

        private string BUILD_COMMAND_FOR_MULTIPLE_FILES(string[] paths)
        {
            bool isNumber = int.TryParse(Parameters.volume, out int targetSize);

            if (!isNumber)
            {
                targetSize = 0;
            }

            if (Parameters.format == "7z" || Parameters.format == "zip" || Parameters.format == "tar")
            {
                StringBuilder command = new StringBuilder();
                command.Append($"a -aoa -t{Parameters.format} \"{Parameters.newFolderPath}\\{Parameters.fileName}.{Parameters.format}\"");

                foreach (var path in paths)
                {
                    string fullPath = Path.GetFullPath(path);
                    command.Append($" \"{fullPath}\"");
                }

                if (ADD_VOLUME_CONDITION(Parameters.totalSize, targetSize))
                {
                    command.Append($" -v{targetSize}m");
                }

                if (!string.IsNullOrEmpty(Parameters.password) && Parameters.format != "tar")
                {
                    command.Append($" -p{Parameters.password}");
                }

                if (Parameters.format == "7z" && Parameters.maxLevelCompress)
                {
                    command.Append(@" -m0=LZMA2 -mx=9 -ms=on");
                }

                return command.ToString();
            }

            return null;
        }

        private bool ADD_VOLUME_CONDITION(long size, int targetSize)
        {
            if (Parameters.format != "tar")
            {
                return size > targetSize && targetSize > 0 && !OptionMenuDisableVolume.Checked;
            }

            else
            {
                return size > targetSize && targetSize > 0 && !OptionMenuDisableVolume.Checked && !CheckBoxZstd.Checked;
            }
        }

        private string ZSTD_COMMAND()
        {
            string targetTar = $"{Parameters.newFolderPath}\\{Parameters.fileName}.tar";

            if (!File.Exists(targetTar) || !Directory.Exists(Parameters.newFolderPath))
            {
                return null; // 如果文件或目录不存在，返回 null
            }

            bool isNumber = int.TryParse(Parameters.volume, out int targetSize);

            if (!isNumber)
            {
                targetSize = 0;
            }

            bool isFile = !Parameters.isHandleSeparately ? File.Exists(Parameters.filePaths[0]) : File.Exists(Parameters.filePath);
            long size = isFile ? Parameters.fileSize : Parameters.folderSize; // 确定使用文件大小还是文件夹大小

            string command = $"a -aoa -tzstd \"{Parameters.newFolderPath}\\{Parameters.fileName}.tar.zst\" \"{Parameters.newFolderPath}\\{Parameters.fileName}.tar\"";

            // 添加分卷选项
            if (size > targetSize && targetSize > 0 && !OptionMenuDisableVolume.Checked)
            {
                command += $" -v{targetSize}m";
            }

            return command;
        }
        #endregion

        #region Generate MD5
        private void GENERATE_MD5(string md5Exe, string path)
        {
            try
            {
                var process = new Process();
                process.StartInfo.FileName = $"\"{md5Exe}\"";
                process.StartInfo.Arguments = $"\"{path}\"";
                process.StartInfo.UseShellExecute = false;

                process.Start();
                process.WaitForExit(); // 等待进程完成
            }
            catch (Exception ex)
            {
                ERROR_EXCEPTION_MESSAGE(ex);
            }
        }
        #endregion

        #region Delete Files
        private void DELETE_DIRECTOR_CONTENTS(string dirPath)
        {
            CHECK_FOLDER_LEGAL(dirPath);

            foreach (string file in Directory.GetFiles(dirPath))
            {
                CHECK_FILE_LEGAL(file);
                File.Delete(file); // 删除文件
            }

            foreach (string subDir in Directory.GetDirectories(dirPath))
            {
                CHECK_FOLDER_LEGAL(subDir);
                DELETE_DIRECTOR_CONTENTS(subDir); // 递归调用删除子目录
                Directory.Delete(subDir); // 删除子目录
            }
        }

        private void DELETE_FILES_AND_FOLDER_WHILE_UNFINISHED()
        {
            if (Directory.Exists($"{Parameters.newFolderPath}"))
            {
                DELETE_DIRECTOR_CONTENTS(Parameters.newFolderPath);
                Directory.Delete(Parameters.newFolderPath);
            }
        }

        private void DELETE_FILES_CAUSE_PORTABLE_MODE()
        {
            if (Directory.Exists($"{Parameters.auto7zPath}"))
            {
                DELETE_DIRECTOR_CONTENTS(Parameters.auto7zPath);
                Directory.Delete(Parameters.auto7zPath);
            }
        }

        private void DELETE_EXTRACT_RESOURCE()
        {
            if (Parameters.portable && !IS_PROCESS_RUNNING("7z"))
            {
                DELETE_FILES_CAUSE_PORTABLE_MODE();
            }
        }

        private void DELETE_TEMP_TAR(string dirPath)
        {
            if (Directory.Exists(dirPath))
            {
                string tempTarFile = $"{dirPath}\\{Parameters.fileName}.tar";

                if (File.Exists(tempTarFile))
                {
                    try
                    {
                        File.Delete(tempTarFile);
                    }
                    catch (Exception ex)
                    {
                        ERROR_EXCEPTION_MESSAGE(ex);
                    }
                }
            }
        }
        #endregion

        #region Get Size
        private bool GET_SINGLE_FILE_SIZE(string path)
        {
            string fullPath = Path.GetFullPath(path);
            string name = File.Exists(fullPath) ? Path.GetFileNameWithoutExtension(fullPath) : Path.GetFileName(fullPath);
            string dirertory = Path.GetDirectoryName(fullPath);

            if (File.Exists(fullPath))
            {
                Parameters.fileSize = GET_FILE_SIZE(fullPath);

                if (Parameters.fileSize == -1)
                {
                    ERROR_EMPTY_SIZE(name, fullPath);
                    return false;
                }

                return true;
            }

            else if (Directory.Exists(fullPath))
            {
                Parameters.folderSize = GET_FOLDER_SIZE(fullPath);

                if (Parameters.folderSize == -1)
                {
                    ERROR_EMPTY_SIZE(name, fullPath);
                    return false;
                }

                return true;
            }

            else if (!File.Exists(fullPath) || !Directory.Exists(fullPath))
            {
                bool canReadWrite = CHECK_PATH_READ_WRITE(dirertory, out Exception noPermissionEx);

                if (fullPath.Length >= 260)
                {
                    ERROR_TOO_LONG_PATH(fullPath);
                }

                if (!canReadWrite)
                {
                    ERROR_APPLICATION_NO_PREMISSION(dirertory, noPermissionEx);
                }

                return false;
            }

            return false;
        }

        private bool GET_MULTIPLE_FILES_SIZE(string[] paths, out string[] newFilePaths)
        {
            List<string> fileList = new List<string>(paths);

            foreach (var path in paths)
            {
                string fullPath = Path.GetFullPath(path);
                string name = File.Exists(fullPath) ? Path.GetFileNameWithoutExtension(fullPath) : Path.GetFileName(fullPath);
                string dirertory = Path.GetDirectoryName(fullPath);

                if (File.Exists(fullPath))
                {
                    Parameters.fileSize = GET_FILE_SIZE(fullPath);

                    if (Parameters.fileSize != -1)
                    {
                        Parameters.fileSizes += Parameters.fileSize;
                    }

                    else
                    {
                        fileList.Remove(fullPath);
                        WARNING_REMOVE_ELEMENT(name, fullPath);
                    }
                }

                else if (Directory.Exists(fullPath))
                {
                    Parameters.folderSize = GET_FOLDER_SIZE(fullPath);

                    if (Parameters.folderSize != -1)
                    {
                        Parameters.folderSizes += Parameters.folderSize;
                    }

                    else
                    {
                        fileList.Remove(fullPath);
                        WARNING_REMOVE_ELEMENT(name, fullPath);
                    }
                }

                else if (!File.Exists(fullPath) || !Directory.Exists(fullPath))
                {
                    bool canReadWrite = CHECK_PATH_READ_WRITE(dirertory, out Exception noPermissionEx);

                    if (fullPath.Length >= 260)
                    {
                        ERROR_TOO_LONG_PATH(fullPath);
                    }

                    if (!canReadWrite)
                    {
                        ERROR_APPLICATION_NO_PREMISSION(dirertory, noPermissionEx);
                    }
                }
            }

            string[] filePaths = fileList.ToArray();
            newFilePaths = filePaths;

            if (filePaths != null)
            {
                Parameters.totalSize = Parameters.fileSizes + Parameters.folderSizes;
                return true;
            }

            return false;
        }

        private long GET_FILE_SIZE(string filePath)
        {
            if (filePath != null)
            {
                FileInfo fileInfo = new FileInfo(filePath);
                long fileSizeBytes = fileInfo.Length;

                long fileSizeMiB = fileSizeBytes / (1024 * 1024);

                return fileSizeMiB;
            }

            else
            {
                return -1;
            }
        }

        private long GET_FOLDER_SIZE(string filePath)
        {
            if (filePath != null)
            {
                long folderSizeBytes = 0;

                // 获取文件夹中的所有文件和子文件夹
                DirectoryInfo directoryInfo = new DirectoryInfo(filePath);

                // 计算文件大小
                FileInfo[] files = directoryInfo.GetFiles("*", SearchOption.AllDirectories);
                foreach (FileInfo file in files)
                {
                    folderSizeBytes += file.Length;
                }

                long folderSizeMiB = folderSizeBytes / (1024 * 1024);

                return folderSizeMiB;
            }

            else
            {
                return -1;
            }
        }
        #endregion

        #region Initialize Configurations
        private void CREATE_DEFAULT_CONFIG(string configFilePath)
        {
            if (File.Exists(configFilePath))
            {
                try
                {
                    File.WriteAllText(configFilePath, string.Empty);
                }

                catch (Exception ex)
                {
                    ERROR_EXCEPTION_MESSAGE(ex);
                    Close();
                }
            }

            int newLocationX = Screen.FromControl(this).Bounds.Width / 2 - Width / 2;
            int newLocationY = Screen.FromControl(this).Bounds.Height / 2 - Height / 2;

            // 获取当前系统的显示语言
            var currentCulture = CultureInfo.CurrentUICulture;

            // 创建一个示例词典，包含支持的语言
            var supportedLanguages = new HashSet<string>
            {
                "zh-CN", // 中文 (简体)
                "zh-TW", // 中文 (繁体)
                "en-US", // 英语 (美国)
                // 其他语言...
            };

            XElement defaultConfig;

            if (supportedLanguages.Contains(currentCulture.Name))
            {
                if (Parameters.currentLanguage != null)
                {
                    defaultConfig = new XElement("Configuration",
                        new XElement("Language", $"{Parameters.currentLanguage}"),
                        new XElement("ScreenWidth", "0"),
                        new XElement("ScreenHeight", "0"),
                        new XElement("SystemScale", "0"),
                        new XElement("LocationX", $"{newLocationX}"),
                        new XElement("LocationY", $"{newLocationY}"),
                        new XElement("Volume", "2000"),
                        new XElement("Format", "7z"),
                        new XElement("Password", ""),
                        new XElement("Zstd", "True"),
                        new XElement("DisableVolume", "False"),
                        new XElement("GenerateMD5", "True"),
                        new XElement("AutoSave", "True"),
                        new XElement("Portable", "True"),
                        new XElement("MaxLevelCompress", "False"),
                        new XElement("SevenZUsageCount", "0")
                    );
                }

                else
                {
                    defaultConfig = new XElement("Configuration",
                        new XElement("Language", $"{currentCulture.Name}"),
                        new XElement("ScreenWidth", "0"),
                        new XElement("ScreenHeight", "0"),
                        new XElement("SystemScale", "0"),
                        new XElement("LocationX", $"{newLocationX}"),
                        new XElement("LocationY", $"{newLocationY}"),
                        new XElement("Volume", "2000"),
                        new XElement("Format", "7z"),
                        new XElement("Password", ""),
                        new XElement("Zstd", "True"),
                        new XElement("DisableVolume", "False"),
                        new XElement("GenerateMD5", "True"),
                        new XElement("AutoSave", "True"),
                        new XElement("Portable", "True"),
                        new XElement("MaxLevelCompress", "False"),
                        new XElement("SevenZUsageCount", "0")
                    );
                }
            }

            else
            {
                defaultConfig = new XElement("Configuration",
                    new XElement("Language", "en-US"),
                    new XElement("ScreenWidth", "0"),
                    new XElement("ScreenHeight", "0"),
                    new XElement("SystemScale", "0"),
                    new XElement("LocationX", $"{newLocationX}"),
                    new XElement("LocationY", $"{newLocationY}"),
                    new XElement("Volume", "2000"),
                    new XElement("Format", "7z"),
                    new XElement("Password", ""),
                    new XElement("Zstd", "True"),
                    new XElement("DisableVolume", "False"),
                    new XElement("GenerateMD5", "True"),
                    new XElement("AutoSave", "True"),
                    new XElement("Portable", "True"),
                    new XElement("MaxLevelCompress", "False"),
                    new XElement("SevenZUsageCount", "0")
                );
            }

            try
            {
                defaultConfig.Save(configFilePath);
            }
            catch (Exception ex)
            {
                ERROR_EXCEPTION_MESSAGE(ex);
            }
        }

        private void UPDATE_CONFIG(string filePath, string key, string newValue)
        {
            try
            {
                XDocument xdoc = XDocument.Load(filePath); // 加载 XML 文件

                var element = xdoc.Descendants(key).FirstOrDefault(); // 查找指定节点
                if (element != null)
                {
                    element.Value = newValue; // 修改节点值
                }
                else
                {
                    // 创建新节点并设置值
                    xdoc.Root.Add(new XElement(key, newValue));
                }

                xdoc.Save(filePath); // 保存文件
            }

            catch (Exception)
            {
                CREATE_DEFAULT_CONFIG(Parameters.xmlPath);
            }
        }

        private string GET_CURRENT_LANGUAGE(string configFilePath)
        {
            // 检查文件是否存在
            if (!File.Exists(configFilePath))
            {
                // 如果不存在，创建默认配置文件
                CREATE_DEFAULT_CONFIG(configFilePath);
            }

            // 获取当前系统的显示语言
            var currentCulture = CultureInfo.CurrentUICulture;

            // 创建一个示例词典，包含支持的语言
            var supportedLanguages = new HashSet<string>
                {
                    "zh-CN", // 中文 (简体)
                    "zh-TW", // 中文 (繁体)
                    "en-US", // 英语 (美国)
                    // 其他语言...
                };

            XDocument xdoc;

            try
            {
                // 加载 XML 文档
                xdoc = XDocument.Load(configFilePath);
            }

            catch (XmlException)
            {
                // 如果加载失败，创建新的默认配置文件并返回默认值
                CREATE_DEFAULT_CONFIG(configFilePath);

                if (supportedLanguages.Contains(currentCulture.Name))
                {
                    return currentCulture.Name;
                }

                else
                {
                    return "en-US";
                }
            }

            // 检查 Language 节点是否存在
            var languageNode = xdoc.Descendants("Language").FirstOrDefault();

            if (languageNode == null)
            {
                // 检查当前语言是否在词典中
                if (supportedLanguages.Contains(currentCulture.Name))
                {
                    // 如果没有找到 Language 节点，创建新的 XML 节点
                    XElement newNode = new XElement("Language", $"{currentCulture.Name}");

                    // 将新节点添加到根节点
                    xdoc.Root.Add(newNode);

                    // 保存更改
                    xdoc.Save(configFilePath);

                    return currentCulture.Name;
                }

                else
                {
                    XElement newNode = new XElement("Language", "en-US");

                    xdoc.Root.Add(newNode);

                    xdoc.Save(configFilePath);

                    return "en-US";
                }
            }

            // 获取 Language 节点的值
            var language = languageNode.Value;

            // 如果获取到的值为空字符串
            if (string.IsNullOrEmpty(language))
            {
                // 检查当前语言是否在词典中
                if (supportedLanguages.Contains(currentCulture.Name))
                {
                    return currentCulture.Name;
                }

                else
                {
                    return "en-US";
                }
            }

            // 检查语言是否在支持语言词典中
            if (!supportedLanguages.Contains(language))
            {
                // 如果在，返回当前的系统显示语言（如果在支持列表中）
                if (supportedLanguages.Contains(currentCulture.Name))
                {
                    return currentCulture.Name;
                }
                else
                {
                    return "en-US"; // 默认语言
                }
            }

            // 返回获取到的值
            return language;
        }

        private int GET_SCREEN_WIDTH(string configFilePath)
        {
            if (!File.Exists(configFilePath))
            {
                CREATE_DEFAULT_CONFIG(configFilePath);
            }

            int newWidth = Screen.FromControl(this).Bounds.Width;

            XDocument xdoc;

            try
            {
                // 加载 XML 文档
                xdoc = XDocument.Load(configFilePath);
            }

            catch (XmlException)
            {
                // 如果加载失败，创建新的默认配置文件并返回默认值
                CREATE_DEFAULT_CONFIG(configFilePath);

                return 0;
            }

            var widthNode = xdoc.Descendants("ScreenWidth").FirstOrDefault();

            if (widthNode == null)
            {

                XElement newNode = new XElement("ScreenWidth", newWidth);

                xdoc.Root.Add(newNode);

                xdoc.Save(configFilePath);

                return 0;
            }

            var width = widthNode.Value;
            int widthToInt;

            if (string.IsNullOrEmpty(width))
            {
                return 0;
            }

            if (!int.TryParse(width, out widthToInt))
            {
                return 0;
            }

            if (widthToInt <= 0)
            {
                return 0;
            }

            if (widthToInt == Screen.FromControl(this).Bounds.Width)
            {
                return widthToInt;
            }

            return 0;
        }

        private int GET_SCREEN_HEIGHT(string configFilePath)
        {
            if (!File.Exists(configFilePath))
            {
                CREATE_DEFAULT_CONFIG(configFilePath);
            }

            int newHeight = Screen.FromControl(this).Bounds.Height;

            XDocument xdoc;

            try
            {
                // 加载 XML 文档
                xdoc = XDocument.Load(configFilePath);
            }

            catch (XmlException)
            {
                // 如果加载失败，创建新的默认配置文件并返回默认值
                CREATE_DEFAULT_CONFIG(configFilePath);

                return 0;
            }

            var heightNode = xdoc.Descendants("ScreenHeight").FirstOrDefault();

            if (heightNode == null)
            {

                XElement newNode = new XElement("ScreenHeight", newHeight);

                xdoc.Root.Add(newNode);

                xdoc.Save(configFilePath);

                return 0;
            }

            var height = heightNode.Value;
            int heightToInt;

            if (string.IsNullOrEmpty(height))
            {
                return 0;
            }

            if (!int.TryParse(height, out heightToInt))
            {
                return 0;
            }

            if (heightToInt <= 0)
            {
                return 0;
            }

            if (heightToInt == Screen.FromControl(this).Bounds.Height)
            {
                return heightToInt;
            }

            return 0;
        }

        private float GET_SYSTEM_SCALE(string configFilePath)
        {
            if (!File.Exists(configFilePath))
            {
                CREATE_DEFAULT_CONFIG(configFilePath);
            }

            XDocument xdoc;

            try
            {
                // 加载 XML 文档
                xdoc = XDocument.Load(configFilePath);
            }

            catch (XmlException)
            {
                // 如果加载失败，创建新的默认配置文件并返回默认值
                CREATE_DEFAULT_CONFIG(configFilePath);

                return 0;
            }

            var scaleNode = xdoc.Descendants("SystemScale").FirstOrDefault();

            if (scaleNode == null)
            {

                XElement newNode = new XElement("SystemScale", Parameters.systemScale);

                xdoc.Root.Add(newNode);

                xdoc.Save(configFilePath);

                return 0;
            }

            var scale = scaleNode.Value;
            float scaleToFloat;

            if (string.IsNullOrEmpty(scale))
            {
                return 0;
            }

            if (!float.TryParse(scale, out scaleToFloat))
            {
                return 0;
            }

            if (scaleToFloat <= 0)
            {
                return 0;
            }

            const float epsilon = 0.00001f; // 定义一个浮点值的误差范围

            if (Math.Abs(scaleToFloat - Parameters.systemScale) < epsilon)
            {
                return scaleToFloat;
            }

            return 0;
        }

        private int GET_LOCATION_X(string configFilePath)
        {
            if (!File.Exists(configFilePath))
            {
                CREATE_DEFAULT_CONFIG(configFilePath);
            }

            int newLocationX = Screen.FromControl(this).Bounds.Width / 2 - Size.Width / 2;

            XDocument xdoc;

            try
            {
                // 加载 XML 文档
                xdoc = XDocument.Load(configFilePath);
            }

            catch (XmlException)
            {
                // 如果加载失败，创建新的默认配置文件并返回默认值
                CREATE_DEFAULT_CONFIG(configFilePath);

                return newLocationX;
            }

            var locationXNode = xdoc.Descendants("LocationX").FirstOrDefault();

            if (locationXNode == null)
            {

                XElement newNode = new XElement("LocationX", newLocationX);

                xdoc.Root.Add(newNode);

                xdoc.Save(configFilePath);

                return newLocationX;
            }

            var locationXValue = locationXNode.Value;
            int locationXToInt;

            if (string.IsNullOrEmpty(locationXValue))
            {
                return newLocationX;
            }

            if (!int.TryParse(locationXValue, out locationXToInt))
            {
                return newLocationX;
            }

            if (locationXToInt > Screen.FromControl(this).Bounds.Width - Size.Width || locationXToInt < -10)
            {
                return newLocationX;
            }

            return locationXToInt;
        }

        private int GET_LOCATION_Y(string configFilePath)
        {
            if (!File.Exists(configFilePath))
            {
                CREATE_DEFAULT_CONFIG(configFilePath);
            }

            int newLocationY = Screen.FromControl(this).Bounds.Height / 2 - Size.Height / 2;

            XDocument xdoc;

            try
            {
                // 加载 XML 文档
                xdoc = XDocument.Load(configFilePath);
            }

            catch (XmlException)
            {
                // 如果加载失败，创建新的默认配置文件并返回默认值
                CREATE_DEFAULT_CONFIG(configFilePath);

                return newLocationY;
            }

            var locationYNode = xdoc.Descendants("LocationY").FirstOrDefault();

            if (locationYNode == null)
            {
                XElement newNode = new XElement("LocationY", newLocationY);

                xdoc.Root.Add(newNode);

                xdoc.Save(configFilePath);

                return newLocationY;
            }

            var locationYValue = locationYNode.Value;
            int locationYToInt;

            if (string.IsNullOrEmpty(locationYValue))
            {
                return newLocationY;
            }

            if (!int.TryParse(locationYValue, out locationYToInt))
            {
                return newLocationY;
            }

            if (locationYToInt > Screen.FromControl(this).Bounds.Height - Size.Height || locationYToInt < 0)
            {
                return newLocationY;
            }

            return locationYToInt;
        }

        private string GET_VOLUME(string configFilePath)
        {
            if (!File.Exists(configFilePath))
            {
                CREATE_DEFAULT_CONFIG(configFilePath);
            }

            XDocument xdoc;

            try
            {
                // 加载 XML 文档
                xdoc = XDocument.Load(configFilePath);
            }

            catch (XmlException)
            {
                // 如果加载失败，创建新的默认配置文件并返回默认值
                CREATE_DEFAULT_CONFIG(configFilePath);

                return "2000";
            }

            var volumeNode = xdoc.Descendants("Volume").FirstOrDefault();

            if (volumeNode == null)
            {
                XElement newNode = new XElement("Volume", "2000");

                xdoc.Root.Add(newNode);

                xdoc.Save(configFilePath);

                return "2000";
            }

            var volumeValue = volumeNode.Value;
            int volumeToInt;

            if (string.IsNullOrEmpty(volumeValue))
            {
                return "2000";
            }

            if (!int.TryParse(volumeValue, out volumeToInt))
            {
                return "2000";
            }

            if (volumeToInt <= 0)
            {
                return "0";
            }

            return volumeToInt.ToString();
        }

        private string GET_FORMAT(string configFilePath)
        {
            if (!File.Exists(configFilePath))
            {
                CREATE_DEFAULT_CONFIG(configFilePath);
            }

            XDocument xdoc;

            try
            {
                // 加载 XML 文档
                xdoc = XDocument.Load(configFilePath);
            }

            catch (XmlException)
            {
                // 如果加载失败，创建新的默认配置文件并返回默认值
                CREATE_DEFAULT_CONFIG(configFilePath);

                return "7z";
            }

            var formatNode = xdoc.Descendants("Format").FirstOrDefault();

            var supportedFormat = new HashSet<string>
            {
                "7z",
                "zip",
                "tar"
            };

            if (formatNode == null)
            {
                XElement newNode = new XElement("Format", "7z");

                xdoc.Root.Add(newNode);

                xdoc.Save(configFilePath);

                return "7z";
            }

            var formatValue = formatNode.Value;

            if (string.IsNullOrEmpty(formatValue))
            {
                return "7z";
            }

            if (!supportedFormat.Contains(formatValue))
            {
                return "7z";
            }

            return formatValue;
        }

        private string GET_PASSWORD(string configFilePath)
        {
            if (!File.Exists(configFilePath))
            {
                CREATE_DEFAULT_CONFIG(configFilePath);
            }

            XDocument xdoc;

            try
            {
                // 加载 XML 文档
                xdoc = XDocument.Load(configFilePath);
            }

            catch (XmlException)
            {
                // 如果加载失败，创建新的默认配置文件并返回默认值
                CREATE_DEFAULT_CONFIG(configFilePath);

                return null;
            }

            var passwordNode = xdoc.Descendants("Password").FirstOrDefault();

            if (passwordNode == null)
            {
                XElement newNode = new XElement("Password", "");

                xdoc.Root.Add(newNode);

                xdoc.Save(configFilePath);

                return null;
            }

            var passwordValue = passwordNode.Value;

            if (string.IsNullOrEmpty(passwordValue))
            {
                return null;
            }

            return passwordValue;
        }

        private bool GET_ZSTD(string configFilePath)
        {
            if (!File.Exists(configFilePath))
            {
                CREATE_DEFAULT_CONFIG(configFilePath);
            }

            XDocument xdoc;

            try
            {
                // 加载 XML 文档
                xdoc = XDocument.Load(configFilePath);
            }

            catch (XmlException)
            {
                // 如果加载失败，创建新的默认配置文件并返回默认值
                CREATE_DEFAULT_CONFIG(configFilePath);

                return true;
            }

            var zstdNode = xdoc.Descendants("Zstd").FirstOrDefault();

            var supportedBool = new HashSet<string>
            {
                "True",
                "False"
            };

            if (zstdNode == null)
            {
                XElement newNode = new XElement("Zstd", "True");

                xdoc.Root.Add(newNode);

                xdoc.Save(configFilePath);

                return true;
            }

            var zstdValue = zstdNode.Value;
            bool zstdToBool;

            if (string.IsNullOrEmpty(zstdValue))
            {
                return true;
            }

            if (!supportedBool.Contains(zstdValue))
            {
                return true;
            }

            if (!bool.TryParse(zstdValue, out zstdToBool))
            {
                return true;
            }

            return zstdToBool;
        }

        private bool GET_DISABLE_VOLUME(string configFilePath)
        {
            if (!File.Exists(configFilePath))
            {
                CREATE_DEFAULT_CONFIG(configFilePath);
            }

            XDocument xdoc;

            try
            {
                // 加载 XML 文档
                xdoc = XDocument.Load(configFilePath);
            }

            catch (XmlException)
            {
                // 如果加载失败，创建新的默认配置文件并返回默认值
                CREATE_DEFAULT_CONFIG(configFilePath);

                return false;
            }

            var disableVolumeNode = xdoc.Descendants("DisableVolume").FirstOrDefault();

            var supportedBool = new HashSet<string>
            {
                "True",
                "False"
            };

            if (disableVolumeNode == null)
            {
                XElement newNode = new XElement("DisableVolume", "False");

                xdoc.Root.Add(newNode);

                xdoc.Save(configFilePath);

                return false;
            }

            var disableVolumeValue = disableVolumeNode.Value;
            bool disableVolumeToBool;

            if (string.IsNullOrEmpty(disableVolumeValue))
            {
                return false;
            }

            if (!supportedBool.Contains(disableVolumeValue))
            {
                return false;
            }

            if (!bool.TryParse(disableVolumeValue, out disableVolumeToBool))
            {
                return false;
            }

            return disableVolumeToBool;
        }

        private bool GET_GENERATE_MD5(string configFilePath)
        {
            if (!File.Exists(configFilePath))
            {
                CREATE_DEFAULT_CONFIG(configFilePath);
            }

            XDocument xdoc;

            try
            {
                // 加载 XML 文档
                xdoc = XDocument.Load(configFilePath);
            }

            catch (XmlException)
            {
                // 如果加载失败，创建新的默认配置文件并返回默认值
                CREATE_DEFAULT_CONFIG(configFilePath);

                return true;
            }

            var generateMd5Node = xdoc.Descendants("GenerateMD5").FirstOrDefault();

            var supportedBool = new HashSet<string>
            {
                "True",
                "False"
            };

            if (generateMd5Node == null)
            {
                XElement newNode = new XElement("GenerateMD5", "True");

                xdoc.Root.Add(newNode);

                xdoc.Save(configFilePath);

                return true;
            }

            var generateMd5Value = generateMd5Node.Value;
            bool generateMd5ToBool;

            if (string.IsNullOrEmpty(generateMd5Value))
            {
                return true;
            }

            if (!supportedBool.Contains(generateMd5Value))
            {
                return true;
            }

            if (!bool.TryParse(generateMd5Value, out generateMd5ToBool))
            {
                return true;
            }

            return generateMd5ToBool;
        }

        private bool GET_AUTOSAVE(string configFilePath)
        {
            if (!File.Exists(configFilePath))
            {
                CREATE_DEFAULT_CONFIG(configFilePath);
            }

            XDocument xdoc;

            try
            {
                // 加载 XML 文档
                xdoc = XDocument.Load(configFilePath);
            }

            catch (XmlException)
            {
                // 如果加载失败，创建新的默认配置文件并返回默认值
                CREATE_DEFAULT_CONFIG(configFilePath);

                return true;
            }

            var autoSaveNode = xdoc.Descendants("AutoSave").FirstOrDefault();

            var supportedBool = new HashSet<string>
            {
                "True",
                "False"
            };

            if (autoSaveNode == null)
            {
                XElement newNode = new XElement("AutoSave", "True");

                xdoc.Root.Add(newNode);

                xdoc.Save(configFilePath);

                return true;
            }

            var autoSaveValue = autoSaveNode.Value;
            bool autoSaveToBool;

            if (string.IsNullOrEmpty(autoSaveValue))
            {
                return true;
            }

            if (!supportedBool.Contains(autoSaveValue))
            {
                return true;
            }

            if (!bool.TryParse(autoSaveValue, out autoSaveToBool))
            {
                return true;
            }

            return autoSaveToBool;
        }

        private bool GET_PORTABLE(string configFilePath)
        {
            if (!File.Exists(configFilePath))
            {
                CREATE_DEFAULT_CONFIG(configFilePath);
            }

            XDocument xdoc;

            try
            {
                // 加载 XML 文档
                xdoc = XDocument.Load(configFilePath);
            }

            catch (XmlException)
            {
                // 如果加载失败，创建新的默认配置文件并返回默认值
                CREATE_DEFAULT_CONFIG(configFilePath);

                return true;
            }

            var portableNode = xdoc.Descendants("Portable").FirstOrDefault();

            var supportedBool = new HashSet<string>
            {
                "True",
                "False"
            };

            if (portableNode == null)
            {
                XElement newNode = new XElement("Portable", "True");

                xdoc.Root.Add(newNode);

                xdoc.Save(configFilePath);

                return true;
            }

            var portableValue = portableNode.Value;
            bool portableToBool;

            if (string.IsNullOrEmpty(portableValue))
            {
                return true;
            }

            if (!supportedBool.Contains(portableValue))
            {
                return true;
            }

            if (!bool.TryParse(portableValue, out portableToBool))
            {
                return true;
            }

            return portableToBool;
        }

        private bool GET_MAXLEVEL_COMPRESS(string configFilePath)
        {
            if (!File.Exists(configFilePath))
            {
                CREATE_DEFAULT_CONFIG(configFilePath);
            }

            XDocument xdoc;

            try
            {
                // 加载 XML 文档
                xdoc = XDocument.Load(configFilePath);
            }

            catch (XmlException)
            {
                // 如果加载失败，创建新的默认配置文件并返回默认值
                CREATE_DEFAULT_CONFIG(configFilePath);

                return false;
            }

            var maxLevelCompressNode = xdoc.Descendants("MaxLevelCompress").FirstOrDefault();

            var supportedBool = new HashSet<string>
            {
                "True",
                "False"
            };

            if (maxLevelCompressNode == null)
            {
                XElement newNode = new XElement("MaxLevelCompress", "False");

                xdoc.Root.Add(newNode);

                xdoc.Save(configFilePath);

                return false;
            }

            var maxLevelCompressValue = maxLevelCompressNode.Value;
            bool maxLevelCompressToBool;

            if (string.IsNullOrEmpty(maxLevelCompressValue))
            {
                return false;
            }

            if (!supportedBool.Contains(maxLevelCompressValue))
            {
                return false;
            }

            if (!bool.TryParse(maxLevelCompressValue, out maxLevelCompressToBool))
            {
                return false;
            }

            return maxLevelCompressToBool;
        }

        private long GET_SEVENZ_USAGE_COUNT(string configFilePath)
        {
            if (!File.Exists(configFilePath))
            {
                CREATE_DEFAULT_CONFIG(configFilePath);
            }

            XDocument xdoc;

            try
            {
                // 加载 XML 文档
                xdoc = XDocument.Load(configFilePath);
            }

            catch (XmlException)
            {
                // 如果加载失败，创建新的默认配置文件并返回默认值
                CREATE_DEFAULT_CONFIG(configFilePath);

                return 0;
            }

            var sevenZUsageCountNode = xdoc.Descendants("SevenZUsageCount").FirstOrDefault();

            if (sevenZUsageCountNode == null)
            {

                XElement newNode = new XElement("SevenZUsageCount", "0");

                xdoc.Root.Add(newNode);

                xdoc.Save(configFilePath);

                return 0;
            }

            var sevenZUsageCountValue = sevenZUsageCountNode.Value;
            long sevenZUsageCountToLong;

            if (string.IsNullOrEmpty(sevenZUsageCountValue))
            {
                return 0;
            }

            if (!long.TryParse(sevenZUsageCountValue, out sevenZUsageCountToLong))
            {
                return 0;
            }

            if (sevenZUsageCountToLong < 0)
            {
                return 0;
            }

            return sevenZUsageCountToLong;
        }
        #endregion

        #region Initialize UI Contents
        private void DEFAULT_VOLUME_TEXTBOX()
        {
            TextBoxSize.Text = Parameters.volume.ToString();
        }

        private void DEFAULT_FORMAT_MENU()
        {
            ComboBoxFormat.Items.Add("7z");
            ComboBoxFormat.Items.Add("zip");
            ComboBoxFormat.Items.Add("tar");

            if (Parameters.format == "7z")
            {
                ComboBoxFormat.SelectedIndex = 0;
            }

            if (Parameters.format == "zip")
            {
                ComboBoxFormat.SelectedIndex = 1;
            }

            if (Parameters.format == "tar")
            {
                ComboBoxFormat.SelectedIndex = 2;
            }
        }

        private void DEFAULT_PASSWORD_TEXTBOX()
        {
            TextBoxPassword.Text = Parameters.password;

            if (Parameters.format == "tar")
            {
                TextBoxPassword.Enabled = false;
            }

            else
            {
                TextBoxPassword.Enabled = true;
            }
        }

        private void DEFAULT_OPTION_MENU_DISABLE_VOLUME()
        {
            OptionMenuDisableVolume.Checked = Parameters.disableVolume;
        }

        private void DEFAULT_OPTION_MENU_GENERATE_MD5()
        {
            OptionMenuGenerateMD5.Checked = Parameters.generateMd5;
        }

        private void DEFAULT_ZSTD()
        {
            CheckBoxZstd.Checked = Parameters.zstd;
        }

        private void DEFAULT_AUTOSAVE()
        {
            CheckBoxAutoSave.Checked = Parameters.autoSave;
        }
        #endregion

        #region Message and Log
        private void WRITE_ERROR_LOG(string message, Exception error)
        {
            string nowTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            CHECK_LOG_LEGAL(LogSetting.logFilePath);

            using (StreamWriter writer = new StreamWriter(LogSetting.logFilePath, true))
            {
                writer.WriteLine($"{nowTime}: {message},{error.Message}");
                writer.WriteLine();
            }
        }

        private void WRITE_MESSAGE_LOG(string message)
        {
            string nowTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            CHECK_LOG_LEGAL(LogSetting.logFilePath);

            using (StreamWriter writer = new StreamWriter(LogSetting.logFilePath, true))
            {
                writer.WriteLine($"{nowTime}: {message}");
                writer.WriteLine();
            }
        }

        private bool QUESTION_PACKED_IN_ONE_FILE()
        {
            bool isPack;
            DialogResult result = DialogResult.None;

            switch (Parameters.currentLanguage)
            {
                case "zh-CN":
                    result = MessageBox.Show("检测到多个文件传入，是否需要将多个文件打包为单个压缩文件中？", "提示", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                    break;
                case "zh-TW":
                    result = MessageBox.Show("檢測到多個文件傳入，是否需要將多個文件打包為單個壓縮檔？", "提示", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                    break;
                case "en-US":
                    result = MessageBox.Show("Multiple files have been detected. Do you want to package them into a single compressed file?", "Notice", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                    break;
            }

            if (result == DialogResult.Yes)
            {
                isPack = true;
            }

            else
            {
                isPack = false;
            }

            return isPack;
        }

        private void NOTICE_CONFIG_SAVED()
        {
            switch (Parameters.currentLanguage)
            {
                case "zh-CN":
                    MessageBox.Show("配置已保存。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    break;
                case "zh-TW":
                    MessageBox.Show("配置已保存。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    break;
                case "en-US":
                    MessageBox.Show("Configuration Saved.", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    break;
            }
        }

        private void ERROR_APPLICATION_NO_PREMISSION(string directoryPath, Exception error)
        {
            if (Parameters.currentLanguage == null)
            {
                var currentCulture = CultureInfo.CurrentUICulture;

                var supportedLanguages = new HashSet<string>
                {
                    "zh-CN", // 中文 (简体)
                    "zh-TW", // 中文 (繁体)
                    "en-US", // 英语 (美国)
                };

                if (supportedLanguages.Contains(currentCulture.Name))
                {
                    Parameters.currentLanguage = currentCulture.Name;
                }

                else
                {
                    Parameters.currentLanguage = "en-US";
                }
            }

            switch (Parameters.currentLanguage)
            {
                case "zh-CN":
                    MessageBox.Show($"应用程序没有在{directoryPath}内进行读写的权限，错误为: {error.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
                case "zh-TW":
                    MessageBox.Show($"應用程式沒有在{directoryPath}内進行讀寫的權限，錯誤為: {error.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
                case "en-US":
                    MessageBox.Show($"The application does not have read/write permissions in {directoryPath}, the error is: {error.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
            }

            string message = $"应用程序没有在{directoryPath}内进行读写的权限";
            WRITE_ERROR_LOG(message, error);
        }

        private void ERROR_CREATE_COMPONENTS_FAILED(Exception error)
        {
            switch (Parameters.currentLanguage)
            {
                case "zh-CN":
                    MessageBox.Show($"创建依赖组件时出错，错误为: {error.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
                case "zh-TW":
                    MessageBox.Show($"創建依賴組件時出錯，錯誤為: {error.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
                case "en-US":
                    MessageBox.Show($"The error occurred while creating the depended components,error message is: {error.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
            }

            string message = $"创建依赖组件时出错";
            WRITE_ERROR_LOG(message, error);
        }

        private void ERROR_RESOURCE_EXIST(string resourceName)
        {
            switch (Parameters.currentLanguage)
            {
                case "zh-CN":
                    MessageBox.Show("没有找到资源: " + resourceName, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
                case "zh-TW":
                    MessageBox.Show("沒有找到資源: " + resourceName, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
                case "en-US":
                    MessageBox.Show("Resource not found: " + resourceName, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
            }

            string message = $"没有找到资源:{resourceName}";
            WRITE_MESSAGE_LOG(message);
        }

        private void ERROR_FILE_MAYBE_NOT_EXIST(string incomingPath, string targetPath)
        {
            switch (Parameters.currentLanguage)
            {
                case "zh-CN":
                    MessageBox.Show("传入路径中包含的文件可能不存在或者程序没有读写它的权限。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
                case "zh-TW":
                    MessageBox.Show("傳入路徑中包含的文件可能不存在或者程式沒有讀寫它的權限。", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
                case "en-US":
                    MessageBox.Show("The file in the specified path may not exist, or the program may not have permission to read or write to it.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
            }

            string message = $"传入文件路径为:{incomingPath},传出的目标路径为{targetPath},错误为:传入路径中包含的文件可能不存在或者程序没有读写它的权限。";
            WRITE_MESSAGE_LOG(message);
        }

        private void ERROR_TOO_LONG_PATH(string incomingPath)
        {
            switch (Parameters.currentLanguage)
            {
                case "zh-CN":
                    MessageBox.Show("传入路径超过260个字符，无法处理。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
                case "zh-TW":
                    MessageBox.Show("傳入路徑超過260個字符，無法處理。", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
                case "en-US":
                    MessageBox.Show("The path exceeds 260 characters and cannot be processed.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
            }

            string message = $"传入文件路径为:{incomingPath},错误为:传入路径超过260个字符，无法处理。";
            WRITE_MESSAGE_LOG(message);
        }

        private void ERROR_EMPTY_SIZE(string name, string incomingPath)
        {
            switch (Parameters.currentLanguage)
            {
                case "zh-CN":
                    MessageBox.Show($"没有获取到{name}的大小。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
                case "zh-TW":
                    MessageBox.Show($"沒有獲取到{name}的大小。", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
                case "en-US":
                    MessageBox.Show($"The size of {name} could not be obtained.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
            }

            string message = $"没有获取到{name}的大小，传入文件路径为{incomingPath}。";
            WRITE_MESSAGE_LOG(message);
        }

        private void WARNING_REMOVE_ELEMENT(string name, string incomingPath)
        {
            switch (Parameters.currentLanguage)
            {
                case "zh-CN":
                    MessageBox.Show($"没有获取到{name}的大小，所以该文件并没有被添加到压缩文件中。", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    break;
                case "zh-TW":
                    MessageBox.Show($"沒有獲取到{name}的大小，所以該文件并沒有被添加到壓縮檔中。", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    break;
                case "en-US":
                    MessageBox.Show($"The size of {name} could not be obtained, so it has not been added to the compressed file.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    break;
            }

            string message = $"传入文件路径为:{incomingPath},错误为:没有获取到{name}的大小，所以该文件并没有被添加到压缩文件中。";
            WRITE_MESSAGE_LOG(message);
        }

        private void WRANING_FILE_IN_USE(string name, Exception error)
        {
            switch (Parameters.currentLanguage)
            {
                case "zh-CN":
                    MessageBox.Show($"{name}被其他进程占用，所有该文件没有被添加到压缩任务中，错误为: {error.Message}", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    break;
                case "zh-TW":
                    MessageBox.Show($"{name}被其他進程占用，所以該文件沒有被添加到壓縮任務中，錯誤為: {error.Message}", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    break;
                case "en-US":
                    MessageBox.Show($"The file {name} is being used by another process, and therefore it has not been added to the compression task. The error is: {error.Message}", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    break;
            }

            string message = $"{name}被其他进程占用，所有该文件没有被添加到压缩任务中，错误为: {error.Message}";
            WRITE_ERROR_LOG(message, error);
        }

        private void ERROR_CREATE_FOLDER_FAILED(Exception error)
        {
            switch (Parameters.currentLanguage)
            {
                case "zh-CN":
                    MessageBox.Show($"创建目标文件夹时出错，错误为: {error.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
                case "zh-TW":
                    MessageBox.Show($"創建目標文件夾時出錯，錯誤為: {error.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
                case "en-US":
                    MessageBox.Show($"The error occurred while creating the target folder,error message is: {error.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
            }

            string message = $"创建目标文件夹时出错";
            WRITE_ERROR_LOG(message, error);
        }

        private void ERROR_FILE_IN_USE(Exception error)
        {
            switch (Parameters.currentLanguage)
            {
                case "zh-CN":
                    MessageBox.Show($"选中文件中存在被其他进程占用的文件，压缩任务中止，错误为: {error.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
                case "zh-TW":
                    MessageBox.Show($"選中文件中存在被其他進程占用的文件，壓縮任務中止，錯誤為: {error.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
                case "en-US":
                    MessageBox.Show($"The selected file is currently in use by another process, and the compression task has been aborted. The error is: {error.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
            }

            string message = $"选中文件中存在被其他进程占用的文件，压缩任务中止";
            WRITE_ERROR_LOG(message, error);
        }

        private void ERROR_EXCEPTION_MESSAGE(Exception error)
        {
            switch (Parameters.currentLanguage)
            {
                case "zh-CN":
                    MessageBox.Show($"出现错误: {error.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
                case "zh-TW":
                    MessageBox.Show($"出現錯誤: {error.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
                case "en-US":
                    MessageBox.Show($"An error occurred: {error.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
            }

            string message = $"出现错误";
            WRITE_ERROR_LOG(message, error);
        }
        #endregion

        #region Config Save Functions
        private void SAVE_CONFIG()
        {
            CHECK_XML_LEGAL(Parameters.xmlPath);

            int width = Screen.FromControl(this).Bounds.Width;
            int height = Screen.FromControl(this).Bounds.Height;

            UPDATE_CONFIG($"{Parameters.xmlPath}", "Language", $"{Parameters.currentLanguage}");
            UPDATE_CONFIG($"{Parameters.xmlPath}", "ScreenWidth", $"{width}");
            UPDATE_CONFIG($"{Parameters.xmlPath}", "ScreenHeight", $"{height}");
            UPDATE_CONFIG($"{Parameters.xmlPath}", "SystemScale", $"{Parameters.systemScale}");
            UPDATE_CONFIG($"{Parameters.xmlPath}", "Volume", $"{Parameters.volume}");
            UPDATE_CONFIG($"{Parameters.xmlPath}", "Format", $"{Parameters.format}");
            UPDATE_CONFIG($"{Parameters.xmlPath}", "Password", $"{Parameters.password}");
            UPDATE_CONFIG($"{Parameters.xmlPath}", "Zstd", $"{Parameters.zstd}");
            UPDATE_CONFIG($"{Parameters.xmlPath}", "DisableVolume", $"{Parameters.disableVolume}");
            UPDATE_CONFIG($"{Parameters.xmlPath}", "GenerateMD5", $"{Parameters.generateMd5}");
            UPDATE_CONFIG($"{Parameters.xmlPath}", "AutoSave", $"{Parameters.autoSave}");
            UPDATE_CONFIG($"{Parameters.xmlPath}", "Portable", $"{Parameters.portable}");
            UPDATE_CONFIG($"{Parameters.xmlPath}", "MaxLevelCompress", $"{Parameters.maxLevelCompress}");
            UPDATE_CONFIG($"{Parameters.xmlPath}", "SevenZUsageCount", $"{Parameters.sevenZUsageCount}");
        }

        private void SAVE_LOCATION()
        {
            CHECK_XML_LEGAL(Parameters.xmlPath);

            int locationX = Location.X;
            int locationY = Location.Y;

            UPDATE_CONFIG($"{Parameters.xmlPath}", "LocationX", $"{locationX}");
            UPDATE_CONFIG($"{Parameters.xmlPath}", "LocationY", $"{locationY}");
        }

        private void ADD_SEVENZ_USAGE_COUNT()
        {
            CHECK_XML_LEGAL(Parameters.xmlPath);
            Parameters.sevenZUsageCount++;
            UPDATE_CONFIG($"{Parameters.xmlPath}", "SevenZUsageCount", $"{Parameters.sevenZUsageCount}");
        }
        #endregion

        #region Initialize UI Behavior
        private void LANGUAGE_MENU_SELECT_zhCN_CLICK(object sender, EventArgs e)
        {
            Parameters.currentLanguage = "zh-CN";
            UpdateLanguage();
            UPDATE_CONFIG($"{Parameters.xmlPath}", "Language", $"{Parameters.currentLanguage}");
        }

        private void LANGUAGE_MENU_SELECT_zhTW_CLICK(object sender, EventArgs e)
        {
            Parameters.currentLanguage = "zh-TW";
            UpdateLanguage();
            UPDATE_CONFIG($"{Parameters.xmlPath}", "Language", $"{Parameters.currentLanguage}");
        }

        private void LANGUAGE_MENU_SELECT_enUS_CLICK(object sender, EventArgs e)
        {
            Parameters.currentLanguage = "en-US";
            UpdateLanguage();
            UPDATE_CONFIG($"{Parameters.xmlPath}", "Language", $"{Parameters.currentLanguage}");
        }

        private void BUTTON_CONFIG_CLICK(object sender, EventArgs e)
        {
            NOTICE_CONFIG_SAVED();
            SAVE_CONFIG();
            SAVE_LOCATION();
        }

        private void OPTION_MENU_DISABLE_VOLUME_CLICK(object sender, EventArgs e)
        {
            ToolStripMenuItem disableVolume = sender as ToolStripMenuItem;

            if (disableVolume != null)
            {
                disableVolume.Checked = !disableVolume.Checked;
            }
        }

        private void OPTION_MENU_GENERATE_MD5_CLICK(object sender, EventArgs e)
        {
            ToolStripMenuItem generateMd5 = sender as ToolStripMenuItem;

            if (generateMd5 != null)
            {
                generateMd5.Checked = !generateMd5.Checked;
            }
        }

        private void ABOUT_AUTO7Z_REV_CLICK(object sender, EventArgs e)
        {
            AboutForm aboutForm = new AboutForm();
            aboutForm.StartPosition = FormStartPosition.CenterParent;
            aboutForm.ShowDialog();
        }

        private void OPTION_MENU_DISABLE_VOLUME_CHECKED_CHANGED(object sender, EventArgs e)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var checkedIconName = "Auto7z_Rev.Resources.icon.Checked.png";
            var unCheckedIconName = "Auto7z_Rev.Resources.icon.Null.png";

            bool isDisable = OptionMenuDisableVolume.Checked;

            if (isDisable)
            {
                using (Stream stream = assembly.GetManifestResourceStream(checkedIconName))
                {
                    if (stream != null)
                    {
                        OptionMenuDisableVolume.Image = Image.FromStream(stream);
                    }
                    else
                    {
                        ERROR_RESOURCE_EXIST(checkedIconName);
                    }
                }

                Parameters.disableVolume = true;
                TextBoxSize.Enabled = false;
            }

            else
            {
                using (Stream stream = assembly.GetManifestResourceStream(unCheckedIconName))
                {
                    if (stream != null)
                    {
                        OptionMenuDisableVolume.Image = Image.FromStream(stream);
                    }
                    else
                    {
                        ERROR_RESOURCE_EXIST(unCheckedIconName);
                    }
                }

                Parameters.disableVolume = false;
                TextBoxSize.Enabled = true;
            }
        }

        private void OPTION_MENU_GENERATE_MD5_CHECKED_CHANGED(object sender, EventArgs e)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var checkedIconName = "Auto7z_Rev.Resources.icon.Checked.png";
            var unCheckedIconName = "Auto7z_Rev.Resources.icon.Null.png";

            bool isCreateMd5 = OptionMenuGenerateMD5.Checked;

            if (isCreateMd5)
            {
                using (Stream stream = assembly.GetManifestResourceStream(checkedIconName))
                {
                    if (stream != null)
                    {
                        OptionMenuGenerateMD5.Image = Image.FromStream(stream);
                    }
                    else
                    {
                        ERROR_RESOURCE_EXIST(checkedIconName);
                    }
                }

                Parameters.generateMd5 = true;
            }

            else
            {
                using (Stream stream = assembly.GetManifestResourceStream(unCheckedIconName))
                {
                    if (stream != null)
                    {
                        OptionMenuGenerateMD5.Image = Image.FromStream(stream);
                    }
                    else
                    {
                        ERROR_RESOURCE_EXIST(unCheckedIconName);
                    }
                }

                Parameters.generateMd5 = false;
            }
        }

        private void TEXTBOX_SIZE_TEXT_CHANGED(object sender, EventArgs e)
        {
            string newSize = TextBoxSize.Text;
            Parameters.volume = newSize;
        }

        private void COMBOBOX_FORMAT_SELECTED_INDEX_CHANGED(object sender, EventArgs e)
        {
            string selectedFormat = ComboBoxFormat.SelectedItem.ToString();
            Parameters.format = selectedFormat;

            if (Parameters.format == "tar")
            {
                TextBoxPassword.Enabled = false;
                CheckBoxZstd.Visible = true;

                if (Parameters.zstd)
                {
                    CheckBoxZstd.Checked = true;
                }
            }

            else
            {
                CheckBoxZstd.Visible = false;
                TextBoxPassword.Enabled = true;
            }
        }

        private void CHECKBOX_ZSTD_CHECKED_CHANGED(object sender, EventArgs e)
        {
            bool isZstd = CheckBoxZstd.Checked;

            if (isZstd)
            {
                Parameters.zstd = true;
            }

            else
            {
                Parameters.zstd = false;
            }
        }

        private void CHECKBOX_AUTOSAVE_CHECKED_CHANGED(object sender, EventArgs e)
        {
            bool isAutoSave = CheckBoxAutoSave.Checked;
            if (isAutoSave)
            {
                Parameters.autoSave = true;
            }

            else
            {
                Parameters.autoSave = false;
            }
        }

        private void TEXTBOX_PASSWORD_TEXT_CHANGED(object sender, EventArgs e)
        {
            string newPassword = TextBoxPassword.Text;
            Parameters.password = newPassword;
        }

        private void TEXTBOX_SIZE_KEYPRESS(object sender, KeyPressEventArgs e)
        {
            // 检查输入的字符是否是数字或控制字符（如退格键）
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
            {
                e.Handled = true; // 如果不是，则拦截该字符
            }
        }
        #endregion

        #region Initialize MainForm Behavior
        private void AUTO7Z_MAINFORM_LOAD(object sender, EventArgs e)
        {
            const float epsilon = 0.00001f; // 定义一个浮点值的误差范围
            int locationX;
            int locationY;

            if (Parameters.newScreenWidth == Screen.FromControl(this).Bounds.Width && Parameters.newScreenHeight == Screen.FromControl(this).Bounds.Height && Math.Abs(Parameters.newSystemScale - Parameters.systemScale) < epsilon)
            {
                locationX = GET_LOCATION_X(Parameters.xmlPath);
                locationY = GET_LOCATION_Y(Parameters.xmlPath);

                Location = new Point(locationX, locationY);
            }

            else
            {
                locationX = Screen.FromControl(this).Bounds.Width / 2 - Size.Width / 2;
                locationY = Screen.FromControl(this).Bounds.Height / 2 - Size.Height / 2;

                Location = new Point(locationX, locationY);
            }

            INITIALIZE_MAINFORM_SIZE(Parameters.systemScale);
        }

        private void AUTO7Z_MAINFORM_SHOWN(object sender, EventArgs e)
        {
            Parameters.isFormLoaded = true;
        }

        private void AUTO7Z_MAINFORM_FORM_CLOSING(object sender, FormClosingEventArgs e)
        {
            DELETE_EXTRACT_RESOURCE();

            if (!CheckBoxAutoSave.Checked)
            {
                UPDATE_CONFIG($"{Parameters.xmlPath}", "AutoSave", "False");
            }

            else
            {
                SAVE_CONFIG();
            }

            if (Parameters.isFormLoaded)
            {
                SAVE_LOCATION();
            }
        }

        private void MAINFORM_DRAGENTER(object sender, DragEventArgs e)
        {
            // 检测数据是否可以处理
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy; // 设置拖拽效果为复制
            }
            else
            {
                e.Effect = DragDropEffects.None; // 不支持的拖拽效果
            }
        }

        private async void MAINFORM_DRAGDROP(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            await Task.Run(() => MAIN_TASK(files));
            PIN_MAINFORM();
            Cursor.Current = Cursors.Default;
        }

        private void MAINFORM_DRAGLEAVE(object sender, EventArgs e)
        {
            Cursor.Current = Cursors.Default; // 设置鼠标指针为默认状态
        }

        private void PIN_MAINFORM()
        {
            TopMost = true;
            TopMost = false;
        }
        #endregion
    }
}
