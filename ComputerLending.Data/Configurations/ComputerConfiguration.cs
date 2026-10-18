using ComputerLending.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComputerLending.Data.Configurations;

public class ComputerConfiguration : IEntityTypeConfiguration<Computer>
{
    public void Configure(EntityTypeBuilder<Computer> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.AssetTag)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(c => c.AssetTag)
            .IsUnique();

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.Specs)
            .HasMaxLength(500);

        builder.Property(c => c.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(c => c.Version)
            .IsConcurrencyToken();

        builder.Property(c => c.CreatedAtUtc)
            .IsRequired();

        builder.HasMany(c => c.Tags)
            .WithMany(t => t.Computers)
            .UsingEntity(j => j.ToTable("ComputerTag"));
    }
}
