using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SportMate.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSportSlugAndSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "slug",
                table: "sports",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at_utc",
                table: "sports",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            // Backfill any sports inserted manually before this migration so the unique slug index can be created.
            migrationBuilder.Sql(
                "UPDATE sports SET slug = LOWER(REPLACE(TRIM(name), ' ', '-')), updated_at_utc = created_at_utc WHERE slug = '';");

            migrationBuilder.InsertData(
                table: "sports",
                columns: new[] { "id", "created_at_utc", "is_active", "name", "slug", "updated_at_utc" },
                values: new object[,]
                {
                    { new Guid("0b1e7a6e-5c2a-4f4e-9a01-000000000001"), new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), true, "Tennis", "tennis", new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("0b1e7a6e-5c2a-4f4e-9a01-000000000002"), new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), true, "Badminton", "badminton", new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("0b1e7a6e-5c2a-4f4e-9a01-000000000003"), new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), true, "Basketball", "basketball", new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("0b1e7a6e-5c2a-4f4e-9a01-000000000004"), new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), true, "Football", "football", new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("0b1e7a6e-5c2a-4f4e-9a01-000000000005"), new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), true, "Pickleball", "pickleball", new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("0b1e7a6e-5c2a-4f4e-9a01-000000000006"), new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), true, "Table Tennis", "table-tennis", new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("0b1e7a6e-5c2a-4f4e-9a01-000000000007"), new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc), true, "Volleyball", "volleyball", new DateTime(2026, 9, 29, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "ix_sports_slug",
                table: "sports",
                column: "slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_sports_slug",
                table: "sports");

            migrationBuilder.DeleteData(
                table: "sports",
                keyColumn: "id",
                keyValue: new Guid("0b1e7a6e-5c2a-4f4e-9a01-000000000001"));

            migrationBuilder.DeleteData(
                table: "sports",
                keyColumn: "id",
                keyValue: new Guid("0b1e7a6e-5c2a-4f4e-9a01-000000000002"));

            migrationBuilder.DeleteData(
                table: "sports",
                keyColumn: "id",
                keyValue: new Guid("0b1e7a6e-5c2a-4f4e-9a01-000000000003"));

            migrationBuilder.DeleteData(
                table: "sports",
                keyColumn: "id",
                keyValue: new Guid("0b1e7a6e-5c2a-4f4e-9a01-000000000004"));

            migrationBuilder.DeleteData(
                table: "sports",
                keyColumn: "id",
                keyValue: new Guid("0b1e7a6e-5c2a-4f4e-9a01-000000000005"));

            migrationBuilder.DeleteData(
                table: "sports",
                keyColumn: "id",
                keyValue: new Guid("0b1e7a6e-5c2a-4f4e-9a01-000000000006"));

            migrationBuilder.DeleteData(
                table: "sports",
                keyColumn: "id",
                keyValue: new Guid("0b1e7a6e-5c2a-4f4e-9a01-000000000007"));

            migrationBuilder.DropColumn(
                name: "slug",
                table: "sports");

            migrationBuilder.DropColumn(
                name: "updated_at_utc",
                table: "sports");
        }
    }
}
