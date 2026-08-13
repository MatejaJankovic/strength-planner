using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StrengthPlanner.Domain.Entities;

namespace StrengthPlanner.Infrastructure.Persistence.Configurations;

public class MesocycleConfiguration : IEntityTypeConfiguration<Mesocycle>
{
    public void Configure(EntityTypeBuilder<Mesocycle> builder)
    {
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Name)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(m => m.Goal)
            .HasConversion<string>()
            .HasMaxLength(16);

        // Zatečeni blokovi su pravljeni bez modela i ravni su po sadržaju, pa im
        // podrazumevana vrednost odgovara stvarnom rasporedu.
        builder.Property(m => m.PeriodizationModel)
            .HasConversion<string>()
            .HasMaxLength(16)
            .HasDefaultValue(Domain.Enums.PeriodizationModel.Flat);

        builder.Property(m => m.DurationWeeks).HasDefaultValue(4);

        // Kaskadni lanac: Mesocycle -> Weeks.
        builder.HasMany(m => m.Weeks)
            .WithOne(w => w.Mesocycle)
            .HasForeignKey(w => w.MesocycleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
