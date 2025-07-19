# 1. CONFIGURE YOUR TEST
# ---
# Path to the application you want to test.
$appPath = "..\Manila.CLI\bin\Release\net9.0\Manila.CLI.exe"
# Arguments to pass to the application (e.g., "-n 1 localhost").
$appArgs = "run zip/main:build"
# Number of times to run the application.
$runs = 10

# 2. RUN THE TEST
# ---
# Create a list to store the time of each run in milliseconds.
$times = [System.Collections.Generic.List[double]]::new()

Write-Host "🚀 Starting test..."
Write-Host "   Application: $appPath"
Write-Host "   Arguments: $appArgs"
Write-Host "   Runs: $runs"
Write-Host "---"

foreach ($i in 1..$runs) {
    # 'Measure-Command' times the script block.
    # '&' is the call operator, used to execute the command.
    $elapsed = Measure-Command {
        & $appPath $appArgs | Out-Null
    }

    # Add the total milliseconds of the run to our list.
    $times.Add($elapsed.TotalMilliseconds)

    # Display the result of the current run.
    $ms = $elapsed.TotalMilliseconds.ToString("F2")
    Write-Host "   Run $i`: $ms ms"
}


# 3. CALCULATE AND DISPLAY THE AVERAGE
# ---
# 'Measure-Object' can calculate the average from a collection of numbers.
$average = ($times | Measure-Object -Average).Average

Write-Host "---"
Write-Host "✅ Test complete."
Write-Host "Average execution time: $($average.ToString("F2")) ms"
