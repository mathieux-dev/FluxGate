using Microsoft.EntityFrameworkCore;

namespace FluxPay.Infrastructure.Data;

public class FluxPayDbContext : DbContext
{
    public FluxPayDbContext(DbContextOptions<FluxPayDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }
}
