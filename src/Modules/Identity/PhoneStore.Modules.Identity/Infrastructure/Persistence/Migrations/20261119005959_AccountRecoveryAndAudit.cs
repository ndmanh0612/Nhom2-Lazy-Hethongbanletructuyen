using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhoneStore.Modules.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AccountRecoveryAndAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VerifiedPhone",
                schema: "identity",
                table: "users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "audit_entries",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<long>(type: "bigint", nullable: false),
                    TargetId = table.Column<long>(type: "bigint", nullable: false),
                    Action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_entries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "verification_challenges",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Purpose = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Channel = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Destination = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    CodeHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResetTokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    VerifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConsumedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_verification_challenges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_verification_challenges_users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_users_VerifiedPhone",
                schema: "identity",
                table: "users",
                column: "VerifiedPhone",
                unique: true,
                filter: "\"VerifiedPhone\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_audit_entries_ActorId",
                schema: "identity",
                table: "audit_entries",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_audit_entries_CreatedAt_Id",
                schema: "identity",
                table: "audit_entries",
                columns: new[] { "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_entries_TargetId",
                schema: "identity",
                table: "audit_entries",
                column: "TargetId");

            migrationBuilder.CreateIndex(
                name: "IX_verification_challenges_ExpiresAt",
                schema: "identity",
                table: "verification_challenges",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_verification_challenges_UserId_Purpose_CreatedAt",
                schema: "identity",
                table: "verification_challenges",
                columns: new[] { "UserId", "Purpose", "CreatedAt" });

            migrationBuilder.Sql("""
                CREATE FUNCTION identity.reject_audit_mutation() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'identity audit history is append-only' USING ERRCODE = '42501';
                END;
                $$;
                CREATE TRIGGER audit_entries_append_only
                BEFORE UPDATE OR DELETE OR TRUNCATE ON identity.audit_entries
                FOR EACH STATEMENT EXECUTE FUNCTION identity.reject_audit_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER audit_entries_append_only ON identity.audit_entries; DROP FUNCTION identity.reject_audit_mutation();");
            migrationBuilder.DropTable(
                name: "audit_entries",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "verification_challenges",
                schema: "identity");

            migrationBuilder.DropIndex(
                name: "IX_users_VerifiedPhone",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "VerifiedPhone",
                schema: "identity",
                table: "users");
        }
    }
}
