using Conformist.HttpRfc.Discovery;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Conformist.HttpRfc.Generators;

public class TestDataGenerator
{
    private readonly Random _random = new();
    private readonly ILogger<TestDataGenerator> _logger;

    public TestDataGenerator(ILogger<TestDataGenerator> logger)
    {
        _logger = logger;
    }

    public async Task<List<HttpRequestMessage>> GenerateRequestsForEndpoint<TContext>(
        EndpointInfo endpoint, 
        TContext dbContext,
        int maxRequests = 10,
        CancellationToken cancellationToken = default) where TContext : DbContext
    {
        var requests = new List<HttpRequestMessage>();
        var requestCount = Math.Min(maxRequests, _random.Next(1, 6)); 

        for (int i = 0; i < requestCount; i++)
        {
            try
            {
                var request = await GenerateSingleRequestAsync(endpoint, dbContext, cancellationToken);
                if (request != null)
                    requests.Add(request);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate request for endpoint {Method} {Path}", endpoint.Method, endpoint.Path);
            }
        }

        _logger.LogDebug("Generated {RequestCount} requests for {Method} {Path}", requests.Count, endpoint.Method, endpoint.Path);
        return requests;
    }

    private async Task<HttpRequestMessage?> GenerateSingleRequestAsync<TContext>(
        EndpointInfo endpoint, 
        TContext dbContext,
        CancellationToken cancellationToken = default) where TContext : DbContext
    {
        var path = await PopulatePathParametersAsync(endpoint, dbContext, cancellationToken);
        if (path == null)
            return null;

        var uri = new UriBuilder("https://localhost") { Path = path };
        var queryParams = new List<string>();

        foreach (var param in endpoint.Parameters.Where(p => p.In == ParameterLocation.Query))
        {
            var value = GenerateParameterValue(param);
            if (value != null)
                queryParams.Add($"{param.Name}={Uri.EscapeDataString(value.ToString()!)}");
        }

        if (queryParams.Any())
            uri.Query = string.Join("&", queryParams);

        var request = new HttpRequestMessage(new HttpMethod(endpoint.Method), uri.Uri);

        AddHeaders(request, endpoint);

        if (ShouldHaveRequestBody(endpoint.Method) && endpoint.RequestBody != null)
        {
            var body = GenerateRequestBody(endpoint.RequestBody);
            if (body.HasValue)
                request.Content = new StringContent(body.Value.Content, System.Text.Encoding.UTF8, body.Value.ContentType);
        }

        return request;
    }

    private async Task<string?> PopulatePathParametersAsync<TContext>(
        EndpointInfo endpoint, 
        TContext dbContext,
        CancellationToken cancellationToken = default) where TContext : DbContext
    {
        var path = endpoint.Path;
        var pathParams = endpoint.Parameters.Where(p => p.In == ParameterLocation.Path).ToList();

        if (!pathParams.Any())
            return path;

        foreach (var param in pathParams)
        {
            var value = await GeneratePathParameterValueAsync(param, dbContext, cancellationToken);
            if (value == null)
            {
                _logger.LogWarning("Could not generate value for required path parameter {ParamName} in {Path}", param.Name, path);
                return null;
            }

            path = path.Replace($"{{{param.Name}}}", value.ToString());
        }

        return path;
    }

    private async Task<object?> GeneratePathParameterValueAsync<TContext>(
        Discovery.ParameterInfo parameter, 
        TContext dbContext,
        CancellationToken cancellationToken = default) where TContext : DbContext
    {
        if (IsIdParameter(parameter))
        {
            var entityId = await TryGetExistingEntityIdAsync(parameter.Name, dbContext, cancellationToken);
            if (entityId != null)
                return entityId;
        }

        return GenerateParameterValue(parameter);
    }

