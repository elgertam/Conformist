using FsCheck;
using FsCheck.Xunit;
using Conformist.HttpRfc.Core;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Conformist.HttpRfc.Extensions;

public static class XUnitExtensions
{
    public static void ShouldConformToHttpRfc<TContext, TProgram>(
        this WebApplicationFactory<TProgram> factory)
        where TContext : DbContext
        where TProgram : class
    {
        var property = factory.HttpRfcConformanceProperty<TContext, TProgram>();
        property.QuickCheckThrowOnFailure();
    }

    public static void ShouldConformToHttpRfc<TContext, TProgram>(
        this WebApplicationFactory<TProgram> factory,
        Action<HttpRfcTestBuilder<TContext, TProgram>> configure)
        where TContext : DbContext
        where TProgram : class
    {
        var property = factory.HttpRfcConformanceProperty(configure);
        property.QuickCheckThrowOnFailure();
    }

    public static void ShouldConformToHttpRfc<TContext, TProgram>(
        this WebApplicationFactory<TProgram> factory,
        Configuration config)
        where TContext : DbContext
        where TProgram : class
    {
        var property = factory.HttpRfcConformanceProperty<TContext, TProgram>();
        property.Check(config);
    }

    public static void ShouldConformToHttpRfc<TContext, TProgram>(
        this WebApplicationFactory<TProgram> factory,
        Action<HttpRfcTestBuilder<TContext, TProgram>> configure,
        Configuration config)
        where TContext : DbContext
        where TProgram : class
    {
        var property = factory.HttpRfcConformanceProperty(configure);
        property.Check(config);
    }
}

[AttributeUsage(AttributeTargets.Method)]
public class HttpRfcPropertyAttribute : PropertyAttribute
{
    public HttpRfcPropertyAttribute() : base()
    {
        MaxTest = 50; 
        StartSize = 0;
        EndSize = 10;
        QuietOnSuccess = false;
    }

    public HttpRfcPropertyAttribute(int maxTest) : this()
    {
        MaxTest = maxTest;
    }
}

[AttributeUsage(AttributeTargets.Method)]
public class HttpRfcFactAttribute : Xunit.FactAttribute
{
    public HttpRfcFactAttribute()
    {
        DisplayName = "HTTP RFC Conformance Test";
    }
}