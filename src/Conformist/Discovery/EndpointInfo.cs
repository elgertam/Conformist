using Microsoft.OpenApi.Models;

namespace Conformist.HttpRfc.Discovery;

public class EndpointInfo
{
    public string Path { get; init; } = "";
    public string Method { get; init; } = "";
    public string OperationId { get; init; } = "";
    public string Summary { get; init; } = "";
    public string Description { get; init; } = "";
    public List<ParameterInfo> Parameters { get; init; } = new();
    public Dictionary<string, ResponseInfo> Responses { get; init; } = new();
    public RequestBodyInfo? RequestBody { get; init; }
    public List<string> Tags { get; init; } = new();
    public bool Deprecated { get; init; }
    public Dictionary<string, object> Extensions { get; init; } = new();
}

public class ParameterInfo
{
    public string Name { get; init; } = "";
    public ParameterLocation In { get; init; }
    public bool Required { get; init; }
    public string Description { get; init; } = "";
    public SchemaInfo Schema { get; init; } = new();
    public object? Example { get; init; }
    public Dictionary<string, ExampleInfo> Examples { get; init; } = new();
}

public class ResponseInfo
{
    public string StatusCode { get; init; } = "";
    public string Description { get; init; } = "";
    public Dictionary<string, MediaTypeInfo> Content { get; init; } = new();
    public Dictionary<string, HeaderInfo> Headers { get; init; } = new();
}

public class RequestBodyInfo
{
    public string Description { get; init; } = "";
    public bool Required { get; init; }
    public Dictionary<string, MediaTypeInfo> Content { get; init; } = new();
}

public class MediaTypeInfo
{
    public string MediaType { get; init; } = "";
    public SchemaInfo Schema { get; init; } = new();
    public object? Example { get; init; }
    public Dictionary<string, ExampleInfo> Examples { get; init; } = new();
}

public class HeaderInfo
{
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public bool Required { get; init; }
    public SchemaInfo Schema { get; init; } = new();
}

public class SchemaInfo
{
    public string Type { get; init; } = "";
    public string Format { get; init; } = "";
    public bool Nullable { get; init; }
    public object? Default { get; init; }
    public object? Example { get; init; }
    public List<object> Enum { get; init; } = new();
    public double? Minimum { get; init; }
    public double? Maximum { get; init; }
    public int? MinLength { get; init; }
    public int? MaxLength { get; init; }
    public string Pattern { get; init; } = "";
    public SchemaInfo? Items { get; init; }
    public Dictionary<string, SchemaInfo> Properties { get; init; } = new();
    public List<string> Required { get; init; } = new();
    public bool AdditionalProperties { get; init; }
    public string Reference { get; init; } = "";
}

public class ExampleInfo
{
    public string Name { get; init; } = "";
    public string Summary { get; init; } = "";
    public string Description { get; init; } = "";
    public object? Value { get; init; }
    public string ExternalValue { get; init; } = "";
}

public enum ParameterLocation
{
    Query,
    Header,
    Path,
    Cookie
}