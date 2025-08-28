# Conformist - HTTP RFC Compliance Testing Library

[![NuGet](https://img.shields.io/nuget/v/Conformist.HttpRfc.svg)](https://www.nuget.org/packages/Conformist.HttpRfc/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A comprehensive C# library for property-based testing of WebAPI endpoints for HTTP RFC compliance. Conformist automatically discovers your API endpoints and validates them against HTTP RFC standards, ensuring your APIs behave correctly according to web standards.

## Features

- 🔍 **Automatic Endpoint Discovery** - Parses OpenAPI/Swagger documentation to discover endpoints
- 🧪 **Property-Based Testing** - Uses FsCheck for comprehensive test coverage
- 🗄️ **EF Core State Tracking** - Monitors database changes using metaprogramming techniques
- 📊 **Comprehensive Reports** - Generate HTML, Markdown, and JSON reports
- 🛠️ **Extensible Architecture** - Add custom business rules and properties
- 📚 **RFC Compliance** - Implements safety, idempotency, and consistency properties from HTTP RFCs

## Quick Start

### Installation

```bash
dotnet add package Conformist.HttpRfc
```

### Basic Usage

```csharp
using Conformist.HttpRfc.Extensions;
using Microsoft.AspNetCore.Mvc.Testing;

[Test]
public async Task TestApiForHttpRfcCompliance()
{
    using var factory = new WebApplicationFactory<Program>();
    
    var results = await factory.TestHttpRfcConformanceAsync<MyDbContext, Program>();
    
    var failedTests = results.Where(r => !r.OverallPassed);
    Assert.That(failedTests, Is.Empty, "HTTP RFC violations detected");
}
```

### Console Application

```csharp
using Conformist.HttpRfc.Extensions;
using Microsoft.AspNetCore.Mvc.Testing;

using var factory = new WebApplicationFactory<Program>();

var results = await factory.TestHttpRfcConformanceAsync<MyDbContext, Program>(
    builder => builder
        .ConfigureStateTracking(opts => 
        {
            opts.ExcludeEntity<AuditLog>();
            opts.TrackEntityCounts = true;
        }),
    maxRequestsPerEndpoint: 3);

foreach (var failed in results.Where(r => !r.OverallPassed))
{
    Console.WriteLine($"❌ {failed.RequestMethod} {failed.RequestPath}");
    
    foreach (var violation in failed.PropertyResults.Where(pr => !pr.Passed))
    {
        Console.WriteLine($"   🔸 {violation.PropertyName}: {violation.FailureReason}");
        Console.WriteLine($"     RFC: {violation.RfcReference}");
    }
}
```

## Core HTTP RFC Properties

### Safety Properties (RFC 7231 Section 4.2.1)
- **GET Method Safety** - GET requests must not cause side effects
- **HEAD Method Safety** - HEAD requests must not cause side effects  
- **OPTIONS Method Safety** - OPTIONS requests must not cause side effects

### Idempotency Properties (RFC 7231 Section 4.2.2)
- **PUT Method Idempotency** - Multiple identical PUT requests have same effect
- **DELETE Method Idempotency** - Multiple identical DELETE requests have same effect

### Response Consistency Properties
- **HEAD-GET Consistency** - HEAD returns same headers as GET without body
- **OPTIONS Allow Header** - OPTIONS returns accurate Allow header
- **405 Method Not Allowed** - Includes required Allow header

## Configuration Options

### State Tracking Configuration

```csharp
.ConfigureStateTracking(opts =>
{
    // Control what gets tracked
    opts.TrackEntityCounts = true;
    opts.TrackEntityChecksums = false; // For better performance
    opts.TrackChangeTrackerState = true;
    
    // Exclude entities from tracking
    opts.ExcludeEntity<AuditLog>();
    opts.ExcludeEntity("SystemLog");
    
    // Performance tuning
    opts.MaxParallelism = Environment.ProcessorCount;
    opts.UseCompiledQueries = true;
    opts.QueryTimeout = TimeSpan.FromSeconds(30);
})
```

### Custom Business Rules

```csharp
.DefineBusinessRule(rule => rule
    .Named("Authentication Required for User Resources")
    .ForEndpoint("/users/*")
    .WithMethod(HttpMethod.Post)
    .When(req => req.Headers.Authorization == null)
    .Should(async (req, resp, db) => resp.StatusCode == HttpStatusCode.Unauthorized)
    .Because("Unauthenticated requests to user resources should return 401"))
```

### Endpoint Filtering

```csharp
// Include only specific endpoints
.IncludeOnlyEndpoints("/api/users/*", "/api/posts/*")

// Exclude specific endpoints  
.ExcludeEndpoints("/health", "/metrics", "/swagger/*")

// Exclude specific properties
.ExcludeAllSafetyProperties()
.ExcludeBuiltInProperty<PutMethodIdempotencyProperty<MyContext>>()
```

## Test Framework Integration

### xUnit Integration

```csharp
public class HttpRfcTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HttpRfcTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [HttpRfcProperty]
    public Property BasicHttpCompliance()
    {
        return _factory.HttpRfcConformanceProperty<BlogContext, Program>();
    }

    [HttpRfcProperty]
    public Property CustomBusinessRules()
    {
        return _factory.HttpRfcConformanceProperty<BlogContext, Program>(builder => 
            builder.DefineBusinessRule(rule => rule
                .ForEndpoint("/posts/{id}")
                .WithMethod(HttpMethod.Delete)
                .Should(async (req, resp, db) => 
                {
                    var id = ExtractIdFromPath(req.RequestUri.AbsolutePath);
                    return await db.Posts.FindAsync(id) == null;
                })
                .Because("Deleted posts should not exist in database")));
    }

    [HttpRfcFact]
    public async Task ApiShouldConformToHttpRfc()
    {
        await _factory.ShouldConformToHttpRfcAsync<BlogContext, Program>();
    }
}
```

### NUnit Integration

```csharp
[TestFixture]
public class HttpRfcTests
{
    private WebApplicationFactory<Program> _factory;

    [SetUp]
    public void SetUp()
    {
        _factory = new WebApplicationFactory<Program>();
    }

    [HttpRfcTest]
    public async Task ApiShouldConformToHttpRfc()
    {
        await _factory.ShouldConformToHttpRfcAsync<BlogContext, Program>(builder =>
            builder.ConfigureStateTracking(opts => opts.ExcludeEntity<AuditLog>()));
    }

    [TearDown]
    public void TearDown()
    {
        _factory?.Dispose();
    }
}
```

## Report Generation

### Generate Reports Programmatically

```csharp
var results = await _factory.TestHttpRfcConformanceAsync<BlogContext, Program>();
var reportGenerator = new TestReportGenerator();

// Generate HTML report
var htmlReport = reportGenerator.GenerateHtmlReport(results, "Blog API RFC Compliance Report");
await File.WriteAllTextAsync("rfc-report.html", htmlReport);

// Generate Markdown report  
var markdownReport = reportGenerator.GenerateMarkdownReport(results);
await File.WriteAllTextAsync("rfc-report.md", markdownReport);

// Generate JSON report
var jsonReport = reportGenerator.GenerateJsonReport(results);
await File.WriteAllTextAsync("rfc-report.json", jsonReport);
```

### Sample Report Output

```
# HTTP RFC Conformance Test Report

**Generated:** 2024-01-15 10:30:00 UTC

## Summary

| Metric | Value |
|--------|-------|
| **Overall Pass Rate** | 95.2% |
| **Total Tests** | 127 |
| **Passed Tests** | 121 ✅ |
| **Failed Tests** | 6 ❌ |
| **Unique Endpoints** | 23 |
| **Total Properties Checked** | 381 |
| **Property Pass Rate** | 98.4% |
| **Average Response Time** | 45.67ms |

## ❌ Failed Tests

### Most Common Failures

| Property | RFC Reference | Failures | Description |
|----------|---------------|----------|-------------|
| **GET Method Safety** | RFC 7231 Section 4.2.1 | 3 | GET requests must not cause observable side effects |
| **OPTIONS Allow Header** | RFC 7231 Section 4.3.7 | 2 | OPTIONS responses must include accurate Allow header |
```

## Advanced Features

### Custom Properties

```csharp
public class AuthenticationRequiredProperty<TContext> : IHttpProperty<TContext> 
    where TContext : DbContext
{
    public string Name => "Authentication Required";
    public string Description => "Protected endpoints require authentication";
    public string RfcReference => "RFC 7235 Section 3.1";

    public async Task<PropertyResult> CheckAsync(
        HttpRequestMessage request, 
        HttpResponseMessage response, 
        TContext dbContext, 
        CancellationToken cancellationToken = default)
    {
        if (IsProtectedEndpoint(request.RequestUri.AbsolutePath))
        {
            if (request.Headers.Authorization == null && response.StatusCode != HttpStatusCode.Unauthorized)
            {
                return PropertyResult.Failure("Protected endpoint should return 401 when unauthenticated");
            }
        }
        
        return PropertyResult.Success();
    }
    
    private bool IsProtectedEndpoint(string path) => path.StartsWith("/api/admin/");
}

// Register custom property
var tester = HttpRfcTester
    .ForApi<MyContext>(_factory)
    .AddCustomProperty<AuthenticationRequiredProperty<MyContext>>()
    .BuildAsync()
    .Result;
```

### Service Registration

```csharp
// In Startup.cs or Program.cs
services.AddHttpRfcConformanceTesting<BlogContext>(options =>
{
    options.ConfigureStateTracking(tracking => 
    {
        tracking.ExcludeEntity<AuditLog>();
        tracking.TrackEntityChecksums = true;
    });
    options.ExcludeEndpoint("/health");
    options.DefaultMaxRequestsPerEndpoint = 10;
});
```

## Performance Considerations

- **Compiled Queries**: Enabled by default for faster database state tracking
- **Parallel Execution**: State tracking runs in parallel across entities
- **Selective Tracking**: Exclude audit tables and logs from state tracking
- **Smart Caching**: OpenAPI metadata and compiled expressions are cached

## Best Practices

1. **Exclude Non-Business Entities**: Exclude audit logs, system logs, and temporary tables from state tracking
2. **Use Realistic Test Data**: The library generates realistic test data based on your schema constraints
3. **Custom Business Rules**: Add domain-specific validation rules for your API
4. **Performance Testing**: Use the library's performance metrics to identify slow endpoints
5. **Continuous Integration**: Run RFC conformance tests in your CI pipeline

## Troubleshooting

### Common Issues

**Problem**: Tests fail with "Could not find DbSet property for entity"
**Solution**: Ensure your DbContext exposes DbSet properties for all tracked entities

**Problem**: State tracking shows false positives for changes
**Solution**: Exclude entities that change due to triggers, audit systems, or background processes

**Problem**: Tests are slow
**Solution**: Disable checksum tracking and reduce MaxRequestsPerEndpoint for CI environments

**Problem**: Swagger discovery fails
**Solution**: Ensure your API includes Swashbuckle and exposes swagger.json at the default path

## Contributing

We welcome contributions! Please see our [Contributing Guide](CONTRIBUTING.md) for details.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Related Projects

- [FsCheck](https://github.com/fscheck/FsCheck) - Property-based testing framework
- [ASP.NET Core Testing](https://docs.microsoft.com/en-us/aspnet/core/test/) - Official testing documentation
- [HTTP RFCs](https://tools.ietf.org/rfc/) - Official HTTP specifications