# Selena Build Script
param(
    [string]$Configuration = "Release",
    [string]$Version = "1.0.0",
    [switch]$Test,
    [switch]$Pack,
    [switch]$Benchmark,
    [switch]$Clean
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

function Write-Header {
    param([string]$Text)
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host " $Text" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
}

# Clean
if ($Clean) {
    Write-Header "Cleaning Solution"
    dotnet clean --configuration $Configuration
    if (Test-Path "artifacts") {
        Remove-Item -Recurse -Force "artifacts"
    }
    Write-Host "Clean completed!" -ForegroundColor Green
}

# Build
Write-Header "Building Selena"
Write-Host "Configuration: $Configuration" -ForegroundColor Yellow
Write-Host "Version: $Version" -ForegroundColor Yellow

dotnet build --configuration $Configuration -p:Version=$Version
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed!"
    exit 1
}
Write-Host "Build completed successfully!" -ForegroundColor Green

# Test
if ($Test) {
    Write-Header "Running Tests"
    dotnet test --configuration $Configuration --no-build `
        --logger "console;verbosity=normal" `
        --collect:"XPlat Code Coverage" `
        --results-directory "./artifacts/TestResults"
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Tests failed!"
        exit 1
    }
    Write-Host "All tests passed!" -ForegroundColor Green
}

# Benchmark
if ($Benchmark) {
    Write-Header "Running Benchmarks"
    Push-Location "tests/Selena.Benchmarks"
    try {
        dotnet run --configuration Release
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Benchmarks failed!"
            exit 1
        }
        Write-Host "Benchmarks completed!" -ForegroundColor Green
    }
    finally {
        Pop-Location
    }
}

# Pack
if ($Pack) {
    Write-Header "Creating NuGet Package"
    dotnet pack src/Selena/Selena.csproj `
        --configuration $Configuration `
        --no-build `
        -p:Version=$Version `
        --output "./artifacts/packages"
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Pack failed!"
        exit 1
    }
    
    $packagePath = Get-ChildItem "./artifacts/packages/*.nupkg" | Select-Object -First 1
    Write-Host "Package created: $($packagePath.Name)" -ForegroundColor Green
    Write-Host "Package size: $([math]::Round($packagePath.Length / 1MB, 2)) MB" -ForegroundColor Yellow
}

Write-Header "Build Complete!"
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  - Run examples: dotnet run --project examples/Selena.Examples" -ForegroundColor White
Write-Host "  - Run tests: .\build.ps1 -Test" -ForegroundColor White
Write-Host "  - Create package: .\build.ps1 -Pack" -ForegroundColor White
Write-Host "  - Run benchmarks: .\build.ps1 -Benchmark" -ForegroundColor White
