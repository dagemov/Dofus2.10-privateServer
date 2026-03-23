using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dofus210.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCharactersAndBreeds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Breeds",
                columns: table => new
                {
                    Id = table.Column<byte>(type: "tinyint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    MaleLook = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    FemaleLook = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    MaleBonesId = table.Column<int>(type: "int", nullable: false),
                    FemaleBonesId = table.Column<int>(type: "int", nullable: false),
                    IsPlayable = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Breeds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GameServers",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Port = table.Column<int>(type: "int", nullable: false),
                    CommunityId = table.Column<byte>(type: "tinyint", nullable: false),
                    Type = table.Column<byte>(type: "tinyint", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    Completion = table.Column<byte>(type: "tinyint", nullable: false),
                    CharacterCapacity = table.Column<byte>(type: "tinyint", nullable: false),
                    CanCreateNewCharacter = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameServers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Characters",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccountId = table.Column<int>(type: "int", nullable: false),
                    GameServerId = table.Column<short>(type: "smallint", nullable: false),
                    BreedId = table.Column<byte>(type: "tinyint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Sex = table.Column<bool>(type: "bit", nullable: false),
                    Level = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)1),
                    Experience = table.Column<long>(type: "bigint", nullable: false),
                    CosmeticId = table.Column<short>(type: "smallint", nullable: false),
                    Color1 = table.Column<int>(type: "int", nullable: false),
                    Color2 = table.Column<int>(type: "int", nullable: false),
                    Color3 = table.Column<int>(type: "int", nullable: false),
                    Color4 = table.Column<int>(type: "int", nullable: false),
                    Color5 = table.Column<int>(type: "int", nullable: false),
                    BonesId = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    SkinId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Characters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Characters_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Characters_Breeds_BreedId",
                        column: x => x.BreedId,
                        principalTable: "Breeds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Characters_GameServers_GameServerId",
                        column: x => x.GameServerId,
                        principalTable: "GameServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CharacterPositions",
                columns: table => new
                {
                    CharacterId = table.Column<long>(type: "bigint", nullable: false),
                    MapId = table.Column<int>(type: "int", nullable: false),
                    CellId = table.Column<short>(type: "smallint", nullable: false),
                    Direction = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)2)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterPositions", x => x.CharacterId);
                    table.ForeignKey(
                        name: "FK_CharacterPositions_Characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "Characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CharacterStats",
                columns: table => new
                {
                    CharacterId = table.Column<long>(type: "bigint", nullable: false),
                    Kamas = table.Column<long>(type: "bigint", nullable: false),
                    StatsPoints = table.Column<short>(type: "smallint", nullable: false),
                    SpellsPoints = table.Column<short>(type: "smallint", nullable: false),
                    LifePoints = table.Column<int>(type: "int", nullable: false),
                    MaxLifePoints = table.Column<int>(type: "int", nullable: false),
                    EnergyPoints = table.Column<short>(type: "smallint", nullable: false),
                    MaxEnergyPoints = table.Column<short>(type: "smallint", nullable: false),
                    ActionPoints = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)6),
                    MovementPoints = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)3)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterStats", x => x.CharacterId);
                    table.ForeignKey(
                        name: "FK_CharacterStats_Characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "Characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Characters_AccountId",
                table: "Characters",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Characters_BreedId",
                table: "Characters",
                column: "BreedId");

            migrationBuilder.CreateIndex(
                name: "IX_Characters_GameServerId_Name",
                table: "Characters",
                columns: new[] { "GameServerId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CharacterPositions");

            migrationBuilder.DropTable(
                name: "CharacterStats");

            migrationBuilder.DropTable(
                name: "Characters");

            migrationBuilder.DropTable(
                name: "Breeds");

            migrationBuilder.DropTable(
                name: "GameServers");
        }
    }
}
