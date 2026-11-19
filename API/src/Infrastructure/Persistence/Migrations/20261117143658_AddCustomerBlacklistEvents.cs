using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HKPartsEcommerce.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerBlacklistEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomerBlacklistEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubjectKey = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    BlacklistEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Action = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedOnUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerBlacklistEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerBlacklistEvents_AtUtc",
                table: "CustomerBlacklistEvents",
                column: "AtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerBlacklistEvents_SubjectKey_AtUtc",
                table: "CustomerBlacklistEvents",
                columns: new[] { "SubjectKey", "AtUtc" });

            // Backfill the history from what is already recorded. SubjectKey = lower email, else customer:{id},
            // else name:{first last} — the same rule as CustomerBlacklistHistory.SubjectKey.
            // 1) Customers blocked from the Customers list: every BlacklistedCustomers row (soft-deleted ones too)
            //    is one block; a soft-deleted row is also the unblock, at DeletedOnUtc. If
            //    BlacklistCustomerFlagHistory (dated earlier, merged later) already turned the table into an
            //    append-only history, each row is one event instead (Action 1 Blocked / 2 Unblocked). Dynamic SQL so
            //    each branch only compiles against the columns that exist.
            migrationBuilder.Sql(@"
IF COL_LENGTH(N'BlacklistedCustomers', N'Deleted') IS NOT NULL
    EXEC(N'
WITH src AS (
    SELECT b.Id, b.CustomerId, b.Reason, b.CreatedById, b.CreatedOnUtc, b.UpdatedById, b.Deleted, b.DeletedOnUtc,
           NULLIF(LTRIM(RTRIM(LEFT(c.FirstName, 100))), '''') AS FirstName,
           NULLIF(LTRIM(RTRIM(LEFT(c.LastName, 100))), '''') AS LastName,
           NULLIF(LOWER(LTRIM(RTRIM(u.Email))), '''') AS Email,
           (SELECT TOP 1 e.Id FROM CustomerBlacklist e WHERE e.CustomerId = b.CustomerId) AS EntryId
    FROM BlacklistedCustomers b
    JOIN Customers c ON c.Id = b.CustomerId
    LEFT JOIN Users u ON u.Id = c.UserId
)
INSERT INTO CustomerBlacklistEvents (Id, SubjectKey, BlacklistEntryId, CustomerId, FirstName, LastName, Email, Action, Reason, AtUtc, CreatedById, CreatedOnUtc, UpdatedById, UpdatedOnUtc)
SELECT NEWID(), LEFT(COALESCE(Email, ''customer:'' + LOWER(CAST(CustomerId AS nvarchar(36)))), 300),
       EntryId, CustomerId, FirstName, LastName, Email, 0, LEFT(Reason, 1000), CreatedOnUtc, CreatedById, CreatedOnUtc, CreatedById, CreatedOnUtc
FROM src
UNION ALL
SELECT NEWID(), LEFT(COALESCE(Email, ''customer:'' + LOWER(CAST(CustomerId AS nvarchar(36)))), 300),
       EntryId, CustomerId, FirstName, LastName, Email, 1, LEFT(Reason, 1000), DeletedOnUtc, UpdatedById, DeletedOnUtc, UpdatedById, DeletedOnUtc
FROM src WHERE Deleted = 1 AND DeletedOnUtc IS NOT NULL;');
ELSE
    EXEC(N'
WITH src AS (
    SELECT b.CustomerId, b.Reason, b.Action, b.CreatedById, b.CreatedOnUtc,
           NULLIF(LTRIM(RTRIM(LEFT(c.FirstName, 100))), '''') AS FirstName,
           NULLIF(LTRIM(RTRIM(LEFT(c.LastName, 100))), '''') AS LastName,
           NULLIF(LOWER(LTRIM(RTRIM(u.Email))), '''') AS Email,
           (SELECT TOP 1 e.Id FROM CustomerBlacklist e WHERE e.CustomerId = b.CustomerId) AS EntryId
    FROM BlacklistedCustomers b
    JOIN Customers c ON c.Id = b.CustomerId
    LEFT JOIN Users u ON u.Id = c.UserId
)
INSERT INTO CustomerBlacklistEvents (Id, SubjectKey, BlacklistEntryId, CustomerId, FirstName, LastName, Email, Action, Reason, AtUtc, CreatedById, CreatedOnUtc, UpdatedById, UpdatedOnUtc)
SELECT NEWID(), LEFT(COALESCE(Email, ''customer:'' + LOWER(CAST(CustomerId AS nvarchar(36)))), 300),
       EntryId, CustomerId, FirstName, LastName, Email, CASE WHEN Action = 2 THEN 1 ELSE 0 END, LEFT(Reason, 1000),
       CreatedOnUtc, CreatedById, CreatedOnUtc, CreatedById, CreatedOnUtc
FROM src;');");

            // 2) Entries added by hand (no customer link): the block at creation; an Inactive entry was unblocked
            //    at its last update.
            migrationBuilder.Sql(@"
WITH src AS (
    SELECT e.*, LEFT(COALESCE(NULLIF(LOWER(LTRIM(RTRIM(e.Email))), ''),
                              'name:' + LOWER(LTRIM(RTRIM(CONCAT(e.FirstName, ' ', e.LastName))))), 300) AS SubjectKey
    FROM CustomerBlacklist e
    WHERE e.CustomerId IS NULL
)
INSERT INTO CustomerBlacklistEvents (Id, SubjectKey, BlacklistEntryId, CustomerId, FirstName, LastName, Email, Action, Reason, AtUtc, CreatedById, CreatedOnUtc, UpdatedById, UpdatedOnUtc)
SELECT NEWID(), SubjectKey, Id, NULL, FirstName, LastName, Email, 0, Reason, CreatedOnUtc, CreatedById, CreatedOnUtc, CreatedById, CreatedOnUtc
FROM src
UNION ALL
SELECT NEWID(), SubjectKey, Id, NULL, FirstName, LastName, Email, 1, Reason, UpdatedOnUtc, UpdatedById, UpdatedOnUtc, UpdatedById, UpdatedOnUtc
FROM src WHERE Status = 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerBlacklistEvents");
        }
    }
}
