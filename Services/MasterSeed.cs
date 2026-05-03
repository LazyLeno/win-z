using System.Collections.Generic;
using WinZ.Models;
using System;

namespace WinZ.Services;

public static class MasterSeed
{
    // INCREMENT THIS NUMBER whenever you change the tasks below to force an update in the app.
    public const int SeedVersion = 25; 

    public static List<SetupTask> GetDefaultTasks() => new()
    {
        // Software - Applications
        new() { Id = "sw_librewolf", Name = "LibreWolf", Type = TaskType.Install, Method = InstallMethod.Winget, PackageId = "LibreWolf.LibreWolf", Category = "Software", Section = "Applications", SubCategory = "Web Browsers", Description = "Privacy-focused Firefox fork with enhanced security defaults.", IsSelected = true },
        new() { Id = "sw_brave", Name = "Brave", Type = TaskType.Install, Method = InstallMethod.Winget, PackageId = "Brave.Brave", Category = "Software", Section = "Applications", SubCategory = "Web Browsers", Description = "Fast browser with built-in ad blocking and privacy features." },
        
        new() { 
            Id = "sw_idm",
            Name = "Internet Download Manager", 
            Type = TaskType.Install, 
            Method = InstallMethod.Winget,
            PackageId = "Tonec.InternetDownloadManager",
            Category = "Software", Section = "Applications", 
            SubCategory = "Download Managers", 
            Description = "Standard version of the high-speed download accelerator.", 
            IsSelected = true,
            ModdedName = "Internet Download Manager",
            ModdedDescription = "High-speed download accelerator with Pro features (Silent Install).",
            ModdedType = TaskType.Tweak,
            ModdedTweakScript = "$url = 'https://raw.githubusercontent.com/LazyLeno/win-z/main/Scoop/IDM-pro/IDM-pro.exe'; $temp = \"$env:TEMP\\IDM-pro.exe\"; Write-Host \"[1/3] Downloading IDM Pro (Silent)...\"; curl.exe -L -s -o $temp $url; if (!(Test-Path $temp)) { Write-Error \"Download failed!\"; return }; Write-Host \"[2/3] Running Silent Setup...\"; $p = Start-Process -FilePath $temp -ArgumentList '/SILENT' -PassThru; $timeout = 60; while ($timeout -gt 0 -and (Get-Process 'IDM-pro' -ErrorAction SilentlyContinue)) { Start-Sleep -s 1; $timeout-- }; Write-Host \"[3/3] Cleanup...\"; Remove-Item $temp -ErrorAction SilentlyContinue; Write-Host \"IDM Install Complete.\"",
            RequiresExplorerRestart = true
        },
        new() { Id = "sw_qbittorrent", Name = "qBittorrent", Type = TaskType.Install, Method = InstallMethod.Winget, PackageId = "qBittorrent.qBittorrent", Category = "Software", Section = "Applications", SubCategory = "Download Managers", Description = "The qBittorrent project aims to provide an open-source software alternative to µTorrent." },
        
        new() { Id = "sw_protonvpn", Name = "Proton VPN", Type = TaskType.Install, Method = InstallMethod.Winget, PackageId = "ProtonVPN.ProtonVPN", Category = "Software", Section = "Applications", SubCategory = "Security", Description = "Secure, no-logs VPN from the makers of ProtonMail.", IsSelected = true },
        new() { Id = "sw_simplewall", Name = "SimpleWall", Type = TaskType.Install, Method = InstallMethod.Winget, PackageId = "Henry++.simplewall", Category = "Software", Section = "Applications", SubCategory = "Security", Description = "Simple tool to configure Windows Filtering Platform (WFP) on your computer." },
        new() { Id = "sw_malwarebytes", Name = "Malwarebytes", Type = TaskType.Install, Method = InstallMethod.Winget, PackageId = "Malwarebytes.Malwarebytes", Category = "Software", Section = "Applications", SubCategory = "Security", Description = "Cybersecurity for home and business." },
        new() { Id = "sw_keepassxc", Name = "KeePassXC", Type = TaskType.Install, Method = InstallMethod.Winget, PackageId = "KeePassXCTeam.KeePassXC", Category = "Software", Section = "Applications", SubCategory = "Security", Description = "Cross-platform community-driven port of KeePass." },
        
        new() { Id = "sw_discord", Name = "Discord", Type = TaskType.Install, Method = InstallMethod.Winget, PackageId = "Discord.Discord", Category = "Software", Section = "Applications", SubCategory = "Communication", Description = "Voice, video and text communication for communities.", IsSelected = true },
        new() { Id = "sw_obsidian", Name = "Obsidian", Type = TaskType.Install, Method = InstallMethod.Winget, PackageId = "Obsidian.Obsidian", Category = "Software", Section = "Applications", SubCategory = "Productivity", Description = "Powerful local Markdown-based knowledge base." },
        
        new() { Id = "sw_pear_desktop", Name = "Pear Desktop", Type = TaskType.Install, Method = InstallMethod.Scoop, PackageId = "extras/pear-desktop", Category = "Software", Section = "Applications", SubCategory = "Music", Description = "Feature-rich YouTube Music desktop client.", IsSelected = true },
        new() { Id = "sw_vlc", Name = "VLC Media Player", Type = TaskType.Install, Method = InstallMethod.Winget, PackageId = "VideoLAN.VLC", Category = "Software", Section = "Applications", SubCategory = "Media", Description = "Free and open source multimedia player." },
        new() { Id = "sw_blender", Name = "Blender", Type = TaskType.Install, Method = InstallMethod.Winget, PackageId = "BlenderFoundation.Blender", Category = "Software", Section = "Applications", SubCategory = "Creativity", Description = "Free and open source 3D creation suite." },
        new() { Id = "sw_obs_studio", Name = "OBS Studio", Type = TaskType.Install, Method = InstallMethod.Winget, PackageId = "OBSProject.OBSStudio", Category = "Software", Section = "Applications", SubCategory = "Creativity", Description = "Professional software for recording and streaming." },
        
        new() { Id = "sw_steam", Name = "Steam", Type = TaskType.Install, Method = InstallMethod.Winget, PackageId = "Valve.Steam", Category = "Software", Section = "Applications", SubCategory = "Gaming", Description = "The ultimate gaming platform." },
        new() { Id = "sw_epic", Name = "Epic Games Launcher", Type = TaskType.Install, Method = InstallMethod.Winget, PackageId = "EpicGames.EpicGamesLauncher", Category = "Software", Section = "Applications", SubCategory = "Gaming", Description = "Epic Games store and launcher." },
        
        new() { Id = "sw_vscode", Name = "Visual Studio Code", Type = TaskType.Install, Method = InstallMethod.Winget, PackageId = "Microsoft.VisualStudioCode", Category = "Software", Section = "Applications", SubCategory = "Development", Description = "Code editing. Redefined." },
        new() { Id = "sw_notepadplus", Name = "Notepad++", Type = TaskType.Install, Method = InstallMethod.Winget, PackageId = "Notepad++.Notepad++", Category = "Software", Section = "Applications", SubCategory = "Development", Description = "Free source code editor." },
        
        new() { Id = "sw_sharex", Name = "ShareX", Type = TaskType.Install, Method = InstallMethod.Winget, PackageId = "ShareX.ShareX", Category = "Software", Section = "Applications", SubCategory = "Utilities", Description = "Screen capture and productivity tool." },
        new() { Id = "sw_7zip", Name = "7-Zip", Type = TaskType.Install, Method = InstallMethod.Winget, PackageId = "7zip.7zip", Category = "Software", Section = "Applications", SubCategory = "Utilities", Description = "A file archiver with a high compression ratio." },
        new() { Id = "sw_winrar", Name = "WinRAR", Type = TaskType.Install, Method = InstallMethod.Winget, PackageId = "RARLab.WinRAR", Category = "Software", Section = "Applications", SubCategory = "Utilities", Description = "Powerful archive manager." },
        new() { Id = "sw_onecommander", Name = "OneCommander", Type = TaskType.Install, Method = InstallMethod.Winget, PackageId = "MilosParipovic.OneCommander", Category = "Software", Section = "Applications", SubCategory = "File Managers", Description = "Modern dual-pane file manager." },
        new() { Id = "sw_directory_opus", Name = "Directory Opus", Type = TaskType.Install, Method = InstallMethod.Winget, PackageId = "GPSoftware.DirectoryOpus", Category = "Software", Section = "Applications", SubCategory = "File Managers", Description = "Professional file manager.", IsSelected = true },

        // Software - Keygens (Placeholder)
        new() { Id = "sw_keygen_mock", Name = "Keygen Placeholder", Type = TaskType.Tweak, TweakScript = "Write-Host 'Placeholder for future tools.'", Category = "Software", Section = "Keygens", SubCategory = "Tools", Description = "This is a law-compliant placeholder for future specialized tools." },

        // Windows - Configuration
        new() { Id = "tweak_verbose_login", Name = "Verbose Login Messages", Type = TaskType.Tweak, TweakScript = "reg add HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\System /v verbosestatus /t REG_DWORD /d 1 /f", Category = "Windows", Section = "Configuration", SubCategory = "System Tweaks", Description = "Show detailed status messages during login and shutdown." },
        new() { 
            Id = "hw_timer_res", 
            Name = "Optimize Timer Resolution", 
            Type = TaskType.Tweak, 
            TweakScript = "try { bcdedit /set useplatformtick yes; bcdedit /set disabledynamictick yes; Write-Host 'BCD timer optimized.' } catch { Write-Error 'BCD access denied.' }", 
            Category = "Windows", Section = "Performance", SubCategory = "Precision", 
            Description = "Configures BCD to use a consistent platform tick.",
            IsSelected = true
        },
        new() { 
            Id = "hw_disable_fastboot", 
            Name = "Disable Fast Boot", 
            Type = TaskType.Tweak, 
            TweakScript = "try { reg add 'HKLM\\SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Power' /v HiberbootEnabled /t REG_DWORD /d 0 /f; Write-Host 'Fast Boot disabled.' } catch { Write-Error 'Registry access denied.' }", 
            Category = "Windows", Section = "Performance", SubCategory = "Boot", 
            Description = "Disables Fast Startup for a clean kernel state.",
            IsSelected = true
        },
        new() { Id = "tweak_disable_cortana", Name = "Disable Cortana", Type = TaskType.Tweak, TweakScript = "reg add HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Search /v AllowCortana /t REG_DWORD /d 0 /f", Category = "Windows", Section = "Privacy", SubCategory = "Security", Description = "Disables Cortana search integration system-wide." },
        new() { Id = "rem_xbox_overlay", Name = "Xbox Gaming Overlay", Type = TaskType.Remove, PackageId = "Microsoft.XboxGamingOverlay", Category = "Windows", Section = "Debloat", SubCategory = "Gaming", Description = "Removes the Xbox Game Bar overlay." },

        // Hardware - Optimizations
        new() { 
            Id = "hw_disable_parking", 
            Name = "Disable CPU Core Parking", 
            Type = TaskType.Tweak, 
            TweakScript = "try { powercfg -attributes SUB_PROCESSOR 0cc5b647-c1df-4637-891a-dec35c318583 3b04d4fd-1d19-4174-a4d8-3e672b9f5835 -ATTRIB_HIDE; powercfg -setacvalueindex scheme_current sub_processor 3b04d4fd-1d19-4174-a4d8-3e672b9f5835 0; powercfg -setactive scheme_current; Write-Host 'CPU Parking disabled.' } catch { Write-Error 'PowerCfg failure.' }", 
            Category = "Hardware", Section = "Optimizations", SubCategory = "CPU", 
            Description = "Prevents CPU cores from sleeping during performance tasks.",
            IsSelected = true
        },
        new() { 
            Id = "hw_enable_hags", 
            Name = "Enable GPU Scheduling (HAGS)", 
            Type = TaskType.Tweak, 
            TweakScript = "try { reg add 'HKLM\\SYSTEM\\CurrentControlSet\\Control\\GraphicsDrivers' /v HwSchMode /t REG_DWORD /d 2 /f; Write-Host 'HAGS enabled.' } catch { Write-Error 'Registry access denied.' }", 
            Category = "Hardware", Section = "Optimizations", SubCategory = "GPU", 
            Description = "Enables Hardware-Accelerated GPU Scheduling.",
            IsSelected = true
        },
        new() { 
            Id = "hw_disable_mem_comp", 
            Name = "Disable Memory Compression", 
            Type = TaskType.Tweak, 
            TweakScript = "try { Disable-mmagent -mc -ErrorAction Stop; Write-Host 'Memory compression disabled.' } catch { Write-Error 'MMAgent failed.' }", 
            Category = "Hardware", Section = "Optimizations", SubCategory = "Memory", 
            Description = "Disables Windows memory compression agent.",
            IsSelected = true
        },
        new() { 
            Id = "hw_check_xmp", 
            Name = "Verify XMP/EXPO Status", 
            Type = TaskType.Tweak, 
            TweakScript = "try { $speed = (Get-WmiObject Win32_PhysicalMemory | Measure-Object -Property Speed -Maximum).Maximum; Write-Host \"Current Memory Speed: $speed MHz\"; if ($speed -lt 3000) { Write-Warning 'XMP/EXPO might be disabled!' } } catch { Write-Error 'WMI failure.' }", 
            Category = "Hardware", Section = "Optimizations", SubCategory = "Memory", 
            Description = "Checks RAM frequency for XMP/EXPO status.",
            IsSelected = true
        }
    };
}
