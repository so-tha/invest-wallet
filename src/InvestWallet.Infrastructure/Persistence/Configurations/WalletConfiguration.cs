using InvestWallet.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvestWallet.Infrastructure.Persistence.Configurations;

public class WalletConfiguration : IEntityTypeConfiguration<Wallet>
{
    public void Configure(EntityTypeBuilder<Wallet> builder)
    {
        builder.HasKey(w => w.Id);
        builder.Property(w => w.Name).IsRequired().HasMaxLength(Wallet.NameMaxLength);
        builder.Property(w => w.Description).HasMaxLength(Wallet.DescriptionMaxLength);
        builder.Property(w => w.CreatedAt).IsRequired();

        builder.HasMany(w => w.Transactions)
               .WithOne(t => t.Wallet)
               .HasForeignKey(t => t.WalletId)
               .OnDelete(DeleteBehavior.Restrict); // RN5: não apaga carteira com transações
    }
}
