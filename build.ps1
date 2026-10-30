$ErrorActionPreference  = 'Stop'
$projectDir             = Split-Path -Parent $MyInvocation.MyCommand.Path
#$compiler               = 'C:\Program Files\dotnet\sdk\10.0.402\Roslyn\bincore\csc.exe'
$compiler               = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$componentInstaller     = Join-Path $projectDir 'ComponentInstaller.cs'
$darkmode               = Join-Path $projectDir 'DarkMode\src\DarkModeForms\DarkModeCS.cs'
$flatcontrols           = Join-Path $projectDir 'DarkMode\src\DarkModeForms\DarkControls\*.cs'
$downloadProgressParser = Join-Path $projectDir 'DownloadProgressParser.cs'
$program                = Join-Path $projectDir 'Program.cs'
$tests                  = Join-Path $projectDir 'Tests.cs'
$gui                    = Join-Path $projectDir 'yt-dlp-gui.exe'
$testExe                = Join-Path $projectDir 'argument-tests.exe'

cls
if (-not (Test-Path -LiteralPath $compiler)) { throw 'The .NET Framework C# compiler was not found.' }
if (      Test-Path -LiteralPath $gui      ) { del $gui     }
if (      Test-Path -LiteralPath $testExe  ) { del $testExe }

#https://github.com/yt-dlp/yt-dlp/blob/master/devscripts/logo.ico
#$compiler /langversion:?
#$compiler /resource:logo.ico,logo.ico ：表示把 logo.ico 作爲嵌入資源加入到 EXE 中，資源名稱也叫logo.ico
& $compiler /nologo /target:winexe /define:__DARK_THEME_SUPPORTED__  /optimize /resource:yt-dlp.ico,yt-dlp.ico /win32icon:yt-dlp.ico  "/out:$gui"     /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll $combobox $componentInstaller $darkmode $downloadProgressParser $flatcontrols $program
if ($LASTEXITCODE -ne 0) { throw 'GUI compilation failed.' }

& $compiler /nologo /target:exe /define:__DARK_THEME_SUPPORTED__ /main:YtDlpGuiMvp.ArgumentTests  "/out:$testExe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll $combobox $componentInstaller $darkmode $downloadProgressParser $flatcontrols $program $tests
if ($LASTEXITCODE -ne 0) { throw 'Argument-test compilation failed.' }

& $testExe
if ($LASTEXITCODE -ne 0) { throw 'Argument tests failed.' }

Write-Host "Build complete: $gui"
#& $gui
