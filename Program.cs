using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace YtDlpGuiMvp
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    internal sealed class MainForm : Form
    {
        private readonly TextBox urlBox = new TextBox();
        private readonly Button[] modeButtons = { new Button(), new Button(), new Button() };
        private readonly ComboBox subtitleLanguageBox = new ComboBox();
        private int selectedMode;
        private readonly TextBox folderBox = new TextBox();
        private readonly CheckBox firefoxBox = new CheckBox();
        private readonly CheckBox playlistBox = new CheckBox();
        private readonly Button browseButton = new Button();
        private readonly Button downloadButton = new Button();
        private readonly Button cancelButton = new Button();
        private readonly Button openButton = new Button();
        private readonly Button updateButton = new Button();
        private readonly ProgressBar progress = new ProgressBar();
        private readonly Label status = new Label();
        private readonly Label urlPreview = new Label();
        private readonly Label savePreview = new Label();
        private readonly TextBox logBox = new TextBox();
        private readonly ToolTip tips = new ToolTip();
        private readonly SynchronizationContext ui;
        private volatile Process currentProcess;
        private volatile bool cancelling;
        private volatile bool noSubtitles;
        private string lastErrorMessage;
        private bool isDouyinDownload;
        private bool douyinCookieError;
        private string lastFolder;
        private readonly Regex percentPattern = new Regex(@"\[download\]\s+(\d+(?:\.\d+)?)%", RegexOptions.Compiled);
        private static readonly Regex urlPattern = new Regex("https?://[^\\s<>\\[\\]\\(\\)（）“”\\\"'，。；！？、]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private sealed class SubtitleSnapshot
        {
            public readonly HashSet<string> Vtt = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public readonly HashSet<string> Srt = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        public MainForm(bool checkComponentsOnShown = true)
        {
            ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
            Text = "视频音频字幕下载 · MVP v0.5";
            Icon = SystemIcons.Application;
            MinimumSize = new Size(760, 700);
            Size = new Size(960, 760);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(237, 242, 248);
            Padding = new Padding(14);
            Font = new Font("Microsoft YaHei UI", 10F);
            BuildUi();
            if (checkComponentsOnShown) Shown += (s, e) => CheckComponentsOnStartup();
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(28, 20, 28, 20);
            root.BackColor = Color.White;
            root.ColumnCount = 1;
            root.RowCount = 15;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 29));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 43));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 29));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 43));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 31));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 29));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);

            var title = new Label();
            title.Text = "视频音频字幕下载";
            title.Font = new Font("Microsoft YaHei UI", 20F, FontStyle.Bold);
            title.ForeColor = Color.FromArgb(27, 43, 67);
            title.Dock = DockStyle.Fill;
            root.Controls.Add(title, 0, 0);

            var subtitle = MakeLabel("复制分享文案或视频网址，选择格式，然后点击开始下载。");
            subtitle.ForeColor = Color.FromArgb(101, 116, 139);
            root.Controls.Add(subtitle, 0, 1);
            root.Controls.Add(MakeLabel("分享文字或视频网址"), 0, 2);
            urlBox.Dock = DockStyle.Fill;
            urlBox.Multiline = true;
            urlBox.ScrollBars = ScrollBars.Vertical;
            urlBox.BorderStyle = BorderStyle.FixedSingle;
            urlBox.BackColor = Color.FromArgb(250, 252, 255);
            urlBox.Font = new Font("Microsoft YaHei UI", 10.5F);
            urlBox.TextChanged += (s, e) => UpdateUrlPreview();
            root.Controls.Add(urlBox, 0, 3);
            urlPreview.Dock = DockStyle.Fill;
            urlPreview.TextAlign = ContentAlignment.MiddleLeft;
            urlPreview.ForeColor = Color.FromArgb(29, 111, 131);
            urlPreview.AutoEllipsis = true;
            root.Controls.Add(urlPreview, 0, 4);

            var optionRow = new FlowLayoutPanel();
            optionRow.Dock = DockStyle.Fill;
            optionRow.WrapContents = false;
            optionRow.FlowDirection = FlowDirection.LeftToRight;
            var downloadTypeLabel = MakeLabel("下载类型");
            downloadTypeLabel.Dock = DockStyle.None;
            downloadTypeLabel.Size = new Size(98, 40);
            downloadTypeLabel.Margin = new Padding(0, 0, 0, 0);
            optionRow.Controls.Add(downloadTypeLabel);
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                modeButtons[i].Text = new[] { "视频 MP4", "音频 MP3", "字幕 SRT" }[i];
                modeButtons[i].Size = new Size(112, 36);
                modeButtons[i].Dock = DockStyle.None;
                modeButtons[i].Margin = new Padding(0, 2, 5, 0);
                modeButtons[i].FlatStyle = FlatStyle.Flat;
                modeButtons[i].FlatAppearance.BorderSize = 1;
                modeButtons[i].UseVisualStyleBackColor = false;
                modeButtons[i].TextAlign = ContentAlignment.MiddleCenter;
                modeButtons[i].Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
                modeButtons[i].Cursor = Cursors.Hand;
                modeButtons[i].Click += (s, e) => SelectMode(index);
                optionRow.Controls.Add(modeButtons[i]);
            }
            var languageLabel = MakeLabel("字幕语言");
            languageLabel.Dock = DockStyle.None;
            languageLabel.Size = new Size(77, 40);
            languageLabel.Margin = new Padding(9, 0, 0, 0);
            optionRow.Controls.Add(languageLabel);
            subtitleLanguageBox.DropDownStyle = ComboBoxStyle.DropDownList;
            subtitleLanguageBox.Items.AddRange(new object[] { "简体中文", "繁体中文", "英文" });
            subtitleLanguageBox.SelectedIndex = 0;
            subtitleLanguageBox.Dock = DockStyle.None;
            subtitleLanguageBox.Width = 133;
            subtitleLanguageBox.Margin = new Padding(0, 5, 0, 0);
            subtitleLanguageBox.FlatStyle = FlatStyle.Standard;
            optionRow.Controls.Add(subtitleLanguageBox);
            root.Controls.Add(optionRow, 0, 5);
            SelectMode(0);

            root.Controls.Add(MakeLabel("保存根目录（自动按类型分类）"), 0, 6);

            var folderRow = new TableLayoutPanel();
            folderRow.Dock = DockStyle.Fill;
            folderRow.ColumnCount = 2;
            folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            folderBox.Dock = DockStyle.Fill;
            folderBox.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "yt-dlp 下载");
            folderBox.BorderStyle = BorderStyle.FixedSingle;
            folderBox.BackColor = Color.FromArgb(250, 252, 255);
            folderBox.TextChanged += (s, e) => UpdateSavePreview();
            folderRow.Controls.Add(folderBox, 0, 0);
            browseButton.Text = "选择目录";
            browseButton.Dock = DockStyle.Fill;
            StyleButton(browseButton, Color.FromArgb(231, 237, 245), Color.FromArgb(36, 53, 77));
            browseButton.Click += BrowseClicked;
            folderRow.Controls.Add(browseButton, 1, 0);
            root.Controls.Add(folderRow, 0, 7);
            savePreview.Dock = DockStyle.Fill;
            savePreview.TextAlign = ContentAlignment.MiddleLeft;
            savePreview.ForeColor = Color.FromArgb(88, 101, 119);
            savePreview.AutoEllipsis = true;
            root.Controls.Add(savePreview, 0, 8);

            var checkRow = new FlowLayoutPanel();
            checkRow.Dock = DockStyle.Fill;
            checkRow.FlowDirection = FlowDirection.LeftToRight;
            firefoxBox.Text = "读取 Firefox Cookies（需要验证时）";
            firefoxBox.AutoSize = true;
            firefoxBox.Margin = new Padding(0, 10, 24, 0);
            playlistBox.Text = "下载整个播放列表";
            playlistBox.AutoSize = true;
            playlistBox.Margin = new Padding(0, 10, 0, 0);
            checkRow.Controls.Add(firefoxBox);
            checkRow.Controls.Add(playlistBox);
            root.Controls.Add(checkRow, 0, 9);
            tips.SetToolTip(firefoxBox, "先用 Firefox 打开目标视频站点并刷新页面，再勾选。Cookies 可能包含登录状态。");

            var buttonRow = new FlowLayoutPanel();
            buttonRow.Dock = DockStyle.Fill;
            buttonRow.Controls.Add(downloadButton);
            buttonRow.Controls.Add(cancelButton);
            buttonRow.Controls.Add(openButton);
            buttonRow.Controls.Add(updateButton);
            downloadButton.Text = "开始下载";
            downloadButton.Width = 145;
            StyleButton(downloadButton, Color.FromArgb(32, 103, 201), Color.White);
            downloadButton.Click += DownloadClicked;
            cancelButton.Text = "取消";
            cancelButton.Width = 92;
            StyleButton(cancelButton, Color.FromArgb(231, 237, 245), Color.FromArgb(36, 53, 77));
            cancelButton.Enabled = false;
            cancelButton.Click += CancelClicked;
            openButton.Text = "打开保存目录";
            openButton.Width = 140;
            StyleButton(openButton, Color.FromArgb(231, 237, 245), Color.FromArgb(36, 53, 77));
            openButton.Click += OpenClicked;
            updateButton.Text = "检查运行组件";
            updateButton.Width = 155;
            StyleButton(updateButton, Color.FromArgb(221, 241, 238), Color.FromArgb(17, 105, 98));
            updateButton.Click += UpdateClicked;
            tips.SetToolTip(updateButton, "检查四个运行组件；缺少时从官方 GitHub 下载并校验，完整时可更新 yt-dlp。");
            root.Controls.Add(buttonRow, 0, 10);

            progress.Dock = DockStyle.Fill;
            progress.Minimum = 0;
            progress.Maximum = 100;
            root.Controls.Add(progress, 0, 11);
            status.Text = "就绪";
            status.Dock = DockStyle.Fill;
            status.ForeColor = Color.FromArgb(70, 82, 96);
            status.AutoEllipsis = true;
            root.Controls.Add(status, 0, 12);
            root.Controls.Add(MakeLabel("运行记录（不显示 Cookies 内容）"), 0, 13);
            logBox.Multiline = true;
            logBox.ScrollBars = ScrollBars.Vertical;
            logBox.ReadOnly = true;
            logBox.Dock = DockStyle.Fill;
            logBox.BorderStyle = BorderStyle.FixedSingle;
            logBox.BackColor = Color.FromArgb(250, 252, 255);
            logBox.Font = new Font("Consolas", 9F);
            root.Controls.Add(logBox, 0, 14);
            UpdateUrlPreview();
            UpdateSavePreview();
        }

        private static void StyleButton(Button button, Color background, Color foreground)
        {
            button.Height = 38;
            button.BackColor = background;
            button.ForeColor = foreground;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.Margin = new Padding(0, 4, 10, 0);
            button.Cursor = Cursors.Hand;
        }

        private Label MakeLabel(string value)
        {
            var label = new Label();
            label.Text = value;
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.ForeColor = Color.FromArgb(76, 92, 112);
            return label;
        }

        private static string ExtractFirstUrl(string input)
        {
            if (String.IsNullOrWhiteSpace(input)) return null;
            Match match = urlPattern.Match(input);
            if (!match.Success) return null;
            return match.Value.TrimEnd('.', ',', ';', '!', '。', '，', '；', '！', '？', '、');
        }

        private static string ModeSubfolder(int mode)
        {
            if (mode == 1) return "mp3";
            if (mode == 2) return "subtitles";
            return "mp4";
        }

        private void SelectMode(int mode)
        {
            selectedMode = mode;
            for (int i = 0; i < modeButtons.Length; i++)
            {
                bool selected = i == mode;
                modeButtons[i].BackColor = selected ? Color.FromArgb(32, 103, 201) : Color.FromArgb(239, 244, 251);
                modeButtons[i].ForeColor = selected ? Color.White : Color.FromArgb(36, 53, 77);
                modeButtons[i].FlatAppearance.BorderColor = selected ? Color.FromArgb(21, 82, 173) : Color.FromArgb(195, 208, 225);
            }
            subtitleLanguageBox.Enabled = mode == 2 && downloadButton.Enabled;
            UpdateSavePreview();
        }

        private string SelectedSubtitleLanguage()
        {
            if (subtitleLanguageBox.SelectedIndex == 1) return "^zh-Hant$";
            if (subtitleLanguageBox.SelectedIndex == 2) return "^en$";
            return "^zh-Hans$";
        }

        private void UpdateUrlPreview()
        {
            string extracted = ExtractFirstUrl(urlBox.Text);
            urlPreview.Text = extracted == null ? "识别到的网址：尚未找到" : "识别到的网址：" + extracted;
            urlPreview.ForeColor = extracted == null ? Color.FromArgb(129, 139, 154) : Color.FromArgb(29, 111, 131);
        }

        private void UpdateSavePreview()
        {
            string rootFolder = folderBox.Text.Trim();
            if (rootFolder.Length == 0) { savePreview.Text = "本次保存到：请选择根目录"; return; }
            try { savePreview.Text = "本次保存到：" + Path.Combine(rootFolder, ModeSubfolder(selectedMode)); }
            catch (ArgumentException) { savePreview.Text = "本次保存到：目录名称无效"; }
        }

        private void BrowseClicked(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "选择下载文件保存目录";
                if (Directory.Exists(folderBox.Text)) dialog.SelectedPath = folderBox.Text;
                if (dialog.ShowDialog(this) == DialogResult.OK) folderBox.Text = dialog.SelectedPath;
            }
        }

        private void DownloadClicked(object sender, EventArgs e)
        {
            string url = ExtractFirstUrl(urlBox.Text);
            if (!Uri.IsWellFormedUriString(url, UriKind.Absolute) ||
                !(url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show(this, "没有识别到有效的视频网址。可以直接粘贴抖音等平台的整段分享文字。", "链接无效", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string rootFolder = folderBox.Text.Trim();
            if (rootFolder.Length == 0 || !Path.IsPathRooted(rootFolder))
            {
                MessageBox.Show(this, "请选择完整的保存根目录。", "缺少目录", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            string folder;
            try { folder = Path.Combine(rootFolder, ModeSubfolder(selectedMode)); }
            catch (ArgumentException)
            {
                MessageBox.Show(this, "保存目录名称无效。", "目录错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string ytDlp = Path.Combine(appDir, "yt-dlp.exe");
            string[] missing = ComponentInstaller.MissingComponents(appDir);
            if (missing.Length > 0)
            {
                DialogResult install = MessageBox.Show(this,
                    "程序目录缺少以下组件：\n\n" + String.Join("\n", missing) +
                    "\n\n是否现在从各项目的官方 GitHub 下载并校验？也可以取消后手动放入程序目录。",
                    "需要安装运行组件", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (install == DialogResult.Yes) StartComponentInstall();
                return;
            }

            isDouyinDownload = IsDouyinUrl(url);
            bool cookiesConfirmed = false;
            if (isDouyinDownload && !firefoxBox.Checked)
            {
                var choice = MessageBox.Show(this,
                    "抖音经常要求新鲜 Cookies（不一定需要登录）。请先在 Firefox 中打开这条抖音链接并刷新页面。\n\n现在从 Firefox 读取 Cookies 吗？选择“否”会继续尝试无 Cookies 下载，但可能失败。",
                    "抖音访问提示", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Information);
                if (choice == DialogResult.Cancel) return;
                if (choice == DialogResult.Yes) { firefoxBox.Checked = true; cookiesConfirmed = true; }
            }
            if (playlistBox.Checked && MessageBox.Show(this, "你选择了下载整个播放列表。请确认链接中的播放列表数量不会过大。", "确认批量下载", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
            if (firefoxBox.Checked && !cookiesConfirmed && MessageBox.Show(this, "将从 Firefox 读取 Cookies，可能包含登录状态。请仅下载你有权访问和使用的内容；如果读取失败，可先完全关闭 Firefox 后重试。", "使用 Firefox Cookies", MessageBoxButtons.OKCancel, MessageBoxIcon.Information) != DialogResult.OK) return;

            try { Directory.CreateDirectory(folder); }
            catch (Exception ex)
            {
                MessageBox.Show(this, "无法创建保存目录：" + ex.Message, "目录错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            bool subtitleMode = selectedMode == 2;
            SubtitleSnapshot subtitleBefore = null;
            try { if (subtitleMode) subtitleBefore = CaptureSubtitles(folder); }
            catch (Exception ex)
            {
                MessageBox.Show(this, "无法检查字幕目录：" + ex.Message, "目录错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            lastFolder = folder;
            progress.Value = 0;
            logBox.Clear();
            cancelling = false;
            noSubtitles = false;
            lastErrorMessage = null;
            douyinCookieError = false;
            bool usedFirefoxCookies = firefoxBox.Checked;
            SetBusy(true);
            status.Text = "正在分析链接…";
            var args = BuildArguments(url, folder, appDir);
            var worker = new Thread(() => RunDownload(ytDlp, args, appDir, folder, subtitleMode, subtitleBefore, usedFirefoxCookies));
            worker.IsBackground = true;
            worker.Start();
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
            args.Append("--ignore-config --newline --no-overwrites --no-post-overwrites --no-playlist ");
            if (playlistBox.Checked) args.Replace("--no-playlist", "--yes-playlist");
            args.Append("--ffmpeg-location ").Append(Quote(appDir)).Append(' ');
            args.Append("-P ").Append(Quote(folder)).Append(' ');
            if (firefoxBox.Checked) args.Append("--cookies-from-browser firefox ");
            if (selectedMode == 0) args.Append("-t mp4 ");
            else if (selectedMode == 1) args.Append("-t mp3 ");
            else args.Append("--write-subs --write-auto-subs --sub-langs ").Append(Quote(SelectedSubtitleLanguage())).Append(" --convert-subs srt --skip-download ");
            args.Append(Quote(url));
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

        private void RunDownload(string ytDlp, string args, string appDir, string folder, bool subtitleMode, SubtitleSnapshot subtitleBefore, bool usedFirefoxCookies)
        {
            try
            {
                var info = new ProcessStartInfo(ytDlp, args);
                info.WorkingDirectory = appDir;
                info.UseShellExecute = false;
                info.CreateNoWindow = true;
                info.RedirectStandardOutput = true;
                info.RedirectStandardError = true;
                // The Windows yt-dlp executable uses the active ANSI code page for redirected pipes.
                // On Chinese Windows this is GBK, not UTF-8.
                info.StandardOutputEncoding = Encoding.Default;
                info.StandardErrorEncoding = Encoding.Default;
                ConfigureProcessEnvironment(info, appDir);
                using (var process = new Process())
                {
                    process.StartInfo = info;
                    process.OutputDataReceived += (s, e) => HandleLine(e.Data);
                    process.ErrorDataReceived += (s, e) => HandleLine(e.Data);
                    if (cancelling) throw new OperationCanceledException("已取消。");
                    if (!process.Start()) throw new InvalidOperationException("yt-dlp 无法启动。");
                    currentProcess = process;
                    if (cancelling) KillProcessTree(process);
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    process.WaitForExit();
                    int code = process.ExitCode;
                    currentProcess = null;
                    int converted = 0;
                    int conversionFailed = 0;
                    if (subtitleMode && !cancelling)
                    {
                        PostUi(() => status.Text = "正在检查和转换字幕…");
                        try { ConvertNewVttFiles(folder, subtitleBefore, Path.Combine(appDir, "ffmpeg.exe"), out converted, out conversionFailed); }
                        catch (Exception ex)
                        {
                            conversionFailed++;
                            HandleLine("字幕转换检查失败：" + ex.Message);
                        }
                    }
                    int convertedCount = converted;
                    int failedCount = conversionFailed;
                    PostUi(() =>
                    {
                        SetBusy(false);
                        if (cancelling) status.Text = "已取消；可能留下未完成的 .part 文件。";
                        else if (code == 0 && subtitleMode && noSubtitles && convertedCount == 0)
                            status.Text = "命令已完成，但视频没有所选语言的字幕。";
                        else if (code != 0 && convertedCount > 0)
                            status.Text = "部分字幕请求失败，但已生成 " + convertedCount + " 个 SRT；请查看运行记录。";
                        else if (code == 0 && failedCount > 0)
                            status.Text = "下载完成，但有 " + failedCount + " 个 VTT 未能转换为 SRT；请查看运行记录。";
                        else if (code == 0)
                        {
                            progress.Value = 100;
                            status.Text = subtitleMode && convertedCount > 0
                                ? "完成！已生成 " + convertedCount + " 个 SRT 字幕。"
                                : "完成！可点击“打开保存目录”。";
                        }
                        else
                        {
                            status.Text = lastErrorMessage ?? "下载失败；请查看下方运行记录。";
                            if (douyinCookieError)
                                AppendLog(usedFirefoxCookies
                                    ? "提示：已经使用 Firefox Cookies 但抖音仍返回空数据。这可能是站点接口限制；请确认 Firefox 中能播放该视频，并关注 yt-dlp 抖音站点问题。"
                                    : "提示：先在 Firefox 中打开并刷新这条抖音视频，再勾选“读取 Firefox Cookies”重试。此方法也不保证站点接口一定可用。");
                        }
                        firefoxBox.Checked = false;
                    });
                }
            }
            catch (OperationCanceledException)
            {
                PostUi(() => { SetBusy(false); status.Text = "已取消。"; firefoxBox.Checked = false; });
            }
            catch (Exception ex)
            {
                PostUi(() => { SetBusy(false); status.Text = "启动失败：" + ex.Message; AppendLog(ex.ToString()); firefoxBox.Checked = false; });
            }
            finally { currentProcess = null; }
        }

        private void ConvertNewVttFiles(string folder, SubtitleSnapshot before, string ffmpeg, out int converted, out int failed)
        {
            converted = 0;
            failed = 0;
            foreach (string vtt in Directory.GetFiles(folder, "*.vtt", SearchOption.TopDirectoryOnly))
            {
                if (cancelling) break;
                bool newVtt = !before.Vtt.Contains(vtt);
                string srt = Path.ChangeExtension(vtt, ".srt");
                if (File.Exists(srt))
                {
                    if (newVtt && !before.Srt.Contains(srt) && new FileInfo(srt).Length > 0)
                    {
                        File.Delete(vtt);
                        converted++;
                        HandleLine("字幕：SRT 已生成，清理本次下载的 VTT：" + Path.GetFileName(vtt));
                    }
                    else if (newVtt)
                    {
                        failed++;
                        HandleLine("字幕：已有同名 SRT，未覆盖，也保留本次 VTT：" + Path.GetFileName(vtt));
                    }
                    continue;
                }

                var info = new ProcessStartInfo(ffmpeg, "-hide_banner -loglevel error -nostdin -n -i " + Quote(vtt) + " -f srt " + Quote(srt));
                info.WorkingDirectory = folder;
                info.UseShellExecute = false;
                info.CreateNoWindow = true;
                info.RedirectStandardOutput = true;
                info.RedirectStandardError = true;
                info.StandardOutputEncoding = Encoding.Default;
                info.StandardErrorEncoding = Encoding.Default;
                using (var process = new Process())
                {
                    process.StartInfo = info;
                    process.OutputDataReceived += (s, e) => HandleLine(e.Data);
                    process.ErrorDataReceived += (s, e) => HandleLine(e.Data);
                    if (!process.Start()) { failed++; continue; }
                    currentProcess = process;
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    process.WaitForExit();
                    currentProcess = null;
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
                        HandleLine("字幕：VTT 转 SRT 失败，原 VTT 已保留：" + Path.GetFileName(vtt));
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
                        status.Text = "下载中 " + value.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%";
                    }
                }
                else if (line.IndexOf("[Merger]", StringComparison.OrdinalIgnoreCase) >= 0 || line.IndexOf("[ExtractAudio]", StringComparison.OrdinalIgnoreCase) >= 0)
                    status.Text = "下载完成，正在处理文件…";
                else if (line.StartsWith("ERROR:", StringComparison.OrdinalIgnoreCase))
                    status.Text = lastErrorMessage = FriendlyError(line);
            });
        }

        private string FriendlyError(string line)
        {
            if (isDouyinDownload && line.IndexOf("Fresh cookies", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                douyinCookieError = true;
                return firefoxBox.Checked
                    ? "抖音仍未返回视频数据；即使有新鲜 Cookies，站点接口也可能限制提取。"
                    : "抖音要求新鲜 Cookies；请先在 Firefox 打开视频，再勾选读取。";
            }
            if (line.IndexOf("not a bot", StringComparison.OrdinalIgnoreCase) >= 0) return "YouTube 要求登录确认：可勾选读取 Firefox Cookies。";
            if (line.IndexOf("HTTP Error 429", StringComparison.OrdinalIgnoreCase) >= 0) return "字幕请求被限流（429）；请稍后重试，避免短时间连续下载。";
            if (line.IndexOf("DPAPI", StringComparison.OrdinalIgnoreCase) >= 0) return "Chrome Cookies 解密失败；请使用 Firefox。";
            if (line.IndexOf("private video", StringComparison.OrdinalIgnoreCase) >= 0) return "此视频需要访问权限；可勾选读取 Firefox Cookies。";
            if (line.IndexOf("Could not copy Firefox cookie database", StringComparison.OrdinalIgnoreCase) >= 0) return "无法读取 Firefox 登录状态；请完全关闭 Firefox 后重试。";
            return "下载遇到错误；请查看运行记录。";
        }

        private void AppendLog(string line)
        {
            if (logBox.IsDisposed) return;
            logBox.AppendText(line + Environment.NewLine);
            if (logBox.TextLength > 90000) logBox.Text = logBox.Text.Substring(logBox.TextLength - 60000);
        }

        private void PostUi(Action action)
        {
            try
            {
                ui.Post(_ =>
                {
                    if (IsDisposed || Disposing || logBox.IsDisposed) return;
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
            downloadButton.Enabled = !busy;
            cancelButton.Enabled = busy && allowCancel;
            updateButton.Enabled = !busy;
            browseButton.Enabled = !busy;
            folderBox.Enabled = !busy;
            urlBox.Enabled = !busy;
            foreach (Button button in modeButtons) button.Enabled = !busy;
            subtitleLanguageBox.Enabled = !busy && selectedMode == 2;
            firefoxBox.Enabled = !busy;
            playlistBox.Enabled = !busy;
        }

        private void CheckComponentsOnStartup()
        {
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] missing = ComponentInstaller.MissingComponents(appDir);
            if (missing.Length == 0)
            {
                status.Text = "运行组件完整，可以开始下载。";
                return;
            }
            status.Text = "首次使用需要安装 " + missing.Length + " 个运行组件。";
            DialogResult choice = MessageBox.Show(this,
                "这是轻量公开版。首次使用需要从各项目的官方服务器下载运行组件。\n\n缺少：\n" +
                String.Join("\n", missing) +
                "\n\n完整下载约 250 MB。程序会自动重试可信备用线路，并使用官方 SHA-256 校验文件。现在安装吗？",
                "首次运行设置", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (choice == DialogResult.Yes) StartComponentInstall();
            else status.Text = "尚未安装完整组件；可点击“检查运行组件”继续。";
        }

        private void StartComponentInstall()
        {
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] missing = ComponentInstaller.MissingComponents(appDir);
            if (missing.Length == 0)
            {
                status.Text = "四个运行组件均已安装。";
                return;
            }
            logBox.Clear();
            progress.Value = 0;
            cancelling = false;
            AppendLog("将安装：" + String.Join("、", missing));
            AppendLog("只使用项目官方或官方上游线路；每个文件安装前都会核对官方 SHA-256。");
            status.Text = "正在准备安装运行组件…";
            SetBusy(true, false);
            var worker = new Thread(() => RunComponentInstall(appDir));
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
                        progress.Value = Math.Max(0, Math.Min(100, value));
                        status.Text = message;
                    }),
                    message => PostUi(() => AppendLog(message)));
                string[] remaining = ComponentInstaller.MissingComponents(appDir);
                PostUi(() =>
                {
                    SetBusy(false);
                    if (remaining.Length == 0)
                    {
                        progress.Value = 100;
                        status.Text = "运行组件安装完成，可以开始下载。";
                        AppendLog("组件安装完成：yt-dlp、FFmpeg、FFprobe、Deno 均已就绪。");
                    }
                    else status.Text = "仍缺少组件：" + String.Join("、", remaining);
                });
            }
            catch (Exception ex)
            {
                PostUi(() =>
                {
                    SetBusy(false);
                    status.Text = "组件安装失败；已保留成功安装的组件，请查看运行记录后重试。";
                    AppendLog("安装失败：" + ComponentInstaller.FriendlyNetworkMessage(ex));
                });
            }
        }

        private void UpdateClicked(object sender, EventArgs e)
        {
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] missing = ComponentInstaller.MissingComponents(appDir);
            if (missing.Length > 0)
            {
                DialogResult install = MessageBox.Show(this,
                    "当前缺少：\n\n" + String.Join("\n", missing) +
                    "\n\n是否从官方线路下载并安装？",
                    "运行组件不完整", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (install == DialogResult.Yes) StartComponentInstall();
                return;
            }
            string ytDlp = Path.Combine(appDir, "yt-dlp.exe");
            if (MessageBox.Show(this,
                "检查完成：yt-dlp、FFmpeg、FFprobe 和 Deno 均已安装。\n\n是否继续使用 yt-dlp 官方自更新功能检查下载核心的新版本？本界面、FFmpeg 和 Deno 不会因此更新。",
                "运行组件完整", MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes)
            {
                status.Text = "运行组件完整。";
                return;
            }

            logBox.Clear();
            progress.Value = 0;
            status.Text = "正在检查 yt-dlp 更新…";
            SetBusy(true, false);
            var worker = new Thread(() => RunCoreUpdate(ytDlp, appDir));
            worker.IsBackground = true;
            worker.Start();
        }

        private void RunCoreUpdate(string ytDlp, string appDir)
        {
            try
            {
                var info = new ProcessStartInfo(ytDlp, "--ignore-config -U");
                info.WorkingDirectory = appDir;
                info.UseShellExecute = false;
                info.CreateNoWindow = true;
                info.RedirectStandardOutput = true;
                info.RedirectStandardError = true;
                info.StandardOutputEncoding = Encoding.Default;
                info.StandardErrorEncoding = Encoding.Default;
                ConfigureProcessEnvironment(info, appDir);
                using (var process = new Process())
                {
                    process.StartInfo = info;
                    process.OutputDataReceived += (s, e) => HandleUpdateLine(e.Data);
                    process.ErrorDataReceived += (s, e) => HandleUpdateLine(e.Data);
                    if (!process.Start()) throw new InvalidOperationException("yt-dlp 更新程序无法启动。");
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    process.WaitForExit();
                    int code = process.ExitCode;
                    PostUi(() =>
                    {
                        SetBusy(false);
                        status.Text = code == 0
                            ? "更新检查完成；下次下载会使用当前目录中的 yt-dlp 核心。"
                            : "更新失败；请查看运行记录，确认网络和目录写入权限。";
                    });
                }
            }
            catch (Exception ex)
            {
                PostUi(() => { SetBusy(false); status.Text = "更新失败：" + ex.Message; AppendLog(ex.ToString()); });
            }
        }

        private void HandleUpdateLine(string line)
        {
            if (String.IsNullOrEmpty(line)) return;
            PostUi(() => { AppendLog(line); status.Text = "正在检查或下载 yt-dlp 更新…"; });
        }

        private void CancelClicked(object sender, EventArgs e)
        {
            cancelling = true;
            cancelButton.Enabled = false;
            status.Text = "正在取消…";
            try { KillProcessTree(currentProcess); }
            catch (Exception ex) { AppendLog("取消失败：" + ex.Message); }
        }

        private static void KillProcessTree(Process process)
        {
            if (process == null || process.HasExited) return;
            var info = new ProcessStartInfo("taskkill.exe", "/PID " + process.Id + " /T /F");
            info.UseShellExecute = false;
            info.CreateNoWindow = true;
            using (var killer = Process.Start(info))
            {
                if (killer == null) throw new InvalidOperationException("无法启动 taskkill。");
                killer.WaitForExit(5000);
            }
        }

        private void OpenClicked(object sender, EventArgs e)
        {
            try
            {
                string folder = lastFolder ?? Path.Combine(folderBox.Text.Trim(), ModeSubfolder(selectedMode));
                if (Directory.Exists(folder)) Process.Start("explorer.exe", Quote(folder));
                else MessageBox.Show(this, "保存目录尚不存在。", "无法打开", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "无法打开目录：" + ex.Message, "目录错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
