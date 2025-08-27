using Conformist.HttpRfc.Core;
using Conformist.HttpRfc.StateTracking;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Net;

namespace Conformist.HttpRfc.Properties;

public class PutMethodIdempotencyProperty<TContext> : IHttpProperty<TContext> where TContext : DbContext
{
    private readonly WebApplicationFactory<object> _factory;
    private readonly EfCoreStateTracker<TContext> _stateTracker;
    private readonly ILogger<PutMethodIdempotencyProperty<TContext>> _logger;

    public string Name => "PUT Method Idempotency";
    public string Description => "Multiple identical PUT requests should have the same effect as a single request";
    public string RfcReference => "RFC 7231 Section 4.2.2";

    public PutMethodIdempotencyProperty(
        WebApplicationFactory<object> factory,
        EfCoreStateTracker<TContext> stateTracker, 
        ILogger<PutMethodIdempotencyProperty<TContext>> logger)
    {
        _factory = factory;
        _stateTracker = stateTracker;
        _logger = logger;
    }

    public async Task<PropertyResult> CheckAsync(
        HttpRequestMessage request, 
        HttpResponseMessage response, 
        TContext dbContext, 
        CancellationToken cancellationToken = default)
    {
        if (request.Method != HttpMethod.Put)
            return PropertyResult.Success("Property does not apply to non-PUT requests");

        if (!response.IsSuccessStatusCode)
            return PropertyResult.Success("Idempotency test skipped for non-successful response");

        try
        {
            var beforeState = await _stateTracker.CaptureStateAsync(cancellationToken);

            var duplicatedRequest = await CloneRequestAsync(request);
            var client = _factory.CreateClient();
            var secondResponse = await client.SendAsync(duplicatedRequest, cancellationToken);

            var afterState = await _stateTracker.CaptureStateAsync(cancellationToken);
            
            var stateComparison = beforeState.CompareTo(afterState);
            
            if (stateComparison.HasChanges)
            {
                var details = $"Second PUT request caused additional changes: {stateComparison.GetSummary()}";
                _logger.LogWarning("PUT idempotency violation: {Details}", details);
                
                return PropertyResult.Failure(
                    "PUT request is not idempotent - second request caused additional state changes",
                    details,
                    new Dictionary<string, object>
                    {
                        ["firstStatusCode"] = (int)response.StatusCode,
                        ["secondStatusCode"] = (int)secondResponse.StatusCode,
                        ["stateChanges"] = stateComparison.Changes.Count
                    });
            }

            if (response.StatusCode != secondResponse.StatusCode)
            {
                return PropertyResult.Failure(
                    "PUT request returned different status codes on repeated requests",
                    $"First: {response.StatusCode}, Second: {secondResponse.StatusCode}",
                    new Dictionary<string, object>
                    {
                        ["firstStatusCode"] = (int)response.StatusCode,
                        ["secondStatusCode"] = (int)secondResponse.StatusCode
                    });
            }

            return PropertyResult.Success(
                "PUT request is idempotent",
                new Dictionary<string, object>
                {
                    ["statusCode"] = (int)response.StatusCode,
                    ["entitiesChecked"] = beforeState.TrackedEntityTypes.Count
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during PUT idempotency check");
            return PropertyResult.Failure($"Idempotency check failed with exception: {ex.Message}", ex.ToString());
        }
    }

    private async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage original)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri);
        
        foreach (var header in original.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        if (original.Content != null)
        {
            var content = await original.Content.ReadAsStringAsync();
            clone.Content = new StringContent(content, System.Text.Encoding.UTF8, 
                original.Content.Headers.ContentType?.MediaType ?? "application/json");
            
            foreach (var header in original.Content.Headers)
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }
}

public class DeleteMethodIdempotencyProperty<TContext> : IHttpProperty<TContext> where TContext : DbContext
{
    private readonly WebApplicationFactory<object> _factory;
    private readonly EfCoreStateTracker<TContext> _stateTracker;
    private readonly ILogger<DeleteMethodIdempotencyProperty<TContext>> _logger;

