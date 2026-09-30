using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StrengthPlanner.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Whether a compound can carry a strength block's 3-6 range.
    ///
    /// Every existing row is backfilled with true, because every existing compound has been
    /// prescribed exactly that way until now; the generated default of false would have
    /// moved every user's own compounds to the accessory range. The seven system exercises
    /// that should not carry it (unilateral, unstable, or with no way to add load) are set
    /// by DbSeeder's reconcile on startup, from ExerciseCatalog - the same path that keeps
    /// every other catalog field aligned.
    /// </summary>
    public partial class AddExerciseSuitsLowReps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SuitsLowReps",
                table: "Exercises",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            // The model declares no default - for a bool, EF would then omit the column
            // whenever the value is false, and a column default of true would overwrite it.
            migrationBuilder.Sql(
                """
                ALTER TABLE "Exercises" ALTER COLUMN "SuitsLowReps" DROP DEFAULT;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SuitsLowReps",
                table: "Exercises");
        }
    }
}
