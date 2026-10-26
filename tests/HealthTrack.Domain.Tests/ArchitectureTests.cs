using System.Reflection;

namespace HealthTrack.Domain.Tests;

public sealed class ArchitectureTests
{
    [Fact]
    public void Domain_does_not_reference_outer_layers_or_framework_services()
    {
        var references = Assembly.Load("HealthTrack.Domain").GetReferencedAssemblies();
        Assert.DoesNotContain(references, reference =>
            reference.Name!.StartsWith("HealthTrack.") ||
            reference.Name.StartsWith("Microsoft.AspNetCore") ||
            reference.Name.StartsWith("Microsoft.EntityFrameworkCore") ||
            reference.Name.StartsWith("Microsoft.Extensions"));
    }
}
