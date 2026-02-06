using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace Auto7z_Rev
{
    public partial class AboutForm : Form
    {
        private Dictionary<string, Dictionary<string, string>> languageTexts;

        public static class Parameters
        {
            public static string currentLanguage { get; set; }
            public static float systemScale { get; set; }
        }

        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            base.OnDpiChanged(e);

            // 获取新的 DPI 缩放因子
            float newScale = e.DeviceDpiNew / 96.0f;
            INITIALIZE_MAINFORM_SIZE(newScale);

            // 强制重绘界面以适应新 DPI 下的字体和控件
            Invalidate();
            Update();
        }

        public AboutForm()
        {
            InitializeComponent();
            Parameters.currentLanguage = Auto7zMainForm.Parameters.currentLanguage;
            Parameters.systemScale = GET_SCALE();
            InitializeLanguageTexts();
            UpdateLanguage();
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

        private void InitializeLanguageTexts()
        {
            languageTexts = new Dictionary<string, Dictionary<string, string>>
            {
                { "zh-CN", new Dictionary<string, string>
                    {
                        { "Title","关于 Auto7z Rev" },
                        { "LinkLabelGitHub","Github主页" },
                        { "LinkLabelLicense","许可证" },
                        { "ButtonConfirm","确定" },
                    }
                },
                { "zh-TW", new Dictionary<string, string>
                    {
                        { "Title","關於 Auto7z Rev" },
                        { "LinkLabelGitHub","Github主頁" },
                        { "LinkLabelLicense","許可證" },
                        { "ButtonConfirm","確定" },
                    }
                },
                { "en-US", new Dictionary<string, string>
                    {
                        { "Title","About Auto7z Rev" },
                        { "LinkLabelGitHub","Github Page" },
                        { "LinkLabelLicense","License" },
                        { "ButtonConfirm","Confirm" },
                    }
                }
            };
        }

        private void UpdateLanguage()
        {
            LinkLabelGitHub.Text = languageTexts[Parameters.currentLanguage]["LinkLabelGitHub"];
            LinkLabelLicense.Text = languageTexts[Parameters.currentLanguage]["LinkLabelLicense"];
            ButtonConfirm.Text = languageTexts[Parameters.currentLanguage]["ButtonConfirm"];

            if (GET_VERSION() != "N/A")
            {
                Text = languageTexts[Parameters.currentLanguage]["Title"] + " " + GET_VERSION();
            }

            else
            {
                Text = languageTexts[Parameters.currentLanguage]["Title"];
            }
        }

        private string GET_VERSION()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            AssemblyFileVersionAttribute fileVersionAttribute = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>();

            if (fileVersionAttribute != null)
            {
                string fileVersion = fileVersionAttribute.Version;

                if (Version.TryParse(fileVersion, out Version versionObject))
                {
                    return "v" + versionObject.ToString(3);
                }
            }

            // 如果无法获取文件版本属性或解析失败，返回一个默认值或空字符串
            return "N/A";
        }

        private void GITHUB_LABEL_LINK_CLICKED(object sender, LinkLabelLinkClickedEventArgs e)
        {
            // 打开网址
            System.Diagnostics.Process.Start("https://github.com/Latte-Yogurt");

            // 标记为已访问
            LinkLabelGitHub.LinkVisited = true;
        }

        private void LICENSE_LABEL_LINK_CLICKED(object sender, LinkLabelLinkClickedEventArgs e)
        {
            // 打开网址
            System.Diagnostics.Process.Start("https://www.gnu.org/licenses/gpl-3.0.html");

            // 标记为已访问
            LinkLabelLicense.LinkVisited = true;
        }

        private void CONFIRM_BUTTON_CLICK(object sender, EventArgs e)
        {
            Close();
        }

        private void COPYRIGHT_REFRESH()
        {
            int startYear = 2024;
            int currentYear = DateTime.Now.Year;
            string copyrightText;

            if (currentYear == startYear)
            {
                copyrightText = $"Copyright© {startYear} LatteYogurt , All rights reserved.";
            }

            else
            {
                copyrightText = $"Copyright© {startYear}-{currentYear} LatteYogurt , All rights reserved.";
            }

            LabelCopyRight.Text = copyrightText;
        }

        private void ABOUT_FORM_LOAD(object sender, EventArgs e)
        {
            int locationX = Screen.FromControl(this).Bounds.Width / 2 - Width / 2;
            int locationY = Screen.FromControl(this).Bounds.Height / 2 - Height / 2;

            Location = new Point(locationX, locationY);
            INITIALIZE_MAINFORM_SIZE(Parameters.systemScale);
            COPYRIGHT_REFRESH();
        }

        private void INITIALIZE_MAINFORM_SIZE(float scale)
        {
            // 设置自动缩放模式
            AutoScaleMode = AutoScaleMode.Dpi;

            MinimumSize = new Size(0, 0);
            MaximumSize = MinimumSize;
            UPDATE_MIN_MAX_SIZE(scale);

            float baseWidth = 510f;
            float baseHeight = 280f;
            Size = new Size((int)(baseWidth * scale), (int)(baseHeight * scale));

            INITIALIZE_TABLE_LAYOUT_PANEL_PIXEL();
            INITIALIZE_PICTUREBOX_SIZE();
            INITIALIZE_UI_FONT_SIZE();
        }

        private void UPDATE_MIN_MAX_SIZE(float scale)
        {
            int width = (int)(300 * scale);
            int height = (int)(280 * scale);
            MinimumSize = new Size(width, height);
            MaximumSize = new Size((int)(width * 1.7), height);
        }

        private void INITIALIZE_TABLE_LAYOUT_PANEL_PIXEL()
        {
            // 索引从0开始，即第一行或列的索引号为0

            // 对列的定义
            SET_COLUMN_SIZE(tableLayoutPanel, 0, SizeType.Percent, 20f);
            SET_COLUMN_SIZE(tableLayoutPanel, 1, SizeType.Percent, 20f);
            SET_COLUMN_SIZE(tableLayoutPanel, 2, SizeType.Percent, 20f);
            SET_COLUMN_SIZE(tableLayoutPanel, 3, SizeType.Percent, 20f);
            SET_COLUMN_SIZE(tableLayoutPanel, 4, SizeType.Percent, 20f);

            // 对行的定义
            SET_ROW_SIZE(tableLayoutPanel, 0, SizeType.Absolute, 15f);
            SET_ROW_SIZE(tableLayoutPanel, 1, SizeType.Percent, 17f);
            SET_ROW_SIZE(tableLayoutPanel, 2, SizeType.Percent, 16f);
            SET_ROW_SIZE(tableLayoutPanel, 3, SizeType.Percent, 17f);
            SET_ROW_SIZE(tableLayoutPanel, 4, SizeType.Percent, 17f);
            SET_ROW_SIZE(tableLayoutPanel, 5, SizeType.Percent, 16f);
            SET_ROW_SIZE(tableLayoutPanel, 6, SizeType.Percent, 17f);
            SET_ROW_SIZE(tableLayoutPanel, 7, SizeType.Absolute, 15f);
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

        private void INITIALIZE_PICTUREBOX_SIZE()
        {
            SET_PICTUREBOX_SIZE(PicBox, Size.Width, Size.Height, Parameters.systemScale);
        }

        private void SET_PICTUREBOX_SIZE(PictureBox pb, float baseWidth, float baseHeight, float scale)
        {
            if (pb == null)
            {
                return;
            }

            pb.SizeMode = PictureBoxSizeMode.Zoom;

            int newWidth = (int)(baseWidth * scale);
            int newHeight = (int)(baseHeight * scale);

            pb.Size = new Size(newWidth, newHeight);
        }

        private void INITIALIZE_UI_FONT_SIZE()
        {
            SET_FONT_SIZE(MainLabel, Font.Size * 2f);
            SET_FONT_SIZE(LinkLabelGitHub, Font.Size * 1.5f);
            SET_FONT_SIZE(LinkLabelLicense, Font.Size * 1.5f);
            SET_FONT_SIZE(LabelCopyRight, Font.Size * 0.8f);
            SET_FONT_SIZE(ButtonConfirm, Font.Size);
        }

        private void SET_FONT_SIZE(Control obj, float fontSize)// 使用dynamic或Control绕过编译时的类型检查，直到运行时才解析
        {
            obj.Font = new Font(obj.Font.FontFamily, fontSize, obj.Font.Style, GraphicsUnit.Point);
        }
    }
}
