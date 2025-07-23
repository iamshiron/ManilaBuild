#!/bin/bash

# Stop script execution on the first error.
# If any command fails, the script will exit immediately.
set -e

# --- Configuration ---
# Get the current directory, which is assumed to be the repository root.
REPO_ROOT=$(pwd)
# Define the path to the solution file.
SOLUTION_FILE="${REPO_ROOT}/Manila.slnx"

# Function to print a green header message.
write_header() {
    local GREEN='\033[0;32m'
    local NC='\033[0m' # No Color
    echo -e "${GREEN}----------------------------------------${NC}"
    echo -e "${GREEN}[Manila Git Hook] $1${NC}"
    echo -e "${GREEN}----------------------------------------${NC}"
}

# Function to print a red failure message to standard error.
write_failure() {
    local RED='\033[0;31m'
    local NC='\033[0m' # No Color
    echo -e "${RED}[Manila Git Hook] $1${NC}" >&2
}

main() {
    # 1. Verify solution file exists
    write_header "Verifying solution file..."
    if [ ! -f "$SOLUTION_FILE" ]; then
        write_failure "Solution file not found at: $SOLUTION_FILE"
        exit 1
    fi
    echo "Solution file found: $SOLUTION_FILE"

    # 2. Format Check
    write_header "Checking code format..."
    dotnet format --verify-no-changes --verbosity minimal
    if [ $? -ne 0 ]; then
        write_failure "Format check failed. Run 'dotnet format' to fix."
        exit 1
    fi
    echo "Code format check passed."

    # 3. Build the entire solution once
    write_header "Building solution..."
    dotnet build "$SOLUTION_FILE" --configuration Release --verbosity minimal -p:TreatWarningsAsErrors=true
    if [ $? -ne 0 ]; then
        write_failure "Build failed for solution."
        exit 1
    fi
    echo "Solution built successfully."

    # 4. Run tests for the main solution
    write_header "Running tests..."
    dotnet test "$SOLUTION_FILE" --configuration Release --verbosity minimal --no-build
    if [ $? -ne 0 ]; then
        write_failure "Tests failed."
        exit 1
    fi
    echo "All tests passed."

    # 5. Success
    local CYAN='\033[0;36m'
    local NC='\033[0m'
    echo -e "${CYAN}========================================${NC}"
    echo -e "${CYAN}✅ All checks passed successfully!${NC}"
    echo -e "${CYAN}========================================${NC}"
    exit 0
}

main
