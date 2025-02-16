using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.Modules.Memory;
using System.Text;
using System.Timers;

namespace FakeRconPlugin;

public class AuthCacheEntry
{
    public string SteamID { get; set; }
    public DateTime AuthTime { get; set; }

    public AuthCacheEntry(string steamId)
    {
        SteamID = steamId;
        AuthTime = DateTime.UtcNow;
    }
}

[MinimumApiVersion(300)]
public class FakeRconPlugin : BasePlugin
{
    public override string ModuleName => "Fake RCON";
    public override string ModuleVersion => "1.2.1";
    public override string ModuleAuthor => "Kriax (Original - MetaMod Plugin) / Converted by Miksen";
    public override string ModuleDescription => "Like the real RCON but it's not the real RCON, thank you Valve";

    private Dictionary<string, AuthCacheEntry> authenticatedUsers = new();
    private string? fakeRconPassword;
    private string? cacheFilePath;
    private System.Timers.Timer? cleanupTimer;
    private const int CACHE_CLEANUP_MINUTES = 120; // 2 hours

    public FileManager(string modPath)
    {
        // Ensure paths exist
        string configDir = Path.Combine(modPath, "configs", "plugins", "fakercon");
        Directory.CreateDirectory(configDir);

        configPath = Path.Combine(configDir, CONFIG_FILE);
        cachePath = Path.Combine(configDir, CACHE_FILE);

        LoadCache();
    }

    public string? GetRconPassword()
    {
        if (!File.Exists(configPath))
        {
            var config = new { RconPassword = "changeme" };
            File.WriteAllText(configPath, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
        }

        try
        {
            var config = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(configPath));
            return config?["RconPassword"];
        }
        catch
        {
            return null;
        }
    }

    public override void Load(bool hotReload)
    {
        // Initialize cache file path
        cacheFilePath = Path.Combine(ModuleDirectory, "cache.ini");

        // Create cache file if it doesn't exist
        if (!File.Exists(cacheFilePath))
        {
            File.WriteAllText(cacheFilePath, "");
        }

        // Setup cleanup timer
        cleanupTimer = new System.Timers.Timer(TimeSpan.FromMinutes(1).TotalMilliseconds); // Check every minute
        cleanupTimer.Elapsed += OnCleanupTimer;
        cleanupTimer.AutoReset = true;
        cleanupTimer.Start();

        fakeRconPassword = NativeAPI.GetCommandParamValue("-fakercon", DataType.DATA_TYPE_STRING, "unknown");
        
        if (hotReload)
        {
            LoadAuthCache();
        }

        RegisterListener<Listeners.OnClientConnected>(slot =>
        {
            var player = Utilities.GetPlayerFromSlot(slot);
            if (player != null && player.IsValid)
            {
                string steamId = player.SteamID.ToString();
                if (!authenticatedUsers.ContainsKey(steamId))
                {
                    authenticatedUsers[steamId] = new AuthCacheEntry(steamId);
                }
            }
        });

        RegisterListener<Listeners.OnClientDisconnect>(slot =>
        {
            var player = Utilities.GetPlayerFromSlot(slot);
            if (player != null && player.IsValid)
            {
                // Don't remove from cache on disconnect
                // Just let the timer handle cleanup
            }
        });

        AddCommand("fake_rcon", "Execute fake RCON command", CommandFakeRcon);
        AddCommand("fake_rcon_password", "Authenticate for fake RCON", CommandFakeRconPassword);
        AddCommand("fake_rcon_cache_clean", "Clean fake RCON authentication cache", CommandFakeRconCacheClean);
    }

    private void OnCleanupTimer(object? sender, ElapsedEventArgs e)
    {
        CleanupExpiredEntries();
    }

    private void CleanupExpiredEntries()
    {
        var now = DateTime.UtcNow;
        var expiredUsers = authenticatedUsers
            .Where(kvp => (now - kvp.Value.AuthTime).TotalMinutes > CACHE_CLEANUP_MINUTES)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var steamId in expiredUsers)
        {
            authenticatedUsers.Remove(steamId);
        }

