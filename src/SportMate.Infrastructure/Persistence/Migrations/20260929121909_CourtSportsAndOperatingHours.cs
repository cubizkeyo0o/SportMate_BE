using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportMate.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CourtSportsAndOperatingHours : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_court_sports_price_per_hour",
                table: "court_sports");

            migrationBuilder.AddColumn<DateTime>(
                name: "created_at_utc",
                table: "court_operating_hours",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at_utc",
                table: "court_operating_hours",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            // Existing periods predate the timestamps; stamp them with the migration time.
            migrationBuilder.Sql(
                "UPDATE court_operating_hours SET created_at_utc = UTC_TIMESTAMP(6), updated_at_utc = UTC_TIMESTAMP(6);");

            migrationBuilder.AddCheckConstraint(
                name: "ck_court_sports_price_per_hour",
                table: "court_sports",
                sql: "price_per_hour >= 0");

            // Make the index unique in one statement: it is the only index covering court_id, which the
            // court foreign key requires, so MySQL refuses a separate DROP INDEX (error 1553).
            migrationBuilder.Sql(
                "ALTER TABLE `court_operating_hours` "
                + "DROP INDEX `ix_court_operating_hours_court_id_day_of_week_opens_at`, "
                + "ADD UNIQUE INDEX `ix_court_operating_hours_court_id_day_of_week_opens_at` (`court_id`, `day_of_week`, `opens_at`);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_court_sports_price_per_hour",
                table: "court_sports");

            migrationBuilder.Sql(
                "ALTER TABLE `court_operating_hours` "
                + "DROP INDEX `ix_court_operating_hours_court_id_day_of_week_opens_at`, "
                + "ADD INDEX `ix_court_operating_hours_court_id_day_of_week_opens_at` (`court_id`, `day_of_week`, `opens_at`);");

            migrationBuilder.DropColumn(
                name: "created_at_utc",
                table: "court_operating_hours");

            migrationBuilder.DropColumn(
                name: "updated_at_utc",
                table: "court_operating_hours");

            // Fails if any court sport has a zero price; raise those prices before rolling back.
            migrationBuilder.AddCheckConstraint(
                name: "ck_court_sports_price_per_hour",
                table: "court_sports",
                sql: "price_per_hour > 0");
        }
    }
}
