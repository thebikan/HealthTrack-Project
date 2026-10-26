using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HealthTrack.Infrastructure.Persistence;

// Intentionally offline: migration generation needs the model, not a server.
public sealed class HealthTrackDbContextFactory : IDesignTimeDbContextFactory<HealthTrackDbContext>
{
    public HealthTrackDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<HealthTrackDbContext>().UseSqlServer().Options);
}
