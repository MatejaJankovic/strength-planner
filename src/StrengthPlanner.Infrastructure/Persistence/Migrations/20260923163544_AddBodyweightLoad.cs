using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StrengthPlanner.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Adds the two columns that let a bodyweight exercise carry a load: the share of body
    /// mass an exercise lifts, and the kilograms of it one logged set actually moved.
    ///
    /// Only the shares of the catalogue exercises are written here. The snapshot column is
    /// left at zero for every existing row on purpose, and the reason is a measurement
    /// rather than caution: in the development database not one bodyweight set was logged
    /// at 0 kg. All 119 of them carry 38 to 77 kg, because the app demanded a number and
    /// the lifter typed what the whole movement felt like. Adding body mass on top of that
    /// would have counted the same body twice and pushed a pull-up estimate from 90 kg to
    /// about 200. History therefore keeps meaning exactly what it meant when it was written.
    ///
    /// Plans of sessions that have <b>not</b> been performed yet are the one exception, and
    /// they are converted rather than left alone: their target used to be the whole load
    /// and now means what is added to the body, so a pull-up planned at 40 kg would have
    /// read as "put 40 kg on your belt". Subtracting the body portion turns that into "your
    /// own body", clamped at zero. Completed sessions are history and are not touched.
    /// </summary>
    public partial class AddBodyweightLoad : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BodyweightLoadKg",
                table: "SetLogs",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "BodyweightShare",
                table: "Exercises",
                type: "numeric(3,2)",
                precision: 3,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            // Shares of the catalogue exercises, matched by name because that is what the
            // seeder identifies a system exercise by. DbSeeder reconciles the same values on
            // every start; this is here so a database that is migrated but never seeded is
            // still correct. A lifter's own exercise carries no share: the shares are
            // estimates made once, for everyone, and nobody estimated theirs.
            migrationBuilder.Sql("""
                UPDATE "Exercises" SET "BodyweightShare" = 1.00
                WHERE "Name" = 'Pull-up' AND NOT "IsCustom";

                UPDATE "Exercises" SET "BodyweightShare" = 0.64
                WHERE "Name" = 'Push-up' AND NOT "IsCustom";

                UPDATE "Exercises" SET "BodyweightShare" = 0.85
                WHERE "Name" = 'Split Squat' AND NOT "IsCustom";
                """);

            // A plank holds the body still; there is no rep whose load could be estimated,
            // so its share stays zero and it is no longer used by any built-in template.
            migrationBuilder.Sql("""
                UPDATE "ExercisePlans"
                SET "TargetWeightKg" = GREATEST(
                    0,
                    "ExercisePlans"."TargetWeightKg"
                        - ROUND("Profiles"."BodyweightKg" * "Exercises"."BodyweightShare", 2))
                FROM "WorkoutSessions", "TrainingWeeks", "Mesocycles", "Profiles", "Exercises"
                WHERE "ExercisePlans"."WorkoutSessionId" = "WorkoutSessions"."Id"
                  AND "WorkoutSessions"."TrainingWeekId" = "TrainingWeeks"."Id"
                  AND "TrainingWeeks"."MesocycleId" = "Mesocycles"."Id"
                  AND "Profiles"."UserId" = "Mesocycles"."UserId"
                  AND "Exercises"."Id" = "ExercisePlans"."ExerciseId"
                  AND "Exercises"."BodyweightShare" > 0
                  AND "ExercisePlans"."TargetWeightKg" IS NOT NULL
                  AND "WorkoutSessions"."Status" <> 'Completed';
                """);

            // One record in the development database holds 0 kg, from a bodyweight exercise
            // whose sets read as no load at all. The domain rule already refuses a
            // non-positive sample, so nothing plans from it any more, but the history screen
            // still lists it as a maximum the lifter once had.
            migrationBuilder.Sql("""
                DELETE FROM "OneRepMaxRecords" WHERE "ValueKg" <= 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The columns go; what was written to them cannot be recovered, and neither can
            // the deleted records or the plan targets that were converted. Same shape as
            // BackfillImpliedFailureFlag: reversing the schema is possible, reversing the
            // data is not.
            migrationBuilder.DropColumn(
                name: "BodyweightLoadKg",
                table: "SetLogs");

            migrationBuilder.DropColumn(
                name: "BodyweightShare",
                table: "Exercises");
        }
    }
}
