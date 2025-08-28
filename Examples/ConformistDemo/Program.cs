extern alias BlogApi;
using Conformist.HttpRfc.Extensions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using BlogApi::BlogApi.Models;

// Create the web application factory using BlogApi's Program class
using var factory = new WebApplicationFactory<BlogApi::Program>().WithWebHostBuilder(builder =>
{
    var contentRoot = Path.Combine(Directory.GetCurrentDirectory(), "examples", "BlogApi");
    builder.UseContentRoot(contentRoot);
});

Console.WriteLine("🚀 Conformist HTTP RFC Compliance Testing Demo");
Console.WriteLine("==============================================\n");

Console.WriteLine("Testing BlogApi for HTTP RFC compliance violations...\n");

try
{
    // Test the API for RFC compliance
    Console.WriteLine("🔍 Running HTTP RFC conformance tests...");
    var results = await factory.TestHttpRfcConformanceAsync<BlogContext, BlogApi::Program>(
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
    
    Console.WriteLine($"\n📊 TEST RESULTS SUMMARY");
    Console.WriteLine($"========================");
    Console.WriteLine($"Total endpoints tested: {results.Select(r => $"{r.RequestMethod} {r.RequestPath}").Distinct().Count()}");
    Console.WriteLine($"Total test executions: {totalTests}");
    Console.WriteLine($"Passed: {passedTests} ✅");
    Console.WriteLine($"Failed: {failedTests} ❌");
    Console.WriteLine($"RFC Compliance Rate: {(double)passedTests / totalTests:P1}\n");

    if (failedTests > 0)
    {
        Console.WriteLine("🚨 HTTP RFC VIOLATIONS DETECTED:");
        Console.WriteLine("=================================\n");
        
        foreach (var failed in results.Where(r => !r.OverallPassed).Take(3)) // Show first 3 failures
        {
            Console.WriteLine($"❌ {failed.RequestMethod} {failed.RequestPath} (Status: {failed.ResponseStatusCode})");
            
            foreach (var propertyResult in failed.PropertyResults.Where(pr => !pr.Passed))
            {
                Console.WriteLine($"   🔸 VIOLATION: {propertyResult.PropertyName}");
                Console.WriteLine($"     RFC: {propertyResult.RfcReference}");
                Console.WriteLine($"     Issue: {propertyResult.FailureReason}");
                Console.WriteLine($"     Description: {propertyResult.PropertyDescription}\n");
            }
        }

        if (results.Where(r => !r.OverallPassed).Count() > 3)
        {
            Console.WriteLine($"... and {results.Where(r => !r.OverallPassed).Count() - 3} more violations\n");
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
            Console.WriteLine("📈 MOST COMMON RFC VIOLATIONS:");
            Console.WriteLine("==============================");
            foreach (var violation in violationStats.Take(3))
            {
                Console.WriteLine($"   • {violation.Property}: {violation.Count} violations ({violation.RfcRef})");
            }
            Console.WriteLine();
        }
    }
    else
    {
        Console.WriteLine("✅ No RFC violations detected - API is compliant!\n");
    }

    Console.WriteLine("✅ CONFORMIST DEMONSTRATION COMPLETE!");
    Console.WriteLine($"   Conformist successfully analyzed your API and found {failedTests} RFC compliance issues.");
    Console.WriteLine($"   This demonstrates the library's ability to automatically detect HTTP RFC violations!");
    
    if (failedTests > 0)
    {
        Console.WriteLine("\n💡 TIP: The violations detected are intentionally built into the BlogApi for demonstration.");
        Console.WriteLine("   In a real application, you would fix these issues to achieve RFC compliance.");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error running conformance tests: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex}");
    Environment.Exit(1);
}

Console.WriteLine("\n🎯 Example violations in BlogApi:");
Console.WriteLine("  • GET /api/users causes side effects (violates safety property)");
Console.WriteLine("  • OPTIONS /api/users missing Allow header (violates response consistency)");
Console.WriteLine("\nPress any key to exit...");
Console.ReadKey();