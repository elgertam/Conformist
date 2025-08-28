using Conformist.HttpRfc.Core;
using Conformist.HttpRfc.Discovery;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Net;

namespace Conformist.HttpRfc.Properties;

public class HeadGetConsistencyProperty<TContext, TProgram> : IHttpProperty<TContext> where TContext : DbContext where TProgram : class
{
    private readonly WebApplicationFactory<TProgram> _factory;
    private readonly ILogger<HeadGetConsistencyProperty<TContext, TProgram>> _logger;

    public string Name => "HEAD-GET Response Consistency";
    public string Description => "HEAD responses must have the same headers as GET responses but without a body";
    public string RfcReference => "RFC 7231 Section 4.3.2";

    public HeadGetConsistencyProperty(WebApplicationFactory<TProgram> factory, ILogger<HeadGetConsistencyProperty<TContext, TProgram>> logger)
    {
        _factory = factory;
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

        try
        {
            var getRequest = new HttpRequestMessage(HttpMethod.Get, request.RequestUri);
            
            foreach (var header in request.Headers)
                getRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);

            var client = _factory.CreateClient();
            var getResponse = await client.SendAsync(getRequest, cancellationToken);

            var headContentLength = response.Content.Headers.ContentLength ?? 0;
            if (headContentLength > 0)
            {
                return PropertyResult.Failure(
                    "HEAD response should not contain a message body",
                    $"Content-Length: {headContentLength}",
                    new Dictionary<string, object>
                    {
                        ["headContentLength"] = headContentLength
                    });
            }

            if (response.StatusCode != getResponse.StatusCode)
            {
                return PropertyResult.Failure(
                    "HEAD and GET responses have different status codes",
                    $"HEAD: {response.StatusCode}, GET: {getResponse.StatusCode}",
                    new Dictionary<string, object>
                    {
                        ["headStatusCode"] = (int)response.StatusCode,
                        ["getStatusCode"] = (int)getResponse.StatusCode
                    });
            }

            var headerDifferences = CompareHeaders(response.Headers, getResponse.Headers);
            var contentHeaderDifferences = CompareHeaders(response.Content.Headers, getResponse.Content.Headers);

            var allDifferences = headerDifferences.Concat(contentHeaderDifferences).ToList();

            if (allDifferences.Any())
            {
                var details = string.Join("; ", allDifferences);
                return PropertyResult.Failure(
                    "HEAD and GET responses have different headers",
                    details,
                    new Dictionary<string, object>
                    {
                        ["headerDifferences"] = allDifferences.Count,
                        ["differences"] = allDifferences
                    });
            }

            return PropertyResult.Success(
                "HEAD response is consistent with GET response",
                new Dictionary<string, object>
                {
                    ["statusCode"] = (int)response.StatusCode,
                    ["headersChecked"] = response.Headers.Count() + response.Content.Headers.Count()
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during HEAD-GET consistency check");
            return PropertyResult.Failure($"Consistency check failed with exception: {ex.Message}", ex.ToString());
        }
    }

    private static List<string> CompareHeaders(System.Net.Http.Headers.HttpHeaders headers1, System.Net.Http.Headers.HttpHeaders headers2)
    {
        var differences = new List<string>();
        var allHeaderNames = headers1.Select(h => h.Key).Union(headers2.Select(h => h.Key)).Distinct();

        foreach (var headerName in allHeaderNames)
        {
            var values1 = headers1.TryGetValues(headerName, out var v1) ? string.Join(", ", v1) : null;
            var values2 = headers2.TryGetValues(headerName, out var v2) ? string.Join(", ", v2) : null;

            if (values1 != values2)
            {
                differences.Add($"{headerName}: HEAD='{values1}', GET='{values2}'");
            }
        }

        return differences;
    }
}

public class OptionsAllowHeaderProperty<TContext> : IHttpProperty<TContext> where TContext : DbContext
{
    private readonly List<EndpointInfo> _endpoints;
    private readonly ILogger<OptionsAllowHeaderProperty<TContext>> _logger;

    public string Name => "OPTIONS Allow Header";
    public string Description => "OPTIONS responses must include an accurate Allow header listing supported methods";
    public string RfcReference => "RFC 7231 Section 4.3.7";

