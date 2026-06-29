using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StrengthPlanner.Domain.Entities;

namespace StrengthPlanner.Infrastructure.Persistence.Configurations;

public class ExerciseMuscleConfiguration : IEntityTypeConfiguration<ExerciseMuscle>
{
    public void Configure(EntityTypeBuilder<ExerciseMuscle> builder)
    {
        // Join entitet sa payload-om (Contribution) => kompozitni ključ.
        // Surogat Id se ne koristi.
        builder.Ignore(em => em.Id);
        builder.HasKey(em => new { em.ExerciseId, em.MuscleGroupId });

        // Frakcioni doprinos volumenu: 1.0 primarna, 0.5 sekundarna.
        builder.Property(em => em.Contribution).HasPrecision(6, 2);

        // Many-to-many Exercise <-> MuscleGroup preko ove veze.
        builder.HasOne(em => em.Exercise)
            .WithMany(e => e.Muscles)
            .HasForeignKey(em => em.ExerciseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(em => em.MuscleGroup)
            .WithMany(mg => mg.ExerciseMuscles)
            .HasForeignKey(em => em.MuscleGroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
