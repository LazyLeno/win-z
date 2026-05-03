using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using System.Text.Json;
using WinZ.Models;
using WinZ.Engine;
using TaskStatus = WinZ.Models.TaskStatus;

namespace WinZ.Services;

public class DataService
{
    private static readonly Lazy<DataService> _instance = new(() => new DataService());
    public static DataService Instance => _instance.Value;

    /// <summary>
    /// When true, all data is kept strictly in memory — no files are written to disk.
    /// Must be set before <see cref="Instance"/> is first accessed.
    /// </summary>
    public static bool IsPortableMode { get; set; }

    private readonly string? _dbPath;
    private readonly SqliteConnection? _memoryConn; // kept alive for the session in portable mode
    private readonly System.Threading.SemaphoreSlim _lock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new() 
    { 
        WriteIndented = true,
        TypeInfoResolver = AppJsonContext.Default
    };

    public DataService()
    {
        if (IsPortableMode)
        {
            _dbPath = null;
            // In-memory: use a named shared cache so the same DB is accessible across
            // multiple SqliteConnection opens within this process.
            _memoryConn = new SqliteConnection("Data Source=WinZMemory;Mode=Memory;Cache=Shared");
            _memoryConn.Open();
            InitializeDatabase(_memoryConn);
        }
        else
        {
            var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinZ");
            Directory.CreateDirectory(appData);
            _dbPath = Path.Combine(appData, "winz_vault_v11.db");
            using var initConn = OpenFileConnection();
            InitializeDatabase(initConn);
        }
    }

    // ── Connection helpers ────────────────────────────────────────────────────
    private SqliteConnection OpenConnection()
    {
        if (IsPortableMode)
        {
            // Return a new connection wired to the same shared memory cache
            var c = new SqliteConnection("Data Source=WinZMemory;Mode=Memory;Cache=Shared");
            c.Open();
            return c;
        }
        return OpenFileConnection();
    }

    private SqliteConnection OpenFileConnection()
    {
        var c = new SqliteConnection($"Data Source={_dbPath}");
        c.Open();
        return c;
    }

