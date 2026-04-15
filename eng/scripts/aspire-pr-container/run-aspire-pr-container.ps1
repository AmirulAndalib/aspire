[CmdletBinding()]
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$RemainingArgs
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Show-Usage {
    @'
Usage:
  run-aspire-pr-container.ps1 PR_NUMBER [get-aspire-cli-pr.ps1 options...]
  run-aspire-pr-container.ps1 bash [args...]
  run-aspire-pr-container.ps1 sh [args...]

Environment:
  ASPIRE_PR_IMAGE          Docker image name to build/run (default: aspire-pr-runner)
  ASPIRE_PR_WORKSPACE      Host directory to mount as /workspace (default: current directory)
  INSTALL_PREFIX           In-container install prefix (default: /workspace/.aspire)
  ASPIRE_DOCKER_SOCKET     Host Docker socket path to mount into /var/run/docker.sock
                           (default: /var/run/docker.sock)
  ASPIRE_CONTAINER_USER    Container user for docker run
                           Default: 0:0 on Windows, current uid:gid elsewhere when available
  ASPIRE_PR_RECORD         Set to 1/true to record the full host-side session with asciinema
  ASPIRE_PR_RECORDING_PATH Output path for the .cast file
                           (default: <workspace>/recordings/<timestamp>-<command>.cast)
  ASPIRE_PR_RECORDING_TITLE
                           Optional title stored in the recording metadata
  GH_TOKEN/GITHUB_TOKEN    GitHub token passed into the container
'@ | Write-Host
}

