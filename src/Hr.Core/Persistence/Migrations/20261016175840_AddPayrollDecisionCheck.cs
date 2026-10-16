using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hr.Core.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollDecisionCheck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_PayrollChange_Decision",
                table: "PayrollChanges",
                sql: "([Status] IN ('Draft','Pending') AND [DecidedByUserId] IS NULL) OR ([Status] IN ('Approved','Rejected') AND [DecidedByUserId] IS NOT NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PayrollChange_Decision",
                table: "PayrollChanges");
        }
    }
}
