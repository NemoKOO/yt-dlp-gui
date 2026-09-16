using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace YtDlpGuiMvp
{
    internal static class ComponentInstaller
    {
        internal const string YtDlpUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
        internal const string YtDlpChecksumsUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/SHA2-256SUMS";
        internal const string YtDlpBackupUrl = "https://github.com/yt-dlp/yt-dlp/releases/download/2026.08.19/yt-dlp.exe";
        internal const string YtDlpBackupChecksumsUrl = "https://github.com/yt-dlp/yt-dlp/releases/download/2026.08.19/SHA2-256SUMS";
        internal const string FfmpegUrl = "https://github.com/yt-dlp/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip";
        internal const string FfmpegChecksumsUrl = "https://github.com/yt-dlp/FFmpeg-Builds/releases/download/latest/checksums.sha256";
        internal const string FfmpegBackupUrl = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip";
        internal const string FfmpegBackupChecksumsUrl = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/checksums.sha256";
        internal const string DenoUrl = "https://github.com/denoland/deno/releases/latest/download/deno-x86_64-pc-windows-msvc.zip";
        internal const string DenoChecksumsUrl = "https://github.com/denoland/deno/releases/latest/download/deno-x86_64-pc-windows-msvc.zip.sha256sum";
        internal const string DenoBackupUrl = "https://dl.deno.land/release/v2.9.6/deno-x86_64-pc-windows-msvc.zip";
        internal const string DenoBackupChecksumsUrl = "https://dl.deno.land/release/v2.9.6/deno-x86_64-pc-windows-msvc.zip.sha256sum";

        private sealed class DownloadSource
        {
            internal readonly string Name;
            internal readonly string FileUrl;
            internal readonly string ChecksumsUrl;

            internal DownloadSource(string name, string fileUrl, string checksumsUrl)
            {
                Name = name;
                FileUrl = fileUrl;
                ChecksumsUrl = checksumsUrl;
            }
        }

        internal static string[] MissingComponents(string appDir)
        {
            var result = new List<string>();
            if (!File.Exists(Path.Combine(appDir, "yt-dlp.exe"))) result.Add("yt-dlp.exe");
            if (!File.Exists(Path.Combine(appDir, "ffmpeg.exe"))) result.Add("ffmpeg.exe");
            if (!File.Exists(Path.Combine(appDir, "ffprobe.exe"))) result.Add("ffprobe.exe");
            if (!File.Exists(Path.Combine(appDir, "deno.exe"))) result.Add("deno.exe");
            return result.ToArray();
        }

        internal static void InstallMissing(string appDir, Action<int, string> report, Action<string> log)
        {
            if (!Directory.Exists(appDir)) throw new DirectoryNotFoundException("程序目录不存在：" + appDir);
            string tempDir = Path.Combine(appDir, ".component-cache-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                if (!File.Exists(Path.Combine(appDir, "yt-dlp.exe")))
                {
                    report(0, "正在下载 yt-dlp…");
                    string downloaded = Path.Combine(tempDir, "yt-dlp.exe");
                    DownloadAndVerify(new[] {
                            new DownloadSource("yt-dlp 官方最新线路", YtDlpUrl, YtDlpChecksumsUrl),
                            new DownloadSource("yt-dlp 官方稳定版备用线路", YtDlpBackupUrl, YtDlpBackupChecksumsUrl)
                        }, "yt-dlp.exe", downloaded,
                        p => report(p, "正在下载 yt-dlp：" + p + "%"), log);
                    ValidateExecutable(downloaded, "yt-dlp.exe");
                    InstallFile(downloaded, Path.Combine(appDir, "yt-dlp.exe"));
                    log("yt-dlp.exe 已安装并通过 SHA-256 校验。");
                }

                bool needsFfmpeg = !File.Exists(Path.Combine(appDir, "ffmpeg.exe")) ||
                                    !File.Exists(Path.Combine(appDir, "ffprobe.exe"));
                if (needsFfmpeg)
                {
                    report(0, "正在下载 FFmpeg（文件较大，请耐心等待）…");
                    string archive = Path.Combine(tempDir, "ffmpeg-master-latest-win64-gpl.zip");
                    DownloadAndVerify(new[] {
                            new DownloadSource("yt-dlp FFmpeg 官方构建线路", FfmpegUrl, FfmpegChecksumsUrl),
                            new DownloadSource("FFmpeg-Builds 上游备用线路", FfmpegBackupUrl, FfmpegBackupChecksumsUrl)
                        }, Path.GetFileName(archive), archive,
                        p => report(p, "正在下载 FFmpeg：" + p + "%"), log);
                    string ffmpeg = ExtractNamedExecutable(archive, "ffmpeg.exe", tempDir);
                    string ffprobe = ExtractNamedExecutable(archive, "ffprobe.exe", tempDir);
                    ValidateExecutable(ffmpeg, "ffmpeg.exe");
                    ValidateExecutable(ffprobe, "ffprobe.exe");
                    InstallFile(ffmpeg, Path.Combine(appDir, "ffmpeg.exe"));
                    InstallFile(ffprobe, Path.Combine(appDir, "ffprobe.exe"));
                    log("ffmpeg.exe 和 ffprobe.exe 已安装，压缩包已通过 SHA-256 校验。");
                }

                if (!File.Exists(Path.Combine(appDir, "deno.exe")))
                {
                    report(0, "正在下载 Deno…");
                    string archive = Path.Combine(tempDir, "deno-x86_64-pc-windows-msvc.zip");
                    DownloadAndVerify(new[] {
                            new DownloadSource("Deno 官方 GitHub 线路", DenoUrl, DenoChecksumsUrl),
                            new DownloadSource("Deno 官方 CDN 备用线路", DenoBackupUrl, DenoBackupChecksumsUrl)
                        }, Path.GetFileName(archive), archive,
                        p => report(p, "正在下载 Deno：" + p + "%"), log);
                    string deno = ExtractNamedExecutable(archive, "deno.exe", tempDir);
                    ValidateExecutable(deno, "deno.exe");
                    InstallFile(deno, Path.Combine(appDir, "deno.exe"));
                    log("deno.exe 已安装并通过 SHA-256 校验。");
                }
            }
            finally
            {
                try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); }
                catch { }
            }
        }

        internal static string ParseExpectedHash(string checksumText, string fileName)
        {
            if (String.IsNullOrWhiteSpace(checksumText)) throw new InvalidDataException("校验文件为空。");
            Match powershellHash = Regex.Match(checksumText, "(?im)^Hash\\s*:\\s*([0-9a-fA-F]{64})\\s*$");
            Match powershellPath = Regex.Match(checksumText, "(?im)^Path\\s*:\\s*(.+?)\\s*$");
            if (powershellHash.Success && powershellPath.Success &&
                Path.GetFileName(powershellPath.Groups[1].Value.Trim()).Equals(fileName, StringComparison.OrdinalIgnoreCase))
                return powershellHash.Groups[1].Value.ToUpperInvariant();
            foreach (string rawLine in checksumText.Replace("\r", "").Split('\n'))
            {
                string line = rawLine.Trim();
                Match match = Regex.Match(line, "^([0-9a-fA-F]{64})\\s+\\*?(.+)$");
                if (!match.Success) continue;
                string listedName = Path.GetFileName(match.Groups[2].Value.Trim());
                if (listedName.Equals(fileName, StringComparison.OrdinalIgnoreCase))
                    return match.Groups[1].Value.ToUpperInvariant();
            }
            throw new InvalidDataException("官方校验清单中没有找到 " + fileName + "。请稍后重试。");
        }

        internal static string ComputeSha256(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(stream);
                var text = new StringBuilder(hash.Length * 2);
                foreach (byte value in hash) text.Append(value.ToString("X2"));
                return text.ToString();
            }
        }

        internal static string ExtractNamedExecutable(string archivePath, string executableName, string outputDir)
        {
            string outputPath = Path.Combine(outputDir, Guid.NewGuid().ToString("N") + "-" + executableName);
            using (var archive = ZipFile.OpenRead(archivePath))
            {
                ZipArchiveEntry found = null;
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (!Path.GetFileName(entry.FullName).Equals(executableName, StringComparison.OrdinalIgnoreCase)) continue;
                    if (found != null) throw new InvalidDataException("压缩包中存在多个 " + executableName + "，已停止安装。");
                    found = entry;
                }
                if (found == null || found.Length <= 0) throw new InvalidDataException("压缩包中没有找到 " + executableName + "。");
                using (Stream input = found.Open())
                using (var output = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    input.CopyTo(output);
            }
            return outputPath;
        }

        internal static void ValidateExecutable(string path, string displayName)
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length < 65536) throw new InvalidDataException(displayName + " 文件过小或不存在。");
            using (var stream = File.OpenRead(path))
            {
                if (stream.ReadByte() != 'M' || stream.ReadByte() != 'Z')
                    throw new InvalidDataException(displayName + " 不是有效的 Windows 可执行文件。");
            }
        }

        private static void DownloadAndVerify(DownloadSource[] sources, string fileName,
            string destination, Action<int> progress, Action<string> log)
        {
            Exception lastError = null;
            foreach (DownloadSource source in sources)
            {
                for (int attempt = 1; attempt <= 2; attempt++)
                {
                    try
                    {
                        if (File.Exists(destination)) File.Delete(destination);
                        log("下载线路：" + source.Name + "（第 " + attempt + " 次尝试）");
                        string checksums = DownloadText(source.ChecksumsUrl);
                        string expected = ParseExpectedHash(checksums, fileName);
                        DownloadFile(source.FileUrl, destination, progress);
                        string actual = ComputeSha256(destination);
                        if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
                            throw new InvalidDataException(fileName + " 的 SHA-256 校验失败。");
                        log(fileName + " SHA-256：" + actual);
                        return;
                    }
                    catch (Exception ex)
                    {
                        lastError = ex;
                        if (File.Exists(destination)) File.Delete(destination);
                        log(source.Name + "失败：" + FriendlyNetworkMessage(ex));
                        if (attempt < 2) System.Threading.Thread.Sleep(1500);
                    }
                }
            }
            throw new InvalidOperationException("所有官方下载线路均失败；未安装 " + fileName + "。" +
                (lastError == null ? "" : " 最后一次错误：" + FriendlyNetworkMessage(lastError)), lastError);
        }

        internal static string FriendlyNetworkMessage(Exception ex)
        {
            var web = ex as WebException;
            if (web != null)
            {
                var response = web.Response as HttpWebResponse;
                if (response != null)
                {
                    int code = (int)response.StatusCode;
                    if (code == 403) return "服务器拒绝访问（HTTP 403，可能是 GitHub 限流）";
                    if (code == 404) return "下载文件不存在（HTTP 404）";
                    return "HTTP " + code + " " + response.StatusDescription;
                }
                if (web.Status == WebExceptionStatus.Timeout) return "连接超时";
                if (web.Status == WebExceptionStatus.NameResolutionFailure) return "域名解析失败";
                if (web.Status == WebExceptionStatus.ConnectFailure) return "无法连接服务器";
            }
            return ex.Message;
        }

        private static string DownloadText(string url)
        {
            var request = CreateRequest(url);
            using (var response = (HttpWebResponse)request.GetResponse())
            using (Stream stream = response.GetResponseStream())
            using (var reader = new StreamReader(stream, Encoding.UTF8, true))
                return reader.ReadToEnd();
        }

        private static void DownloadFile(string url, string destination, Action<int> progress)
        {
            var request = CreateRequest(url);
            using (var response = (HttpWebResponse)request.GetResponse())
            using (Stream input = response.GetResponseStream())
            using (var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                long total = response.ContentLength;
                long received = 0;
                int lastPercent = -1;
                var buffer = new byte[128 * 1024];
                int count;
                while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    output.Write(buffer, 0, count);
                    received += count;
                    int percent = total > 0 ? (int)Math.Min(100, received * 100L / total) : 0;
                    if (percent != lastPercent)
                    {
                        lastPercent = percent;
                        progress(percent);
                    }
                }
                if (received == 0) throw new InvalidDataException("服务器返回了空文件。");
                progress(100);
            }
        }

        private static HttpWebRequest CreateRequest(string url)
        {
            if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("组件只能从 HTTPS 地址下载。");
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.AllowAutoRedirect = true;
            request.UserAgent = "yt-dlp-gui-mvp/0.5";
            request.Timeout = 30000;
            request.ReadWriteTimeout = 30000;
            return request;
        }

        private static void InstallFile(string source, string destination)
        {
            string incoming = destination + ".new";
            if (File.Exists(incoming)) File.Delete(incoming);
            File.Copy(source, incoming, true);
            try
            {
                if (File.Exists(destination))
                {
                    string backup = destination + ".backup";
                    if (File.Exists(backup)) File.Delete(backup);
                    File.Replace(incoming, destination, backup, true);
                    if (File.Exists(backup)) File.Delete(backup);
                }
                else File.Move(incoming, destination);
            }
            finally
            {
                if (File.Exists(incoming)) File.Delete(incoming);
            }
        }
    }
}
