using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hr.Core.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLabourCodeArticles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LabourCodeArticles",
                columns: table => new
                {
                    Number = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabourCodeArticles", x => x.Number);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LabourCodeArticles");
        }
    }
}
