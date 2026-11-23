#Requires -Version 7.0
param(
    [Parameter(Mandatory)]
    [ValidatePattern('\A(?:0|[1-9][0-9]{0,4})(?:\.(?:0|[1-9][0-9]{0,4})){3}\z')]
    [string]$Version,
    [Parameter(Mandatory)] [string]$Repository,
    [Parameter(Mandatory)] [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$output = (New-Item -ItemType Directory -Force -Path $OutputDirectory).FullName

Push-Location $projectRoot
try {
    dotnet build MasterPlayer.csproj -c Release -p:CI=true -p:UsePromeRotationSdk=true "-p:Version=$Version"
    if ($LASTEXITCODE) { throw "Build failed with exit code $LASTEXITCODE." }

    $sdkMetadata = dotnet msbuild MasterPlayer.csproj -nologo -p:Configuration=Release -p:UsePromeRotationSdk=true `
        '-getProperty:PkgPromeRotation_SDK_CNGL,PromeRotationSdkDalamudApiLevel'
    if ($LASTEXITCODE) { throw "Could not read PromeRotation SDK properties (exit code $LASTEXITCODE)." }
    $sdk = ($sdkMetadata | ConvertFrom-Json).Properties
}
finally {
    Pop-Location
}

$promeAssembly = Join-Path $sdk.PkgPromeRotation_SDK_CNGL 'ref/net10.0-windows7.0/PromeRotation.dll'
$promeVersion = [Reflection.AssemblyName]::GetAssemblyName($promeAssembly).Version.ToString()
$apiLevel = [int]$sdk.PromeRotationSdkDalamudApiLevel

$build = Join-Path $projectRoot 'bin/Release'
$builtVersion = [Reflection.AssemblyName]::GetAssemblyName((Join-Path $build 'MasterPlayer.dll')).Version.ToString()
if ($builtVersion -cne $Version) { throw "Expected plugin version $Version, built $builtVersion." }

$package = New-Item -ItemType Directory -Path (Join-Path $output 'MasterPlayer')
$notices = New-Item -ItemType Directory -Path (Join-Path $package.FullName 'ThirdPartyNotices')
foreach ($file in 'MasterPlayer.dll', 'MasterPlayer.MechanicRuntime.dll', 'MasterPlayer.deps.json', 'DotRecast.Core.dll', 'DotRecast.Recast.dll') {
    Copy-Item -LiteralPath (Join-Path $build $file) -Destination $package.FullName
}
foreach ($file in 'BossModReborn.LICENSE', 'PromeRotation.LICENSE', 'DotRecast.LICENSE', 'PromeDrawPlus.NOTICE') {
    Copy-Item -LiteralPath (Join-Path $build "ThirdPartyNotices/$file") -Destination $notices.FullName
}

$tag = "v$Version"
$zipName = "PromeRadar-$tag.zip"
$zip = Join-Path $output $zipName
[IO.Compression.ZipFile]::CreateFromDirectory($package.FullName, $zip, [IO.Compression.CompressionLevel]::Optimal, $true)
dotnet run --project (Join-Path $projectRoot 'tests/ReleasePackaging/MasterPlayer.ReleasePackaging.Tests.csproj') -c Release -p:UsePromeRotationSdk=true -- $zip
if ($LASTEXITCODE) { throw "Release package terrain validation failed with exit code $LASTEXITCODE." }
$manifest = [ordered]@{
    id = 'masterplayer'
    name = 'PromeRadar'
    author = 'XSZ'
    description = '雷达悬浮窗 + 真实世界绘制 + 智能走位 + 机制库 + 录制回放'
    version = $builtVersion
    tags = @('combat', 'utility')
    apiVersion = $apiLevel
    referencePromeVersion = $promeVersion
    downloadUrl = "https://github.com/$Repository/releases/download/$tag/$zipName"
    sourceRepositoryUrl = "https://github.com/$Repository"
    sha256 = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
}
$json = ConvertTo-Json -InputObject @($manifest)
[IO.File]::WriteAllText((Join-Path $output 'masterplayer.json'), $json.Replace("`r`n", "`n") + "`n")
Write-Host "Packaged $zipName for PromeRotation $promeVersion (Dalamud API $($manifest.apiVersion))."
