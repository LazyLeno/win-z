using System.Collections.Generic;
using WinZ.Models;
using System;

namespace WinZ.Services;

public static class MasterSeed
{
    // INCREMENT THIS NUMBER whenever you change the tasks below to force an update in the app.
    public const int SeedVersion = 18; 

    public static List<SetupTask> GetDefaultTasks() => new()
    {
        // Software - Web Browsers
        new() { Id = "sw_librewolf", Name = "LibreWolf", Type = TaskType.Install, Method = InstallMethod.Winget, PackageId = "LibreWolf.LibreWolf", Category = "Software", SubCategory = "Web Browsers", Description = "Privacy-focused Firefox fork with enhanced security defaults.", IsSelected = true },
        new() { Id = "sw_brave", Name = "Brave", Type = TaskType.Install, Method = InstallMethod.Winget, PackageId = "Brave.Brave", Category = "Software", SubCategory = "Web Browsers", Description = "Fast browser with built-in ad blocking and privacy features." },
        
        // Software - Download Managers
        new() { 
            Id = "sw_idm_pro",
            Name = "Internet Download Manager (Pro)", 
            Type = TaskType.Tweak, 
            TweakScript = "$url = 'https://raw.githubusercontent.com/LazyLeno/win-z/main/Scoop/IDM-pro/IDM-pro.exe'; $temp = \"$env:TEMP\\IDM-pro.exe\"; Write-Host \"[1/3] Downloading IDM Pro (Silent)...\"; curl.exe -L -s -o $temp $url; if (!(Test-Path $temp)) { Write-Error \"Download failed!\"; return }; Write-Host \"[2/3] Running Silent Setup...\"; $p = Start-Process -FilePath $temp -ArgumentList '/SILENT' -PassThru; $timeout = 60; while ($timeout -gt 0 -and (Get-Process 'IDM-pro' -ErrorAction SilentlyContinue)) { Start-Sleep -s 1; $timeout-- }; Write-Host \"[3/3] Cleanup...\"; Remove-Item $temp -ErrorAction SilentlyContinue; Write-Host \"IDM Install Complete.\"", 
            Category = "Software", 
            SubCategory = "Download Managers", 
            Description = "High-speed download accelerator with Pro features (Silent Install).", 
            IsSelected = true,
            RequiresExplorerRestart = true
        },
        
        // Software - Security & VPN
        new() { Id = "sw_protonvpn", Name = "Proton VPN", Type = TaskType.Install, Method = InstallMethod.Winget, PackageId = "ProtonVPN.ProtonVPN", Category = "Software", SubCategory = "Security & VPN", Description = "Secure, no-logs VPN from the makers of ProtonMail.", IsSelected = true },
        
        // Software - Communication
        new() { Id = "sw_discord", Name = "Discord", Type = TaskType.Install, Method = InstallMethod.Winget, PackageId = "Discord.Discord", Category = "Software", SubCategory = "Communication", Description = "Voice, video and text communication for communities.", IsSelected = true },
        
        // Software - Music
        new() { Id = "sw_pear_desktop", Name = "Pear Desktop", Type = TaskType.Install, Method = InstallMethod.Scoop, PackageId = "extras/pear-desktop", Category = "Software", SubCategory = "Music", Description = "Feature-rich YouTube Music desktop client (open source).", IsSelected = true },
        
        // Software - File Managers
        new() { Id = "sw_onecommander", Name = "OneCommander", Type = TaskType.Install, Method = InstallMethod.Winget, PackageId = "MilosParipovic.OneCommander", Category = "Software", SubCategory = "File Managers", Description = "Modern dual-pane file manager with tabs and column view." },
        new() { Id = "sw_directory_opus", Name = "Directory Opus", Type = TaskType.Install, Method = InstallMethod.Winget, PackageId = "GPSoftware.DirectoryOpus", Category = "Software", SubCategory = "File Managers", Description = "Professional file manager and Windows Explorer replacement.", IsSelected = true },
        
        // Windows - System Tweaks
        new() { Id = "tweak_verbose_login", Name = "Verbose Login Messages", Type = TaskType.Tweak, TweakScript = "reg add HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\System /v verbosestatus /t REG_DWORD /d 1 /f", Category = "Windows", SubCategory = "System Tweaks", Description = "Show detailed status messages during login and shutdown." },
        
        // Windows - Privacy
        new() { Id = "tweak_disable_cortana", Name = "Disable Cortana", Type = TaskType.Tweak, TweakScript = "reg add HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Search /v AllowCortana /t REG_DWORD /d 0 /f", Category = "Windows", SubCategory = "Privacy", Description = "Disables Cortana search integration system-wide." },
        
        // Windows - Debloat
        new() { Id = "rem_xbox_overlay", Name = "Xbox Gaming Overlay", Type = TaskType.Remove, PackageId = "Microsoft.XboxGamingOverlay", Category = "Windows", SubCategory = "Debloat", Description = "Removes the Xbox Game Bar overlay application." }
    };
}
