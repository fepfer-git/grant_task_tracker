using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GrantTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Grants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Reference = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    OrganisationName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ApplicationDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ClosedNote = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    ClosedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Version = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Grants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GrantTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GrantId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    DueDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CancelledAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Version = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GrantTasks", x => x.Id);
                    table.CheckConstraint("CK_GrantTask_CompletedXorCancelled", "\"CompletedAtUtc\" IS NULL OR \"CancelledAtUtc\" IS NULL");
                    table.ForeignKey(
                        name: "FK_GrantTasks_Grants_GrantId",
                        column: x => x.GrantId,
                        principalTable: "Grants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Grants_Reference",
                table: "Grants",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Grants_Status",
                table: "Grants",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_GrantTasks_DueDate_Pending",
                table: "GrantTasks",
                column: "DueDate",
                filter: "\"CompletedAtUtc\" IS NULL AND \"CancelledAtUtc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_GrantTasks_GrantId",
                table: "GrantTasks",
                column: "GrantId");

            migrationBuilder.CreateIndex(
                name: "IX_GrantTasks_GrantId_Priority_Pending",
                table: "GrantTasks",
                columns: new[] { "GrantId", "Priority" },
                filter: "\"CompletedAtUtc\" IS NULL AND \"CancelledAtUtc\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GrantTasks");

            migrationBuilder.DropTable(
                name: "Grants");
        }
    }
}
