using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Journeys.Core.Services.Ingest;

public class DynamicServiceInvoker
{
    // Static caches for performance optimization - shared across all instances
    private static readonly ConcurrentDictionary<string, Type> _typeCache = new();
    private static readonly ConcurrentDictionary<string, MethodInfo> _methodCache = new();
    private static readonly ConcurrentDictionary<Type, PropertyInfo> _taskResultPropertyCache = new();

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DynamicServiceInvoker> _logger;

    public DynamicServiceInvoker(IServiceProvider serviceProvider, ILogger<DynamicServiceInvoker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<object?> InvokeServiceAsync(string serviceTypeName, string methodName, params object?[] parameters)
    {
        try
        {
            _logger.LogDebug("Invoking service method {ServiceType}.{MethodName} with {ParameterCount} parameters", 
                serviceTypeName, methodName, parameters.Length);

            // Get the service type by full name (cached)
            var serviceType = _typeCache.GetOrAdd(serviceTypeName, typeName =>
            {
                var type = Type.GetType(typeName);
                if (type == null)
                {
                    throw new ArgumentException($"Service type '{typeName}' not found");
                }
                return type;
            });

            // Get the service instance from DI
            var service = _serviceProvider.GetRequiredService(serviceType);

            // Find the method by name and parameter count (cached)
            var method = FindMethod(serviceType, methodName, parameters);

            if (method == null)
            {
                throw new ArgumentException($"Method '{methodName}' not found on type '{serviceTypeName}' with the provided parameters");
            }

            // Convert parameters to match method signature
            var convertedParameters = ConvertParameters(method.GetParameters(), parameters);

            // Invoke the method
            var result = method.Invoke(service, convertedParameters);

            // Handle async methods
            if (result is Task task)
            {
                await task.ConfigureAwait(false);
                
                // Get the result if it's a Task<T> (cached PropertyInfo)
                if (task.GetType().IsGenericType)
                {
                    var taskType = task.GetType();
                    var resultProperty = _taskResultPropertyCache.GetOrAdd(taskType, t => t.GetProperty("Result"));
                    return resultProperty?.GetValue(task);
                }
                return null;
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to invoke service method {ServiceType}.{MethodName}", serviceTypeName, methodName);
            throw;
        }
    }

    private MethodInfo? FindMethod(Type serviceType, string methodName, object?[] parameters)
    {
        // Generate cache key based on service type, method name, and parameter types
        var cacheKey = GetMethodCacheKey(serviceType, methodName, parameters);
        
        // Check cache first
        if (_methodCache.TryGetValue(cacheKey, out var cachedMethod))
        {
            return cachedMethod;
        }

        // First try to find exact match by parameter count and types
        var methods = serviceType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == methodName)
            .ToList();

        if (!methods.Any())
        {
            return null;
        }

        // Try to find exact parameter match
        var exactMatch = methods.FirstOrDefault(m => 
            ParametersMatch(m.GetParameters(), parameters));

        if (exactMatch != null)
        {
            // Cache the result
            _methodCache.TryAdd(cacheKey, exactMatch);
            return exactMatch;
        }

        // If no exact match, try to find by parameter count only
        var parameterCount = parameters.Length;
        var candidates = methods.Where(m => m.GetParameters().Length == parameterCount).ToList();

        if (candidates.Count == 1)
        {
            var method = candidates.First();
            // Cache the result
            _methodCache.TryAdd(cacheKey, method);
            return method;
        }

        // If multiple candidates, try to find one that can accept the parameters
        foreach (var candidate in candidates)
        {
            try
            {
                var convertedParams = ConvertParameters(candidate.GetParameters(), parameters);
                if (convertedParams != null)
                {
                    // Cache the result
                    _methodCache.TryAdd(cacheKey, candidate);
                    return candidate;
                }
            }
            catch
            {
                // Continue to next candidate
            }
        }

        return null;
    }

    private string GetMethodCacheKey(Type serviceType, string methodName, object?[] parameters)
    {
        // Create a cache key that includes service type, method name, and parameter types
        // This ensures we handle method overloads correctly
        var paramTypes = parameters.Select(p => p?.GetType().FullName ?? "null").ToArray();
        return $"{serviceType.FullName}|{methodName}|{string.Join(",", paramTypes)}";
    }

    private bool ParametersMatch(ParameterInfo[] methodParameters, object?[] providedParameters)
    {
        if (methodParameters.Length != providedParameters.Length)
        {
            return false;
        }

        for (int i = 0; i < methodParameters.Length; i++)
        {
            var methodParam = methodParameters[i];
            var providedParam = providedParameters[i];

            if (providedParam == null)
            {
                // Null is acceptable for nullable types
                if (!IsNullableType(methodParam.ParameterType))
                {
                    return false;
                }
            }
            else
            {
                // Check if types are compatible
                if (!methodParam.ParameterType.IsAssignableFrom(providedParam.GetType()))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private object?[]? ConvertParameters(ParameterInfo[] methodParameters, object?[] providedParameters)
    {
        if (methodParameters.Length != providedParameters.Length)
        {
            return null;
        }

        var converted = new object?[methodParameters.Length];

        for (int i = 0; i < methodParameters.Length; i++)
        {
            var methodParam = methodParameters[i];
            var providedParam = providedParameters[i];

            if (providedParam == null)
            {
                converted[i] = null;
            }
            else
            {
                try
                {
                    // Try direct assignment first
                    if (methodParam.ParameterType.IsAssignableFrom(providedParam.GetType()))
                    {
                        converted[i] = providedParam;
                    }
                    else
                    {
                        // Try conversion
                        converted[i] = Convert.ChangeType(providedParam, methodParam.ParameterType);
                    }
                }
                catch
                {
                    // Conversion failed
                    return null;
                }
            }
        }

        return converted;
    }

    private bool IsNullableType(Type type)
    {
        return !type.IsValueType || Nullable.GetUnderlyingType(type) != null;
    }
} 