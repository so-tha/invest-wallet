using InvestWallet.Application.Exceptions;
using InvestWallet.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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

    /// <summary>
    /// Traduz erros de integridade do PostgreSQL em ConflictException (HTTP 409).
    /// A checagem "já existe?" na Application tem uma janela de corrida: dois pedidos simultâneos podem
    /// passar por ela. O índice único do banco é quem decide de fato, e aqui o erro dele vira 409 em vez de 500.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg)
        {
            if (pg.SqlState == PostgresErrorCodes.UniqueViolation)
                throw new ConflictException(pg.ConstraintName == "ix_assets_ticker"
                    ? "Já existe um ativo com este ticker."
                    : "Já existe um registro com os mesmos dados.");

            if (pg.SqlState == PostgresErrorCodes.ForeignKeyViolation)
                throw new ConflictException("A operação conflita com registros relacionados (talvez eles tenham acabado de ser alterados).");

            throw;
        }
    }
}