        if (expiredUsers.Any())
        {
            SaveAuthCache();
        }
    }

    private void LoadAuthCache()
    {
        if (cacheFilePath == null || !File.Exists(cacheFilePath)) return;

        var lines = File.ReadAllLines(cacheFilePath);
        var now = DateTime.UtcNow;

        foreach (var line in lines)
        {
            var parts = line.Split('=');
            if (parts.Length == 2)
            {
                var steamId = parts[0].Trim();
                if (DateTime.TryParse(parts[1].Trim(), out DateTime authTime))
                {
                    if ((now - authTime).TotalMinutes <= CACHE_CLEANUP_MINUTES)
                    {
                        authenticatedUsers[steamId] = new AuthCacheEntry(steamId) { AuthTime = authTime };
                    }
                }
            }
        }
    }

    private void SaveAuthCache()
    {
        if (cacheFilePath == null) return;

        var sb = new StringBuilder();
        foreach (var entry in authenticatedUsers)
        {
            sb.AppendLine($"{entry.Key}={entry.Value.AuthTime:yyyy-MM-dd HH:mm:ss}");
        }
        File.WriteAllText(cacheFilePath, sb.ToString());
    }

    private void CommandFakeRconPassword(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid)
        {
            return;
        }

        if (command.ArgCount < 2)
        {
            player.PrintToConsole("Usage: fake_rcon_password <rconpass>");
            return;
        }

        string password = command.ArgByIndex(1);
        string steamId = player.SteamID.ToString();

        if (string.IsNullOrEmpty(fakeRconPassword) || fakeRconPassword == "unknown")
        {
            player.PrintToConsole("Bad rcon_password. Try again");
            player.PrintToChat($" \x08[RCON]\x01 Bad rcon_password. Try again");
            return;
        }

        if (password == fakeRconPassword)
        {
            authenticatedUsers[steamId] = new AuthCacheEntry(steamId);
            SaveAuthCache();

            player.PrintToConsole("RCON admin authentication successful.");
            player.PrintToConsole("Privileges granted.");
            player.PrintToChat($" \x08[RCON]\x01 Authentication successful");
        }
        else
        {
            player.PrintToConsole("Bad rcon_password.");
            authenticatedUsers.Remove(steamId);
            SaveAuthCache();
        }
    }

    private void CommandFakeRcon(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid)
        {
            return;
        }

        string steamId = player.SteamID.ToString();

        if (!authenticatedUsers.ContainsKey(steamId) || 
            (DateTime.UtcNow - authenticatedUsers[steamId].AuthTime).TotalMinutes > CACHE_CLEANUP_MINUTES)
        {
            player.PrintToConsole("You have not been authenticated.");
            player.PrintToChat($" \x08[RCON]\x01 You are not logged in");
            authenticatedUsers.Remove(steamId);
            SaveAuthCache();
            return;
        }

        if (command.ArgCount < 2)
        {
            player.PrintToConsole("No command specified.");
            player.PrintToChat($" \x08[RCON]\x01 No command");
            return;
        }

        string fullCommand = string.Join(" ", Enumerable.Range(1, command.ArgCount - 1)
            .Select(i => command.ArgByIndex(i)));

        Server.ExecuteCommand(fullCommand);
    }

    private void CommandFakeRconCacheClean(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid)
        {
            return;
        }

        string steamId = player.SteamID.ToString();

        if (!authenticatedUsers.ContainsKey(steamId) || 
            (DateTime.UtcNow - authenticatedUsers[steamId].AuthTime).TotalMinutes > CACHE_CLEANUP_MINUTES)
        {
            player.PrintToConsole("You have not been authenticated.");
            player.PrintToChat($" \x08[RCON]\x01 You are not logged in");
            return;
        }

        // Keep only the current user's authentication
        var currentAuth = authenticatedUsers[steamId];
        authenticatedUsers.Clear();
        authenticatedUsers[steamId] = currentAuth;
        
        SaveAuthCache();

        player.PrintToConsole("Fake RCON authentication cache has been cleared.");
        player.PrintToConsole("All other users must re-authenticate.");
        player.PrintToChat($" \x08[RCON]\x01 Authentication cache cleared");

        Console.WriteLine($"Fake RCON cache cleared by {player.PlayerName} ({steamId})");
    }

    public override void Unload(bool hotReload)
    {
        SaveAuthCache();
        if (cleanupTimer != null)
        {
            cleanupTimer.Stop();
            cleanupTimer.Dispose();
        }
    }
}
