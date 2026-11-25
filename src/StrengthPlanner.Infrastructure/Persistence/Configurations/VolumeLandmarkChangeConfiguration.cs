using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StrengthPlanner.Domain.Entities;

namespace StrengthPlanner.Infrastructure.Persistence.Configurations;

public class VolumeLandmarkChangeConfiguration : IEntityTypeConfiguration<VolumeLandmarkChange>
{
    public void Configure(EntityTypeBuilder<VolumeLandmarkChange> builder)
    {
        builder.HasKey(change => change.Id);

        // Nedelja pomera granice jednog mišića najviše jednom.
        builder.HasIndex(change => new { change.TrainingWeekId, change.MuscleGroupId })
            .IsUnique();

        // Pomeraj živi koliko i nedelja koja ga je napravila: brisanje bloka briše i njega,
        // pa naloga kaskada stiže do njega istim putem kao do ostalih delova nedelje.
        builder.HasOne(change => change.TrainingWeek)
            .WithMany()
            .HasForeignKey(change => change.TrainingWeekId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(change => change.MuscleGroup)
            .WithMany()
            .HasForeignKey(change => change.MuscleGroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
