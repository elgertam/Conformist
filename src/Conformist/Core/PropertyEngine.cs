using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Conformist.HttpRfc.Core;

public class PropertyEngine
{
    private readonly ILogger<PropertyEngine> _logger;

    public PropertyEngine(ILogger<PropertyEngine> logger)
    {
        _logger = logger;
    }

    public async Task<PropertyTestResult> ExecutePropertiesAsync<TContext>(
        IEnumerable<IHttpProperty<TContext>> properties,
        HttpRequestMessage request,
        HttpResponseMessage response,
        TContext dbContext,
        CancellationToken cancellationToken = default) where TContext : DbContext
    {
        var results = new List<IndividualPropertyResult>();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        foreach (var property in properties)
        {
            _logger.LogDebug("Executing property: {PropertyName}", property.Name);
            
            var propertyStopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                var result = await property.CheckAsync(request, response, dbContext, cancellationToken);
                propertyStopwatch.Stop();

                results.Add(new IndividualPropertyResult
                {
                    PropertyName = property.Name,
                    PropertyDescription = property.Description,
                    RfcReference = property.RfcReference,
                    Passed = result.Passed,
                    FailureReason = result.FailureReason,
                    Details = result.Details,
                    ExecutionTime = propertyStopwatch.Elapsed,
                    Metrics = result.Metrics ?? new Dictionary<string, object>()
                });

                if (!result.Passed)
                {
                    _logger.LogWarning("Property {PropertyName} failed: {FailureReason}", 
                        property.Name, result.FailureReason);
                }
            }
            catch (Exception ex)
            {
                propertyStopwatch.Stop();
                _logger.LogError(ex, "Property {PropertyName} threw exception", property.Name);
                
                results.Add(new IndividualPropertyResult
                {
                    PropertyName = property.Name,
                    PropertyDescription = property.Description,
                    RfcReference = property.RfcReference,
                    Passed = false,
                    FailureReason = $"Exception: {ex.Message}",
                    Details = ex.ToString(),
                    ExecutionTime = propertyStopwatch.Elapsed,
                    Metrics = new Dictionary<string, object>()
                });
            }
        }

        stopwatch.Stop();

        return new PropertyTestResult
        {
            RequestMethod = request.Method.Method,
            RequestPath = request.RequestUri?.PathAndQuery ?? "",
            ResponseStatusCode = (int)response.StatusCode,
            TotalExecutionTime = stopwatch.Elapsed,
            PropertyResults = results,
            OverallPassed = results.All(r => r.Passed),
            TotalProperties = results.Count,
            PassedProperties = results.Count(r => r.Passed),
            FailedProperties = results.Count(r => !r.Passed)
        };
    }
}

public class PropertyTestResult
{
    public string RequestMethod { get; init; } = "";
    public string RequestPath { get; init; } = "";
    public int ResponseStatusCode { get; init; }
    public TimeSpan TotalExecutionTime { get; init; }
    public List<IndividualPropertyResult> PropertyResults { get; init; } = new();
    public bool OverallPassed { get; init; }
    public int TotalProperties { get; init; }
    public int PassedProperties { get; init; }
    public int FailedProperties { get; init; }
}

public class IndividualPropertyResult
{
    public string PropertyName { get; init; } = "";
    public string PropertyDescription { get; init; } = "";
    public string RfcReference { get; init; } = "";
    public bool Passed { get; init; }
    public string? FailureReason { get; init; }
    public string? Details { get; init; }
    public TimeSpan ExecutionTime { get; init; }
    public Dictionary<string, object> Metrics { get; init; } = new();
}