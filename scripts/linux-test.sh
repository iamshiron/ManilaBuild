#!/bin/sh

# Exit immediately if a command exits with a non-zero status.
set -e

# --- Configuration ---
# Get the root directory of the Git repository.
REPO_ROOT=$(git rev-parse --show-toplevel)
SOLUTION_FILE="$REPO_ROOT/Manila.slnx"

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

write_failure() {
    echo "${RED}[Manila Git Hook] $1${NC}"
}

# --- Main Script ---
# Navigate to the repository root to ensure commands run in the correct context
cd "$REPO_ROOT"

# 1. Format Check
write_header "Checking code format..."
dotnet format --verify-no-changes --verbosity minimal
# The 'set -e' command at the top will automatically cause the script to
# exit if the format check fails (returns a non-zero exit code).

# 2. Build & Test
write_header "Building and running tests..."
dotnet test "$SOLUTION_FILE" --configuration Release --verbosity minimal /p:TreatWarningsAsErrors=true
# 'set -e' also handles this. If tests fail, the script will stop here.

# 3. Success (only reached if all previous steps passed)
echo "${CYAN}========================================${NC}"
echo "${CYAN}✅ All checks passed successfully!${NC}"
echo "${CYAN}========================================${NC}"

exit 0
