using System.Collections.Generic;

namespace WinZ.Models;

public record ExpressConfig(
    List<SetupTask> InstallTasks,
    List<SetupTask> TweakTasks,
    List<SetupTask> RemoveTasks
);
