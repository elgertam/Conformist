using FsCheck;
using Conformist.HttpRfc.Core;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace Conformist.HttpRfc.Extensions;

public static class NUnitExtensions
{
    public static void ShouldConformToHttpRfc<TContext, TProgram>(
        this WebApplicationFactory<TProgram> factory)
        where TContext : DbContext
        where TProgram : class
    {
        var property = factory.HttpRfcConformanceProperty<TContext, TProgram>();
        
        var config = Configuration.QuickThrowOnFailure;
        config.MaxNbOfTest = 50;
        
        property.Check(config);
    }

    public static void ShouldConformToHttpRfc<TContext, TProgram>(
        this WebApplicationFactory<TProgram> factory,
        Action<HttpRfcTestBuilder<TContext, TProgram>> configure)
        where TContext : DbContext
        where TProgram : class
    {
        var property = factory.HttpRfcConformanceProperty(configure);
        
        var config = Configuration.QuickThrowOnFailure;
        config.MaxNbOfTest = 50;
        
        property.Check(config);
    }

    public static async Task ShouldConformToHttpRfcAsync<TContext, TProgram>(
        this WebApplicationFactory<TProgram> factory,
        int maxRequestsPerEndpoint = 5)
        where TContext : DbContext
        where TProgram : class
    {
        var results = await factory.TestHttpRfcConformanceAsync<TContext, TProgram>(maxRequestsPerEndpoint);
        
        var failedTests = results.Where(r => !r.OverallPassed).ToList();
        
        if (failedTests.Any())
        {
            var reportGenerator = new TestReportGenerator();
            var report = reportGenerator.GenerateMarkdownReport(failedTests, "HTTP RFC Conformance Failures");
            
            Assert.Fail($"HTTP RFC conformance violations detected:\n\n{report}");
        }
        
        Assert.Pass($"All {results.Count} HTTP RFC conformance tests passed across {results.Select(r => $"{r.RequestMethod} {r.RequestPath}").Distinct().Count()} unique endpoints.");
    }

    public static async Task ShouldConformToHttpRfcAsync<TContext, TProgram>(
        this WebApplicationFactory<TProgram> factory,
        Action<HttpRfcTestBuilder<TContext, TProgram>> configure,
        int maxRequestsPerEndpoint = 5)
        where TContext : DbContext
        where TProgram : class
    {
        var results = await factory.TestHttpRfcConformanceAsync(configure, maxRequestsPerEndpoint);
        
        var failedTests = results.Where(r => !r.OverallPassed).ToList();
        
        if (failedTests.Any())
        {
            var reportGenerator = new TestReportGenerator();
            var report = reportGenerator.GenerateMarkdownReport(failedTests, "HTTP RFC Conformance Failures");
            
            Assert.Fail($"HTTP RFC conformance violations detected:\n\n{report}");
        }
        
        Assert.Pass($"All {results.Count} HTTP RFC conformance tests passed across {results.Select(r => $"{r.RequestMethod} {r.RequestPath}").Distinct().Count()} unique endpoints.");
    }
}

[AttributeUsage(AttributeTargets.Method)]
public class HttpRfcTestAttribute : TestAttribute
{
    public HttpRfcTestAttribute()
    {
        Description = "HTTP RFC Conformance Test";
    }
}