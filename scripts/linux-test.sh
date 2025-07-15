#!/bin/sh

# Exit immediately if a command exits with a non-zero status.
set -e

# --- Configuration ---
REPO_ROOT=$(git rev-parse --show-toplevel)

# --- ANSI Color Codes ---
GREEN='\033[0;32m'
RED='\033[0;31m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# --- Functions ---
write_header() {
    echo "${GREEN}----------------------------------------${NC}"
    echo "${GREEN}[Manila Git Hook] $1${NC}"
    echo "${GREEN}----------------------------------------${NC}"
}

# --- Main Script ---
cd "$REPO_ROOT"

# 1. Format Check
write_header "Checking code format..."
dotnet format --verify-no-changes --verbosity minimal

# 2. Build all discovered projects
write_header "Building all discovered projects..."
# Use 'find' to locate all .csproj files and 'xargs' to build them.
find . -name "*.csproj" -print0 | xargs -0 -n1 dotnet build --configuration Release --verbosity minimal /p:TreatWarningsAsErrors=true

# 3. Run tests
write_header "Running tests..."
dotnet test "$REPO_ROOT/Manila.slnx" --configuration Release --verbosity minimal --no-build

# 4. Success
echo "${CYAN}========================================${NC}"
echo "${CYAN}✅ All checks passed successfully!${NC}"
echo "${CYAN}========================================${NC}"

exit 0
