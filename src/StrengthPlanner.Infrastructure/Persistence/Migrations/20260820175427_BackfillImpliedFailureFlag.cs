using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StrengthPlanner.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Sets logged before <c>WorkingSet.ImpliesFailure</c> existed kept whatever the
    /// checkbox happened to say, so a set with zero reps in reserve below its exercise's
    /// rep range floor can still read <c>IsFailure = false</c> in the database. Brings
    /// those rows in line with the rule new writes already follow, so history-reading
    /// code that trusts the stored flag directly (auto-deload's fatigue signals, adaptive
    /// volume landmark learning) does not keep treating an old failed set as completed.
    /// </summary>
    public partial class BackfillImpliedFailureFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "SetLogs"
                SET "IsFailure" = TRUE
                FROM "ExercisePlans"
                WHERE "SetLogs"."ExercisePlanId" = "ExercisePlans"."Id"
                  AND "SetLogs"."IsFailure" = FALSE
                  AND "SetLogs"."Rir" = 0
                  AND "SetLogs"."Reps" < "ExercisePlans"."RepRangeMin";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Deliberately empty: nothing distinguishes a row this backfill flipped from
            // one that was already IsFailure = true for an unrelated reason, so there is
            // no way to tell which rows to flip back.
        }
    }
}
