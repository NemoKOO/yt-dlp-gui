#region Namespace Dependencies
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
#endregion

namespace YtDlpGuiMvp
{
    internal static class ComponentInstaller
    {
        internal const string YtDlpUrl                  = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
        internal const string YtDlpChecksumsUrl         = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/SHA2-256SUMS";
        internal const string YtDlpBackupUrl            = "https://github.com/yt-dlp/yt-dlp/releases/download/2026.08.19/yt-dlp.exe";
        internal const string YtDlpBackupChecksumsUrl   = "https://github.com/yt-dlp/yt-dlp/releases/download/2026.08.19/SHA2-256SUMS";
        internal const string FfmpegUrl                 = "https://github.com/yt-dlp/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip";
        internal const string FfmpegChecksumsUrl        = "https://github.com/yt-dlp/FFmpeg-Builds/releases/download/latest/checksums.sha256";
        internal const string FfmpegBackupUrl           = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip";
        internal const string FfmpegBackupChecksumsUrl  = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/checksums.sha256";
        internal const string DenoUrl                   = "https://github.com/denoland/deno/releases/latest/download/deno-x86_64-pc-windows-msvc.zip";
        internal const string DenoChecksumsUrl          = "https://github.com/denoland/deno/releases/latest/download/deno-x86_64-pc-windows-msvc.zip.sha256sum";
        internal const string DenoBackupUrl             = "https://dl.deno.land/release/v2.9.6/deno-x86_64-pc-windows-msvc.zip";
        internal const string DenoBackupChecksumsUrl    = "https://dl.deno.land/release/v2.9.6/deno-x86_64-pc-windows-msvc.zip.sha256sum";

        private sealed class DownloadSource
        {
            internal readonly string Name;
            internal readonly string FileUrl;
            internal readonly string ChecksumsUrl;

            internal DownloadSource(string name, string fileUrl, string checksumsUrl)
            {
                Name            = name;
                FileUrl         = fileUrl;
                ChecksumsUrl    = checksumsUrl;
            }
        }

        internal static string[] MissingComponents(string appDir, Boolean checkEnvPath = false)
        {
            var binaries    = new string[] {"yt-dlp.exe", "ffmpeg.exe", "ffprobe.exe", "deno.exe"};
            var result      = new List<string>();

            foreach (var binary in binaries)
            {
                if (string.Empty == CheckBinaryExist(appDir, binary, checkEnvPath)) result.Add(binary);
            }

         //if (string.Empty == GetEnvironmentPath(appDir, "yt-dlp.exe" )) result.Add("yt-dlp.exe");
         //if (string.Empty == GetEnvironmentPath(appDir, "ffmpeg.exe" )) result.Add("ffmpeg.exe");
         //if (string.Empty == GetEnvironmentPath(appDir, "ffprobe.exe")) result.Add("ffprobe.exe");
         //if (string.Empty == GetEnvironmentPath(appDir, "deno.exe"   )) result.Add("deno.exe");
            return result.ToArray();
        }

