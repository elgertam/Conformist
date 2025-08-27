using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Conformist.HttpRfc.StateTracking;

public class StateSnapshot
{
    public DateTime CapturedAt { get; init; }
    public Dictionary<string, EntitySnapshot> Entities { get; init; } = new();
    public List<string> TrackedEntityTypes { get; init; } = new();
    public TimeSpan CaptureTime { get; init; }

    public StateComparison CompareTo(StateSnapshot other)
    {
        var changes = new List<EntityChange>();
        var allEntityTypes = TrackedEntityTypes.Union(other.TrackedEntityTypes).Distinct();

        foreach (var entityType in allEntityTypes)
        {
            var beforeSnapshot = Entities.GetValueOrDefault(entityType);
            var afterSnapshot = other.Entities.GetValueOrDefault(entityType);

            if (beforeSnapshot == null && afterSnapshot == null)
                continue;

            var change = new EntityChange
            {
                EntityType = entityType,
                CountBefore = beforeSnapshot?.Count ?? 0,
                CountAfter = afterSnapshot?.Count ?? 0,
                ChecksumBefore = beforeSnapshot?.Checksum,
                ChecksumAfter = afterSnapshot?.Checksum
            };

            change.CountChanged = change.CountBefore != change.CountAfter;
            change.DataChanged = !string.IsNullOrEmpty(change.ChecksumBefore) && 
                               !string.IsNullOrEmpty(change.ChecksumAfter) && 
                               change.ChecksumBefore != change.ChecksumAfter;

            if (change.CountChanged || change.DataChanged)
                changes.Add(change);
        }

        return new StateComparison
        {
            BeforeSnapshot = this,
            AfterSnapshot = other,
            Changes = changes,
            HasChanges = changes.Any(),
            TotalEntitiesChanged = changes.Count,
            TotalCountChanges = changes.Count(c => c.CountChanged),
            TotalDataChanges = changes.Count(c => c.DataChanged)
        };
    }
}

public class EntitySnapshot
{
    public string EntityType { get; init; } = "";
    public int Count { get; init; }
    public string? Checksum { get; init; }
    public DateTime CapturedAt { get; init; }
    public TimeSpan QueryTime { get; init; }
}

public class StateComparison
{
    public StateSnapshot BeforeSnapshot { get; init; } = null!;
    public StateSnapshot AfterSnapshot { get; init; } = null!;
    public List<EntityChange> Changes { get; init; } = new();
    public bool HasChanges { get; init; }
    public int TotalEntitiesChanged { get; init; }
    public int TotalCountChanges { get; init; }
    public int TotalDataChanges { get; init; }

    public string GetSummary()
    {
        if (!HasChanges)
            return "No database changes detected";

        var summaryParts = new List<string>();
        
        foreach (var change in Changes)
        {
            var parts = new List<string>();
            
            if (change.CountChanged)
                parts.Add($"{change.CountBefore} → {change.CountAfter} records");
            
            if (change.DataChanged)
                parts.Add("data modified");
            
            summaryParts.Add($"{change.EntityType}: {string.Join(", ", parts)}");
        }

        return string.Join("; ", summaryParts);
    }
}

public class EntityChange
{
    public string EntityType { get; init; } = "";
    public int CountBefore { get; init; }
    public int CountAfter { get; init; }
    public string? ChecksumBefore { get; init; }
    public string? ChecksumAfter { get; init; }
    public bool CountChanged { get; set; }
    public bool DataChanged { get; set; }
}