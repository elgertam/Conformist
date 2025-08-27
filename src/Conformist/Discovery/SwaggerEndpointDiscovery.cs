using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using System.Text.Json;

namespace Conformist.HttpRfc.Discovery;

public class SwaggerEndpointDiscovery
{
    private readonly ILogger<SwaggerEndpointDiscovery> _logger;

    public SwaggerEndpointDiscovery(ILogger<SwaggerEndpointDiscovery> logger)
    {
        _logger = logger;
    }

    public async Task<List<EndpointInfo>> DiscoverEndpointsAsync<TEntryPoint>(
        WebApplicationFactory<TEntryPoint> factory,
        string swaggerPath = "/swagger/v1/swagger.json",
        CancellationToken cancellationToken = default) where TEntryPoint : class
    {
        try
        {
            var client = factory.CreateClient();
            var swaggerResponse = await client.GetAsync(swaggerPath, cancellationToken);

            if (!swaggerResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch swagger document from {SwaggerPath}. Status: {StatusCode}", 
                    swaggerPath, swaggerResponse.StatusCode);
                return new List<EndpointInfo>();
            }

            var swaggerContent = await swaggerResponse.Content.ReadAsStringAsync(cancellationToken);
            var reader = new OpenApiStringReader();
            var document = reader.Read(swaggerContent, out var diagnostic);

            if (diagnostic.Errors.Any())
            {
                _logger.LogWarning("OpenAPI document parsing errors: {Errors}", 
                    string.Join(", ", diagnostic.Errors.Select(e => e.Message)));
            }

            return ParseEndpoints(document);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to discover endpoints from swagger document");
            return new List<EndpointInfo>();
        }
    }

    public async Task<List<EndpointInfo>> DiscoverEndpointsFromFileAsync(
        string swaggerFilePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var swaggerContent = await File.ReadAllTextAsync(swaggerFilePath, cancellationToken);
            var reader = new OpenApiStringReader();
            var document = reader.Read(swaggerContent, out var diagnostic);

            if (diagnostic.Errors.Any())
            {
                _logger.LogWarning("OpenAPI document parsing errors: {Errors}", 
                    string.Join(", ", diagnostic.Errors.Select(e => e.Message)));
            }

            return ParseEndpoints(document);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to discover endpoints from swagger file {FilePath}", swaggerFilePath);
            return new List<EndpointInfo>();
        }
    }

    private List<EndpointInfo> ParseEndpoints(OpenApiDocument document)
    {
        var endpoints = new List<EndpointInfo>();

        foreach (var path in document.Paths)
        {
            foreach (var operation in path.Value.Operations)
            {
                var endpointInfo = new EndpointInfo
                {
                    Path = path.Key,
                    Method = operation.Key.ToString().ToUpperInvariant(),
                    OperationId = operation.Value.OperationId ?? "",
                    Summary = operation.Value.Summary ?? "",
                    Description = operation.Value.Description ?? "",
                    Parameters = ParseParameters(operation.Value.Parameters),
                    Responses = ParseResponses(operation.Value.Responses),
                    RequestBody = ParseRequestBody(operation.Value.RequestBody),
                    Tags = operation.Value.Tags?.Select(t => t.Name).ToList() ?? new List<string>(),
                    Deprecated = operation.Value.Deprecated,
                    Extensions = operation.Value.Extensions?.ToDictionary(
                        kvp => kvp.Key, 
                        kvp => (object)kvp.Value.ToString()) ?? new Dictionary<string, object>()
                };

                endpoints.Add(endpointInfo);
            }
        }

        _logger.LogDebug("Discovered {EndpointCount} endpoints from OpenAPI document", endpoints.Count);
        return endpoints;
    }

    private List<ParameterInfo> ParseParameters(IList<OpenApiParameter>? parameters)
    {
        if (parameters == null)
            return new List<ParameterInfo>();

        return parameters.Select(p => new ParameterInfo
        {
            Name = p.Name,
            In = ParseParameterLocation(p.In),
            Required = p.Required,
            Description = p.Description ?? "",
            Schema = ParseSchema(p.Schema),
            Example = p.Example?.ToString(),
            Examples = ParseExamples(p.Examples)
        }).ToList();
    }

    private Dictionary<string, ResponseInfo> ParseResponses(OpenApiResponses responses)
    {
        return responses.ToDictionary(
            kvp => kvp.Key,
            kvp => new ResponseInfo
            {
                StatusCode = kvp.Key,
                Description = kvp.Value.Description ?? "",
                Content = ParseMediaTypes(kvp.Value.Content),
                Headers = ParseHeaders(kvp.Value.Headers)
            });
    }

    private RequestBodyInfo? ParseRequestBody(OpenApiRequestBody? requestBody)
    {
        if (requestBody == null)
            return null;

        return new RequestBodyInfo
        {
            Description = requestBody.Description ?? "",
            Required = requestBody.Required,
            Content = ParseMediaTypes(requestBody.Content)
        };
    }

    private Dictionary<string, MediaTypeInfo> ParseMediaTypes(IDictionary<string, OpenApiMediaType>? content)
    {
        if (content == null)
            return new Dictionary<string, MediaTypeInfo>();

        return content.ToDictionary(
            kvp => kvp.Key,
            kvp => new MediaTypeInfo
            {
                MediaType = kvp.Key,
                Schema = ParseSchema(kvp.Value.Schema),
                Example = kvp.Value.Example?.ToString(),
                Examples = ParseExamples(kvp.Value.Examples)
            });
    }

    private Dictionary<string, HeaderInfo> ParseHeaders(IDictionary<string, OpenApiHeader>? headers)
    {
        if (headers == null)
            return new Dictionary<string, HeaderInfo>();

        return headers.ToDictionary(
            kvp => kvp.Key,
            kvp => new HeaderInfo
            {
                Name = kvp.Key,
                Description = kvp.Value.Description ?? "",
                Required = kvp.Value.Required,
                Schema = ParseSchema(kvp.Value.Schema)
            });
    }

    private SchemaInfo ParseSchema(OpenApiSchema? schema)
    {
        if (schema == null)
            return new SchemaInfo();

        return new SchemaInfo
        {
            Type = schema.Type ?? "",
            Format = schema.Format ?? "",
            Nullable = schema.Nullable,
            Default = schema.Default?.ToString(),
            Example = schema.Example?.ToString(),
            Enum = schema.Enum?.Select(e => (object)e.ToString()).ToList() ?? new List<object>(),
            Minimum = (double?)schema.Minimum,
            Maximum = (double?)schema.Maximum,
            MinLength = schema.MinLength,
            MaxLength = schema.MaxLength,
            Pattern = schema.Pattern ?? "",
            Items = ParseSchema(schema.Items),
            Properties = schema.Properties?.ToDictionary(
                kvp => kvp.Key,
                kvp => ParseSchema(kvp.Value)) ?? new Dictionary<string, SchemaInfo>(),
            Required = schema.Required?.ToList() ?? new List<string>(),
            AdditionalProperties = schema.AdditionalProperties != null,
            Reference = schema.Reference?.Id ?? ""
        };
    }

    private Dictionary<string, ExampleInfo> ParseExamples(IDictionary<string, OpenApiExample>? examples)
    {
        if (examples == null)
            return new Dictionary<string, ExampleInfo>();

        return examples.ToDictionary(
            kvp => kvp.Key,
            kvp => new ExampleInfo
            {
                Name = kvp.Key,
                Summary = kvp.Value.Summary ?? "",
                Description = kvp.Value.Description ?? "",
                Value = kvp.Value.Value?.ToString(),
                ExternalValue = kvp.Value.ExternalValue ?? ""
            });
    }

    private static ParameterLocation ParseParameterLocation(Microsoft.OpenApi.Models.ParameterLocation? location)
    {
        return location switch
        {
            Microsoft.OpenApi.Models.ParameterLocation.Query => ParameterLocation.Query,
            Microsoft.OpenApi.Models.ParameterLocation.Header => ParameterLocation.Header,
            Microsoft.OpenApi.Models.ParameterLocation.Path => ParameterLocation.Path,
            Microsoft.OpenApi.Models.ParameterLocation.Cookie => ParameterLocation.Cookie,
            _ => ParameterLocation.Query
        };
    }

    public List<string> GetAllSupportedMethods(List<EndpointInfo> endpoints, string path)
    {
        return endpoints
            .Where(e => NormalizePath(e.Path) == NormalizePath(path))
            .Select(e => e.Method)
            .Distinct()
            .ToList();
    }

    public List<EndpointInfo> GetEndpointsForPath(List<EndpointInfo> endpoints, string path)
    {
        return endpoints
            .Where(e => NormalizePath(e.Path) == NormalizePath(path))
            .ToList();
    }

    private static string NormalizePath(string path)
    {
        return path.TrimEnd('/').ToLowerInvariant();
    }
}