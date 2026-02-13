using System.Diagnostics;

namespace TaskVsValueTaskDemo
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== Task vs ValueTask Demonstration ===\n");

            // 1. Basic Usage Demo
            await DemonstrateBasicUsage();

            Console.WriteLine("\n" + new string('-', 60) + "\n");

            // 2. Performance Comparison
            await PerformanceComparison();

            Console.WriteLine("\n" + new string('-', 60) + "\n");

            // 3. Demonstrate Caching Scenario
            await DemonstrateCachingScenario();

            Console.WriteLine("\nDemo completed!");
        }

        #region Basic Usage Demo

        static async Task DemonstrateBasicUsage()
        {
            Console.WriteLine("1. BASIC USAGE DEMONSTRATION");
            Console.WriteLine("============================\n");

            // Using Task
            Console.WriteLine("Calling method that returns Task<int>...");
            int taskResult = await GetValueUsingTask(42);
            Console.WriteLine($"Result from Task: {taskResult}\n");

            // Using ValueTask
            Console.WriteLine("Calling method that returns ValueTask<int>...");
            int valueTaskResult = await GetValueUsingValueTask(42);
            Console.WriteLine($"Result from ValueTask: {valueTaskResult}\n");
        }

        static async Task<int> GetValueUsingTask(int input)
        {
            // Simulating async operation
            await Task.Delay(100);
            return input * 2;
        }

        static async ValueTask<int> GetValueUsingValueTask(int input)
        {
            // Simulating async operation
            await Task.Delay(100);
            return input * 2;
        }

        #endregion

        #region Performance Comparison

        static async Task PerformanceComparison()
        {
            Console.WriteLine("2. PERFORMANCE COMPARISON");
            Console.WriteLine("=========================\n");

            const int iterations = 1_000_000;

            // Test 1: Always Asynchronous (Both should perform similarly)
            Console.WriteLine($"Test 1: Always Asynchronous Operations ({iterations:N0} iterations)");
            Console.WriteLine("---------------------------------------------------------------");

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                await AlwaysAsyncTask();
            }
            sw.Stop();
            Console.WriteLine($"Task:      {sw.ElapsedMilliseconds:N0} ms");

            sw.Restart();
            for (int i = 0; i < iterations; i++)
            {
                await AlwaysAsyncValueTask();
            }
            sw.Stop();
            Console.WriteLine($"ValueTask: {sw.ElapsedMilliseconds:N0} ms\n");

            // Test 2: Often Synchronous (ValueTask should perform better)
            Console.WriteLine($"Test 2: Often Synchronous Operations ({iterations:N0} iterations)");
            Console.WriteLine("---------------------------------------------------------------");

            sw.Restart();
            for (int i = 0; i < iterations; i++)
            {
                await SometimesSyncTask(i);
            }
            sw.Stop();
            var taskTime = sw.ElapsedMilliseconds;
            Console.WriteLine($"Task:      {taskTime:N0} ms");

            sw.Restart();
            for (int i = 0; i < iterations; i++)
            {
                await SometimesSyncValueTask(i);
            }
            sw.Stop();
            var valueTaskTime = sw.ElapsedMilliseconds;
            Console.WriteLine($"ValueTask: {valueTaskTime:N0} ms");

            if (valueTaskTime < taskTime)
            {
                var improvement = ((taskTime - valueTaskTime) / (double)taskTime) * 100;
                Console.WriteLine($"\n✓ ValueTask is ~{improvement:F1}% faster in synchronous scenarios!");
            }
        }

        static async Task<int> AlwaysAsyncTask()
        {
            await Task.Yield(); // Force async
            return 42;
        }

        static async ValueTask<int> AlwaysAsyncValueTask()
        {
            await Task.Yield(); // Force async
            return 42;
        }

        static Task<int> SometimesSyncTask(int value)
        {
            // 90% of the time, return synchronously
            if (value % 10 != 0)
            {
                return Task.FromResult(value * 2);
            }
            return Task.Delay(1).ContinueWith(_ => value * 2);
        }

        static ValueTask<int> SometimesSyncValueTask(int value)
        {
            // 90% of the time, return synchronously (no allocation!)
            if (value % 10 != 0)
            {
                return new ValueTask<int>(value * 2);
            }
            return new ValueTask<int>(Task.Delay(1).ContinueWith(_ => value * 2));
        }

        #endregion

        #region Caching Scenario Demo

        static async Task DemonstrateCachingScenario()
        {
            Console.WriteLine("3. CACHING SCENARIO (Real-World Example)");
            Console.WriteLine("==========================================\n");

            var cacheWithTask = new CacheWithTask();
            var cacheWithValueTask = new CacheWithValueTask();

            const int requests = 100_000;

            Console.WriteLine($"Simulating {requests:N0} cache requests (90% hit rate)...\n");

            // Warm up
            await cacheWithTask.GetDataAsync("key1");
            await cacheWithValueTask.GetDataAsync("key1");

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < requests; i++)
            {
                // 90% cache hits
                string key = i % 10 == 0 ? $"key{i}" : "key1";
                await cacheWithTask.GetDataAsync(key);
            }
            sw.Stop();
            var taskCacheTime = sw.ElapsedMilliseconds;
            Console.WriteLine($"Cache with Task:      {taskCacheTime:N0} ms");

            sw.Restart();
            for (int i = 0; i < requests; i++)
            {
                string key = i % 10 == 0 ? $"key{i}" : "key1";
                await cacheWithValueTask.GetDataAsync(key);
            }
            sw.Stop();
            var valueTaskCacheTime = sw.ElapsedMilliseconds;
            Console.WriteLine($"Cache with ValueTask: {valueTaskCacheTime:N0} ms");

            if (valueTaskCacheTime < taskCacheTime)
            {
                var improvement = ((taskCacheTime - valueTaskCacheTime) / (double)taskCacheTime) * 100;
                Console.WriteLine($"\n✓ ValueTask reduced cache lookup time by ~{improvement:F1}%!");
                Console.WriteLine("  Reason: Avoided heap allocations on cache hits");
            }
        }

        class CacheWithTask
        {
            private readonly Dictionary<string, string> _cache = new();

            public async Task<string> GetDataAsync(string key)
            {
                if (_cache.TryGetValue(key, out var cachedValue))
                {
                    // Cache hit - but still allocates Task object
                    return await Task.FromResult(cachedValue);
                }

                // Cache miss - simulate database call
                await Task.Delay(1);
                var value = $"Data for {key}";
                _cache[key] = value;
                return value;
            }
        }

        class CacheWithValueTask
        {
            private readonly Dictionary<string, string> _cache = new();

            public ValueTask<string> GetDataAsync(string key)
            {
                if (_cache.TryGetValue(key, out var cachedValue))
                {
                    // Cache hit - NO heap allocation!
                    return new ValueTask<string>(cachedValue);
                }

                // Cache miss - simulate database call
                return new ValueTask<string>(LoadDataAsync(key));
            }

            private async Task<string> LoadDataAsync(string key)
            {
                await Task.Delay(1);
                var value = $"Data for {key}";
                _cache[key] = value;
                return value;
            }
        }

        #endregion
    }
}
