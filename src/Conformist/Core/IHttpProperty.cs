using Microsoft.EntityFrameworkCore;

namespace Conformist.HttpRfc.Core;

public interface IHttpProperty<in TContext> where TContext : DbContext
{
    string Name { get; }
    string Description { get; }
    string RfcReference { get; }
    
    Task<PropertyResult> CheckAsync(
        HttpRequestMessage request, 
        HttpResponseMessage response, 
        TContext dbContext,
        CancellationToken cancellationToken = default);
}

public class PropertyResult
{
    public bool Passed { get; init; }
    public string? FailureReason { get; init; }
    public string? Details { get; init; }
    public Dictionary<string, object>? Metrics { get; init; }

    public static PropertyResult Success(string? details = null, Dictionary<string, object>? metrics = null) =>
        new() { Passed = true, Details = details, Metrics = metrics };

    public static PropertyResult Failure(string reason, string? details = null, Dictionary<string, object>? metrics = null) =>
        new() { Passed = false, FailureReason = reason, Details = details, Metrics = metrics };
}