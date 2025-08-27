using FsCheck;
using Conformist.HttpRfc.Core;
using Conformist.HttpRfc.StateTracking;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Conformist.HttpRfc.Extensions;

public static class TestFrameworkExtensions
{
    public static Property HttpRfcConformanceProperty<TContext, TProgram>(
        this WebApplicationFactory<TProgram> factory)
        where TContext : DbContext
        where TProgram : class
    {
        var tester = HttpRfcTester<TContext, TProgram>.ForApi(factory).BuildAsync().GetAwaiter().GetResult();
        return tester.CheckAllProperties();
    }

    public static Property HttpRfcConformanceProperty<TContext, TProgram>(
        this WebApplicationFactory<TProgram> factory,
        Action<HttpRfcTestBuilder<TContext, TProgram>> configure)
        where TContext : DbContext
        where TProgram : class
    {
        var builder = HttpRfcTester<TContext, TProgram>.ForApi(factory);
        configure(builder);
        var tester = builder.BuildAsync().GetAwaiter().GetResult();
        return tester.CheckAllProperties();
    }

    public static async Task<List<PropertyTestResult>> TestHttpRfcConformanceAsync<TContext, TProgram>(
        this WebApplicationFactory<TProgram> factory,
        int maxRequestsPerEndpoint = 5,
        CancellationToken cancellationToken = default)
        where TContext : DbContext
        where TProgram : class
    {
        var tester = await HttpRfcTester<TContext, TProgram>.ForApi(factory).BuildAsync(cancellationToken);
        return await tester.ExecuteAllEndpointTestsAsync(maxRequestsPerEndpoint, cancellationToken);
    }

    public static async Task<List<PropertyTestResult>> TestHttpRfcConformanceAsync<TContext, TProgram>(
        this WebApplicationFactory<TProgram> factory,
        Action<HttpRfcTestBuilder<TContext, TProgram>> configure,
        int maxRequestsPerEndpoint = 5,
        CancellationToken cancellationToken = default)
        where TContext : DbContext
        where TProgram : class
    {
        var builder = HttpRfcTester<TContext, TProgram>.ForApi(factory);
        configure(builder);
        var tester = await builder.BuildAsync(cancellationToken);
        return await tester.ExecuteAllEndpointTestsAsync(maxRequestsPerEndpoint, cancellationToken);
    }
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHttpRfcConformanceTesting(this IServiceCollection services)
    {
        services.AddScoped<PropertyEngine>();
        services.AddScoped<TestReportGenerator>();
        return services;
    }

    public static IServiceCollection AddHttpRfcConformanceTesting<TContext>(
        this IServiceCollection services,
        Action<HttpRfcConformanceOptions<TContext>>? configure = null)
        where TContext : DbContext
    {
        var options = new HttpRfcConformanceOptions<TContext>();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddScoped<PropertyEngine>();
        services.AddScoped<TestReportGenerator>();
        
        return services;
    }
}

public class HttpRfcConformanceOptions<TContext> where TContext : DbContext
{
    public StateTrackingOptions StateTracking { get; } = new();
    public List<string> ExcludeEndpoints { get; } = new();
    public List<string> IncludeOnlyEndpoints { get; } = new();
    public bool EnableDetailedLogging { get; set; } = false;
    public int DefaultMaxRequestsPerEndpoint { get; set; } = 5;
    
    public HttpRfcConformanceOptions<TContext> ConfigureStateTracking(Action<StateTrackingOptions> configure)
    {
        configure(StateTracking);
        return this;
    }

    public HttpRfcConformanceOptions<TContext> ExcludeEndpoint(string pattern)
    {
        ExcludeEndpoints.Add(pattern);
        return this;
    }

    public HttpRfcConformanceOptions<TContext> IncludeOnlyEndpoint(string pattern)
    {
        IncludeOnlyEndpoints.Add(pattern);
        return this;
    }
}