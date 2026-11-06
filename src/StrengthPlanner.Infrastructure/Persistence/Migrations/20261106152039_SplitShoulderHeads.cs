using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StrengthPlanner.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Splits the aggregate "Shoulders" group into the front, side and rear delt (R5).
    ///
    /// The heads are inserted here rather than left to the seeder because the rows that move
    /// to them need the foreign key now. What moves, and why:
    /// <list type="bullet">
    /// <item>Learned landmarks of "Shoulders" are copied to "Side Delts" unchanged. The side
    /// delt keeps the aggregate's seed (8/16/26) and a learned landmark is a drift from its
    /// seed, so the copy means the same thing it meant before. The front and rear delt start
    /// from their own seeds: nothing was ever learned about them separately.</item>
    /// <item>A custom exercise's "Shoulders" contribution moves to "Side Delts", which keeps
    /// it in the same budget, with the same numbers, as before. Which head a lifter's own
    /// exercise trains cannot be read from the row, and the app has no way to edit an
    /// exercise's muscles, so a lifter who wants another head recreates the exercise.</item>
    /// <item>System exercises lose their "Shoulders" rows with the group, and the seeder's
    /// reconciliation writes the head contributions from the catalogue on the next start,
    /// as it does for every catalogue change.</item>
    /// </list>
    /// Measured on the development database before writing it: 25 learned "Shoulders"
    /// landmarks, 5 system exercise rows and no custom exercise rows.
    ///
    /// MEV may now be zero: RP gives the front and rear delt an MEV of zero, because pressing
    /// and rowing already stimulate them and the model deliberately does not credit those sets
    /// to the heads.
    /// </summary>
    public partial class SplitShoulderHeads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_VolumeLandmarks_LandmarkOrder",
                table: "VolumeLandmarks");

            migrationBuilder.DropCheckConstraint(
                name: "CK_UserVolumeLandmarks_LandmarkOrder",
                table: "UserVolumeLandmarks");

            migrationBuilder.AddCheckConstraint(
                name: "CK_VolumeLandmarks_LandmarkOrder",
                table: "VolumeLandmarks",
                sql: "\"Mev\" >= 0 AND \"Mav\" > \"Mev\" AND \"Mrv\" > \"Mav\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserVolumeLandmarks_LandmarkOrder",
                table: "UserVolumeLandmarks",
                sql: "\"Mev\" >= 0 AND \"Mav\" > \"Mev\" AND \"Mrv\" > \"Mav\"");

            migrationBuilder.Sql(
                """
                INSERT INTO "MuscleGroups" ("Id", "Name")
                SELECT gen_random_uuid(), heads.name
                FROM (VALUES ('Front Delts'), ('Side Delts'), ('Rear Delts')) AS heads(name)
                WHERE NOT EXISTS (SELECT 1 FROM "MuscleGroups" AS m WHERE m."Name" = heads.name);
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO "UserVolumeLandmarks" ("Id", "UserId", "MuscleGroupId", "Mev", "Mav", "Mrv", "UpdatedAt")
                SELECT gen_random_uuid(), u."UserId", side."Id", u."Mev", u."Mav", u."Mrv", u."UpdatedAt"
                FROM "UserVolumeLandmarks" AS u
                JOIN "MuscleGroups" AS shoulders ON shoulders."Id" = u."MuscleGroupId" AND shoulders."Name" = 'Shoulders'
                JOIN "MuscleGroups" AS side ON side."Name" = 'Side Delts'
                WHERE NOT EXISTS (
                    SELECT 1 FROM "UserVolumeLandmarks" AS existing
                    WHERE existing."UserId" = u."UserId" AND existing."MuscleGroupId" = side."Id");
                """);

            migrationBuilder.Sql(
                """
                UPDATE "ExerciseMuscles" AS em
                SET "MuscleGroupId" = side."Id"
                FROM "MuscleGroups" AS shoulders, "MuscleGroups" AS side, "Exercises" AS e
                WHERE shoulders."Name" = 'Shoulders'
                  AND side."Name" = 'Side Delts'
                  AND em."MuscleGroupId" = shoulders."Id"
                  AND e."Id" = em."ExerciseId"
                  AND e."IsCustom"
                  AND NOT EXISTS (
                      SELECT 1 FROM "ExerciseMuscles" AS existing
                      WHERE existing."ExerciseId" = em."ExerciseId" AND existing."MuscleGroupId" = side."Id");
                """);

            // Cascades to its seed landmark, its learned landmarks and every exercise row
            // still pointing at it.
            migrationBuilder.Sql(
                """
                DELETE FROM "MuscleGroups" WHERE "Name" = 'Shoulders';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The reverse folds the heads back into one group. Learned side-delt landmarks
            // become the group's again; a custom exercise keeps its largest head contribution.
            // Zero MEVs are removed with the heads before the constraint asks for one set.
            migrationBuilder.Sql(
                """
                INSERT INTO "MuscleGroups" ("Id", "Name")
                SELECT gen_random_uuid(), 'Shoulders'
                WHERE NOT EXISTS (SELECT 1 FROM "MuscleGroups" WHERE "Name" = 'Shoulders');
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO "UserVolumeLandmarks" ("Id", "UserId", "MuscleGroupId", "Mev", "Mav", "Mrv", "UpdatedAt")
                SELECT gen_random_uuid(), u."UserId", shoulders."Id", u."Mev", u."Mav", u."Mrv", u."UpdatedAt"
                FROM "UserVolumeLandmarks" AS u
                JOIN "MuscleGroups" AS side ON side."Id" = u."MuscleGroupId" AND side."Name" = 'Side Delts'
                JOIN "MuscleGroups" AS shoulders ON shoulders."Name" = 'Shoulders'
                WHERE NOT EXISTS (
                    SELECT 1 FROM "UserVolumeLandmarks" AS existing
                    WHERE existing."UserId" = u."UserId" AND existing."MuscleGroupId" = shoulders."Id");
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO "ExerciseMuscles" ("ExerciseId", "MuscleGroupId", "Contribution")
                SELECT em."ExerciseId", shoulders."Id", MAX(em."Contribution")
                FROM "ExerciseMuscles" AS em
                JOIN "MuscleGroups" AS head ON head."Id" = em."MuscleGroupId"
                    AND head."Name" IN ('Front Delts', 'Side Delts', 'Rear Delts')
                JOIN "Exercises" AS e ON e."Id" = em."ExerciseId" AND e."IsCustom"
                JOIN "MuscleGroups" AS shoulders ON shoulders."Name" = 'Shoulders'
                WHERE NOT EXISTS (
                    SELECT 1 FROM "ExerciseMuscles" AS existing
                    WHERE existing."ExerciseId" = em."ExerciseId" AND existing."MuscleGroupId" = shoulders."Id")
                GROUP BY em."ExerciseId", shoulders."Id";
                """);

            migrationBuilder.Sql(
                """
                DELETE FROM "MuscleGroups" WHERE "Name" IN ('Front Delts', 'Side Delts', 'Rear Delts');
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_VolumeLandmarks_LandmarkOrder",
                table: "VolumeLandmarks");

            migrationBuilder.DropCheckConstraint(
                name: "CK_UserVolumeLandmarks_LandmarkOrder",
                table: "UserVolumeLandmarks");

            migrationBuilder.AddCheckConstraint(
                name: "CK_VolumeLandmarks_LandmarkOrder",
                table: "VolumeLandmarks",
                sql: "\"Mev\" >= 1 AND \"Mav\" > \"Mev\" AND \"Mrv\" > \"Mav\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_UserVolumeLandmarks_LandmarkOrder",
                table: "UserVolumeLandmarks",
                sql: "\"Mev\" >= 1 AND \"Mav\" > \"Mev\" AND \"Mrv\" > \"Mav\"");
        }
    }
}
