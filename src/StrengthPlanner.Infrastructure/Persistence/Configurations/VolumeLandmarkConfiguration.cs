using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StrengthPlanner.Domain.Entities;

namespace StrengthPlanner.Infrastructure.Persistence.Configurations;

public class VolumeLandmarkConfiguration : IEntityTypeConfiguration<VolumeLandmark>
{
    public void Configure(EntityTypeBuilder<VolumeLandmark> builder)
    {
        // Isti redosled koji važi za naučene granice mora da važi i za seed vrednosti,
        // inače bi marker cilja mogao da završi iza plafona na ekranu.
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_VolumeLandmarks_LandmarkOrder",
            "\"Mev\" >= 1 AND \"Mav\" > \"Mev\" AND \"Mrv\" > \"Mav\""));

        builder.HasKey(vl => vl.Id);

        // Jedinstven landmark po mišićnoj grupi.
        builder.HasIndex(vl => vl.MuscleGroupId).IsUnique();

        // Veza ka MuscleGroup konfigurisana je u MuscleGroupConfiguration.
    }
}
