using System.Collections.Generic;
using WinZ.Models;

namespace WinZ.Services;

public static class MasterSeed
{
    // INCREMENT THIS NUMBER whenever you change the tasks below to force an update in the app.
    public const int SeedVersion = 7; 

    public static List<SetupTask> GetDefaultTasks() => new()
    {
        // Software - Web Browsers
        new() { Name = "LibreWolf", Type = TaskType.Install, Method = InstallMethod.Winget, PackageId = "LibreWolf.LibreWolf", Category = "Software", SubCategory = "Web Browsers", IconUrl = "https://external-content.duckduckgo.com/iu/?u=https%3A%2F%2Fstyles.redditmedia.com%2Ft5_3qrw5l%2Fstyles%2FcommunityIcon_ym4z85jq62c61.png&f=1&nofb=1&ipt=b914ffe45fe595565f4b00623b07ab7122a48f48dbd5a2fbdcb634f1feea00c5", Description = "Privacy-focused Firefox fork with enhanced security defaults.", IsSelected = true },
        new() { Name = "Brave", Type = TaskType.Install, Method = InstallMethod.Winget, PackageId = "Brave.Brave", Category = "Software", SubCategory = "Web Browsers", IconUrl = "https://external-content.duckduckgo.com/iu/?u=https%3A%2F%2Fcdn.icon-icons.com%2Ficons2%2F2552%2FPNG%2F512%2Fbrave_browser_logo_icon_153013.png&f=1&nofb=1&ipt=0d4f4f8bd48dfb1cc79698e53a3e3302f56e984a6c2a643765b66c6bd3d78b14", Description = "Fast browser with built-in ad blocking and privacy features." },
        
        // Software - Download Managers
        new() { Name = "Internet Download Manager", Type = TaskType.Install, Method = InstallMethod.Winget, PackageId = "Tonec.InternetDownloadManager", Category = "Software", SubCategory = "Download Managers", IconUrl = "https://cdn.discordapp.com/attachments/1282767944266547284/1498726556309127178/internet_download_manager-2058509539.png?ex=69f23560&is=69f0e3e0&hm=00ce1c8e7ff5e9df003b0e78c5e12b1accfa4394d9147b7d20e01cf107e04beb&", Description = "High-speed download accelerator with browser integration.", IsSelected = true },
        
        // Software - Security & VPN
        new() { Name = "Proton VPN", Type = TaskType.Install, Method = InstallMethod.Winget, PackageId = "ProtonVPN.ProtonVPN", Category = "Software", SubCategory = "Security & VPN", IconUrl = "https://external-content.duckduckgo.com/iu/?u=https%3A%2F%2Fc.clc2l.com%2Ft%2Fp%2Fr%2Fprotonvpn--9KoBD.png&f=1&nofb=1&ipt=ccc4c9d46a98c224e4f5ffe98c0cac835e1b1d0c32b9f9ba8d71b3ffb9ca04f6", Description = "Secure, no-logs VPN from the makers of ProtonMail.", IsSelected = true },
        
        // Software - Communication
        new() { Name = "Discord", Type = TaskType.Install, Method = InstallMethod.Winget, PackageId = "Discord.Discord", Category = "Software", SubCategory = "Communication", IconUrl = "https://external-content.duckduckgo.com/iu/?u=https%3A%2F%2Ffreelogopng.com%2Fimages%2Fall_img%2F1691730767discord-logo-transparent.png&f=1&nofb=1&ipt=5f6962f72c32e5a3bfe2f083b9e8cafe88ec8545b480a02fe3fd79bbd12bebf8", Description = "Voice, video and text communication for communities.", IsSelected = true },
        
        // Software - Music
        new() { Name = "Pear Desktop", Type = TaskType.Install, Method = InstallMethod.DirectDownload, PackageId = "th-ch/youtube-music", FallbackUrl = "https://github.com/th-ch/youtube-music/releases/latest/download/YouTube-Music-Setup.exe", Category = "Software", SubCategory = "Music", IconUrl = "https://external-content.duckduckgo.com/iu/?u=https%3A%2F%2Fcdn.afterdawn.fi%2Fv3%2Fnews%2Foriginal%2FYoutube_Music_logo.png&f=1&nofb=1&ipt=0c204a8470c2f48a0c6a0c8fe97692b1194ff0ccf584a69ed8ee98fb44ed3cba", Description = "Feature-rich YouTube Music desktop client (open source).", IsSelected = true },
        
        // Software - File Managers
        new() { Name = "OneCommander", Type = TaskType.Install, Method = InstallMethod.Winget, PackageId = "MilosParipovic.OneCommander", Category = "Software", SubCategory = "File Managers", IconUrl = "https://external-content.duckduckgo.com/iu/?u=https%3A%2F%2Fi-cdn.apsgo.com%2Fcdn%2FaAqrzAuZr0yWyhieHWeXgDwUjutksQTxuB42qBDG.png&f=1&nofb=1&ipt=71aa3bad7a34f1f97038d518cd971e1af06c11e6d914b731f5b29ba0a1f72f5a", Description = "Modern dual-pane file manager with tabs and column view." },
        new() { Name = "Directory Opus", Type = TaskType.Install, Method = InstallMethod.Winget, PackageId = "GPSoftware.DirectoryOpus", Category = "Software", SubCategory = "File Managers", IconUrl = "https://external-content.duckduckgo.com/iu/?u=https%3A%2F%2Fwww.directory-opus.de%2Fassets%2Fimg%2Flogo.png&f=1&nofb=1&ipt=8207b890c1035fbcd47a0673937b3af42fcfff672087e9fa328124744ff9997a", Description = "Professional file manager and Windows Explorer replacement.", IsSelected = true },
        
        // Windows - System Tweaks
        new() { Name = "Verbose Login Messages", Type = TaskType.Tweak, TweakScript = "reg add HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\System /v verbosestatus /t REG_DWORD /d 1 /f", Category = "Windows", SubCategory = "System Tweaks", Icon = "", Description = "Show detailed status messages during login and shutdown." },
        
        // Windows - Privacy
        new() { Name = "Disable Cortana", Type = TaskType.Tweak, TweakScript = "reg add HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Search /v AllowCortana /t REG_DWORD /d 0 /f", Category = "Windows", SubCategory = "Privacy", Icon = "", Description = "Disables Cortana search integration system-wide." },
        
        // Windows - Debloat
        new() { Name = "Xbox Gaming Overlay", Type = TaskType.Remove, PackageId = "Microsoft.XboxGamingOverlay", Category = "Windows", SubCategory = "Debloat", Icon = "", Description = "Removes the Xbox Game Bar overlay application." }
    };
}
