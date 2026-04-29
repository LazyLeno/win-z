using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using System.Text.Json;
using WinZ.Models;
using WinZ.Engine;

namespace WinZ.Services;

public class DataService
{
    private readonly string _dbPath;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public DataService()
    {
        var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinZ");
        Directory.CreateDirectory(appData);
        _dbPath = Path.Combine(appData, "winz_vault_v11.db");

        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();

        // Create tables using raw SQL for maximum control and minimum overhead
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS SetupTasks (
                Id TEXT PRIMARY KEY,
                Name TEXT,
                Type TEXT,
                Method TEXT,
                PackageId TEXT,
                FallbackUrl TEXT,
                TweakScript TEXT,
                Category TEXT,
                SubCategory TEXT,
                Description TEXT,
                Icon TEXT,
                Status TEXT,
                IsSelected INTEGER,
                RetryMax INTEGER,
                RequiresExplorerRestart INTEGER DEFAULT 0
            );";
        cmd.ExecuteNonQuery();

        // Migration: Ensure column exists in older versions
        try
        {
            cmd.CommandText = "ALTER TABLE SetupTasks ADD COLUMN RequiresExplorerRestart INTEGER DEFAULT 0";
            cmd.ExecuteNonQuery();
        }
        catch { /* already exists */ }
        
        // ... rest of the tables ...
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS InstallationHistory (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                TaskName TEXT,
                Status TEXT,
                ErrorDetail TEXT,
                Timestamp DATETIME DEFAULT CURRENT_TIMESTAMP
            );
            CREATE TABLE IF NOT EXISTS AppMetadata (
                Key TEXT PRIMARY KEY,
                Value TEXT
            );";
        cmd.ExecuteNonQuery();

        // Version check
        cmd.CommandText = "SELECT Value FROM AppMetadata WHERE Key = 'SeedVersion'";
        var versionVal = cmd.ExecuteScalar()?.ToString();
        int currentDbVersion = int.TryParse(versionVal, out int v) ? v : 0;

        if (currentDbVersion < MasterSeed.SeedVersion)
        {
            SeedFromMasterCode(conn);
            cmd.CommandText = "INSERT OR REPLACE INTO AppMetadata (Key, Value) VALUES ('SeedVersion', @v)";
            cmd.Parameters.AddWithValue("@v", MasterSeed.SeedVersion.ToString());
            cmd.ExecuteNonQuery();
        }
    }

    private void SeedFromMasterCode(SqliteConnection conn)
    {
        var masterTasks = MasterSeed.GetDefaultTasks();
        
        using var transaction = conn.BeginTransaction();
        try
        {
            foreach (var task in masterTasks)
            {
                using var cmd = conn.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    INSERT INTO SetupTasks (Id, Name, Type, Method, PackageId, FallbackUrl, TweakScript, Category, SubCategory, Description, Icon, Status, IsSelected, RetryMax, RequiresExplorerRestart)
                    VALUES (@id, @name, @type, @method, @packageId, @fallbackUrl, @tweakScript, @category, @subCategory, @description, @icon, @status, @isSelected, @retryMax, @requiresExplorerRestart)
                    ON CONFLICT(Id) DO UPDATE SET
                        Type = excluded.Type,
                        Method = excluded.Method,
                        PackageId = excluded.PackageId,
                        FallbackUrl = excluded.FallbackUrl,
                        TweakScript = excluded.TweakScript,
                        Category = excluded.Category,
                        SubCategory = excluded.SubCategory,
                        Description = excluded.Description,
                        Status = excluded.Status,
                        Icon = excluded.Icon,
                        RequiresExplorerRestart = excluded.RequiresExplorerRestart;";
                
                cmd.Parameters.AddWithValue("@id", task.Id);
                cmd.Parameters.AddWithValue("@name", task.Name);
                cmd.Parameters.AddWithValue("@type", task.Type.ToString());
                cmd.Parameters.AddWithValue("@method", task.Method?.ToString() ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@packageId", task.PackageId ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@fallbackUrl", task.FallbackUrl?.ToString() ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@tweakScript", task.TweakScript ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@category", task.Category);
                cmd.Parameters.AddWithValue("@subCategory", task.SubCategory);
                cmd.Parameters.AddWithValue("@description", task.Description);
                cmd.Parameters.AddWithValue("@icon", task.Icon);
                cmd.Parameters.AddWithValue("@status", task.Status.ToString());
                cmd.Parameters.AddWithValue("@isSelected", task.IsSelected ? 1 : 0);
                cmd.Parameters.AddWithValue("@retryMax", task.RetryMax);
                cmd.Parameters.AddWithValue("@requiresExplorerRestart", task.RequiresExplorerRestart ? 1 : 0);
                cmd.ExecuteNonQuery();
            }

            // Cleanup removed tasks using IDs (not names) to ensure old random GUIDs are purged
            using var cleanupCmd = conn.CreateCommand();
            cleanupCmd.Transaction = transaction;
            var masterIds = string.Join(",", masterTasks.Select(t => $"'{t.Id}'"));
            cleanupCmd.CommandText = $"DELETE FROM SetupTasks WHERE Id NOT IN ({masterIds})";
            cleanupCmd.ExecuteNonQuery();

            transaction.Commit();
        }
        catch { transaction.Rollback(); throw; }
    }

