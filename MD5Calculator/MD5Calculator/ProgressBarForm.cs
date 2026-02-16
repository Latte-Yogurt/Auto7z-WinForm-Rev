using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MD5Calculator
{
    public partial class ProgressBarForm : Form
    {
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

			if (args != null && args.Length > 0 && args.ToList().TrueForAll(arg => !string.IsNullOrEmpty(arg)))
			{
				if (!isMultipleFiles)
				{
					string filePath = Path.GetFullPath(args[0]);
					string[] fileInDirectoryPaths = null;
					long sizeBytes = GET_SINGLE_FILE_SIZE(filePath);

					string md5FileName = File.Exists(filePath) ? $"{Path.GetFileNameWithoutExtension(filePath)}.md5" : $"{Path.GetFileName(filePath)}.md5";
					string md5Path = File.Exists(filePath) ? Path.Combine(Path.GetDirectoryName(filePath), md5FileName) : Path.Combine(filePath, md5FileName);

					if (Directory.Exists(filePath))
					{
						fileInDirectoryPaths = GET_DIRECTORY_FILES(filePath);
					}

					Exception fileEx = null;
					Exception folderEx = null;

					string[] md5 = File.Exists(filePath) ? await Task.Run(() => CALCULATE_MD5(sizeBytes, new string[] { filePath }, out fileEx)) : await Task.Run(() => CALCULATE_MD5(sizeBytes, fileInDirectoryPaths, out folderEx));

					if (md5 == null)
					{
						Exception error = (File.Exists(filePath) ? fileEx : folderEx);
						ERROR_EXCEPTION_MESSAGE(error);
						return;
					}

					CHECK_FILE_LEGAL(md5Path);

					if (!File.Exists(md5Path))
					{
						WRITE_MD5(md5Path, md5);
					}

					else
					{
						string newFolder = File.Exists(filePath) ? Path.Combine(Path.GetDirectoryName(filePath), $"新生成的文件名为{Path.GetFileNameWithoutExtension(filePath)}的MD5文件_{nowTime}") : Path.Combine(filePath, $"新生成的文件名为{Path.GetFileNameWithoutExtension(filePath)}的MD5文件_{nowTime}");

						if (Directory.Exists(newFolder))
						{
							CHECK_FOLDER_LEGAL(newFolder);
						}

						else
						{
							CREATE_FOLDER(newFolder);
						}

						string newMd5Path = Path.Combine(newFolder, $"{Path.GetFileNameWithoutExtension(filePath)}.md5");
						WRITE_MD5(newMd5Path, md5);
					}
				}

				else
				{
					string[] filePaths = args;

					if (!packedOneFile)
					{
						foreach (string file in filePaths)
						{
							string filePath = Path.GetFullPath(file);
							string[] fileInDirectoryPaths = null;
							long sizeBytes = GET_SINGLE_FILE_SIZE(filePath);

							string md5FileName = File.Exists(filePath) ? $"{Path.GetFileNameWithoutExtension(filePath)}.md5" : $"{Path.GetFileName(filePath)}.md5";
							string md5Path = File.Exists(filePath) ? Path.Combine(Path.GetDirectoryName(filePath), md5FileName) : Path.Combine(filePath, md5FileName);

							if (Directory.Exists(filePath))
							{
								fileInDirectoryPaths = GET_DIRECTORY_FILES(filePath);
							}

							Exception fileEx = null;
							Exception folderEx = null;

							string[] md5 = File.Exists(filePath) ? await Task.Run(() => CALCULATE_MD5(sizeBytes, new string[] { filePath }, out fileEx)) : await Task.Run(() => CALCULATE_MD5(sizeBytes, fileInDirectoryPaths, out folderEx));

							if (md5 == null)
							{
								Exception error = File.Exists(filePath) ? fileEx : folderEx;
								ERROR_EXCEPTION_MESSAGE(error);
								return;
							}

							CHECK_FILE_LEGAL(md5Path);

							if (!File.Exists(md5Path))
							{
								WRITE_MD5(md5Path, md5);
							}

							else
							{
								string newFolder = File.Exists(filePath) ? Path.Combine(Path.GetDirectoryName(filePath), $"新生成的文件名为{Path.GetFileNameWithoutExtension(filePath)}的MD5文件_{nowTime}") : Path.Combine(filePath, $"新生成的文件名为{Path.GetFileNameWithoutExtension(filePath)}的MD5文件_{nowTime}");

								if (Directory.Exists(newFolder))
								{
									CHECK_FOLDER_LEGAL(newFolder);
								}

								else
								{
									CREATE_FOLDER(newFolder);
								}

								string newMd5Path = Path.Combine(newFolder, $"{Path.GetFileNameWithoutExtension(filePath)}.md5");
								WRITE_MD5(newMd5Path, md5);
							}
						}
					}

					else
					{
						string filePath = Path.GetFullPath(args[0]);
						string[] fileInDirectoryPaths = null;
						long sizeBytes = GET_MULTIPLE_FILES_SIZE(filePaths, out string[] newFilePaths);

						string md5FileName = $"{Path.GetFileNameWithoutExtension(filePath)}等{numOfFiles}个文件.md5";
						string md5Path = File.Exists(filePath) ? Path.Combine(Path.GetDirectoryName(filePath), md5FileName) : Path.Combine(filePath, md5FileName);

						List<string> md5 = new List<string>();

						foreach (var file in newFilePaths)
						{
							if (Directory.Exists(file))
							{
								fileInDirectoryPaths = GET_DIRECTORY_FILES(file);
							}

							Exception fileEx = null;
							Exception folderEx = null;

							string[] value;

							if (File.Exists(file))
							{
								value = await Task.Run(() => CALCULATE_MD5(sizeBytes, new string[] { file }, out fileEx));
							}

							else
							{
								value = await Task.Run(() => CALCULATE_MD5(sizeBytes, fileInDirectoryPaths, out folderEx));
							}

							if (fileEx != null)
							{
								ERROR_EXCEPTION_MESSAGE(fileEx);
								return;
							}

							if (folderEx != null)
							{
								ERROR_EXCEPTION_MESSAGE(folderEx);
								return;
							}

							if (value != null)
							{
								md5.AddRange(value);
							}
						}

						CHECK_FILE_LEGAL(md5Path);

						if (!File.Exists(md5Path))
						{
							WRITE_MD5(md5Path, md5.ToArray());
						}

						else
						{
							string newFolder = File.Exists(filePath) ? Path.Combine(Path.GetDirectoryName(filePath), $"新生成的文件名为{Path.GetFileNameWithoutExtension(filePath)}等{numOfFiles}个文件的MD5文件_{nowTime}") : Path.Combine(filePath, $"新生成的文件名为{Path.GetFileNameWithoutExtension(filePath)}等{numOfFiles}个文件的MD5文件_{nowTime}");

							if (Directory.Exists(newFolder))
							{
								CHECK_FOLDER_LEGAL(newFolder);
							}

							else
							{
								CREATE_FOLDER(newFolder);
							}

							string newMd5Path = Path.Combine(newFolder, $"{Path.GetFileNameWithoutExtension(filePath)}等{numOfFiles}个文件.md5");
							WRITE_MD5(newMd5Path, md5.ToArray());
						}
					}
				}

				Close();
			}

			else
			{
				Close();
			}
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
			// tableLayoutPanel的索引从0开始，即第一行或列的索引号为0
			// tableLayoutPanel的cell指定为先列后行，例如cell=0,1即为第一列第二行

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

        private void SET_FONT_SIZE(Control obj, float fontSize)// 使用dynamic或Control绕过编译时的类型检查，直到运行时才解析
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
			long processedTotalSize = 0; // 记录所有文件已处理的总大小

			// 定义块大小为 100MB
			const int CHUNK_SIZE = 100 * 1024 * 1024;

			string labelText = null;
			switch (currentLanguage)
			{
				case "zh-CN":
				case "zh-TW":
					labelText = "已完成：";
					break;
				case "en-US":
					labelText = "Processed: ";
					break;
				default: // 默认值
					labelText = "Processed: ";
					break;
			}

			error = null; // 初始化错误为 null

			foreach (string path in paths)
			{
				try
				{
					string fullPath = Path.GetFullPath(path);
					string valueName = Path.GetFileName(fullPath);
					long currentFileLength = new FileInfo(fullPath).Length; // 当前文件的大小
					long currentFileProcessedSize = 0; // 当前文件已处理的大小

					using (var md5 = MD5.Create())
					{
						using (var stream = File.OpenRead(fullPath))
						{
							byte[] buffer = new byte[CHUNK_SIZE]; // 缓冲区
							int bytesRead;

							while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
							{
								md5.TransformBlock(buffer, 0, bytesRead, buffer, 0);

								currentFileProcessedSize += bytesRead;
								processedTotalSize += bytesRead; // 累加到总处理大小

								int percent = (int)(processedTotalSize * 100 / totalSize);
								Invoke((MethodInvoker)delegate
								{
									ProgressBar.Value = percent;
									LabelPercent.Text = $"{labelText}{percent}%";
								});

								// 更新太频繁可能导致 UI 闪烁或卡顿，加入10毫秒的缓冲时间
								System.Threading.Thread.Sleep(10);
							}

							// TransformFinalBlock 会计算最终的哈希值
							md5.TransformFinalBlock(buffer, 0, 0); // 0, 0 表示所有数据都已通过 TransformBlock处理，现在完成计算

							byte[] hashBytes = md5.Hash; // 获取最终的哈希值
							string originValue = $"{BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant()} *{valueName}";
							data.Add(originValue);
						}
					}
				}
				catch (Exception ex)
				{
					error = ex;
					// 如果发生错误，返回空数组，并跳出循环
					Invoke((MethodInvoker)delegate
					{
						ProgressBar.Value = 0; // 错误时重置进度条
						LabelPercent.Text = labelText + "0%";
					});
					return new string[0];
				}
			}

			string[] result = data.ToArray();

			// 所有文件处理完毕后，确保进度条显示 100%
			Invoke((MethodInvoker)delegate
			{
				ProgressBar.Value = 100;
				LabelPercent.Text = labelText + "100%";
			});

			return result;
		}

		private long GET_SINGLE_FILE_SIZE(string path)
		{
			string fullPath = Path.GetFullPath(path);
			string name = Path.GetFileNameWithoutExtension(fullPath);
			string dirertory = Path.GetDirectoryName(fullPath);

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

			else if (!File.Exists(fullPath) || !Directory.Exists(fullPath))
			{
				bool canReadWrite = CHECK_PATH_READ_WRITE(dirertory, out Exception ex);

				if (fullPath.Length >= 260)
				{
					ERROR_TOO_LONG_PATH(fullPath);
				}

				if (!canReadWrite)
				{
					ERROR_APPLICATION_NO_PREMISSION(dirertory, ex);
				}

				return -1;
			}

			return -1;
		}

		private long GET_MULTIPLE_FILES_SIZE(string[] paths,out string[] newFilePaths)
		{
			List<string> fileList = new List<string>(paths);

			long fileSize;
			long fileSizes = 0;
			long folderSize;
			long folderSizes = 0;
			long totalSize;

			foreach (var path in paths)
			{
				string fullPath = Path.GetFullPath(path);
				string name = Path.GetFileNameWithoutExtension(fullPath);
				string dirertory = Path.GetDirectoryName(fullPath);

				if (File.Exists(fullPath))
				{
					fileSize = GET_FILE_SIZE(fullPath);

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
					folderSize = GET_FOLDER_SIZE(fullPath);

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

				else if (!File.Exists(fullPath) || !Directory.Exists(fullPath))
				{
					bool canReadWrite = CHECK_PATH_READ_WRITE(dirertory, out Exception ex);

					if (fullPath.Length >= 260)
					{
						ERROR_TOO_LONG_PATH(fullPath);
					}

					if (!canReadWrite)
					{
						ERROR_APPLICATION_NO_PREMISSION(dirertory, ex);
					}
				}
			}

			string[] filePaths = fileList.ToArray();
			newFilePaths = filePaths;

			if (filePaths != null)
			{
				totalSize = fileSizes + folderSizes;
				return totalSize;
			}

			return -1;
		}

		private long GET_FILE_SIZE(string filePath)
		{
			if (filePath != null)
			{
				FileInfo fileInfo = new FileInfo(filePath);
				long fileSizeBytes = fileInfo.Length;

				return fileSizeBytes;
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

				return folderSizeBytes;
			}

			else
			{
				return -1;
			}
		}

		private void WRITE_MD5(string md5Path, string[] message)
		{
			try
			{
				foreach (var md5 in message)
				{
					using (StreamWriter writer = new StreamWriter(md5Path, true, Encoding.GetEncoding("GBK")))
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
			error = null; // 初始化异常为 null

			try
			{
				// 检查可写性
				string checkFilePath = Path.Combine(path, "Directory_checker");

				// 尝试写入
				using (FileStream testFile = File.Create(checkFilePath))
				{
					// 写入一些数据（随意）
					byte[] info = new UTF8Encoding(true).GetBytes("dir check");
					testFile.Write(info, 0, info.Length);
				}

				// 尝试读取
				using (FileStream testFile = File.OpenRead(checkFilePath))
				{
					// 尝试读取数据
					byte[] buffer = new byte[1024];
					testFile.Read(buffer, 0, buffer.Length);
				}

				// 删除测试文件
				File.Delete(checkFilePath);

				return true; // 两者都成功
			}
			catch (UnauthorizedAccessException unauthorizedEx)
			{
				error = unauthorizedEx;
				return false; // 不具备权限
			}
			catch (Exception otherEx)
			{
				error = otherEx;
				return false; // 发生其他异常
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

			if (dialogResult == DialogResult.Yes)
			{
				packedOneFile = true;
			}

			else
			{
				packedOneFile = false;
			}
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

			string message = $"传入文件路径为:{incomingPath},错误为:没有获取到{name}的大小，所以该文件并没有被添加到压缩文件中。";
			WRITE_MESSAGE_LOG(message);
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

			string message = $"应用程序没有在{directoryPath}内进行读写的权限";
			WRITE_ERROR_LOG(message, error);
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

			string message = $"传入文件路径为:{incomingPath},错误为:传入路径超过260个字符，无法处理。";
			WRITE_MESSAGE_LOG(message);
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

			string message = $"没有获取到{name}的大小，传入的路径为{incomingPath}。";
			WRITE_MESSAGE_LOG(message);
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

			string message = $"出现错误";
			WRITE_ERROR_LOG(message, error);
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

			string message = $"创建依赖组件时出错";
			WRITE_ERROR_LOG(message, error);
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

			string message = $"没有找到资源:{resourceName}";
			WRITE_MESSAGE_LOG(message);
		}


		private string GET_CURRENT_LANGUAGE()
		{
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

			if (supportedLanguages.Contains(currentCulture.Name))
			{
				return currentCulture.Name;
			}

			else
			{
				return "en-US";
			}
		}
	}
}
