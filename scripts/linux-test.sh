#!/bin/sh

# Exit immediately if a command exits with a non-zero status.
set -e

# Build the solution, treating warnings as errors
echo "Building solution..."
dotnet build E:/dev/Manila/manila/Manila.slnx --no-restore --configuration Release --verbosity minimal /p:TreatWarningsAsErrors=true

# Run the tests
echo "Build successful, running tests..."
dotnet test E:/dev/Manila/manila/Manila.slnx

echo "Build and tests completed successfully!"
exit 0
