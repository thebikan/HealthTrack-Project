using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthTrack.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGoalsPreferencesAndMealTimes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeOnly>(
                name: "PlannedTimeUtc",
                table: "DietPlanMeals",
                type: "time",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FitnessGoals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClientUserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Baseline = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: true),
                    Target = table.Column<decimal>(type: "decimal(6,2)", precision: 6, scale: 2, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TargetDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FitnessGoals", x => x.Id);
                    table.CheckConstraint("CK_Goal_Dates", "[TargetDate] >= [StartDate]");
                    table.CheckConstraint("CK_Goal_Status", "([Status] = N'Completed' AND [CompletedAtUtc] IS NOT NULL) OR ([Status] IN (N'Active', N'Cancelled') AND [CompletedAtUtc] IS NULL)");
                    table.CheckConstraint("CK_Goal_Target", "[Target] > 0 AND ([Baseline] IS NULL OR [Baseline] > 0)");
                    table.CheckConstraint("CK_Goal_Type", "[Type] IN (N'WeightReduction', N'WeightGain', N'WorkoutConsistency', N'WorkoutCompletion')");
                    table.ForeignKey(
                        name: "FK_FitnessGoals_ClientProfiles_ClientUserId",
                        column: x => x.ClientUserId,
                        principalTable: "ClientProfiles",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NotificationPreferences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    WorkoutReminders = table.Column<bool>(type: "bit", nullable: false),
                    MealReminders = table.Column<bool>(type: "bit", nullable: false),
                    AppointmentReminders = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationPreferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationPreferences_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FitnessGoals_ClientUserId_Status",
                table: "FitnessGoals",
                columns: new[] { "ClientUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationPreferences_UserId",
                table: "NotificationPreferences",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FitnessGoals");

            migrationBuilder.DropTable(
                name: "NotificationPreferences");

            migrationBuilder.DropColumn(
                name: "PlannedTimeUtc",
                table: "DietPlanMeals");
        }
    }
}