    public string Name => "DELETE Method Idempotency";
    public string Description => "Multiple identical DELETE requests should have the same effect as a single request";
    public string RfcReference => "RFC 7231 Section 4.2.2";

    public DeleteMethodIdempotencyProperty(
        WebApplicationFactory<object> factory,
        EfCoreStateTracker<TContext> stateTracker, 
        ILogger<DeleteMethodIdempotencyProperty<TContext>> logger)
    {
        _factory = factory;
        _stateTracker = stateTracker;
        _logger = logger;
    }

    public async Task<PropertyResult> CheckAsync(
        HttpRequestMessage request, 
        HttpResponseMessage response, 
        TContext dbContext, 
        CancellationToken cancellationToken = default)
    {
        if (request.Method != HttpMethod.Delete)
            return PropertyResult.Success("Property does not apply to non-DELETE requests");

        try
        {
            var beforeState = await _stateTracker.CaptureStateAsync(cancellationToken);

            var duplicatedRequest = await CloneRequestAsync(request);
            var client = _factory.CreateClient();
            var secondResponse = await client.SendAsync(duplicatedRequest, cancellationToken);

            var afterState = await _stateTracker.CaptureStateAsync(cancellationToken);
            
            var stateComparison = beforeState.CompareTo(afterState);
            
            if (stateComparison.HasChanges)
            {
                var details = $"Second DELETE request caused additional changes: {stateComparison.GetSummary()}";
                _logger.LogWarning("DELETE idempotency violation: {Details}", details);
                
                return PropertyResult.Failure(
                    "DELETE request is not idempotent - second request caused additional state changes",
                    details,
                    new Dictionary<string, object>
                    {
                        ["firstStatusCode"] = (int)response.StatusCode,
                        ["secondStatusCode"] = (int)secondResponse.StatusCode,
                        ["stateChanges"] = stateComparison.Changes.Count
                    });
            }

            var isValidIdempotentBehavior = IsValidDeleteIdempotentResponse(response.StatusCode, secondResponse.StatusCode);
            
            if (!isValidIdempotentBehavior)
            {
                return PropertyResult.Failure(
                    "DELETE request returned invalid status code sequence for idempotent behavior",
                    $"First: {response.StatusCode}, Second: {secondResponse.StatusCode}. Expected: success followed by 404 or same status",
                    new Dictionary<string, object>
                    {
                        ["firstStatusCode"] = (int)response.StatusCode,
                        ["secondStatusCode"] = (int)secondResponse.StatusCode
                    });
            }

            return PropertyResult.Success(
                "DELETE request is idempotent",
                new Dictionary<string, object>
                {
                    ["firstStatusCode"] = (int)response.StatusCode,
                    ["secondStatusCode"] = (int)secondResponse.StatusCode,
                    ["entitiesChecked"] = beforeState.TrackedEntityTypes.Count
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during DELETE idempotency check");
            return PropertyResult.Failure($"Idempotency check failed with exception: {ex.Message}", ex.ToString());
        }
    }

    private static bool IsValidDeleteIdempotentResponse(HttpStatusCode first, HttpStatusCode second)
    {
        if (first == second)
            return true;

        if (first is HttpStatusCode.OK or HttpStatusCode.Accepted or HttpStatusCode.NoContent &&
            second == HttpStatusCode.NotFound)
            return true;

        return false;
    }

    private async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage original)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri);
        
        foreach (var header in original.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        if (original.Content != null)
        {
            var content = await original.Content.ReadAsStringAsync();
            clone.Content = new StringContent(content, System.Text.Encoding.UTF8, 
                original.Content.Headers.ContentType?.MediaType ?? "application/json");
            
            foreach (var header in original.Content.Headers)
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }
}