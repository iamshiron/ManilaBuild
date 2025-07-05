using System.Reflection;
using Shiron.Manila.Profiling;

namespace Shiron.Manila;

/// <summary>
/// A simple service to demonstrate the usage of the Profiler and ProfileScope.
/// </summary>
public class TestService {
    /// <summary>
    /// Initializes the TestService and enables the profiler.
    /// </summary>
    public TestService() {
        // Enable the profiler when the service is instantiated
        Console.WriteLine("Profiler enabled for TestService.");
    }

    /// <summary>
    /// Demonstrates profiling a method where the name is automatically derived.
    /// </summary>
    /// <param name="dataSize">A parameter to simulate work based on size.</param>
    public void ProcessDataAutomatically(int dataSize) {
        // Using ProfileScope without a name argument will automatically capture
        // the method name "ProcessDataAutomatically".
        using (new ProfileScope(MethodBase.GetCurrentMethod(), new Dictionary<string, object> { { "size", dataSize } })) {
            Console.WriteLine($"Processing data of size: {dataSize}");
            // Simulate some CPU-bound work
            for (int i = 0; i < dataSize * 1000; i++) {
                Math.Sqrt(i);
            }
        }
    }

    /// <summary>
    /// Demonstrates profiling a method with custom-named internal scopes.
    /// </summary>
    /// <param name="itemId">The ID of the item to process.</param>
    public void HandleItemWithCustomScopes(string itemId) {
        // Profile the entire method, name automatically derived
        using (new ProfileScope(MethodBase.GetCurrentMethod())) {
            Console.WriteLine($"Handling item: {itemId}");

            // Custom scope for a specific part of the logic
            using (new ProfileScope("Validation")) {
                Console.WriteLine("  Validating item...");
                System.Threading.Thread.Sleep(50); // Simulate validation work
            }

            // Another custom scope for a different part
            using (new ProfileScope("Database")) {
                Console.WriteLine("  Persisting item to database...");
                System.Threading.Thread.Sleep(100); // Simulate DB write
            }

            Console.WriteLine($"Finished handling item: {itemId}");
        }
    }

    /// <summary>
    /// Demonstrates profiling an asynchronous method. The duration will include await times.
    /// </summary>
    /// <param name="delayMs">The delay in milliseconds to simulate async work.</param>
    public async Task PerformAsyncOperation(int delayMs) {
        // Automatic naming for the async method
        using (new ProfileScope("NetworkOperations")) {
            Console.WriteLine($"Starting async operation with {delayMs}ms delay...");
            await Task.Delay(delayMs); // This delay is included in the profile scope duration
            Console.WriteLine("Async operation completed.");
        }
    }

    /// <summary>
    /// Demonstrates combining automatic and custom scopes in an async method.
    /// </summary>
    /// <param name="resourceId">The ID of the resource to fetch.</param>
    public async Task FetchAndProcessResource(string resourceId) {
        // Automatic naming for the async method
        using (new ProfileScope("ResourceWorkflow")) {
            Console.WriteLine($"Fetching and processing resource: {resourceId}");

            // Custom scope for fetching
            using (new ProfileScope("Network")) {
                Console.WriteLine("  Fetching resource...");
                await Task.Delay(75); // Simulate network fetch
            }

            // Custom scope for processing
            using (new ProfileScope("CPU")) {
                Console.WriteLine("  Processing resource...");
                // Simulate CPU-bound processing
                for (int i = 0; i < 50000; i++) {
                    Math.Sin(i);
                }
            }

            Console.WriteLine($"Finished resource workflow for: {resourceId}");
        }
    }
}