    private async Task<object?> TryGetExistingEntityIdAsync<TContext>(
        string parameterName, 
        TContext dbContext,
        CancellationToken cancellationToken = default) where TContext : DbContext
    {
        try
        {
            var entityType = GuessEntityTypeFromParameterName(parameterName, dbContext);
            if (entityType == null)
                return null;

            var dbSetProperty = typeof(TContext).GetProperties()
                .FirstOrDefault(p => p.PropertyType.IsGenericType &&
                               p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>) &&
                               p.PropertyType.GetGenericArguments()[0] == entityType.ClrType);

            if (dbSetProperty == null)
                return null;

            var dbSet = (IQueryable)dbSetProperty.GetValue(dbContext)!;
            var primaryKey = entityType.FindPrimaryKey();
            if (primaryKey?.Properties.Count != 1)
                return null;

            var keyProperty = primaryKey.Properties[0];
            var entities = await EntityFrameworkQueryableExtensions.ToListAsync(
                (IQueryable<object>)dbSet, cancellationToken);

            if (!entities.Any())
                return null;

            var randomEntity = entities[_random.Next(entities.Count)];
            var keyValue = keyProperty.PropertyInfo?.GetValue(randomEntity);

            return keyValue;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to get existing entity ID for parameter {ParameterName}", parameterName);
            return null;
        }
    }

    private Microsoft.EntityFrameworkCore.Metadata.IEntityType? GuessEntityTypeFromParameterName<TContext>(
        string parameterName, 
        TContext dbContext) where TContext : DbContext
    {
        var normalizedParam = parameterName.ToLowerInvariant()
            .Replace("id", "")
            .Replace("_", "")
            .Replace("-", "");

        var entityTypes = dbContext.Model.GetEntityTypes();

        return entityTypes.FirstOrDefault(et => 
            et.ClrType.Name.ToLowerInvariant().Contains(normalizedParam) ||
            et.GetTableName()?.ToLowerInvariant().Contains(normalizedParam) == true);
    }

    private void AddHeaders(HttpRequestMessage request, EndpointInfo endpoint)
    {
        foreach (var param in endpoint.Parameters.Where(p => p.In == ParameterLocation.Header))
        {
            var value = GenerateParameterValue(param);
            if (value != null)
                request.Headers.TryAddWithoutValidation(param.Name, value.ToString());
        }

        if (!request.Headers.Contains("User-Agent"))
            request.Headers.TryAddWithoutValidation("User-Agent", "Conformist.HttpRfc/1.0");
    }

    private (string Content, string ContentType)? GenerateRequestBody(RequestBodyInfo requestBody)
    {
        var jsonContent = requestBody.Content.FirstOrDefault(c => 
            c.Key.Contains("json", StringComparison.OrdinalIgnoreCase));

        if (jsonContent.Value != null)
        {
            var jsonObject = GenerateJsonFromSchema(jsonContent.Value.Schema);
            return (JsonSerializer.Serialize(jsonObject), "application/json");
        }

        var xmlContent = requestBody.Content.FirstOrDefault(c => 
            c.Key.Contains("xml", StringComparison.OrdinalIgnoreCase));

        if (xmlContent.Value != null)
        {
            return ("<root>Sample XML content</root>", "application/xml");
        }

        var textContent = requestBody.Content.FirstOrDefault(c => 
            c.Key.Contains("text", StringComparison.OrdinalIgnoreCase));

        if (textContent.Value != null)
        {
            return ("Sample text content", "text/plain");
        }

        return null;
    }

    private object? GenerateJsonFromSchema(SchemaInfo schema)
    {
        return schema.Type?.ToLowerInvariant() switch
        {
            "object" => GenerateObjectFromSchema(schema),
            "array" => GenerateArrayFromSchema(schema),
            "string" => GenerateStringValue(schema),
            "integer" => GenerateIntegerValue(schema),
            "number" => GenerateDoubleValue(schema),
            "boolean" => _random.NextDouble() > 0.5,
            _ => schema.Example ?? GenerateStringValue(schema)
        };
    }

    private Dictionary<string, object?> GenerateObjectFromSchema(SchemaInfo schema)
    {
        var obj = new Dictionary<string, object?>();

        foreach (var property in schema.Properties)
        {
            var isRequired = schema.Required.Contains(property.Key);
            
            if (isRequired || _random.NextDouble() > 0.3)
            {
                obj[property.Key] = GenerateJsonFromSchema(property.Value);
            }
        }

        return obj;
    }

    private List<object?> GenerateArrayFromSchema(SchemaInfo schema)
    {
        var count = _random.Next(1, 4);
        var array = new List<object?>();

        if (schema.Items != null)
        {
            for (int i = 0; i < count; i++)
            {
                array.Add(GenerateJsonFromSchema(schema.Items));
            }
        }

        return array;
    }

    private object? GenerateParameterValue(Discovery.ParameterInfo parameter)
    {
        if (parameter.Schema.Enum.Any())
        {
            var enumValues = parameter.Schema.Enum;
            return enumValues[_random.Next(enumValues.Count)];
        }

        if (parameter.Example != null)
            return parameter.Example;

        return parameter.Schema.Type?.ToLowerInvariant() switch
        {
            "string" => GenerateStringValue(parameter.Schema),
            "integer" => GenerateIntegerValue(parameter.Schema),
            "number" => GenerateDoubleValue(parameter.Schema),
            "boolean" => _random.NextDouble() > 0.5,
            _ => GenerateStringValue(parameter.Schema)
        };
    }

    private string GenerateStringValue(SchemaInfo schema)
    {
        if (schema.Example != null)
            return schema.Example.ToString() ?? "";

        if (!string.IsNullOrEmpty(schema.Pattern))
        {
            try
            {
                return GenerateStringFromPattern(schema.Pattern);
            }
            catch
            {
                // Fall back to format-based generation
            }
        }

        return schema.Format?.ToLowerInvariant() switch
        {
            "email" => $"test{_random.Next(1000)}@example.com",
            "date" => DateTime.Today.ToString("yyyy-MM-dd"),
            "date-time" => DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            "uuid" => Guid.NewGuid().ToString(),
            "uri" => "https://example.com/resource",
            "password" => "SecurePassword123!",
            _ => GenerateRandomString(schema)
        };
    }

    private string GenerateRandomString(SchemaInfo schema)
    {
        var minLength = Math.Max(1, schema.MinLength ?? 1);
        var maxLength = Math.Min(50, schema.MaxLength ?? 20);
        var length = _random.Next(minLength, maxLength + 1);

        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[_random.Next(s.Length)]).ToArray());
    }

    private string GenerateStringFromPattern(string pattern)
    {
        var simplified = pattern
            .Replace("\\d", "[0-9]")
            .Replace("\\w", "[a-zA-Z0-9_]")
            .Replace("\\s", " ");

        if (simplified.Contains("[0-9]"))
            return _random.Next(100000, 999999).ToString();

        return $"pattern_match_{_random.Next(100)}";
    }

    private int GenerateIntegerValue(SchemaInfo schema)
    {
        var min = (int)(schema.Minimum ?? 1);
        var max = (int)(schema.Maximum ?? 1000);
        return _random.Next(min, max + 1);
    }

    private double GenerateDoubleValue(SchemaInfo schema)
    {
        var min = schema.Minimum ?? 0.0;
        var max = schema.Maximum ?? 1000.0;
        return min + _random.NextDouble() * (max - min);
    }

    private static bool ShouldHaveRequestBody(string method)
    {
        return method.ToUpperInvariant() is "POST" or "PUT" or "PATCH";
    }

    private static bool IsIdParameter(Discovery.ParameterInfo parameter)
    {
        var name = parameter.Name.ToLowerInvariant();
        return name.EndsWith("id") || name == "id" || 
               (parameter.Schema.Type?.ToLowerInvariant() is "integer" or "string" && name.Contains("id"));
    }
}