using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthTrack.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGoalAndSessionIntegrityChecks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_WorkoutLogs_SessionPlan",
                table: "WorkoutLogs",
                sql: "[SessionId] IS NULL OR [WorkoutPlanId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Goal_CompletionValue",
                table: "FitnessGoals",
                sql: "[Status] <> N'Completed' OR [CompletedValue] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_WorkoutLogs_SessionPlan",
                table: "WorkoutLogs");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Goal_CompletionValue",
                table: "FitnessGoals");
        }
    }
}
