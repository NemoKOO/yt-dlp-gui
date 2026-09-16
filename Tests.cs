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
                var type = typeof(MainForm);
                var build = type.GetMethod("BuildArguments", BindingFlags.NonPublic | BindingFlags.Instance);
                var selectMode = type.GetMethod("SelectMode", BindingFlags.NonPublic | BindingFlags.Instance);
                var subtitleLanguage = (ComboBox)type.GetField("subtitleLanguageBox", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(form);
                var modeButtons = (Button[])type.GetField("modeButtons", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(form);
                var firefox = (CheckBox)type.GetField("firefoxBox", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(form);
                var playlist = (CheckBox)type.GetField("playlistBox", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(form);
                const string url = "https://example.com/video?a=1&b=2";
                const string folder = @"C:\视频 下载\";
                const string appDir = @"C:\工具\";

                string video = (string)build.Invoke(form, new object[] { url, folder, appDir });
                Check(video.Contains("--no-playlist") && video.Contains("-t mp4") && video.Contains("\"https://example.com/video?a=1&b=2\""), "视频参数");
                Check(video.Contains("--no-overwrites") && video.Contains("--no-post-overwrites") && video.Contains("--ignore-config"), "保护已有文件参数");
                Check(video.Contains("\"C:\\视频 下载\\\\\""), "路径末尾反斜杠转义");

                Check(modeButtons.Length == 3 && modeButtons[0].BackColor != modeButtons[1].BackColor, "下载类型按钮选中状态");
                Check(modeButtons[0].Text == "视频 MP4" && modeButtons[1].Text == "音频 MP3" && modeButtons[2].Text == "字幕 SRT", "三个按钮必须有明确文字");
                Check(modeButtons[0].Parent is FlowLayoutPanel && modeButtons[0].TextAlign == System.Drawing.ContentAlignment.MiddleCenter && modeButtons[0].Width >= 100, "按钮布局及文字绘制参数");
                selectMode.Invoke(form, new object[] { 1 });
                firefox.Checked = true;
                playlist.Checked = true;
                string audio = (string)build.Invoke(form, new object[] { url, folder, appDir });
                Check(audio.Contains("--yes-playlist") && audio.Contains("--cookies-from-browser firefox") && audio.Contains("-t mp3"), "音频、Firefox 和列表参数");

                selectMode.Invoke(form, new object[] { 2 });
                string subtitles = (string)build.Invoke(form, new object[] { url, folder, appDir });
                Check(subtitles.Contains("--skip-download") && subtitles.Contains("--sub-langs \"^zh-Hans$\"") && subtitles.Contains("--convert-subs srt"), "单一简体字幕参数");
                Check(!subtitles.Contains("zh.*,en.*"), "不能批量请求翻译字幕");
                subtitleLanguage.SelectedIndex = 1;
                Check(((string)build.Invoke(form, new object[] { url, folder, appDir })).Contains("--sub-langs \"^zh-Hant$\""), "单一繁体字幕参数");
                subtitleLanguage.SelectedIndex = 2;
                Check(((string)build.Invoke(form, new object[] { url, folder, appDir })).Contains("--sub-langs \"^en$\""), "单一英文字幕参数");

                var extract = type.GetMethod("ExtractFirstUrl", BindingFlags.NonPublic | BindingFlags.Static);
                string share = "7.61 Q@K.JV kcN:/ 06/28 :2pm 今天粤菜厨师揭秘杨枝甘露！ https://v.douyin.com/59nAEfuhXCY/ 复制此链接，打开抖音观看视频！";
                Check((string)extract.Invoke(null, new object[] { share }) == "https://v.douyin.com/59nAEfuhXCY/", "分享文案提取网址");
                Check((string)extract.Invoke(null, new object[] { "[https://v.douyin.com/test/](https://v.douyin.com/test/)" }) == "https://v.douyin.com/test/", "Markdown 链接提取");
                Check((string)extract.Invoke(null, new object[] { "网址 https://youtu.be/test?x=1&y=2。" }) == "https://youtu.be/test?x=1&y=2", "网址末尾标点与查询参数");
                Check(extract.Invoke(null, new object[] { "没有网址" }) == null, "无网址识别");

                var subfolder = type.GetMethod("ModeSubfolder", BindingFlags.NonPublic | BindingFlags.Static);
                Check((string)subfolder.Invoke(null, new object[] { 0 }) == "mp4", "视频目录");
                Check((string)subfolder.Invoke(null, new object[] { 1 }) == "mp3", "音频目录");
                Check((string)subfolder.Invoke(null, new object[] { 2 }) == "subtitles", "字幕目录");

                var isDouyin = type.GetMethod("IsDouyinUrl", BindingFlags.NonPublic | BindingFlags.Static);
                Check((bool)isDouyin.Invoke(null, new object[] { "https://v.douyin.com/GPZN9WJ-R6o/" }), "抖音短链接识别");
                Check(!(bool)isDouyin.Invoke(null, new object[] { "https://notdouyin.com/video" }), "抖音域名边界");
                type.GetField("isDouyinDownload", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(form, true);
                var friendly = type.GetMethod("FriendlyError", BindingFlags.NonPublic | BindingFlags.Instance);
                firefox.Checked = false;
                Check(((string)friendly.Invoke(form, new object[] { "ERROR: Fresh cookies (not necessarily logged in) are needed" })).Contains("新鲜 Cookies"), "无 Cookies 抖音提示");
                firefox.Checked = true;
                Check(((string)friendly.Invoke(form, new object[] { "ERROR: Fresh cookies (not necessarily logged in) are needed" })).Contains("站点接口"), "有 Cookies 抖音提示");
                Check(((string)friendly.Invoke(form, new object[] { "ERROR: HTTP Error 429: Too Many Requests" })).Contains("限流"), "字幕限流提示");
                TestSubtitleConversion(form, type);
                TestComponentInstaller();
            }
            // Drain callbacks posted by the subtitle test after its form has been disposed.
            Application.DoEvents();
            TestRedirectedEncoding();
            Console.WriteLine("参数、网址提取、目录、字幕和编码测试通过");
            return 0;
        }

        private static void TestSubtitleConversion(MainForm form, Type type)
        {
            string ffmpegPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");
            if (!File.Exists(ffmpegPath))
            {
                Console.WriteLine("跳过 VTT→SRT 集成测试：未安装 ffmpeg.exe");
                return;
            }
            string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "subtitle-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            string vtt = Path.Combine(folder, "中文样例.vtt");
            string srt = Path.Combine(folder, "中文样例.srt");
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
                Check((int)arguments[3] == 1 && (int)arguments[4] == 0, "VTT 转 SRT 计数");
                Check(File.Exists(srt) && !File.Exists(vtt), "只保留 SRT");
                string content = File.ReadAllText(srt, Encoding.UTF8);
                Check(content.Contains("00:00:01,000") && content.Contains("你好，字幕"), "SRT 时间戳和中文内容");

                File.WriteAllText(oldVtt, "WEBVTT\n\n00:00:03.000 --> 00:00:04.000\n原有内容\n", new UTF8Encoding(false));
                object existingBefore = capture.Invoke(null, new object[] { folder });
                object[] existingArguments = { folder, existingBefore, ffmpegPath, 0, 0 };
                convert.Invoke(form, existingArguments);
                Check((int)existingArguments[3] == 1 && File.Exists(oldVtt) && File.Exists(oldSrt), "补转原有 VTT 不删除原文件");
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
                    image.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ui-preview-v0.5.png"));
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
                Console.WriteLine("跳过 yt-dlp 中文管道集成测试：未安装 yt-dlp.exe");
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
                Check((output + error).Contains("文件名"), "Windows 重定向中文解码");
                if (Encoding.Default.CodePage == 936)
                    Check((output + error).Contains("out gbk"), "yt-dlp GBK 管道识别");
            }
        }

        private static void TestComponentInstaller()
        {
            const string expected = "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";
            string parsed = ComponentInstaller.ParseExpectedHash(expected.ToLowerInvariant() + "  *yt-dlp.exe\n", "yt-dlp.exe");
            Check(parsed == expected, "官方 SHA-256 清单解析");
            string denoFormat = "Algorithm : SHA256\r\nHash : " + expected + "\r\nPath : C:\\build\\deno-x86_64-pc-windows-msvc.zip\r\n";
            Check(ComponentInstaller.ParseExpectedHash(denoFormat, "deno-x86_64-pc-windows-msvc.zip") == expected, "Deno PowerShell 校验格式解析");
            Check(ComponentInstaller.YtDlpUrl.StartsWith("https://") && ComponentInstaller.YtDlpBackupUrl.StartsWith("https://"), "yt-dlp 官方备用线路");
            Check(ComponentInstaller.FfmpegUrl.Contains("yt-dlp/FFmpeg-Builds") && ComponentInstaller.FfmpegBackupUrl.Contains("BtbN/FFmpeg-Builds"), "FFmpeg 官方上游备用线路");
            Check(ComponentInstaller.DenoUrl.Contains("github.com/denoland") && ComponentInstaller.DenoBackupUrl.Contains("dl.deno.land"), "Deno 官方备用线路");
            Check(ComponentInstaller.FriendlyNetworkMessage(new System.Net.WebException("timeout", System.Net.WebExceptionStatus.Timeout)).Contains("超时"), "网络错误中文提示");

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
                Check(File.Exists(extracted) && new FileInfo(extracted).Length == data.Length, "组件压缩包安全提取");
                string[] missing = ComponentInstaller.MissingComponents(folder);
                Check(missing.Length == 4, "空目录组件检查");
            }
            finally
            {
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
            }
        }

        private static void Check(bool condition, string name)
        {
            if (!condition) throw new Exception(name + " 测试失败");
        }
    }
}
