using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AIBridge.Editor
{
    /// <summary>
    /// Asset database operations: find, import, refresh, search
    /// Supports multiple sub-commands via "action" parameter
    /// </summary>
    public class AssetDatabaseCommand : ICommand
    {
        public string Type => "asset";
        public bool RequiresRefresh => true;

        /// <summary>
        /// Predefined search mode filters
        /// </summary>
        private static readonly Dictionary<string, string> SearchModeFilters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "all", "" },
            { "prefab", "t:Prefab" },
            { "scene", "t:Scene" },
            { "script", "t:Script" },
            { "texture", "t:Texture" },
            { "material", "t:Material" },
            { "audio", "t:AudioClip" },
            { "animation", "t:AnimationClip" },
            { "shader", "t:Shader" },
            { "font", "t:Font" },
            { "model", "t:Model" },
            { "so", "t:ScriptableObject" }
        };

        public CommandResult Execute(CommandRequest request)
        {
            var action = request.GetParam("action", "find");

            try
            {
                switch (action.ToLower())
                {
                    case "find":
                        return FindAssets(request);
                    case "search":
                        return SearchAssets(request);
                    case "import":
                        return ImportAsset(request);
                    case "refresh":
                        return RefreshAssets(request);
                    case "get_path":
                        return GetAssetPath(request);
                    case "load":
                        return LoadAsset(request);
                    default:
                        return CommandResult.Failure(request.id, $"Unknown action: {action}. Supported: find, search, import, refresh, get_path, load");
                }
            }
            catch (Exception ex)
            {
                return CommandResult.FromException(request.id, ex);
            }
        }

        private CommandResult FindAssets(CommandRequest request)
        {
            var filter = request.GetParam("filter", "");
            var searchInFolders = request.GetParam<string>("searchInFolders", null);
            var maxResults = request.GetParam("maxResults", 100);

            string[] guids;
            if (!string.IsNullOrEmpty(searchInFolders))
            {
                var folders = searchInFolders.Split(',');
                guids = AssetDatabase.FindAssets(filter, folders);
            }
            else
            {
                guids = AssetDatabase.FindAssets(filter);
            }

            var results = new List<AssetInfo>();
            var count = Math.Min(guids.Length, maxResults);

            for (var i = 0; i < count; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var assetType = AssetDatabase.GetMainAssetTypeAtPath(path);

                results.Add(new AssetInfo
                {
                    guid = guids[i],
                    path = path,
                    type = assetType?.Name ?? "Unknown"
                });
            }

            return CommandResult.Success(request.id, new
            {
                assets = results,
                totalFound = guids.Length,
                returned = count
            });
        }

        /// <summary>
        /// Search assets with predefined modes (simplified wrapper for FindAssets)
        /// </summary>
        private CommandResult SearchAssets(CommandRequest request)
        {
            var mode = request.GetParam("mode", "all");
            var customFilter = request.GetParam<string>("filter", null);
            var keyword = request.GetParam<string>("keyword", null);
            var searchInFolders = request.GetParam<string>("searchInFolders", null);
            var maxResults = request.GetParam("maxResults", 100);

            // Determine the filter to use
            string filter;
            if (!string.IsNullOrEmpty(customFilter))
            {
                // Custom filter overrides mode
                filter = customFilter;
            }
            else if (SearchModeFilters.TryGetValue(mode, out var modeFilter))
            {
                filter = modeFilter;
            }
            else
            {
                // Return available modes if invalid mode specified
                var availableModes = string.Join(", ", SearchModeFilters.Keys);
                return CommandResult.Failure(request.id, $"Unknown mode: {mode}. Available modes: {availableModes}");
            }

            // Append keyword to filter if provided
            if (!string.IsNullOrEmpty(keyword))
            {
                filter = string.IsNullOrEmpty(filter) ? keyword : $"{filter} {keyword}";
            }

            // Execute search
            string[] guids;
            if (!string.IsNullOrEmpty(searchInFolders))
            {
                var folders = searchInFolders.Split(',');
                for (var i = 0; i < folders.Length; i++)
                {
                    folders[i] = folders[i].Trim();
                }
                guids = AssetDatabase.FindAssets(filter, folders);
            }
            else
            {
                guids = AssetDatabase.FindAssets(filter);
            }

            var results = new List<SearchAssetInfo>();
            var count = Math.Min(guids.Length, maxResults);

            for (var i = 0; i < count; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var assetType = AssetDatabase.GetMainAssetTypeAtPath(path);
                var assetName = System.IO.Path.GetFileNameWithoutExtension(path);

                results.Add(new SearchAssetInfo
                {
                    guid = guids[i],
                    path = path,
                    name = assetName,
                    type = assetType?.Name ?? "Unknown"
                });
            }

            return CommandResult.Success(request.id, new
            {
                assets = results,
                mode = mode,
                filter = filter,
                totalFound = guids.Length,
                returned = count
            });
        }

        private CommandResult ImportAsset(CommandRequest request)
        {
            var assetPath = request.GetParam<string>("assetPath");
            if (string.IsNullOrEmpty(assetPath))
            {
                return CommandResult.Failure(request.id, "Missing 'assetPath' parameter");
            }

            var options = request.GetParam("forceUpdate", false)
                ? ImportAssetOptions.ForceUpdate
                : ImportAssetOptions.Default;

            AssetDatabase.ImportAsset(assetPath, options);

            return CommandResult.Success(request.id, new
            {
                assetPath = assetPath,
                imported = true
            });
        }

        private CommandResult RefreshAssets(CommandRequest request)
        {
            var options = request.GetParam("forceUpdate", false)
                ? ImportAssetOptions.ForceUpdate
                : ImportAssetOptions.Default;

            AssetDatabase.Refresh(options);

            return CommandResult.Success(request.id, new
            {
                refreshed = true
            });
        }

        private CommandResult GetAssetPath(CommandRequest request)
        {
            var guid = request.GetParam<string>("guid");
            if (string.IsNullOrEmpty(guid))
            {
                return CommandResult.Failure(request.id, "Missing 'guid' parameter");
            }

            var path = AssetDatabase.GUIDToAssetPath(guid);

            return CommandResult.Success(request.id, new
            {
                guid = guid,
                path = path,
                exists = !string.IsNullOrEmpty(path)
            });
        }

        private CommandResult LoadAsset(CommandRequest request)
        {
            var assetPath = request.GetParam<string>("assetPath");
            if (string.IsNullOrEmpty(assetPath))
            {
                return CommandResult.Failure(request.id, "Missing 'assetPath' parameter");
            }

            var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (asset == null)
            {
                return CommandResult.Failure(request.id, $"Asset not found at path: {assetPath}");
            }

            return CommandResult.Success(request.id, new
            {
                name = asset.name,
                path = assetPath,
                type = asset.GetType().Name,
                instanceId = asset.GetInstanceID()
            });
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
