namespace Conformist.HttpRfc.StateTracking;

public class StateTrackingOptions
{
    public bool TrackEntityCounts { get; set; } = true;
    public bool TrackEntityChecksums { get; set; } = false;
    public bool TrackChangeTrackerState { get; set; } = true;
    public HashSet<string> ExcludeEntities { get; set; } = new();
    public HashSet<string> IncludeOnlyEntities { get; set; } = new();
    public int MaxParallelism { get; set; } = Environment.ProcessorCount;
    public bool UseCompiledQueries { get; set; } = true;
    public TimeSpan QueryTimeout { get; set; } = TimeSpan.FromSeconds(30);
    
    public void ExcludeEntity(string entityName)
    {
        ExcludeEntities.Add(entityName);
    }
    
    public void ExcludeEntity<TEntity>()
    {
        ExcludeEntities.Add(typeof(TEntity).Name);
    }
    
    public void IncludeOnlyEntity(string entityName)
    {
        IncludeOnlyEntities.Add(entityName);
    }
    
    public void IncludeOnlyEntity<TEntity>()
    {
        IncludeOnlyEntities.Add(typeof(TEntity).Name);
    }
    
    public bool ShouldTrackEntity(string entityName)
    {
        if (IncludeOnlyEntities.Any())
            return IncludeOnlyEntities.Contains(entityName);
        
        return !ExcludeEntities.Contains(entityName);
    }
}