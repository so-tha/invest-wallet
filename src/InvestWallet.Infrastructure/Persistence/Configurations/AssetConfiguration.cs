using InvestWallet.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvestWallet.Infrastructure.Persistence.Configurations;

public class AssetConfiguration : IEntityTypeConfiguration<Asset>
{
    public void Configure(EntityTypeBuilder<Asset> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Ticker).IsRequired().HasMaxLength(Asset.TickerMaxLength);
        builder.Property(a => a.Name).IsRequired().HasMaxLength(Asset.NameMaxLength);
        builder.Property(a => a.Type).IsRequired();
        builder.Property(a => a.CreatedAt).IsRequired();

        builder.HasIndex(a => a.Ticker).IsUnique(); // RN2
    }
}
