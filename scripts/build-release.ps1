param(
    [string]$Version = "1.1.0"
)

$ErrorActionPreference = "Stop"

$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$artifacts = Join-Path $root "artifacts"
$packageRoot = Join-Path $artifacts "package"
$selfContainedName = "FH6RoadScanner-$Version-win-x64-self-contained"
$frameworkDependentName = "FH6RoadScanner-$Version-win-x64-framework-dependent"

if (-not $artifacts.StartsWith($root, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Artifacts path escaped the repository root."
}

if (Test-Path -LiteralPath $artifacts) {
    Remove-Item -LiteralPath $artifacts -Recurse -Force
}

New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null

Write-Host "Running Python tests..."
& python -m unittest discover -s (Join-Path $root "tests") -v
if ($LASTEXITCODE -ne 0) { throw "Python tests failed." }

Write-Host "Building Python backend..."
$backendDist = Join-Path $artifacts "backend-dist"
$backendWork = Join-Path $artifacts "pyinstaller-build"
$specPath = Join-Path $artifacts "spec"
& pyinstaller `
    --noconfirm `
    --clean `
    --onefile `
    --name FH6ScannerBackend `
    --distpath $backendDist `
    --workpath $backendWork `
    --specpath $specPath `
    (Join-Path $root "main.py")
if ($LASTEXITCODE -ne 0) { throw "PyInstaller build failed." }

Write-Host "Publishing WPF frontend..."
$project = Join-Path $root "src\FH6RoadScanner\FH6RoadScanner.vbproj"
$selfContainedPublish = Join-Path $artifacts "wpf-self-contained"
$frameworkDependentPublish = Join-Path $artifacts "wpf-framework-dependent"

function Publish-Frontend {
    param(
        [string]$OutputPath,
        [bool]$SelfContained
    )

    $selfContainedValue = $SelfContained.ToString().ToLowerInvariant()
    $compressionValue = $selfContainedValue
    & dotnet publish `
        $project `
        -c Release `
        -r win-x64 `
        --self-contained $selfContainedValue `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=$compressionValue `
        -p:PublishReadyToRun=false `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        -o $OutputPath
    if ($LASTEXITCODE -ne 0) { throw "WPF publish failed: $OutputPath" }
}

Publish-Frontend -OutputPath $selfContainedPublish -SelfContained $true
Publish-Frontend -OutputPath $frameworkDependentPublish -SelfContained $false

$backendExe = Join-Path $backendDist "FH6ScannerBackend.exe"
if (-not (Test-Path -LiteralPath $backendExe)) {
    throw "Packaged backend executable was not created."
}

function New-ReleasePackage {
    param(
        [string]$ReleaseName,
        [string]$PublishPath
    )

    $releaseRoot = Join-Path $packageRoot $ReleaseName
    $zipPath = Join-Path $artifacts "$ReleaseName.zip"
    New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null

    Copy-Item -Path (Join-Path $PublishPath "*") -Destination $releaseRoot -Recurse -Force
    Copy-Item -LiteralPath $backendExe -Destination (Join-Path $releaseRoot "FH6ScannerBackend.exe")

    $licensesTarget = Join-Path $releaseRoot "LICENSES"
    New-Item -ItemType Directory -Path $licensesTarget -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $root "LICENSE") -Destination (Join-Path $licensesTarget "FH6-Road-Scanner-LICENSE.txt")
    Copy-Item -LiteralPath (Join-Path $root "LICENSE") -Destination (Join-Path $releaseRoot "LICENSE")
    Copy-Item -Path (Join-Path $root "LICENSES\*") -Destination $licensesTarget -Recurse -Force
    Copy-Item -LiteralPath (Join-Path $root "NOTICE.md") -Destination $releaseRoot
    Copy-Item -LiteralPath (Join-Path $root "README.md") -Destination $releaseRoot

    Compress-Archive -LiteralPath $releaseRoot -DestinationPath $zipPath -CompressionLevel Optimal
    Write-Host "Release package created: $zipPath"
}

New-ReleasePackage -ReleaseName $selfContainedName -PublishPath $selfContainedPublish
New-ReleasePackage -ReleaseName $frameworkDependentName -PublishPath $frameworkDependentPublish
