using System.Collections.Generic;
using WinZ.Models;
using System;

namespace WinZ.Services;

public static class MasterSeed
{
    // INCREMENT THIS NUMBER whenever you change the tasks below to force an update in the app.
    public const int SeedVersion = 11; 

    public static List<SetupTask> GetDefaultTasks() => new()
    {
        // Software - Web Browsers
        new() { Name = "LibreWolf", Type = TaskType.Install, Method = InstallMethod.Winget, PackageId = "LibreWolf.LibreWolf", Category = "Software", SubCategory = "Web Browsers", Description = "Privacy-focused Firefox fork with enhanced security defaults.", IsSelected = true },
        new() { Name = "Brave", Type = TaskType.Install, Method = InstallMethod.Winget, PackageId = "Brave.Brave", Category = "Software", SubCategory = "Web Browsers", Description = "Fast browser with built-in ad blocking and privacy features." },
        
        // Software - Download Managers
        new() { Name = "Internet Download Manager (Pro)", Type = TaskType.Install, Method = InstallMethod.Scoop, PackageId = "https://raw.githubusercontent.com/LazyLeno/win-z/main/Scoop/IDM-pro/idm-custom.json", Category = "Software", SubCategory = "Download Managers", Description = "High-speed download accelerator with Pro features pre-configured.", IsSelected = true },
        
        // Software - Security & VPN
        new() { Name = "Proton VPN", Type = TaskType.Install, Method = InstallMethod.Winget, PackageId = "ProtonVPN.ProtonVPN", Category = "Software", SubCategory = "Security & VPN", Description = "Secure, no-logs VPN from the makers of ProtonMail.", IsSelected = true },
        
        // Software - Communication
        new() { Name = "Discord", Type = TaskType.Install, Method = InstallMethod.Winget, PackageId = "Discord.Discord", Category = "Software", SubCategory = "Communication", Description = "Voice, video and text communication for communities.", IsSelected = true },
        
        // Software - Music
        new() { Name = "Pear Desktop", Type = TaskType.Install, Method = InstallMethod.Scoop, PackageId = "extras/pear-desktop", Category = "Software", SubCategory = "Music", Description = "Feature-rich YouTube Music desktop client (open source).", IsSelected = true },
        
        // Software - File Managers
        new() { Name = "OneCommander", Type = TaskType.Install, Method = InstallMethod.Winget, PackageId = "MilosParipovic.OneCommander", Category = "Software", SubCategory = "File Managers", Description = "Modern dual-pane file manager with tabs and column view." },
        new() { Name = "Directory Opus", Type = TaskType.Install, Method = InstallMethod.Winget, PackageId = "GPSoftware.DirectoryOpus", Category = "Software", SubCategory = "File Managers", Description = "Professional file manager and Windows Explorer replacement.", IsSelected = true },
        
        // Windows - System Tweaks
        new() { Name = "Verbose Login Messages", Type = TaskType.Tweak, TweakScript = "reg add HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\System /v verbosestatus /t REG_DWORD /d 1 /f", Category = "Windows", SubCategory = "System Tweaks", Description = "Show detailed status messages during login and shutdown." },
        
        // Windows - Privacy
        new() { Name = "Disable Cortana", Type = TaskType.Tweak, TweakScript = "reg add HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Search /v AllowCortana /t REG_DWORD /d 0 /f", Category = "Windows", SubCategory = "Privacy", Description = "Disables Cortana search integration system-wide." },
        
        // Windows - Debloat
        new() { Name = "Xbox Gaming Overlay", Type = TaskType.Remove, PackageId = "Microsoft.XboxGamingOverlay", Category = "Windows", SubCategory = "Debloat", Description = "Removes the Xbox Game Bar overlay application." }
    };
}
