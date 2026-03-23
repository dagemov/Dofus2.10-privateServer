using Dofus210.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dofus210.Data.Configurations;

public sealed class CharacterConfiguration : IEntityTypeConfiguration<Character>
{
    public void Configure(EntityTypeBuilder<Character> builder)
    {
        builder.ToTable("Characters");

        builder.HasKey(character => character.Id);

        builder.Property(character => character.Name)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(character => character.Level)
            .HasDefaultValue((short)1);

        builder.Property(character => character.BonesId)
            .HasDefaultValue(1);

        builder.Property(character => character.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(character => new { character.GameServerId, character.Name })
            .IsUnique();

        builder.HasOne(character => character.Account)
            .WithMany(account => account.Characters)
            .HasForeignKey(character => character.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(character => character.Breed)
            .WithMany(breed => breed.Characters)
            .HasForeignKey(character => character.BreedId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(character => character.GameServer)
            .WithMany(server => server.Characters)
            .HasForeignKey(character => character.GameServerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