function Test-Truthy {
    param([string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return $false
    }

    switch ($Value.Trim().ToLowerInvariant()) {
        '1' { return $true }
        'true' { return $true }
        'yes' { return $true }
        'on' { return $true }
        default { return $false }
    }
}

function Get-RecordingStem {
    param([string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return 'session'
    }

    $stem = $Value
    if ($stem -match '^\d+$') {
        $stem = "pr-$stem"
    }

    $stem = [System.Text.RegularExpressions.Regex]::Replace($stem.ToLowerInvariant(), '[^a-z0-9._-]+', '-').Trim('-')
    if ([string]::IsNullOrWhiteSpace($stem)) {
        return 'session'
    }

    return $stem
}

function Test-InteractiveConsole {
    try {
        return -not [Console]::IsInputRedirected -and -not [Console]::IsOutputRedirected
    }
    catch {
        return $false
    }
}

function Get-CurrentUidGid {
    try {
        $uid = (& id -u 2>$null).Trim()
        $gid = (& id -g 2>$null).Trim()
        if ($LASTEXITCODE -eq 0 -and $uid -and $gid) {
            return "$uid`:$gid"
        }
    }
    catch {
    }

    return $null
}

if (-not $RemainingArgs -or $RemainingArgs.Count -lt 1) {
    Show-Usage
    exit 1
}

if ($RemainingArgs[0] -in @('-h', '--help')) {
    Show-Usage
    exit 0
}

$scriptDir = Split-Path -Parent $PSCommandPath
$imageName = if ($env:ASPIRE_PR_IMAGE) { $env:ASPIRE_PR_IMAGE } else { 'aspire-pr-runner' }
$workspace = if ($env:ASPIRE_PR_WORKSPACE) { $env:ASPIRE_PR_WORKSPACE } else { (Get-Location).Path }
$installPrefix = if ($env:INSTALL_PREFIX) { $env:INSTALL_PREFIX } else { '/workspace/.aspire' }
$dockerSocketPath = if ($env:ASPIRE_DOCKER_SOCKET) { $env:ASPIRE_DOCKER_SOCKET } else { '/var/run/docker.sock' }
$isWindows = $env:OS -eq 'Windows_NT'

$containerUser = $env:ASPIRE_CONTAINER_USER
if ([string]::IsNullOrWhiteSpace($containerUser)) {
    if ($isWindows) {
        $containerUser = '0:0'
    }
    else {
        $containerUser = Get-CurrentUidGid
    }
}

if (-not (Test-Path -LiteralPath $workspace -PathType Container)) {
    Write-Error "Workspace directory does not exist: $workspace"
    exit 1
}

if (-not $env:ASPIRE_PR_RECORDING_ACTIVE -and (Test-Truthy $env:ASPIRE_PR_RECORD)) {
    $asciinema = Get-Command asciinema -ErrorAction SilentlyContinue
    if ($null -eq $asciinema) {
        Write-Error 'asciinema is required when ASPIRE_PR_RECORD is enabled.'
        exit 1
    }

    $recordingPath = if ($env:ASPIRE_PR_RECORDING_PATH) {
        $env:ASPIRE_PR_RECORDING_PATH
    }
    else {
        $timestamp = (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssZ')
        Join-Path $workspace "recordings/$timestamp-$(Get-RecordingStem $RemainingArgs[0]).cast"
    }

    $recordingDir = Split-Path -Parent $recordingPath
    if (-not [string]::IsNullOrWhiteSpace($recordingDir)) {
        New-Item -ItemType Directory -Force -Path $recordingDir | Out-Null
    }

    $argLiterals = @(
        foreach ($arg in $RemainingArgs) {
            "'{0}'" -f ($arg -replace "'", "''")
        }
    )

    $encodedCommandText = "& '{0}' @({1}); exit `$LASTEXITCODE" -f ($PSCommandPath -replace "'", "''"), ($argLiterals -join ', ')
    $encodedCommandBytes = [System.Text.Encoding]::Unicode.GetBytes($encodedCommandText)
    $encodedCommand = [Convert]::ToBase64String($encodedCommandBytes)
    $powerShellCommand = if ($PSVersionTable.PSEdition -eq 'Core') {
        "pwsh -NoProfile -EncodedCommand $encodedCommand"
    }
    else {
        "powershell -NoProfile -ExecutionPolicy Bypass -EncodedCommand $encodedCommand"
    }

    $env:ASPIRE_PR_RECORDING_ACTIVE = '1'
    Write-Host "Recording session to $recordingPath"

    $recordArgs = @(
        'record'
        '--return'
        '--command'
        $powerShellCommand
    )

    if ($env:ASPIRE_PR_RECORDING_TITLE) {
        $recordArgs += @('--title', $env:ASPIRE_PR_RECORDING_TITLE)
    }

    $recordArgs += $recordingPath

    & $asciinema.Source @recordArgs
    exit $LASTEXITCODE
}

if (-not $env:GH_TOKEN -and -not $env:GITHUB_TOKEN) {
    $env:GH_TOKEN = (& gh auth token).Trim()
}
elseif (-not $env:GH_TOKEN -and $env:GITHUB_TOKEN) {
    $env:GH_TOKEN = $env:GITHUB_TOKEN
}

$ttyArgs = @()
if (Test-InteractiveConsole) {
    $ttyArgs = @('-it')
}

$runArgs = @(
    'run'
    '--rm'
    '-e'
    'GH_TOKEN'
    '-e'
    'ASPIRE_REPO'
    '-e'
    "INSTALL_PREFIX=$installPrefix"
    '-e'
    'HOME=/workspace'
)

if (-not [string]::IsNullOrWhiteSpace($containerUser)) {
    $runArgs += @('-u', $containerUser)
}

$runArgs += @(
    '-v'
    "${workspace}:/workspace"
    '-w'
    '/workspace'
)

if ($ttyArgs.Count -gt 0) {
    $runArgs += $ttyArgs
}

if (-not [string]::IsNullOrWhiteSpace($dockerSocketPath)) {
    if ($dockerSocketPath -eq '/var/run/docker.sock') {
        $runArgs += @('-v', '/var/run/docker.sock:/var/run/docker.sock')
    }
    elseif (Test-Path -LiteralPath $dockerSocketPath) {
        $resolvedDockerSocketPath = (Resolve-Path -LiteralPath $dockerSocketPath).Path
        $runArgs += @('-v', "${resolvedDockerSocketPath}:/var/run/docker.sock")
    }
}

& docker build -t $imageName $scriptDir
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$runArgs += $imageName
$runArgs += $RemainingArgs

& docker @runArgs
exit $LASTEXITCODE
