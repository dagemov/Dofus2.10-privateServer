using Dofus210.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dofus210.Data.Configurations;

public sealed class BreedConfiguration : IEntityTypeConfiguration<Breed>
{
    public void Configure(EntityTypeBuilder<Breed> builder)
    {
        builder.ToTable("Breeds");

        builder.HasKey(breed => breed.Id);

        builder.Property(breed => breed.Name)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(breed => breed.MaleLook)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(breed => breed.FemaleLook)
            .IsRequired()
            .HasMaxLength(256);
    }
}
