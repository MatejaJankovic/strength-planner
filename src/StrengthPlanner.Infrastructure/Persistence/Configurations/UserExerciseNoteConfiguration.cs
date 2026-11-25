using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StrengthPlanner.Application.Security;
using StrengthPlanner.Domain.Entities;

namespace StrengthPlanner.Infrastructure.Persistence.Configurations;

public class UserExerciseNoteConfiguration : IEntityTypeConfiguration<UserExerciseNote>
{
    public void Configure(EntityTypeBuilder<UserExerciseNote> builder)
    {
        builder.HasKey(note => note.Id);

        builder.Property(note => note.Text)
            .IsRequired()
            .HasMaxLength(NotePolicy.MaximumLength);

        // At most one note per lifter and exercise.
        builder.HasIndex(note => new { note.UserId, note.ExerciseId })
            .IsUnique();

        // Brisanje vežbe ne sme tiho da obriše korisničke beleške (kao ni podešavanja).
        builder.HasOne(note => note.Exercise)
            .WithMany()
            .HasForeignKey(note => note.ExerciseId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
