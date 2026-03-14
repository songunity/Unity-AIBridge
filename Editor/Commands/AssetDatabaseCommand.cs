using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEditor;

namespace AIBridge.Editor
{
    public static class AssetDatabaseCommand
    {
        [AIBridge("通过 AssetDatabase 过滤器查找资源",
            "AIBridgeCLI AssetDatabaseCommand_Find --filter \"t:Prefab\"")]
        public static IEnumerator Find(
            [Description("AssetDatabase 过滤器（例如 t:Prefab, t:Texture2D）")] string filter = "",
            [Description("要搜索的文件夹列表，用逗号分隔")] string searchInFolders = null,
            [Description("最大结果数量")] int maxResults = 100)
        {
            string[] guids;
            if (!string.IsNullOrEmpty(searchInFolders))
                guids = AssetDatabase.FindAssets(filter, searchInFolders.Split(','));
            else
                guids = AssetDatabase.FindAssets(filter);

            var results = new List<AssetInfo>();
            var count = Math.Min(guids.Length, maxResults);
            for (var i = 0; i < count; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var assetType = AssetDatabase.GetMainAssetTypeAtPath(path);
                results.Add(new AssetInfo { guid = guids[i], path = path, type = assetType?.Name ?? "Unknown" });
            }

            yield return CommandResult.Success(new { assets = results, totalFound = guids.Length, returned = count });
        }

        [AIBridge("刷新资源数据库",
            "AIBridgeCLI AssetDatabaseCommand_Refresh")]
        public static IEnumerator Refresh(
            [Description("强制更新所有资源")] bool forceUpdate = false)
        {
            AssetDatabase.Refresh(forceUpdate ? ImportAssetOptions.ForceUpdate : ImportAssetOptions.Default);
            yield return CommandResult.Success(new { refreshed = true });
        }

        [Serializable]
        private class AssetInfo
        {
            public string guid;
            public string path;
            public string type;
        }
    }
}
