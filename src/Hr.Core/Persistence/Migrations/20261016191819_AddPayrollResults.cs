using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hr.Core.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PayrollResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    BaseSalary = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FixedAllowance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NormHours = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Overtime50Hours = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Overtime100Hours = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NormalOvertimePay = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Allowance50 = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Allowance100 = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Gross = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RuleIds = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CalculatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CalculatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollResults", x => x.Id);
                    table.CheckConstraint("CK_PayrollResult_Month", "[Month] BETWEEN 1 AND 12");
                    table.ForeignKey(
                        name: "FK_PayrollResults_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollResults_EmployeeId_Year_Month",
                table: "PayrollResults",
                columns: new[] { "EmployeeId", "Year", "Month" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayrollResults");
        }
    }
}
