using OneLauncher.Core.Helper.Models;

namespace OneLauncher.Core.Global.ModelDataMangers;

internal static class VersionListMigration
{
    internal static async Task InitializeAsync(DBManager configManager,
        VersionListManager versionManager, string configPath)
    {
        var legacy = configManager.Data.LegacyVersionList;
        string? backupPath = null;
        var skipped = 0;
        var initialVersions = new List<UserVersion>();
        var indexExists = File.Exists(versionManager.IndexPath);

        if (legacy != null)
        {
            backupPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(configPath))!,
                $"config.pre-version-list-migration.{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.{Guid.NewGuid():N}.json");
            try { File.Copy(configPath, backupPath, overwrite: false); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new OlanException("版本索引迁移失败",
                    $"无法备份原配置 {configPath} 到 {backupPath}：{ex.Message}",
                    OlanExceptionAction.FatalError, ex);
            }
            if (!indexExists)
            {
                foreach (var version in legacy)
                {
                    // A single directory component cannot escape the current versions directory.
                    if (version == null || !VersionListManager.IsValidVersionId(version.VersionID) ||
                        !File.Exists(Path.Combine(versionManager.GameRootPath, "versions", version.VersionID, "version.json")))
                    {
                        skipped++;
                        continue;
                    }
                    VersionListManager.MergeVersion(initialVersions, version);
                }
            }
        }

        await versionManager.InitializeAsync(new VersionListData { VersionList = initialVersions });
        await versionManager.VerifySavedDataAsync();
        if (legacy == null) return;

        // Remove the legacy field only after the directory-local index is committed and verified.
        configManager.Data.LegacyVersionList = null;
        try { await configManager.Save(); }
        catch
        {
            configManager.Data.LegacyVersionList = legacy;
            throw;
        }

        versionManager.MigrationNotice = !indexExists
            ? $"检测到旧版游戏版本配置，已将 {initialVersions.Count} 个版本迁移到：{versionManager.IndexPath}" +
                (skipped > 0 ? $"\n{skipped} 个无效或不属于当前目录的条目保留在备份中。" : "") +
                $"\n原配置备份：{backupPath}"
            : $"已使用当前安装目录的版本索引：{versionManager.IndexPath}" +
                $"\n旧版全局版本列表未重复导入，已保留到备份：{backupPath}";
    }
}
