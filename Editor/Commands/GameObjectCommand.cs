using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEditor;
using UnityEngine;
using Component = UnityEngine.Component;

namespace AIBridge.Editor
{
    public static class GameObjectCommand
    {
        [AIBridge("Create a new GameObject in the scene",
            "AIBridgeCLI GameObjectCommand_Create --name \"MyCube\" --primitiveType Cube")]
        public static IEnumerator Create(
            [Description("Name for the new GameObject")] string name = "New GameObject",
            [Description("Primitive type: Cube, Sphere, Capsule, Cylinder, Plane, Quad")] string primitiveType = null,
            [Description("Hierarchy path of parent GameObject")] string parentPath = null)
        {
            GameObject go;
            if (!string.IsNullOrEmpty(primitiveType))
            {
                if (!Enum.TryParse<PrimitiveType>(primitiveType, true, out var primitive))
                {
                    yield return CommandResult.Failure($"Unknown primitive type: {primitiveType}. Supported: Cube, Sphere, Capsule, Cylinder, Plane, Quad");
                    yield break;
                }
                go = GameObject.CreatePrimitive(primitive);
                go.name = name;
            }
            else
            {
                go = new GameObject(name);
            }

            if (!string.IsNullOrEmpty(parentPath))
            {
                var parent = GameObject.Find(parentPath);
                if (parent != null)
                    go.transform.SetParent(parent.transform, false);
            }

            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            Selection.activeGameObject = go;

            yield return CommandResult.Success(new
            {
                name = go.name,
                path = GetGameObjectPath(go),
                instanceId = go.GetInstanceID()
            });
        }

        [AIBridge("Destroy a GameObject",
            "AIBridgeCLI GameObjectCommand_Destroy --path \"Player\"")]
        public static IEnumerator Destroy(
            [Description("Hierarchy path of the GameObject")] string path = null,
            [Description("Instance ID of the GameObject")] int instanceId = 0)
        {
            var go = GetTargetGameObject(path, instanceId);
            if (go == null)
            {
                yield return CommandResult.Failure("GameObject not found. Provide 'path' or 'instanceId', or select a GameObject");
                yield break;
            }

            var goName = go.name;
            var goPath = GetGameObjectPath(go);
            Undo.DestroyObjectImmediate(go);

            yield return CommandResult.Success(new
            {
                action = "destroy",
                destroyedName = goName,
                destroyedPath = goPath
            });
        }

        [AIBridge("Find GameObjects by name, tag, or component",
            "AIBridgeCLI GameObjectCommand_Find --name \"Player\"")]
        public static IEnumerator Find(
            [Description("Name or partial name to search")] string name = null,
            [Description("Tag to filter by")] string tag = null,
            [Description("Component type name to filter by")] string withComponent = null,
            [Description("Maximum number of results")] int maxResults = 50)
        {
            var results = new List<GameObjectInfo>();

            if (!string.IsNullOrEmpty(tag))
            {
                var objects = GameObject.FindGameObjectsWithTag(tag);
                foreach (var obj in objects)
                {
                    if (results.Count >= maxResults) break;
                    if (string.IsNullOrEmpty(name) || obj.name.Contains(name))
                        results.Add(CreateGameObjectInfo(obj));
                }
            }
            else
            {
                var allObjects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
                foreach (var obj in allObjects)
                {
                    if (results.Count >= maxResults) break;
                    if (!string.IsNullOrEmpty(name) && !obj.name.Contains(name)) continue;
                    if (!string.IsNullOrEmpty(withComponent))
                    {
                        var hasComponent = false;
                        foreach (var comp in obj.GetComponents<Component>())
                        {
                            if (comp != null && comp.GetType().Name == withComponent)
                            {
                                hasComponent = true;
                                break;
                            }
                        }
                        if (!hasComponent) continue;
                    }
                    results.Add(CreateGameObjectInfo(obj));
                }
            }

            yield return CommandResult.Success(new { results, count = results.Count });
        }

        [AIBridge("Set a GameObject active or inactive",
            "AIBridgeCLI GameObjectCommand_SetActive --path \"Player\" --active false")]
        public static IEnumerator SetActive(
            [Description("Hierarchy path of the GameObject")] string path = null,
            [Description("Instance ID of the GameObject")] int instanceId = 0,
            [Description("Whether to activate the GameObject")] bool active = true,
            [Description("Toggle the current active state")] bool toggle = false)
        {
            var go = GetTargetGameObject(path, instanceId);
            if (go == null)
            {
                yield return CommandResult.Failure("GameObject not found");
                yield break;
            }

            Undo.RecordObject(go, $"Set Active {go.name}");
            go.SetActive(toggle ? !go.activeSelf : active);

            yield return CommandResult.Success(new
            {
                name = go.name,
                activeSelf = go.activeSelf,
                activeInHierarchy = go.activeInHierarchy
            });
        }

