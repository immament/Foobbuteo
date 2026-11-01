param([switch]$SkipGitUpdate)

& (Join-Path $PSScriptRoot 'build.ps1') -Target server -SkipGitUpdate:$SkipGitUpdate
exit $LASTEXITCODE
