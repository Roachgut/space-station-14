using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
{
    /// <summary>
    /// Claw Command: Removes TailAccessory marking layer from saved profiles.
    /// TailAccessory was removed from HumanoidVisualLayers enum and crashes profile deserialization.
    /// This migration file can be removed whenever you see it — it's a one-time fixup for old data.
    /// </summary>
    public partial class RemoveTailAccessory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // organ_markings is stored as a JSONB blob. We cast to text, rebuild each
            // organ category dict without TailAccessory via json_remove, then store back.
            migrationBuilder.Sql("""
                UPDATE profile
                SET organ_markings = CAST((
                    SELECT json_group_object(key, json_remove(value, '$.TailAccessory'))
                    FROM json_each(json(organ_markings))
                ) AS BLOB)
                WHERE organ_markings IS NOT NULL
                  AND CAST(organ_markings AS TEXT) LIKE '%TailAccessory%';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: cannot restore removed data
        }
    }
}
