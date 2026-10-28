# Builds a per-user (no-admin) setup EXE with the app, zlgcan.dll, and kerneldlls.
$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $PSScriptRoot
if (-not $Root) { $Root = Resolve-Path (Join-Path $PSScriptRoot "..") }
$InstallerDir = $PSScriptRoot
$DistDir = Join-Path $Root "dist"
$PayloadDir = Join-Path $InstallerDir "payload"
$ZipPath = Join-Path $InstallerDir "payload.zip"
$NativeDir = Join-Path $Root "native\x64"

function Find-Sdk {
    $candidates = @(
        $env:ZLG_CAN_SDK,
        $NativeDir,
        "C:\Users\allahmoradi\Downloads\zlgcan(261014)\zlgcan_x64"
    )
    foreach ($c in $candidates) {
        if ([string]::IsNullOrWhiteSpace($c)) { continue }
        $dll = Join-Path $c "zlgcan.dll"
        if (Test-Path -LiteralPath $dll) { return (Resolve-Path -LiteralPath $c).Path }
        $nested = Join-Path $c "kerneldlls"
        if ((Test-Path -LiteralPath (Join-Path $c "zlgcan.dll")) -or (Test-Path -LiteralPath $nested)) {
            return (Resolve-Path -LiteralPath $c).Path
        }
    }
    throw "zlgcan.dll not found. Set ZLG_CAN_SDK or copy the x64 ZLG runtime into native\x64 (zlgcan.dll + kerneldlls)."
}

function Find-Csc {
    $csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
    if (Test-Path $csc) { return $csc }
    throw "csc.exe not found (need .NET Framework 4.8 developer / runtime targeting pack)."
}

function Find-MSBuild {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswhere) {
        $found = & $vswhere -latest -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" | Select-Object -First 1
        if ($found -and (Test-Path $found)) { return $found }
    }
    return $null
}

function Find-AppExe {
    $paths = @(
        (Join-Path $Root "USBCANTool\bin\x64\Release\USBCANTool.exe"),
        (Join-Path $Root "USBCANTool\bin\Release\USBCANTool.exe"),
        (Join-Path $Root "USBCANTool\bin\x64\Debug\USBCANTool.exe"),
        (Join-Path $Root "USBCANTool\bin\Debug\USBCANTool.exe")
    )
    foreach ($p in $paths) {
        if (Test-Path -LiteralPath $p) { return $p }
    }
    return $null
}

Write-Host "Locating ZLG x64 runtime..."
$sdk = Find-Sdk
Write-Host "  SDK: $sdk"

if (-not (Test-Path -LiteralPath (Join-Path $NativeDir "zlgcan.dll"))) {
    Write-Host "Staging native\x64..."
    New-Item -ItemType Directory -Force -Path $NativeDir | Out-Null
    Copy-Item -LiteralPath (Join-Path $sdk "zlgcan.dll") -Destination (Join-Path $NativeDir "zlgcan.dll") -Force
    $kernelSrc = Join-Path $sdk "kerneldlls"
    if (Test-Path -LiteralPath $kernelSrc) {
        $kernelDst = Join-Path $NativeDir "kerneldlls"
        if (Test-Path $kernelDst) { Remove-Item -LiteralPath $kernelDst -Recurse -Force }
        Copy-Item -LiteralPath $kernelSrc -Destination $kernelDst -Recurse -Force
    }
}

$msbuild = Find-MSBuild
if ($msbuild) {
    Write-Host "Building USBCANTool (Release|x64)..."
    & $msbuild (Join-Path $Root "USBCANTool.sln") /nologo /v:m /p:Configuration=Release /p:Platform=x64 /p:AllowUnsafeBlocks=true
    if ($LASTEXITCODE -ne 0) { throw "MSBuild failed with exit $LASTEXITCODE" }
} else {
    Write-Host "MSBuild not found; using an existing USBCANTool.exe if present."
}

$appExe = Find-AppExe
if (-not $appExe) { throw "USBCANTool.exe not found. Build the x64 project in Visual Studio, then re-run Pack.ps1." }
Write-Host "  App: $appExe"

$appDir = Split-Path -Parent $appExe
Write-Host "Preparing payload..."
if (Test-Path $PayloadDir) { Remove-Item -LiteralPath $PayloadDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $PayloadDir | Out-Null
Copy-Item -LiteralPath $appExe -Destination (Join-Path $PayloadDir "USBCANTool.exe") -Force
$cfg = Join-Path $appDir "USBCANTool.exe.config"
if (-not (Test-Path $cfg)) { $cfg = Join-Path $Root "USBCANTool\App.config" }
if (Test-Path $cfg) {
    Copy-Item -LiteralPath $cfg -Destination (Join-Path $PayloadDir "USBCANTool.exe.config") -Force
}

$zlgSrc = Join-Path $NativeDir "zlgcan.dll"
if (-not (Test-Path $zlgSrc)) { $zlgSrc = Join-Path $sdk "zlgcan.dll" }
Copy-Item -LiteralPath $zlgSrc -Destination (Join-Path $PayloadDir "zlgcan.dll") -Force

$kernelFromNative = Join-Path $NativeDir "kerneldlls"
$kernelFromSdk = Join-Path $sdk "kerneldlls"
$kernelSrc = if (Test-Path $kernelFromNative) { $kernelFromNative } else { $kernelFromSdk }
if (-not (Test-Path $kernelSrc)) { throw "kerneldlls folder not found next to zlgcan.dll." }
Copy-Item -LiteralPath $kernelSrc -Destination (Join-Path $PayloadDir "kerneldlls") -Recurse -Force

if (Test-Path $ZipPath) { Remove-Item -LiteralPath $ZipPath -Force }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($PayloadDir, $ZipPath, [System.IO.Compression.CompressionLevel]::Optimal, $false)

$csc = Find-Csc
New-Item -ItemType Directory -Force -Path $DistDir | Out-Null
$outExe = Join-Path $DistDir "USBCANToolSetup.exe"
Write-Host "Compiling $outExe ..."
$refs = @(
    "/r:System.dll",
    "/r:System.Drawing.dll",
    "/r:System.Windows.Forms.dll",
    "/r:System.IO.Compression.dll",
    "/r:System.IO.Compression.FileSystem.dll"
)
& $csc /nologo /target:winexe /platform:x64 /optimize+ /win32manifest:"$InstallerDir\app.manifest" /resource:"$ZipPath,payload.zip" /out:$outExe @refs "$InstallerDir\Installer.cs"
if ($LASTEXITCODE -ne 0) { throw "csc failed with exit $LASTEXITCODE" }

Write-Host ""
Write-Host "Done: $outExe"
Write-Host "Copy that file to the other PC and run it (no admin). Installs to %LOCALAPPDATA%\Programs\USBCANTool"
