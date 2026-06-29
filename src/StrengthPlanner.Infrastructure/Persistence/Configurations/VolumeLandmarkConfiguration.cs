using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StrengthPlanner.Domain.Entities;

namespace StrengthPlanner.Infrastructure.Persistence.Configurations;

public class VolumeLandmarkConfiguration : IEntityTypeConfiguration<VolumeLandmark>
{
    public void Configure(EntityTypeBuilder<VolumeLandmark> builder)
    {
        builder.HasKey(vl => vl.Id);

        // Jedinstven landmark po mišićnoj grupi.
        builder.HasIndex(vl => vl.MuscleGroupId).IsUnique();

        // Veza ka MuscleGroup konfigurisana je u MuscleGroupConfiguration.
    }
}
