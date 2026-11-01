param([switch]$SkipGitUpdate)

& (Join-Path $PSScriptRoot 'build.ps1') -Target web-dev -SkipGitUpdate:$SkipGitUpdate
exit $LASTEXITCODE
