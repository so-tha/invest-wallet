using InvestWallet.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvestWallet.Infrastructure.Persistence.Configurations;

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Type).IsRequired();
        builder.Property(t => t.Quantity).HasPrecision(18, 6).IsRequired();
        builder.Property(t => t.UnitPrice).HasPrecision(18, 6).IsRequired();
        builder.Property(t => t.ExecutedAt).IsRequired();
        builder.Property(t => t.CreatedAt).IsRequired();

        builder.HasOne(t => t.Asset)
               .WithMany()
               .HasForeignKey(t => t.AssetId)
               .OnDelete(DeleteBehavior.Restrict); // RN5: não apaga ativo com transações

        // Acelera o cálculo da posição (filtra por carteira + ativo, ordena por data)
        builder.HasIndex(t => new { t.WalletId, t.AssetId, t.ExecutedAt });

        // Defesa em profundidade: o banco também recusa valores inválidos
        builder.ToTable(tb =>
        {
            tb.HasCheckConstraint("ck_transactions_quantity_positive", "quantity > 0");
            tb.HasCheckConstraint("ck_transactions_unit_price_positive", "unit_price > 0");
        });
    }
}
