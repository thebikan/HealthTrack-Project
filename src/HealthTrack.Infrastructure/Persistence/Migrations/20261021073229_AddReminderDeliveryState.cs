using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthTrack.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReminderDeliveryState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NotificationSchedules_DeliveredAtUtc_DueAtUtc",
                table: "NotificationSchedules");

            migrationBuilder.AddColumn<int>(
                name: "AppointmentId",
                table: "NotificationSchedules",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCancelled",
                table: "NotificationSchedules",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextAttemptAtUtc",
                table: "NotificationSchedules",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationSchedules_AppointmentId",
                table: "NotificationSchedules",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationSchedules_IsCancelled_DeliveredAtUtc_NextAttemptAtUtc_DueAtUtc",
                table: "NotificationSchedules",
                columns: new[] { "IsCancelled", "DeliveredAtUtc", "NextAttemptAtUtc", "DueAtUtc" });

            migrationBuilder.AddForeignKey(
                name: "FK_NotificationSchedules_Appointments_AppointmentId",
                table: "NotificationSchedules",
                column: "AppointmentId",
                principalTable: "Appointments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NotificationSchedules_Appointments_AppointmentId",
                table: "NotificationSchedules");

            migrationBuilder.DropIndex(
                name: "IX_NotificationSchedules_AppointmentId",
                table: "NotificationSchedules");

            migrationBuilder.DropIndex(
                name: "IX_NotificationSchedules_IsCancelled_DeliveredAtUtc_NextAttemptAtUtc_DueAtUtc",
                table: "NotificationSchedules");

            migrationBuilder.DropColumn(
                name: "AppointmentId",
                table: "NotificationSchedules");

            migrationBuilder.DropColumn(
                name: "IsCancelled",
                table: "NotificationSchedules");

            migrationBuilder.DropColumn(
                name: "NextAttemptAtUtc",
                table: "NotificationSchedules");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationSchedules_DeliveredAtUtc_DueAtUtc",
                table: "NotificationSchedules",
                columns: new[] { "DeliveredAtUtc", "DueAtUtc" });
        }
    }
}
