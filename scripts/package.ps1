param([string]$SourceDir, [string]$OutputDir, [string]$Compiler)
$ErrorActionPreference='Stop'
$repoRoot=Split-Path $PSScriptRoot -Parent
if(!$SourceDir){$SourceDir=Join-Path $repoRoot 'artifacts\LexPercentPro-win-x64'}
if(!$OutputDir){$OutputDir=Join-Path $repoRoot 'artifacts'}
if(!$Compiler){
    $candidates=@(
        "$env:ISCC_PATH",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        (Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'Codex\Tools\InnoSetup\compiler\ISCC.exe')
    )
    $Compiler=$candidates | Where-Object {$_ -and (Test-Path -LiteralPath $_)} | Select-Object -First 1
}
if(!$Compiler){throw 'Install Inno Setup 6 or pass -Compiler with the ISCC.exe path.'}
if(!(Test-Path -LiteralPath (Join-Path $SourceDir 'LexPercentPro.exe'))){throw 'Publish the application before packaging.'}
[xml]$project=Get-Content -LiteralPath (Join-Path $repoRoot 'src\LexPercent.UI\LexPercent.UI.csproj')
$version=@($project.Project.PropertyGroup.Version | Where-Object {$_})[0]
& $Compiler /Qp "/DSourceDir=$SourceDir" "/DOutputDir=$OutputDir" "/DAppVersion=$version" (Join-Path $repoRoot 'installer\LexPercentPro.iss')
if($LASTEXITCODE -ne 0){throw 'Installer compilation failed.'}
$file=Join-Path $OutputDir "LexPercentPro-Setup-$version.exe"
$hash=(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath "$file.sha256" -Value "$hash  $([IO.Path]::GetFileName($file))" -Encoding ascii
Write-Output $file
