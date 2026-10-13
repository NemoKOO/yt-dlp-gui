#region Namespace Dependencies
using System;
using System.Globalization;
using System.Text.RegularExpressions;
#endregion /* Namespace Dependencies */

namespace YtDlpGuiMvp
{

    internal sealed class DownloadProgressInfo
    {
        public string  Downloader          { get; set; }
        public double  Percent             { get; set; }

      //public double? DownloadedSizeValue { get; set; }
        public double  DownloadedBytes     { get; set; }
        public string  DownloadedSize      { get; set; }

      //public double? TotalSizeValue      { get; set; }
        public double  TotalBytes          { get; set; }
        public string  TotalSize           { get; set; }

        public double? SpeedValue          { get; set; }
        public string  SpeedUnit           { get; set; }

        public string  ETA                 { get; set; }

        public DownloadProgressInfo()
        {
            Downloader          = string.Empty;

            DownloadedBytes     = 0.0;
            DownloadedSize      = string.Empty;

            TotalBytes          = 0.0;
            TotalSize           = string.Empty;

            SpeedUnit           = string.Empty;
            ETA                 = string.Empty;
        }
    }

    internal static class DownloadProgressParser
    {
        private static readonly Regex YtDlpRegex = new Regex(
        /* [download] 12.3% of 311MiB at 3.6MiB/s ETA 00:01:22 */
            @"\[download\]\s+" +
            @"(?<percent>[0-9]+(?:\.[0-9]+)?)%\s+" +
            @"of\s+" +
            @"(?<totalSize>[0-9]+(?:\.[0-9]+)?\s*[KMGTPE]?i?B|[0-9]+(?:\.[0-9]+)?\s*B)\s+" +
            @"at\s+" +
            @"(?<speed>[0-9]+(?:\.[0-9]+)?\s*[KMGTPE]?i?B/s|[0-9]+(?:\.[0-9]+)?\s*B/s)\s+" +
            @"ETA\s+" +
            @"(?<eta>\S+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex Aria2cRegex = new Regex(
        /* [#12eefe 5.9MiB/311MiB(1%) CN:16 DL:3.6MiB ETA:1m22s] */
            @"\[#.*?\s+" +
            @"(?<downloadedSize>[0-9]+(?:\.[0-9]+)?\s*[KMGTPE]?i?B|[0-9]+(?:\.[0-9]+)?\s*B)" +
            @"/" +
            @"(?<totalSize>[0-9]+(?:\.[0-9]+)?\s*[KMGTPE]?i?B|[0-9]+(?:\.[0-9]+)?\s*B)" +
            @"\(" +
            @"(?<percent>[0-9]+(?:\.[0-9]+)?)%" +
            @"\).*?" +
            @"\bDL:" +
            @"(?<speed>[0-9]+(?:\.[0-9]+)?\s*[KMGTPE]?i?B|[0-9]+(?:\.[0-9]+)?\s*B)" +
            @"(?:/s)?" +
            @"\s+ETA:" +
            @"(?<eta>[^\]\s]+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex SizeRegex = new Regex(
            @"^\s*(?<value>[0-9]+(?:\.[0-9]+)?)\s*(?<unit>[KMGTPE]?i?B/s|[KMGTPE]?B/s|B/s|[KMGTPE]?i?B|B)\s*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static bool TryParse(string log, out DownloadProgressInfo progress)
        {
            progress = new DownloadProgressInfo();

            if (string.IsNullOrWhiteSpace(log)   ) return false;
            if (TryParseYtDlp (log, out progress)) return true;
            if (TryParseAria2c(log, out progress)) return true;

            return false;
        }

        public static bool TryParseYtDlp(string log, out DownloadProgressInfo progress)
        {
            progress                    = new DownloadProgressInfo();
            var match                   = YtDlpRegex.Match(log);
            if (!match.Success) return false;

            progress.Downloader          = "yt-dlp";
            progress.Percent             = double.Parse(match.Groups["percent"].Value, NumberStyles.Float, CultureInfo.InvariantCulture);
            progress.ETA                 = match.Groups["eta"].Value;

            double speedValue, totalValue;
            string speedUnit , totalUnit;
            ParseSize(match.Groups["speed"].Value    , out speedValue, out speedUnit);
            ParseSize(match.Groups["totalSize"].Value, out totalValue, out totalUnit);

            progress.TotalBytes          = CalcBytes(totalValue, totalUnit);
            progress.TotalSize           = totalValue + totalUnit;

            progress.SpeedValue          = speedValue;
            progress.SpeedUnit           = speedUnit;

            // yt-dlp 此格式沒有直接輸出已下載大小，因此根據百分比和總大小估算
            progress.DownloadedBytes     = progress.TotalBytes * progress.Percent / 100.0;
            progress.DownloadedSize      = (totalValue * progress.Percent / 100.0).ToString("F2").PadLeft(6, ' ') + totalUnit;

            return true;
        }

        public static bool TryParseAria2c(string log, out DownloadProgressInfo progress)
        {
            progress                     = new DownloadProgressInfo();
            var match                    = Aria2cRegex.Match(log);
            if (!match.Success) return false;

            progress.Downloader          = "aria2c";
            progress.Percent             = double.Parse(match.Groups["percent"].Value, NumberStyles.Float, CultureInfo.InvariantCulture);
            progress.ETA                 = match.Groups["eta"].Value;

            double downloadedValue, totalValue, speedValue;
            string downloadedUnit , totalUnit , speedUnit;

            ParseSize(match.Groups["downloadedSize"].Value, out downloadedValue, out downloadedUnit);
            ParseSize(match.Groups["speed"].Value         , out speedValue     , out speedUnit     );
            ParseSize(match.Groups["totalSize"].Value     , out totalValue     , out totalUnit     );


            progress.DownloadedBytes     = CalcBytes(downloadedValue, downloadedUnit);
            progress.DownloadedSize      = downloadedValue.ToString().PadLeft(6, ' ') + downloadedUnit;
            // aria2c 的 DL:3.6MiB 通常表示 3.6MiB/s
            progress.SpeedValue          = speedValue;
            progress.SpeedUnit           = speedUnit.EndsWith("/s", StringComparison.OrdinalIgnoreCase)
                                           ? speedUnit : speedUnit + "/s";
            progress.TotalBytes          = CalcBytes(totalValue, totalUnit);
            progress.TotalSize           = totalValue.ToString() + totalUnit;
          //progress.Percent             = progress.DownloadedBytes / progress.TotalBytes;

            return true;
        }

        private static void ParseSize(string text, out double value, out string unit)
        {
            value = 0;
            unit  = string.Empty;

            if (string.IsNullOrWhiteSpace(text)) return;

            Match match = SizeRegex.Match(text);
            if (!match.Success) return;

            value = double.Parse(match.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture);
            unit  = match.Groups["unit"].Success ? match.Groups[2].Value : "B";
        }

        private static double CalcBytes(double value, string unit)
        {
            switch (unit)
            {
                case "B"   : {  return value                            ; }
                case "KB"  : {  return value * 1000                     ; }
                case "KiB" : {  return value * 1024                     ; }
                case "MB"  : {  return value * 1000 * 1000              ; }
                case "MiB" : {  return value * 1024 * 1024              ; }
                case "GB"  : {  return value * 1000 * 1000 * 1000       ; }
                case "GiB" : {  return value * 1024 * 1024 * 1024       ; }
            //  case "TB"  : {  return value * 1000 * 1000 * 1000 * 1000; }
            //  case "TiB" : {  return value * 1024 * 1024 * 1024 * 1024; }
                default    : {  return value                            ; }
            }
        }
    }
}