        internal static void InstallMissing(string appDir, Action<int, string> report, Action<string> log)
        {
            if (!Directory.Exists(appDir)) throw new DirectoryNotFoundException("程式目録不存在：" + appDir);

            string tempDir = Path.Combine(appDir, ".component-cache-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                if (!File.Exists(Path.Combine(appDir, "yt-dlp.exe")))
                {
                    report(0, "正在下載 yt-dlp…");
                    string downloaded = Path.Combine(tempDir, "yt-dlp.exe");
                    DownloadAndVerify(new[] { new DownloadSource("yt-dlp 官方最新綫路"      , YtDlpUrl      , YtDlpChecksumsUrl),
                                              new DownloadSource("yt-dlp 官方穩定版備用綫路", YtDlpBackupUrl, YtDlpBackupChecksumsUrl)},
                                      "yt-dlp.exe", downloaded, p => report(p, "正在下載 yt-dlp：" + p + "%"), log);
                    ValidateExecutable(downloaded, "yt-dlp.exe");
                    InstallFile(downloaded, Path.Combine(appDir, "yt-dlp.exe"));
                    log("yt-dlp.exe 已安裝並通過 SHA-256 校驗。");
                }

                if (!File.Exists(Path.Combine(appDir, "ffmpeg.exe" )) ||
                    !File.Exists(Path.Combine(appDir, "ffprobe.exe")))
                {
                    report(0, "正在下載 FFmpeg（文件較大，請耐心等待）…");
                    string archive = Path.Combine(tempDir, "ffmpeg-master-latest-win64-gpl.zip");
                    DownloadAndVerify(new[] { new DownloadSource("yt-dlp FFmpeg 官方構建綫路", FfmpegUrl      , FfmpegChecksumsUrl),
                                              new DownloadSource("FFmpeg-Builds 上游備用綫路", FfmpegBackupUrl, FfmpegBackupChecksumsUrl)},
                                      Path.GetFileName(archive), archive, p => report(p, "正在下載 FFmpeg：" + p + "%"), log);
                    string ffmpeg  = ExtractNamedExecutable(archive, "ffmpeg.exe" , tempDir);
                    string ffprobe = ExtractNamedExecutable(archive, "ffprobe.exe", tempDir);
                    ValidateExecutable(ffmpeg , "ffmpeg.exe" );
                    ValidateExecutable(ffprobe, "ffprobe.exe");
                    InstallFile(ffmpeg , Path.Combine(appDir, "ffmpeg.exe" ));
                    InstallFile(ffprobe, Path.Combine(appDir, "ffprobe.exe"));
                    log("ffmpeg.exe 和 ffprobe.exe 已安裝，壓縮包已通過 SHA-256 校驗。");
                }

                if (!File.Exists(Path.Combine(appDir, "deno.exe")))
                {
                    report(0, "正在下載 Deno…");
                    string archive = Path.Combine(tempDir, "deno-x86_64-pc-windows-msvc.zip");
                    DownloadAndVerify(new[] { new DownloadSource("Deno 官方 GitHub 綫路" , DenoUrl      , DenoChecksumsUrl),
                                              new DownloadSource("Deno 官方 CDN 備用綫路", DenoBackupUrl, DenoBackupChecksumsUrl)},
                                      Path.GetFileName(archive), archive, p => report(p, "正在下載 Deno：" + p + "%"), log);
                    string deno = ExtractNamedExecutable(archive, "deno.exe", tempDir);
                    ValidateExecutable(deno, "deno.exe");
                    InstallFile(deno, Path.Combine(appDir, "deno.exe"));
                    log("deno.exe 已安裝並通過 SHA-256 校驗。");
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
            if (String.IsNullOrWhiteSpace(checksumText)) throw new InvalidDataException("校驗文件爲空。");
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
            throw new InvalidDataException("官方校驗淸單中未找到 " + fileName + "。請稍後重試。");
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
                    if (found != null) throw new InvalidDataException("壓縮包中存在多箇 " + executableName + "，已停止安裝。");
                    found = entry;
                }
                if (found == null || found.Length <= 0) throw new InvalidDataException("壓縮包中未找到 " + executableName + "。");
                using (Stream input = found.Open())
                using (var output = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    input.CopyTo(output);
            }
            return outputPath;
        }

        internal static void ValidateExecutable(string path, string displayName)
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length < 65536) throw new InvalidDataException(displayName + " 文件過小或不存在。");
            using (var stream = File.OpenRead(path))
            {
                if (stream.ReadByte() != 'M' || stream.ReadByte() != 'Z')
                    throw new InvalidDataException(displayName + " 不是有效的 Windows 可執行文件。");
            }
        }

