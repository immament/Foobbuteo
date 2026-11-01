[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('web-dev', 'web-fast', 'web-release', 'server', 'android')]
    [string]$Target,

    [switch]$SkipGitUpdate
)

$ErrorActionPreference = 'Stop'
# Native failures are handled explicitly so their exit codes reach the caller.
$PSNativeCommandUseErrorActionPreference = $false
$rootDir = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

if (-not $SkipGitUpdate) {
    & git -C $rootDir fetch
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    & git -C $rootDir pull --ff-only
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}

$buildSettings = switch ($Target) {
    'web-dev' {
        @{ Profile = 'Web - Desktop - Development.asset'; Name = 'web-dev'; OutputFile = '' }
    }
    'web-fast' {
        @{ Profile = 'Web - Desktop - Fast.asset'; Name = 'web-fast-dev'; OutputFile = '' }
    }
    'web-release' {
        @{ Profile = 'Web - Desktop - Release.asset'; Name = 'web-release'; OutputFile = '' }
    }
    'server' {
        @{ Profile = 'Windows Server.asset'; Name = 'server-release'; OutputFile = 'Foobbuteo.exe' }
    }
    'android' {
        @{ Profile = 'Android™.asset'; Name = 'android-release'; OutputFile = 'foobbuteo.apk' }
    }
}

$buildProfile = "Assets/Settings/Build Profiles/$($buildSettings.Profile)"
if (-not (Test-Path -LiteralPath (Join-Path $rootDir $buildProfile) -PathType Leaf)) {
    throw "Build profile '$buildProfile' does not exist."
}

$projectVersionFile = Join-Path $rootDir 'ProjectSettings/ProjectVersion.txt'
$projectVersionLine = Select-String -LiteralPath $projectVersionFile -Pattern '^m_EditorVersion:\s*(\S+)\s*$'
if (-not $projectVersionLine) {
    throw "Could not resolve the Unity Editor version from '$projectVersionFile'."
}

$unityVersion = $projectVersionLine.Matches[0].Groups[1].Value
$unity = "C:\Program Files\Unity\Hub\Editor\$unityVersion\Editor\Unity.exe"
if (-not (Test-Path -LiteralPath $unity -PathType Leaf)) {
    throw "Unity Editor $unityVersion is not installed at '$unity'."
}

$buildName = "$($buildSettings.Name)"
$buildsDir = Join-Path (Split-Path -Parent $rootDir) 'Foobbuteo-Builds'
$buildOutputDir = Join-Path $buildsDir $buildName
$buildLogDir = Join-Path $buildsDir 'BuildLogs'
$buildLog = Join-Path $buildLogDir "$buildName.log"
$buildOutput = $buildOutputDir
if ($buildSettings.OutputFile) {
    $buildOutput = Join-Path $buildOutputDir $buildSettings.OutputFile
}

New-Item -ItemType Directory -Force -Path $buildOutputDir | Out-Null
New-Item -ItemType Directory -Force -Path $buildLogDir | Out-Null

$unityArguments = @(
    '-batchmode'
    '-disableManagedDebugger'
    '-quit'
    '-projectPath', $rootDir
    '-activeBuildProfile', $buildProfile
    '-build', $buildOutput
    '-logFile', '-'
)

$previousErrorActionPreference = $ErrorActionPreference
try {
    # Windows PowerShell treats redirected native stderr as errors, even on successful builds.
    $ErrorActionPreference = 'Continue'
    & $unity @unityArguments 2>&1 |
        ForEach-Object { $_.ToString() } -ErrorAction Stop |
        Tee-Object -FilePath $buildLog -ErrorAction Stop
    $unityExitCode = $LASTEXITCODE
}
finally {
    $ErrorActionPreference = $previousErrorActionPreference
}

# Unity can exit normally after a failed player build, so also check its build report and artifact.
$buildResultPattern = '^\s*(?:Build Finished,\s*Result:\s*|Build completed with a result of\s+[''"]?)(?<result>\w+)'
$buildReport = Select-String -LiteralPath $buildLog -Pattern $buildResultPattern | Select-Object -Last 1
$buildFailure = Select-String -LiteralPath $buildLog -Pattern '^\s*(?:BuildFailedException:|Error building Player:|DisplayProgressNotification: Build Failed\s*$)' | Select-Object -Last 1
$failureReason = $null

if ($unityExitCode -ne 0) {
    $failureReason = "Unity exited with code $unityExitCode."
}
elseif ($buildFailure) {
    $failureReason = $buildFailure.Line.Trim()
}
elseif (-not $buildReport) {
    $failureReason = 'Unity did not report a successful build.'
}
elseif ($buildReport.Matches[0].Groups['result'].Value -notin @('Success', 'Succeeded')) {
    $failureReason = "Unity reported build result: $($buildReport.Matches[0].Groups['result'].Value)."
}
else {
    $expectedArtifact = if ($buildSettings.OutputFile) { $buildOutput } else { Join-Path $buildOutput 'index.html' }
    if (-not (Test-Path -LiteralPath $expectedArtifact -PathType Leaf)) {
        $failureReason = "Expected build output was not created: $expectedArtifact"
    }
}

if ($failureReason) {
    Write-Host "BUILD FAILED ($Target): $failureReason" -ForegroundColor Red
    Write-Host "Build log: $buildLog"
    if ($unityExitCode -eq 0) {
        $unityExitCode = 1
    }
    exit $unityExitCode
}

Write-Host "BUILD SUCCEEDED ($Target): $buildOutput" -ForegroundColor Green
Write-Host "Build log: $buildLog"
exit 0
