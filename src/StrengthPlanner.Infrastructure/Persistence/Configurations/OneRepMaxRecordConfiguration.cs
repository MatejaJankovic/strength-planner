using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StrengthPlanner.Domain.Entities;

namespace StrengthPlanner.Infrastructure.Persistence.Configurations;

public class OneRepMaxRecordConfiguration : IEntityTypeConfiguration<OneRepMaxRecord>
{
    public void Configure(EntityTypeBuilder<OneRepMaxRecord> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.ValueKg).HasPrecision(6, 2);

        builder.Property(r => r.Source)
            .HasConversion<string>()
            .HasMaxLength(16);

        // Veze ka ApplicationUser (Cascade) i Exercise (Restrict) konfigurisane su u
        // ApplicationUserConfiguration i ExerciseConfiguration.
    }
}
