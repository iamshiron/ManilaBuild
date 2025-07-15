# Stop script execution on the first error.
$ErrorActionPreference = "Stop"

# --- Configuration ---
$RepoRoot = Get-Location

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
    # 1. Discover all C# projects
    $AllProjects = Get-ChildItem -Path $RepoRoot -Recurse -Filter *.csproj

    if ($null -eq $AllProjects) {
        throw "No .csproj files found in the repository."
    }

    # 2. Format Check
    Write-Header "Checking code format..."
    dotnet format --verify-no-changes --verbosity minimal
    if ($LASTEXITCODE -ne 0) {
        throw "Format check failed. Run 'dotnet format' to fix."
    }

    # 3. Build all projects individually to ensure plugins are included
    Write-Header "Building all discovered projects..."
    foreach ($ProjectFile in $AllProjects) {
        Write-Host "Building $($ProjectFile.FullName)..."
        dotnet build $ProjectFile.FullName --configuration Release --verbosity minimal /p:TreatWarningsAsErrors=true
        if ($LASTEXITCODE -ne 0) {
            throw "Build failed for project: $($ProjectFile.Name)"
        }
    }

    # 4. Run tests for the main solution
    Write-Header "Running tests..."
    dotnet test (Join-Path $RepoRoot "Manila.slnx") --configuration Release --verbosity minimal --no-build
    if ($LASTEXITCODE -ne 0) {
        throw "Tests failed."
    }

    # 5. Success
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "✅ All checks passed successfully!" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    exit 0

} catch {
    Write-Failure "A step failed. See output above for details."
    exit 1
}
