using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Conformist.HttpRfc.StateTracking;

public class EfCoreStateTracker<TContext> where TContext : DbContext
{
    private readonly TContext _context;
    private readonly StateTrackingOptions _options;
    private readonly ILogger<EfCoreStateTracker<TContext>> _logger;
    private readonly ConcurrentDictionary<string, Func<Task<int>>> _compiledCountQueries = new();
    private readonly ConcurrentDictionary<string, Func<Task<string>>> _compiledChecksumQueries = new();
    private readonly List<IEntityType> _trackedEntityTypes;

    public EfCoreStateTracker(TContext context, StateTrackingOptions options, ILogger<EfCoreStateTracker<TContext>> logger)
    {
        _context = context;
        _options = options;
        _logger = logger;
        _trackedEntityTypes = DiscoverEntityTypes();
        
        if (_options.UseCompiledQueries)
        {
            InitializeCompiledQueries();
        }
    }

    public async Task<StateSnapshot> CaptureStateAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var entitySnapshots = new ConcurrentDictionary<string, EntitySnapshot>();

        var semaphore = new SemaphoreSlim(_options.MaxParallelism);
        var tasks = _trackedEntityTypes.Select(async entityType =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                var snapshot = await CaptureEntitySnapshotAsync(entityType, cancellationToken);
                if (snapshot != null)
                    entitySnapshots[entityType.Name] = snapshot;
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        stopwatch.Stop();

        _logger.LogDebug("Captured state for {EntityCount} entities in {ElapsedMs}ms", 
            entitySnapshots.Count, stopwatch.ElapsedMilliseconds);

        return new StateSnapshot
        {
            CapturedAt = DateTime.UtcNow,
            Entities = new Dictionary<string, EntitySnapshot>(entitySnapshots),
            TrackedEntityTypes = _trackedEntityTypes.Select(e => e.Name).ToList(),
            CaptureTime = stopwatch.Elapsed
        };
    }

    private List<IEntityType> DiscoverEntityTypes()
    {
        var model = _context.Model;
        var entityTypes = model.GetEntityTypes()
            .Where(e => !e.IsOwned())
            .Where(e => _options.ShouldTrackEntity(e.Name))
            .ToList();

        _logger.LogDebug("Discovered {EntityCount} entity types for tracking: {EntityNames}", 
            entityTypes.Count, string.Join(", ", entityTypes.Select(e => e.Name)));

        return entityTypes;
    }

    private void InitializeCompiledQueries()
    {
        foreach (var entityType in _trackedEntityTypes)
        {
            try
            {
                var countQuery = CreateCompiledCountQuery(entityType);
                _compiledCountQueries[entityType.Name] = countQuery;

                if (_options.TrackEntityChecksums)
                {
                    var checksumQuery = CreateCompiledChecksumQuery(entityType);
                    _compiledChecksumQueries[entityType.Name] = checksumQuery;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create compiled queries for entity {EntityName}", entityType.Name);
            }
        }
    }

    private Func<Task<int>> CreateCompiledCountQuery(IEntityType entityType)
    {
        var clrType = entityType.ClrType;
        var dbSetProperty = typeof(TContext).GetProperties()
            .FirstOrDefault(p => p.PropertyType.IsGenericType && 
                               p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>) &&
                               p.PropertyType.GetGenericArguments()[0] == clrType);

        if (dbSetProperty == null)
            throw new InvalidOperationException($"Could not find DbSet property for entity {entityType.Name}");

        var contextParam = Expression.Parameter(typeof(TContext), "context");
        var dbSetAccess = Expression.Property(contextParam, dbSetProperty);
        var countCall = Expression.Call(typeof(EntityFrameworkQueryableExtensions), "CountAsync",
            new[] { clrType }, dbSetAccess, Expression.Constant(CancellationToken.None));

        var lambda = Expression.Lambda<Func<TContext, Task<int>>>(countCall, contextParam);
        var compiled = lambda.Compile();

        return () => compiled(_context);
    }

