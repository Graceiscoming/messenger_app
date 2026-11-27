using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Messenger.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sticker_packs",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    thumbnail_path = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sticker_packs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "stickers",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    pack_id = table.Column<int>(type: "integer", nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    image_path = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stickers", x => x.id);
                    table.ForeignKey(
                        name: "fk_stickers_sticker_packs_pack_id",
                        column: x => x.pack_id,
                        principalTable: "sticker_packs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "attachments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    uploader_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stored_file_name = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    original_file_name = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    width = table.Column<int>(type: "integer", nullable: true),
                    height = table.Column<int>(type: "integer", nullable: true),
                    uploaded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attachments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    username = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    normalized_username = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    display_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    status_message = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: true),
                    avatar_attachment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    failed_login_count = table.Column<int>(type: "integer", nullable: false),
                    lockout_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                    table.ForeignKey(
                        name: "fk_users_attachments_avatar_attachment_id",
                        column: x => x.avatar_attachment_id,
                        principalTable: "attachments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "conversations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<short>(type: "smallint", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    avatar_attachment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    direct_key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_message_id = table.Column<long>(type: "bigint", nullable: true),
                    last_message_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_conversations", x => x.id);
                    table.ForeignKey(
                        name: "fk_conversations_attachments_avatar_attachment_id",
                        column: x => x.avatar_attachment_id,
                        principalTable: "attachments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_conversations_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "friendships",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    requester_id = table.Column<Guid>(type: "uuid", nullable: false),
                    addressee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    responded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_friendships", x => x.id);
                    table.CheckConstraint("ck_friendships_not_self", "requester_id <> addressee_id");
                    table.ForeignKey(
                        name: "fk_friendships_users_addressee_id",
                        column: x => x.addressee_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_friendships_users_requester_id",
                        column: x => x.requester_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
                });

            migrationBuilder.CreateTable(
                name: "user_blocks",
                columns: table => new
                {
                    blocker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    blocked_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_blocks", x => new { x.blocker_id, x.blocked_id });
                    table.ForeignKey(
                        name: "fk_user_blocks_users_blocked_id",
                        column: x => x.blocked_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_blocks_users_blocker_id",
                        column: x => x.blocker_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "conversation_members",
                columns: table => new
                {
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<short>(type: "smallint", nullable: false),
                    joined_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    left_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_read_message_id = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_conversation_members", x => new { x.conversation_id, x.user_id });
                    table.ForeignKey(
                        name: "fk_conversation_members_conversations_conversation_id",
                        column: x => x.conversation_id,
                        principalTable: "conversations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_conversation_members_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "messages",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sender_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<short>(type: "smallint", nullable: false),
                    content = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    attachment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sticker_id = table.Column<int>(type: "integer", nullable: true),
                    reply_to_message_id = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    unsent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_messages", x => x.id);
                    table.CheckConstraint("ck_messages_text_has_content", "type <> 0 OR content IS NOT NULL OR unsent_at IS NOT NULL");
                    table.ForeignKey(
                        name: "fk_messages_attachments_attachment_id",
                        column: x => x.attachment_id,
                        principalTable: "attachments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_messages_conversations_conversation_id",
                        column: x => x.conversation_id,
                        principalTable: "conversations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_messages_messages_reply_to_message_id",
                        column: x => x.reply_to_message_id,
                        principalTable: "messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_messages_stickers_sticker_id",
                        column: x => x.sticker_id,
                        principalTable: "stickers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_messages_users_sender_id",
                        column: x => x.sender_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "sticker_packs",
                columns: new[] { "id", "name", "thumbnail_path" },
                values: new object[] { 1, "basic", "/stickers/basic/hello.svg" });

            migrationBuilder.InsertData(
                table: "stickers",
                columns: new[] { "id", "code", "image_path", "pack_id" },
                values: new object[,]
                {
                    { 1, "hello", "/stickers/basic/hello.svg", 1 },
                    { 2, "thanks", "/stickers/basic/thanks.svg", 1 },
                    { 3, "love", "/stickers/basic/love.svg", 1 },
                    { 4, "laugh", "/stickers/basic/laugh.svg", 1 },
                    { 5, "cry", "/stickers/basic/cry.svg", 1 },
                    { 6, "ok", "/stickers/basic/ok.svg", 1 },
                    { 7, "sorry", "/stickers/basic/sorry.svg", 1 },
                    { 8, "wow", "/stickers/basic/wow.svg", 1 },
                    { 9, "angry", "/stickers/basic/angry.svg", 1 },
                    { 10, "sleepy", "/stickers/basic/sleepy.svg", 1 },
                    { 11, "yes", "/stickers/basic/yes.svg", 1 },
                    { 12, "no", "/stickers/basic/no.svg", 1 }
                });

            migrationBuilder.CreateIndex(
                name: "ix_attachments_uploader_id",
                table: "attachments",
                column: "uploader_id");

            migrationBuilder.CreateIndex(
                name: "ix_conversation_members_user_id",
                table: "conversation_members",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_conversations_avatar_attachment_id",
                table: "conversations",
                column: "avatar_attachment_id");

            migrationBuilder.CreateIndex(
                name: "ix_conversations_created_by_id",
                table: "conversations",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_conversations_direct_key",
                table: "conversations",
                column: "direct_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_friendships_addressee_id_status",
                table: "friendships",
                columns: new[] { "addressee_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_friendships_requester_id",
                table: "friendships",
                column: "requester_id");

            migrationBuilder.CreateIndex(
                name: "ix_messages_attachment_id",
                table: "messages",
                column: "attachment_id");

            migrationBuilder.CreateIndex(
                name: "ix_messages_conversation_id_id",
                table: "messages",
                columns: new[] { "conversation_id", "id" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_messages_reply_to_message_id",
                table: "messages",
                column: "reply_to_message_id");

            migrationBuilder.CreateIndex(
                name: "ix_messages_sender_id",
                table: "messages",
                column: "sender_id");

            migrationBuilder.CreateIndex(
                name: "ix_messages_sticker_id",
                table: "messages",
                column: "sticker_id");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_token_hash",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_user_id",
                table: "refresh_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_stickers_code",
                table: "stickers",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stickers_pack_id",
                table: "stickers",
                column: "pack_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_blocks_blocked_id",
                table: "user_blocks",
                column: "blocked_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_avatar_attachment_id",
                table: "users",
                column: "avatar_attachment_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_normalized_username",
                table: "users",
                column: "normalized_username",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_attachments_users_uploader_id",
                table: "attachments",
                column: "uploader_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // Hand-written SQL: the fluent API cannot express an expression index.
            // One friendship row per unordered user pair (docs/05).
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX ux_friendships_pair ON friendships " +
                "(LEAST(requester_id, addressee_id), GREATEST(requester_id, addressee_id));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_attachments_users_uploader_id",
                table: "attachments");

            migrationBuilder.DropTable(
                name: "conversation_members");

            migrationBuilder.DropTable(
                name: "friendships");

            migrationBuilder.DropTable(
                name: "messages");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "user_blocks");

            migrationBuilder.DropTable(
                name: "conversations");

            migrationBuilder.DropTable(
                name: "stickers");

            migrationBuilder.DropTable(
                name: "sticker_packs");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "attachments");
        }
    }
}
