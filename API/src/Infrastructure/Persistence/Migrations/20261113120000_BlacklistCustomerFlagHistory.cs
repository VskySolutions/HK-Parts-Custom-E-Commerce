using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HKPartsEcommerce.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Moves the blacklist's current state onto <c>Customers.IsBlacklisted</c> and turns
    /// <c>BlacklistedCustomers</c> into an append-only block/unblock history.
    ///
    /// Backfill:
    ///  - every customer with an active (Deleted = 0) history row is marked IsBlacklisted = 1;
    ///  - every existing history row becomes Action = 1 (Blocked), keeping its reason, notes, actor and timestamp;
    ///  - each soft-deleted row (a reversed block) additionally gets a matching Action = 2 (Unblocked) row at
    ///    DeletedOnUtc (actor = UpdatedById, falling back to CreatedById), so the full timeline is preserved.
    /// </summary>
    public partial class BlacklistCustomerFlagHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsBlacklisted",
                table: "Customers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_IsBlacklisted",
                table: "Customers",
                column: "IsBlacklisted");

            // Action is added nullable, backfilled, then made required so existing rows never need a default.
            migrationBuilder.AddColumn<int>(
                name: "Action",
                table: "BlacklistedCustomers",
                type: "int",
                nullable: true);

            migrationBuilder.Sql("UPDATE [BlacklistedCustomers] SET [Action] = 1 WHERE [Action] IS NULL;");

            migrationBuilder.AlterColumn<int>(
                name: "Action",
                table: "BlacklistedCustomers",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            // Unblocked rows do not carry a reason.
            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                table: "BlacklistedCustomers",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000);

            // Current state = any active (Deleted = 0) history row before the soft-delete columns disappear.
            migrationBuilder.Sql(@"
UPDATE c
SET c.[IsBlacklisted] = 1
FROM [Customers] c
WHERE EXISTS (
    SELECT 1
    FROM [BlacklistedCustomers] b
    WHERE b.[CustomerId] = c.[Id] AND b.[Deleted] = 0
);");

            // Drop the one-active-row-per-customer index before appending history rows for the same customer.
            migrationBuilder.DropIndex(
                name: "UX_BlacklistedCustomers_CustomerId_Active",
                table: "BlacklistedCustomers");

            // Preserve the unblock half of the timeline for rows that soft-deleted a block. The old NOT NULL
            // UpdatedOnUtc column still exists here, so it is populated as well (both columns are dropped below).
            migrationBuilder.Sql(@"
INSERT INTO [BlacklistedCustomers] ([Id], [CustomerId], [Action], [Reason], [InternalNotes], [CreatedById], [CreatedOnUtc], [Deleted], [UpdatedById], [UpdatedOnUtc])
SELECT NEWID(), b.[CustomerId], 2, NULL, NULL, COALESCE(b.[UpdatedById], b.[CreatedById]), b.[DeletedOnUtc], 1,
       COALESCE(b.[UpdatedById], b.[CreatedById]), b.[DeletedOnUtc]
FROM [BlacklistedCustomers] b
WHERE b.[Deleted] = 1 AND b.[DeletedOnUtc] IS NOT NULL;");

            // The old Deleted column carries a system-named default constraint; drop it before the column.
            migrationBuilder.Sql(@"
DECLARE @defaultConstraint sysname;
SELECT @defaultConstraint = dc.[name]
FROM sys.default_constraints dc
INNER JOIN sys.columns c
    ON c.[object_id] = dc.[parent_object_id] AND c.[column_id] = dc.[parent_column_id]
WHERE dc.[parent_object_id] = OBJECT_ID(N'[BlacklistedCustomers]') AND c.[name] = N'Deleted';
IF @defaultConstraint IS NOT NULL
    EXEC(N'ALTER TABLE [BlacklistedCustomers] DROP CONSTRAINT [' + @defaultConstraint + N']');");

            migrationBuilder.DropColumn(name: "Deleted", table: "BlacklistedCustomers");
            migrationBuilder.DropColumn(name: "DeletedOnUtc", table: "BlacklistedCustomers");
            migrationBuilder.DropColumn(name: "UpdatedById", table: "BlacklistedCustomers");
            migrationBuilder.DropColumn(name: "UpdatedOnUtc", table: "BlacklistedCustomers");

            migrationBuilder.CreateIndex(
                name: "IX_BlacklistedCustomers_CustomerId_CreatedOnUtc",
                table: "BlacklistedCustomers",
                columns: new[] { "CustomerId", "CreatedOnUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BlacklistedCustomers_CustomerId_CreatedOnUtc",
                table: "BlacklistedCustomers");

            // Best-effort reconstruction of the old "one active row per customer + soft-deleted history" shape.
            migrationBuilder.AddColumn<bool>(
                name: "Deleted",
                table: "BlacklistedCustomers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedOnUtc",
                table: "BlacklistedCustomers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedById",
                table: "BlacklistedCustomers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedOnUtc",
                table: "BlacklistedCustomers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.Sql("UPDATE [BlacklistedCustomers] SET [UpdatedOnUtc] = [CreatedOnUtc] WHERE [UpdatedOnUtc] IS NULL;");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedOnUtc",
                table: "BlacklistedCustomers",
                type: "datetime2",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            // Unblock events can only be represented as soft-deleted rows in the old schema.
            migrationBuilder.Sql(@"
UPDATE [BlacklistedCustomers]
SET [Deleted] = 1, [DeletedOnUtc] = [CreatedOnUtc]
WHERE [Action] = 2;");

            // Restore each reversed block's original DeletedOnUtc from its matching unblock event when possible.
            migrationBuilder.Sql(@"
UPDATE b
SET b.[DeletedOnUtc] = (
    SELECT MIN(u.[CreatedOnUtc])
    FROM [BlacklistedCustomers] u
    WHERE u.[CustomerId] = b.[CustomerId]
      AND u.[Action] = 2
      AND u.[CreatedOnUtc] >= b.[CreatedOnUtc])
FROM [BlacklistedCustomers] b
WHERE b.[Action] = 1;");

            // Keep exactly one active blocked row (the latest) for customers that are currently blacklisted;
            // every other blocked event becomes soft-deleted history.
            migrationBuilder.Sql(@"
UPDATE b
SET b.[Deleted] = 1, b.[DeletedOnUtc] = COALESCE(b.[DeletedOnUtc], b.[CreatedOnUtc])
FROM [BlacklistedCustomers] b
WHERE b.[Action] = 1
  AND (
      NOT EXISTS (
          SELECT 1 FROM [Customers] c
          WHERE c.[Id] = b.[CustomerId] AND c.[IsBlacklisted] = 1)
      OR EXISTS (
          SELECT 1 FROM [BlacklistedCustomers] later
          WHERE later.[CustomerId] = b.[CustomerId]
            AND later.[Action] = 1
            AND (later.[CreatedOnUtc] > b.[CreatedOnUtc]
                 OR (later.[CreatedOnUtc] = b.[CreatedOnUtc] AND later.[Id] > b.[Id])))
  );");

            // Reason was NOT NULL before this migration; unblock rows are the only nulls.
            migrationBuilder.Sql("UPDATE [BlacklistedCustomers] SET [Reason] = N'Unblocked' WHERE [Reason] IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                table: "BlacklistedCustomers",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.DropColumn(name: "Action", table: "BlacklistedCustomers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_IsBlacklisted",
                table: "Customers");

            migrationBuilder.DropColumn(name: "IsBlacklisted", table: "Customers");

            migrationBuilder.CreateIndex(
                name: "UX_BlacklistedCustomers_CustomerId_Active",
                table: "BlacklistedCustomers",
                column: "CustomerId",
                unique: true,
                filter: "[Deleted] = 0");
        }
    }
}
