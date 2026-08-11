using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StrengthPlanner.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMacrocycles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Macrocycles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Macrocycles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Macrocycles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MacrocycleBlocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MacrocycleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Goal = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TemplateKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    MesocycleId = table.Column<Guid>(type: "uuid", nullable: true),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MacrocycleBlocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MacrocycleBlocks_Macrocycles_MacrocycleId",
                        column: x => x.MacrocycleId,
                        principalTable: "Macrocycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MacrocycleBlocks_Mesocycles_MesocycleId",
                        column: x => x.MesocycleId,
                        principalTable: "Mesocycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MacrocycleBlocks_MacrocycleId_Order",
                table: "MacrocycleBlocks",
                columns: new[] { "MacrocycleId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MacrocycleBlocks_MesocycleId",
                table: "MacrocycleBlocks",
                column: "MesocycleId",
                unique: true,
                filter: "\"MesocycleId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Macrocycles_UserId_IsActive",
                table: "Macrocycles",
                columns: new[] { "UserId", "IsActive" });

            // Svaki mezociklus od sada pripada dugorocnom planu. Zatecene redove
            // pretvaramo u planove sa jednim blokom: Id plana je namerno isti kao Id
            // mezociklusa, pa je veza jednoznacna bez privremenih tabela.
            migrationBuilder.Sql("""
                INSERT INTO "Macrocycles" ("Id", "UserId", "Name", "StartDate", "IsActive")
                SELECT m."Id", m."UserId", m."Name", m."StartDate", m."IsActive"
                FROM "Mesocycles" m;
                """);

            // Sablon nije bio cuvan uz mezociklus, ali se jednoznacno prepoznaje po
            // nazivima dana koje je generator upisao.
            migrationBuilder.Sql("""
                INSERT INTO "MacrocycleBlocks"
                    ("Id", "MacrocycleId", "Order", "Goal", "TemplateKey", "MesocycleId", "GeneratedAt")
                SELECT
                    gen_random_uuid(),
                    m."Id",
                    1,
                    m."Goal",
                    CASE
                        WHEN EXISTS (
                            SELECT 1 FROM "TrainingWeeks" w
                            JOIN "WorkoutSessions" s ON s."TrainingWeekId" = w."Id"
                            WHERE w."MesocycleId" = m."Id" AND s."DayLabel" = 'Push')
                            THEN 'push-pull-legs'
                        WHEN EXISTS (
                            SELECT 1 FROM "TrainingWeeks" w
                            JOIN "WorkoutSessions" s ON s."TrainingWeekId" = w."Id"
                            WHERE w."MesocycleId" = m."Id" AND s."DayLabel" = 'Upper A')
                            THEN 'upper-lower'
                        ELSE 'full-body'
                    END,
                    m."Id",
                    now()
                FROM "Mesocycles" m;
                """);
        }


        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MacrocycleBlocks");

            migrationBuilder.DropTable(
                name: "Macrocycles");
        }
    }
}
