using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StrengthPlanner.Domain.Entities;

namespace StrengthPlanner.Infrastructure.Persistence.Configurations;

public class UserExerciseSettingConfiguration : IEntityTypeConfiguration<UserExerciseSetting>
{
    public void Configure(EntityTypeBuilder<UserExerciseSetting> builder)
    {
        builder.HasKey(setting => setting.Id);

        builder.Property(setting => setting.WeightStepKg)
            .HasPrecision(6, 2);

        // Najviše jedan override po korisniku i vežbi.
        builder.HasIndex(setting => new { setting.UserId, setting.ExerciseId })
            .IsUnique();

        // Brisanje vežbe ne sme tiho da obriše korisnička podešavanja.
        builder.HasOne(setting => setting.Exercise)
            .WithMany()
            .HasForeignKey(setting => setting.ExerciseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