    private void InitializeDatabase(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            PRAGMA journal_mode=WAL;
            PRAGMA synchronous=NORMAL;
            CREATE TABLE IF NOT EXISTS SetupTasks (
                Id TEXT PRIMARY KEY,
                Name TEXT,
                Type TEXT,
                Method TEXT,
                PackageId TEXT,
                FallbackUrl TEXT,
                TweakScript TEXT,
                Category TEXT,
                Section TEXT,
                SubCategory TEXT,
                Description TEXT,
                Icon TEXT,
                Status TEXT,
                IsSelected INTEGER,
                RetryMax INTEGER,
                RequiresExplorerRestart INTEGER DEFAULT 0,
                ModdedName TEXT,
                ModdedDescription TEXT,
                ModdedPackageId TEXT,
                ModdedMethod TEXT,
                ModdedFallbackUrl TEXT,
                ModdedTweakScript TEXT,
                ModdedType TEXT
            );";
        cmd.ExecuteNonQuery();

        // Migration: Ensure columns exist
        string[] columns = { 
            "Section TEXT",
            "RequiresExplorerRestart INTEGER DEFAULT 0", 
            "ModdedName TEXT", 
            "ModdedDescription TEXT", 
            "ModdedPackageId TEXT", 
            "ModdedMethod TEXT", 
            "ModdedFallbackUrl TEXT", 
            "ModdedTweakScript TEXT",
            "ModdedType TEXT"
        };
        foreach (var col in columns)
        {
            try { cmd.CommandText = $"ALTER TABLE SetupTasks ADD COLUMN {col}"; cmd.ExecuteNonQuery(); } catch { }
        }
        
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS InstallationHistory (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                TaskName TEXT,
                Status TEXT,
                ErrorDetail TEXT,
                Timestamp DATETIME DEFAULT CURRENT_TIMESTAMP
            );
            CREATE INDEX IF NOT EXISTS IDX_History_TaskName ON InstallationHistory(TaskName);
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
            try
            {
                SeedFromMasterCode(conn);
                cmd.CommandText = "INSERT OR REPLACE INTO AppMetadata (Key, Value) VALUES ('SeedVersion', @v)";
                cmd.Parameters.AddWithValue("@v", MasterSeed.SeedVersion.ToString());
                cmd.ExecuteNonQuery();
            }
            catch
            {
                // If seeding fails for any reason, fail gracefully.
                // This prevents a startup crash and allows the app to run with existing data.
            }
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
                    INSERT INTO SetupTasks (Id, Name, Type, Method, PackageId, FallbackUrl, TweakScript, Category, Section, SubCategory, Description, Icon, Status, IsSelected, RetryMax, RequiresExplorerRestart, ModdedName, ModdedDescription, ModdedPackageId, ModdedMethod, ModdedFallbackUrl, ModdedTweakScript, ModdedType)
                    VALUES (@id, @name, @type, @method, @packageId, @fallbackUrl, @tweakScript, @category, @section, @subCategory, @description, @icon, @status, @isSelected, @retryMax, @requiresExplorerRestart, @modName, @modDesc, @modPkg, @modMeth, @modUrl, @modScript, @modType)
                    ON CONFLICT(Id) DO UPDATE SET
                        Type = excluded.Type,
                        Method = excluded.Method,
                        PackageId = excluded.PackageId,
                        FallbackUrl = excluded.FallbackUrl,
                        TweakScript = excluded.TweakScript,
                        Category = excluded.Category,
                        Section = excluded.Section,
                        SubCategory = excluded.SubCategory,
                        Description = excluded.Description,
                        Status = excluded.Status,
                        Icon = excluded.Icon,
                        RequiresExplorerRestart = excluded.RequiresExplorerRestart,
                        ModdedName = excluded.ModdedName,
                        ModdedDescription = excluded.ModdedDescription,
                        ModdedPackageId = excluded.ModdedPackageId,
                        ModdedMethod = excluded.ModdedMethod,
                        ModdedFallbackUrl = excluded.ModdedFallbackUrl,
                        ModdedTweakScript = excluded.ModdedTweakScript,
                        ModdedType = excluded.ModdedType;";
                
                cmd.Parameters.AddWithValue("@id", task.Id);
                cmd.Parameters.AddWithValue("@name", task.Name);
                cmd.Parameters.AddWithValue("@type", task.Type.ToString());
                cmd.Parameters.AddWithValue("@method", task.Method?.ToString() ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@packageId", task.PackageId ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@fallbackUrl", task.FallbackUrl?.ToString() ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@tweakScript", task.TweakScript ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@category", task.Category);
                cmd.Parameters.AddWithValue("@section", task.Section ?? "");
                cmd.Parameters.AddWithValue("@subCategory", task.SubCategory);
                cmd.Parameters.AddWithValue("@description", task.Description);
                cmd.Parameters.AddWithValue("@icon", task.Icon);
                cmd.Parameters.AddWithValue("@status", task.Status.ToString());
                cmd.Parameters.AddWithValue("@isSelected", task.IsSelected ? 1 : 0);
                cmd.Parameters.AddWithValue("@retryMax", task.RetryMax);
                cmd.Parameters.AddWithValue("@requiresExplorerRestart", task.RequiresExplorerRestart ? 1 : 0);
                cmd.Parameters.AddWithValue("@modName", task.ModdedName ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@modDesc", task.ModdedDescription ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@modPkg", task.ModdedPackageId ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@modMeth", task.ModdedMethod?.ToString() ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@modUrl", task.ModdedFallbackUrl?.ToString() ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@modScript", task.ModdedTweakScript ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@modType", task.ModdedType?.ToString() ?? (object)DBNull.Value);
                cmd.ExecuteNonQuery();
            }

            using var cleanupCmd = conn.CreateCommand();
            cleanupCmd.Transaction = transaction;
            var masterIds = string.Join(",", masterTasks.Select(t => $"'{t.Id}'"));
            cleanupCmd.CommandText = $"DELETE FROM SetupTasks WHERE Id NOT IN ({masterIds})";
            cleanupCmd.ExecuteNonQuery();

