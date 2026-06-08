using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AttendanceSystem.API.Migrations
{
    /// <inheritdoc />
    public partial class AllowMultipleClockEventsPerDay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_ClockEvents_Employee_Type_Date",
                table: "ClockEvents");

            migrationBuilder.DropColumn(
                name: "EventDate",
                table: "ClockEvents");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "EventDate",
                table: "ClockEvents",
                type: "date",
                nullable: false,
                computedColumnSql: "CAST([Timestamp] AS DATE)",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "UX_ClockEvents_Employee_Type_Date",
                table: "ClockEvents",
                columns: new[] { "EmployeeId", "EventType", "EventDate" },
                unique: true);
        }
    }
}
