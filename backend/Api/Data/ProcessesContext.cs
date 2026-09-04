using Microsoft.EntityFrameworkCore;
using TremblantLifecycle.Api.Models.Entities;

namespace TremblantLifecycle.Api.Data;

/// <summary>Read-only context pointed at the existing PROCESSES database — separate from
/// AppDbContext/WorkdayContext because this is yet another genuinely different, separately-owned
/// database on the same vm-trm-sql1 instance. Never call SaveChanges on this context;
/// dbo.AdAccount (and this view over it) is externally managed by the GroupMembershipSync repo's
/// SQL Agent job.</summary>
public class ProcessesContext : DbContext
{
    public ProcessesContext(DbContextOptions<ProcessesContext> options) : base(options) { }

    public DbSet<ProcessesAdAccount> AdAccountPeople => Set<ProcessesAdAccount>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProcessesAdAccount>(entity =>
        {
            entity.ToView("vw_AdAccount_People", schema: "dbo");
            entity.HasNoKey();
        });
    }
}
