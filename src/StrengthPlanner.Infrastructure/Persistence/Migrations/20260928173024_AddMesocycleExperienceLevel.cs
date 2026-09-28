using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StrengthPlanner.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Stores on the block the experience level it was generated with.
    ///
    /// The backfill is not cosmetic. Both readers of the level — the scaled volume
    /// landmarks and the auto-deload threshold — used to read the profile, so the profile's
    /// current level <i>is</i> what every existing block has been computed from. Leaving the
    /// column at the enum's zero would have relabelled 151 of the 160 blocks in the
    /// development database as a beginner's: the 19 advanced ones would lose the 1.2 band,
    /// and the 132 intermediate ones would lose the deload threshold altogether, since a
    /// beginner has none.
    /// </summary>
    public partial class AddMesocycleExperienceLevel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ExperienceLevel",
                table: "Mesocycles",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.Sql(
                """
                UPDATE "Mesocycles" AS m
                SET "ExperienceLevel" = p."ExperienceLevel"
                FROM "Profiles" AS p
                WHERE p."UserId" = m."UserId";
                """);

            // The model declares no default, and the generator always writes the level. A
            // column default left behind would only be there to answer an insert that has
            // already decided not to say.
            migrationBuilder.Sql(
                """
                ALTER TABLE "Mesocycles" ALTER COLUMN "ExperienceLevel" DROP DEFAULT;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExperienceLevel",
                table: "Mesocycles");
        }
    }
}
