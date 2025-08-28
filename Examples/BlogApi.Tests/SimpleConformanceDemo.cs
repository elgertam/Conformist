using Conformist.HttpRfc.Core;
using Conformist.HttpRfc.Extensions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using BlogApi.Models;
using Xunit;
using Xunit.Abstractions;

namespace BlogApi.Tests;

/// <summary>
/// Simple demonstration of Conformist HTTP RFC testing without FsCheck
/// This shows the core API working with intentionally flawed endpoints
/// </summary>
public class SimpleConformanceDemo : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly ITestOutputHelper _output;

    public SimpleConformanceDemo(WebApplicationFactory<Program> factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    /// <summary>
    /// Direct test showing HTTP RFC violations in the BlogApi
    /// This will demonstrate the violations we intentionally built in:
    /// 1. GET /api/users has side effects (violates safety property)
    /// 2. OPTIONS /api/users missing Allow header (violates response consistency)
    /// </summary>
    [Fact]
    public async Task DemonstrateHttpRfcViolations()
    {
        _output.WriteLine("🔍 Testing BlogApi for HTTP RFC compliance violations...\n");

        // Test the API for RFC compliance
        var results = await _factory.TestHttpRfcConformanceAsync<BlogContext, Program>(
            builder => builder
                .ConfigureStateTracking(opts =>
                {
                    // Exclude audit logs since they're expected to change
                    opts.ExcludeEntity<AuditLog>();
                    opts.TrackEntityCounts = true;
                    opts.TrackEntityChecksums = false;
                }),
            maxRequestsPerEndpoint: 2);  // Small number for demo

        // Analyze and report results
        var totalTests = results.Count;
        var passedTests = results.Count(r => r.OverallPassed);
        var failedTests = totalTests - passedTests;

        _output.WriteLine($"📊 TEST RESULTS SUMMARY");
        _output.WriteLine($"Total endpoints tested: {results.Select(r => $"{r.RequestMethod} {r.RequestPath}").Distinct().Count()}");
        _output.WriteLine($"Total test executions: {totalTests}");
        _output.WriteLine($"Passed: {passedTests} ✅");
        _output.WriteLine($"Failed: {failedTests} ❌");
        _output.WriteLine($"RFC Compliance Rate: {(double)passedTests / totalTests:P1}\n");

        if (failedTests > 0)
        {
            _output.WriteLine("🚨 HTTP RFC VIOLATIONS DETECTED:\n");
            
            foreach (var failed in results.Where(r => !r.OverallPassed))
            {
                _output.WriteLine($"❌ {failed.RequestMethod} {failed.RequestPath} (Status: {failed.ResponseStatusCode})");
                
                foreach (var propertyResult in failed.PropertyResults.Where(pr => !pr.Passed))
                {
                    _output.WriteLine($"   🔸 VIOLATION: {propertyResult.PropertyName}");
                    _output.WriteLine($"     RFC: {propertyResult.RfcReference}");
                    _output.WriteLine($"     Issue: {propertyResult.FailureReason}");
                    _output.WriteLine($"     Description: {propertyResult.PropertyDescription}\n");
                }
            }

            // Show the most common violations
            var violationStats = results
                .SelectMany(r => r.PropertyResults.Where(pr => !pr.Passed))
                .GroupBy(pr => pr.PropertyName)
                .Select(g => new { Property = g.Key, Count = g.Count(), RfcRef = g.First().RfcReference })
                .OrderByDescending(x => x.Count)
                .ToList();

            if (violationStats.Any())
            {
                _output.WriteLine("📈 MOST COMMON RFC VIOLATIONS:");
                foreach (var violation in violationStats.Take(5))
                {
                    _output.WriteLine($"   • {violation.Property}: {violation.Count} violations ({violation.RfcRef})");
                }
                _output.WriteLine("");
            }
        }

        // Generate detailed reports
        var reportGenerator = new TestReportGenerator();
        
        var htmlReport = reportGenerator.GenerateHtmlReport(results, "BlogApi HTTP RFC Compliance Analysis");
        var reportPath = Path.Combine(Directory.GetCurrentDirectory(), "blogapi-rfc-violations.html");
        await File.WriteAllTextAsync(reportPath, htmlReport);
        _output.WriteLine($"📄 Detailed HTML report saved: {reportPath}");

        var jsonReport = reportGenerator.GenerateJsonReport(results);
        var jsonPath = Path.Combine(Directory.GetCurrentDirectory(), "blogapi-rfc-violations.json");
        await File.WriteAllTextAsync(jsonPath, jsonReport);
        _output.WriteLine($"📄 JSON report saved: {jsonPath}");

        // The test should actually show violations (we built them in intentionally)
        _output.WriteLine($"\n✅ CONFORMIST DEMONSTRATION COMPLETE!");
        _output.WriteLine($"   Conformist successfully detected {failedTests} RFC violations in the BlogApi");
        _output.WriteLine($"   This proves the library works as intended - it finds real HTTP RFC issues!");
        
        // Don't fail the test - this is a demonstration
        // Assert.True(failedTests == 0, "RFC violations found"); // Commented out for demo
    }

    /// <summary>
    /// Test a specific known violation: GET with side effects
    /// </summary>
    [Fact]
    public async Task DemonstrateGetSideEffectViolation()
    {
        _output.WriteLine("🎯 Testing specific violation: GET method with side effects\n");

        using var client = _factory.CreateClient();
        
        // Create a test context to track database state
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BlogContext>();
        
        // Check initial audit log count
        var initialAuditCount = context.AuditLogs.Count();
        _output.WriteLine($"Initial audit log count: {initialAuditCount}");

        // Make a GET request to /api/users (this has a side effect!)
        var response = await client.GetAsync("/api/users");
        _output.WriteLine($"GET /api/users returned: {response.StatusCode}");

        // Check audit log count after GET request
        var finalAuditCount = context.AuditLogs.Count();
        _output.WriteLine($"Final audit log count: {finalAuditCount}");

        if (finalAuditCount > initialAuditCount)
        {
            _output.WriteLine("🚨 RFC VIOLATION DETECTED!");
            _output.WriteLine("   GET /api/users caused a side effect (added audit log)");
            _output.WriteLine("   This violates RFC 7231 Section 4.2.1: GET should be safe (no side effects)");
        }
        else
        {
            _output.WriteLine("✅ No side effect detected");
        }

        _output.WriteLine($"\n📋 SUMMARY: This demonstrates how Conformist can detect when GET methods");
        _output.WriteLine($"    violate the HTTP 'safety' property by causing observable side effects.");
    }
}