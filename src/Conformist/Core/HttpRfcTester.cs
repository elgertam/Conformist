using FsCheck;
using Conformist.HttpRfc.Discovery;
using Conformist.HttpRfc.Generators;
using Conformist.HttpRfc.StateTracking;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Conformist.HttpRfc.Core;

public class HttpRfcTester<TContext, TProgram> where TContext : DbContext where TProgram : class
{
    private readonly WebApplicationFactory<TProgram> _factory;
    private readonly List<EndpointInfo> _endpoints;
    private readonly List<IHttpProperty<TContext>> _properties;
    private readonly PropertyEngine _propertyEngine;
    private readonly TestDataGenerator _testDataGenerator;
    private readonly StateTrackingOptions _stateTrackingOptions;
    private readonly ILogger<HttpRfcTester<TContext, TProgram>> _logger;

    internal HttpRfcTester(
        WebApplicationFactory<TProgram> factory,
        List<EndpointInfo> endpoints,
        List<IHttpProperty<TContext>> properties,
        PropertyEngine propertyEngine,
        TestDataGenerator testDataGenerator,
        StateTrackingOptions stateTrackingOptions,
        ILogger<HttpRfcTester<TContext, TProgram>> logger)
    {
        _factory = factory;
        _endpoints = endpoints;
        _properties = properties;
        _propertyEngine = propertyEngine;
        _testDataGenerator = testDataGenerator;
        _stateTrackingOptions = stateTrackingOptions;
        _logger = logger;
    }

    public static HttpRfcTestBuilder<TContext, TStartup> ForApi<TStartup>(WebApplicationFactory<TStartup> factory) 
        where TStartup : class
    {
        return new HttpRfcTestBuilder<TContext, TStartup>(factory);
    }

    public Property CheckAllProperties()
    {
        return Prop.ForAll(GenerateEndpointRequest(), request =>
        {
            var result = CheckRequestAsync(request).GetAwaiter().GetResult();
            return result.OverallPassed;
        });
    }

    public Property CheckProperty<TProperty>() where TProperty : IHttpProperty<TContext>
    {
        var targetProperty = _properties.OfType<TProperty>().FirstOrDefault();
        if (targetProperty == null)
        {
            throw new InvalidOperationException($"Property {typeof(TProperty).Name} is not configured for this tester");
        }

        return Prop.ForAll(GenerateEndpointRequest(), request =>
        {
            var result = CheckRequestWithSinglePropertyAsync(request, targetProperty).GetAwaiter().GetResult();
            return result.OverallPassed;
        });
    }

    public async Task<PropertyTestResult> CheckRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
        
        try
        {
            var client = _factory.CreateClient();
            var response = await client.SendAsync(request, cancellationToken);

            var result = await _propertyEngine.ExecutePropertiesAsync(
                _properties, request, response, dbContext, cancellationToken);

            _logger.LogDebug("Checked {PropertyCount} properties for {Method} {Path}. Passed: {PassedCount}, Failed: {FailedCount}",
                result.TotalProperties, result.RequestMethod, result.RequestPath, 
                result.PassedProperties, result.FailedProperties);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during property check for {Method} {Path}", 
                request.Method, request.RequestUri?.PathAndQuery);

