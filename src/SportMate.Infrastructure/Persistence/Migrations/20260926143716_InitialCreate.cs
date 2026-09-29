using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportMate.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "sports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    name = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    is_active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sports", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    email = table.Column<string>(type: "varchar(254)", maxLength: 254, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    password_hash = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    display_name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    phone_number = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    avatar_url = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    role = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    status = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "courts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    owner_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    description = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    address = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    latitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                    longitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                    time_zone_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_courts", x => x.id);
                    table.CheckConstraint("ck_courts_latitude", "latitude BETWEEN -90 AND 90");
                    table.CheckConstraint("ck_courts_longitude", "longitude BETWEEN -180 AND 180");
                    table.ForeignKey(
                        name: "fk_courts_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "play_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    creator_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    sport_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    title = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    description = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    starts_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ends_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    spots_needed = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    min_skill_level = table.Column<byte>(type: "tinyint unsigned", nullable: true),
                    max_skill_level = table.Column<byte>(type: "tinyint unsigned", nullable: true),
                    location_name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    latitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                    longitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: false),
                    status = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    cancelled_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_play_requests", x => x.id);
                    table.CheckConstraint("ck_play_requests_latitude", "latitude BETWEEN -90 AND 90");
                    table.CheckConstraint("ck_play_requests_longitude", "longitude BETWEEN -180 AND 180");
                    table.CheckConstraint("ck_play_requests_skill_range", "min_skill_level IS NULL OR max_skill_level IS NULL OR min_skill_level <= max_skill_level");
                    table.CheckConstraint("ck_play_requests_spots_needed", "spots_needed > 0");
                    table.CheckConstraint("ck_play_requests_time_range", "starts_at_utc < ends_at_utc");
                    table.ForeignKey(
                        name: "fk_play_requests_sports_sport_id",
                        column: x => x.sport_id,
                        principalTable: "sports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_play_requests_users_creator_id",
                        column: x => x.creator_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    user_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    token_hash = table.Column<string>(type: "char(64)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    expires_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    revoked_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_refresh_tokens_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "user_sport_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    user_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    sport_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    skill_level = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_sport_profiles", x => x.id);
                    table.CheckConstraint("ck_user_sport_profiles_skill_level", "skill_level BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "fk_user_sport_profiles_sports_sport_id",
                        column: x => x.sport_id,
                        principalTable: "sports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_sport_profiles_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "court_operating_hours",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    court_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    day_of_week = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    opens_at = table.Column<TimeOnly>(type: "time", nullable: false),
                    closes_at = table.Column<TimeOnly>(type: "time", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_court_operating_hours", x => x.id);
                    table.CheckConstraint("ck_court_operating_hours_day_of_week", "day_of_week BETWEEN 0 AND 6");
                    table.CheckConstraint("ck_court_operating_hours_time_range", "opens_at < closes_at");
                    table.ForeignKey(
                        name: "fk_court_operating_hours_courts_court_id",
                        column: x => x.court_id,
                        principalTable: "courts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "court_sports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    court_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    sport_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    price_per_hour = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "char(3)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    is_active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_court_sports", x => x.id);
                    table.UniqueConstraint("ak_court_sports_id_court_id", x => new { x.id, x.court_id });
                    table.CheckConstraint("ck_court_sports_price_per_hour", "price_per_hour > 0");
                    table.ForeignKey(
                        name: "fk_court_sports_courts_court_id",
                        column: x => x.court_id,
                        principalTable: "courts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_court_sports_sports_sport_id",
                        column: x => x.sport_id,
                        principalTable: "sports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "play_request_participants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    play_request_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    user_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    status = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    responded_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_play_request_participants", x => x.id);
                    table.ForeignKey(
                        name: "fk_play_request_participants_play_requests_play_request_id",
                        column: x => x.play_request_id,
                        principalTable: "play_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_play_request_participants_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "bookings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    user_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    court_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    court_sport_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    starts_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ends_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    total_price = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "char(3)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    cancellation_reason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    cancelled_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bookings", x => x.id);
                    table.CheckConstraint("ck_bookings_time_range", "starts_at_utc < ends_at_utc");
                    table.CheckConstraint("ck_bookings_total_price", "total_price >= 0");
                    table.ForeignKey(
                        name: "fk_bookings_court_sports_court_sport_id_court_id",
                        columns: x => new { x.court_sport_id, x.court_id },
                        principalTable: "court_sports",
                        principalColumns: new[] { "id", "court_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_bookings_courts_court_id",
                        column: x => x.court_id,
                        principalTable: "courts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_bookings_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_bookings_court_id_starts_at_utc_ends_at_utc",
                table: "bookings",
                columns: new[] { "court_id", "starts_at_utc", "ends_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_bookings_court_sport_id_court_id",
                table: "bookings",
                columns: new[] { "court_sport_id", "court_id" });

            migrationBuilder.CreateIndex(
                name: "ix_bookings_user_id_starts_at_utc",
                table: "bookings",
                columns: new[] { "user_id", "starts_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_court_operating_hours_court_id_day_of_week_opens_at",
                table: "court_operating_hours",
                columns: new[] { "court_id", "day_of_week", "opens_at" });

            migrationBuilder.CreateIndex(
                name: "ix_court_sports_court_id_sport_id",
                table: "court_sports",
                columns: new[] { "court_id", "sport_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_court_sports_sport_id",
                table: "court_sports",
                column: "sport_id");

            migrationBuilder.CreateIndex(
                name: "ix_courts_latitude_longitude",
                table: "courts",
                columns: new[] { "latitude", "longitude" });

            migrationBuilder.CreateIndex(
                name: "ix_courts_owner_id",
                table: "courts",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_play_request_participants_play_request_id_user_id",
                table: "play_request_participants",
                columns: new[] { "play_request_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_play_request_participants_user_id_status",
                table: "play_request_participants",
                columns: new[] { "user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_play_requests_creator_id_starts_at_utc",
                table: "play_requests",
                columns: new[] { "creator_id", "starts_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_play_requests_latitude_longitude",
                table: "play_requests",
                columns: new[] { "latitude", "longitude" });

            migrationBuilder.CreateIndex(
                name: "ix_play_requests_sport_id_status_starts_at_utc",
                table: "play_requests",
                columns: new[] { "sport_id", "status", "starts_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_token_hash",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_user_id_expires_at_utc",
                table: "refresh_tokens",
                columns: new[] { "user_id", "expires_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_sports_name",
                table: "sports",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_sport_profiles_sport_id_skill_level",
                table: "user_sport_profiles",
                columns: new[] { "sport_id", "skill_level" });

            migrationBuilder.CreateIndex(
                name: "ix_user_sport_profiles_user_id_sport_id",
                table: "user_sport_profiles",
                columns: new[] { "user_id", "sport_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                table: "users",
                column: "email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bookings");

            migrationBuilder.DropTable(
                name: "court_operating_hours");

            migrationBuilder.DropTable(
                name: "play_request_participants");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "user_sport_profiles");

            migrationBuilder.DropTable(
                name: "court_sports");

            migrationBuilder.DropTable(
                name: "play_requests");

            migrationBuilder.DropTable(
                name: "courts");

            migrationBuilder.DropTable(
                name: "sports");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
