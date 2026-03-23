using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenRocketArena.Server.Migrations
{
    /// <inheritdoc />
    public partial class IamSessionNullableAccountId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IamSessions_Accounts_AccountId",
                table: "IamSessions");

            migrationBuilder.AlterColumn<long>(
                name: "AccountId",
                table: "IamSessions",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "INTEGER");

            migrationBuilder.AddForeignKey(
                name: "FK_IamSessions_Accounts_AccountId",
                table: "IamSessions",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_IamSessions_Accounts_AccountId",
                table: "IamSessions");

            migrationBuilder.AlterColumn<long>(
                name: "AccountId",
                table: "IamSessions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_IamSessions_Accounts_AccountId",
                table: "IamSessions",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
