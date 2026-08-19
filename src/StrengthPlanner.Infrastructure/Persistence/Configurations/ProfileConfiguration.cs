using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StrengthPlanner.Application.Security;
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
        builder.Property(p => p.HeightCm).HasPrecision(5, 1);

        // Ista granica kao u validaciji zahteva. Bez nje kolona je neograničen text, pa
        // dugačko ime prolazi bazu i puca tek na prikazu.
        builder.Property(p => p.DisplayName).HasMaxLength(ProfilePolicy.DisplayNameMaximumLength);

        // Tip slike upisuje server iz bajtova, pa je spisak vrednosti kratak i poznat;
        // granica je tu da kolona ne bude neograničen text.
        builder.Property(p => p.AvatarContentType).HasMaxLength(32);
    }
}