            transaction.Commit();
        }
        catch { transaction.Rollback(); throw; }
    }

    public async Task<List<SetupTask>> LoadTasksAsync()
    {
        await _lock.WaitAsync();
        try
        {
            var tasks = new List<SetupTask>();
            using var conn = OpenConnection();

            var allowModded = await GetSettingAsyncInternal(conn, "AllowModdedSoftware") == "True";

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT Id, Name, Type, Method, PackageId, FallbackUrl, TweakScript, 
                       Category, Section, SubCategory, Description, Icon, Status, 
                       IsSelected, RetryMax, RequiresExplorerRestart, 
                       ModdedName, ModdedDescription, ModdedPackageId, ModdedMethod, 
                       ModdedFallbackUrl, ModdedTweakScript, ModdedType 
                FROM SetupTasks";

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var task = new SetupTask
                {
                    Id = reader.GetString(0),
                    Name = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    Type = Enum.Parse<TaskType>(reader.GetString(2)),
                    Method = reader.IsDBNull(3) ? null : Enum.Parse<InstallMethod>(reader.GetString(3)),
                    PackageId = reader.IsDBNull(4) ? null : reader.GetString(4),
                    FallbackUrl = (!reader.IsDBNull(5) && Uri.TryCreate(reader.GetString(5), UriKind.Absolute, out var uri)) ? uri : null,
                    TweakScript = reader.IsDBNull(6) ? null : reader.GetString(6),
                    Category = reader.IsDBNull(7) ? "General" : reader.GetString(7),
                    Section = reader.IsDBNull(8) ? "" : reader.GetString(8),
                    SubCategory = reader.IsDBNull(9) ? "Misc" : reader.GetString(9),
                    Description = reader.IsDBNull(10) ? "" : reader.GetString(10),
                    Icon = reader.IsDBNull(11) ? "" : reader.GetString(11),
                    Status = reader.IsDBNull(12) ? TaskStatus.Queued : Enum.Parse<TaskStatus>(reader.GetString(12)),
                    IsSelected = !reader.IsDBNull(13) && reader.GetInt32(13) == 1,
                    RetryMax = reader.IsDBNull(14) ? 3 : reader.GetInt32(14),
                    RequiresExplorerRestart = !reader.IsDBNull(15) && reader.GetInt32(15) == 1,
                    ModdedName = reader.IsDBNull(16) ? null : reader.GetString(16),
                    ModdedDescription = reader.IsDBNull(17) ? null : reader.GetString(17),
                    ModdedPackageId = reader.IsDBNull(18) ? null : reader.GetString(18),
                    ModdedMethod = reader.IsDBNull(19) ? null : Enum.Parse<InstallMethod>(reader.GetString(19)),
                    ModdedFallbackUrl = reader.IsDBNull(20) ? null : new Uri(reader.GetString(20)),
                    ModdedTweakScript = reader.IsDBNull(21) ? null : reader.GetString(21),
                    ModdedType = reader.IsDBNull(22) ? null : Enum.Parse<TaskType>(reader.GetString(22))
                };
            
            task.IsModded = allowModded && task.CanBeModded;
            tasks.Add(task);
        }
        return tasks;
        }
        finally { _lock.Release(); }
    }

    public async Task SaveTaskSelectionAsync(SetupTask task)
    {
        await _lock.WaitAsync();
        try
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE SetupTasks SET IsSelected = @sel WHERE Id = @id";
            cmd.Parameters.AddWithValue("@sel", task.IsSelected ? 1 : 0);
            cmd.Parameters.AddWithValue("@id", task.Id);
            await cmd.ExecuteNonQueryAsync();
        }
        finally { _lock.Release(); }
    }

    public async Task SaveSettingAsync(string key, string value)
    {
        await _lock.WaitAsync();
        try
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT OR REPLACE INTO AppMetadata (Key, Value) VALUES (@k, @v)";
            cmd.Parameters.AddWithValue("@k", key);
            cmd.Parameters.AddWithValue("@v", value);
            await cmd.ExecuteNonQueryAsync();
        }
        finally { _lock.Release(); }
    }

    public async Task<string?> GetSettingAsync(string key)
    {
        await _lock.WaitAsync();
        try
        {
            using var conn = OpenConnection();
            return await GetSettingAsyncInternal(conn, key);
        }
        finally { _lock.Release(); }
    }

    private async Task<string?> GetSettingAsyncInternal(SqliteConnection conn, string key)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Value FROM AppMetadata WHERE Key = @k";
        cmd.Parameters.AddWithValue("@k", key);
        var result = await cmd.ExecuteScalarAsync();
        return result?.ToString();
    }

    public async Task SaveResultAsync(SetupResult result)
    {
        await _lock.WaitAsync();
        try
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO InstallationHistory (TaskName, Status, ErrorDetail) VALUES (@name, @status, @err)";
            cmd.Parameters.AddWithValue("@name", result.Name);
            cmd.Parameters.AddWithValue("@status", result.Status.ToString());
            cmd.Parameters.AddWithValue("@err", result.ErrorDetail ?? (object)DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }
        finally { _lock.Release(); }
    }


    public async Task ForceSyncFromCodeAsync()
    {
        using var conn = OpenConnection();
        SeedFromMasterCode(conn);
    }

    public async Task<List<string>> GetInstalledTaskNamesAsync()
    {
        var installed = new List<string>();
        try
        {
            using var conn = OpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT DISTINCT TaskName FROM InstallationHistory WHERE Status = 'Success'";
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                installed.Add(reader.GetString(0));
            }
        }
        catch { /* best effort */ }
        return installed;
    }

    // Keep sync versions for migration/startup if needed, but mark as obsolete or use sparingly
    [Obsolete("Use LoadTasksAsync instead")]
    public List<SetupTask> LoadTasks() => LoadTasksAsync().GetAwaiter().GetResult();
    
    public string? GetSetting(string key) => GetSettingAsync(key).GetAwaiter().GetResult();
    public void SaveSetting(string key, string value) => SaveSettingAsync(key, value).GetAwaiter().GetResult();
}