        private static void DownloadAndVerify(DownloadSource[] sources, string fileName, string destination,
                                              Action<int> progress, Action<string> log)
        {
            Exception lastError = null;
            foreach (DownloadSource source in sources)
            {
                for (int attempt = 1; attempt <= 2; attempt++)
                {
                    try
                    {
                        if (File.Exists(destination)) File.Delete(destination);
                        log("下載綫路：" + source.Name + "（第 " + attempt + " 次嘗試）");
                        string checksums = DownloadText(source.ChecksumsUrl);
                        string expected  = ParseExpectedHash(checksums, fileName);
                        DownloadFile(source.FileUrl, destination, progress);
                        string actual    = ComputeSha256(destination);
                        if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
                            throw new InvalidDataException(fileName + " 的 SHA-256 校驗失敗。");
                        log(fileName + " SHA-256：" + actual);
                        return;
                    }
                    catch (Exception ex)
                    {
                        lastError = ex;
                        if (File.Exists(destination)) File.Delete(destination);
                        log(source.Name + "失敗：" + FriendlyNetworkMessage(ex));
                        if (attempt < 2) System.Threading.Thread.Sleep(1500);
                    }
                }
            }
            throw new InvalidOperationException("所有官方下載綫路均失敗；未安裝 " + fileName + "。" +
                (lastError == null ? "" : " 最後一次錯誤：" + FriendlyNetworkMessage(lastError)), lastError);
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
                    if (code == 403) return "伺服器拒絶訪問（HTTP 403，可能是 GitHub 限流）";
                    if (code == 404) return "下載文件不存在（HTTP 404）";
                    return "HTTP " + code + " " + response.StatusDescription;
                }
                if (web.Status == WebExceptionStatus.Timeout) return "連綫超時";
                if (web.Status == WebExceptionStatus.NameResolutionFailure) return "域名解析失敗";
                if (web.Status == WebExceptionStatus.ConnectFailure) return "無法連綫伺服器";
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
                long total      = response.ContentLength;
                long received   = 0;
                int lastPercent = -1;
                var buffer      = new byte[128 * 1024];
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
                if (received == 0) throw new InvalidDataException("伺服器返回了空文件。");
                progress(100);
            }
        }

        private static HttpWebRequest CreateRequest(string url)
        {
            if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("組件只能從 HTTPS 地址下載。");

            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
            var request                          = (HttpWebRequest)WebRequest.Create(url);
            request.AllowAutoRedirect            = true;
            request.UserAgent                    = "yt-dlp-gui-mvp/0.5";
            request.Timeout                      = 30000;
            request.ReadWriteTimeout             = 30000;

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

        #region ENVIRONMENT PATH
        private static string CheckBinaryExist(string appDir, string binaryName, Boolean checkEnvPath = true)
        {
            if (File.Exists(Path.Combine(appDir, binaryName))) return appDir;
            if (checkEnvPath)
            {
                HashSet<string>             envPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                EnvironmentVariableTarget[] targets  = new EnvironmentVariableTarget[] { EnvironmentVariableTarget.Process,
                                                                                        EnvironmentVariableTarget.User,
                                                                                        EnvironmentVariableTarget.Machine};

                foreach (var target in targets)
                {
                    // 讀取現有的系統環境變數 'PATH' 與用戸環境變數 'PATH'
                    var envPath   = Environment.GetEnvironmentVariable("PATH", target) ?? string.Empty;
                    if (string.IsNullOrEmpty(envPath)) continue;

                    string result = string.Empty;
                    foreach (var path in envPath.Split(';')) // 拆分
                    {
                        var p = path.Trim();
                        if (string.IsNullOrEmpty(p) || string.IsNullOrWhiteSpace(p)) continue;

                        string normalized = Environment.ExpandEnvironmentVariables(p).Trim();
                        try
                        {
                            // 標準化路徑並去除尾部分隔符
                            normalized    = Path.GetFullPath(normalized)
                                                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                        }
                        catch
                        {
                            // 如果包含不可解析的環境變量或格式問題，退回基本清理版本
                            normalized    = normalized.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                        }

                        if (string.IsNullOrEmpty(normalized) || !envPaths.Add(normalized)) continue; // 袪重（大小寫不敏感）

                        var binaryPath    = Path.Combine(normalized, binaryName);
                        if (!File.Exists(binaryPath)) continue;

                        if (EnvironmentVariableTarget.Process != target)
                        {
                            Environment.SetEnvironmentVariable("PATH", string.Join(";", envPaths), EnvironmentVariableTarget.Process);
                        }

                        return normalized;
                    }
                }
            }

            return string.Empty;
        }
        #endregion /* ENVIRONMENT PATH */
    }
}