    private Func<Task<string>> CreateCompiledChecksumQuery(IEntityType entityType)
    {
        var clrType = entityType.ClrType;
        var dbSetProperty = typeof(TContext).GetProperties()
            .FirstOrDefault(p => p.PropertyType.IsGenericType && 
                               p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>) &&
                               p.PropertyType.GetGenericArguments()[0] == clrType);

        if (dbSetProperty == null)
            throw new InvalidOperationException($"Could not find DbSet property for entity {entityType.Name}");

        return async () =>
        {
            var dbSet = (IQueryable)dbSetProperty.GetValue(_context)!;
            var entities = await EntityFrameworkQueryableExtensions.ToListAsync(
                (IQueryable<object>)dbSet, CancellationToken.None);
            
            return ComputeChecksum(entities);
        };
    }

    private async Task<EntitySnapshot?> CaptureEntitySnapshotAsync(IEntityType entityType, CancellationToken cancellationToken)
    {
        var queryStopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            int count;
            string? checksum = null;

            if (_options.UseCompiledQueries && _compiledCountQueries.TryGetValue(entityType.Name, out var compiledCountQuery))
            {
                count = await compiledCountQuery();
            }
            else
            {
                count = await GetEntityCountAsync(entityType, cancellationToken);
            }

            if (_options.TrackEntityChecksums)
            {
                if (_options.UseCompiledQueries && _compiledChecksumQueries.TryGetValue(entityType.Name, out var compiledChecksumQuery))
                {
                    checksum = await compiledChecksumQuery();
                }
                else
                {
                    checksum = await GetEntityChecksumAsync(entityType, cancellationToken);
                }
            }

            queryStopwatch.Stop();

            return new EntitySnapshot
            {
                EntityType = entityType.Name,
                Count = count,
                Checksum = checksum,
                CapturedAt = DateTime.UtcNow,
                QueryTime = queryStopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            queryStopwatch.Stop();
            _logger.LogWarning(ex, "Failed to capture snapshot for entity {EntityName}", entityType.Name);
            return null;
        }
    }

    private async Task<int> GetEntityCountAsync(IEntityType entityType, CancellationToken cancellationToken)
    {
        var clrType = entityType.ClrType;
        var dbSetProperty = typeof(TContext).GetProperties()
            .FirstOrDefault(p => p.PropertyType.IsGenericType && 
                               p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>) &&
                               p.PropertyType.GetGenericArguments()[0] == clrType);

        if (dbSetProperty == null)
            return 0;

        var dbSet = dbSetProperty.GetValue(_context);
        var countMethod = typeof(EntityFrameworkQueryableExtensions)
            .GetMethods()
            .First(m => m.Name == "CountAsync" && m.GetParameters().Length == 2)
            .MakeGenericMethod(clrType);

        var countTask = (Task<int>)countMethod.Invoke(null, new[] { dbSet, cancellationToken })!;
        return await countTask;
    }

    private async Task<string> GetEntityChecksumAsync(IEntityType entityType, CancellationToken cancellationToken)
    {
        var clrType = entityType.ClrType;
        var dbSetProperty = typeof(TContext).GetProperties()
            .FirstOrDefault(p => p.PropertyType.IsGenericType && 
                               p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>) &&
                               p.PropertyType.GetGenericArguments()[0] == clrType);

        if (dbSetProperty == null)
            return "";

        var dbSet = (IQueryable)dbSetProperty.GetValue(_context)!;
        var toListMethod = typeof(EntityFrameworkQueryableExtensions)
            .GetMethods()
            .First(m => m.Name == "ToListAsync" && m.GetParameters().Length == 2)
            .MakeGenericMethod(clrType);

        var listTask = (Task)toListMethod.Invoke(null, new object[] { dbSet, cancellationToken })!;
        await listTask;

        var result = listTask.GetType().GetProperty("Result")!.GetValue(listTask);
        var entities = ((System.Collections.IEnumerable)result!).Cast<object>().ToList();

        return ComputeChecksum(entities);
    }

    private static string ComputeChecksum(IEnumerable<object> entities)
    {
        var json = JsonSerializer.Serialize(entities, new JsonSerializerOptions 
        { 
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });

        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hashBytes);
    }
}