using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StrengthPlanner.Domain.Entities;

namespace StrengthPlanner.Infrastructure.Persistence.Configurations;

public class WorkoutSessionConfiguration : IEntityTypeConfiguration<WorkoutSession>
{
    public void Configure(EntityTypeBuilder<WorkoutSession> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.DayLabel)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(s => s.Status)
            .HasConversion<string>()
            .HasMaxLength(16);

        // Kaskadni lanac: Sessions -> ExercisePlans.
        builder.HasMany(s => s.ExercisePlans)
            .WithOne(ep => ep.WorkoutSession)
            .HasForeignKey(ep => ep.WorkoutSessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
