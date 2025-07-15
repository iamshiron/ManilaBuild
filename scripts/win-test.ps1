# Build the solution, treating warnings as errors
dotnet build E:/dev/Manila/manila/Manila.slnx --no-restore --configuration Release --verbosity minimal /p:TreatWarningsAsErrors=true
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!"
    exit $LASTEXITCODE
}

# If the build was successful, run the tests
Write-Host "Build successful, running tests..."
dotnet test E:/dev/Manila/manila/Manila.slnx
if ($LASTEXITCODE -ne 0) {
    Write-Host "Tests failed!"
    exit $LASTEXITCODE
}

Write-Host "Build and tests completed successfully!"
exit 0
