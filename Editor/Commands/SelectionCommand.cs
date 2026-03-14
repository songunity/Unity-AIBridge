using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEditor;
using UnityEngine;
using Component = UnityEngine.Component;

namespace AIBridge.Editor
{
    public static class SelectionCommand
    {
        [AIBridge("获取当前选择的GameObject",
            "AIBridgeCLI SelectionCommand_Get")]
        public static IEnumerator Get(
            [Description("是否返回 GameObject 包含组件列表")] bool includeComponents = false)
        {
            var gameObjects = new List<GameObjectInfo>();
            var assets = new List<AssetInfo>();

            foreach (var go in Selection.gameObjects)
            {
                var info = new GameObjectInfo
                {
                    name = go.name,
                    path = GameObjectHelper.GetGameObjectPath(go),
                    tag = go.tag,
                    layer = LayerMask.LayerToName(go.layer),
                    activeSelf = go.activeSelf,
                    activeInHierarchy = go.activeInHierarchy,
                    instanceId = go.GetInstanceID()
                };
                if (includeComponents)
                {
                    info.components = new List<string>();
                    foreach (var component in go.GetComponents<Component>())
                    {
                        if (component != null)
                            info.components.Add(component.GetType().Name);
                    }
                }
                gameObjects.Add(info);
            }

            foreach (var obj in Selection.objects)
            {
                if (obj is GameObject) continue;
                var assetPath = AssetDatabase.GetAssetPath(obj);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    assets.Add(new AssetInfo
                    {
                        name = obj.name,
                        path = assetPath,
                        type = obj.GetType().Name,
                        instanceId = obj.GetInstanceID()
                    });
                }
            }

            yield return CommandResult.Success(new
            {
                gameObjects,
                assets,
                activeObject = Selection.activeObject?.name,
                activeObjectInstanceId = Selection.activeObject?.GetInstanceID(),
                count = gameObjects.Count + assets.Count
            });
        }

        [AIBridge("设置当前选择的Object，可以同时传递多个参数",
            "AIBridgeCLI SelectionCommand_Set --path \"Player\"")]
        public static IEnumerator Set(
            [Description("要选择的 GameObject 的层级路径")] string path = null,
            [Description("要选择的资源路径")] string assetPath = null,
            [Description("要选择的实例 ID")] int instanceId = 0,
            [Description("要选择的实例 ID 列表，用逗号分隔")] string instanceIds = null)
        {
            UnityEngine.Object selectedObject = null;
            var selectedObjects = new List<UnityEngine.Object>();

            if (instanceId != 0)
            {
                selectedObject = EditorUtility.InstanceIDToObject(instanceId);
                if (selectedObject != null) selectedObjects.Add(selectedObject);
            }
            if (!string.IsNullOrEmpty(instanceIds))
            {
                var ids = instanceIds.Split(',');
                foreach (var idStr in ids)
                {
                    if (int.TryParse(idStr.Trim(), out var id))
                    {
                        var obj = EditorUtility.InstanceIDToObject(id);
                        if (obj != null) selectedObjects.Add(obj);
                    }
                }
            }
            if (!string.IsNullOrEmpty(path))
            {
                var go = GameObject.Find(path);
                if (go != null) { selectedObject = go; selectedObjects.Add(go); }
            }
            if (!string.IsNullOrEmpty(assetPath))
            {
                selectedObject = AssetDatabase.LoadMainAssetAtPath(assetPath);
                if (selectedObject != null) selectedObjects.Add(selectedObject);
            }

            if (selectedObjects.Count == 0)
            {
                yield return CommandResult.Failure("No objects found. Provide 'path', 'assetPath', 'instanceId', or 'instanceIds'");
                yield break;
            }

            Selection.objects = selectedObjects.ToArray();
            Selection.activeObject = selectedObject ?? selectedObjects[0];

            yield return CommandResult.Success(new
            {
                action = "set",
                selectedCount = selectedObjects.Count,
                activeObject = Selection.activeObject?.name
            });
        }

        [AIBridge("清除当前选择",
            "AIBridgeCLI SelectionCommand_Clear")]
        public static IEnumerator Clear()
        {
            Selection.objects = new UnityEngine.Object[0];
            Selection.activeObject = null;
            yield return CommandResult.Success(new { action = "clear", cleared = true });
        }

        [Serializable]
        private class GameObjectInfo
        {
            public string name;
            public string path;
            public string tag;
            public string layer;
            public bool activeSelf;
            public bool activeInHierarchy;
            public int instanceId;
            public List<string> components;
        }

        [Serializable]
        private class AssetInfo
        {
            public string name;
            public string path;
            public string type;
            public int instanceId;
        }
    }
}
