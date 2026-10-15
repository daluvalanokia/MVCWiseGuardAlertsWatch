using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace MVCWiseGuardAlertsWatch.Services
{
    /// <summary>
    /// SQLite in-place schema upgrade. EnsureCreated() only creates a missing
    /// database — it never alters an existing one, so a DB created by an older
    /// build (e.g. before ProxyLocations.DisplayName existed) crashes the app
    /// with 'no such column'. This adds any model columns the file is missing.
    /// </summary>
    public static class DbSchemaUpgrader
    {
        public static void Apply(AppDbContext db)
        {
            var connection = db.Database.GetDbConnection();
            var wasClosed = connection.State == ConnectionState.Closed;
            if (wasClosed) connection.Open();
            try
            {
                using var tx = connection.BeginTransaction();
                foreach (var entityType in db.Model.GetEntityTypes())
                {
                    var table = entityType.GetTableName();
                    if (string.IsNullOrEmpty(table)) continue;

                    var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    using (var pragma = connection.CreateCommand())
                    {
                        pragma.Transaction = tx;
                        pragma.CommandText = "PRAGMA table_info(\"" + table.Replace("\"", "\"\"") + "\")";
                        using var reader = pragma.ExecuteReader();
                        int nameIdx = reader.GetOrdinal("name");
                        while (reader.Read()) existing.Add(reader.GetString(nameIdx));
                    }
                    if (existing.Count == 0) continue; // table absent -> EnsureCreated handles it

                    var tableId = StoreObjectIdentifier.Table(table, entityType.GetSchema());
                    foreach (var prop in entityType.GetProperties())
                    {
                        var column = prop.GetColumnName(tableId);
                        if (string.IsNullOrEmpty(column) || existing.Contains(column)) continue;

                        var type = prop.GetColumnType() ?? "TEXT";
                        var notNull = !prop.IsNullable && !prop.IsPrimaryKey();
                        // SQLite requires a DEFAULT when adding a NOT NULL column
                        var defaultValue = type.ToUpperInvariant() switch
                        {
                            "INTEGER" => "0",
                            "REAL" => "0",
                            _ => "''"
                        };
                        var defaultClause = notNull ? " NOT NULL DEFAULT " + defaultValue : "";

                        using var cmd = connection.CreateCommand();
                        cmd.Transaction = tx;
                        cmd.CommandText =
                            "ALTER TABLE \"" + table.Replace("\"", "\"\"") + "\" " +
                            "ADD COLUMN \"" + column.Replace("\"", "\"\"") + "\" " + type + defaultClause;
                        cmd.ExecuteNonQuery();
                        Console.WriteLine("[schema] added missing column " + table + "." + column + " (" + type + ")");
                    }
                }
                tx.Commit();
            }
            finally
            {
                if (wasClosed) connection.Close();
            }
        }
    }
}
