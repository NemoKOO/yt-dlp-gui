using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace YtDlpGuiMvp
{
    internal static class ArgumentTests
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length == 1 && args[0] == "--ui-snapshot") return CaptureUiSnapshot();
            if (args.Length == 1 && args[0] == "--component-test")
            {
                try { TestComponentInstaller(); return 0; }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex.GetType().FullName + ": " + ex.Message);
                    return 1;
                }
            }
            if (args.Length == 2 && args[0] == "--install-components")
            {
                Directory.CreateDirectory(args[1]);
                ComponentInstaller.InstallMissing(args[1],
                    (percent, message) => Console.WriteLine(percent + "% " + message),
                    message => Console.WriteLine(message));
                return ComponentInstaller.MissingComponents(args[1]).Length == 0 ? 0 : 1;
            }
            using (var form = new MainForm(false))
            {
                Check(Program.IsWindows7OrOlder(PlatformID.Win32NT, new Version(6, 1)), "Windows 7 應收到兼容性提示");
                Check(!Program.IsWindows7OrOlder(PlatformID.Win32NT, new Version(6, 2)), "Windows 8 不應誤判爲 Windows 7");
                Check(!Program.IsWindows7OrOlder(PlatformID.Win32NT, new Version(10, 0)), "Windows 10 不應誤判爲 Windows 7");
                var type = typeof(MainForm);
                var build = type.GetMethod("BuildArguments", BindingFlags.NonPublic | BindingFlags.Instance);
                var selectMode = type.GetMethod("btnSelectMode_EventHandler_Click_SelectMode", BindingFlags.NonPublic | BindingFlags.Instance);
                var subtitleLanguage = (ComboBox)type.GetField("cmbSubtitleLanguage", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(form);
                var videoResolution = (ComboBox)type.GetField("cmbVideoResolution", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(form);
                var modeButtons = (Button[])type.GetField("btnSelectMode", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(form);
                var firefox = (CheckBox)type.GetField("chkFirefox", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(form);
                var playlist = (CheckBox)type.GetField("chkPlaylist", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(form);
                const string url = "https://example.com/video?a=1&b=2";
                const string folder = @"C:\影片 下載\";
                const string appDir = @"C:\工具\";

                string video = (string)build.Invoke(form, new object[] { url, folder, appDir });
                Check(video.Contains("--no-playlist") && video.Contains("-t mp4") && video.Contains("\"https://example.com/video?a=1&b=2\""), "影片參數");
                Check(video.Contains("--no-overwrites") && video.Contains("--no-post-overwrites") && video.Contains("--ignore-config"), "保護已有文件參數");
                Check(!video.Contains("-S \"res:"), "自動畵質保持原有參數");
                Check(video.Contains("\"C:\\影片 下載\\\\\""), "路徑末尾反斜杠轉義");

                int[] expectedHeights = { 360, 480, 720, 1080, 1440, 2160 };
                Check(videoResolution.Items.Count == 7 && videoResolution.Enabled, "影片解析度選項可見且可選");
                for (int i = 0; i < expectedHeights.Length; i++)
                {
                    videoResolution.SelectedIndex = i + 1;
                    string height = expectedHeights[i].ToString();
                    string limited = (string)build.Invoke(form, new object[] { url, folder, appDir });
                    Check(limited.Contains("-S \"res:" + height + "\""), "優先 " + height + "p 畵質排序");
                    Check(limited.Contains("%(resolution)s"), "不同實際解析度不會共用文件名");
                    Check(limited.Contains("-t mp4"), "畵質選择保持 MP4 輸出");
                }

                Check(modeButtons.Length == 3 && modeButtons[0].BackColor != modeButtons[1].BackColor, "下載類型按鈕選中狀態");
                Check(modeButtons[0].Text == "影片 MP4" && modeButtons[1].Text == "音訊 MP3" && modeButtons[2].Text == "字幕 SRT", "三个按鈕必須有明確文字");
                Check(modeButtons[0].Parent is FlowLayoutPanel && modeButtons[0].TextAlign == System.Drawing.ContentAlignment.MiddleCenter && modeButtons[0].Width >= 100, "按鈕佈局及文字繪製參數");
                selectMode.Invoke(form, new object[] { 1 });
                Check(!videoResolution.Enabled, "音訊模式禁用解析度");
                firefox.Checked = true;
                playlist.Checked = true;
                string audio = (string)build.Invoke(form, new object[] { url, folder, appDir });
                Check(audio.Contains("--yes-playlist") && audio.Contains("--cookies-from-browser firefox") && audio.Contains("-t mp3"), "音訊、Firefox 和列表參數");
                Check(!audio.Contains("-S \"res:"), "音訊模式不帶影片畵質參數");

                selectMode.Invoke(form, new object[] { 2 });
                Check(!videoResolution.Enabled, "字幕模式禁用解析度");
                string subtitles = (string)build.Invoke(form, new object[] { url, folder, appDir });
                Check(subtitles.Contains("--skip-download") && subtitles.Contains("--sub-langs \"^zh-Hans$\"") && subtitles.Contains("--convert-subs srt"), "單一简體字幕參數");
                Check(!subtitles.Contains("-S \"res:"), "字幕模式不帶影片畵質參數");
                Check(!subtitles.Contains("zh.*,en.*"), "不能批量請求翻譯字幕");
                subtitleLanguage.SelectedIndex = 1;
                Check(((string)build.Invoke(form, new object[] { url, folder, appDir })).Contains("--sub-langs \"^zh-Hant$\""), "單一繁體字幕參數");
                subtitleLanguage.SelectedIndex = 2;
                Check(((string)build.Invoke(form, new object[] { url, folder, appDir })).Contains("--sub-langs \"^en$\""), "單一英文字幕參數");

                var extract = type.GetMethod("ExtractPreferredUrl", BindingFlags.NonPublic | BindingFlags.Static);
                string share = "7.61 Q@K.JV kcN:/ 06/28 :2pm 今天粤菜厨師揭秘楊枝甘露！ https://v.douyin.com/59nAEfuhXCY/ 複製此鏈接，打開抖音觀看影片！";
                Check((string)extract.Invoke(null, new object[] { share }) == "https://v.douyin.com/59nAEfuhXCY/", "分享文案提取網址");
                Check((string)extract.Invoke(null, new object[] { "[https://v.douyin.com/test/](https://v.douyin.com/test/)" }) == "https://v.douyin.com/test/", "Markdown 鏈接提取");
                Check((string)extract.Invoke(null, new object[] { "網址 https://youtu.be/test?x=1&y=2。" }) == "https://youtu.be/test?x=1&y=2", "網址末尾標點與查詢參數");
                Check((string)extract.Invoke(null, new object[] { "舊網址 https://youtu.be/old 新網址 https://youtu.be/new" }) == "https://youtu.be/new", "換影片時優先使用最後粘貼的新網址");
                Check(extract.Invoke(null, new object[] { "沒有網址" }) == null, "無網址識別");

                var linkInput = (ReplaceOnPasteTextBox)type.GetField("txtUrl", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(form);
                linkInput.Text = "舊網址 https://youtu.be/old";
                linkInput.SelectionStart = linkInput.TextLength;
                linkInput.SelectionLength = 0;
                linkInput.SelectExistingTextForPaste();
                Check(linkInput.SelectionStart == 0 && linkInput.SelectionLength == linkInput.TextLength, "粘貼前自動選中並替換舊内容");

                var subfolder = type.GetMethod("ModeSubfolder", BindingFlags.NonPublic | BindingFlags.Static);
                Check((string)subfolder.Invoke(null, new object[] { 0 }) == "mp4", "影片目録");
                Check((string)subfolder.Invoke(null, new object[] { 1 }) == "mp3", "音訊目録");
                Check((string)subfolder.Invoke(null, new object[] { 2 }) == "subtitles", "字幕目録");

                var isDouyin = type.GetMethod("IsDouyinUrl", BindingFlags.NonPublic | BindingFlags.Static);
                Check((bool)isDouyin.Invoke(null, new object[] { "https://v.douyin.com/GPZN9WJ-R6o/" }), "抖音短鏈接識別");
                Check(!(bool)isDouyin.Invoke(null, new object[] { "https://notdouyin.com/video" }), "抖音域名邊界");
                type.GetField("isDouyinDownload", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(form, true);
                var friendly = type.GetMethod("FriendlyError", BindingFlags.NonPublic | BindingFlags.Instance);
                firefox.Checked = false;
                Check(((string)friendly.Invoke(form, new object[] { "ERROR: Fresh cookies (not necessarily logged in) are needed" })).Contains("新鮮 Cookies"), "無 Cookies 抖音提示");
                firefox.Checked = true;
                Check(((string)friendly.Invoke(form, new object[] { "ERROR: Fresh cookies (not necessarily logged in) are needed" })).Contains("站點接口"), "有 Cookies 抖音提示");
                Check(((string)friendly.Invoke(form, new object[] { "ERROR: HTTP Error 429: Too Many Requests" })).Contains("限流"), "字幕限流提示");
                TestSubtitleConversion(form, type);
                TestComponentInstaller();
            }
            // Drain callbacks posted by the subtitle test after its form has been disposed.
            Application.DoEvents();
            TestRedirectedEncoding();
            Console.WriteLine("參數、網址提取、目録、字幕和編碼測試通過");
            return 0;
        }

        private static void TestSubtitleConversion(MainForm form, Type type)
        {
            string ffmpegPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");
            if (!File.Exists(ffmpegPath))
            {
                Console.WriteLine("跳過 VTT→SRT 集成測試：未安裝 ffmpeg.exe");
                return;
            }
            string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "subtitle-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            string vtt = Path.Combine(folder, "中文樣例.vtt");
            string srt = Path.Combine(folder, "中文樣例.srt");
            string oldVtt = Path.Combine(folder, "原有字幕.vtt");
            string oldSrt = Path.Combine(folder, "原有字幕.srt");
            try
            {
                var capture = type.GetMethod("CaptureSubtitles", BindingFlags.NonPublic | BindingFlags.Static);
                object before = capture.Invoke(null, new object[] { folder });
                File.WriteAllText(vtt, "WEBVTT\n\n00:00:01.000 --> 00:00:02.000\n你好，字幕\n", new UTF8Encoding(false));
                var convert = type.GetMethod("ConvertNewVttFiles", BindingFlags.NonPublic | BindingFlags.Instance);
                object[] arguments = { folder, before, ffmpegPath, 0, 0 };
                convert.Invoke(form, arguments);
                Check((int)arguments[3] == 1 && (int)arguments[4] == 0, "VTT 轉 SRT 計數");
                Check(File.Exists(srt) && !File.Exists(vtt), "只保留 SRT");
                string content = File.ReadAllText(srt, Encoding.UTF8);
                Check(content.Contains("00:00:01,000") && content.Contains("你好，字幕"), "SRT 時間戳和中文内容");

                File.WriteAllText(oldVtt, "WEBVTT\n\n00:00:03.000 --> 00:00:04.000\n原有内容\n", new UTF8Encoding(false));
                object existingBefore = capture.Invoke(null, new object[] { folder });
                object[] existingArguments = { folder, existingBefore, ffmpegPath, 0, 0 };
                convert.Invoke(form, existingArguments);
                Check((int)existingArguments[3] == 1 && File.Exists(oldVtt) && File.Exists(oldSrt), "補轉原有 VTT 不刪除原文件");
            }
            finally
            {
                if (File.Exists(vtt)) File.Delete(vtt);
                if (File.Exists(srt)) File.Delete(srt);
                if (File.Exists(oldVtt)) File.Delete(oldVtt);
                if (File.Exists(oldSrt)) File.Delete(oldSrt);
                Directory.Delete(folder);
            }
        }

        private static int CaptureUiSnapshot()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (var form = new MainForm(false))
            {
                form.Show();
                Application.DoEvents();
                using (var image = new Bitmap(form.Width, form.Height))
                {
                    form.DrawToBitmap(image, new Rectangle(0, 0, image.Width, image.Height));
                    image.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ui-preview-v0.6.1.png"));
                }
                form.Close();
            }
            Application.DoEvents();
            Console.WriteLine("界面快照已保存");
            return 0;
        }

        private static void TestRedirectedEncoding()
        {
            string ytDlp = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "yt-dlp.exe");
            if (!File.Exists(ytDlp))
            {
                Console.WriteLine("跳過 yt-dlp 中文管道集成測試：未安裝 yt-dlp.exe");
                return;
            }
            var info = new ProcessStartInfo(ytDlp, "-v --ignore-config \"badproto:文件名\"");
            info.UseShellExecute = false;
            info.CreateNoWindow = true;
            info.RedirectStandardOutput = true;
            info.RedirectStandardError = true;
            info.StandardOutputEncoding = Encoding.Default;
            info.StandardErrorEncoding = Encoding.Default;
            string runtimeTemp = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".runtime-temp");
            Directory.CreateDirectory(runtimeTemp);
            info.EnvironmentVariables["TEMP"] = runtimeTemp;
            info.EnvironmentVariables["TMP"] = runtimeTemp;
            using (var process = Process.Start(info))
            {
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                Check((output + error).Contains("文件名"), "Windows 重定向中文解碼");
                if (Encoding.Default.CodePage == 936)
                    Check((output + error).Contains("out gbk"), "yt-dlp GBK 管道識別");
            }
        }

        private static void TestComponentInstaller()
        {
            const string expected = "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";
            string parsed = ComponentInstaller.ParseExpectedHash(expected.ToLowerInvariant() + "  *yt-dlp.exe\n", "yt-dlp.exe");
            Check(parsed == expected, "官方 SHA-256 淸單解析");
            string denoFormat = "Algorithm : SHA256\r\nHash : " + expected + "\r\nPath : C:\\build\\deno-x86_64-pc-windows-msvc.zip\r\n";
            Check(ComponentInstaller.ParseExpectedHash(denoFormat, "deno-x86_64-pc-windows-msvc.zip") == expected, "Deno PowerShell 校驗格式解析");
            Check(ComponentInstaller.YtDlpUrl.StartsWith("https://") && ComponentInstaller.YtDlpBackupUrl.StartsWith("https://"), "yt-dlp 官方備用綫路");
            Check(ComponentInstaller.FfmpegUrl.Contains("yt-dlp/FFmpeg-Builds") && ComponentInstaller.FfmpegBackupUrl.Contains("BtbN/FFmpeg-Builds"), "FFmpeg 官方上游備用綫路");
            Check(ComponentInstaller.DenoUrl.Contains("github.com/denoland") && ComponentInstaller.DenoBackupUrl.Contains("dl.deno.land"), "Deno 官方備用綫路");
            Check(ComponentInstaller.FriendlyNetworkMessage(new System.Net.WebException("timeout", System.Net.WebExceptionStatus.Timeout)).Contains("超時"), "網路錯誤中文提示");

            string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "component-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            string archivePath = Path.Combine(folder, "sample.zip");
            string sourceExe = Path.Combine(folder, "source.exe");
            try
            {
                byte[] data = new byte[70000];
                data[0] = (byte)'M';
                data[1] = (byte)'Z';
                File.WriteAllBytes(sourceExe, data);
                using (ZipArchive archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
                {
                    ZipArchiveEntry entry = archive.CreateEntry("ffmpeg-test/bin/ffmpeg.exe");
                    using (Stream input = File.OpenRead(sourceExe))
                    using (Stream output = entry.Open()) input.CopyTo(output);
                }
                string extracted = ComponentInstaller.ExtractNamedExecutable(archivePath, "ffmpeg.exe", folder);
                ComponentInstaller.ValidateExecutable(extracted, "ffmpeg.exe");
                Check(File.Exists(extracted) && new FileInfo(extracted).Length == data.Length, "組件壓縮包安全提取");
                string[] missing = ComponentInstaller.MissingComponents(folder);
                Check(missing.Length == 4, "空目録組件检查");
            }
            finally
            {
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
            }
        }

        private static void Check(bool condition, string name)
        {
            if (!condition) throw new Exception(name + " 測試失败");
        }
    }
}
