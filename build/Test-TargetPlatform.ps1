$ErrorActionPreference = 'Stop'
Push-Location (Join-Path $PSScriptRoot '..')
try {
    $profiles = @{
        WindowsAot = @('Windows', 'win-x64', 'true')
        WindowsJit = @('Windows', 'win-x64', 'false')
        LinuxAot = @('Linux', 'linux-x64', 'true')
        LinuxJit = @('Linux', 'linux-x64', 'false')
        MacOSAot = @('macOS', 'osx-arm64', 'true')
        MacOSJit = @('macOS', 'osx-arm64', 'false')
    }
    foreach ($profile in $profiles.Keys) {
        foreach ($project in @('OneLauncher', 'OneLauncher.Core', 'OneLauncher.Console', 'OneLauncher.Desktop')) {
            $raw = & dotnet msbuild "$project/$project.csproj" "-p:PublishProfile=$profile" '-getProperty:OneLauncherTargetOS,RuntimeIdentifier,PublishAot,DefineConstants' '-getItem:PackageReference' -nologo
            if ($LASTEXITCODE -ne 0) { throw "Evaluation failed: $project/$profile" }
            $evaluation = $raw -join "`n" | ConvertFrom-Json
            $props = $evaluation.Properties
            $expected = $profiles[$profile]
            if ($props.OneLauncherTargetOS -ne $expected[0] -or $props.RuntimeIdentifier -ne $expected[1] -or $props.PublishAot -ne $expected[2]) {
                throw "Inconsistent target: $project/$profile $($props | ConvertTo-Json -Compress)"
            }
            $symbols = @($props.DefineConstants -split ';' | Where-Object { $_ -like 'ONELAUNCHER_TARGET_*' })
            $symbol = 'ONELAUNCHER_TARGET_' + $expected[0].ToUpperInvariant()
            if ($symbols.Count -ne 1 -or $symbols[0] -ne $symbol) { throw "Wrong target symbols: $project/$profile $symbols" }
            if ($project -eq 'OneLauncher.Core') {
                $hasBroker = @($evaluation.Items.PackageReference.Identity) -contains 'Microsoft.Identity.Client.Broker'
                if ($hasBroker -ne ($expected[0] -eq 'Windows')) { throw "Wrong platform dependency: $profile Broker=$hasBroker" }
            }
        }
    }

    $project = 'OneLauncher.Desktop/OneLauncher.Desktop.csproj'
    foreach ($invalid in @(
        @('-p:Configuration=Release'),
        @('-p:PublishProfile=WindowsAot', '-p:RuntimeIdentifier=linux-x64'),
        @('-p:Configuration=Release', '-p:RuntimeIdentifier=freebsd-x64')
    )) {
        $result = & dotnet msbuild $project -t:ValidateOneLauncherTargetPlatform @invalid -nologo 2>&1
        if ($LASTEXITCODE -eq 0 -or ($result -join "`n") -notmatch 'Directory.Build.targets') {
            throw "Expected target validation failure: $invalid $result"
        }
    }
    foreach ($invalid in @(
        @('-p:Configuration=Debug'),
        @('-p:PublishProfile=WindowsAot', '-p:PublishAot=false'),
        @('-p:PublishProfile=WindowsJit', '-p:PublishAot=true')
    )) {
        $result = & dotnet msbuild $project -t:ValidateOneLauncherPublishTarget @invalid -nologo 2>&1
        if ($LASTEXITCODE -eq 0 -or ($result -join "`n") -notmatch 'PublishValidation.targets') {
            throw "Expected publish validation failure: $invalid $result"
        }
    }
    $result = & dotnet msbuild $project -t:ValidateOneLauncherAotToolchain '-p:PublishAot=true' '-p:NativeCompilationDuringPublish=false' -nologo 2>&1
    if ($LASTEXITCODE -eq 0 -or ($result -join "`n") -notmatch 'Native AOT toolchain is missing') { throw "Missing native compiler guard failed: $result" }
    Write-Host 'PASS: 24 project/profile evaluations (including Broker dependencies) and 7 invalid build/publish checks.'
}
finally { Pop-Location }
