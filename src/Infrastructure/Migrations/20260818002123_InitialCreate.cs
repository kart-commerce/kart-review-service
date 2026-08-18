using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kart.Review.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_log",
                columns: table => new
                {
                    entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_name = table.Column<string>(type: "text", nullable: false),
                    actor_id = table.Column<string>(type: "text", nullable: false),
                    actor_type = table.Column<string>(type: "text", nullable: false),
                    action = table.Column<string>(type: "text", nullable: false),
                    entity_type = table.Column<string>(type: "text", nullable: false),
                    entity_id = table.Column<string>(type: "text", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    metadata = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_log", x => x.entry_id);
                });

            migrationBuilder.CreateTable(
                name: "idempotency_keys",
                columns: table => new
                {
                    idempotency_key = table.Column<string>(type: "text", nullable: false),
                    endpoint = table.Column<string>(type: "text", nullable: false),
                    request_payload_hash = table.Column<string>(type: "text", nullable: false),
                    stored_response = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_idempotency_keys", x => new { x.idempotency_key, x.endpoint });
                });

            migrationBuilder.CreateTable(
                name: "product_ratings",
                columns: table => new
                {
                    sku = table.Column<string>(type: "text", nullable: false),
                    avg = table.Column<double>(type: "double precision", nullable: false),
                    count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_ratings", x => x.sku);
                    table.CheckConstraint("ck_product_ratings_count", "count >= 0");
                });

            migrationBuilder.CreateTable(
                name: "review_outbox",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    read_model_projected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    trace_parent = table.Column<string>(type: "text", nullable: true),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: false),
                    review_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "varchar(24)", nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_review_outbox", x => x.id);
                    table.CheckConstraint("ck_review_outbox_event_type", "event_type IN ('ReviewSubmitted', 'ReviewUpdated', 'ReviewUnpublished')");
                });

            migrationBuilder.CreateTable(
                name: "reviews",
                columns: table => new
                {
                    review_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku = table.Column<string>(type: "text", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rating = table.Column<short>(type: "smallint", nullable: false),
                    body_text = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "varchar(20)", nullable: false),
                    pending_revision = table.Column<string>(type: "jsonb", nullable: true),
                    first_published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_edited_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    retracted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    idempotency_key = table.Column<string>(type: "text", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reviews", x => x.review_id);
                    table.CheckConstraint("ck_reviews_status", "status IN ('PendingModeration', 'Published', 'Rejected', 'Retracted')");
                });

            migrationBuilder.CreateTable(
                name: "verified_purchase_records",
                columns: table => new
                {
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    skus = table.Column<string>(type: "jsonb", nullable: false),
                    delivered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_verified_purchase_records", x => x.order_id);
                });

            migrationBuilder.CreateTable(
                name: "product_rating_ledger",
                columns: table => new
                {
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku = table.Column<string>(type: "text", nullable: false),
                    last_applied_rating = table.Column<short>(type: "smallint", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_rating_ledger", x => new { x.order_id, x.sku });
                    table.CheckConstraint("ck_product_rating_ledger_rating", "last_applied_rating IS NULL OR last_applied_rating BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_product_rating_ledger_product_ratings_sku",
                        column: x => x.sku,
                        principalTable: "product_ratings",
                        principalColumn: "sku",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_audit_log_entity",
                table: "audit_log",
                columns: new[] { "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "idx_idempotency_keys_expiry",
                table: "idempotency_keys",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "IX_product_rating_ledger_sku",
                table: "product_rating_ledger",
                column: "sku");

            migrationBuilder.CreateIndex(
                name: "idx_review_outbox_unprojected",
                table: "review_outbox",
                column: "created_at",
                filter: "read_model_projected_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "idx_review_outbox_unpublished",
                table: "review_outbox",
                column: "created_at",
                filter: "published_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "idx_reviews_moderation_queue",
                table: "reviews",
                column: "created_at",
                filter: "status = 'PendingModeration'");

            migrationBuilder.CreateIndex(
                name: "uq_reviews_idempotency_key",
                table: "reviews",
                column: "idempotency_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_reviews_order_id_sku",
                table: "reviews",
                columns: new[] { "order_id", "sku" },
                unique: true);

            // database-design.md's "Monotonic terminal states" backstop: the application layer
            // (Review.Edit/Retract/Moderate) already guards every illegal transition as a no-op,
            // but this trigger is the double-defense DB-level enforcement — belt AND suspenders
            // against a future bug or a direct DB write bypassing the application entirely.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION enforce_review_status_transition() RETURNS trigger AS $$
                BEGIN
                    IF OLD.status IN ('Rejected', 'Retracted') AND NEW.status <> OLD.status THEN
                        RAISE EXCEPTION 'illegal ModerationStatus transition: % is terminal, cannot move to %', OLD.status, NEW.status;
                    END IF;
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER trg_reviews_status_guard
                    BEFORE UPDATE OF status ON reviews
                    FOR EACH ROW EXECUTE FUNCTION enforce_review_status_transition();
                """);

            // database-design.md's Row-Level Security section. Session variables are set fresh on
            // every connection open by RlsConnectionInterceptor (never left to a client-suppliable
            // header). current_setting(..., true) (missing_ok) returns NULL rather than erroring
            // when unset, which safely evaluates every branch below to false.
            migrationBuilder.Sql("ALTER TABLE reviews ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("""
                CREATE POLICY reviews_owner_or_moderator ON reviews
                    USING (
                        user_id = NULLIF(current_setting('app.current_principal', true), '')::uuid
                        OR current_setting('app.current_principal_role', true) IN ('support_agent', 'admin')
                        OR current_setting('app.current_principal_kind', true) IN ('service', 'system')
                    );
                """);

            migrationBuilder.Sql("ALTER TABLE verified_purchase_records ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("""
                CREATE POLICY verified_purchase_records_owner_or_system ON verified_purchase_records
                    USING (
                        user_id = NULLIF(current_setting('app.current_principal', true), '')::uuid
                        OR current_setting('app.current_principal_kind', true) IN ('service', 'system')
                    );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP POLICY IF EXISTS verified_purchase_records_owner_or_system ON verified_purchase_records;");
            migrationBuilder.Sql("DROP POLICY IF EXISTS reviews_owner_or_moderator ON reviews;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_reviews_status_guard ON reviews;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS enforce_review_status_transition();");

            migrationBuilder.DropTable(
                name: "audit_log");

            migrationBuilder.DropTable(
                name: "idempotency_keys");

            migrationBuilder.DropTable(
                name: "product_rating_ledger");

            migrationBuilder.DropTable(
                name: "review_outbox");

            migrationBuilder.DropTable(
                name: "reviews");

            migrationBuilder.DropTable(
                name: "verified_purchase_records");

            migrationBuilder.DropTable(
                name: "product_ratings");
        }
    }
}
