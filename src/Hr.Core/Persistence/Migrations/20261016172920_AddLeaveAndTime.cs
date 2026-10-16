using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hr.Core.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaveAndTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LeaveRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    From = table.Column<DateOnly>(type: "date", nullable: false),
                    To = table.Column<DateOnly>(type: "date", nullable: false),
                    Days = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DecidedByEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DecisionReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveRequests", x => x.Id);
                    table.CheckConstraint("CK_LeaveRequest_Days", "[Days] > 0");
                    table.CheckConstraint("CK_LeaveRequest_Period", "[To] >= [From]");
                    table.ForeignKey(
                        name: "FK_LeaveRequests_Employees_DecidedByEmployeeId",
                        column: x => x.DecidedByEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LeaveRequests_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "WorkEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Start = table.Column<DateTime>(type: "datetime2", nullable: false),
                    End = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Break = table.Column<TimeSpan>(type: "time", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkEntries", x => x.Id);
                    table.CheckConstraint("CK_WorkEntry_Break", "[Break] >= '00:00:00'");
                    table.CheckConstraint("CK_WorkEntry_Period", "[End] > [Start]");
                    table.ForeignKey(
                        name: "FK_WorkEntries_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_DecidedByEmployeeId",
                table: "LeaveRequests",
                column: "DecidedByEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_EmployeeId_From",
                table: "LeaveRequests",
                columns: new[] { "EmployeeId", "From" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkEntries_EmployeeId_Start",
                table: "WorkEntries",
                columns: new[] { "EmployeeId", "Start" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeaveRequests");

            migrationBuilder.DropTable(
                name: "WorkEntries");
        }
    }
}