    public OptionsAllowHeaderProperty(List<EndpointInfo> endpoints, ILogger<OptionsAllowHeaderProperty<TContext>> logger)
    {
        _endpoints = endpoints;
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

        var path = request.RequestUri?.AbsolutePath ?? "";
        var supportedMethods = GetSupportedMethodsForPath(path);

        if (!response.Headers.TryGetValues("Allow", out var allowHeaderValues))
        {
            return PropertyResult.Failure(
                "OPTIONS response missing Allow header",
                $"Path: {path}",
                new Dictionary<string, object>
                {
                    ["path"] = path,
                    ["expectedMethods"] = supportedMethods
                });
        }

        var allowedMethods = allowHeaderValues
            .SelectMany(v => v.Split(','))
            .Select(m => m.Trim().ToUpperInvariant())
            .ToHashSet();

        var expectedMethods = supportedMethods.Select(m => m.ToUpperInvariant()).ToHashSet();
        
        if (!expectedMethods.Add("OPTIONS"))
            expectedMethods.Add("OPTIONS");

        var missingMethods = expectedMethods.Except(allowedMethods).ToList();
        var extraMethods = allowedMethods.Except(expectedMethods).ToList();

        if (missingMethods.Any() || extraMethods.Any())
        {
            var details = new List<string>();
            if (missingMethods.Any())
                details.Add($"Missing methods: {string.Join(", ", missingMethods)}");
            if (extraMethods.Any())
                details.Add($"Extra methods: {string.Join(", ", extraMethods)}");

            return PropertyResult.Failure(
                "Allow header does not accurately reflect supported methods",
                string.Join("; ", details),
                new Dictionary<string, object>
                {
                    ["path"] = path,
                    ["expectedMethods"] = expectedMethods.ToList(),
                    ["actualMethods"] = allowedMethods.ToList(),
                    ["missingMethods"] = missingMethods,
                    ["extraMethods"] = extraMethods
                });
        }

        return PropertyResult.Success(
            "Allow header accurately lists supported methods",
            new Dictionary<string, object>
            {
                ["path"] = path,
                ["allowedMethods"] = allowedMethods.ToList()
            });
    }

    private List<string> GetSupportedMethodsForPath(string path)
    {
        return _endpoints
            .Where(e => PathMatches(e.Path, path))
            .Select(e => e.Method)
            .Distinct()
            .ToList();
    }

    private static bool PathMatches(string pattern, string actualPath)
    {
        if (pattern == actualPath)
            return true;

        var patternParts = pattern.Split('/');
        var pathParts = actualPath.Split('/');

        if (patternParts.Length != pathParts.Length)
            return false;

        for (int i = 0; i < patternParts.Length; i++)
        {
            var patternPart = patternParts[i];
            var pathPart = pathParts[i];

            if (patternPart.StartsWith('{') && patternPart.EndsWith('}'))
                continue; 

            if (patternPart != pathPart)
                return false;
        }

        return true;
    }
}

public class MethodNotAllowedProperty<TContext> : IHttpProperty<TContext> where TContext : DbContext
{
    private readonly List<EndpointInfo> _endpoints;
    private readonly ILogger<MethodNotAllowedProperty<TContext>> _logger;

    public string Name => "405 Method Not Allowed Allow Header";
    public string Description => "405 Method Not Allowed responses must include an Allow header";
    public string RfcReference => "RFC 7231 Section 6.5.5";

    public MethodNotAllowedProperty(List<EndpointInfo> endpoints, ILogger<MethodNotAllowedProperty<TContext>> logger)
    {
        _endpoints = endpoints;
        _logger = logger;
    }

    public async Task<PropertyResult> CheckAsync(
        HttpRequestMessage request, 
        HttpResponseMessage response, 
        TContext dbContext, 
        CancellationToken cancellationToken = default)
    {
        if (response.StatusCode != HttpStatusCode.MethodNotAllowed)
            return PropertyResult.Success("Property does not apply to non-405 responses");

        if (!response.Headers.TryGetValues("Allow", out var allowHeaderValues))
        {
            var path = request.RequestUri?.AbsolutePath ?? "";
            var supportedMethods = GetSupportedMethodsForPath(path);

            return PropertyResult.Failure(
                "405 Method Not Allowed response missing required Allow header",
                $"Path: {path}, Method: {request.Method}",
                new Dictionary<string, object>
                {
                    ["path"] = path,
                    ["method"] = request.Method.Method,
                    ["supportedMethods"] = supportedMethods
                });
        }

        var allowedMethods = allowHeaderValues
            .SelectMany(v => v.Split(','))
            .Select(m => m.Trim())
            .ToList();

        return PropertyResult.Success(
            "405 response includes Allow header",
            new Dictionary<string, object>
            {
                ["allowedMethods"] = allowedMethods,
                ["requestMethod"] = request.Method.Method
            });
    }

    private List<string> GetSupportedMethodsForPath(string path)
    {
        return _endpoints
            .Where(e => PathMatches(e.Path, path))
            .Select(e => e.Method)
            .Distinct()
            .ToList();
    }

    private static bool PathMatches(string pattern, string actualPath)
    {
        if (pattern == actualPath)
            return true;

        var patternParts = pattern.Split('/');
        var pathParts = actualPath.Split('/');

        if (patternParts.Length != pathParts.Length)
            return false;

        for (int i = 0; i < patternParts.Length; i++)
        {
            var patternPart = patternParts[i];
            var pathPart = pathParts[i];

            if (patternPart.StartsWith('{') && patternPart.EndsWith('}'))
                continue;

            if (patternPart != pathPart)
                return false;
        }

        return true;
    }
}