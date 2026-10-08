using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MD5Calculator
{
    public partial class ProgressBarForm : Form
    {
        // 4MB 缓冲足够喂饱 MD5，再大只是白占内存
        private const int BUFFER_SIZE = 4 * 1024 * 1024;

        // UI 刷新间隔，100ms 人眼已经察觉不到
        private const int UI_REFRESH_MS = 100;

        private static readonly char[] HexLower = "0123456789abcdef".ToCharArray();

        // 大文件读取缓冲，同一实例内复用，避免每次重新分配大对象
        private byte[] _fileBuffer;

        private Dictionary<string, Dictionary<string, string>> languageTexts;
        public readonly static string workPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        public readonly static string appConfigPath = Path.Combine(workPath, "MD5Calculator.exe.config");

        private readonly string currentLanguage;
        private readonly string[] newArgs;
        private readonly int numOfFiles = 0;

        private bool isMultipleFiles = false;
        private bool packedOneFile = false;
        private float systemScale;

        public class LogSetting
        {
            private readonly string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            private readonly string logFileName = "MD5Calculator.log";
            public readonly string logFilePath;

            public LogSetting()
            {
                logFilePath = Path.Combine(desktopPath, logFileName);
            }
        }

        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            base.OnDpiChanged(e);

            // 获取新的 DPI 缩放因子
            float newScale = e.DeviceDpiNew / 96.0f;
            systemScale = newScale;

            INITIALIZE_MAINFORM_SIZE(newScale);

            // 强制重绘界面以适应新 DPI 下的字体和控件
            Invalidate();
            Update();
        }

        public ProgressBarForm(string[] args)
        {
            newArgs = args;
            InitializeComponent();

            currentLanguage = GET_CURRENT_LANGUAGE();
            systemScale = GET_SCALE();
            INITIALIZE_MAINFORM_SIZE(systemScale);

            InitializeLanguageTexts();
            UpdateLanguage();
            UpdateLabelText();

            if (!CHECK_MD5CALCULATOR_EXIST())
            {
                CREATE_COMPONENTS(out Exception ex);
                if (ex != null)
                {
                    ERROR_CREATE_COMPONENTS_FAILED(ex);
                    Close();
                }
            }

            if (newArgs.Length != 0)
            {
                if (newArgs.Length > 1)
                {
                    isMultipleFiles = true;
                    bool allPathAreFiles = true;

                    foreach (string path in newArgs)
                    {
                        numOfFiles += 1;
                        if (!File.Exists(path))
                        {
                            allPathAreFiles = false;
                            break;
                        }
                    }

                    if (allPathAreFiles)
                    {
                        QUESTION_PACKED_IN_ONE_FILE();
                    }
                }
                else
                {
                    isMultipleFiles = false;
                }
            }

            if (newArgs != null && newArgs.Length != 0)
            {
                Show();
            }
            else
            {
                ERROR_NO_FILE();
                Close();
            }
        }

        private float GET_SCALE()
        {
            float dpi;
            using (Graphics g = CreateGraphics())
            {
                dpi = g.DpiX;
            }

            return dpi / 96.0f;
        }

        private async void PROGRESS_BAR_FORM_SHOWN(object sender, EventArgs e)
        {
            if (newArgs != null && newArgs.Length > 0)
            {
                await ASYNC_TASK(newArgs);
            }
        }

        private async Task ASYNC_TASK(string[] args)
        {
            string nowTime = DateTime.Now.ToString("HH-mm-ss");

            bool allValid = args != null && args.Length > 0;
            if (allValid)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    if (string.IsNullOrEmpty(args[i]))
                    {
                        allValid = false;
                        break;
                    }
                }
            }

            if (!allValid)
            {
                Close();
                return;
            }

            if (!isMultipleFiles)
            {
                string filePath = Path.GetFullPath(args[0]);
                bool isFile = File.Exists(filePath);

                long sizeBytes = GET_SINGLE_FILE_SIZE(filePath);
                if (sizeBytes < 0) { Close(); return; }

                string[] targets = isFile ? new[] { filePath } : GET_DIRECTORY_FILES(filePath);

                string md5FileName = isFile
                    ? $"{Path.GetFileNameWithoutExtension(filePath)}.md5"
                    : $"{Path.GetFileName(filePath)}.md5";
                string md5Path = isFile
                    ? Path.Combine(Path.GetDirectoryName(filePath), md5FileName)
                    : Path.Combine(filePath, md5FileName);

                Exception ex = null;
                string[] md5 = await Task.Run(() => CALCULATE_MD5(sizeBytes, targets, out ex));

                if (md5 == null)
                {
                    if (ex != null) ERROR_EXCEPTION_MESSAGE(ex);
                    Close();
                    return;
                }

                WRITE_MD5_WITH_FALLBACK(md5Path, md5, filePath, isFile, nowTime);
            }
            else if (!packedOneFile)
            {
                foreach (string file in args)
                {
                    string filePath = Path.GetFullPath(file);
                    bool isFile = File.Exists(filePath);

                    long sizeBytes = GET_SINGLE_FILE_SIZE(filePath);
                    if (sizeBytes < 0) continue;

                    string[] targets = isFile ? new[] { filePath } : GET_DIRECTORY_FILES(filePath);

                    string md5FileName = isFile
                        ? $"{Path.GetFileNameWithoutExtension(filePath)}.md5"
                        : $"{Path.GetFileName(filePath)}.md5";
                    string md5Path = isFile
                        ? Path.Combine(Path.GetDirectoryName(filePath), md5FileName)
                        : Path.Combine(filePath, md5FileName);

                    Exception ex = null;
                    string[] md5 = await Task.Run(() => CALCULATE_MD5(sizeBytes, targets, out ex));

                    if (md5 == null)
                    {
                        if (ex != null) ERROR_EXCEPTION_MESSAGE(ex);
                        Close();
                        return;
                    }

                    WRITE_MD5_WITH_FALLBACK(md5Path, md5, filePath, isFile, nowTime);
                }
            }
            else
            {
                string filePath = Path.GetFullPath(args[0]);
                bool isFile = File.Exists(filePath);

                long sizeBytes = GET_MULTIPLE_FILES_SIZE(args, out string[] newFilePaths);
                if (sizeBytes < 0) { Close(); return; }

                string tag = $"{Path.GetFileNameWithoutExtension(filePath)}等{numOfFiles}个文件";
                string md5Path = isFile
                    ? Path.Combine(Path.GetDirectoryName(filePath), $"{tag}.md5")
                    : Path.Combine(filePath, $"{tag}.md5");

                List<string> md5 = new List<string>();

                foreach (string file in newFilePaths)
                {
                    bool itemIsFile = File.Exists(file);
                    string[] targets = itemIsFile ? new[] { file } : GET_DIRECTORY_FILES(file);

                    Exception ex = null;
                    string[] value = await Task.Run(() => CALCULATE_MD5(sizeBytes, targets, out ex));

                    if (ex != null)
                    {
                        ERROR_EXCEPTION_MESSAGE(ex);
                        Close();
                        return;
                    }

                    if (value != null) md5.AddRange(value);
                }

                WRITE_MD5_WITH_FALLBACK(md5Path, md5.ToArray(), filePath, isFile, nowTime, tag);
            }

            Close();
        }

        // 目标 md5 已存在时，改写到带时间戳的子目录，不覆盖原文件
        private void WRITE_MD5_WITH_FALLBACK(string md5Path, string[] md5, string srcPath, bool isFile, string nowTime, string tag = null)
        {
            CHECK_FILE_LEGAL(md5Path);

            if (!File.Exists(md5Path))
            {
                WRITE_MD5(md5Path, md5);
                return;
            }

            string baseName = Path.GetFileNameWithoutExtension(srcPath);
            string folderName = tag == null
                ? $"新生成的文件名为{baseName}的MD5文件_{nowTime}"
                : $"新生成的文件名为{tag}的MD5文件_{nowTime}";

            string newFolder = isFile
                ? Path.Combine(Path.GetDirectoryName(srcPath), folderName)
                : Path.Combine(srcPath, folderName);

            if (Directory.Exists(newFolder))
            {
                CHECK_FOLDER_LEGAL(newFolder);
            }
            else
            {
                CREATE_FOLDER(newFolder);
            }

            string targetName = tag == null ? $"{baseName}.md5" : $"{tag}.md5";
            WRITE_MD5(Path.Combine(newFolder, targetName), md5);
        }

        private bool CHECK_MD5CALCULATOR_EXIST()
        {
            return File.Exists(appConfigPath);
        }

        private void CREATE_COMPONENTS(out Exception error)
        {
            error = null;

            try
            {
                if (!File.Exists(appConfigPath))
                {
                    CREATE_APP_CONFIG();
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

        private void CREATE_APP_CONFIG()
        {
            string resourceName = "MD5Calculator.Resources.MD5Calculator.exe.config";
            string outputFileName = resourceName.Replace("MD5Calculator.Resources.", "");
            string outputPath = Path.Combine(workPath, outputFileName);
            EXTRACT_RESOURCE(resourceName, outputPath);
        }

        private void INITIALIZE_MAINFORM_SIZE(float scale)
        {
            // 设置自动缩放模式
            AutoScaleMode = AutoScaleMode.Dpi;

            MinimumSize = new Size(0, 0);
            MaximumSize = MinimumSize;
            UPDATE_MIN_MAX_SIZE(scale);

            float baseWidth = 300f;
            float baseHeight = 150f;
            Size = new Size((int)(baseWidth * scale), (int)(baseHeight * scale));

            INITIALIZE_TABLE_LAYOUT_PANEL_PIXEL();
            INITIALIZE_UI_FONT_SIZE();
        }

        private void UPDATE_MIN_MAX_SIZE(float scale)
        {
            int width = (int)(300 * scale);
            int height = (int)(150 * scale);
            MinimumSize = new Size(width, height);
            MaximumSize = MinimumSize;
        }

        private void INITIALIZE_TABLE_LAYOUT_PANEL_PIXEL()
        {
            // tableLayoutPanel 的索引从 0 开始，cell 是先列后行

            // 对列的定义
            SET_COLUMN_SIZE(tableLayoutPanel, 0, SizeType.Percent, 5f);
            SET_COLUMN_SIZE(tableLayoutPanel, 1, SizeType.Percent, 30f);
            SET_COLUMN_SIZE(tableLayoutPanel, 2, SizeType.Percent, 30f);
            SET_COLUMN_SIZE(tableLayoutPanel, 3, SizeType.Percent, 30f);
            SET_COLUMN_SIZE(tableLayoutPanel, 4, SizeType.Percent, 5f);

            // 对行的定义
            SET_ROW_SIZE(tableLayoutPanel, 0, SizeType.Percent, 20f);
            SET_ROW_SIZE(tableLayoutPanel, 1, SizeType.Percent, 20f);
            SET_ROW_SIZE(tableLayoutPanel, 2, SizeType.Percent, 20f);
            SET_ROW_SIZE(tableLayoutPanel, 3, SizeType.Percent, 20f);
            SET_ROW_SIZE(tableLayoutPanel, 4, SizeType.Percent, 20f);
        }

        private void SET_COLUMN_SIZE(TableLayoutPanel panel, int num, SizeType type, float fontSize)
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
            SET_FONT_SIZE(LabelCalculating, Font.Size);
            SET_FONT_SIZE(LabelPercent, Font.Size);
        }

        private void SET_FONT_SIZE(Control obj, float fontSize)
        {
            obj.Font = new Font(obj.Font.FontFamily, fontSize, obj.Font.Style, GraphicsUnit.Point);
        }

        private void InitializeLanguageTexts()
        {
            languageTexts = new Dictionary<string, Dictionary<string, string>>
            {
                {
                    "zh-CN",
                    new Dictionary<string, string>
                    {
                        { "Title", "计算MD5..." },
                        { "LabelCalculating", "正在计算MD5..." }
                    }
                },
                {
                    "zh-TW",
                    new Dictionary<string, string>
                    {
                        { "Title", "計算MD5..." },
                        { "LabelCalculating", "正在計算MD5..." }
                    }
                },
                {
                    "en-US",
                    new Dictionary<string, string>
                    {
                        { "Title", "Calculate MD5..." },
                        { "LabelCalculating", "Calculating MD5..." }
                    }
                }
            };
        }

        private void UpdateLanguage()
        {
            Text = languageTexts[currentLanguage]["Title"];
            LabelCalculating.Text = languageTexts[currentLanguage]["LabelCalculating"];
        }

        private void UpdateLabelText()
        {
            string text = null;
            switch (currentLanguage)
            {
                case "zh-CN":
                    text = "已完成：0%";
                    break;
                case "zh-TW":
                    text = "已完成：0%";
                    break;
                case "en-US":
                    text = "Processed: 0%";
                    break;
            }
            LabelPercent.Text = text;
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

        private void CREATE_FOLDER(string newFolderPath)
        {
            if (!Directory.Exists(newFolderPath))
            {
                Directory.CreateDirectory(newFolderPath);
            }
        }

        private void PROGRESSBAR_FORM_LOAD(object sender, EventArgs e)
        {
            int locationX = Screen.PrimaryScreen.Bounds.Width / 2 - Width / 2;
            int locationY = Screen.PrimaryScreen.Bounds.Height / 2 - Height / 2;
            Location = new Point(locationX, locationY);
            Size = MinimumSize;
        }

        private string[] GET_DIRECTORY_FILES(string path)
        {
            return Directory.GetFiles(path);
        }

        private string[] CALCULATE_MD5(long totalSize, string[] paths, out Exception error)
        {
            List<string> data = new List<string>();
            long processedTotalSize = 0;

            string labelText;
            switch (currentLanguage)
            {
                case "zh-CN":
                case "zh-TW":
                    labelText = "已完成：";
                    break;
                default:
                    labelText = "Processed: ";
                    break;
            }

            error = null;

            if (paths == null || paths.Length == 0)
            {
                return data.ToArray();
            }

            // 缓冲只分配一次，同一 Form 内复用
            if (_fileBuffer == null)
            {
                _fileBuffer = new byte[BUFFER_SIZE];
            }
            byte[] buffer = _fileBuffer;

            DateTime lastUi = DateTime.MinValue;
            int lastPercent = -1;

            foreach (string path in paths)
            {
                try
                {
                    string fullPath = Path.GetFullPath(path);
                    string valueName = Path.GetFileName(fullPath);

                    using (var md5 = MD5.Create())
                    using (var stream = new FileStream(
                        fullPath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read,
                        4096,
                        FileOptions.SequentialScan))
                    {
                        int bytesRead;
                        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            md5.TransformBlock(buffer, 0, bytesRead, buffer, 0);
                            processedTotalSize += bytesRead;

                            // 按时间节流，别用 Thread.Sleep 硬卡
                            var now = DateTime.UtcNow;
                            if ((now - lastUi).TotalMilliseconds < UI_REFRESH_MS) continue;
                            lastUi = now;

                            int percent = totalSize > 0
                                ? (int)(processedTotalSize * 100 / totalSize)
                                : 100;
                            if (percent > 100) percent = 100;
                            if (percent == lastPercent) continue;
                            lastPercent = percent;

                            int snapshot = percent;
                            BeginInvoke((MethodInvoker)delegate
                            {
                                ProgressBar.Value = snapshot;
                                LabelPercent.Text = $"{labelText}{snapshot}%";
                            });
                        }

                        md5.TransformFinalBlock(buffer, 0, 0);

                        string hex = ToHexLower(md5.Hash);
                        data.Add($"{hex} *{valueName}");
                    }
                }
                catch (Exception ex)
                {
                    error = ex;
                    BeginInvoke((MethodInvoker)delegate
                    {
                        ProgressBar.Value = 0;
                        LabelPercent.Text = labelText + "0%";
                    });
                    return null;
                }
            }

            string[] result = data.ToArray();

            BeginInvoke((MethodInvoker)delegate
            {
                ProgressBar.Value = 100;
                LabelPercent.Text = labelText + "100%";
            });

            return result;
        }

        // 查表拼十六进制小写串，比 BitConverter.ToString 那套少分配两次
        private static string ToHexLower(byte[] bytes)
        {
            char[] chars = new char[bytes.Length * 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                byte b = bytes[i];
                chars[i * 2] = HexLower[b >> 4];
                chars[i * 2 + 1] = HexLower[b & 0x0F];
            }
            return new string(chars);
        }

        private long GET_SINGLE_FILE_SIZE(string path)
        {
            string fullPath = Path.GetFullPath(path);
            string name = Path.GetFileNameWithoutExtension(fullPath);
            string directory = Path.GetDirectoryName(fullPath);

            long fileSize;
            long folderSize;

            if (File.Exists(fullPath))
            {
                fileSize = GET_FILE_SIZE(fullPath);

                if (fileSize == -1)
                {
                    ERROR_EMPTY_SIZE(name, fullPath);
                    return -1;
                }

                return fileSize;
            }
            else if (Directory.Exists(fullPath))
            {
                folderSize = GET_FOLDER_SIZE(fullPath);

                if (folderSize == -1)
                {
                    ERROR_EMPTY_SIZE(name, fullPath);
                    return -1;
                }

                return folderSize;
            }
            else
            {
                bool canReadWrite = CHECK_PATH_READ_WRITE(directory, out Exception ex);

                if (fullPath.Length >= 260)
                {
                    ERROR_TOO_LONG_PATH(fullPath);
                }

                if (!canReadWrite)
                {
                    ERROR_APPLICATION_NO_PREMISSION(directory, ex);
                }

                return -1;
            }
        }

        private long GET_MULTIPLE_FILES_SIZE(string[] paths, out string[] newFilePaths)
        {
            List<string> fileList = new List<string>(paths);

            long fileSizes = 0;
            long folderSizes = 0;

            foreach (string path in paths)
            {
                string fullPath = Path.GetFullPath(path);
                string name = Path.GetFileNameWithoutExtension(fullPath);
                string directory = Path.GetDirectoryName(fullPath);

                if (File.Exists(fullPath))
                {
                    long fileSize = GET_FILE_SIZE(fullPath);

                    if (fileSize != -1)
                    {
                        fileSizes += fileSize;
                    }
                    else
                    {
                        fileList.Remove(fullPath);
                        WARNING_REMOVE_ELEMENT(name, fullPath);
                    }
                }
                else if (Directory.Exists(fullPath))
                {
                    long folderSize = GET_FOLDER_SIZE(fullPath);

                    if (folderSize != -1)
                    {
                        folderSizes += folderSize;
                    }
                    else
                    {
                        fileList.Remove(fullPath);
                        WARNING_REMOVE_ELEMENT(name, fullPath);
                    }
                }
                else
                {
                    bool canReadWrite = CHECK_PATH_READ_WRITE(directory, out Exception ex);

                    if (fullPath.Length >= 260)
                    {
                        ERROR_TOO_LONG_PATH(fullPath);
                    }

                    if (!canReadWrite)
                    {
                        ERROR_APPLICATION_NO_PREMISSION(directory, ex);
                    }
                }
            }

            newFilePaths = fileList.ToArray();
            return fileSizes + folderSizes;
        }

        private long GET_FILE_SIZE(string filePath)
        {
            if (filePath != null)
            {
                FileInfo fileInfo = new FileInfo(filePath);
                return fileInfo.Length;
            }

            return -1;
        }

        private long GET_FOLDER_SIZE(string filePath)
        {
            if (filePath == null) return -1;

            long folderSizeBytes = 0;

            // EnumerateFiles 是流式的，不会把整个目录一次性展开成数组
            foreach (string file in Directory.EnumerateFiles(filePath, "*", SearchOption.AllDirectories))
            {
                try
                {
                    folderSizeBytes += new FileInfo(file).Length;
                }
                catch
                {
                    // 个别文件读不到就跳过，别为它断了整个统计
                }
            }

            return folderSizeBytes;
        }

        private void WRITE_MD5(string md5Path, string[] message)
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(md5Path, true, Encoding.GetEncoding("GBK")))
                {
                    foreach (string md5 in message)
                    {
                        writer.WriteLine(md5);
                    }
                }
            }
            catch (Exception error)
            {
                ERROR_EXCEPTION_MESSAGE(error);
            }
        }

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
            FileInfo fileInfo = new FileInfo(filePath);

            if (fileInfo.Exists && (fileInfo.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
            {
                fileInfo.Attributes = FileAttributes.Normal;
            }
            if (fileInfo.Exists && (fileInfo.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
            {
                fileInfo.Attributes = FileAttributes.Normal;
            }
        }

        private void WRITE_ERROR_LOG(string message, Exception error)
        {
            LogSetting logSetting = new LogSetting();

            string nowTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            CHECK_FILE_LEGAL(logSetting.logFilePath);

            using (StreamWriter writer = new StreamWriter(logSetting.logFilePath, true))
            {
                writer.WriteLine($"{nowTime}: {message},{error.Message}");
                writer.WriteLine();
            }
        }

        private void WRITE_MESSAGE_LOG(string message)
        {
            LogSetting logSetting = new LogSetting();

            string nowTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            CHECK_FILE_LEGAL(logSetting.logFilePath);

            using (StreamWriter writer = new StreamWriter(logSetting.logFilePath, true))
            {
                writer.WriteLine($"{nowTime}: {message}");
                writer.WriteLine();
            }
        }

        private void QUESTION_PACKED_IN_ONE_FILE()
        {
            DialogResult dialogResult = DialogResult.None;

            switch (currentLanguage)
            {
                case "zh-CN":
                    dialogResult = MessageBox.Show("检测到多个文件传入，是否需要将多个文件的MD5值打包到单个MD5文件中？", "提示", MessageBoxButtons.YesNo, MessageBoxIcon.Asterisk);
                    break;
                case "zh-TW":
                    dialogResult = MessageBox.Show("檢測到多個文件傳入，是否需要將多個文件的MD5值打包到單個MD5文件中？", "提示", MessageBoxButtons.YesNo, MessageBoxIcon.Asterisk);
                    break;
                case "en-US":
                    dialogResult = MessageBox.Show("Multiple files have been detected. Do you want to package the values into a single MD5 file?", "Notice", MessageBoxButtons.YesNo, MessageBoxIcon.Asterisk);
                    break;
            }

            packedOneFile = dialogResult == DialogResult.Yes;
        }

        private void WARNING_REMOVE_ELEMENT(string name, string incomingPath)
        {
            switch (currentLanguage)
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

            WRITE_MESSAGE_LOG($"传入文件路径为:{incomingPath},错误为:没有获取到{name}的大小，所以该文件并没有被添加到压缩文件中。");
        }

        private void ERROR_APPLICATION_NO_PREMISSION(string directoryPath, Exception error)
        {
            switch (currentLanguage)
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

            WRITE_ERROR_LOG($"应用程序没有在{directoryPath}内进行读写的权限", error);
        }

        private void ERROR_TOO_LONG_PATH(string incomingPath)
        {
            switch (currentLanguage)
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

            WRITE_MESSAGE_LOG($"传入文件路径为:{incomingPath},错误为:传入路径超过260个字符，无法处理。");
        }

        private void ERROR_NO_FILE()
        {
            switch (currentLanguage)
            {
                case "zh-CN":
                    MessageBox.Show("没有传入任何文件，任务终止。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
                case "zh-TW":
                    MessageBox.Show("沒有傳入任何文件，任務終止。", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
                case "en-US":
                    MessageBox.Show("No file uploaded, task terminated.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    break;
            }
        }

        private void ERROR_EMPTY_SIZE(string name, string incomingPath)
        {
            switch (currentLanguage)
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

            WRITE_MESSAGE_LOG($"没有获取到{name}的大小，传入的路径为{incomingPath}。");
        }

        private void ERROR_EXCEPTION_MESSAGE(Exception error)
        {
            switch (currentLanguage)
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

            WRITE_ERROR_LOG("出现错误", error);
        }

        private void ERROR_CREATE_COMPONENTS_FAILED(Exception error)
        {
            switch (currentLanguage)
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

            WRITE_ERROR_LOG("创建依赖组件时出错", error);
        }

        private void ERROR_RESOURCE_EXIST(string resourceName)
        {
            switch (currentLanguage)
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

            WRITE_MESSAGE_LOG($"没有找到资源:{resourceName}");
        }

        private string GET_CURRENT_LANGUAGE()
        {
            var currentCulture = CultureInfo.CurrentUICulture;

            var supportedLanguages = new HashSet<string>
            {
                "zh-CN",
                "zh-TW",
                "en-US",
            };

            if (supportedLanguages.Contains(currentCulture.Name))
            {
                return currentCulture.Name;
            }

            return "en-US";
        }
    }
}