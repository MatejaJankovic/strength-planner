using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StrengthPlanner.Infrastructure.Identity;

namespace StrengthPlanner.Infrastructure.Persistence.Configurations;

public class LoginSessionConfiguration : IEntityTypeConfiguration<LoginSession>
{
    /// <summary>A SHA-256 hash written in hexadecimal.</summary>
    private const int TokenHashLength = 64;

    public void Configure(EntityTypeBuilder<LoginSession> builder)
    {
        builder.HasKey(session => session.Id);

        builder.Property(session => session.HeldTokenHash).IsRequired().HasMaxLength(TokenHashLength);
        builder.Property(session => session.IssuedTokenHash).HasMaxLength(TokenHashLength);
        builder.Property(session => session.SecurityStamp).IsRequired();

        // Sesije nestaju sa nalogom, kao i ostali korisnički podaci.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(session => session.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