            return new PropertyTestResult
            {
                RequestMethod = request.Method.Method,
                RequestPath = request.RequestUri?.PathAndQuery ?? "",
                ResponseStatusCode = 500,
                TotalExecutionTime = TimeSpan.Zero,
                PropertyResults = new List<IndividualPropertyResult>
                {
                    new()
                    {
                        PropertyName = "Request Execution",
                        PropertyDescription = "Basic request execution",
                        RfcReference = "N/A",
                        Passed = false,
                        FailureReason = $"Request failed with exception: {ex.Message}",
                        Details = ex.ToString(),
                        ExecutionTime = TimeSpan.Zero,
                        Metrics = new Dictionary<string, object>()
                    }
                },
                OverallPassed = false,
                TotalProperties = 1,
                PassedProperties = 0,
                FailedProperties = 1
            };
        }
    }

    private async Task<PropertyTestResult> CheckRequestWithSinglePropertyAsync(
        HttpRequestMessage request, 
        IHttpProperty<TContext> property,
        CancellationToken cancellationToken = default)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();

        try
        {
            var client = _factory.CreateClient();
            var response = await client.SendAsync(request, cancellationToken);

            var result = await _propertyEngine.ExecutePropertiesAsync(
                new[] { property }, request, response, dbContext, cancellationToken);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during single property check for {Method} {Path}", 
                request.Method, request.RequestUri?.PathAndQuery);

            return new PropertyTestResult
            {
                RequestMethod = request.Method.Method,
                RequestPath = request.RequestUri?.PathAndQuery ?? "",
                ResponseStatusCode = 500,
                TotalExecutionTime = TimeSpan.Zero,
                PropertyResults = new List<IndividualPropertyResult>
                {
                    new()
                    {
                        PropertyName = property.Name,
                        PropertyDescription = property.Description,
                        RfcReference = property.RfcReference,
                        Passed = false,
                        FailureReason = $"Property check failed with exception: {ex.Message}",
                        Details = ex.ToString(),
                        ExecutionTime = TimeSpan.Zero,
                        Metrics = new Dictionary<string, object>()
                    }
                },
                OverallPassed = false,
                TotalProperties = 1,
                PassedProperties = 0,
                FailedProperties = 1
            };
        }
    }

    private Arbitrary<HttpRequestMessage> GenerateEndpointRequest()
    {
        return Gen.Elements(_endpoints.ToArray())
            .SelectMany(endpoint => GenerateRequestsForEndpoint(endpoint))
            .ToArbitrary();
    }

    private Gen<HttpRequestMessage> GenerateRequestsForEndpoint(EndpointInfo endpoint)
    {
        return Gen.Fresh(() =>
        {
            using var scope = _factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
            
            var requests = _testDataGenerator.GenerateRequestsForEndpoint(endpoint, dbContext, 1).GetAwaiter().GetResult();
            return requests.FirstOrDefault() ?? new HttpRequestMessage(HttpMethod.Get, "/");
        });
    }

    public async Task<List<PropertyTestResult>> ExecuteAllEndpointTestsAsync(
        int maxRequestsPerEndpoint = 5,
        CancellationToken cancellationToken = default)
    {
        var results = new List<PropertyTestResult>();

        foreach (var endpoint in _endpoints)
        {
            try
            {
                using var scope = _factory.Services.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();

                var requests = await _testDataGenerator.GenerateRequestsForEndpoint(
                    endpoint, dbContext, maxRequestsPerEndpoint, cancellationToken);

                foreach (var request in requests)
                {
                    var result = await CheckRequestAsync(request, cancellationToken);
                    results.Add(result);

                    if (!result.OverallPassed)
                    {
                        _logger.LogWarning("Properties failed for {Method} {Path}: {FailedCount}/{TotalCount}",
                            result.RequestMethod, result.RequestPath, 
                            result.FailedProperties, result.TotalProperties);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to test endpoint {Method} {Path}", endpoint.Method, endpoint.Path);
            }
        }

        var summary = GenerateTestSummary(results);
        _logger.LogInformation("Test execution complete. {Summary}", summary);

        return results;
    }

    private string GenerateTestSummary(List<PropertyTestResult> results)
    {
        var totalTests = results.Count;
        var passedTests = results.Count(r => r.OverallPassed);
        var failedTests = totalTests - passedTests;
        var totalProperties = results.Sum(r => r.TotalProperties);
        var totalPassedProperties = results.Sum(r => r.PassedProperties);
        var totalFailedProperties = results.Sum(r => r.FailedProperties);

        return $"Tests: {passedTests}/{totalTests} passed. " +
               $"Properties: {totalPassedProperties}/{totalProperties} passed. " +
               $"Endpoints: {results.Select(r => $"{r.RequestMethod} {r.RequestPath}").Distinct().Count()}";
    }

    public List<EndpointInfo> GetDiscoveredEndpoints() => _endpoints.ToList();
    
    public List<IHttpProperty<TContext>> GetConfiguredProperties() => _properties.ToList();

    public StateTrackingOptions GetStateTrackingConfiguration() => _stateTrackingOptions;
}