using Dofus210.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dofus210.Data.Configurations;

public sealed class CharacterStatsConfiguration : IEntityTypeConfiguration<CharacterStats>
{
    public void Configure(EntityTypeBuilder<CharacterStats> builder)
    {
        builder.ToTable("CharacterStats");

        builder.HasKey(stats => stats.CharacterId);

        builder.Property(stats => stats.ActionPoints)
            .HasDefaultValue((short)6);

        builder.Property(stats => stats.MovementPoints)
            .HasDefaultValue((short)3);

        builder.HasOne(stats => stats.Character)
            .WithOne(character => character.Stats)
            .HasForeignKey<CharacterStats>(stats => stats.CharacterId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
