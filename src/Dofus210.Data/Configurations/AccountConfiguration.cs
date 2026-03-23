using Dofus210.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dofus210.Data.Configurations;

public sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("Accounts");

        builder.HasKey(account => account.Id);

        builder.Property(account => account.Username)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(account => account.PasswordHash)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(account => account.Nickname)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(account => account.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(account => account.Username)
            .IsUnique();
    }
}
