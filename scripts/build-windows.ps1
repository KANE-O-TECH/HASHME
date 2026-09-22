[CmdletBinding()]
param(
    [switch]$SkipInstaller,
    [string]$DotNetExecutable = $env:HASHME_DOTNET,
    [string]$WixExecutable = $env:HASHME_WIX
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $projectRoot 'artifacts'
$publish = Join-Path $artifacts 'publish'
$portableStage = Join-Path $artifacts 'portable'
$portableZip = Join-Path $artifacts 'HASHME_v1.2_Portable_Windows_x64.zip'
$installerMsi = Join-Path $artifacts 'HASHME_v1.2_Setup_Windows_x64.msi'
$installerExe = Join-Path $artifacts 'HASHME_v1.2_Setup_Windows_x64.exe'

function Invoke-Checked {
    param([string]$FilePath, [string[]]$Arguments)
    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$FilePath failed with exit code $LASTEXITCODE."
    }
}

$runningOnWindows = if (Get-Variable -Name IsWindows -ErrorAction SilentlyContinue) {
    [bool]$IsWindows
} else {
    $env:OS -eq 'Windows_NT'
}
if (-not $runningOnWindows) {
    throw 'HASHME release builds require Windows 11 or later.'
}

$dotnetCandidates = @()
if ($DotNetExecutable) {
    $dotnetCandidates += $DotNetExecutable
}
$systemDotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if ($systemDotnet) {
    $dotnetCandidates += $systemDotnet.Source
}
$dotnetPath = $dotnetCandidates | Where-Object {
    (Test-Path -LiteralPath $_) -and
    ((& $_ --list-sdks) | Where-Object { $_ -match '^10\.0\.4\d{2}\s' })
} | Select-Object -First 1
if (-not $dotnetPath) {
    throw 'HASHME v1.2 requires a .NET 10 SDK from the 10.0.4xx feature band.'
}

New-Item -ItemType Directory -Force -Path $artifacts | Out-Null
if (Test-Path $publish) { Remove-Item -Recurse -Force $publish }
if (Test-Path $portableStage) { Remove-Item -Recurse -Force $portableStage }

