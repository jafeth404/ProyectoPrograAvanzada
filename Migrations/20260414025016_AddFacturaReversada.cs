using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace proyectoprogra.Migrations
{
    /// <inheritdoc />
    public partial class AddFacturaReversada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Reversada",
                table: "Facturas",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Reversada",
                table: "Facturas");
        }
    }
}