    public List<SetupTask> LoadTasks()
    {
        var tasks = new List<SetupTask>();
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM SetupTasks";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            tasks.Add(new SetupTask
            {
                Id = reader.GetString(0),
                Name = reader.IsDBNull(1) ? "" : reader.GetString(1),
                Type = Enum.Parse<TaskType>(reader.GetString(2)),
                Method = reader.IsDBNull(3) ? null : Enum.Parse<InstallMethod>(reader.GetString(3)),
                PackageId = reader.IsDBNull(4) ? null : reader.GetString(4),
                FallbackUrl = reader.IsDBNull(5) ? null : new Uri(reader.GetString(5)),
                TweakScript = reader.IsDBNull(6) ? null : reader.GetString(6),
                Category = reader.IsDBNull(7) ? "General" : reader.GetString(7),
                SubCategory = reader.IsDBNull(8) ? "Misc" : reader.GetString(8),
                Description = reader.IsDBNull(9) ? "" : reader.GetString(9),
                Icon = reader.IsDBNull(10) ? "" : reader.GetString(10),
                Status = reader.IsDBNull(11) ? TaskStatus.Queued : Enum.Parse<TaskStatus>(reader.GetString(11)),
                IsSelected = !reader.IsDBNull(12) && reader.GetInt32(12) == 1,
                RetryMax = reader.IsDBNull(13) ? 3 : reader.GetInt32(13),
                RequiresExplorerRestart = !reader.IsDBNull(14) && reader.GetInt32(14) == 1
            });
        }
        return tasks;
    }

    public void SaveTaskSelection(SetupTask task)
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE SetupTasks SET IsSelected = @sel WHERE Id = @id";
        cmd.Parameters.AddWithValue("@sel", task.IsSelected ? 1 : 0);
        cmd.Parameters.AddWithValue("@id", task.Id);
        cmd.ExecuteNonQuery();
    }

    public void SaveResult(SetupResult result)
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO InstallationHistory (TaskName, Status, ErrorDetail) VALUES (@name, @status, @err)";
        cmd.Parameters.AddWithValue("@name", result.Name);
        cmd.Parameters.AddWithValue("@status", result.Status.ToString());
        cmd.Parameters.AddWithValue("@err", result.ErrorDetail ?? (object)DBNull.Value);
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
        File.WriteAllText(path, JsonSerializer.Serialize(config, JsonOptions));
    }

    public void ForceSyncFromCode()
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        SeedFromMasterCode(conn);
    }

    public List<string> GetInstalledTaskNames()
    {
        var installed = new List<string>();
        try
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT DISTINCT TaskName FROM InstallationHistory WHERE Status = 'Success'";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                installed.Add(reader.GetString(0));
            }
        }
        catch { /* best effort */ }
        return installed;
    }
}
