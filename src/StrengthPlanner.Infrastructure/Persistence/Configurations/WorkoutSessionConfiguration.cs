using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StrengthPlanner.Application.Security;
using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Entities;

namespace StrengthPlanner.Infrastructure.Persistence.Configurations;

public class WorkoutSessionConfiguration : IEntityTypeConfiguration<WorkoutSession>
{
    public void Configure(EntityTypeBuilder<WorkoutSession> builder)
    {
        builder.HasKey(s => s.Id);

        // Ista granica kao naziv dana u šablonu: generator naziv dana prepisuje ovamo
        // (MesocycleGenerator: DayLabel = templateDay.Name). Uža kolona ovde znači da se
        // šablon sačuva, a plan napravljen od njega padne na upisu.
        builder.Property(s => s.DayLabel)
            .IsRequired()
            .HasMaxLength(TrainingConstants.MaxDayNameLength);

        builder.Property(s => s.Status)
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(s => s.Note)
            .HasMaxLength(NotePolicy.MaximumLength);

        // Ocena umora je 1 do 5; granica se brani i u bazi, jer iz nje nedeljni signal računa porast.
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_WorkoutSessions_PerceivedFatigueRange",
            "\"PerceivedFatigue\" IS NULL OR \"PerceivedFatigue\" BETWEEN 1 AND 5"));

        // Kaskadni lanac: Sessions -> ExercisePlans.
        builder.HasMany(s => s.ExercisePlans)
            .WithOne(ep => ep.WorkoutSession)
            .HasForeignKey(ep => ep.WorkoutSessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
