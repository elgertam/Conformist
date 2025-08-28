using Conformist.HttpRfc.Discovery;
using Conformist.HttpRfc.Generators;
using Conformist.HttpRfc.Properties;
using Conformist.HttpRfc.StateTracking;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Conformist.HttpRfc.Core;

public class HttpRfcTestBuilder<TContext, TProgram> where TContext : DbContext where TProgram : class
{
    private readonly WebApplicationFactory<TProgram> _factory;
    private readonly StateTrackingOptions _stateTrackingOptions = new();
    private readonly List<string> _excludeEndpointPatterns = new();
    private readonly List<string> _includeOnlyEndpointPatterns = new();
    private readonly List<IHttpProperty<TContext>> _customProperties = new();
    private readonly List<Func<PropertyBuilder<TContext>, CustomHttpProperty<TContext>>> _businessRules = new();
    private readonly List<Type> _excludedBuiltInProperties = new();

    public HttpRfcTestBuilder(WebApplicationFactory<TProgram> factory)
    {
        _factory = factory;
    }

    public HttpRfcTestBuilder<TContext, TProgram> ConfigureStateTracking(Action<StateTrackingOptions> configure)
    {
        configure(_stateTrackingOptions);
        return this;
    }

    public HttpRfcTestBuilder<TContext, TProgram> ExcludeEndpoints(params string[] patterns)
    {
        _excludeEndpointPatterns.AddRange(patterns);
        return this;
    }

    public HttpRfcTestBuilder<TContext, TProgram> IncludeOnlyEndpoints(params string[] patterns)
    {
        _includeOnlyEndpointPatterns.AddRange(patterns);
        return this;
    }

    public HttpRfcTestBuilder<TContext, TProgram> AddCustomProperty<TProperty>() where TProperty : class, IHttpProperty<TContext>
    {
        var serviceProvider = _factory.Services;
        var property = ActivatorUtilities.CreateInstance<TProperty>(serviceProvider);
        _customProperties.Add(property);
        return this;
    }

    public HttpRfcTestBuilder<TContext, TProgram> AddCustomProperty(IHttpProperty<TContext> property)
    {
        _customProperties.Add(property);
        return this;
    }

    public HttpRfcTestBuilder<TContext, TProgram> DefineBusinessRule(Func<PropertyBuilder<TContext>, CustomHttpProperty<TContext>> configure)
    {
        _businessRules.Add(configure);
        return this;
    }

    public HttpRfcTestBuilder<TContext, TProgram> ExcludeBuiltInProperty<TProperty>() where TProperty : IHttpProperty<TContext>
    {
        _excludedBuiltInProperties.Add(typeof(TProperty));
        return this;
    }

    public HttpRfcTestBuilder<TContext, TProgram> ExcludeAllSafetyProperties()
    {
        _excludedBuiltInProperties.AddRange(new[]
        {
            typeof(GetMethodSafetyProperty<TContext>),
            typeof(HeadMethodSafetyProperty<TContext>),
            typeof(OptionsMethodSafetyProperty<TContext>)
        });
        return this;
    }

    public HttpRfcTestBuilder<TContext, TProgram> ExcludeAllIdempotencyProperties()
    {
        _excludedBuiltInProperties.AddRange(new[]
        {
            typeof(PutMethodIdempotencyProperty<TContext, TProgram>),
            typeof(DeleteMethodIdempotencyProperty<TContext, TProgram>)
        });
        return this;
    }

    public HttpRfcTestBuilder<TContext, TProgram> ExcludeAllResponseConsistencyProperties()
    {
        _excludedBuiltInProperties.AddRange(new[]
        {
            typeof(HeadGetConsistencyProperty<TContext, TProgram>),
            typeof(OptionsAllowHeaderProperty<TContext>),
            typeof(MethodNotAllowedProperty<TContext>)
        });
        return this;
    }

