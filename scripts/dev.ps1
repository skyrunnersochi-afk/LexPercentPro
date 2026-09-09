param([ValidateSet('build','test','run','publish')][string]$Action='build', [string]$Output)
$ErrorActionPreference='Stop'
$repoRoot=Split-Path $PSScriptRoot -Parent
$bundled=Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'Codex\Tools\dotnet\dotnet.exe'
$dotnet=if(Test-Path -LiteralPath $bundled){$bundled}else{'dotnet'}
Push-Location $repoRoot
try {
    switch($Action) {
        build { & $dotnet build LexPercentPro.slnx -v minimal }
        test { & $dotnet test tests/LexPercent.Tests -c Release --logger 'trx;LogFileName=tests.trx' }
        run { & $dotnet run --project src/LexPercent.UI }
        publish {
            if(!$Output){$Output=Join-Path $repoRoot 'artifacts\LexPercentPro-win-x64'}
            & $dotnet test tests/LexPercent.Tests -c Release -v minimal
            if($LASTEXITCODE -ne 0){throw 'Tests failed'}
            & $dotnet publish src/LexPercent.UI -c Release -r win-x64 --self-contained true -p:PublishTrimmed=false -o $Output
            if($LASTEXITCODE -ne 0){throw 'Publish failed'}
            Copy-Item -LiteralPath 'README.md','CHANGELOG.md','LICENSE' -Destination $Output
        }
    }
    if($LASTEXITCODE -ne 0){throw "dotnet exit code $LASTEXITCODE"}
} finally { Pop-Location }
