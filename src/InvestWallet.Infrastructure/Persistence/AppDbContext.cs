using InvestWallet.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InvestWallet.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<Transaction> Transactions => Set<Transaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Carrega todas as classes IEntityTypeConfiguration<> deste projeto
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
