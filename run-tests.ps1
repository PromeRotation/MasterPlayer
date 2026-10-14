# Runs the regression suite in one process per logical core (--shard=i/n).
# Usage: .\run-tests.ps1 [-Filter Blunderville] [-Shards N]
param([string]$Filter = "", [int]$Shards = [Environment]::ProcessorCount, [switch]$MeasuredLatency)
$ErrorActionPreference = "Stop"
# All cores busy inflate measured solve times; keep the scenarios' fixed latency unless asked.
if (-not $MeasuredLatency) { $env:MP_SIM_FIXED_LATENCY = "1" }
dotnet build tests/MasterPlayer.RegressionTests.csproj -p:CI=true -v:q -nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$exe = "tests/bin/Debug/net10.0-windows/MasterPlayer.RegressionTests.exe"
$dir = "bin/test-shards"; Remove-Item -Recurse -Force $dir -ErrorAction SilentlyContinue; New-Item -ItemType Directory -Force $dir | Out-Null
$watch = [Diagnostics.Stopwatch]::StartNew()
$procs = 0..($Shards - 1) | ForEach-Object {
    $a = @("--keep-going", "--shard=$_/$Shards"); if ($Filter) { $a += "--filter=$Filter" }
    Start-Process -FilePath $exe -ArgumentList $a -NoNewWindow -PassThru -RedirectStandardOutput "$dir/shard-$_.log" -RedirectStandardError "$dir/shard-$_.err"
}
$procs | Wait-Process
$lines = Get-Content "$dir/shard-*.log"
$pass = ($lines | Select-String '^PASS ').Count
$fail = $lines | Select-String '^FAIL '
"{0} shards, {1:F0}s, PASS {2}, FAIL {3}" -f $Shards, $watch.Elapsed.TotalSeconds, $pass, $fail.Count
$fail | ForEach-Object { $_.Line }
if ($fail.Count -gt 0) { exit 1 }

