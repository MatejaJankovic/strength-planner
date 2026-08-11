using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StrengthPlanner.Domain.Entities;
using StrengthPlanner.Infrastructure.Identity;

namespace StrengthPlanner.Infrastructure.Persistence.Configurations;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        // 1:1 sa Profile — FK je Profile.UserId (ujedno PK profila).
        builder.HasOne(u => u.Profile)
            .WithOne()
            .HasForeignKey<Profile>(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.Mesocycles)
            .WithOne()
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.OneRepMaxRecords)
            .WithOne()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Korisnička podešavanja vežbi nemaju navigaciju na korisniku, ali moraju
        // da nestanu sa nalogom kao i ostali korisnički podaci.
        builder.HasMany<UserExerciseSetting>()
            .WithOne()
            .HasForeignKey(setting => setting.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany<UserVolumeLandmark>()
            .WithOne()
            .HasForeignKey(landmark => landmark.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany<Macrocycle>()
            .WithOne()
            .HasForeignKey(macrocycle => macrocycle.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
