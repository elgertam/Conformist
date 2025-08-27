using System.Text;
using System.Text.Json;

namespace Conformist.HttpRfc.Core;

public class TestReportGenerator
{
    public string GenerateMarkdownReport(List<PropertyTestResult> results, string title = "HTTP RFC Conformance Test Report")
    {
        var sb = new StringBuilder();
        
        sb.AppendLine($"# {title}");
        sb.AppendLine();
        sb.AppendLine($"**Generated:** {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();

        AddSummarySection(sb, results);
        AddFailuresSummary(sb, results);
        AddDetailedResults(sb, results);
        AddPerformanceMetrics(sb, results);

        return sb.ToString();
    }

    public string GenerateJsonReport(List<PropertyTestResult> results, string title = "HTTP RFC Conformance Test Report")
    {
        var report = new
        {
            Title = title,
            GeneratedAt = DateTime.UtcNow,
            Summary = GenerateSummary(results),
            Results = results.Select(r => new
            {
                r.RequestMethod,
                r.RequestPath,
                r.ResponseStatusCode,
                r.OverallPassed,
                r.TotalProperties,
                r.PassedProperties,
                r.FailedProperties,
                ExecutionTimeMs = r.TotalExecutionTime.TotalMilliseconds,
                PropertyResults = r.PropertyResults.Select(pr => new
                {
                    pr.PropertyName,
                    pr.PropertyDescription,
                    pr.RfcReference,
                    pr.Passed,
                    pr.FailureReason,
                    pr.Details,
                    ExecutionTimeMs = pr.ExecutionTime.TotalMilliseconds,
                    pr.Metrics
                }).ToArray()
            }).ToArray()
        };

        return JsonSerializer.Serialize(report, new JsonSerializerOptions 
        { 
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    public string GenerateHtmlReport(List<PropertyTestResult> results, string title = "HTTP RFC Conformance Test Report")
    {
        var summary = GenerateSummary(results);
        var failedResults = results.Where(r => !r.OverallPassed).ToList();

        var html = $@"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>{title}</title>
    <style>
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; margin: 40px; line-height: 1.6; }}
        .header {{ border-bottom: 2px solid #eee; padding-bottom: 20px; margin-bottom: 30px; }}
        .summary {{ background: #f8f9fa; padding: 20px; border-radius: 8px; margin: 20px 0; }}
        .metric {{ display: inline-block; margin-right: 30px; }}
        .metric-value {{ font-size: 2em; font-weight: bold; color: #007bff; }}
        .metric-label {{ display: block; font-size: 0.9em; color: #666; }}
        .passed {{ color: #28a745; }}
        .failed {{ color: #dc3545; }}
        .test-result {{ border: 1px solid #ddd; margin: 10px 0; border-radius: 4px; }}
        .test-header {{ background: #f8f9fa; padding: 10px; font-weight: bold; }}
        .test-header.failed {{ background: #f8d7da; }}
        .property-result {{ margin: 10px 0; padding: 10px; background: white; }}
        .property-result.failed {{ background: #fff5f5; border-left: 4px solid #dc3545; }}
        .property-result.passed {{ border-left: 4px solid #28a745; }}
        .code {{ background: #f4f4f4; padding: 2px 4px; border-radius: 3px; font-family: monospace; }}
        .details {{ margin-top: 10px; font-size: 0.9em; color: #666; }}
        table {{ width: 100%; border-collapse: collapse; margin: 20px 0; }}
        th, td {{ border: 1px solid #ddd; padding: 8px; text-align: left; }}
        th {{ background-color: #f2f2f2; }}
    </style>
</head>
<body>
    <div class='header'>
        <h1>{title}</h1>
        <p>Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</p>
    </div>

    <div class='summary'>
        <h2>Summary</h2>
        <div class='metric'>
            <div class='metric-value {(summary.OverallPassRate >= 0.9 ? "passed" : "failed")}'>{summary.OverallPassRate:P1}</div>
            <span class='metric-label'>Overall Pass Rate</span>
        </div>
        <div class='metric'>
            <div class='metric-value'>{summary.TotalTests}</div>
            <span class='metric-label'>Total Tests</span>
        </div>
        <div class='metric'>
            <div class='metric-value passed'>{summary.PassedTests}</div>
            <span class='metric-label'>Passed</span>
        </div>
        <div class='metric'>
            <div class='metric-value failed'>{summary.FailedTests}</div>
            <span class='metric-label'>Failed</span>
        </div>
        <div class='metric'>
            <div class='metric-value'>{summary.UniqueEndpoints}</div>
            <span class='metric-label'>Endpoints Tested</span>
        </div>
    </div>

    {(failedResults.Any() ? GenerateFailuresHtml(failedResults) : "<div class='summary'><h2>✅ All Tests Passed!</h2><p>No RFC violations detected.</p></div>")}

    <h2>Detailed Results</h2>
    {string.Join("", results.Take(50).Select(GenerateTestResultHtml))}
    
    {(results.Count > 50 ? $"<p><em>Showing first 50 results out of {results.Count} total tests.</em></p>" : "")}
    
</body>
</html>";

        return html;
    }

    private static void AddSummarySection(StringBuilder sb, List<PropertyTestResult> results)
    {
        var summary = GenerateSummary(results);

        sb.AppendLine("## Summary");
        sb.AppendLine();
        sb.AppendLine($"| Metric | Value |");
        sb.AppendLine($"|--------|-------|");
        sb.AppendLine($"| **Overall Pass Rate** | {summary.OverallPassRate:P1} |");
        sb.AppendLine($"| **Total Tests** | {summary.TotalTests} |");
        sb.AppendLine($"| **Passed Tests** | {summary.PassedTests} ✅ |");
        sb.AppendLine($"| **Failed Tests** | {summary.FailedTests} ❌ |");
        sb.AppendLine($"| **Unique Endpoints** | {summary.UniqueEndpoints} |");
        sb.AppendLine($"| **Total Properties Checked** | {summary.TotalProperties} |");
        sb.AppendLine($"| **Property Pass Rate** | {summary.PropertyPassRate:P1} |");
        sb.AppendLine($"| **Average Response Time** | {summary.AverageResponseTime:F2}ms |");
        sb.AppendLine();
    }

    private static void AddFailuresSummary(StringBuilder sb, List<PropertyTestResult> results)
    {
        var failedResults = results.Where(r => !r.OverallPassed).ToList();

        if (!failedResults.Any())
        {
            sb.AppendLine("## 🎉 All Tests Passed!");
            sb.AppendLine();
            sb.AppendLine("No RFC violations were detected in your API endpoints.");
            sb.AppendLine();
            return;
        }

        sb.AppendLine("## ❌ Failed Tests");
        sb.AppendLine();

        var failuresByProperty = failedResults
            .SelectMany(r => r.PropertyResults.Where(pr => !pr.Passed))
            .GroupBy(pr => pr.PropertyName)
            .OrderByDescending(g => g.Count())
            .Take(10);

        sb.AppendLine("### Most Common Failures");
        sb.AppendLine();
        sb.AppendLine("| Property | RFC Reference | Failures | Description |");
        sb.AppendLine("|----------|---------------|----------|-------------|");

        foreach (var group in failuresByProperty)
        {
            var firstFailure = group.First();
            sb.AppendLine($"| **{firstFailure.PropertyName}** | {firstFailure.RfcReference} | {group.Count()} | {firstFailure.PropertyDescription} |");
        }

        sb.AppendLine();
    }

    private static void AddDetailedResults(StringBuilder sb, List<PropertyTestResult> results)
    {
        sb.AppendLine("## Detailed Test Results");
        sb.AppendLine();

        var failedResults = results.Where(r => !r.OverallPassed).Take(20).ToList();
        
        if (failedResults.Any())
        {
            sb.AppendLine("### Failed Tests (First 20)");
            sb.AppendLine();

            foreach (var result in failedResults)
            {
                sb.AppendLine($"#### ❌ {result.RequestMethod} {result.RequestPath}");
                sb.AppendLine();
                sb.AppendLine($"- **Status Code:** {result.ResponseStatusCode}");
                sb.AppendLine($"- **Execution Time:** {result.TotalExecutionTime.TotalMilliseconds:F2}ms");
                sb.AppendLine($"- **Properties:** {result.PassedProperties}/{result.TotalProperties} passed");
                sb.AppendLine();

                var failedProperties = result.PropertyResults.Where(pr => !pr.Passed);
                foreach (var property in failedProperties)
                {
                    sb.AppendLine($"**{property.PropertyName}** ({property.RfcReference}):");
                    sb.AppendLine($"- ❌ {property.FailureReason}");
                    if (!string.IsNullOrEmpty(property.Details))
                        sb.AppendLine($"- Details: {property.Details}");
                    sb.AppendLine();
                }
            }
        }
    }

    private static void AddPerformanceMetrics(StringBuilder sb, List<PropertyTestResult> results)
    {
        sb.AppendLine("## Performance Metrics");
        sb.AppendLine();

        var avgByEndpoint = results
            .GroupBy(r => $"{r.RequestMethod} {r.RequestPath}")
            .Select(g => new
            {
                Endpoint = g.Key,
                AvgTime = g.Average(r => r.TotalExecutionTime.TotalMilliseconds),
                Count = g.Count(),
                PassRate = (double)g.Count(r => r.OverallPassed) / g.Count()
            })
            .OrderByDescending(x => x.AvgTime)
            .Take(10);

        sb.AppendLine("### Slowest Endpoints (Top 10)");
        sb.AppendLine();
        sb.AppendLine("| Endpoint | Avg Response Time | Test Count | Pass Rate |");
        sb.AppendLine("|----------|-------------------|------------|-----------|");

        foreach (var endpoint in avgByEndpoint)
        {
            sb.AppendLine($"| `{endpoint.Endpoint}` | {endpoint.AvgTime:F2}ms | {endpoint.Count} | {endpoint.PassRate:P1} |");
        }

        sb.AppendLine();
    }

    private static TestSummary GenerateSummary(List<PropertyTestResult> results)
    {
        var totalTests = results.Count;
        var passedTests = results.Count(r => r.OverallPassed);
        var totalProperties = results.Sum(r => r.TotalProperties);
        var passedProperties = results.Sum(r => r.PassedProperties);
        var uniqueEndpoints = results.Select(r => $"{r.RequestMethod} {r.RequestPath}").Distinct().Count();
        var avgResponseTime = results.Any() ? results.Average(r => r.TotalExecutionTime.TotalMilliseconds) : 0;

        return new TestSummary
        {
            TotalTests = totalTests,
            PassedTests = passedTests,
            FailedTests = totalTests - passedTests,
            TotalProperties = totalProperties,
            PassedProperties = passedProperties,
            FailedProperties = totalProperties - passedProperties,
            UniqueEndpoints = uniqueEndpoints,
            OverallPassRate = totalTests > 0 ? (double)passedTests / totalTests : 1.0,
            PropertyPassRate = totalProperties > 0 ? (double)passedProperties / totalProperties : 1.0,
            AverageResponseTime = avgResponseTime
        };
    }

    private string GenerateFailuresHtml(List<PropertyTestResult> failedResults)
    {
        return $@"
    <div class='summary'>
        <h2>❌ Failed Tests ({failedResults.Count})</h2>
        {string.Join("", failedResults.Take(10).Select(GenerateTestResultHtml))}
        {(failedResults.Count > 10 ? $"<p><em>Showing first 10 failed tests out of {failedResults.Count} total failures.</em></p>" : "")}
    </div>";
    }

    private string GenerateTestResultHtml(PropertyTestResult result)
    {
        var headerClass = result.OverallPassed ? "" : "failed";
        var statusIcon = result.OverallPassed ? "✅" : "❌";

        return $@"
    <div class='test-result'>
        <div class='test-header {headerClass}'>
            {statusIcon} {result.RequestMethod} {result.RequestPath} ({result.ResponseStatusCode})
            <span style='float: right;'>{result.PassedProperties}/{result.TotalProperties} properties passed</span>
        </div>
        {string.Join("", result.PropertyResults.Where(pr => !pr.Passed).Select(GeneratePropertyResultHtml))}
    </div>";
    }

    private string GeneratePropertyResultHtml(IndividualPropertyResult property)
    {
        var cssClass = property.Passed ? "passed" : "failed";
        var icon = property.Passed ? "✅" : "❌";

        return $@"
    <div class='property-result {cssClass}'>
        <strong>{icon} {property.PropertyName}</strong> <span class='code'>{property.RfcReference}</span>
        <div>{property.PropertyDescription}</div>
        {(!property.Passed ? $"<div class='details'><strong>Failure:</strong> {property.FailureReason}</div>" : "")}
        {(!string.IsNullOrEmpty(property.Details) && !property.Passed ? $"<div class='details'><strong>Details:</strong> {property.Details}</div>" : "")}
    </div>";
    }

    private class TestSummary
    {
        public int TotalTests { get; set; }
        public int PassedTests { get; set; }
        public int FailedTests { get; set; }
        public int TotalProperties { get; set; }
        public int PassedProperties { get; set; }
        public int FailedProperties { get; set; }
        public int UniqueEndpoints { get; set; }
        public double OverallPassRate { get; set; }
        public double PropertyPassRate { get; set; }
        public double AverageResponseTime { get; set; }
    }
}