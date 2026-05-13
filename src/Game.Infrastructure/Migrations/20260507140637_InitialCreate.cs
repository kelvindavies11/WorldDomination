using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Game.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Games",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    MapArea = table.Column<string>(type: "TEXT", nullable: false),
                    MaxHumanPlayers = table.Column<int>(type: "INTEGER", nullable: false),
                    NpcFactions = table.Column<int>(type: "INTEGER", nullable: false),
                    TerritoryCount = table.Column<int>(type: "INTEGER", nullable: false),
                    WinningControlPercentage = table.Column<double>(type: "REAL", nullable: false),
                    IsStarted = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsEnded = table.Column<bool>(type: "INTEGER", nullable: false),
                    WinnerFactionId = table.Column<string>(type: "TEXT", nullable: true),
                    WinnerFactionName = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    EndedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Games", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MatchSnapshots",
                columns: table => new
                {
                    GameId = table.Column<string>(type: "TEXT", nullable: false),
                    MapId = table.Column<string>(type: "TEXT", nullable: false),
                    MapArea = table.Column<string>(type: "TEXT", nullable: false),
                    SnapshotGeneratedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    GameStateJson = table.Column<string>(type: "TEXT", nullable: false),
                    TerritoryOwnersJson = table.Column<string>(type: "TEXT", nullable: false),
                    ArmiesJson = table.Column<string>(type: "TEXT", nullable: false),
                    FactionsJson = table.Column<string>(type: "TEXT", nullable: false),
                    RoutesJson = table.Column<string>(type: "TEXT", nullable: false),
                    LastTerritoryMovementUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchSnapshots", x => x.GameId);
                });

            migrationBuilder.CreateTable(
                name: "NpcTickCounts",
                columns: table => new
                {
                    GameId = table.Column<string>(type: "TEXT", nullable: false),
                    FactionId = table.Column<string>(type: "TEXT", nullable: false),
                    TickCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NpcTickCounts", x => new { x.GameId, x.FactionId });
                });

            migrationBuilder.CreateTable(
                name: "PostcodeTerritories",
                columns: table => new
                {
                    MapArea = table.Column<string>(type: "TEXT", nullable: false),
                    TerritoryId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Latitude = table.Column<double>(type: "REAL", nullable: false),
                    Longitude = table.Column<double>(type: "REAL", nullable: false),
                    Road = table.Column<string>(type: "TEXT", nullable: true),
                    BoundaryCoordinatesJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostcodeTerritories", x => new { x.MapArea, x.TerritoryId });
                });

            migrationBuilder.CreateTable(
                name: "TerritoryFeatures",
                columns: table => new
                {
                    MapArea = table.Column<string>(type: "TEXT", nullable: false),
                    TerritoryId = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Postcode = table.Column<string>(type: "TEXT", nullable: true),
                    AreaSquareKm = table.Column<double>(type: "REAL", nullable: false),
                    Factories = table.Column<int>(type: "INTEGER", nullable: false),
                    Shops = table.Column<int>(type: "INTEGER", nullable: false),
                    CommercialAreas = table.Column<int>(type: "INTEGER", nullable: false),
                    Offices = table.Column<int>(type: "INTEGER", nullable: false),
                    IndustrialSites = table.Column<int>(type: "INTEGER", nullable: false),
                    FarmlandOrResources = table.Column<int>(type: "INTEGER", nullable: false),
                    PopulationSupport = table.Column<int>(type: "INTEGER", nullable: false),
                    Mountains = table.Column<int>(type: "INTEGER", nullable: false),
                    Hills = table.Column<int>(type: "INTEGER", nullable: false),
                    MilitarySites = table.Column<int>(type: "INTEGER", nullable: false),
                    CastlesOrForts = table.Column<int>(type: "INTEGER", nullable: false),
                    GovernmentSites = table.Column<int>(type: "INTEGER", nullable: false),
                    Chokepoints = table.Column<int>(type: "INTEGER", nullable: false),
                    UrbanDensity = table.Column<int>(type: "INTEGER", nullable: false),
                    Roads = table.Column<int>(type: "INTEGER", nullable: false),
                    Railways = table.Column<int>(type: "INTEGER", nullable: false),
                    BridgesOrTunnels = table.Column<int>(type: "INTEGER", nullable: false),
                    Airports = table.Column<int>(type: "INTEGER", nullable: false),
                    Ports = table.Column<int>(type: "INTEGER", nullable: false),
                    Connections = table.Column<int>(type: "INTEGER", nullable: false),
                    SpecialFeatures = table.Column<int>(type: "INTEGER", nullable: false),
                    StatsEconomy = table.Column<int>(type: "INTEGER", nullable: false),
                    StatsDefense = table.Column<int>(type: "INTEGER", nullable: false),
                    StatsMobility = table.Column<int>(type: "INTEGER", nullable: false),
                    StatsStrategicValue = table.Column<int>(type: "INTEGER", nullable: false),
                    StatsRevenuePerTick = table.Column<int>(type: "INTEGER", nullable: false),
                    StatsArmyGrowthPerTick = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TerritoryFeatures", x => new { x.MapArea, x.TerritoryId });
                });

            migrationBuilder.CreateTable(
                name: "GamePlayers",
                columns: table => new
                {
                    GameId = table.Column<string>(type: "TEXT", nullable: false),
                    PlayerId = table.Column<string>(type: "TEXT", nullable: false),
                    FactionId = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: false),
                    SelectedTerritoryId = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GamePlayers", x => new { x.GameId, x.PlayerId });
                    table.ForeignKey(
                        name: "FK_GamePlayers_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PostcodeTerritories_MapArea",
                table: "PostcodeTerritories",
                column: "MapArea");

            migrationBuilder.CreateIndex(
                name: "IX_TerritoryFeatures_MapArea",
                table: "TerritoryFeatures",
                column: "MapArea");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GamePlayers");

            migrationBuilder.DropTable(
                name: "MatchSnapshots");

            migrationBuilder.DropTable(
                name: "NpcTickCounts");

            migrationBuilder.DropTable(
                name: "PostcodeTerritories");

            migrationBuilder.DropTable(
                name: "TerritoryFeatures");

            migrationBuilder.DropTable(
                name: "Games");
        }
    }
}
