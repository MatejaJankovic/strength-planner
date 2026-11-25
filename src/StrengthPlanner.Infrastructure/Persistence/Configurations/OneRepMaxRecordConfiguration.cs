using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StrengthPlanner.Domain.Entities;

namespace StrengthPlanner.Infrastructure.Persistence.Configurations;

public class OneRepMaxRecordConfiguration : IEntityTypeConfiguration<OneRepMaxRecord>
{
    public void Configure(EntityTypeBuilder<OneRepMaxRecord> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.ValueKg).HasPrecision(6, 2);

        builder.Property(r => r.Source)
            .HasConversion<string>()
            .HasMaxLength(16);

        // Procena živi i posle treninga iz kog je nastala (brisanje plana ne briše istoriju
        // maksimuma), pa ključ ide na null umesto da kaskadira.
        builder.HasOne<WorkoutSession>()
            .WithMany()
            .HasForeignKey(r => r.WorkoutSessionId)
            .OnDelete(DeleteBehavior.SetNull);

        // Veze ka ApplicationUser (Cascade) i Exercise (Restrict) konfigurisane su u
        // ApplicationUserConfiguration i ExerciseConfiguration.
    }
}
