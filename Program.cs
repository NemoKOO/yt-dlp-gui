#region Namespace Dependencies
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using System.Security.Permissions;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
#endregion /* Namespace Dependencies */

#region Assembly Information
// 程序集的相關信息，修改以下特性値以更新程序集的關聯信息。
[assembly: AssemblyProduct("yt-dlp-gui")]
[assembly: AssemblyCopyright("MIT License")]
[assembly: AssemblyTitle("yt-dlp Portable Lightweight GUI (Recommended for Windows 10/11 x64)")]
[assembly: AssemblyDescription("https://github.com/linblank/yt-dlp-gui-for-beginners")]
// 程序集的版本信息
[assembly: AssemblyFileVersion("0.6.1")]
[assembly: AssemblyInformationalVersion("Release")]
[assembly: AssemblyVersion("0.6.*")]
#endregion /* Assembly Information */

namespace YtDlpGuiMvp
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Version windows = Environment.OSVersion.Version;
            if (IsWindows7OrOlder(Environment.OSVersion.Platform, windows))
            {
                 MessageBox.Show("此公開版使用的 yt-dlp 官方程式已不支援 Windows 7，因此無法透過安裝或複製 python310.dll 來修復。\n\n請在 Windows 10/11 x64 上使用。若必須繼續使用 Windows 7，需要自行維護和測試專用的舊版元件組合；本程式不會在 Win7 上自動下載不相容的元件。",
                                 "Windows 7 不受支援", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Application.Run(new MainForm());
        }

        internal static bool IsWindows7OrOlder(PlatformID platform, Version windows)
        {
            return platform == PlatformID.Win32NT &&
                   (windows.Major < 6 || (windows.Major == 6 && windows.Minor < 2));
        }
    }

    internal sealed class ReplaceOnPasteTextBox : TextBox
    {
        private const int WmPaste = 0x0302;

        internal void SelectExistingTextForPaste()
        {
            if (TextLength > 0) SelectAll();
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == WmPaste) SelectExistingTextForPaste();
            base.WndProc(ref message);
        }
    }

    internal sealed class MainForm : Form
    {
        private         volatile bool                   cancelling;
        private                  bool                   douyinCookieError;
        private                  bool                   isDouyinDownload;
        private         volatile bool                   noSubtitles;
        private         volatile Process                currentProcess;
        private                  int                    selectedMode;
        private                  string                 lastErrorMessage;
        private                  string                 lastFolder;

        private         readonly Regex                  percentPattern          = new Regex(@"\[download\]\s+(\d+(?:\.\d+)?)%", RegexOptions.Compiled);
        private static  readonly Regex                  urlPattern              = new Regex("https?://[^\\s<>\\[\\]\\(\\)（）“”\\\"'，。；！？、]+",
                                                                                            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private         readonly SynchronizationContext ui;

        private         readonly Button                 btnBrowse               = new Button();
        private         readonly Button                 btnCancel               = new Button();
        private         readonly Button                 btnDownload             = new Button();
        private         readonly Button                 btnOpenDownloadFolder   = new Button();
        private         readonly Button                 btnUpdateComponents     = new Button();
        private         readonly Button[]               btnSelectMode           = { new Button(), new Button(), new Button() };
        private         readonly CheckBox               chkFirefox              = new CheckBox();
        private         readonly CheckBox               chkPlaylist             = new CheckBox();
        private         readonly ComboBox               cmbSubtitleLanguage     = new ComboBox();
        private         readonly ComboBox               cmbVideoResolution      = new ComboBox();
        private         readonly Label                  lblStatus               = new Label();
        private         readonly Label                  lblSavePreview          = new Label();
        private         readonly Label                  lblUrlPreview           = new Label();
        private         readonly ProgressBar            progress                = new ProgressBar();
        private         readonly ReplaceOnPasteTextBox  txtUrl                  = new ReplaceOnPasteTextBox();
        private         readonly TextBox                txtLog                  = new TextBox();
        private         readonly TextBox                txtSavePath             = new TextBox();
        private         readonly ToolTip                ttpTips                 = new ToolTip();

        private sealed class SubtitleSnapshot
        {
            public readonly HashSet<string> Vtt = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public readonly HashSet<string> Srt = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        public MainForm(bool checkComponentsOnShown = true)
        {
            var version     = Assembly.GetExecutingAssembly().GetName().Version;

            AutoScaleMode   = System.Windows.Forms.AutoScaleMode.None/* System.Windows.Forms.AutoScaleMode.Font */;
            BackColor       = Color.FromArgb(237, 242, 248);
            Font            = new Font("Microsoft YaHei UI", 10F);
            Icon            = SystemIcons.Application;
            MinimumSize     = new Size(760, 700);
            Padding         = new Padding(14);
            Size            = new Size(960, 800);
            StartPosition   = FormStartPosition.CenterScreen;
            Text            = Process.GetCurrentProcess().ProcessName + " · MVP v" +
                              FileVersionInfo.GetVersionInfo(this.GetType().Assembly.Location).FileVersion +
                              "(" + Application.ProductVersion + " Revison: " + version.Revision + ", " +
                              " Build: " + version.Build + ")";
            ui              = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

            BuildUi();
            if (checkComponentsOnShown) Shown += (s, e) => CheckComponentsOnStartup();
        }

        private void BuildUi()
        {
            var root                                        = new TableLayoutPanel();
            root.Dock                                       = DockStyle.Fill;
            root.Padding                                    = new Padding(28, 20, 28, 20);
            root.BackColor                                  = Color.White;
            root.ColumnCount                                = 1;
            root.RowCount                                   = 16;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 29));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 43));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 29));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 43));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 31));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 29));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);

            var title                                       = new Label();
            title.Dock                                      = DockStyle.Fill;
            title.Font                                      = new Font("Microsoft YaHei UI", 20F, FontStyle.Bold);
            title.ForeColor                                 = Color.FromArgb(27, 43, 67);
            title.Text                                      = "影片/音訊/字幕下載";
            root.Controls.Add(title, 0, 0);

            var subtitle                                    = MakeLabel("複製分享文案或影片網址，選擇格式，然後點擊開始下載。");
            subtitle.ForeColor                              = Color.FromArgb(101, 116, 139);
            root.Controls.Add(subtitle, 0, 1);
            root.Controls.Add(MakeLabel("分享文字或影片網址（貼上新內容會自動取代舊內容）"), 0, 2);

            txtUrl.BackColor                                = Color.FromArgb(250, 252, 255);
            txtUrl.BorderStyle                              = BorderStyle.FixedSingle;
            txtUrl.Dock                                     = DockStyle.Fill;
            txtUrl.Font                                     = new Font("Microsoft YaHei UI", 10.5F);
            txtUrl.Multiline                                = true;
            txtUrl.ScrollBars                               = ScrollBars.Vertical;
            txtUrl.TextChanged                             += (s, e) => txtUrl_EventHandler_TextChanged_UpdatePreview();
            root.Controls.Add(txtUrl, 0, 3);

            lblUrlPreview.AutoEllipsis                      = true;
            lblUrlPreview.Dock                              = DockStyle.Fill;
            lblUrlPreview.ForeColor                         = Color.FromArgb(29, 111, 131);
            lblUrlPreview.TextAlign                         = ContentAlignment.MiddleLeft;
            root.Controls.Add(lblUrlPreview, 0, 4);

            var optionRow                                   = new FlowLayoutPanel();
            optionRow.Dock                                  = DockStyle.Fill;
            optionRow.FlowDirection                         = FlowDirection.LeftToRight;
            optionRow.WrapContents                          = false;
            var downloadTypeLabel                           = MakeLabel("下載類型");
            downloadTypeLabel.Dock                          = DockStyle.None;
            downloadTypeLabel.Margin                        = new Padding(0, 0, 0, 0);
            downloadTypeLabel.Size                          = new Size(98, 40);
            optionRow.Controls.Add(downloadTypeLabel);
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                btnSelectMode[i].Click                     += (s, e) => btnSelectMode_EventHandler_Click_SelectMode(index);
                btnSelectMode[i].Cursor                     = Cursors.Hand;
                btnSelectMode[i].Dock                       = DockStyle.None;
                btnSelectMode[i].Font                       = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
                btnSelectMode[i].FlatStyle                  = FlatStyle.Flat;
                btnSelectMode[i].FlatAppearance.BorderSize  = 1;
                btnSelectMode[i].Margin                     = new Padding(0, 2, 5, 0);
                btnSelectMode[i].Size                       = new Size(112, 36);
                btnSelectMode[i].Text                       = new[] { "影片 MP4", "音訊 MP3", "字幕 SRT" }[i];
                btnSelectMode[i].TextAlign                  = ContentAlignment.MiddleCenter;
                btnSelectMode[i].UseVisualStyleBackColor    = false;
                optionRow.Controls.Add(btnSelectMode[i]);
            }
            var languageLabel                               = MakeLabel("字幕語言");
            languageLabel.Dock                              = DockStyle.None;
            languageLabel.Margin                            = new Padding(9, 0, 0, 0);
            languageLabel.Size                              = new Size(77, 40);
            optionRow.Controls.Add(languageLabel);

            cmbSubtitleLanguage.Dock                        = DockStyle.None;
            cmbSubtitleLanguage.DropDownStyle               = ComboBoxStyle.DropDownList;
            cmbSubtitleLanguage.FlatStyle                   = FlatStyle.Standard;
            cmbSubtitleLanguage.Items.AddRange(new object[] { "簡體中文", "正體中文", "英文" });
            cmbSubtitleLanguage.Margin                      = new Padding(0, 5, 0, 0);
            cmbSubtitleLanguage.SelectedIndex               = 0;
            cmbSubtitleLanguage.Width                       = 133;
            optionRow.Controls.Add(cmbSubtitleLanguage);
            root.Controls.Add(optionRow, 0, 5);

            var resolutionRow                               = new FlowLayoutPanel();
            resolutionRow.Dock                              = DockStyle.Fill;
            resolutionRow.WrapContents                      = false;
            var resolutionLabel                             = MakeLabel("影片目標解析度");
            resolutionLabel.Dock                            = DockStyle.None;
            resolutionLabel.Size                            = new Size(143, 38);
            resolutionRow.Controls.Add(resolutionLabel);
            cmbVideoResolution.DropDownStyle                = ComboBoxStyle.DropDownList;
            cmbVideoResolution.FlatStyle                    = FlatStyle.Standard;
            cmbVideoResolution.Items.AddRange(new object[] {"自動（原有選擇）",
                                                            " 360p 優先",
                                                            " 480p 優先",
                                                            " 720p 優先",
                                                            "1080p 優先",
                                                            "1440p 優先",
                                                            "2160p 優先"});
            cmbVideoResolution.Margin                       = new Padding(0, 5, 12, 0);
            cmbVideoResolution.SelectedIndex                = 0;
            cmbVideoResolution.Width                        = 190;
            resolutionRow.Controls.Add(cmbVideoResolution);
            var resolutionHint                              = MakeLabel("按片源實際畵質選擇，不會放大畵面");
            resolutionHint.Dock                             = DockStyle.None;
            resolutionHint.ForeColor                        = Color.FromArgb(101, 116, 139);
            resolutionHint.Size                             = new Size(310, 38);
            resolutionRow.Controls.Add(resolutionHint);
            root.Controls.Add(resolutionRow, 0, 6);
            ttpTips.SetToolTip(cmbVideoResolution, "優先選不高於目標的最佳畵質；如果片源只提供更高畵質，會選它提供的最低畵質。直屏影片也按較短邊判斷。");
            btnSelectMode_EventHandler_Click_SelectMode(0);
            root.Controls.Add(MakeLabel("保存根目録（自動按類型分類）"), 0, 7);

            var folderRow                                   = new TableLayoutPanel();
            folderRow.Dock                                  = DockStyle.Fill;
            folderRow.ColumnCount                           = 2;
            folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent , 150));
            folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,  40));
            txtSavePath.BackColor                           = Color.FromArgb(250, 252, 255);
            txtSavePath.BorderStyle                         = BorderStyle.FixedSingle;
            txtSavePath.Dock                                = DockStyle.Fill;
            txtSavePath.Font                                = new Font("Microsoft YaHei UI", 10.5F);
            txtSavePath.Text                                = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop), "yt-dlp");
            txtSavePath.TextChanged                        += (s, e) => txtSavePath_EventHandler_TextChanged_UpdateFolderPreview();
            folderRow.Controls.Add(txtSavePath, 0, 0);
            btnBrowse.Dock                                  = DockStyle.Fill;
            btnBrowse.Text                                  = "···";
          //btnBrowse.TextAlign                             = ContentAlignment.MiddleCenter;
            StyleButton(btnBrowse, Color.FromArgb(231, 237, 245), Color.FromArgb(36, 53, 77));
            btnBrowse.Click                                += btnBrowse_EventHandler_Click;
            folderRow.Controls.Add(btnBrowse, 1, 0);
            root.Controls.Add(folderRow, 0, 8);
            lblSavePreview.AutoEllipsis                     = true;
            lblSavePreview.Dock                             = DockStyle.Fill;
            lblSavePreview.ForeColor                        = Color.FromArgb(88, 101, 119);
            lblSavePreview.TextAlign                        = ContentAlignment.MiddleLeft;
            root.Controls.Add(lblSavePreview, 0, 9);

            var checkRow                                    = new FlowLayoutPanel();
            chkFirefox.AutoSize                             = true;
            checkRow.Dock                                   = DockStyle.Fill;
            checkRow.FlowDirection                          = FlowDirection.LeftToRight;
            chkFirefox.Margin                               = new Padding(0, 10, 24, 0);
            chkFirefox.Text                                 = "讀取 Firefox Cookies（需要驗證時）";

            chkPlaylist.AutoSize                            = true;
            chkPlaylist.Text                                = "下載播放列表";
            chkPlaylist.Margin                              = new Padding(0, 10, 0, 0);
            checkRow.Controls.Add(chkFirefox);
            checkRow.Controls.Add(chkPlaylist);
            root.Controls.Add(checkRow, 0, 10);
            ttpTips.SetToolTip(chkFirefox, "先用 Firefox 打開影片目標網址並刷新頁面，再勾選。Cookies 可能包含登録狀態。");

            var buttonRow                                   = new FlowLayoutPanel();
            buttonRow.Dock                                  = DockStyle.Fill;
            buttonRow.Controls.Add(btnDownload);
            buttonRow.Controls.Add(btnCancel);
            buttonRow.Controls.Add(btnOpenDownloadFolder);
            buttonRow.Controls.Add(btnUpdateComponents);
            btnDownload.Text                                = "開始下載";
            btnCancel.TextAlign                             = ContentAlignment.MiddleCenter;
            btnDownload.Width                               = 145;
            StyleButton(btnDownload, Color.FromArgb(32, 103, 201), Color.White);
            btnDownload.Click                              += btnDownload_EventHandler_Click;
            btnCancel.Text                                  = "取消";
            btnCancel.TextAlign                             = ContentAlignment.MiddleCenter;
            btnCancel.Width                                 = 92;
            StyleButton(btnCancel, Color.FromArgb(231, 237, 245), Color.FromArgb(36, 53, 77));
            btnCancel.Click                                += btnCancel_EventHandler_Click;
            btnCancel.Enabled                               = false;
            btnOpenDownloadFolder.Text                      = "打開保存目録";
            btnOpenDownloadFolder.TextAlign                 = ContentAlignment.MiddleCenter;
            btnOpenDownloadFolder.Width                     = 140;
            StyleButton(btnOpenDownloadFolder, Color.FromArgb(231, 237, 245), Color.FromArgb(36, 53, 77));
            btnOpenDownloadFolder.Click                    += btnOpenDownloadFolder_EventHandler_Click;
            btnUpdateComponents.Text                        = "檢查運行組件";
            btnUpdateComponents.TextAlign                   = ContentAlignment.MiddleCenter;
            btnUpdateComponents.Width                       = 155;
            StyleButton(btnUpdateComponents, Color.FromArgb(221, 241, 238), Color.FromArgb(17, 105, 98));
            btnUpdateComponents.Click                      += btnUpdateComponents_EventHandler_Click;
            ttpTips.SetToolTip(btnUpdateComponents, "檢查四箇運行組件；缺少時從官方 GitHub 下載並校驗，完整時可更新 yt-dlp。");
            root.Controls.Add(buttonRow, 0, 11);

            progress.Dock                                   = DockStyle.Fill;
            progress.Minimum                                = 0;
            progress.Maximum                                = 100;
            root.Controls.Add(progress, 0, 12);

            lblStatus.AutoEllipsis                          = true;
            lblStatus.Dock                                  = DockStyle.Fill;
            lblStatus.ForeColor                             = Color.FromArgb(70, 82, 96);
            lblStatus.Text                                  = "就緒";
            root.Controls.Add(lblStatus, 0, 13);
            root.Controls.Add(MakeLabel("運行日誌（不顯示 Cookies 内容）"), 0, 14);

            txtLog.BackColor                                = Color.FromArgb(250, 252, 255);
            txtLog.BorderStyle                              = BorderStyle.FixedSingle;
            txtLog.Dock                                     = DockStyle.Fill;
            txtLog.Font                                     = new Font("Consolas", 9F);
            txtLog.Multiline                                = true;
            txtLog.ReadOnly                                 = true;
            txtLog.ScrollBars                               = ScrollBars.Vertical;
            root.Controls.Add(txtLog, 0, 15);
            txtUrl_EventHandler_TextChanged_UpdatePreview();
            txtSavePath_EventHandler_TextChanged_UpdateFolderPreview();
        }

        private static void StyleButton(Button btn, Color background, Color foreground)
        {
            btn.BackColor                   = background;
            btn.Cursor                      = Cursors.Hand;
            btn.FlatStyle                   = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize   = 0;
            btn.ForeColor                   = foreground;
            btn.Height                      = 38;
            btn.Margin                      = new Padding(0, 4, 10, 0);
        }

        private Label MakeLabel(string value)
        {
            var label                       = new Label();
            label.ForeColor                 = Color.FromArgb(76, 92, 112);
            label.Dock                      = DockStyle.Fill;
            label.Text                      = value;
            label.TextAlign                 = ContentAlignment.MiddleLeft;

            return label;
        }

        private static string ExtractPreferredUrl(string input)
        {
            if (String.IsNullOrWhiteSpace(input)) return null;
            MatchCollection matches = urlPattern.Matches(input);
            if (0 == matches.Count) return null;
            Match match             = matches[matches.Count - 1];
            return match.Value.TrimEnd('.', ',', ';', '!', '。', '，', '；', '！', '？', '、');
        }

        private static string ModeSubfolder(int mode)
        {
            return (1 == mode) ? "mp3" : ((2 == mode) ? "subtitles" :  "mp4");
        }

        private string SelectedSubtitleLanguage()
        {
            return (1 == cmbSubtitleLanguage.SelectedIndex) ? "^zh-Hant$" : ((2 == cmbSubtitleLanguage.SelectedIndex) ? "^en$" :  "^zh-Hans$");
        }

        private int SelectedPreferredResolution()
        {
            switch (cmbVideoResolution.SelectedIndex)
            {
                case 1: return 360;
                case 2: return 480;
                case 3: return 720;
                case 4: return 1080;
                case 5: return 1440;
                case 6: return 2160;
                default: return 0;
            }
        }

        private static SubtitleSnapshot CaptureSubtitles(string folder)
        {
            var result = new SubtitleSnapshot();
            foreach (string path in Directory.GetFiles(folder, "*.vtt", SearchOption.TopDirectoryOnly)) result.Vtt.Add(path);
            foreach (string path in Directory.GetFiles(folder, "*.srt", SearchOption.TopDirectoryOnly)) result.Srt.Add(path);
            return result;
        }

        private string BuildArguments(string url, string folder, string appDir)
        {
            var args = new StringBuilder();

            args.Append("--ignore-config --newline --no-overwrites --no-post-overwrites ");
            args.Append(chkPlaylist.Checked ? "--yes-playlist ":"--no-playlist ").Append("--concurrent-fragment 12 ");
            args.Append("--ffmpeg-location ").Append(Quote(appDir)).Append(" -P ").Append(Quote(folder)).Append(' ');

            if (chkFirefox.Checked) args.Append("--cookies-from-browser firefox ");

            switch (selectedMode)
            {
                case 0:
                {
                    int preferredResolution = SelectedPreferredResolution();
                    if (preferredResolution > 0)
                    {
                        string resolution = preferredResolution.ToString(System.Globalization.CultureInfo.InvariantCulture);

                        args.Append("-t mp4 -S ").Append(Quote("res:" + resolution)).Append("-o ")
                            .Append(Quote("%(title)s [%(id)s] [%(resolution)s].%(ext)s")).Append(' ').Append(Quote(url));
                    }
                    else args.Append("-t mp4 ").Append(Quote(url));

                    break;
                }
                case 1: { args.Append("-t mp3 ").Append(Quote(url)); break; }
                default:
                {
                    args.Append("--write-subs --write-auto-subs --sub-langs ")
                        .Append(Quote(SelectedSubtitleLanguage()))
                        .Append(" --convert-subs srt --skip-download ")
                        .Append(Quote(url));
                    break;
                }
            }

            return args.ToString();
        }

        private static bool IsDouyinUrl(string url)
        {
            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri)) return false;
            string host = uri.DnsSafeHost;
            return host.Equals("douyin.com", StringComparison.OrdinalIgnoreCase) ||
                   host.EndsWith(".douyin.com", StringComparison.OrdinalIgnoreCase);
        }

        private static string Quote(string value)
        {
            var result = new StringBuilder("\"");
            int slashCount = 0;
            foreach (char ch in value)
            {
                if (ch == '\\') { slashCount++; continue; }
                if (ch == '"')
                {
                    result.Append('\\', slashCount * 2 + 1);
                    result.Append('"');
                    slashCount = 0;
                    continue;
                }
                result.Append('\\', slashCount);
                slashCount = 0;
                result.Append(ch);
            }
            result.Append('\\', slashCount * 2);
            result.Append('"');
            return result.ToString();
        }

        private static void ConfigureProcessEnvironment(ProcessStartInfo info, string appDir)
        {
            string runtimeTemp = Path.Combine(appDir, ".runtime-temp");
            Directory.CreateDirectory(runtimeTemp);
            info.EnvironmentVariables["PATH"] = appDir + ";" + info.EnvironmentVariables["PATH"];
            info.EnvironmentVariables["TEMP"] = runtimeTemp;
            info.EnvironmentVariables["TMP"] = runtimeTemp;
        }

        private void RunDownload(string ytDlp, string args, string appDir, string folder, bool subtitleMode,
                                 SubtitleSnapshot subtitleBefore, bool usedFirefoxCookies)
        {
            try
            {
                var info                    = new ProcessStartInfo(ytDlp, args);
                info.WorkingDirectory       = appDir;
                info.UseShellExecute        = false;
                info.CreateNoWindow         = true;
                info.RedirectStandardOutput = true;
                info.RedirectStandardError  = true;
                // The Windows yt-dlp executable uses the active ANSI code page for redirected pipes.
                // On Chinese Windows this is GBK, not UTF-8.
                info.StandardOutputEncoding = Encoding.Default;
                info.StandardErrorEncoding  = Encoding.Default;
                ConfigureProcessEnvironment(info, appDir);
                using (var process = new Process())
                {
                    process.StartInfo           = info;
                    process.OutputDataReceived += (s, e) => HandleLine(e.Data);
                    process.ErrorDataReceived  += (s, e) => HandleLine(e.Data);
                    if (cancelling) throw new OperationCanceledException("已取消。");
                    if (!process.Start()) throw new InvalidOperationException("yt-dlp 無法啓動。");
                    currentProcess              = process;
                    if (cancelling) KillProcessTree(process);
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    process.WaitForExit();
                    int code                    = process.ExitCode;
                    currentProcess              = null;
                    int converted               = 0;
                    int conversionFailed        = 0;
                    if (subtitleMode && !cancelling)
                    {
                        PostUi(() => lblStatus.Text = "正在檢查與轉換字幕…");
                        try { ConvertNewVttFiles(folder, subtitleBefore, Path.Combine(appDir, "ffmpeg.exe"), out converted, out conversionFailed); }
                        catch (Exception ex)
                        {
                            conversionFailed++;
                            HandleLine("字幕轉換檢查失敗：" + ex.Message);
                        }
                    }
                    int convertedCount          = converted;
                    int failedCount             = conversionFailed;
                    PostUi(() =>
                    {
                        SetBusy(false);
                        if (cancelling) lblStatus.Text = "已取消；可能留下未完成的 .part 文件。";
                        else if (code == 0 && subtitleMode && noSubtitles && convertedCount == 0)
                            lblStatus.Text = "命令已完成，但影片沒有所選語言的字幕。";
                        else if (code != 0 && convertedCount > 0)
                            lblStatus.Text = "部分字幕請求失敗，但已生成 " + convertedCount + " 箇 SRT；請查看運行記録。";
                        else if (code == 0 && failedCount > 0)
                            lblStatus.Text = "下載完成，但有 " + failedCount + " 箇 VTT 未能转换为 SRT；請查看運行記録。";
                        else if (code == 0)
                        {
                            progress.Value = 100;
                            lblStatus.Text = subtitleMode && convertedCount > 0
                                ? "完成！已生成 " + convertedCount + " 箇 SRT 字幕。"
                                : "完成！可點擊「打開保存目録」。";
                        }
                        else
                        {
                            lblStatus.Text = lastErrorMessage ?? "下載失敗；請查看下方運行記録。";
                            if (douyinCookieError)
                                AppendLog(usedFirefoxCookies
                                    ? "提示：已經使用 Firefox Cookies 但抖音仍返回空數據。这可能是站點接口限制；請確認 Firefox 中能播放該影片，並關注 yt-dlp 抖音站點問題。"
                                    : "提示：先在 Firefox 中打開並刷新該抖音影片，再勾選「讀取 Firefox Cookies」重試。此方法也不保證站點接口一定可用。");
                        }
                        chkFirefox.Checked = false;
                    });
                }
            }
            catch (OperationCanceledException)
            {
                PostUi(() => { SetBusy(false); lblStatus.Text = "已取消。"; chkFirefox.Checked = false; });
            }
            catch (Exception ex)
            {
                PostUi(() => { SetBusy(false); lblStatus.Text = "啓動失敗：" + ex.Message; AppendLog(ex.ToString()); chkFirefox.Checked = false; });
            }
            finally { currentProcess = null; }
        }

        private void ConvertNewVttFiles(string folder, SubtitleSnapshot before, string ffmpeg, out int converted, out int failed)
        {
            converted = 0;
            failed    = 0;
            foreach (string vtt in Directory.GetFiles(folder, "*.vtt", SearchOption.TopDirectoryOnly))
            {
                if (cancelling) break;
                bool newVtt = !before.Vtt.Contains(vtt);
                string srt  = Path.ChangeExtension(vtt, ".srt");
                if (File.Exists(srt))
                {
                    if (newVtt && !before.Srt.Contains(srt) && new FileInfo(srt).Length > 0)
                    {
                        File.Delete(vtt);
                        converted++;
                        HandleLine("字幕：SRT 已生成，淸理本次下載的 VTT：" + Path.GetFileName(vtt));
                    }
                    else if (newVtt)
                    {
                        failed++;
                        HandleLine("字幕：已有同名 SRT，未覆盖，也保留本次 VTT：" + Path.GetFileName(vtt));
                    }
                    continue;
                }

                var info                    = new ProcessStartInfo(ffmpeg, "-hide_banner -loglevel error -nostdin -n -i " + Quote(vtt) + " -f srt " + Quote(srt));
                info.WorkingDirectory       = folder;
                info.UseShellExecute        = false;
                info.CreateNoWindow         = true;
                info.RedirectStandardOutput = true;
                info.RedirectStandardError  = true;
                info.StandardOutputEncoding = Encoding.Default;
                info.StandardErrorEncoding  = Encoding.Default;
                using (var process = new Process())
                {
                    process.StartInfo           = info;
                    process.OutputDataReceived += (s, e) => HandleLine(e.Data);
                    process.ErrorDataReceived  += (s, e) => HandleLine(e.Data);
                    if (!process.Start()) { failed++; continue; }
                    currentProcess              = process;
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    process.WaitForExit();
                    currentProcess              = null;
                    if (cancelling) break;
                    if (process.ExitCode == 0 && File.Exists(srt) && new FileInfo(srt).Length > 0)
                    {
                        if (newVtt) File.Delete(vtt);
                        converted++;
                        HandleLine("字幕：已转换为 SRT：" + Path.GetFileName(srt) + (newVtt ? "" : "（原有 VTT 已保留）"));
                    }
                    else
                    {
                        if (File.Exists(srt) && new FileInfo(srt).Length == 0) File.Delete(srt);
                        failed++;
                        HandleLine("字幕：VTT 转 SRT 失敗，原 VTT 已保留：" + Path.GetFileName(vtt));
                    }
                }
            }
        }

        private void HandleLine(string line)
        {
            if (String.IsNullOrEmpty(line)) return;
            PostUi(() =>
            {
                AppendLog(line);
                if (line.IndexOf("no subtitles", StringComparison.OrdinalIgnoreCase) >= 0) noSubtitles = true;
                Match match = percentPattern.Match(line);
                if (match.Success)
                {
                    double value;
                    if (Double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value))
                    {
                        progress.Value = Math.Max(0, Math.Min(100, (int)Math.Round(value)));
                        lblStatus.Text = "下載中 " + value.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%";
                    }
                }
                else if (line.IndexOf("[Merger]", StringComparison.OrdinalIgnoreCase) >= 0 || line.IndexOf("[ExtractAudio]", StringComparison.OrdinalIgnoreCase) >= 0)
                    lblStatus.Text = "下載完成，正在處理文件…";
                else if (line.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase))
                    lblStatus.Text = lastErrorMessage = FriendlyError(line);
            });
        }

        private string FriendlyError(string line)
        {
            if (isDouyinDownload && line.IndexOf("Fresh cookies", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                douyinCookieError = true;
                return chkFirefox.Checked
                    ? "抖音仍未返回影片數據；即使有新鮮 Cookies，站點接口也可能限制提取。"
                    : "抖音要求新鮮 Cookies；請先在 Firefox 打開影片，再勾選讀取。";
            }
            if (line.IndexOf("not a bot", StringComparison.OrdinalIgnoreCase) >= 0) return "YouTube 要求登録確認：可勾選讀取 Firefox Cookies。";
            if (line.IndexOf("HTTP Error 429", StringComparison.OrdinalIgnoreCase) >= 0) return "字幕請求被限流（429）；請稍後重試，避免短時間連續下載。";
            if (line.IndexOf("DPAPI", StringComparison.OrdinalIgnoreCase) >= 0) return "Chrome Cookies 解密失敗；請使用 Firefox。";
            if (line.IndexOf("private video", StringComparison.OrdinalIgnoreCase) >= 0) return "此影片需要訪問權限；可勾選讀取 Firefox Cookies。";
            if (line.IndexOf("Could not copy Firefox cookie database", StringComparison.OrdinalIgnoreCase) >= 0) return "無法讀取 Firefox 登録状态；請完全關閉 Firefox 後重試。";
            return "下載遇到錯誤；請查看運行記録。";
        }

        private void AppendLog(string line)
        {
            if (txtLog.IsDisposed) return;
            txtLog.AppendText(line + Environment.NewLine);
            if (txtLog.TextLength > 90000) txtLog.Text = txtLog.Text.Substring(txtLog.TextLength - 60000);
        }

        private void PostUi(Action action)
        {
            try
            {
                ui.Post(_ =>
                {
                    if (IsDisposed || Disposing || txtLog.IsDisposed) return;
                    action();
                }, null);
            }
            catch (ObjectDisposedException) { }
            catch (InvalidAsynchronousStateException) { }
        }

        private void SetBusy(bool busy)
        {
            SetBusy(busy, true);
        }

        private void SetBusy(bool busy, bool allowCancel)
        {
            foreach (Button btn in btnSelectMode)
            {
                btn.Enabled             = !busy;
            }
            btnDownload.Enabled         = !busy;
            btnCancel.Enabled           = busy && allowCancel;
            btnUpdateComponents.Enabled = !busy;
            btnBrowse.Enabled           = !busy;

            cmbSubtitleLanguage.Enabled = !busy && selectedMode == 2;
            cmbVideoResolution.Enabled  = !busy && selectedMode == 0;
            chkFirefox.Enabled          = !busy;
            chkPlaylist.Enabled         = !busy;

            txtSavePath.Enabled           = !busy;
            txtUrl.Enabled              = !busy;
        }

        private void CheckComponentsOnStartup()
        {
            string[] missing    = ComponentInstaller.MissingComponents(AppDomain.CurrentDomain.BaseDirectory, true);
            if (0 == missing.Length) { lblStatus.Text = "運行組件完整，可以開始下載。"; return; }

            lblStatus.Text      = "首次使用需要安裝 " + missing.Length + " 箇運行組件。";
            if (DialogResult.Yes == MBOX_I("這是輕量公開版。首次使用需要從各項目的官方伺服器下載運行組件。\n\n缺少：\n" +
                                         String.Join("\n", missing) + "\n\n完整下載约 250 MB。" +
                                         "程式會自動重試可信備用綫路，並使用官方 SHA-256 校驗文件。現在安裝吗？",
                                         "首次運行設置")) StartComponentInstall();
            else lblStatus.Text = "尚未安裝完整組件；可點击「檢查運行組件」繼續。";
        }

        private void StartComponentInstall()
        {
            string appDir       = AppDomain.CurrentDomain.BaseDirectory;
            string[] missing    = ComponentInstaller.MissingComponents(appDir, true);
            if (0 == missing.Length) { lblStatus.Text = "四箇運行組件均已安裝。"; return; }

            txtLog.Clear();
            progress.Value      = 0;
            cancelling          = false;
            AppendLog("將安裝：" + String.Join("、", missing));
            AppendLog("只使用項目官方或官方上游綫路；每箇文件安裝前都會校核官方 SHA-256。");
            lblStatus.Text      = "正在准備安裝運行組件…";
            SetBusy(true, false);
            var worker          = new Thread(() => RunComponentInstall(appDir));
            worker.IsBackground = true;
            worker.Start();
        }

        private void RunComponentInstall(string appDir)
        {
            try
            {
                ComponentInstaller.InstallMissing(appDir,
                                                  (value, message) => PostUi(() =>
                                                  {
                                                      progress.Value  = Math.Max(0, Math.Min(100, value));
                                                      lblStatus.Text  = message;
                                                  }),
                                                  message => PostUi(() => AppendLog(message)));
                string[] remaining = ComponentInstaller.MissingComponents(appDir, true);
                PostUi(() =>
                {
                    SetBusy(false);
                    if (remaining.Length == 0)
                    {
                        progress.Value  = 100;
                        lblStatus.Text     = "運行組件安裝完成，可以開始下載。";
                        AppendLog("組件安裝完成：yt-dlp、FFmpeg、FFprobe、Deno 均已就緒。");
                    }
                    else lblStatus.Text = "仍缺少組件：" + String.Join("、", remaining);
                });
            }
            catch (Exception ex)
            {
                PostUi(() =>
                {
                    SetBusy(false);
                    lblStatus.Text = "組件安裝失敗；已保留成功安裝的組件，請查看運行記録後重試。";
                    AppendLog("安裝失敗：" + ComponentInstaller.FriendlyNetworkMessage(ex));
                });
            }
        }

        private void btnUpdateComponents_EventHandler_Click(object sender, EventArgs e)
        {
            string appDir    = AppDomain.CurrentDomain.BaseDirectory;
            string[] missing = ComponentInstaller.MissingComponents(appDir, true);
            if ((missing.Length > 0) &&
                (DialogResult.Yes == MBOX_I("當前缺少：\n\n" + String.Join("\n", missing) +
                                            "\n\n是否從官方綫路下載並安裝？", "運行組件不完整")))
            {
                StartComponentInstall();
                return;
            }
            string ytDlp = Path.Combine(appDir, "yt-dlp.exe");
            if (DialogResult.Yes != MBOX_I("檢查完成：yt-dlp、FFmpeg、FFprobe 和 Deno 均已安裝。\n\n" +
                                           "是否繼續使用 yt-dlp 官方自更新功能檢查下載核心的新版本？\n\n\n\n" +
                                           "（本界面、FFmpeg、Deno不會因此更新。）", "運行組件完整"))
            {
                lblStatus.Text = "運行組件完整。";
                return;
            }

            txtLog.Clear();
            progress.Value      = 0;
            lblStatus.Text      = "正在檢查 yt-dlp 更新…";
            SetBusy(true, false);
            var worker          = new Thread(() => RunCoreUpdate(ytDlp, appDir));
            worker.IsBackground = true;
            worker.Start();
        }

        private void RunCoreUpdate(string ytDlp, string appDir)
        {
            try
            {
                var info                        = new ProcessStartInfo(ytDlp, "--ignore-config -U");
                info.WorkingDirectory           = appDir;
                info.UseShellExecute            = false;
                info.CreateNoWindow             = true;
                info.RedirectStandardOutput     = true;
                info.RedirectStandardError      = true;
                info.StandardOutputEncoding     = Encoding.Default;
                info.StandardErrorEncoding      = Encoding.Default;
                ConfigureProcessEnvironment(info, appDir);
                using (var process = new Process())
                {
                    process.StartInfo           = info;
                    process.OutputDataReceived += (s, e) => HandleUpdateLine(e.Data);
                    process.ErrorDataReceived  += (s, e) => HandleUpdateLine(e.Data);
                    if (!process.Start()) throw new InvalidOperationException("yt-dlp 更新程序無法啓動。");
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    process.WaitForExit();
                    int code                    = process.ExitCode;
                    PostUi(() =>
                    {
                        SetBusy(false);
                        lblStatus.Text          = code == 0
                            ? "更新檢查完成；下次下載會使用當前目録中的 yt-dlp 核心。"
                            : "更新失敗；請查看運行記録，確認网络和目録写入權限。";
                    });
                }
            }
            catch (Exception ex)
            {
                PostUi(() => { SetBusy(false); lblStatus.Text = "更新失敗：" + ex.Message; AppendLog(ex.ToString()); });
            }
        }

        private void HandleUpdateLine(string line)
        {
            if (String.IsNullOrEmpty(line)) return;
            PostUi(() => { AppendLog(line); lblStatus.Text = "正在檢查或下載 yt-dlp 更新…"; });
        }

        private static void KillProcessTree(Process process)
        {
            if (process == null || process.HasExited) return;
            var info                = new ProcessStartInfo("taskkill.exe", "/PID " + process.Id + " /T /F");
            info.UseShellExecute    = false;
            info.CreateNoWindow     = true;
            using (var killer = Process.Start(info))
            {
                if (killer == null) throw new InvalidOperationException("無法啓動 taskkill。");
                killer.WaitForExit(5000);
            }
        }

    #region Event Handler
        private void btnBrowse_EventHandler_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "選擇下載文件保存目録";
                if (Directory.Exists(txtSavePath.Text)) dialog.SelectedPath = txtSavePath.Text;
                if (DialogResult.OK == dialog.ShowDialog(this)) txtSavePath.Text = dialog.SelectedPath;
            }
        }

        private void btnCancel_EventHandler_Click(object sender, EventArgs e)
        {
            cancelling           = true;
            btnCancel.Enabled    = false;
            lblStatus.Text       = "正在取消…";
            try { KillProcessTree(currentProcess); }
            catch (Exception ex) { AppendLog("取消失敗：" + ex.Message); }
        }

        private void btnDownload_EventHandler_Click(object sender, EventArgs e)
        {
            string url = ExtractPreferredUrl(txtUrl.Text);
            if (!Uri.IsWellFormedUriString(url, UriKind.Absolute) ||
                !(url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
            {
                MBOX_W("沒有識別到有效的視頻網址。可以直接貼上抖音等平臺的整段分享文字。", "鏈接無效");
                return;
            }

            string rootFolder = txtSavePath.Text.Trim();
            if (0 == rootFolder.Length || !Path.IsPathRooted(rootFolder))
            {
                MBOX_W("請選擇完整的保存根目録。", "缺少目録");
                return;
            }
            string folder;
            try { folder = Path.Combine(rootFolder, ModeSubfolder(selectedMode)); }
            catch (ArgumentException)
            {
                MBOX_W("保存目録名無效。", "目録錯誤");
                return;
            }

            string appDir       = AppDomain.CurrentDomain.BaseDirectory;
            string ytDlp        = Path.Combine(appDir, "yt-dlp.exe");
            string[] missing    = ComponentInstaller.MissingComponents(appDir, true);
            if ((missing.Length > 0) &&
                (DialogResult.Yes ==  MBOX_E("程式目録缺少以下組件：\n\n" + String.Join("\n", missing) +
                                             "\n\n是否現在從各項目的官方 GitHub 下載並校驗？也可以取消後手動放入程式目録。",
                                             "需要安裝運行組件", MessageBoxButtons.YesNo)))
            {
                StartComponentInstall();
                return;
            }

            isDouyinDownload = IsDouyinUrl(url);
            bool cookiesConfirmed = false;
            if (isDouyinDownload && !chkFirefox.Checked)
            {
                var choice = MBOX_I("抖音經常要求新鮮 Cookies（不一定需要登録）。請先在 Firefox 中打開該抖音鏈接並刷新頁面。\n\n" +
                                    "現在從 Firefox 讀取 Cookies 嗎？選擇「否」會繼續嘗試無 Cookies 下載，但可能失敗。",
                                    "抖音訪問提示", MessageBoxButtons.YesNoCancel);
                if (choice == DialogResult.Cancel) return;
                if (choice == DialogResult.Yes) { chkFirefox.Checked = true; cookiesConfirmed = true; }
            }
            if (chkPlaylist.Checked && DialogResult.OK != MBOX_Q("你選擇了下載整箇播放列表。請確認鏈接中的播放列表數量不會過大。", "確認批量下載")) return;
            if (chkFirefox.Checked  && !cookiesConfirmed &&
                DialogResult.OK != MBOX_I("將從 Firefox 讀取 Cookies，可能包含登録状态。" +
                                          "請僅下載你有權訪問和使用的内容；如果讀取失敗，可先完全關閉 Firefox 後重試。",
                                          "使用 Firefox Cookies", MessageBoxButtons.OKCancel)) return;

            try { Directory.CreateDirectory(folder); }
            catch (Exception ex)
            {
                 MBOX_E("無法創建保存目録：" + ex.Message, "目録錯誤");
                return;
            }

            bool subtitleMode = selectedMode == 2;
            SubtitleSnapshot subtitleBefore = null;
            try { if (subtitleMode) subtitleBefore = CaptureSubtitles(folder); }
            catch (Exception ex)
            {
                 MBOX_E("無法檢查字幕目録：" + ex.Message, "目録錯誤");
                return;
            }

            lastFolder              = folder;
            progress.Value          = 0;
            txtLog.Clear();
            cancelling              = false;
            noSubtitles             = false;
            lastErrorMessage        = null;
            douyinCookieError       = false;
            bool usedFirefoxCookies = chkFirefox.Checked;
            SetBusy(true);
            lblStatus.Text          = "正在分析鏈接……";
            var args                = BuildArguments(url, folder, appDir);
            var worker              = new Thread(() => RunDownload(ytDlp, args, appDir, folder, subtitleMode, subtitleBefore, usedFirefoxCookies));
            worker.IsBackground     = true;
            worker.Start();
        }

        private void btnOpenDownloadFolder_EventHandler_Click(object sender, EventArgs e)
        {
            try
            {
                string folder = lastFolder ?? Path.Combine(txtSavePath.Text.Trim(), ModeSubfolder(selectedMode));
                if (Directory.Exists(folder)) Process.Start("explorer.exe", Quote(folder));
                else  MBOX_E("保存目録尚不存在。", "無法打開");
            }
            catch (Exception ex)
            {
                 MBOX_E("無法打開目録：" + ex.Message, "目録錯誤");
            }
        }

        private void btnSelectMode_EventHandler_Click_SelectMode(int mode)
        {
            selectedMode = mode;
            for (int i = 0; i < btnSelectMode.Length; i++)
            {
                bool selected = i == mode;
                btnSelectMode[i].BackColor                  = selected ? Color.FromArgb(32, 103, 201) : Color.FromArgb(239, 244, 251);
                btnSelectMode[i].ForeColor                  = selected ? Color.White                  : Color.FromArgb(36, 53, 77);
                btnSelectMode[i].FlatAppearance.BorderColor = selected ? Color.FromArgb(21, 82, 173)  : Color.FromArgb(195, 208, 225);
            }
            cmbSubtitleLanguage.Enabled = mode == 2 && btnDownload.Enabled;
            cmbVideoResolution.Enabled  = mode == 0 && btnDownload.Enabled;
            txtSavePath_EventHandler_TextChanged_UpdateFolderPreview();
        }

        private void txtSavePath_EventHandler_TextChanged_UpdateFolderPreview()
        {
            string rootFolder = txtSavePath.Text.Trim();
            if (0 == rootFolder.Length) { lblSavePreview.Text = "本次保存到：請選擇根目録"; return; }
            try { lblSavePreview.Text = "本次保存到：" + Path.Combine(rootFolder, ModeSubfolder(selectedMode)); }
            catch (ArgumentException) { lblSavePreview.Text = "本次保存到：目録名無效"; }
        }

        private void txtUrl_EventHandler_TextChanged_UpdatePreview()
        {
            string extracted        = ExtractPreferredUrl(txtUrl.Text);
            lblUrlPreview.Text      = "識別到的網址：" + (extracted == null ? "尚未找到" : extracted);
            lblUrlPreview.ForeColor = extracted == null ? Color.FromArgb(129, 139, 154) : Color.FromArgb(29, 111, 131);
        }
    #endregion /* Event Handler */

    #region MESSAGE BOX
        private DialogResult MBOX_Q(string __MESSAGE__, string __CAPTION__ = null, MessageBoxButtons __BUTTON__ = MessageBoxButtons.OKCancel)
        {
            return MessageBox.Show(this, __MESSAGE__, __CAPTION__ ?? Process.GetCurrentProcess().ProcessName, __BUTTON__, MessageBoxIcon.Question);
        }
        private DialogResult MBOX_E(string __MESSAGE__, string __CAPTION__ = null, MessageBoxButtons __BUTTON__ = MessageBoxButtons.OK)
        {
            return MessageBox.Show(this, __MESSAGE__, __CAPTION__ ?? Process.GetCurrentProcess().ProcessName, __BUTTON__, MessageBoxIcon.Error);
        }
        private DialogResult MBOX_I(string __MESSAGE__, string __CAPTION__ = null, MessageBoxButtons __BUTTON__ = MessageBoxButtons.YesNo)
        {
            return MessageBox.Show(this, __MESSAGE__, __CAPTION__ ?? Process.GetCurrentProcess().ProcessName, __BUTTON__, MessageBoxIcon.Information);
        }
        private DialogResult MBOX_W(string __MESSAGE__, string __CAPTION__ = null, MessageBoxButtons __BUTTON__ = MessageBoxButtons.OK)
        {
            return MessageBox.Show(this, __MESSAGE__, __CAPTION__ ?? Process.GetCurrentProcess().ProcessName, __BUTTON__, MessageBoxIcon.Warning);
        }
    #endregion /* MESSAGE BOX */
    }
}
