# Runs two copies of the already-built test player on loopback only.
# Does not build, edit project settings, change execution policy, open firewall rules or upload evidence.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9a-fA-F]{40}$')]
    [string]$Revision,
    [string]$Player = (Join-Path $PSScriptRoot '..\Builds\LanRange\UndeadOvertimeLanRange.exe'),
    [ValidateRange(0, 65535)]
    [int]$Port = 0
)
$ErrorActionPreference = 'Stop'
$hostProcess = $null
$clientProcess = $null
$exitCode = 1
try {
    $Player = (Resolve-Path -LiteralPath $Player).Path
    if ([IO.Path]::GetExtension($Player) -ne '.exe') { throw 'Expected the built Windows test player .exe.' }
    if ($Port -eq 0) {
        $endpoint = [Net.IPEndPoint]::new([Net.IPAddress]::Loopback, 0)
        $reservation = [Net.Sockets.UdpClient]::new($endpoint)
        try { $Port = ([Net.IPEndPoint]$reservation.Client.LocalEndPoint).Port } finally { $reservation.Dispose() }
        # Reservation is released before Unity binds. A race is possible and is reported, never worked around silently.
    }
    if ($Port -lt 1024) { throw 'Use a non-privileged loopback port from 1024 through 65535.' }
    $run = [Guid]::NewGuid().ToString('N')
    $directory = Join-Path ([IO.Path]::GetDirectoryName($Player)) ('SmokeResults\' + $run)
    New-Item -ItemType Directory -Path $directory -ErrorAction Stop | Out-Null
    $started = [DateTime]::UtcNow
    $playerHash = (Get-FileHash -LiteralPath $Player -Algorithm SHA256).Hash.ToLowerInvariant()
    $playerDirectory = [IO.Path]::GetDirectoryName($Player)
    $codeFiles = @($Player)
    $managed = Join-Path $playerDirectory 'UndeadOvertimeLanRange_Data\Managed'
    if (Test-Path -LiteralPath $managed) { $codeFiles += @(Get-ChildItem -LiteralPath $managed -Filter 'UndeadOvertime*.dll' -File | ForEach-Object { $_.FullName }) }
    $il2cpp = Join-Path $playerDirectory 'GameAssembly.dll'
    if (Test-Path -LiteralPath $il2cpp) { $codeFiles += $il2cpp }
    $fingerprints = @($codeFiles | Sort-Object -Unique | ForEach-Object {
        [ordered]@{ file = $_.Substring($playerDirectory.Length + 1); sha256 = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant(); bytes = (Get-Item -LiteralPath $_).Length }
    })
    $common = '-batchmode -nographics -uo-smoke-run ' + $run + ' -uo-smoke-port ' + $Port + ' -uo-smoke-revision ' + $Revision.ToLowerInvariant()
    $hostArguments = $common + ' -uo-smoke-role host -logFile "' + (Join-Path $directory 'host.log') + '"'
    $clientArguments = $common + ' -uo-smoke-role client -logFile "' + (Join-Path $directory 'client.log') + '"'
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $hostProcess = Start-Process -FilePath $Player -ArgumentList $hostArguments -PassThru
    $hostReady = $false
    $readyDeadlineSeconds = 30
    while (!$hostReady -and $watch.Elapsed.TotalSeconds -lt $readyDeadlineSeconds) {
        if ($hostProcess.HasExited) { throw ('Host exited before client launch. Read ' + (Join-Path $directory 'host.log')) }
        $hostLog = Join-Path $directory 'host.log'
        if (Test-Path -LiteralPath $hostLog) { $hostReady = [bool](Select-String -LiteralPath $hostLog -SimpleMatch 'UO_LAN_SMOKE_STEP host host-started' -Quiet -ErrorAction SilentlyContinue) }
        if (!$hostReady) { Start-Sleep -Milliseconds 100 }
    }
    if (!$hostReady) { throw 'Host did not report loopback readiness within 30 seconds.' }
    $clientProcess = Start-Process -FilePath $Player -ArgumentList $clientArguments -PassThru
    while ((!$hostProcess.HasExited -or !$clientProcess.HasExited) -and $watch.Elapsed.TotalSeconds -lt 120) { Start-Sleep -Milliseconds 250 }
    if (!$hostProcess.HasExited -or !$clientProcess.HasExited) { throw 'Two-process test exceeded 120 seconds.' }
    $hostProcess.WaitForExit(); $clientProcess.WaitForExit()
    $hostReport = Get-Content -LiteralPath (Join-Path $directory 'host.json') -Raw | ConvertFrom-Json
    $clientReport = Get-Content -LiteralPath (Join-Path $directory 'client.json') -Raw | ConvertFrom-Json
    foreach ($report in @($hostReport, $clientReport)) {
        if ($report.runId -ne $run -or $report.requestedRevision -ne $Revision.ToLowerInvariant() -or $report.port -ne $Port) { throw 'Report identity does not match this launched test.' }
        if ($report.formatVersion -ne 1) { throw 'Unexpected report format.' }
        if ($report.runtimeErrorCount -ne 0) { throw ($report.role + ' reported Unity runtime errors: ' + $report.firstRuntimeError) }
        if (!$report.success) { throw ($report.role + ' failed: ' + $report.error) }
    }
    if ($hostReport.role -ne 'host' -or $clientReport.role -ne 'client' -or
        $hostReport.processId -ne $hostProcess.Id -or $clientReport.processId -ne $clientProcess.Id -or
        $hostProcess.Id -eq $clientProcess.Id) { throw 'Reports do not prove two distinct launched processes.' }
    if ($hostReport.steps -notcontains 'host-shutdown-completed' -or $clientReport.steps -notcontains 'host-shutdown-observed') { throw 'A terminal lifecycle check is missing.' }
    if ($hostReport.steps -notcontains 'remote-movement-observed' -or $clientReport.steps -notcontains 'replicated-movement-and-expiry') { throw 'The movement/expiry check is missing.' }
    if ($hostReport.steps -notcontains 'infected-result-acknowledged' -or $clientReport.steps -notcontains 'replicated-infected-kill') { throw 'The infected encounter check is missing.' }
    if ($hostReport.steps -notcontains 'revived-client-action-acknowledged' -or $clientReport.steps -notcontains 'revived-client-actions-replicated') { throw 'The co-op revive check is missing.' }
    if ($hostProcess.ExitCode -ne 0 -or $clientProcess.ExitCode -ne 0) { throw 'A test player returned a nonzero exit code.' }
    $summary = [ordered]@{
        formatVersion = 1; success = $true; runId = $run; requestedRevision = $Revision.ToLowerInvariant()
        playerSha256 = $playerHash; buildFiles = $fingerprints; hostProcessId = $hostProcess.Id; clientProcessId = $clientProcess.Id
        port = $Port; startedUtc = $started.ToString('O'); finishedUtc = [DateTime]::UtcNow.ToString('O')
        hostExitCode = $hostProcess.ExitCode; clientExitCode = $clientProcess.ExitCode
        scope = 'Automated two-process 127.0.0.1 only. Source revision must be verified by the build runner; this is not real LAN or ten-player acceptance.'
    }
    $summary | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $directory 'summary.json') -Encoding UTF8
    Write-Host ('PASS: two-process loopback. Evidence: ' + $directory)
    $exitCode = 0
} catch {
    Write-Error -Message $_ -ErrorAction Continue
    if ($directory) { Write-Host ('Evidence, if created: ' + $directory) }
} finally {
    # Stop only child processes launched by this script, never the editor or other Unity/player instances.
    foreach ($child in @($clientProcess, $hostProcess)) {
        if ($null -ne $child -and !$child.HasExited) { try { $child.Kill(); $child.WaitForExit(5000) | Out-Null } catch { Write-Warning 'A launched test player could not be stopped; inspect its recorded PID.' } }
        if ($null -ne $child) { $child.Dispose() }
    }
}
exit $exitCode
