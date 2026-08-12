using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StrengthPlanner.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMaxAdaptiveVolume : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_UserVolumeLandmarks_MevBelowMrv",
                table: "UserVolumeLandmarks");

            migrationBuilder.AddColumn<int>(
                name: "Mav",
                table: "VolumeLandmarks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Mav",
                table: "UserVolumeLandmarks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Kolone se dodaju sa nulom, a novo ogranicenje trazi Mev < Mav < Mrv —
            // zato se MAV mora popuniti PRE nego sto ogranicenje pocne da vazi, inace
            // migracija pada na svakoj bazi koja vec ima naucene granice.

            // Pojas mora biti bar dve serije sirok da bi MAV imao gde da stane.
            migrationBuilder.Sql("""
                UPDATE "VolumeLandmarks" SET "Mrv" = "Mev" + 2 WHERE "Mrv" - "Mev" < 2;
                UPDATE "UserVolumeLandmarks" SET "Mrv" = "Mev" + 2 WHERE "Mrv" - "Mev" < 2;
                """);

            // Seed tabela dobija vrednosti iz prirucnika (raspon 8-20 serija nedeljno);
            // nepoznata misicna grupa pada na sredinu svog pojasa.
            migrationBuilder.Sql("""
                UPDATE "VolumeLandmarks" v
                SET "Mav" = CASE m."Name"
                    WHEN 'Chest' THEN 16
                    WHEN 'Back' THEN 18
                    WHEN 'Shoulders' THEN 16
                    WHEN 'Quads' THEN 14
                    WHEN 'Hamstrings' THEN 11
                    WHEN 'Glutes' THEN 10
                    WHEN 'Biceps' THEN 14
                    WHEN 'Triceps' THEN 12
                    WHEN 'Calves' THEN 13
                    WHEN 'Abs' THEN 12
                    ELSE (v."Mev" + v."Mrv") / 2
                END
                FROM "MuscleGroups" m
                WHERE m."Id" = v."MuscleGroupId";
                """);

            // Naucene granice su licne, pa se za njih ne uzima vrednost iz prirucnika
            // nego sredina korisnikovog sopstvenog pojasa.
            migrationBuilder.Sql("""
                UPDATE "UserVolumeLandmarks"
                SET "Mav" = ("Mev" + "Mrv") / 2;
                """);

            // Seed vrednost iz CASE-a moze da ispadne van suzenog licnog pojasa.
            migrationBuilder.Sql("""
                UPDATE "VolumeLandmarks"
                SET "Mav" = GREATEST("Mev" + 1, LEAST("Mrv" - 1, "Mav"));
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserVolumeLandmarks_MevBelowMrv",
                table: "UserVolumeLandmarks",
                sql: "\"Mev\" >= 1 AND \"Mav\" > \"Mev\" AND \"Mrv\" > \"Mav\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_UserVolumeLandmarks_MevBelowMrv",
                table: "UserVolumeLandmarks");

            migrationBuilder.DropColumn(
                name: "Mav",
                table: "VolumeLandmarks");

            migrationBuilder.DropColumn(
                name: "Mav",
                table: "UserVolumeLandmarks");

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserVolumeLandmarks_MevBelowMrv",
                table: "UserVolumeLandmarks",
                sql: "\"Mev\" >= 1 AND \"Mrv\" > \"Mev\"");
        }
    }
}
