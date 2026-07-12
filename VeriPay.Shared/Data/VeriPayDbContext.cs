using Microsoft.EntityFrameworkCore;
using VeriPay.Shared.Models;

namespace VeriPay.Shared.Data;

public class VeriPayDbContext : DbContext
{
    public VeriPayDbContext(DbContextOptions options) : base(options) { }

    public DbSet<Transfer>      Transfers      => Set<Transfer>();
    public DbSet<TransferEvent> TransferEvents => Set<TransferEvent>();
    public DbSet<Wallet>        Wallets        => Set<Wallet>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Transfer>()
          .HasIndex(t => t.TransferId).IsUnique();

        mb.Entity<Transfer>()
          .HasMany(t => t.Events)
          .WithOne(e => e.Transfer)
          .HasForeignKey(e => e.TransferId)
          .HasPrincipalKey(t => t.TransferId)
          .OnDelete(DeleteBehavior.Cascade);
    }
}
