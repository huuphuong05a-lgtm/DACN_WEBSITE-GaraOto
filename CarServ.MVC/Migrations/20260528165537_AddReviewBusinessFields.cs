using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarServ.MVC.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewBusinessFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CustomerId",
                table: "Reviews",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "Reviews",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OrderItemId",
                table: "Reviews",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ServiceHistoryId",
                table: "Reviews",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_CustomerId",
                table: "Reviews",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_OrderItemId",
                table: "Reviews",
                column: "OrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_ServiceHistoryId",
                table: "Reviews",
                column: "ServiceHistoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_Reviews_Customers",
                table: "Reviews",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "CustomerID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Reviews_OrderItems",
                table: "Reviews",
                column: "OrderItemId",
                principalTable: "OrderItems",
                principalColumn: "OrderItemID",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Reviews_ServiceHistory",
                table: "Reviews",
                column: "ServiceHistoryId",
                principalTable: "ServiceHistory",
                principalColumn: "ServiceHistoryID",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Reviews_Customers",
                table: "Reviews");

            migrationBuilder.DropForeignKey(
                name: "FK_Reviews_OrderItems",
                table: "Reviews");

            migrationBuilder.DropForeignKey(
                name: "FK_Reviews_ServiceHistory",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_Reviews_CustomerId",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_Reviews_OrderItemId",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_Reviews_ServiceHistoryId",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "OrderItemId",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "ServiceHistoryId",
                table: "Reviews");
        }
    }
}
