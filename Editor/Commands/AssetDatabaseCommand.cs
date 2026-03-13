using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEditor;

namespace AIBridge.Editor
{
    public static class AssetDatabaseCommand
    {
        private static readonly Dictionary<string, string> SearchModeFilters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "all", "" }, { "prefab", "t:Prefab" }, { "scene", "t:Scene" }, { "script", "t:Script" },
            { "texture", "t:Texture" }, { "material", "t:Material" }, { "audio", "t:AudioClip" },
            { "animation", "t:AnimationClip" }, { "shader", "t:Shader" }, { "font", "t:Font" },
            { "model", "t:Model" }, { "so", "t:ScriptableObject" }
        };

        [AIBridge("Find assets by AssetDatabase filter",
            "AIBridgeCLI AssetDatabaseCommand_Find --filter \"t:Prefab\"")]
        public static IEnumerator Find(
            [Description("AssetDatabase filter (e.g. t:Prefab, t:Texture2D)")] string filter = "",
            [Description("Comma-separated list of folders to search in")] string searchInFolders = null,
            [Description("Maximum number of results")] int maxResults = 100)
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

        [AIBridge("Search assets with preset modes (all/prefab/scene/script/texture/material/audio/animation/shader/font/model/so)",
            "AIBridgeCLI AssetDatabaseCommand_Search --mode prefab --keyword \"Player\"")]
        public static IEnumerator Search(
            [Description("Preset mode: all, prefab, scene, script, texture, material, audio, animation, shader, font, model, so")] string mode = "all",
            [Description("Custom filter string (overrides mode)")] string filter = null,
            [Description("Keyword to search within the mode")] string keyword = null,
            [Description("Comma-separated list of folders to search in")] string searchInFolders = null,
            [Description("Maximum number of results")] int maxResults = 100)
        {
            string resolvedFilter;
            if (!string.IsNullOrEmpty(filter))
            {
                resolvedFilter = filter;
            }
            else if (SearchModeFilters.TryGetValue(mode, out var modeFilter))
            {
                resolvedFilter = modeFilter;
            }
            else
            {
                yield return CommandResult.Failure($"Unknown mode: {mode}. Available: {string.Join(", ", SearchModeFilters.Keys)}");
                yield break;
            }

            if (!string.IsNullOrEmpty(keyword))
                resolvedFilter = string.IsNullOrEmpty(resolvedFilter) ? keyword : $"{resolvedFilter} {keyword}";

            string[] guids;
            if (!string.IsNullOrEmpty(searchInFolders))
            {
                var folders = searchInFolders.Split(',');
                for (var i = 0; i < folders.Length; i++) folders[i] = folders[i].Trim();
                guids = AssetDatabase.FindAssets(resolvedFilter, folders);
            }
            else
            {
                guids = AssetDatabase.FindAssets(resolvedFilter);
            }

            var results = new List<SearchAssetInfo>();
            var count = Math.Min(guids.Length, maxResults);
            for (var i = 0; i < count; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var assetType = AssetDatabase.GetMainAssetTypeAtPath(path);
                results.Add(new SearchAssetInfo
                {
                    guid = guids[i],
                    path = path,
                    name = System.IO.Path.GetFileNameWithoutExtension(path),
                    type = assetType?.Name ?? "Unknown"
                });
            }

            yield return CommandResult.Success(new { assets = results, mode, filter = resolvedFilter, totalFound = guids.Length, returned = count });
        }

        [AIBridge("Import a specific asset",
            "AIBridgeCLI AssetDatabaseCommand_Import --assetPath \"Assets/Textures/icon.png\"")]
        public static IEnumerator Import(
            [Description("Asset path to import")] string assetPath = null,
            [Description("Force update even if asset is unchanged")] bool forceUpdate = false)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                yield return CommandResult.Failure("Missing 'assetPath' parameter");
                yield break;
            }
            AssetDatabase.ImportAsset(assetPath, forceUpdate ? ImportAssetOptions.ForceUpdate : ImportAssetOptions.Default);
            yield return CommandResult.Success(new { assetPath, imported = true });
        }

        [AIBridge("Refresh the AssetDatabase",
            "AIBridgeCLI AssetDatabaseCommand_Refresh")]
        public static IEnumerator Refresh(
            [Description("Force update all assets")] bool forceUpdate = false)
        {
            AssetDatabase.Refresh(forceUpdate ? ImportAssetOptions.ForceUpdate : ImportAssetOptions.Default);
            yield return CommandResult.Success(new { refreshed = true });
        }

        [AIBridge("Get asset path from GUID",
            "AIBridgeCLI AssetDatabaseCommand_GetPath --guid \"abc123...\"")]
        public static IEnumerator GetPath(
            [Description("Asset GUID")] string guid = null)
        {
            if (string.IsNullOrEmpty(guid))
            {
                yield return CommandResult.Failure("Missing 'guid' parameter");
                yield break;
            }
            var path = AssetDatabase.GUIDToAssetPath(guid);
            yield return CommandResult.Success(new { guid, path, exists = !string.IsNullOrEmpty(path) });
        }

        [AIBridge("Load and get info about an asset",
            "AIBridgeCLI AssetDatabaseCommand_Load --assetPath \"Assets/Prefabs/Player.prefab\"")]
        public static IEnumerator Load(
            [Description("Asset path to load")] string assetPath = null)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                yield return CommandResult.Failure("Missing 'assetPath' parameter");
                yield break;
            }
            var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (asset == null)
            {
                yield return CommandResult.Failure($"Asset not found at path: {assetPath}");
                yield break;
            }
            yield return CommandResult.Success(new { name = asset.name, path = assetPath, type = asset.GetType().Name, instanceId = asset.GetInstanceID() });
        }

        [Serializable]
        private class AssetInfo
        {
            public string guid;
            public string path;
            public string type;
        }

        [Serializable]
        private class SearchAssetInfo
        {
            public string guid;
            public string path;
            public string name;
            public string type;
        }
    }
}
