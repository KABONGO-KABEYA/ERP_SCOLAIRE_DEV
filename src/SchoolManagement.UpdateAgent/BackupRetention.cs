namespace SchoolManagement.UpdateAgent;

public static class BackupRetention
{
    public static IReadOnlyList<string> PathsToKeep(AgentState state, int completedKeep = 3)
    {
        var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(state.BackupFilePath)
            && state.Phase is not DeployPhases.Completed and not DeployPhases.Idle
            and not DeployPhases.RollbackSucceeded)
        {
            keep.Add(Path.GetFullPath(state.BackupFilePath));
        }

        if (state.Phase == DeployPhases.RollbackRequired && !string.IsNullOrWhiteSpace(state.BackupFilePath))
        {
            keep.Add(Path.GetFullPath(state.BackupFilePath));
        }

        foreach (var path in state.CompletedBackupPaths.AsEnumerable().Reverse().Take(completedKeep))
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                keep.Add(Path.GetFullPath(path));
            }
        }

        return keep.ToList();
    }

    public static void Prune(string backupsRoot, AgentState state)
    {
        if (!Directory.Exists(backupsRoot))
        {
            return;
        }

        var keep = PathsToKeep(state);
        foreach (var file in Directory.GetFiles(backupsRoot, "*.bak"))
        {
            var full = Path.GetFullPath(file);
            if (!keep.Contains(full))
            {
                try
                {
                    File.Delete(full);
                }
                catch
                {
                    // best-effort
                }
            }
        }
    }
}
