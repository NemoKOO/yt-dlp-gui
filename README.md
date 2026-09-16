# 视频音频字幕下载 MVP v0.5（Windows x64）

这是一个面向普通 Windows 用户的 yt-dlp 图形界面。它把复制链接、选择 MP4/MP3/SRT、分类保存和 Cookies 选项放进一个窗口，不要求用户输入命令。本项目不是 yt-dlp 官方产品。

公开版是轻量便携包，**不随包重新分发 yt-dlp、FFmpeg 或 Deno 的二进制文件**。第一次运行时，程序会询问是否从各项目的官方发布地址下载所需组件；安装完成后，所有文件都留在程序目录，不修改系统 PATH，也不需要管理员权限。

## 第一次使用

1. 完整解压 Release 中的 `yt-dlp-gui-v0.5-windows-x64.zip`，不要直接在压缩包里运行。
2. 双击 `yt-dlp-gui.exe`。
3. 首次运行会列出缺少的组件。点击“是”后开始下载，完整下载量约 250 MB，FFmpeg 文件较大，请耐心等待。
4. 程序依次下载、核对官方 SHA-256、检查 Windows 可执行文件格式，再放入程序目录。下载或校验失败的文件不会安装。
5. 安装中断后可以再次点击“检查运行组件”，已经成功安装的组件不会重复下载。

每个组件先尝试首选官方线路；连接超时、403、404或校验失败时会自动重试，再切换可信备用线路。不同线路的文件分别使用其对应的官方校验清单，**不会使用不明网盘或第三方加速镜像**。

| 组件 | 首选来源 | 备用来源 |
| --- | --- | --- |
| yt-dlp | `yt-dlp/yt-dlp` 最新稳定版 | 同一官方仓库的已知稳定版本 |
| FFmpeg / FFprobe | `yt-dlp/FFmpeg-Builds` | 其上游项目 `BtbN/FFmpeg-Builds` |
| Deno | `denoland/deno` GitHub Release | Deno 官方 `dl.deno.land` CDN |

如果自动安装一直失败，也可以从 `THIRD_PARTY_NOTICES.md` 列出的官方项目手动下载 Windows x64 文件，将 `yt-dlp.exe`、`ffmpeg.exe`、`ffprobe.exe` 和 `deno.exe` 放在 `yt-dlp-gui.exe` 旁边。

## 下载视频、音频和字幕

1. 粘贴普通视频网址，或直接粘贴带前后文字的抖音分享文案。界面会显示自动识别出的第一个 `http/https` 网址。
2. 点击“视频 MP4”“音频 MP3”或“字幕 SRT”。字幕模式可选择简体中文、繁体中文或英文，每次只请求一种语言。
3. 选择保存根目录，点击“开始下载”。文件分别进入 `mp4`、`mp3`、`subtitles` 子目录，降低不同类型文件重名覆盖的风险。
4. 完成后点击“打开保存目录”。

默认只下载单个视频。勾选“下载整个播放列表”前，请先确认列表大小。并非每个视频都有所选语言字幕；程序不会凭空生成视频中不存在的字幕。

字幕下载后会检查 VTT，并使用 FFmpeg 补做 SRT 转换。即使某次字幕请求被限流或 yt-dlp 中途报错，也会转换已经下载到本地的 VTT。若提示 HTTP 429，请稍后再试，避免短时间连续请求。

## Cookies、抖音和更新

平时不要勾选“读取 Firefox Cookies”。只有网站提示登录或验证时，先在 Firefox 中打开目标视频并刷新，再勾选重试。Cookies 可能包含登录状态，不要公开完整日志、浏览器资料或 Cookies 文件。每次下载结束后，该选项会自动取消。

抖音经常要求新鲜 Cookies，但不一定要求登录。即使提供 Cookies，网站接口、反自动化策略或地域限制也可能导致失败。软件不能绕过 DRM、访问权限或网站规则。

“检查运行组件”会确认四个文件是否存在；组件完整时，可以继续调用 yt-dlp 官方的 `-U` 自更新。该操作只更新 yt-dlp，不更新界面、FFmpeg 或 Deno。GitHub 返回 403 时通常是临时限流，可稍后重试。

## 安全和隐私

- 组件只通过代码中列出的 HTTPS 地址下载，并在安装前验证对应官方 SHA-256。
- 下载过程不会生成或上传 `cookies.txt`；需要时由 yt-dlp 直接读取本机 Firefox 数据。
- 安装缓存和 yt-dlp 运行临时文件放在程序目录的工作文件夹中，正常结束后组件下载缓存会删除。
- 本程序没有代码签名，Windows 第一次运行可能显示 SmartScreen 提示。请从项目自己的 GitHub Release 下载，并核对 Release 提供的 SHA-256。
- 请仅下载你有权访问和使用的内容，遵守网站条款和当地法律。

## 已知限制

- 仅面向 Windows x64；Windows ARM64 暂未提供原生组件组合。
- 不保证“全网”或所有视频都能下载。站点页面、接口、权限和限流会不断变化。
- 暂无清晰度细选、任务队列、下载历史和 GUI 自动更新。
- 播放列表进度条显示当前文件进度，不是整个列表总进度。
- 取消视频下载可能留下 `.part` 文件；组件安装阶段目前不可从界面中途取消。
- 使用 `--ignore-config`，不会读取用户的 yt-dlp 全局配置，以保证界面选项和保存路径可预测。
- 只支持直接读取 Firefox Cookies，不支持 Windows Chrome App-Bound Encryption。

## 构建与测试

源码文件包括 `Program.cs`、`ComponentInstaller.cs`、`Tests.cs` 和 `build.ps1`。在 Windows PowerShell 中执行：

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

构建脚本使用 Windows 自带的 .NET Framework C# 编译器，不需要另装 .NET SDK。测试覆盖参数生成、网址提取、分类目录、VTT→SRT、本地中文编码、组件缺失检测、两种官方校验清单格式、压缩包定向提取及备用线路配置；常规构建测试不会下载大型组件。

## 许可证与第三方组件

本图形界面源码使用 MIT License，参见 `LICENSE`。首次运行下载的 yt-dlp、FFmpeg 和 Deno 是独立程序，分别受各自许可证约束，来源和许可链接见 `THIRD_PARTY_NOTICES.md`。

轻量 Release 不包含这些第三方二进制。若你自行制作包含第三方二进制的完整整合包，需要自行履行相应许可证的再分发义务；不要直接把本机下载后的四个组件提交进本仓库。
