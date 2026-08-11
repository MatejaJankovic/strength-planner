using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StrengthPlanner.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPerExerciseWeightStep : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "WeightStepKg",
                table: "Exercises",
                type: "numeric(6,2)",
                precision: 6,
                scale: 2,
                nullable: false,
                defaultValue: 2.5m);

            // Postojeci redovi dobijaju korak izveden iz sprave. Pre ove migracije korak
            // nije postojao kao pojam, pa se ovim nista korisnicko ne gazi -- korisnicka
            // odstupanja od ovog trenutka zive u UserExerciseSettings.
            migrationBuilder.Sql("""
                UPDATE "Exercises"
                SET "WeightStepKg" = CASE lower(trim("Equipment"))
                    WHEN 'barbell' THEN 2.5
                    WHEN 'dumbbell' THEN 2.0
                    WHEN 'machine' THEN 5.0
                    WHEN 'cable' THEN 2.5
                    WHEN 'bodyweight' THEN 1.0
                    ELSE 2.5
                END;
                """);

            migrationBuilder.CreateTable(
                name: "UserExerciseSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExerciseId = table.Column<Guid>(type: "uuid", nullable: false),
                    WeightStepKg = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserExerciseSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserExerciseSettings_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserExerciseSettings_Exercises_ExerciseId",
                        column: x => x.ExerciseId,
                        principalTable: "Exercises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserExerciseSettings_ExerciseId",
                table: "UserExerciseSettings",
                column: "ExerciseId");

            migrationBuilder.CreateIndex(
                name: "IX_UserExerciseSettings_UserId_ExerciseId",
                table: "UserExerciseSettings",
                columns: new[] { "UserId", "ExerciseId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserExerciseSettings");

            migrationBuilder.DropColumn(
                name: "WeightStepKg",
                table: "Exercises");
        }
    }
}
