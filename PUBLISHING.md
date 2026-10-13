# GitHub 發佈步驟

1. 新建一箇公開倉庫，例如 `yt-dlp-gui-for-beginners`。
2. 上傳源碼與説明文件：`Program.cs`、`ComponentInstaller.cs`、`DownloadProgressParser.cs`、`Tests.cs`、`build.ps1`、`README.md`、`RELEASE_NOTES.md`、`LICENSE`、`THIRD_PARTY_NOTICES.md`、`.gitignore`。
3. 不要上傳 `aria2c.exe`、`deno.exe`、`ffmpeg.exe`、`ffprobe.exe`、`yt-dlp.exe`、`argument-tests.exe` 或本機下載目録。
4. 創建標籤 `v0.6.2` 和對應 GitHub Release。
5. Release 標題可寫：`視頻/音頻/字幕下載器 v0.6.2（Windows x64）`。
6. Release 正文可複製 `RELEASE_NOTES.md`「v0.6.2」
7. 上傳 `yt-dlp-gui-v0.6.2-windows-x64.zip` 和 `SHA256SUMS-v0.6.2.txt` 兩箇附件。
8. 發佈後，用瀏覽器隱私窗口下載 Release 附件，解壓到全新目録，在 Windows 10/11 x64 上完成一次首次組件安裝和一條有權使用的視頻測試；若要聲明其他系統受支持，還需在對應系統上實測。

視頻簡介應鏈接到該項目自己的 Release 頁面，不要直接鏈接來歷不明的網盤。不要宣稱是 yt-dlp 官方軟件或保證所有網站均可下載。
