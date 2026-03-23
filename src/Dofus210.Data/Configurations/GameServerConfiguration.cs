using Dofus210.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dofus210.Data.Configurations;

public sealed class GameServerConfiguration : IEntityTypeConfiguration<GameServer>
{
    public void Configure(EntityTypeBuilder<GameServer> builder)
    {
        builder.ToTable("GameServers");

        builder.HasKey(server => server.Id);

        builder.Property(server => server.Name)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(server => server.Address)
            .IsRequired()
            .HasMaxLength(64);
    }
}
