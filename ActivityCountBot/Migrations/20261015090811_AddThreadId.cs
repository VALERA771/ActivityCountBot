using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ActivityCountBot.Migrations
{
    /// <inheritdoc />
    public partial class AddThreadId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "thread_id",
                table: "messages",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "thread_id",
                table: "messages");
        }
    }
}
