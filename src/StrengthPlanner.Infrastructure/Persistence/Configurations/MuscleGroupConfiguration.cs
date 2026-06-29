using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StrengthPlanner.Domain.Entities;

namespace StrengthPlanner.Infrastructure.Persistence.Configurations;

public class MuscleGroupConfiguration : IEntityTypeConfiguration<MuscleGroup>
{
    public void Configure(EntityTypeBuilder<MuscleGroup> builder)
    {
        builder.HasKey(mg => mg.Id);

        builder.Property(mg => mg.Name)
            .IsRequired()
            .HasMaxLength(64);

        builder.HasIndex(mg => mg.Name).IsUnique();

        // 1:1 sa VolumeLandmark (MEV/MRV granice) — FK na strani VolumeLandmark.
        builder.HasOne(mg => mg.VolumeLandmark)
            .WithOne(vl => vl.MuscleGroup)
            .HasForeignKey<VolumeLandmark>(vl => vl.MuscleGroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
