using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StrengthPlanner.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Records the block's base rep range per exercise plan.
    ///
    /// Until custom templates every plan in a block shared the goal's range, so the deload
    /// logic could read it straight off the goal. A custom template gives each exercise its
    /// own range, and the week's shift cannot be inverted to recover it because
    /// Periodization also clamps the range to its bounds. The base is therefore stored.
    /// </summary>
    public partial class AddBaseRepRange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BaseRepRangeMin",
                table: "ExercisePlans",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BaseRepRangeMax",
                table: "ExercisePlans",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Zero would be a lie that the deload logic would then act on. Every plan that
            // already exists came from a built-in template, where the base is the goal's
            // range - the same two pairs GoalPrescriptions defines (Strength 3-6,
            // Hypertrophy 8-12). Plans are backfilled from the goal of their mesocycle.
            migrationBuilder.Sql(
                """
                UPDATE "ExercisePlans" AS p
                SET "BaseRepRangeMin" = CASE m."Goal" WHEN 'Strength' THEN 3 ELSE 8 END,
                    "BaseRepRangeMax" = CASE m."Goal" WHEN 'Strength' THEN 6 ELSE 12 END
                FROM "WorkoutSessions" AS s
                JOIN "TrainingWeeks" AS w ON w."Id" = s."TrainingWeekId"
                JOIN "Mesocycles" AS m ON m."Id" = w."MesocycleId"
                WHERE s."Id" = p."WorkoutSessionId";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BaseRepRangeMin",
                table: "ExercisePlans");

            migrationBuilder.DropColumn(
                name: "BaseRepRangeMax",
                table: "ExercisePlans");
        }
    }
}
