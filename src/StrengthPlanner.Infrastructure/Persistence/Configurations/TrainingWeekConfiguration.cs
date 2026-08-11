using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StrengthPlanner.Domain.Entities;

namespace StrengthPlanner.Infrastructure.Persistence.Configurations;

public class TrainingWeekConfiguration : IEntityTypeConfiguration<TrainingWeek>
{
    public void Configure(EntityTypeBuilder<TrainingWeek> builder)
    {
        builder.HasKey(w => w.Id);

        // Ocena umora je uvek 0-1; bez preciznosti bi kolona bila neograničeni numeric.
        builder.Property(w => w.FatigueScore).HasPrecision(4, 3);

        // Jedan broj nedelje po mezociklusu.
        builder.HasIndex(w => new { w.MesocycleId, w.WeekNumber }).IsUnique();

        // Kaskadni lanac: Weeks -> Sessions.
        builder.HasMany(w => w.Sessions)
            .WithOne(s => s.TrainingWeek)
            .HasForeignKey(s => s.TrainingWeekId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
