param([switch]$SkipGitUpdate)

& (Join-Path $PSScriptRoot 'build.ps1') -Target web-release -SkipGitUpdate:$SkipGitUpdate
exit $LASTEXITCODE
