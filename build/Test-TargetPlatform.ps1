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
    $evaluationCount = 0
    foreach ($profile in $profiles.Keys) {
        $profilePath = [System.IO.Path]::GetFullPath("OneLauncher.Desktop/Properties/PublishProfiles/$profile.pubxml")
        [xml]$profileXml = Get-Content -LiteralPath $profilePath -Raw
        $localGroup = $profileXml.SelectSingleNode('/Project/PropertyGroup')
        # VS profile discovery reads local XML; MSBuild evaluation alone cannot
        # detect a regression where these properties move into an Import.
        foreach ($field in @('Configuration', 'Platform', 'PublishDir', 'PublishUrl', 'PublishProtocol', '_TargetId', 'TargetFramework', 'SelfContained', 'PublishAot', 'PublishTrimmed', 'PublishSingleFile', 'PublishReadyToRun')) {
            $node = $localGroup.SelectSingleNode($field)
            if ($null -eq $node -or [string]::IsNullOrWhiteSpace($node.InnerText)) { throw "Missing local VS publish field: $profile/$field" }
        }
        if ($localGroup.PublishProtocol -ne 'FileSystem' -or $localGroup._TargetId -ne 'Folder') { throw "Not a VS folder publish profile: $profile" }
        $sizeFields = if ($profiles[$profile][2] -eq 'true') { @('OptimizationPreference') } else { @('IncludeNativeLibrariesForSelfExtract', 'IncludeAllContentForSelfExtract', 'EnableCompressionInSingleFile') }
        foreach ($field in $sizeFields) {
            if ($null -eq $localGroup.SelectSingleNode($field)) { throw "Missing local size setting: $profile/$field" }
        }
        foreach ($selector in @('PublishProfile', 'WebPublishProfileFile')) {
            $profileValue = if ($selector -eq 'PublishProfile') { $profile } else { $profilePath }
            foreach ($project in @('OneLauncher', 'OneLauncher.Core', 'OneLauncher.Console', 'OneLauncher.Desktop')) {
                $raw = & dotnet msbuild "$project/$project.csproj" "-p:$selector=$profileValue" '-getProperty:OneLauncherTargetOS,RuntimeIdentifier,PublishAot,DefineConstants,PublishProtocol,_TargetId,PublishDir,PublishUrl,Configuration,Platform,TargetFramework,SelfContained,PublishTrimmed,PublishSingleFile,PublishReadyToRun,DebugSymbols,DebugType,CopyOutputSymbolsToPublishDirectory,StripSymbols,PublishDocumentationFiles,TrimMode,OptimizationPreference,IncludeNativeLibrariesForSelfExtract,IncludeAllContentForSelfExtract,EnableCompressionInSingleFile' '-getItem:PackageReference' -nologo
                if ($LASTEXITCODE -ne 0) { throw "Evaluation failed: $project/$profile" }
                $evaluation = $raw -join "`n" | ConvertFrom-Json
                $props = $evaluation.Properties
                $expected = $profiles[$profile]
                $evaluationCount++
                if ($props.OneLauncherTargetOS -ne $expected[0] -or $props.RuntimeIdentifier -ne $expected[1] -or $props.PublishAot -ne $expected[2]) {
                    throw "Inconsistent target: $project/$profile $($props | ConvertTo-Json -Compress)"
                }
                $symbols = @($props.DefineConstants -split ';' | Where-Object { $_ -like 'ONELAUNCHER_TARGET_*' })
                $symbol = 'ONELAUNCHER_TARGET_' + $expected[0].ToUpperInvariant()
                if ($symbols.Count -ne 1 -or $symbols[0] -ne $symbol) { throw "Wrong target symbols: $project/$profile $symbols" }
                if ($props.PublishProtocol -ne 'FileSystem' -or $props._TargetId -ne 'Folder' -or $props.Configuration -ne 'Release' -or $props.Platform -ne 'Any CPU' -or $props.TargetFramework -ne 'net10.0') {
                    throw "Invalid VS publish settings: $project/$profile ($selector)"
                }
                $expectedSingleFile = if ($expected[2] -eq 'true') { 'false' } else { 'true' }
                if ($props.SelfContained -ne 'true' -or $props.PublishTrimmed -ne 'true' -or $props.PublishSingleFile -ne $expectedSingleFile -or $props.PublishReadyToRun -ne 'false') {
                    throw "Changed publish semantics: $project/$profile ($selector)"
                }
                if ($props.DebugSymbols -ne 'false' -or $props.DebugType -ne 'none' -or $props.CopyOutputSymbolsToPublishDirectory -ne 'false' -or $props.StripSymbols -ne 'true' -or $props.PublishDocumentationFiles -ne 'false' -or $props.TrimMode -ne 'full') {
                    throw "Unwanted symbols/docs or incorrect trimming: $project/$profile ($selector)"
                }
                if ($expected[2] -eq 'true') {
                    if ($props.OptimizationPreference -ne 'Size') { throw "AOT size optimization missing: $project/$profile ($selector)" }
                }
                elseif ($props.IncludeNativeLibrariesForSelfExtract -ne 'true' -or $props.IncludeAllContentForSelfExtract -ne 'false' -or $props.EnableCompressionInSingleFile -ne 'true') {
                    throw "Compressed native-inclusive JIT bundle missing: $project/$profile ($selector)"
                }
                $mode = if ($expected[2] -eq 'true') { 'aot' } else { 'jit' }
                $expectedOutput = [System.IO.Path]::GetFullPath("$project/bin/Release/net10.0/publish/$($expected[1])/$mode/")
                if ([System.IO.Path]::GetFullPath($props.PublishDir) -ne $expectedOutput -or [System.IO.Path]::GetFullPath($props.PublishUrl) -ne $expectedOutput) {
                    throw "VS/CLI output mismatch: $project/$profile ($selector)"
                }
                if ($project -eq 'OneLauncher.Core') {
                    $hasBroker = @($evaluation.Items.PackageReference.Identity) -contains 'Microsoft.Identity.Client.Broker'
                    if ($hasBroker -ne ($expected[0] -eq 'Windows')) { throw "Wrong platform dependency: $profile Broker=$hasBroker" }
                }
            }
        }

        # VS may also select by PublishProfileFullPath. Explicit CLI overrides
        # (the equivalent of -r and -o) must survive all profile imports.
        $alternateRid = $profiles[$profile][1] -replace '-(x64|arm64)$', $(if ($profiles[$profile][1] -like '*-x64') { '-arm64' } else { '-x64' })
        $overrideOutput = [System.IO.Path]::GetFullPath("bin/profile-override/$profile/") + [System.IO.Path]::DirectorySeparatorChar
        $raw = & dotnet msbuild 'OneLauncher.Desktop/OneLauncher.Desktop.csproj' "-p:PublishProfileFullPath=$profilePath" "-p:RuntimeIdentifier=$alternateRid" "-p:PublishDir=$overrideOutput" '-getProperty:RuntimeIdentifier,OneLauncherTargetOS,PublishDir,PublishUrl,PublishAot' -nologo
        if ($LASTEXITCODE -ne 0) { throw "Profile-path evaluation failed: $profile" }
        $props = ($raw -join "`n" | ConvertFrom-Json).Properties
        if ($props.RuntimeIdentifier -ne $alternateRid -or $props.OneLauncherTargetOS -ne $profiles[$profile][0] -or $props.PublishAot -ne $profiles[$profile][2] -or $props.PublishDir -ne $overrideOutput -or $props.PublishUrl -ne $overrideOutput) {
            throw "Profile path / RID / output override failed: $profile"
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
    foreach ($invalid in @('-p:ILLinkTargetsPath=', '-p:RunILLink=false')) {
        $result = & dotnet msbuild $project -t:ValidateOneLauncherTrimToolchain '-p:PublishTrimmed=true' '-p:PublishAot=false' $invalid -nologo 2>&1
        if ($LASTEXITCODE -eq 0 -or ($result -join "`n") -notmatch 'Trimming toolchain is missing or disabled') { throw "Missing/disabled trimmer guard failed: $invalid $result" }
    }
    Write-Host "PASS: 6 VS profile XML checks, $evaluationCount CLI/VS project evaluations (including size settings and Broker dependencies), 6 profile-path/RID/output override checks, and 9 invalid build/publish checks."
}
finally { Pop-Location }
