$ErrorActionPreference  = 'Stop'
$projectDir             = Split-Path -Parent $MyInvocation.MyCommand.Path
#$compiler               = 'C:\Program Files\dotnet\sdk\10.0.401\Roslyn\bincore\csc.exe'
$compiler               = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$downloadProgressParser = Join-Path $projectDir 'DownloadProgressParser.cs'
$program                = Join-Path $projectDir 'Program.cs'
$componentInstaller     = Join-Path $projectDir 'ComponentInstaller.cs'
$tests                  = Join-Path $projectDir 'Tests.cs'
$gui                    = Join-Path $projectDir 'yt-dlp-gui.exe'
$testExe                = Join-Path $projectDir 'argument-tests.exe'

cls
if (-not (Test-Path -LiteralPath $compiler)) { throw 'The .NET Framework C# compiler was not found.' }
if (      Test-Path -LiteralPath $gui      ) { del $gui     }
if (      Test-Path -LiteralPath $testExe  ) { del $testExe }

#https://github.com/yt-dlp/yt-dlp/blob/master/devscripts/logo.ico
#$compiler /langversion:?
& $compiler /nologo /target:winexe /optimize /resource:yt-dlp.resx /win32icon:yt-dlp.ico  "/out:$gui"     /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll $componentInstaller $downloadProgressParser $program
if ($LASTEXITCODE -ne 0) { throw 'GUI compilation failed.' }

& $compiler /nologo /target:exe    /main:YtDlpGuiMvp.ArgumentTests  "/out:$testExe" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll $componentInstaller $downloadProgressParser $program $tests
if ($LASTEXITCODE -ne 0) { throw 'Argument-test compilation failed.' }

& $testExe
if ($LASTEXITCODE -ne 0) { throw 'Argument tests failed.' }

Write-Host "Build complete: $gui"
#& $gui
