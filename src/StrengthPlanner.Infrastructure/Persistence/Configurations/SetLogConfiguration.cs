using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StrengthPlanner.Domain.Entities;

namespace StrengthPlanner.Infrastructure.Persistence.Configurations;

public class SetLogConfiguration : IEntityTypeConfiguration<SetLog>
{
    public void Configure(EntityTypeBuilder<SetLog> builder)
    {
        builder.HasKey(sl => sl.Id);

        builder.Property(sl => sl.WeightKg).HasPrecision(6, 2);

        builder.Property(sl => sl.IsFailure).HasDefaultValue(false);

        // Progresija računa da otkaz znači RIR 0; invarijanta se brani i u bazi,
        // a ne samo u servisu, jer o njoj zavisi smer korekcije opterećenja.
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_SetLogs_FailureHasNoRir",
            "NOT \"IsFailure\" OR \"Rir\" = 0"));
    }
}
