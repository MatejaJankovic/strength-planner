using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StrengthPlanner.Domain.Entities;

namespace StrengthPlanner.Infrastructure.Persistence.Configurations;

public class UserVolumeLandmarkConfiguration : IEntityTypeConfiguration<UserVolumeLandmark>
{
    public void Configure(EntityTypeBuilder<UserVolumeLandmark> builder)
    {
        builder.HasKey(landmark => landmark.Id);

        // Najviše jedan lični par granica po korisniku i mišićnoj grupi.
        builder.HasIndex(landmark => new { landmark.UserId, landmark.MuscleGroupId })
            .IsUnique();

        builder.HasOne(landmark => landmark.MuscleGroup)
            .WithMany()
            .HasForeignKey(landmark => landmark.MuscleGroupId)
            .OnDelete(DeleteBehavior.Cascade);

        // Optimalni pojas mora da postoji i u bazi, ne samo u algoritmu.
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_UserVolumeLandmarks_MevBelowMrv",
            "\"Mev\" >= 1 AND \"Mav\" > \"Mev\" AND \"Mrv\" > \"Mav\""));
    }
}
