using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StrengthPlanner.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Turns Profiles.Sex from free text into the Sex enum, and drops TrainingDaysPerWeek.
    ///
    /// The scaffolded AlterColumn is deliberately replaced by hand: PostgreSQL cannot cast
    /// 'male' to integer, so the generated version fails on any database that has rows, and
    /// a USING clause would not help either. The column is converted through a temporary one
    /// so the values already stored are mapped instead of lost.
    /// </summary>
    public partial class SexAsEnumAndDropTrainingDays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TrainingDaysPerWeek",
                table: "Profiles");

            migrationBuilder.Sql("""ALTER TABLE "Profiles" ADD COLUMN "SexValue" integer NULL;""");

            // Registration wrote 'male'/'female'; the profile screen wrote 'M'/'F'. Both sets
            // are real data in this database, so both are mapped. Anything else was never a
            // value any screen could display, and becomes "not stated" rather than a guess.
            migrationBuilder.Sql(
                """
                UPDATE "Profiles"
                SET "SexValue" = CASE lower("Sex")
                    WHEN 'male' THEN 0
                    WHEN 'm' THEN 0
                    WHEN 'female' THEN 1
                    WHEN 'f' THEN 1
                    ELSE NULL
                END
                WHERE "Sex" IS NOT NULL;
                """);

            migrationBuilder.Sql("""ALTER TABLE "Profiles" DROP COLUMN "Sex";""");
            migrationBuilder.Sql("""ALTER TABLE "Profiles" RENAME COLUMN "SexValue" TO "Sex";""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """ALTER TABLE "Profiles" ADD COLUMN "SexText" character varying(16) NULL;""");

            // Rolling back writes the registration spelling, which is the one the old code
            // produced for new accounts.
            migrationBuilder.Sql(
                """
                UPDATE "Profiles"
                SET "SexText" = CASE "Sex"
                    WHEN 0 THEN 'male'
                    WHEN 1 THEN 'female'
                    ELSE NULL
                END
                WHERE "Sex" IS NOT NULL;
                """);

            migrationBuilder.Sql("""ALTER TABLE "Profiles" DROP COLUMN "Sex";""");
            migrationBuilder.Sql("""ALTER TABLE "Profiles" RENAME COLUMN "SexText" TO "Sex";""");

            // The values are gone with the column, so a rollback has to invent one. Three is
            // what the form defaulted to, not zero, which the old [Range(1, 7)] rejected.
            migrationBuilder.AddColumn<int>(
                name: "TrainingDaysPerWeek",
                table: "Profiles",
                type: "integer",
                nullable: false,
                defaultValue: 3);
        }
    }
}
