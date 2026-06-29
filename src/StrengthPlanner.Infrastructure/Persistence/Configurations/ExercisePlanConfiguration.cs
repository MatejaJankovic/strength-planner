using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StrengthPlanner.Domain.Entities;

namespace StrengthPlanner.Infrastructure.Persistence.Configurations;

public class ExercisePlanConfiguration : IEntityTypeConfiguration<ExercisePlan>
{
    public void Configure(EntityTypeBuilder<ExercisePlan> builder)
    {
        builder.HasKey(ep => ep.Id);

        // Preporučeno opterećenje koje puni algoritam.
        builder.Property(ep => ep.TargetWeightKg).HasPrecision(6, 2);

        // Kaskadni lanac: ExercisePlans -> SetLogs.
        builder.HasMany(ep => ep.SetLogs)
            .WithOne(sl => sl.ExercisePlan)
            .HasForeignKey(sl => sl.ExercisePlanId)
            .OnDelete(DeleteBehavior.Cascade);

        // Veza ka Exercise je konfigurisana u ExerciseConfiguration (OnDelete = Restrict).
    }
}
