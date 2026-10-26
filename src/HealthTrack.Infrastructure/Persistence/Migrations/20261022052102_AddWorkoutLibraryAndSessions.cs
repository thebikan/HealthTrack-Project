using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthTrack.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkoutLibraryAndSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PreviousPlanId",
                table: "WorkoutPlans",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SessionId",
                table: "WorkoutLogs",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WorkoutSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkoutPlanId = table.Column<int>(type: "int", nullable: false),
                    ClientUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ScheduledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkoutSessions", x => x.Id);
                    table.UniqueConstraint("AK_WorkoutSessions_Id_WorkoutPlanId_ClientUserId", x => new { x.Id, x.WorkoutPlanId, x.ClientUserId });
                    table.ForeignKey(
                        name: "FK_WorkoutSessions_ClientProfiles_ClientUserId",
                        column: x => x.ClientUserId,
                        principalTable: "ClientProfiles",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkoutSessions_WorkoutPlans_WorkoutPlanId_ClientUserId",
                        columns: x => new { x.WorkoutPlanId, x.ClientUserId },
                        principalTable: "WorkoutPlans",
                        principalColumns: new[] { "Id", "ClientUserId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkoutTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TrainerUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkoutTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkoutTemplates_TrainerProfiles_TrainerUserId",
                        column: x => x.TrainerUserId,
                        principalTable: "TrainerProfiles",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkoutTemplateExercises",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkoutTemplateId = table.Column<int>(type: "int", nullable: false),
                    ExerciseName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Sets = table.Column<int>(type: "int", nullable: true),
                    Reps = table.Column<int>(type: "int", nullable: true),
                    DurationMinutes = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkoutTemplateExercises", x => x.Id);
                    table.CheckConstraint("CK_TemplateExercise_Target", "([Reps] IS NOT NULL OR [DurationMinutes] IS NOT NULL) AND ([Sets] IS NULL OR [Sets] > 0) AND ([Reps] IS NULL OR [Reps] > 0) AND ([DurationMinutes] IS NULL OR [DurationMinutes] > 0)");
                    table.ForeignKey(
                        name: "FK_WorkoutTemplateExercises_WorkoutTemplates_WorkoutTemplateId",
                        column: x => x.WorkoutTemplateId,
                        principalTable: "WorkoutTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutPlans_PreviousPlanId",
                table: "WorkoutPlans",
                column: "PreviousPlanId",
                unique: true,
                filter: "[PreviousPlanId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutPlans_PreviousPlanId_ClientUserId",
                table: "WorkoutPlans",
                columns: new[] { "PreviousPlanId", "ClientUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutLogs_SessionId",
                table: "WorkoutLogs",
                column: "SessionId",
                unique: true,
                filter: "[SessionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutLogs_SessionId_WorkoutPlanId_ClientUserId",
                table: "WorkoutLogs",
                columns: new[] { "SessionId", "WorkoutPlanId", "ClientUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutSessions_ClientUserId_ScheduledAtUtc",
                table: "WorkoutSessions",
                columns: new[] { "ClientUserId", "ScheduledAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutSessions_WorkoutPlanId_ClientUserId",
                table: "WorkoutSessions",
                columns: new[] { "WorkoutPlanId", "ClientUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutSessions_WorkoutPlanId_ScheduledAtUtc",
                table: "WorkoutSessions",
                columns: new[] { "WorkoutPlanId", "ScheduledAtUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutTemplateExercises_WorkoutTemplateId",
                table: "WorkoutTemplateExercises",
                column: "WorkoutTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkoutTemplates_TrainerUserId_IsArchived",
                table: "WorkoutTemplates",
                columns: new[] { "TrainerUserId", "IsArchived" });

            migrationBuilder.AddForeignKey(
                name: "FK_WorkoutLogs_WorkoutSessions_SessionId_WorkoutPlanId_ClientUserId",
                table: "WorkoutLogs",
                columns: new[] { "SessionId", "WorkoutPlanId", "ClientUserId" },
                principalTable: "WorkoutSessions",
                principalColumns: new[] { "Id", "WorkoutPlanId", "ClientUserId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkoutPlans_WorkoutPlans_PreviousPlanId_ClientUserId",
                table: "WorkoutPlans",
                columns: new[] { "PreviousPlanId", "ClientUserId" },
                principalTable: "WorkoutPlans",
                principalColumns: new[] { "Id", "ClientUserId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkoutLogs_WorkoutSessions_SessionId_WorkoutPlanId_ClientUserId",
                table: "WorkoutLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkoutPlans_WorkoutPlans_PreviousPlanId_ClientUserId",
                table: "WorkoutPlans");

            migrationBuilder.DropTable(
                name: "WorkoutSessions");

            migrationBuilder.DropTable(
                name: "WorkoutTemplateExercises");

            migrationBuilder.DropTable(
                name: "WorkoutTemplates");

            migrationBuilder.DropIndex(
                name: "IX_WorkoutPlans_PreviousPlanId",
                table: "WorkoutPlans");

            migrationBuilder.DropIndex(
                name: "IX_WorkoutPlans_PreviousPlanId_ClientUserId",
                table: "WorkoutPlans");

            migrationBuilder.DropIndex(
                name: "IX_WorkoutLogs_SessionId",
                table: "WorkoutLogs");

            migrationBuilder.DropIndex(
                name: "IX_WorkoutLogs_SessionId_WorkoutPlanId_ClientUserId",
                table: "WorkoutLogs");

            migrationBuilder.DropColumn(
                name: "PreviousPlanId",
                table: "WorkoutPlans");

            migrationBuilder.DropColumn(
                name: "SessionId",
                table: "WorkoutLogs");
        }
    }
}
