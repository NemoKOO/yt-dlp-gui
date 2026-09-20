# GitHub 发布步骤

1. 新建一个公开仓库，例如 `yt-dlp-gui-for-beginners`。
2. 上传源码与说明文件：`Program.cs`、`ComponentInstaller.cs`、`Tests.cs`、`build.ps1`、`README.md`、`RELEASE_NOTES_v0.6.1.md`、`LICENSE`、`THIRD_PARTY_NOTICES.md`、`.gitignore`。
3. 不要上传 `yt-dlp.exe`、`ffmpeg.exe`、`ffprobe.exe`、`deno.exe`、`argument-tests.exe` 或本机下载目录。
4. 创建标签 `v0.6.1` 和对应 GitHub Release。
5. Release 标题可写：`视频音频字幕下载 v0.6.1（Windows x64）`。
6. Release 正文可复制 `RELEASE_NOTES_v0.6.1.md`。
7. 上传 `yt-dlp-gui-v0.6.1-windows-x64.zip` 和 `SHA256SUMS-v0.6.1.txt` 两个附件。
8. 发布后，用浏览器隐私窗口下载 Release 附件，解压到全新目录，在 Windows 10/11 x64 上完成一次首次组件安装和一条有权使用的视频测试；若要声明其他系统受支持，还需在对应系统上实测。

视频简介应链接到该项目自己的 Release 页面，不要直接链接来历不明的网盘。不要宣称是 yt-dlp 官方软件或保证所有网站均可下载。
