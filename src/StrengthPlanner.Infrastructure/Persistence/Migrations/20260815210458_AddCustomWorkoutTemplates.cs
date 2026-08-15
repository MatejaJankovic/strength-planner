using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StrengthPlanner.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomWorkoutTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserWorkoutTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserWorkoutTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserWorkoutTemplateDays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserWorkoutTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserWorkoutTemplateDays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserWorkoutTemplateDays_UserWorkoutTemplates_UserWorkoutTem~",
                        column: x => x.UserWorkoutTemplateId,
                        principalTable: "UserWorkoutTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserWorkoutTemplateExercises",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserWorkoutTemplateDayId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExerciseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Sets = table.Column<int>(type: "integer", nullable: false),
                    RepRangeMin = table.Column<int>(type: "integer", nullable: false),
                    RepRangeMax = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserWorkoutTemplateExercises", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserWorkoutTemplateExercises_Exercises_ExerciseId",
                        column: x => x.ExerciseId,
                        principalTable: "Exercises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserWorkoutTemplateExercises_UserWorkoutTemplateDays_UserWo~",
                        column: x => x.UserWorkoutTemplateDayId,
                        principalTable: "UserWorkoutTemplateDays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserWorkoutTemplateDays_UserWorkoutTemplateId",
                table: "UserWorkoutTemplateDays",
                column: "UserWorkoutTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_UserWorkoutTemplateExercises_ExerciseId",
                table: "UserWorkoutTemplateExercises",
                column: "ExerciseId");

            migrationBuilder.CreateIndex(
                name: "IX_UserWorkoutTemplateExercises_UserWorkoutTemplateDayId",
                table: "UserWorkoutTemplateExercises",
                column: "UserWorkoutTemplateDayId");

            migrationBuilder.CreateIndex(
                name: "IX_UserWorkoutTemplates_UserId",
                table: "UserWorkoutTemplates",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserWorkoutTemplateExercises");

            migrationBuilder.DropTable(
                name: "UserWorkoutTemplateDays");

            migrationBuilder.DropTable(
                name: "UserWorkoutTemplates");
        }
    }
}
