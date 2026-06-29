using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StrengthPlanner.Domain.Entities;

namespace StrengthPlanner.Infrastructure.Persistence.Configurations;

public class ProfileConfiguration : IEntityTypeConfiguration<Profile>
{
    public void Configure(EntityTypeBuilder<Profile> builder)
    {
        // 1:1 sa User: primarni ključ je ujedno strani ključ (PK = FK = UserId).
        // Surogat Id se ne koristi za ovaj entitet.
        builder.Ignore(p => p.Id);
        builder.HasKey(p => p.UserId);

        builder.Property(p => p.BodyweightKg).HasPrecision(6, 2);
        builder.Property(p => p.Sex).HasMaxLength(16);
    }
}
