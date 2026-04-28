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
        [AIBridge("在场景中创建新的 GameObject",
            "AIBridgeCLI GameObjectCommand_Create --name \"MyCube\" --primitiveType Cube")]
        public static IEnumerator Create(
            string name = "New GameObject",
            [Description("基础类型：Cube, Sphere, Capsule, Cylinder, Plane, Quad")] string primitiveType = null,
            [Description("父级 GameObject 的层级路径")] string parentPath = null)
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
                path = GameObjectHelper.GetGameObjectPath(go),
                instanceId = go.GetInstanceID()
            });
        }

        [AIBridge("销毁 GameObject",
            "AIBridgeCLI GameObjectCommand_Destroy --path \"Player\"")]
        public static IEnumerator Destroy(
            [Description("GameObject 的层级路径")] string path = null,
            [Description("GameObject 的实例 ID")] int instanceId = 0)
        {
            var go = GameObjectHelper.GetTargetGameObject(path, instanceId);
            if (go == null)
            {
                yield return CommandResult.Failure("GameObject not found. Provide 'path' or 'instanceId', or select a GameObject");
                yield break;
            }

            var goName = go.name;
            var goPath = GameObjectHelper.GetGameObjectPath(go);
            Undo.DestroyObjectImmediate(go);

            yield return CommandResult.Success(new
            {
                action = "destroy",
                destroyedName = goName,
                destroyedPath = goPath
            });
        }

        [AIBridge("在场景中查找GameObject",
            @"参数优先级（互斥，只使用优先级最高的）：
1. path - 精确路径查找（最快），如 Canvas/Button，找到后立即返回
2. name - 精确名称查找，返回所有匹配的对象
3. tag - 按标签查找，返回所有该标签的对象
4. withComponent - 按组件查找，返回所有包含该组件的对象
5. namePattern - 模糊名称搜索，支持*通配符

组合使用：tag/withComponent 可作为额外过滤条件配合 name/namePattern 使用

示例：
AIBridgeCLI GameObjectCommand_Find --path ""Canvas/Button""
AIBridgeCLI GameObjectCommand_Find --name ""Player""
AIBridgeCLI GameObjectCommand_Find --tag ""Enemy""
AIBridgeCLI GameObjectCommand_Find --withComponent ""Rigidbody""
AIBridgeCLI GameObjectCommand_Find --namePattern ""Enemy*"" --tag ""Enemy""
AIBridgeCLI GameObjectCommand_Find --name ""Player"" --withComponent ""CharacterController""")]
        public static IEnumerator Find(
            [Description("精确路径查找，如 Canvas/Button")] string path = null,
            [Description("精确名称查找")] string name = null,
            [Description("按标签过滤")] string tag = null,
            [Description("按组件类型名称过滤，如 Rigidbody、BoxCollider")] string withComponent = null,
            [Description("模糊名称匹配，支持*通配符（如 Enemy*）")] string namePattern = null,
            [Description("是否包含未激活的对象")] bool includeInactive = false,
            [Description("最大结果数量")] int maxResults = 50)
        {
            var results = new List<GameObjectInfo>();

            // 优先级1: 精确路径查找
            if (!string.IsNullOrEmpty(path))
            {
                var go = GameObject.Find(path);
                if (go == null && includeInactive)
                {
                    foreach (var obj in Resources.FindObjectsOfTypeAll<GameObject>())
                    {
                        if (!IsSceneObject(obj)) continue;
                        if (GameObjectHelper.GetGameObjectPath(obj) == path)
                        {
                            go = obj;
                            break;
                        }
                    }
                }
                if (go != null)
                {
                    results.Add(GameObjectHelper.CreateGameObjectInfo(go));
                }
                yield return CommandResult.Success(new { searchType = "path", count = results.Count, gameObjects = results });
                yield break;
            }

            // 优先级2: 精确名称查找
            if (!string.IsNullOrEmpty(name))
            {
                var allObjects = includeInactive
                    ? Resources.FindObjectsOfTypeAll<GameObject>()
                    : UnityEngine.Object.FindObjectsOfType<GameObject>();

                foreach (var obj in allObjects)
                {
                    if (results.Count >= maxResults) break;
                    if (includeInactive && !IsSceneObject(obj)) continue;
                    if (!includeInactive && !obj.activeInHierarchy) continue;

                    if (obj.name == name)
                    {
                        if (!string.IsNullOrEmpty(tag) && obj.tag != tag) continue;
                        if (!string.IsNullOrEmpty(withComponent) && !HasComponent(obj, withComponent)) continue;
                        results.Add(GameObjectHelper.CreateGameObjectInfo(obj));
                    }
                }

                yield return CommandResult.Success(new { searchType = "name", count = results.Count, gameObjects = results });
                yield break;
            }

            // 优先级3: 按标签查找
            if (!string.IsNullOrEmpty(tag))
            {
                GameObject[] objects;
                try
                {
                    objects = GameObject.FindGameObjectsWithTag(tag);
                }
                catch (UnityException)
                {
                    yield return CommandResult.Failure($"Tag '{tag}' is not defined in the Tag Manager");
                    yield break;
                }
                foreach (var obj in objects)
                {
                    if (results.Count >= maxResults) break;
                    if (!includeInactive && !obj.activeInHierarchy) continue;

                    if (!string.IsNullOrEmpty(namePattern) && !MatchesPattern(obj.name, namePattern)) continue;
                    if (!string.IsNullOrEmpty(withComponent) && !HasComponent(obj, withComponent)) continue;

                    results.Add(GameObjectHelper.CreateGameObjectInfo(obj));
                }

                yield return CommandResult.Success(new { searchType = "tag", count = results.Count, gameObjects = results });
                yield break;
            }

            // 优先级4: 按组件查找
            if (!string.IsNullOrEmpty(withComponent))
            {
                var allObjects = includeInactive
                    ? Resources.FindObjectsOfTypeAll<GameObject>()
                    : UnityEngine.Object.FindObjectsOfType<GameObject>();

                foreach (var obj in allObjects)
                {
                    if (results.Count >= maxResults) break;
                    if (includeInactive && !IsSceneObject(obj)) continue;
                    if (!includeInactive && !obj.activeInHierarchy) continue;

                    if (HasComponent(obj, withComponent))
                    {
                        if (!string.IsNullOrEmpty(namePattern) && !MatchesPattern(obj.name, namePattern)) continue;
                        results.Add(GameObjectHelper.CreateGameObjectInfo(obj));
                    }
                }

                yield return CommandResult.Success(new { searchType = "component", count = results.Count, gameObjects = results });
                yield break;
            }

            // 优先级5: 模糊名称搜索
            if (!string.IsNullOrEmpty(namePattern))
            {
                var allObjects = includeInactive
                    ? Resources.FindObjectsOfTypeAll<GameObject>()
                    : UnityEngine.Object.FindObjectsOfType<GameObject>();

                foreach (var obj in allObjects)
                {
                    if (results.Count >= maxResults) break;
                    if (includeInactive && !IsSceneObject(obj)) continue;
                    if (!includeInactive && !obj.activeInHierarchy) continue;

                    if (MatchesPattern(obj.name, namePattern))
                    {
                        results.Add(GameObjectHelper.CreateGameObjectInfo(obj));
                    }
                }

                yield return CommandResult.Success(new { searchType = "pattern", count = results.Count, gameObjects = results });
                yield break;
            }

            yield return CommandResult.Failure("必须指定至少一个查找条件：path、name、tag、withComponent 或 namePattern");
        }

        private static bool MatchesPattern(string name, string pattern)
        {
            if (string.IsNullOrEmpty(pattern)) return true;

            if (pattern.Contains("*"))
            {
                var regex = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
                    .Replace("\\*", ".*") + "$";
                return System.Text.RegularExpressions.Regex.IsMatch(name, regex,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            return name.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool HasComponent(GameObject go, string componentType)
        {
            foreach (var comp in go.GetComponents<Component>())
            {
                if (comp != null && comp.GetType().Name.Equals(componentType, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static bool IsSceneObject(GameObject go)
        {
            return go.scene.IsValid() && (go.hideFlags & HideFlags.HideInHierarchy) == 0;
        }

        [AIBridge("设置 GameObject 的激活或非激活状态",
            "AIBridgeCLI GameObjectCommand_SetActive --path \"Player\" --active false")]
        public static IEnumerator SetActive(
            [Description("GameObject 的层级路径")] string path = null,
            [Description("GameObject 的实例 ID")] int instanceId = 0,
            [Description("是否激活 GameObject")] bool active = true)
        {
            var go = GameObjectHelper.GetTargetGameObject(path, instanceId);
            if (go == null)
            {
                yield return CommandResult.Failure("GameObject not found");
                yield break;
            }

            Undo.RecordObject(go, $"Set Active {go.name}");
            go.SetActive(active);

            yield return CommandResult.Success(new
            {
                name = go.name,
                activeSelf = go.activeSelf,
                activeInHierarchy = go.activeInHierarchy
            });
        }

        [AIBridge("获取 GameObject 的详细信息",
            "AIBridgeCLI GameObjectCommand_GetInfo --path \"Player\"")]
        public static IEnumerator GetInfo(
            [Description("GameObject 的层级路径")] string path = null,
            [Description("GameObject 的实例 ID")] int instanceId = 0)
        {
            var go = GameObjectHelper.GetTargetGameObject(path, instanceId);
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
                path = GameObjectHelper.GetGameObjectPath(go),
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
    }
}
