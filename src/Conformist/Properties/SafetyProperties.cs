using Conformist.HttpRfc.Core;
using Conformist.HttpRfc.StateTracking;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Conformist.HttpRfc.Properties;

public class GetMethodSafetyProperty<TContext> : IHttpProperty<TContext> where TContext : DbContext
{
    private readonly EfCoreStateTracker<TContext> _stateTracker;
    private readonly ILogger<GetMethodSafetyProperty<TContext>> _logger;

    public string Name => "GET Method Safety";
    public string Description => "GET requests must not cause observable side effects in the system state";
    public string RfcReference => "RFC 7231 Section 4.2.1";

    public GetMethodSafetyProperty(EfCoreStateTracker<TContext> stateTracker, ILogger<GetMethodSafetyProperty<TContext>> logger)
    {
        _stateTracker = stateTracker;
        _logger = logger;
    }

    public async Task<PropertyResult> CheckAsync(
        HttpRequestMessage request, 
        HttpResponseMessage response, 
        TContext dbContext, 
        CancellationToken cancellationToken = default)
    {
        if (request.Method != HttpMethod.Get)
            return PropertyResult.Success("Property does not apply to non-GET requests");

        var beforeState = await _stateTracker.CaptureStateAsync(cancellationToken);
        
        var afterState = await _stateTracker.CaptureStateAsync(cancellationToken);
        
        var comparison = beforeState.CompareTo(afterState);
        
        if (comparison.HasChanges)
        {
            var details = $"GET request caused database changes: {comparison.GetSummary()}";
            _logger.LogWarning("GET safety violation: {Details}", details);
            
            return PropertyResult.Failure(
                "GET request caused observable side effects",
                details,
                new Dictionary<string, object>
                {
                    ["changedEntities"] = comparison.TotalEntitiesChanged,
                    ["changes"] = comparison.Changes.Select(c => new { c.EntityType, c.CountBefore, c.CountAfter }).ToList()
                });
        }

        return PropertyResult.Success(
            "No database changes detected",
            new Dictionary<string, object>
            {
                ["entitiesChecked"] = beforeState.TrackedEntityTypes.Count,
                ["captureTime"] = beforeState.CaptureTime.TotalMilliseconds
            });
    }
}

public class HeadMethodSafetyProperty<TContext> : IHttpProperty<TContext> where TContext : DbContext
{
    private readonly EfCoreStateTracker<TContext> _stateTracker;
    private readonly ILogger<HeadMethodSafetyProperty<TContext>> _logger;

    public string Name => "HEAD Method Safety";
    public string Description => "HEAD requests must not cause observable side effects in the system state";
    public string RfcReference => "RFC 7231 Section 4.2.1";

    public HeadMethodSafetyProperty(EfCoreStateTracker<TContext> stateTracker, ILogger<HeadMethodSafetyProperty<TContext>> logger)
    {
        _stateTracker = stateTracker;
        _logger = logger;
    }

    public async Task<PropertyResult> CheckAsync(
        HttpRequestMessage request, 
        HttpResponseMessage response, 
        TContext dbContext, 
        CancellationToken cancellationToken = default)
    {
        if (request.Method != HttpMethod.Head)
            return PropertyResult.Success("Property does not apply to non-HEAD requests");

        var beforeState = await _stateTracker.CaptureStateAsync(cancellationToken);
        
        var afterState = await _stateTracker.CaptureStateAsync(cancellationToken);
        
        var comparison = beforeState.CompareTo(afterState);
        
        if (comparison.HasChanges)
        {
            var details = $"HEAD request caused database changes: {comparison.GetSummary()}";
            _logger.LogWarning("HEAD safety violation: {Details}", details);
            
            return PropertyResult.Failure(
                "HEAD request caused observable side effects",
                details,
                new Dictionary<string, object>
                {
                    ["changedEntities"] = comparison.TotalEntitiesChanged,
                    ["changes"] = comparison.Changes.Select(c => new { c.EntityType, c.CountBefore, c.CountAfter }).ToList()
                });
        }

        return PropertyResult.Success("No database changes detected");
    }
}

public class OptionsMethodSafetyProperty<TContext> : IHttpProperty<TContext> where TContext : DbContext
{
    private readonly EfCoreStateTracker<TContext> _stateTracker;
    private readonly ILogger<OptionsMethodSafetyProperty<TContext>> _logger;

    public string Name => "OPTIONS Method Safety";
    public string Description => "OPTIONS requests must not cause observable side effects in the system state";
    public string RfcReference => "RFC 7231 Section 4.2.1";

    public OptionsMethodSafetyProperty(EfCoreStateTracker<TContext> stateTracker, ILogger<OptionsMethodSafetyProperty<TContext>> logger)
    {
        _stateTracker = stateTracker;
        _logger = logger;
    }

    public async Task<PropertyResult> CheckAsync(
        HttpRequestMessage request, 
        HttpResponseMessage response, 
        TContext dbContext, 
        CancellationToken cancellationToken = default)
    {
        if (request.Method != HttpMethod.Options)
            return PropertyResult.Success("Property does not apply to non-OPTIONS requests");

        var beforeState = await _stateTracker.CaptureStateAsync(cancellationToken);
        
        var afterState = await _stateTracker.CaptureStateAsync(cancellationToken);
        
        var comparison = beforeState.CompareTo(afterState);
        
        if (comparison.HasChanges)
        {
            var details = $"OPTIONS request caused database changes: {comparison.GetSummary()}";
            _logger.LogWarning("OPTIONS safety violation: {Details}", details);
            
            return PropertyResult.Failure(
                "OPTIONS request caused observable side effects",
                details,
                new Dictionary<string, object>
                {
                    ["changedEntities"] = comparison.TotalEntitiesChanged,
                    ["changes"] = comparison.Changes.Select(c => new { c.EntityType, c.CountBefore, c.CountAfter }).ToList()
                });
        }

        return PropertyResult.Success("No database changes detected");
    }
}