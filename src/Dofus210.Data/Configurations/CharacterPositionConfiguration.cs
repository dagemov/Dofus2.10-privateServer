using Dofus210.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dofus210.Data.Configurations;

public sealed class CharacterPositionConfiguration : IEntityTypeConfiguration<CharacterPosition>
{
    public void Configure(EntityTypeBuilder<CharacterPosition> builder)
    {
        builder.ToTable("CharacterPositions");

        builder.HasKey(position => position.CharacterId);

        builder.Property(position => position.Direction)
            .HasDefaultValue((byte)2);

        builder.HasOne(position => position.Character)
            .WithOne(character => character.Position)
            .HasForeignKey<CharacterPosition>(position => position.CharacterId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