Push-Location $projectRoot
try {
    foreach ($project in @(
        'src/HashMe.Core/HashMe.Core.csproj',
        'src/HashMe.App/HashMe.App.csproj',
        'tests/HashMe.Core.Tests/HashMe.Core.Tests.csproj'
    )) {
        Invoke-Checked $dotnetPath @('restore', $project)
    }
    foreach ($project in @(
        'src/HashMe.Core/HashMe.Core.csproj',
        'src/HashMe.App/HashMe.App.csproj',
        'tests/HashMe.Core.Tests/HashMe.Core.Tests.csproj'
    )) {
        Invoke-Checked $dotnetPath @('build', $project, '-c', 'Release', '--no-restore', '-p:UseSharedCompilation=false')
    }
    Invoke-Checked $dotnetPath @('run', '--project', 'tests/HashMe.Core.Tests', '-c', 'Release', '--no-build')
    Invoke-Checked $dotnetPath @('restore', 'src/HashMe.App/HashMe.App.csproj', '-r', 'win-x64')
    Invoke-Checked $dotnetPath @(
        'publish', 'src/HashMe.App/HashMe.App.csproj',
        '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true', '--no-restore',
        '-p:PublishSingleFile=true',
        '-p:IncludeNativeLibrariesForSelfExtract=true',
        '-p:EnableCompressionInSingleFile=true',
        '-p:DebugType=none', '-p:DebugSymbols=false',
        '-p:UseSharedCompilation=false',
        '-o', $publish
    )

    New-Item -ItemType Directory -Force -Path $portableStage | Out-Null
    Copy-Item (Join-Path $publish 'HASHME.exe') $portableStage
    Copy-Item (Join-Path $projectRoot 'README.md') $portableStage
    Copy-Item (Join-Path $projectRoot 'RELEASE_NOTES.md') $portableStage
    Copy-Item (Join-Path $projectRoot 'licenses\DOTNET_LICENSE.txt') $portableStage
    Copy-Item (Join-Path $projectRoot 'licenses\DOTNET_THIRD_PARTY_NOTICES.txt') $portableStage
    Copy-Item (Join-Path $projectRoot 'LICENSE.md') $portableStage
    Copy-Item (Join-Path $projectRoot 'NOTICE.md') $portableStage
    Copy-Item (Join-Path $projectRoot 'BRAND_ASSETS_LICENSE.md') $portableStage

    Copy-Item (Join-Path $projectRoot 'README.md') $publish
    Copy-Item (Join-Path $projectRoot 'RELEASE_NOTES.md') $publish
    Copy-Item (Join-Path $projectRoot 'licenses\DOTNET_LICENSE.txt') $publish
    Copy-Item (Join-Path $projectRoot 'licenses\DOTNET_THIRD_PARTY_NOTICES.txt') $publish
    Copy-Item (Join-Path $projectRoot 'LICENSE.md') $publish
    Copy-Item (Join-Path $projectRoot 'NOTICE.md') $publish
    Copy-Item (Join-Path $projectRoot 'BRAND_ASSETS_LICENSE.md') $publish

    if (Test-Path $portableZip) { Remove-Item -Force $portableZip }
    Compress-Archive -Path (Join-Path $portableStage '*') -DestinationPath $portableZip -CompressionLevel Optimal

    if (-not $SkipInstaller) {
        $wix = $null
        if ($WixExecutable) {
            $wix = Get-Command $WixExecutable -ErrorAction SilentlyContinue
            if (-not $wix -and (Test-Path -LiteralPath $WixExecutable)) {
                $wix = Get-Item -LiteralPath $WixExecutable
            }
        }
        if (-not $wix) {
            $wix = Get-Command wix -ErrorAction SilentlyContinue
        }
        if ($wix) {
            $wixPath = if ($wix.Source) { $wix.Source } else { $wix.FullName }
            Invoke-Checked $wixPath @(
                'build', 'installer/HASHME.wxs', 'installer/HASHME.UI.wxs', '-arch', 'x64',
                '-d', "ProjectRoot=$projectRoot",
                '-d', "PublishDir=$publish",
                '-o', $installerMsi
            )

            $iexpress = Join-Path $env:WINDIR 'System32\iexpress.exe'
            if (-not (Test-Path -LiteralPath $iexpress)) {
                throw 'Windows IExpress is required to build the single-file Setup.exe.'
            }

            $sedTemplatePath = Join-Path $projectRoot 'installer\HASHME.Setup.sed.template'
            $sedPath = Join-Path $artifacts 'HASHME.Setup.sed'
            $sed = (Get-Content -Raw -LiteralPath $sedTemplatePath).
                Replace('@@MSI_DIRECTORY@@', "$artifacts\").
                Replace('@@OUTPUT_EXE@@', $installerExe)
            $sed | Set-Content -LiteralPath $sedPath -Encoding ascii

            if (Test-Path -LiteralPath $installerExe) {
                Remove-Item -LiteralPath $installerExe -Force
            }
            $iexpressProcess = Start-Process -FilePath $iexpress `
                -ArgumentList @('/N', '/Q', $sedPath) `
                -Wait -PassThru -WindowStyle Hidden
            if ($iexpressProcess.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $installerExe)) {
                throw "IExpress failed to create HASHME Setup.exe (exit code $($iexpressProcess.ExitCode))."
            }
        } else {
            Write-Warning 'WiX 6 was not found. Portable release created; MSI and EXE compilation skipped.'
        }
    }
}
finally {
    Pop-Location
}

$hashes = Get-ChildItem $artifacts -File | Sort-Object Name | ForEach-Object {
    $hash = Get-FileHash -Algorithm SHA256 $_.FullName
    "{0}  {1}" -f $hash.Hash.ToLowerInvariant(), $_.Name
}
$hashes | Set-Content -Encoding ascii (Join-Path $artifacts 'SHA256SUMS_HASHME_v1.2.txt')
Write-Host "HASHME v1.2 release artifacts: $artifacts"
