[CmdletBinding()]
Param(
    [Parameter(Position=0,Mandatory=$false,ValueFromRemainingArguments=$true)]
    [string[]]$BuildArguments
)

Write-Output "PowerShell $($PSVersionTable.PSEdition) version $($PSVersionTable.PSVersion)"

Set-StrictMode -Version 2.0; $ErrorActionPreference = "Stop"; $ConfirmPreference = "None"; trap { Write-Error $_ -ErrorAction Continue; exit 1 }
$PSScriptRoot = Split-Path $MyInvocation.MyCommand.Path -Parent

###########################################################################
# CONFIGURATION
###########################################################################

$BuildProjectFile = "$PSScriptRoot\build\_build.csproj"
$TempDirectory = "$PSScriptRoot\\.nuke\temp"

$DotNetGlobalFile = "$PSScriptRoot\\global.json"
$DotNetInstallUrl = "https://dot.net/v1/dotnet-install.ps1"
$DotNetChannel = "Current"
$DotNetCliVersion = Get-Content DotnetCliVersion.txt

$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = 1
$env:DOTNET_CLI_TELEMETRY_OPTOUT = 1
$env:DOTNET_MULTILEVEL_LOOKUP = 0

###########################################################################
# EXECUTION
###########################################################################

function ExecSafe([scriptblock] $cmd) {
    $global:LASTEXITCODE = 0
    & $cmd
    if ($LASTEXITCODE) { exit $LASTEXITCODE }
}

try {
    $json = "{`"sdk`":{`"version`":`"$DotNetCliVersion`"}}"
    Out-File -FilePath $DotNetGlobalFile -Encoding utf8 -InputObject $json
    
    # If global.json exists, load expected version
    if (Test-Path $DotNetGlobalFile) {
        $DotNetGlobal = $(Get-Content $DotNetGlobalFile | Out-String | ConvertFrom-Json)
        if ($DotNetGlobal.PSObject.Properties["sdk"] -and $DotNetGlobal.sdk.PSObject.Properties["version"]) {
            $DotNetVersion = $DotNetGlobal.sdk.version
        }
    }
    
    $DotNetDirectory = "$TempDirectory\dotnet-win"
    $env:DOTNET_EXE = "$DotNetDirectory\dotnet.exe"

    # If dotnet CLI is installed globally and it matches requested version, use for execution
    if ($null -ne (Get-Command "dotnet" -ErrorAction SilentlyContinue) -and `
         $(dotnet --version) -eq $DotNetVersion) {
        $env:DOTNET_EXE = (Get-Command "dotnet").Path
    }
    # If dotnet CLI is installed locally and it matches requested version, use for execution
    elseif ($null -eq (Get-Command $env:DOTNET_EXE -ErrorAction SilentlyContinue) -or `
        !(Test-Path variable:DotNetVersion) -or $(& $env:DOTNET_EXE --version) -ne $DotNetVersion) {

        # Download install script
        $DotNetInstallFile = "$TempDirectory\dotnet-install.ps1"
        New-Item -ItemType Directory -Path $TempDirectory -Force | Out-Null
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        (New-Object System.Net.WebClient).DownloadFile($DotNetInstallUrl, $DotNetInstallFile)

        # Install by channel or version
        if (!(Test-Path variable:DotNetVersion)) {
            ExecSafe { & powershell $DotNetInstallFile -InstallDir $DotNetDirectory -Channel $DotNetChannel -NoPath }
        } else {
            ExecSafe { & powershell $DotNetInstallFile -InstallDir $DotNetDirectory -Version $DotNetVersion -NoPath }
        }
        $env:DOTNET_EXE = "$DotNetDirectory\dotnet.exe"
    }

    Write-Output "Microsoft (R) .NET Core SDK version $(& $env:DOTNET_EXE --version)"

    ExecSafe { & $env:DOTNET_EXE build $BuildProjectFile /nodeReuse:false /p:UseSharedCompilation=false -nologo -clp:NoSummary --verbosity quiet }
    ExecSafe { & $env:DOTNET_EXE run --project $BuildProjectFile --no-build -- $BuildArguments }
}
catch {
    Write-Output $_.Exception.Message
}
finally {
    if (Test-Path $DotNetGlobalFile) {
        Remove-Item $DotNetGlobalFile
    }
}