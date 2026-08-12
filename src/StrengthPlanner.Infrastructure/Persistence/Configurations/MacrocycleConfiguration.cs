using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StrengthPlanner.Domain.Entities;

namespace StrengthPlanner.Infrastructure.Persistence.Configurations;

public class MacrocycleConfiguration : IEntityTypeConfiguration<Macrocycle>
{
    public void Configure(EntityTypeBuilder<Macrocycle> builder)
    {
        builder.HasKey(macrocycle => macrocycle.Id);

        builder.Property(macrocycle => macrocycle.Name)
            .IsRequired()
            .HasMaxLength(128);

        builder.HasIndex(macrocycle => new { macrocycle.UserId, macrocycle.IsActive });

        builder.HasMany(macrocycle => macrocycle.Blocks)
            .WithOne(block => block.Macrocycle)
            .HasForeignKey(block => block.MacrocycleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class MacrocycleBlockConfiguration : IEntityTypeConfiguration<MacrocycleBlock>
{
    public void Configure(EntityTypeBuilder<MacrocycleBlock> builder)
    {
        builder.HasKey(block => block.Id);

        builder.Property(block => block.TemplateKey)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(block => block.Goal)
            .HasConversion<string>()
            .HasMaxLength(16);

        // Jedan blok po rednom broju unutar plana.
        builder.HasIndex(block => new { block.MacrocycleId, block.Order })
            .IsUnique();

        // Mezociklus pripada tačno jednom bloku.
        builder.HasIndex(block => block.MesocycleId)
            .IsUnique()
            .HasFilter("\"MesocycleId\" IS NOT NULL");

        // Brisanje mezociklusa ne sme da obriše i plan; blok se vraća u "negenerisano".
        builder.HasOne(block => block.Mesocycle)
            .WithMany()
            .HasForeignKey(block => block.MesocycleId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
