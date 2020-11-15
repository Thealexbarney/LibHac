[CmdletBinding()]
Param(
    [Parameter(Position = 0, Mandatory = $false, ValueFromRemainingArguments = $true)]
    [string[]]$BuildArguments
)

Write-Output "Windows PowerShell $($Host.Version)"

Set-StrictMode -Version 2.0; $ErrorActionPreference = "Stop"; $ConfirmPreference = "None"; trap { exit 1 }
$PSScriptRoot = Split-Path $MyInvocation.MyCommand.Path -Parent

###########################################################################
# CONFIGURATION
###########################################################################

$BuildProjectFile = "$PSScriptRoot\build\_build.csproj"
$TempDirectory = "$PSScriptRoot\.tmp"

$DotNetGlobalFile = "$PSScriptRoot\global.json"
$DotNetInstallUrl = "https://dot.net/v1/dotnet-install.ps1"
$DotNetChannel = "Current"
$DotNetCliVersion = Get-Content DotnetCliVersion.txt

$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = 1
$env:DOTNET_CLI_TELEMETRY_OPTOUT = 1
$env:NUGET_XMLDOC_MODE = "skip"

###########################################################################
# EXECUTION
###########################################################################

function ExecSafe([scriptblock] $cmd) {
    & $cmd
    if ($LASTEXITCODE) { exit $LASTEXITCODE }
}

try {
    $json = "{`"sdk`":{`"version`":`"$DotNetCliVersion`"}}"
    Out-File -FilePath $DotNetGlobalFile -Encoding utf8 -InputObject $json

    # If global.json exists, load expected version
    if (Test-Path $DotNetGlobalFile) {
        $DotNetVersion = $(Get-Content $DotNetGlobalFile | Out-String | ConvertFrom-Json).sdk.version
    }

    $DotNetDirectory = "$TempDirectory\dotnet-win"
    $env:DOTNET_EXE = "$DotNetDirectory\dotnet.exe"

    # If dotnet is installed locally, and expected version is not set or installation matches the expected version
    if ($null -ne (Get-Command "dotnet" -ErrorAction SilentlyContinue) -and `
        (!(Test-Path variable:DotNetVersion) -or $(& cmd.exe /c 'dotnet --version 2>&1') -eq $DotNetVersion)) {
        $env:DOTNET_EXE = (Get-Command "dotnet").Path
    }
    elseif ($null -eq (Get-Command $env:DOTNET_EXE -ErrorAction SilentlyContinue) -or `
        !(Test-Path variable:DotNetVersion) -or $(& cmd.exe /c "$env:DOTNET_EXE --version 2>&1") -ne $DotNetVersion) {

        # Download install script
        $DotNetInstallFile = "$TempDirectory\dotnet-install.ps1"
        New-Item -ItemType Directory -Path $TempDirectory -Force | Out-Null
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        (New-Object System.Net.WebClient).DownloadFile($DotNetInstallUrl, $DotNetInstallFile)

        # Install by channel or version
        if (!(Test-Path variable:DotNetVersion)) {
            ExecSafe { & $DotNetInstallFile -InstallDir $DotNetDirectory -Channel $DotNetChannel -NoPath }
        }
        else {
            ExecSafe { & $DotNetInstallFile -InstallDir $DotNetDirectory -Version $DotNetVersion -NoPath }
        }
    }

    # Make sure we have a 3.1 runtime. At the time of writing GitVersion doesn't have a 5.0 build
    # Remove when a 5.0 build is available
    if((& $env:DOTNET_EXE --list-runtimes | Out-String) -notlike "*Microsoft.NETCore.App 3.1.*") {

        # Download install script
        $DotNetInstallFile = "$TempDirectory\dotnet-install.ps1"
        New-Item -ItemType Directory -Path $TempDirectory -Force | Out-Null
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        (New-Object System.Net.WebClient).DownloadFile($DotNetInstallUrl, $DotNetInstallFile)

        ExecSafe { & $DotNetInstallFile -InstallDir $DotNetDirectory -Channel 3.1 -Runtime dotnet -NoPath }
    }

    Write-Output "Microsoft (R) .NET Core SDK version $(& $env:DOTNET_EXE --version)"

    ExecSafe { & $env:DOTNET_EXE run --project $BuildProjectFile -- $BuildArguments }
}
catch {
    Write-Output $_.Exception.Message
}
finally {
    if (Test-Path $DotNetGlobalFile) {
        Remove-Item $DotNetGlobalFile
    }
}