        [AIBridge("Rename a GameObject",
            "AIBridgeCLI GameObjectCommand_Rename --path \"OldName\" --newName \"NewName\"")]
        public static IEnumerator Rename(
            [Description("Hierarchy path of the GameObject")] string path = null,
            [Description("Instance ID of the GameObject")] int instanceId = 0,
            [Description("New name for the GameObject")] string newName = null)
        {
            var go = GetTargetGameObject(path, instanceId);
            if (go == null)
            {
                yield return CommandResult.Failure("GameObject not found");
                yield break;
            }
            if (string.IsNullOrEmpty(newName))
            {
                yield return CommandResult.Failure("Missing 'newName' parameter");
                yield break;
            }

            var oldName = go.name;
            Undo.RecordObject(go, $"Rename {oldName}");
            go.name = newName;

            yield return CommandResult.Success(new
            {
                oldName,
                newName = go.name,
                path = GetGameObjectPath(go)
            });
        }

        [AIBridge("Duplicate a GameObject",
            "AIBridgeCLI GameObjectCommand_Duplicate --path \"Original\"")]
        public static IEnumerator Duplicate(
            [Description("Hierarchy path of the GameObject")] string path = null,
            [Description("Instance ID of the GameObject")] int instanceId = 0)
        {
            var go = GetTargetGameObject(path, instanceId);
            if (go == null)
            {
                yield return CommandResult.Failure("GameObject not found");
                yield break;
            }

            var duplicate = UnityEngine.Object.Instantiate(go, go.transform.parent);
            duplicate.name = go.name;
            Undo.RegisterCreatedObjectUndo(duplicate, $"Duplicate {go.name}");
            Selection.activeGameObject = duplicate;

            yield return CommandResult.Success(new
            {
                originalName = go.name,
                duplicateName = duplicate.name,
                duplicatePath = GetGameObjectPath(duplicate),
                duplicateInstanceId = duplicate.GetInstanceID()
            });
        }

        [AIBridge("Get detailed info about a GameObject",
            "AIBridgeCLI GameObjectCommand_GetInfo --path \"Player\"")]
        public static IEnumerator GetInfo(
            [Description("Hierarchy path of the GameObject")] string path = null,
            [Description("Instance ID of the GameObject")] int instanceId = 0)
        {
            var go = GetTargetGameObject(path, instanceId);
            if (go == null)
            {
                yield return CommandResult.Failure("GameObject not found");
                yield break;
            }

            var components = new List<string>();
            foreach (var comp in go.GetComponents<Component>())
            {
                if (comp != null) components.Add(comp.GetType().FullName);
            }

            var childCount = go.transform.childCount;
            var children = new List<string>();
            for (var i = 0; i < Math.Min(childCount, 20); i++)
                children.Add(go.transform.GetChild(i).name);

            yield return CommandResult.Success(new
            {
                name = go.name,
                path = GetGameObjectPath(go),
                instanceId = go.GetInstanceID(),
                tag = go.tag,
                layer = LayerMask.LayerToName(go.layer),
                layerIndex = go.layer,
                activeSelf = go.activeSelf,
                activeInHierarchy = go.activeInHierarchy,
                isStatic = go.isStatic,
                components,
                childCount,
                children,
                parentName = go.transform.parent?.name
            });
        }

        private static GameObject GetTargetGameObject(string path, int instanceId)
        {
            if (instanceId != 0)
                return EditorUtility.InstanceIDToObject(instanceId) as GameObject;
            if (!string.IsNullOrEmpty(path))
                return GameObject.Find(path);
            return Selection.activeGameObject;
        }

        private static string GetGameObjectPath(GameObject go)
        {
            var goPath = go.name;
            var parent = go.transform.parent;
            while (parent != null)
            {
                goPath = parent.name + "/" + goPath;
                parent = parent.parent;
            }
            return goPath;
        }

        private static GameObjectInfo CreateGameObjectInfo(GameObject go)
        {
            return new GameObjectInfo
            {
                name = go.name,
                path = GetGameObjectPath(go),
                instanceId = go.GetInstanceID(),
                activeSelf = go.activeSelf
            };
        }

        [Serializable]
        private class GameObjectInfo
        {
            public string name;
            public string path;
            public int instanceId;
            public bool activeSelf;
        }
    }
}
