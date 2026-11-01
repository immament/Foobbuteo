param([switch]$SkipGitUpdate)

& (Join-Path $PSScriptRoot 'build.ps1') -Target web-fast -SkipGitUpdate:$SkipGitUpdate
exit $LASTEXITCODE
