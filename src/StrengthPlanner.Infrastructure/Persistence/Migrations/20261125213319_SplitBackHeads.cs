using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StrengthPlanner.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Splits the aggregate "Back" group into lats and upper back (R20), as "Shoulders" was split into three heads.
    ///
    /// The heads are inserted here rather than left to the seeder because the rows that move to them need the foreign
    /// key now. What moves, and why:
    /// <list type="bullet">
    /// <item>Learned landmarks of "Back" are carried to both heads as half of the learned drift. A learned landmark is
    /// a drift from its seed (at most 50% of it), the heads' seeds are 5/9/15 against the aggregate's 10/18/25, so a
    /// copy would mean something else: each head takes its own seed plus half of the aggregate's drift. The order
    /// MEV &lt; MAV &lt; MRV is restored where rounding broke it.</item>
    /// <item>A custom exercise's "Back" contribution moves to "Upper Back", with the same number. Which head a
    /// lifter's own exercise trains cannot be read from the row, and the app has no way to edit an exercise's muscles.
    /// "Upper Back" is chosen because rows are by far the commonest custom back exercise; a lifter who wants the lats
    /// recreates the exercise.</item>
    /// <item>System exercises lose their "Back" rows with the group, and the seeder's reconciliation writes the head
    /// contributions from the catalogue on the next start, as it does for every catalogue change.</item>
    /// </list>
    /// Measured on the development database before writing it: 25 learned "Back" landmarks, 8 system exercise rows and
    /// 6 custom exercise rows (all rows).
    /// </summary>
    public partial class SplitBackHeads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO "MuscleGroups" ("Id", "Name")
                SELECT gen_random_uuid(), heads.name
                FROM (VALUES ('Lats'), ('Upper Back')) AS heads(name)
                WHERE NOT EXISTS (SELECT 1 FROM "MuscleGroups" AS m WHERE m."Name" = heads.name);
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO "UserVolumeLandmarks" ("Id", "UserId", "MuscleGroupId", "Mev", "Mav", "Mrv", "UpdatedAt")
                SELECT gen_random_uuid(), r."UserId", r."HeadId", r.mev, r.mav, r.mrv, r."UpdatedAt"
                FROM (
                    SELECT d."UserId", d."UpdatedAt", d."HeadId", d.mev,
                           GREATEST(d.mav, d.mev + 1) AS mav,
                           GREATEST(d.mrv, GREATEST(d.mav, d.mev + 1) + 1) AS mrv
                    FROM (
                        SELECT u."UserId", u."UpdatedAt", head."Id" AS "HeadId",
                               GREATEST(1, ROUND(5 + (u."Mev" - s."Mev") / 2.0))::int AS mev,
                               ROUND(9 + (u."Mav" - s."Mav") / 2.0)::int AS mav,
                               ROUND(15 + (u."Mrv" - s."Mrv") / 2.0)::int AS mrv
                        FROM "UserVolumeLandmarks" AS u
                        JOIN "MuscleGroups" AS back ON back."Id" = u."MuscleGroupId" AND back."Name" = 'Back'
                        JOIN "VolumeLandmarks" AS s ON s."MuscleGroupId" = back."Id"
                        CROSS JOIN "MuscleGroups" AS head
                        WHERE head."Name" IN ('Lats', 'Upper Back')
                    ) AS d
                ) AS r
                WHERE NOT EXISTS (
                    SELECT 1 FROM "UserVolumeLandmarks" AS existing
                    WHERE existing."UserId" = r."UserId" AND existing."MuscleGroupId" = r."HeadId");
                """);

            migrationBuilder.Sql(
                """
                UPDATE "ExerciseMuscles" AS em
                SET "MuscleGroupId" = upper_back."Id"
                FROM "MuscleGroups" AS back, "MuscleGroups" AS upper_back, "Exercises" AS e
                WHERE back."Name" = 'Back'
                  AND upper_back."Name" = 'Upper Back'
                  AND em."MuscleGroupId" = back."Id"
                  AND e."Id" = em."ExerciseId"
                  AND e."IsCustom"
                  AND NOT EXISTS (
                      SELECT 1 FROM "ExerciseMuscles" AS existing
                      WHERE existing."ExerciseId" = em."ExerciseId" AND existing."MuscleGroupId" = upper_back."Id");
                """);

            // Cascades to its seed landmark, its learned landmarks and every exercise row still pointing at it.
            migrationBuilder.Sql(
                """
                DELETE FROM "MuscleGroups" WHERE "Name" = 'Back';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The reverse folds the heads back into one group. The aggregate's learned landmark is its seed plus the
            // drifts of both heads added (each was half of it); a head without a learned landmark counts as no drift.
            // A custom exercise keeps its largest head contribution.
            migrationBuilder.Sql(
                """
                INSERT INTO "MuscleGroups" ("Id", "Name")
                SELECT gen_random_uuid(), 'Back'
                WHERE NOT EXISTS (SELECT 1 FROM "MuscleGroups" WHERE "Name" = 'Back');
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO "UserVolumeLandmarks" ("Id", "UserId", "MuscleGroupId", "Mev", "Mav", "Mrv", "UpdatedAt")
                SELECT gen_random_uuid(), r."UserId", back."Id", r.mev,
                       GREATEST(r.mav, r.mev + 1),
                       GREATEST(r.mrv, GREATEST(r.mav, r.mev + 1) + 1),
                       r."UpdatedAt"
                FROM (
                    SELECT COALESCE(l."UserId", u."UserId") AS "UserId",
                           COALESCE(l."UpdatedAt", u."UpdatedAt") AS "UpdatedAt",
                           GREATEST(1, 10 + (COALESCE(l."Mev", 5) - 5) + (COALESCE(u."Mev", 5) - 5)) AS mev,
                           18 + (COALESCE(l."Mav", 9) - 9) + (COALESCE(u."Mav", 9) - 9) AS mav,
                           25 + (COALESCE(l."Mrv", 15) - 15) + (COALESCE(u."Mrv", 15) - 15) AS mrv
                    FROM (
                        SELECT x.* FROM "UserVolumeLandmarks" AS x
                        JOIN "MuscleGroups" AS g ON g."Id" = x."MuscleGroupId" AND g."Name" = 'Lats'
                    ) AS l
                    FULL JOIN (
                        SELECT x.* FROM "UserVolumeLandmarks" AS x
                        JOIN "MuscleGroups" AS g ON g."Id" = x."MuscleGroupId" AND g."Name" = 'Upper Back'
                    ) AS u ON u."UserId" = l."UserId"
                ) AS r
                CROSS JOIN "MuscleGroups" AS back
                WHERE back."Name" = 'Back'
                  AND NOT EXISTS (
                      SELECT 1 FROM "UserVolumeLandmarks" AS existing
                      WHERE existing."UserId" = r."UserId" AND existing."MuscleGroupId" = back."Id");
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO "ExerciseMuscles" ("ExerciseId", "MuscleGroupId", "Contribution")
                SELECT em."ExerciseId", back."Id", MAX(em."Contribution")
                FROM "ExerciseMuscles" AS em
                JOIN "MuscleGroups" AS head ON head."Id" = em."MuscleGroupId" AND head."Name" IN ('Lats', 'Upper Back')
                JOIN "Exercises" AS e ON e."Id" = em."ExerciseId" AND e."IsCustom"
                JOIN "MuscleGroups" AS back ON back."Name" = 'Back'
                WHERE NOT EXISTS (
                    SELECT 1 FROM "ExerciseMuscles" AS existing
                    WHERE existing."ExerciseId" = em."ExerciseId" AND existing."MuscleGroupId" = back."Id")
                GROUP BY em."ExerciseId", back."Id";
                """);

            migrationBuilder.Sql(
                """
                DELETE FROM "MuscleGroups" WHERE "Name" IN ('Lats', 'Upper Back');
                """);
        }
    }
}
