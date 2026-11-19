param([ValidateSet('WindowsAot', 'WindowsJit')][string]$PublishProfile = 'WindowsAot')
$ErrorActionPreference = 'Stop'
$workspacePath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Push-Location $workspacePath
try {
    $originalPath = $env:PATH
    if ($PublishProfile -eq 'WindowsAot') {
        $toolRoot = Join-Path $workspacePath 'build/toolchains'
        $toolBin = Join-Path $toolRoot 'llvm-mingw-20261020-ucrt-x86_64/bin'
        if (-not (Test-Path (Join-Path $toolBin 'llvm-ar.exe'))) {
            New-Item -ItemType Directory -Path $toolRoot -Force | Out-Null
            $archivePath = Join-Path $toolRoot 'llvm-mingw.zip'
            Invoke-WebRequest 'https://github.com/mstorsjo/llvm-mingw/releases/download/20261020/llvm-mingw-20261020-ucrt-x86_64.zip' -OutFile $archivePath
            if ((Get-FileHash $archivePath -Algorithm SHA256).Hash -ne 'FD5462B7F5D93B2FF981FCD3F9C7E427E47F3203C64EA91C27BCC444EEBD74CD') {
                throw 'LLVM archive checksum mismatch.'
            }
            Expand-Archive -LiteralPath $archivePath -DestinationPath $toolRoot -Force
        }
        # LLD selects the COFF driver from its filename.
        Copy-Item -LiteralPath (Join-Path $toolBin 'ld.lld.exe') -Destination (Join-Path $toolBin 'lld-link.exe') -Force
        $env:PATH = $toolBin + ';' + $env:PATH
    }
    try {
        # NativeSingleFile validates every file under PublishDir. Remove output
        # from previous publishes so stale project runtime files cannot fail the
        # single-file check.
        $publishDir = Join-Path $workspacePath 'OneLauncher.Desktop/bin/Release/net10.0/publish'
        if (Test-Path -LiteralPath $publishDir) {
            Remove-Item -LiteralPath $publishDir -Recurse -Force
        }
        # The pubxml owns RID, build mode and PublishDir.
        dotnet publish OneLauncher.Desktop/OneLauncher.Desktop.csproj "-p:PublishProfile=$PublishProfile" `
            --artifacts-path "build/output/$PublishProfile" --disable-build-servers
        if ($LASTEXITCODE -ne 0) { throw "Publish failed ($LASTEXITCODE)." }
    } finally { $env:PATH = $originalPath }
    Write-Host "Published using $PublishProfile.pubxml (output directory is defined by PublishDir)."
} finally { Pop-Location }