    public async Task<HttpRfcTester<TContext, TProgram>> BuildAsync(CancellationToken cancellationToken = default)
    {
        var serviceProvider = _factory.Services;
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

        var swaggerDiscovery = new SwaggerEndpointDiscovery(
            loggerFactory.CreateLogger<SwaggerEndpointDiscovery>());

        var endpoints = await swaggerDiscovery.DiscoverEndpointsAsync(_factory, cancellationToken: cancellationToken);

        if (_includeOnlyEndpointPatterns.Any())
        {
            endpoints = endpoints.Where(e => _includeOnlyEndpointPatterns.Any(pattern => 
                System.Text.RegularExpressions.Regex.IsMatch(e.Path, ConvertPatternToRegex(pattern)))).ToList();
        }

        if (_excludeEndpointPatterns.Any())
        {
            endpoints = endpoints.Where(e => !_excludeEndpointPatterns.Any(pattern => 
                System.Text.RegularExpressions.Regex.IsMatch(e.Path, ConvertPatternToRegex(pattern)))).ToList();
        }

        var properties = new List<IHttpProperty<TContext>>();

        properties.AddRange(CreateBuiltInProperties(serviceProvider, loggerFactory, endpoints));

        properties.AddRange(_customProperties);

        foreach (var businessRule in _businessRules)
        {
            var builder = new PropertyBuilder<TContext>();
            var property = businessRule(builder);
            properties.Add(property);
        }

        var propertyEngine = new PropertyEngine(loggerFactory.CreateLogger<PropertyEngine>());
        var testDataGenerator = new TestDataGenerator(loggerFactory.CreateLogger<TestDataGenerator>());

        return new HttpRfcTester<TContext, TProgram>(
            _factory,
            endpoints,
            properties,
            propertyEngine,
            testDataGenerator,
            _stateTrackingOptions,
            loggerFactory.CreateLogger<HttpRfcTester<TContext, TProgram>>());
    }

    private List<IHttpProperty<TContext>> CreateBuiltInProperties(
        IServiceProvider serviceProvider,
        ILoggerFactory loggerFactory,
        List<EndpointInfo> endpoints)
    {
        var properties = new List<IHttpProperty<TContext>>();

        if (!IsExcluded<GetMethodSafetyProperty<TContext>>())
        {
            var stateTracker = CreateStateTracker(serviceProvider, loggerFactory);
            properties.Add(new GetMethodSafetyProperty<TContext>(
                stateTracker, 
                loggerFactory.CreateLogger<GetMethodSafetyProperty<TContext>>()));
        }

        if (!IsExcluded<HeadMethodSafetyProperty<TContext>>())
        {
            var stateTracker = CreateStateTracker(serviceProvider, loggerFactory);
            properties.Add(new HeadMethodSafetyProperty<TContext>(
                stateTracker, 
                loggerFactory.CreateLogger<HeadMethodSafetyProperty<TContext>>()));
        }

        if (!IsExcluded<OptionsMethodSafetyProperty<TContext>>())
        {
            var stateTracker = CreateStateTracker(serviceProvider, loggerFactory);
            properties.Add(new OptionsMethodSafetyProperty<TContext>(
                stateTracker, 
                loggerFactory.CreateLogger<OptionsMethodSafetyProperty<TContext>>()));
        }

        if (!IsExcluded<PutMethodIdempotencyProperty<TContext, TProgram>>())
        {
            var stateTracker = CreateStateTracker(serviceProvider, loggerFactory);
            properties.Add(new PutMethodIdempotencyProperty<TContext, TProgram>(
                _factory,
                stateTracker, 
                loggerFactory.CreateLogger<PutMethodIdempotencyProperty<TContext, TProgram>>()));
        }

        if (!IsExcluded<DeleteMethodIdempotencyProperty<TContext, TProgram>>())
        {
            var stateTracker = CreateStateTracker(serviceProvider, loggerFactory);
            properties.Add(new DeleteMethodIdempotencyProperty<TContext, TProgram>(
                _factory,
                stateTracker, 
                loggerFactory.CreateLogger<DeleteMethodIdempotencyProperty<TContext, TProgram>>()));
        }

        if (!IsExcluded<HeadGetConsistencyProperty<TContext, TProgram>>())
        {
            properties.Add(new HeadGetConsistencyProperty<TContext, TProgram>(
                _factory,
                loggerFactory.CreateLogger<HeadGetConsistencyProperty<TContext, TProgram>>()));
        }

        if (!IsExcluded<OptionsAllowHeaderProperty<TContext>>())
        {
            properties.Add(new OptionsAllowHeaderProperty<TContext>(
                endpoints,
                loggerFactory.CreateLogger<OptionsAllowHeaderProperty<TContext>>()));
        }

        if (!IsExcluded<MethodNotAllowedProperty<TContext>>())
        {
            properties.Add(new MethodNotAllowedProperty<TContext>(
                endpoints,
                loggerFactory.CreateLogger<MethodNotAllowedProperty<TContext>>()));
        }

        return properties;
    }

    private EfCoreStateTracker<TContext> CreateStateTracker(IServiceProvider serviceProvider, ILoggerFactory loggerFactory)
    {
        return new EfCoreStateTracker<TContext>(
            serviceProvider, 
            _stateTrackingOptions, 
            loggerFactory.CreateLogger<EfCoreStateTracker<TContext>>());
    }

    private bool IsExcluded<T>() => _excludedBuiltInProperties.Contains(typeof(T));

    private static string ConvertPatternToRegex(string pattern)
    {
        var regex = System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace(@"\*", ".*")
            .Replace(@"\{[^}]+\}", @"[^/]+");
        return $"^{regex}$";
    }
}