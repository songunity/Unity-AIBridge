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
        [AIBridge("Get the current selection",
            "AIBridgeCLI SelectionCommand_Get")]
        public static IEnumerator Get(
            [Description("Include component list for each selected GameObject")] bool includeComponents = false)
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

        [AIBridge("Set the current selection",
            "AIBridgeCLI SelectionCommand_Set --path \"Player\"")]
        public static IEnumerator Set(
            [Description("Hierarchy path of the GameObject to select")] string path = null,
            [Description("Asset path to select")] string assetPath = null,
            [Description("Instance ID to select")] int instanceId = 0,
            [Description("Comma-separated list of instance IDs to select")] string instanceIds = null)
        {
            UnityEngine.Object selectedObject = null;
            var selectedObjects = new List<UnityEngine.Object>();

            if (instanceId != 0)
            {
                selectedObject = EditorUtility.InstanceIDToObject(instanceId);
                if (selectedObject != null) selectedObjects.Add(selectedObject);
            }
            else if (!string.IsNullOrEmpty(instanceIds))
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
            else if (!string.IsNullOrEmpty(path))
            {
                var go = GameObject.Find(path);
                if (go != null) { selectedObject = go; selectedObjects.Add(go); }
            }
            else if (!string.IsNullOrEmpty(assetPath))
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

        [AIBridge("Clear the current selection",
            "AIBridgeCLI SelectionCommand_Clear")]
        public static IEnumerator Clear()
        {
            Selection.objects = new UnityEngine.Object[0];
            Selection.activeObject = null;
            yield return CommandResult.Success(new { action = "clear", cleared = true });
        }

        [AIBridge("Add an object to the current selection",
            "AIBridgeCLI SelectionCommand_Add --path \"Enemy1\"")]
        public static IEnumerator Add(
            [Description("Hierarchy path of the GameObject")] string path = null,
            [Description("Asset path")] string assetPath = null,
            [Description("Instance ID")] int instanceId = 0)
        {
            UnityEngine.Object objectToAdd = null;
            if (instanceId != 0)
                objectToAdd = EditorUtility.InstanceIDToObject(instanceId);
            else if (!string.IsNullOrEmpty(path))
                objectToAdd = GameObject.Find(path);
            else if (!string.IsNullOrEmpty(assetPath))
                objectToAdd = AssetDatabase.LoadMainAssetAtPath(assetPath);

            if (objectToAdd == null)
            {
                yield return CommandResult.Failure("Object not found");
                yield break;
            }

            var current = new List<UnityEngine.Object>(Selection.objects);
            if (!current.Contains(objectToAdd))
            {
                current.Add(objectToAdd);
                Selection.objects = current.ToArray();
            }

            yield return CommandResult.Success(new
            {
                action = "add",
                addedObject = objectToAdd.name,
                newCount = Selection.objects.Length
            });
        }

        [AIBridge("Remove an object from the current selection",
            "AIBridgeCLI SelectionCommand_Remove --path \"Enemy1\"")]
        public static IEnumerator Remove(
            [Description("Hierarchy path of the GameObject")] string path = null,
            [Description("Asset path")] string assetPath = null,
            [Description("Instance ID")] int instanceId = 0)
        {
            UnityEngine.Object objectToRemove = null;
            if (instanceId != 0)
                objectToRemove = EditorUtility.InstanceIDToObject(instanceId);
            else if (!string.IsNullOrEmpty(path))
                objectToRemove = GameObject.Find(path);
            else if (!string.IsNullOrEmpty(assetPath))
                objectToRemove = AssetDatabase.LoadMainAssetAtPath(assetPath);

            if (objectToRemove == null)
            {
                yield return CommandResult.Failure("Object not found");
                yield break;
            }

            var current = new List<UnityEngine.Object>(Selection.objects);
            current.Remove(objectToRemove);
            Selection.objects = current.ToArray();

            yield return CommandResult.Success(new
            {
                action = "remove",
                removedObject = objectToRemove.name,
                newCount = Selection.objects.Length
            });
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
