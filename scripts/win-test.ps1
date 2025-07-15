# Stop script execution on the first error.
$ErrorActionPreference = "Stop"

# --- Configuration ---
# Get the root directory of the Git repository.
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
    # 1. Format Check
    Write-Header "Checking code format..."
    dotnet format --verify-no-changes --verbosity minimal
    # Manually check the exit code and throw an error to trigger the catch block.
    if ($LASTEXITCODE -ne 0) {
        throw "Format check failed. Run 'dotnet format' to fix."
    }

    # 2. Build & Test
    Write-Header "Building and running tests..."
    dotnet test $SolutionFile --configuration Release --verbosity minimal /p:TreatWarningsAsErrors=true
    # Manually check the exit code here as well.
    if ($LASTEXITCODE -ne 0) {
        throw "Build or tests failed."
    }

    # 3. Success (only reached if all previous steps passed)
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "✅ All checks passed successfully!" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    exit 0

} catch {
    # This block will now reliably catch any failure.
    Write-Failure "A step failed. See output above for details."
    exit 1
}
