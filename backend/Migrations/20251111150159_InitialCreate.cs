using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BelfastBinsApi.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BinSchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Route = table.Column<string>(type: "TEXT", nullable: false),
                    BinType = table.Column<string>(type: "TEXT", nullable: false),
                    DayOfWeek = table.Column<string>(type: "TEXT", nullable: false),
                    WeekCycle = table.Column<string>(type: "TEXT", nullable: false),
                    HouseNumber = table.Column<string>(type: "TEXT", nullable: false),
                    HouseSuffix = table.Column<string>(type: "TEXT", nullable: false),
                    Street = table.Column<string>(type: "TEXT", nullable: false),
                    StreetNormalized = table.Column<string>(type: "TEXT", nullable: false),
                    City = table.Column<string>(type: "TEXT", nullable: false),
                    County = table.Column<string>(type: "TEXT", nullable: false),
                    Postcode = table.Column<string>(type: "TEXT", nullable: false),
                    PostcodeNormalized = table.Column<string>(type: "TEXT", nullable: false),
                    FullAddress = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BinSchedules", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BinSchedules_PostcodeNormalized",
                table: "BinSchedules",
                column: "PostcodeNormalized");

            migrationBuilder.CreateIndex(
                name: "IX_BinSchedules_PostcodeNormalized_HouseNumber",
                table: "BinSchedules",
                columns: new[] { "PostcodeNormalized", "HouseNumber" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BinSchedules");
        }
    }
}
