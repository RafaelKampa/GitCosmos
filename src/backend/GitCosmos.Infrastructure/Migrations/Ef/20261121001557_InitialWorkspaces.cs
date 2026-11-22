using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GitCosmos.Infrastructure.Migrations.Ef
{
    /// <inheritdoc />
    public partial class InitialWorkspaces : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "git_repositories",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    local_path = table.Column<string>(type: "TEXT", nullable: false),
                    path_key = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    active = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_git_repositories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "workspaces",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    name_key = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    active = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workspaces", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "workspace_repositories",
                columns: table => new
                {
                    workspace_id = table.Column<int>(type: "INTEGER", nullable: false),
                    git_repository_id = table.Column<int>(type: "INTEGER", nullable: false),
                    added_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workspace_repositories", x => new { x.workspace_id, x.git_repository_id });
                    table.ForeignKey(
                        name: "FK_workspace_repositories_git_repositories_git_repository_id",
                        column: x => x.git_repository_id,
                        principalTable: "git_repositories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_workspace_repositories_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_git_repositories_path_key",
                table: "git_repositories",
                column: "path_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_workspace_repositories_git_repository_id",
                table: "workspace_repositories",
                column: "git_repository_id");

            migrationBuilder.CreateIndex(
                name: "IX_workspaces_name_key",
                table: "workspaces",
                column: "name_key",
                unique: true,
                filter: "active = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "workspace_repositories");

            migrationBuilder.DropTable(
                name: "git_repositories");

            migrationBuilder.DropTable(
                name: "workspaces");
        }
    }
}
