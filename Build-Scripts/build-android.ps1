param([switch]$SkipGitUpdate)

& (Join-Path $PSScriptRoot 'build.ps1') -Target android -SkipGitUpdate:$SkipGitUpdate
exit $LASTEXITCODE
