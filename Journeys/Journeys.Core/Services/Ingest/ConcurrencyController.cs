using Serilog;

namespace Journeys.Core.Services.Ingest;

/// <summary>
/// Controls concurrency for ingest operations using SemaphoreSlim to limit concurrent operations.
/// </summary>
public class ConcurrencyController
{
    private readonly SemaphoreSlim _semaphore;
    private readonly bool _enableParallelProcessing;
    private readonly int _maxConcurrency;
    private readonly ILogger _logger;

    public ConcurrencyController(int maxConcurrency, bool enableParallelProcessing, ILogger logger)
    {
        _maxConcurrency = maxConcurrency;
        _enableParallelProcessing = enableParallelProcessing;
        _logger = logger;
        _semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);

        _logger.Debug("ConcurrencyController initialized: MaxConcurrency={MaxConcurrency}, EnableParallelProcessing={EnableParallelProcessing}",
            maxConcurrency, enableParallelProcessing);
    }

    /// <summary>
    /// Executes a single operation with concurrency control.
    /// </summary>
    /// <typeparam name="T">The return type of the operation</typeparam>
    /// <param name="operation">The operation to execute</param>
    /// <returns>The result of the operation</returns>
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        if (!_enableParallelProcessing)
        {
            _logger.Debug("Executing operation serially (parallel processing disabled)");
            return await operation();
        }

        await _semaphore.WaitAsync();
        try
        {
            _logger.Debug("Executing operation with concurrency control. Available slots: {AvailableSlots}", _semaphore.CurrentCount);
            return await operation();
        }
        finally
        {
            _semaphore.Release();
            _logger.Debug("Released concurrency slot. Available slots: {AvailableSlots}", _semaphore.CurrentCount);
        }
    }

    /// <summary>
    /// Executes multiple operations in parallel with concurrency control.
    /// </summary>
    /// <typeparam name="T">The return type of the operations</typeparam>
    /// <param name="operations">The operations to execute</param>
    /// <returns>List of results from all operations</returns>
    public async Task<List<T>> ExecuteParallelAsync<T>(IEnumerable<Func<Task<T>>> operations)
    {
        var operationsList = operations.ToList();

        if (!_enableParallelProcessing || operationsList.Count == 0)
        {
            _logger.Debug("Executing {OperationCount} operations serially", operationsList.Count);
            var results = new List<T>();
            foreach (var operation in operationsList)
            {
                results.Add(await operation());
            }
            return results;
        }

        _logger.Debug("Executing {OperationCount} operations in parallel with max concurrency {MaxConcurrency}",
            operationsList.Count, _maxConcurrency);

        var tasks = operationsList.Select(async operation =>
        {
            await _semaphore.WaitAsync();
            try
            {
                return await operation();
            }
            finally
            {
                _semaphore.Release();
            }
        });

        var taskResults = await Task.WhenAll(tasks);
        _logger.Debug("Completed {OperationCount} parallel operations", taskResults.Length);

        return taskResults.ToList();
    }

    /// <summary>
    /// Executes multiple operations in parallel with concurrency control, providing progress feedback.
    /// </summary>
    /// <typeparam name="T">The return type of the operations</typeparam>
    /// <param name="operations">The operations to execute</param>
    /// <param name="progressCallback">Optional callback for progress updates</param>
    /// <returns>List of results from all operations</returns>
    public async Task<List<T>> ExecuteParallelAsync<T>(IEnumerable<Func<Task<T>>> operations, Action<int, int>? progressCallback = null)
    {
        var operationsList = operations.ToList();

        if (!_enableParallelProcessing || operationsList.Count == 0)
        {
            _logger.Debug("Executing {OperationCount} operations serially", operationsList.Count);
            var results = new List<T>();
            for (int i = 0; i < operationsList.Count; i++)
            {
                results.Add(await operationsList[i]());
                progressCallback?.Invoke(i + 1, operationsList.Count);
            }
            return results;
        }

        _logger.Debug("Executing {OperationCount} operations in parallel with max concurrency {MaxConcurrency}",
            operationsList.Count, _maxConcurrency);

        var completedCount = 0;
        var tasks = operationsList.Select(async (operation, index) =>
        {
            await _semaphore.WaitAsync();
            try
            {
                var result = await operation();
                Interlocked.Increment(ref completedCount);
                progressCallback?.Invoke(completedCount, operationsList.Count);
                return result;
            }
            finally
            {
                _semaphore.Release();
            }
        });

        var taskResults = await Task.WhenAll(tasks);
        _logger.Debug("Completed {OperationCount} parallel operations", taskResults.Length);

        return taskResults.ToList();
    }

    /// <summary>
    /// Gets the current concurrency status.
    /// </summary>
    /// <returns>Information about current concurrency state</returns>
    public (int AvailableSlots, int MaxConcurrency, bool ParallelProcessingEnabled) GetStatus()
    {
        return (_semaphore.CurrentCount, _maxConcurrency, _enableParallelProcessing);
    }

    /// <summary>
    /// Disposes the semaphore.
    /// </summary>
    public void Dispose()
    {
        _semaphore?.Dispose();
    }
}
