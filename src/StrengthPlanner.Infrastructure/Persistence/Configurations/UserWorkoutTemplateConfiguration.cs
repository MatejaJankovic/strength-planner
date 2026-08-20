using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StrengthPlanner.Domain.Algorithms;
using StrengthPlanner.Domain.Entities;

namespace StrengthPlanner.Infrastructure.Persistence.Configurations;

public class UserWorkoutTemplateConfiguration : IEntityTypeConfiguration<UserWorkoutTemplate>
{
    public void Configure(EntityTypeBuilder<UserWorkoutTemplate> builder)
    {
        builder.HasKey(template => template.Id);

        builder.Property(template => template.Name)
            .IsRequired()
            .HasMaxLength(128);

        // Spisak ličnih šablona se uvek čita za jednog korisnika.
        builder.HasIndex(template => template.UserId);

        // Kaskadni lanac: Template -> Days -> Exercises. Brisanje šablona nosi sve sa sobom.
        builder.HasMany(template => template.Days)
            .WithOne(day => day.UserWorkoutTemplate)
            .HasForeignKey(day => day.UserWorkoutTemplateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class UserWorkoutTemplateDayConfiguration : IEntityTypeConfiguration<UserWorkoutTemplateDay>
{
    public void Configure(EntityTypeBuilder<UserWorkoutTemplateDay> builder)
    {
        builder.HasKey(day => day.Id);

        builder.Property(day => day.Name)
            .IsRequired()
            .HasMaxLength(TrainingConstants.MaxDayNameLength);

        builder.HasMany(day => day.Exercises)
            .WithOne(exercise => exercise.UserWorkoutTemplateDay)
            .HasForeignKey(exercise => exercise.UserWorkoutTemplateDayId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class UserWorkoutTemplateExerciseConfiguration
    : IEntityTypeConfiguration<UserWorkoutTemplateExercise>
{
    public void Configure(EntityTypeBuilder<UserWorkoutTemplateExercise> builder)
    {
        builder.HasKey(exercise => exercise.Id);

        // Vežba se ne briše dok je neki šablon koristi: tiho uklanjanje stavke iz šablona
        // značilo bi da plan generisan iz njega odjednom nosi manje vežbi nego što piše.
        builder.HasOne(exercise => exercise.Exercise)
            .WithMany()
            .HasForeignKey(exercise => exercise.ExerciseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
