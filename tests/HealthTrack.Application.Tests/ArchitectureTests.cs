using System.Reflection;

namespace HealthTrack.Application.Tests;

public sealed class ArchitectureTests
{
    [Fact]
    public void Application_does_not_reference_infrastructure_web_or_persistence()
    {
        var references = Assembly.Load("HealthTrack.Application").GetReferencedAssemblies();
        Assert.DoesNotContain(references, reference =>
            reference.Name is "HealthTrack.Infrastructure" or "HealthTrack.Web" ||
            reference.Name!.StartsWith("Microsoft.AspNetCore") ||
            reference.Name.StartsWith("Microsoft.EntityFrameworkCore") ||
            reference.Name.StartsWith("Microsoft.Data.SqlClient"));
    }
}
