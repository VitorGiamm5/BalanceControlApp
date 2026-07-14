using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BalanceControl.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialBalanceControl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "balance_control");

            migrationBuilder.CreateTable(
                name: "tb_audit_log",
                schema: "balance_control",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    entity_name = table.Column<string>(type: "varchar", maxLength: 200, nullable: false),
                    entity_key = table.Column<string>(type: "varchar", maxLength: 200, nullable: false),
                    action = table.Column<string>(type: "varchar", maxLength: 50, nullable: false),
                    old_values = table.Column<string>(type: "jsonb", nullable: true),
                    new_values = table.Column<string>(type: "jsonb", nullable: true),
                    user_id = table.Column<string>(type: "varchar", maxLength: 120, nullable: false),
                    user_name = table.Column<string>(type: "varchar", maxLength: 200, nullable: false),
                    correlation_id = table.Column<string>(type: "varchar", maxLength: 120, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tb_audit_log", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tb_balance_movement",
                schema: "balance_control",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<string>(type: "varchar", maxLength: 100, nullable: false),
                    operation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    balance_after = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    request_hash = table.Column<string>(type: "char(64)", maxLength: 64, nullable: false),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    description = table.Column<string>(type: "varchar", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tb_balance_movement", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tb_outbox_message",
                schema: "balance_control",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    event_type = table.Column<string>(type: "varchar", maxLength: 200, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "varchar", maxLength: 50, nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    error = table.Column<string>(type: "text", nullable: true),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tb_outbox_message", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tb_user_balance",
                schema: "balance_control",
                columns: table => new
                {
                    user_id = table.Column<string>(type: "varchar", maxLength: 100, nullable: false),
                    balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tb_user_balance", x => x.user_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tb_audit_log_created_at",
                schema: "balance_control",
                table: "tb_audit_log",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_tb_audit_log_entity_name_entity_key",
                schema: "balance_control",
                table: "tb_audit_log",
                columns: new[] { "entity_name", "entity_key" });

            migrationBuilder.CreateIndex(
                name: "IX_tb_balance_movement_user_id_created_at_id",
                schema: "balance_control",
                table: "tb_balance_movement",
                columns: new[] { "user_id", "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_tb_balance_movement_user_id_operation_id",
                schema: "balance_control",
                table: "tb_balance_movement",
                columns: new[] { "user_id", "operation_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tb_outbox_message_status_occurred_at",
                schema: "balance_control",
                table: "tb_outbox_message",
                columns: new[] { "status", "occurred_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tb_audit_log",
                schema: "balance_control");

            migrationBuilder.DropTable(
                name: "tb_balance_movement",
                schema: "balance_control");

            migrationBuilder.DropTable(
                name: "tb_outbox_message",
                schema: "balance_control");

            migrationBuilder.DropTable(
                name: "tb_user_balance",
                schema: "balance_control");
        }
    }
}
