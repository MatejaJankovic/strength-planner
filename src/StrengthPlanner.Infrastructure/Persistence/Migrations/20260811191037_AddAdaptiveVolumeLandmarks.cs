using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StrengthPlanner.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAdaptiveVolumeLandmarks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserVolumeLandmarks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MuscleGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Mev = table.Column<int>(type: "integer", nullable: false),
                    Mrv = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserVolumeLandmarks", x => x.Id);
                    table.CheckConstraint("CK_UserVolumeLandmarks_MevBelowMrv", "\"Mev\" >= 1 AND \"Mrv\" > \"Mev\"");
                    table.ForeignKey(
                        name: "FK_UserVolumeLandmarks_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserVolumeLandmarks_MuscleGroups_MuscleGroupId",
                        column: x => x.MuscleGroupId,
                        principalTable: "MuscleGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserVolumeLandmarks_MuscleGroupId",
                table: "UserVolumeLandmarks",
                column: "MuscleGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_UserVolumeLandmarks_UserId_MuscleGroupId",
                table: "UserVolumeLandmarks",
                columns: new[] { "UserId", "MuscleGroupId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserVolumeLandmarks");
        }
    }
}
