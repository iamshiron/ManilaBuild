# Stop script execution on the first error.
$ErrorActionPreference = "Stop"

# --- Configuration ---
$RepoRoot = Get-Location
$SolutionFile = Join-Path $RepoRoot "Manila.slnx"

# --- Functions ---
function Write-Header($Message) {
    Write-Host "----------------------------------------" -ForegroundColor Green
    Write-Host "[Manila Git Hook] $Message" -ForegroundColor Green
    Write-Host "----------------------------------------" -ForegroundColor Green
}

function Write-Failure($Message) {
    Write-Host "[Manila Git Hook] $Message" -ForegroundColor Red
}

# --- Main Script ---
try {
    # 1. Verify solution file exists
    if (-not (Test-Path $SolutionFile)) {
        throw "Solution file not found at: $SolutionFile"
    }

    # 2. Format Check
    Write-Header "Checking code format..."
    dotnet format --verify-no-changes --verbosity minimal
    if ($LASTEXITCODE -ne 0) {
        throw "Format check failed. Run 'dotnet format' to fix."
    }

    # 3. Build the entire solution once
    Write-Header "Building solution..."
    dotnet build $SolutionFile --configuration Release --verbosity minimal /p:TreatWarningsAsErrors=true
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed for solution"
    }

    # 4. Run tests for the main solution
    Write-Header "Running tests..."
    dotnet test $SolutionFile --configuration Release --verbosity minimal --no-build
    if ($LASTEXITCODE -ne 0) {
        throw "Tests failed."
    }

    # 5. Build documentation
    Write-Header "Building documentation..."
    & .\win-build-docs.ps1
    if ($LASTEXITCODE -ne 0) {
        throw "Documentation build failed."
    }

    # 6. Success
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "✅ All checks passed successfully!" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    exit 0

} catch {
    Write-Failure "A step failed. See output above for details."
    exit 1
}
