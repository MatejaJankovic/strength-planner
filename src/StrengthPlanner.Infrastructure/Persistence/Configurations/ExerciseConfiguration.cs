using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Entities;

namespace StrengthPlanner.Infrastructure.Persistence.Configurations;

public class ExerciseConfiguration : IEntityTypeConfiguration<Exercise>
{
    public void Configure(EntityTypeBuilder<Exercise> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(e => e.Equipment)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(e => e.WeightStepKg)
            .HasPrecision(6, 2)
            .HasDefaultValue(TrainingConstants.WeightStepKg);

        // Enum -> string radi čitljivosti u bazi.
        builder.Property(e => e.Type)
            .HasConversion<string>()
            .HasMaxLength(16);

        // Ne brišemo planove/1RM zapise kada se briše vežba (sprečavamo kaskadni gubitak istorije).
        builder.HasMany(e => e.ExercisePlans)
            .WithOne(ep => ep.Exercise)
            .HasForeignKey(ep => ep.ExerciseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.OneRepMaxRecords)
            .WithOne(r => r.Exercise)
            .HasForeignKey(r => r.ExerciseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
