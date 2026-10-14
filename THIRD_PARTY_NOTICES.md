# 第三方運行組件

本項目的輕量 Release 不包含下列二進制。用戸确認後，程序從列出的官方 HTTPS 地址下載獨立程序，並使用相同發佈方提供的 SHA-256 清單驗證文件。

## aria2

- 項目：<https://github.com/aria2/aria2>
- Windows x64 Release：<https://github.com/aria2/aria2/releases/latest/download/aria2-1.37.0-win-64bit-build1.zip>
- 許可證：<https://github.com/aria2/aria2/blob/master/LICENSE.OpenSSL>

## deno

- 項目：<https://github.com/denoland/deno>
- Windows x64 Release：<https://github.com/denoland/deno/releases/latest>
- 官方備用 CDN：<https://dl.deno.land/release-latest.txt>
- 許可證：<https://github.com/denoland/deno/blob/main/LICENSE.md>

deno 使用 MIT License，並包含受各自許可證約束的第三方組件。請以對應 Release 和項目源碼中的通知爲準。

## FFmpeg 和 FFprobe

- yt-dlp 構建項目：<https://github.com/yt-dlp/FFmpeg-Builds>
- 上游構建項目：<https://github.com/BtbN/FFmpeg-Builds>
- FFmpeg 法律與許可説明：<https://ffmpeg.org/legal.html>

程序下載 Windows x64 GPL 靜態構建。FFmpeg 以及構建中包含的庫擁有各自許可證。輕量 Release 不重新託管這些二進制。

## yt-dlp

- 項目：<https://github.com/yt-dlp/yt-dlp>
- Windows x64：<https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe>
- 校驗清單：<https://github.com/yt-dlp/yt-dlp/releases/latest/download/SHA2-256SUMS>
- 許可説明：<https://github.com/yt-dlp/yt-dlp#licensing>

yt-dlp 源碼主體使用 Unlicense，但官方 PyInstaller Windows 可執行文件包含其他許可證組件；官方説明該組合文件爲 GPLv3+。請以下載版本隨附的 `THIRD_PARTY_LICENSES.txt` 和官方許可説明爲準。

## 免責聲明

本項目與 aria2、deno、FFmpeg、BtbN 或 、yt-dlp 的維護者沒有隸屬或官方合作關係。各項目名稱僅用於説明兼容性和下載來源。
