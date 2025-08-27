using FsCheck;
using FsCheck.Xunit;
using Conformist.HttpRfc.Core;
using Conformist.HttpRfc.Extensions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Conformist.HttpRfc.Examples;

// Example entities for demonstration
public class BlogContext : DbContext
{
    public BlogContext(DbContextOptions<BlogContext> options) : base(options) { }
    
    public DbSet<Post> Posts { get; set; } = null!;
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Comment> Comments { get; set; } = null!;
    public DbSet<AuditLog> AuditLogs { get; set; } = null!;
}

public class Post
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public int AuthorId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public User Author { get; set; } = null!;
    public List<Comment> Comments { get; set; } = new();
}

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = "";
    public string Name { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public List<Post> Posts { get; set; } = new();
    public List<Comment> Comments { get; set; } = new();
}

public class Comment
{
    public int Id { get; set; }
    public string Content { get; set; } = "";
    public int PostId { get; set; }
    public int AuthorId { get; set; }
    public DateTime CreatedAt { get; set; }
    public Post Post { get; set; } = null!;
    public User Author { get; set; } = null!;
}

public class AuditLog
{
    public int Id { get; set; }
    public string Action { get; set; } = "";
    public string EntityType { get; set; } = "";
    public int EntityId { get; set; }
    public DateTime Timestamp { get; set; }
    public string UserId { get; set; } = "";
}

// Mock Program class for example purposes
// In real usage, replace this with your actual web application's Program class
public class Program
{
    public static void Main(string[] args) { }
}

/// <summary>
/// Simple example showing how to use the Conformist.HttpRfc library
/// 
/// IMPORTANT: In real usage:
/// 1. Replace 'Program' with your actual web application's Program class
/// 2. Replace 'BlogContext' with your actual DbContext
/// 3. Configure your actual web application factory
/// </summary>
public class SimpleHttpRfcTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SimpleHttpRfcTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Basic RFC compliance test - tests all endpoints against all built-in properties
    /// This is a placeholder example - it won't actually test anything because
    /// the mock Program class doesn't configure any endpoints
    /// </summary>
    [HttpRfcProperty]
    public Property BasicRfcCompliance()
    {
        return HttpRfcTester<BlogContext, Program>
            .ForApi(_factory)
            .BuildAsync()
            .Result
            .CheckAllProperties();
    }

    /// <summary>
    /// Test with state tracking to monitor database changes
    /// </summary>
    [HttpRfcProperty]
    public Property RfcComplianceWithStateTracking()
    {
        return HttpRfcTester<BlogContext, Program>
            .ForApi(_factory)
            .ConfigureStateTracking(opts =>
            {
                // Exclude entities that change on every operation (like audit logs)
                opts.ExcludeEntity<AuditLog>();
            })
            .BuildAsync()
            .Result
            .CheckAllProperties();
    }

    /// <summary>
    /// Test only specific endpoints
    /// </summary>
    [HttpRfcProperty]
    public Property SpecificEndpointsOnly()
    {
        return HttpRfcTester<BlogContext, Program>
            .ForApi(_factory)
            .IncludeOnlyEndpoints("/api/posts/*")
            .ConfigureStateTracking(opts => opts.ExcludeEntity<AuditLog>())
            .BuildAsync()
            .Result
            .CheckAllProperties();
    }

    /// <summary>
    /// Async test that generates a detailed report
    /// </summary>
    [HttpRfcFact]
    public async Task ComprehensiveRfcComplianceTest()
    {
        var results = await _factory.TestHttpRfcConformanceAsync<BlogContext, Program>(
            builder => builder
                .ConfigureStateTracking(opts => opts.ExcludeEntity<AuditLog>())
                .IncludeOnlyEndpoints("/api/*"),
            maxRequestsPerEndpoint: 5);

        // Generate HTML report
        var reportGenerator = new TestReportGenerator();
        var htmlReport = reportGenerator.GenerateHtmlReport(results, "API RFC Compliance Report");
        await File.WriteAllTextAsync("rfc-compliance-report.html", htmlReport);

        // Assert that tests passed
        var failedTests = results.Where(r => !r.OverallPassed).ToList();
        Assert.True(!failedTests.Any(), 
            $"RFC compliance violations found. Failed: {failedTests.Count}/{results.Count} tests");
    }
}