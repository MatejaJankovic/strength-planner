using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StrengthPlanner.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EditableCompletedWorkouts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "WorkoutSessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RevisionPending",
                table: "WorkoutSessions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "WorkoutSessionId",
                table: "OneRepMaxRecords",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "VolumeLandmarkChanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TrainingWeekId = table.Column<Guid>(type: "uuid", nullable: false),
                    MuscleGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    MevDelta = table.Column<int>(type: "integer", nullable: false),
                    MavDelta = table.Column<int>(type: "integer", nullable: false),
                    MrvDelta = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VolumeLandmarkChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VolumeLandmarkChanges_MuscleGroups_MuscleGroupId",
                        column: x => x.MuscleGroupId,
                        principalTable: "MuscleGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VolumeLandmarkChanges_TrainingWeeks_TrainingWeekId",
                        column: x => x.TrainingWeekId,
                        principalTable: "TrainingWeeks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OneRepMaxRecords_WorkoutSessionId",
                table: "OneRepMaxRecords",
                column: "WorkoutSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_VolumeLandmarkChanges_MuscleGroupId",
                table: "VolumeLandmarkChanges",
                column: "MuscleGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_VolumeLandmarkChanges_TrainingWeekId_MuscleGroupId",
                table: "VolumeLandmarkChanges",
                columns: new[] { "TrainingWeekId", "MuscleGroupId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_OneRepMaxRecords_WorkoutSessions_WorkoutSessionId",
                table: "OneRepMaxRecords",
                column: "WorkoutSessionId",
                principalTable: "WorkoutSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OneRepMaxRecords_WorkoutSessions_WorkoutSessionId",
                table: "OneRepMaxRecords");

            migrationBuilder.DropTable(
                name: "VolumeLandmarkChanges");

            migrationBuilder.DropIndex(
                name: "IX_OneRepMaxRecords_WorkoutSessionId",
                table: "OneRepMaxRecords");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "WorkoutSessions");

            migrationBuilder.DropColumn(
                name: "RevisionPending",
                table: "WorkoutSessions");

            migrationBuilder.DropColumn(
                name: "WorkoutSessionId",
                table: "OneRepMaxRecords");
        }
    }
}
