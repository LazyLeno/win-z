using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using WinZ.Models;
using WinZ.Engine;

namespace WinZ.Services;

public class DataService
{
    private readonly string _dbPath;

    public DataService()
    {
        var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinZ");
        Directory.CreateDirectory(appData);
        _dbPath = Path.Combine(appData, "winz_vault.db");

        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var connection = new SqliteConnection(string.Format("Data Source={0}", _dbPath));
        connection.Open();

        using var transaction = connection.BeginTransaction();
        try
        {
            CreateTables(connection, transaction);

            // Version check
            int currentDbVersion = GetStoredVersion(connection, transaction);
            if (currentDbVersion < MasterSeed.SeedVersion)
            {
                SeedFromMasterCode(connection, transaction);
                UpdateStoredVersion(connection, transaction, MasterSeed.SeedVersion);
            }

            transaction.Commit();
        }
        catch (Exception)
        {
            transaction.Rollback();
            throw;
        }
    }

    private static void CreateTables(SqliteConnection conn, SqliteTransaction trans)
    {
        using var command = conn.CreateCommand();
        command.Transaction = trans;
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS SetupTasks (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Type TEXT NOT NULL,
                Method TEXT,
                PackageId TEXT,
                Category TEXT,
                SubCategory TEXT,
                Icon TEXT,
                IconUrl TEXT,
                Description TEXT,
                TweakScript TEXT,
                FallbackUrl TEXT,
                IsSelected INTEGER DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS InstallationHistory (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                TaskName TEXT NOT NULL,
                Status TEXT NOT NULL,
                Timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
                ErrorDetail TEXT
            );

            CREATE TABLE IF NOT EXISTS AppMetadata (
                Key TEXT PRIMARY KEY,
                Value TEXT
            );
        ";
        command.ExecuteNonQuery();
    }

    private static int GetStoredVersion(SqliteConnection conn, SqliteTransaction trans)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = trans;
        cmd.CommandText = "SELECT Value FROM AppMetadata WHERE Key = 'SeedVersion'";
        var val = cmd.ExecuteScalar();
        return val != null && int.TryParse(val.ToString(), out int res) ? res : 0;
    }

    private static void UpdateStoredVersion(SqliteConnection conn, SqliteTransaction trans, int version)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = trans;
        cmd.CommandText = "INSERT OR REPLACE INTO AppMetadata (Key, Value) VALUES ('SeedVersion', $v)";
        cmd.Parameters.AddWithValue("$v", version.ToString());
        cmd.ExecuteNonQuery();
    }

    private static void SeedFromMasterCode(SqliteConnection conn, SqliteTransaction trans)
    {
        using var clearCmd = conn.CreateCommand();
        clearCmd.Transaction = trans;
        clearCmd.CommandText = "DELETE FROM SetupTasks";
        clearCmd.ExecuteNonQuery();

        var all = MasterSeed.GetDefaultTasks();
        foreach (var t in all)
        {
            InsertTask(conn, trans, t);
        }
    }

    private static void InsertTask(SqliteConnection conn, SqliteTransaction trans, SetupTask t)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = trans;
        cmd.CommandText = @"
            INSERT INTO SetupTasks (Name, Type, Method, PackageId, Category, SubCategory, Icon, IconUrl, Description, TweakScript, FallbackUrl, IsSelected)
            VALUES ($name, $type, $method, $pid, $cat, $sub, $icon, $iconUrl, $desc, $script, $fallback, $sel)
        ";
        cmd.Parameters.AddWithValue("$name", t.Name ?? "");
        cmd.Parameters.AddWithValue("$type", t.Type.ToString());
        cmd.Parameters.AddWithValue("$method", t.Method.HasValue ? t.Method.Value.ToString() : DBNull.Value);
        cmd.Parameters.AddWithValue("$pid", (object?)t.PackageId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$cat", t.Category ?? "");
        cmd.Parameters.AddWithValue("$sub", t.SubCategory ?? "");
        cmd.Parameters.AddWithValue("$icon", t.Icon ?? "");
        cmd.Parameters.AddWithValue("$iconUrl", (object?)t.IconUrl?.ToString() ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$desc", t.Description ?? "");
        cmd.Parameters.AddWithValue("$script", (object?)t.TweakScript ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$fallback", (object?)t.FallbackUrl?.ToString() ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$sel", t.IsSelected ? 1 : 0);
        cmd.ExecuteNonQuery();
    }

    public List<SetupTask> LoadTasks()
    {
        var tasks = new List<SetupTask>();
        using var connection = new SqliteConnection(string.Format("Data Source={0}", _dbPath));
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Name, Type, Method, PackageId, Category, SubCategory, Icon, IconUrl, Description, TweakScript, FallbackUrl, IsSelected FROM SetupTasks";
        
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            tasks.Add(new SetupTask
            {
                Name = reader.IsDBNull(1) ? "" : reader.GetString(1),
                Type = Enum.Parse<TaskType>(reader.GetString(2)),
                Method = reader.IsDBNull(3) ? (InstallMethod?)null : Enum.Parse<InstallMethod>(reader.GetString(3)),
                PackageId = reader.IsDBNull(4) ? null : reader.GetString(4),
                Category = reader.IsDBNull(5) ? "" : reader.GetString(5),
                SubCategory = reader.IsDBNull(6) ? "" : reader.GetString(6),
                Icon = reader.IsDBNull(7) ? "" : reader.GetString(7),
                IconUrl = reader.IsDBNull(8) ? null : new Uri(reader.GetString(8)),
                Description = reader.IsDBNull(9) ? "" : reader.GetString(9),
                TweakScript = reader.IsDBNull(10) ? null : reader.GetString(10),
                FallbackUrl = reader.IsDBNull(11) ? null : new Uri(reader.GetString(11)),
                IsSelected = reader.GetInt32(12) == 1
            });
        }
        return tasks;
    }


    public void SaveResult(SetupResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        
        using var connection = new SqliteConnection(string.Format("Data Source={0}", _dbPath));
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "INSERT INTO InstallationHistory (TaskName, Status, ErrorDetail) VALUES ($n, $s, $e)";
        cmd.Parameters.AddWithValue("$n", result.Name ?? "Unknown");
        cmd.Parameters.AddWithValue("$s", result.Status.ToString());
        cmd.Parameters.AddWithValue("$e", (object?)result.ErrorDetail ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    public void ExportToJson(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        
        var tasks = LoadTasks();
        var config = new ExpressConfig(
            tasks.Where(t => t.Type == TaskType.Install).ToList(),
            tasks.Where(t => t.Type == TaskType.Tweak).ToList(),
            tasks.Where(t => t.Type == TaskType.Remove).ToList()
        );

        var json = JsonConvert.SerializeObject(config, Formatting.Indented);
        File.WriteAllText(path, json);
    }

    public void ForceSyncFromCode()
    {
        using var connection = new SqliteConnection(string.Format("Data Source={0}", _dbPath));
        connection.Open();
        using var trans = connection.BeginTransaction();
        SeedFromMasterCode(connection, trans);
        UpdateStoredVersion(connection, trans, MasterSeed.SeedVersion);
        trans.Commit();
    }
}

