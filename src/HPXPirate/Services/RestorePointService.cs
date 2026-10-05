using System.Management;

namespace HPXPirate.Services;

/// <summary>
/// Creates Windows System Restore points before applying system changes.
/// Falls back to an HPX-internal JSON snapshot when System Restore is disabled.
/// </summary>
public sealed class RestorePointService
{
    private readonly ILogger _log;
    public RestorePointService(ILogger log) => _log = log;

    public bool TryCreateRestorePoint(string description, out string message)
    {
        try
        {
            var scope = new ManagementScope(@"\\.\root\default");
            scope.Connect();
            using var cls = new ManagementClass(scope, new ManagementPath("SystemRestore"), null);
            var inParams = cls.GetMethodParameters("CreateRestorePoint");
            inParams["Description"] = description;
            inParams["RestorePointType"] = 12; // MODIFY_SETTINGS
            inParams["EventType"] = 100;       // BEGIN_SYSTEM_CHANGE
            cls.InvokeMethod("CreateRestorePoint", inParams, null);

            message = "System Restore point created.";
            _log.Info("Restore point created: " + description);
            return true;
        }
        catch (Exception ex)
        {
            message = "System Restore point could not be created (System Restore may be disabled). " +
                      "An HPX internal backup will be used instead.";
            _log.Warn("Restore point failed: " + ex.Message);
            return false;
        }
    }

    /// <summary>Writes a JSON snapshot of the current change set for rollback.</summary>
    public string WriteInternalSnapshot(string json)
    {
        AppPaths.EnsureCreated();
        var file = Path.Combine(AppPaths.RestorePoints, $"snapshot_{DateTime.Now:yyyyMMdd_HHmmss}.json");
        File.WriteAllText(file, json);
        _log.Info("Internal restore snapshot written: " + file);
        return file;
    }

    public bool SystemRestoreEnabled()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\default", "SELECT * FROM SystemRestore");
            foreach (ManagementObject _ in searcher.Get()) return true;
            return false;
        }
        catch { return false; }
    }
}
