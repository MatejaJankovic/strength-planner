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
    }
}
