using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace Conformist.HttpRfc.Core;

public class PropertyBuilder<TContext> where TContext : DbContext
{
    private readonly List<string> _endpointPatterns = new();
    private readonly List<HttpMethod> _methods = new();
    private readonly List<Func<HttpRequestMessage, bool>> _predicates = new();
    private Func<HttpRequestMessage, HttpResponseMessage, TContext, Task<bool>>? _assertion;
    private string? _reason;
    private string? _name;

    public PropertyBuilder<TContext> ForEndpoint(string pattern)
    {
        _endpointPatterns.Add(pattern);
        return this;
    }

    public PropertyBuilder<TContext> WithMethod(HttpMethod method)
    {
        _methods.Add(method);
        return this;
    }

    public PropertyBuilder<TContext> When(Func<HttpRequestMessage, bool> predicate)
    {
        _predicates.Add(predicate);
        return this;
    }

    public PropertyBuilder<TContext> Should(Func<HttpRequestMessage, HttpResponseMessage, TContext, Task<bool>> assertion)
    {
        _assertion = assertion;
        return this;
    }

    public PropertyBuilder<TContext> Because(string reason)
    {
        _reason = reason;
        return this;
    }

    public PropertyBuilder<TContext> Named(string name)
    {
        _name = name;
        return this;
    }

    public CustomHttpProperty<TContext> Build()
    {
        if (_assertion == null)
            throw new InvalidOperationException("Property must have an assertion defined with Should()");

        return new CustomHttpProperty<TContext>(
            name: _name ?? "Custom Property",
            description: _reason ?? "Custom business rule",
            rfcReference: "Custom",
            endpointPatterns: _endpointPatterns,
            methods: _methods,
            predicates: _predicates,
            assertion: _assertion);
    }
}

public class CustomHttpProperty<TContext> : IHttpProperty<TContext> where TContext : DbContext
{
    private readonly List<string> _endpointPatterns;
    private readonly List<HttpMethod> _methods;
    private readonly List<Func<HttpRequestMessage, bool>> _predicates;
    private readonly Func<HttpRequestMessage, HttpResponseMessage, TContext, Task<bool>> _assertion;

    public string Name { get; }
    public string Description { get; }
    public string RfcReference { get; }

    public CustomHttpProperty(
        string name,
        string description,
        string rfcReference,
        List<string> endpointPatterns,
        List<HttpMethod> methods,
        List<Func<HttpRequestMessage, bool>> predicates,
        Func<HttpRequestMessage, HttpResponseMessage, TContext, Task<bool>> assertion)
    {
        Name = name;
        Description = description;
        RfcReference = rfcReference;
        _endpointPatterns = endpointPatterns;
        _methods = methods;
        _predicates = predicates;
        _assertion = assertion;
    }

    public async Task<PropertyResult> CheckAsync(
        HttpRequestMessage request, 
        HttpResponseMessage response, 
        TContext dbContext, 
        CancellationToken cancellationToken = default)
    {
        if (!AppliesToRequest(request))
            return PropertyResult.Success("Property does not apply to this request");

        try
        {
            var result = await _assertion(request, response, dbContext);
            return result 
                ? PropertyResult.Success("Custom property assertion passed")
                : PropertyResult.Failure("Custom property assertion failed", Description);
        }
        catch (Exception ex)
        {
            return PropertyResult.Failure($"Property check threw exception: {ex.Message}", ex.ToString());
        }
    }

    private bool AppliesToRequest(HttpRequestMessage request)
    {
        if (_methods.Any() && !_methods.Contains(request.Method))
            return false;

        if (_endpointPatterns.Any())
        {
            var path = request.RequestUri?.AbsolutePath ?? "";
            var matches = _endpointPatterns.Any(pattern => 
                Regex.IsMatch(path, ConvertPatternToRegex(pattern), RegexOptions.IgnoreCase));
            if (!matches)
                return false;
        }

        return _predicates.All(predicate => predicate(request));
    }

    private static string ConvertPatternToRegex(string pattern)
    {
        var regex = Regex.Escape(pattern)
            .Replace(@"\*", ".*")
            .Replace(@"\{[^}]+\}", @"[^/]+");
        return $"^{regex}$";
    }
}