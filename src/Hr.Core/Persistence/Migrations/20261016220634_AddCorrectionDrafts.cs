using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hr.Core.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCorrectionDrafts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CorrectionDrafts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProposedStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProposedEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProposedBreak = table.Column<TimeSpan>(type: "time", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DecidedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecisionNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorrectionDrafts", x => x.Id);
                    table.CheckConstraint("CK_CorrectionDraft_Period", "[ProposedEnd] > [ProposedStart]");
                    table.ForeignKey(
                        name: "FK_CorrectionDrafts_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CorrectionDrafts_WorkEntries_WorkEntryId",
                        column: x => x.WorkEntryId,
                        principalTable: "WorkEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CorrectionDrafts_EmployeeId",
                table: "CorrectionDrafts",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_CorrectionDrafts_Status_CreatedAtUtc",
                table: "CorrectionDrafts",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CorrectionDrafts_WorkEntryId",
                table: "CorrectionDrafts",
                column: "WorkEntryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CorrectionDrafts");
        }
    }
}
