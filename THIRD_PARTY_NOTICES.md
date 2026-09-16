# 第三方运行组件

本项目的轻量 Release 不包含下列二进制。用户确认后，程序从列出的官方 HTTPS 地址下载独立程序，并使用相同发布方提供的 SHA-256 清单验证文件。

## yt-dlp

- 项目：<https://github.com/yt-dlp/yt-dlp>
- Windows x64：<https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe>
- 校验清单：<https://github.com/yt-dlp/yt-dlp/releases/latest/download/SHA2-256SUMS>
- 许可说明：<https://github.com/yt-dlp/yt-dlp#licensing>

yt-dlp 源码主体使用 Unlicense，但官方 PyInstaller Windows 可执行文件包含其他许可证组件；官方说明该组合文件为 GPLv3+。请以下载版本随附的 `THIRD_PARTY_LICENSES.txt` 和官方许可说明为准。

## FFmpeg 和 FFprobe

- yt-dlp 构建项目：<https://github.com/yt-dlp/FFmpeg-Builds>
- 上游构建项目：<https://github.com/BtbN/FFmpeg-Builds>
- FFmpeg 法律与许可说明：<https://ffmpeg.org/legal.html>

程序下载 Windows x64 GPL 静态构建。FFmpeg 以及构建中包含的库拥有各自许可证。轻量 Release 不重新托管这些二进制。

## Deno

- 项目：<https://github.com/denoland/deno>
- Windows x64 Release：<https://github.com/denoland/deno/releases/latest>
- 官方备用 CDN：<https://dl.deno.land/release-latest.txt>
- 许可证：<https://github.com/denoland/deno/blob/main/LICENSE.md>

Deno 使用 MIT License，并包含受各自许可证约束的第三方组件。请以对应 Release 和项目源码中的通知为准。

## 免责声明

本项目与 yt-dlp、FFmpeg、BtbN 或 Deno 的维护者没有隶属或官方合作关系。各项目名称仅用于说明兼容性和下载来源。
