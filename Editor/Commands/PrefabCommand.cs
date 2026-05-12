using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEditor;
using UnityEngine;

namespace AIBridge.Editor
{
    public static class PrefabCommand
    {
        [AIBridge("在场景中实例化预制体",
            "AIBridgeCLI PrefabCommand_Instantiate --prefabPath \"Assets/Prefabs/Player.prefab\"")]
        public static IEnumerator Instantiate(
            [Description("预制体的资源路径")] string prefabPath = null,
            [Description("X 位置")] float posX = 0f,
            [Description("Y 位置")] float posY = 0f,
            [Description("Z 位置")] float posZ = 0f)
        {
            if (string.IsNullOrEmpty(prefabPath))
            {
                yield return CommandResult.Failure("Missing 'prefabPath' parameter");
                yield break;
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                yield return CommandResult.Failure($"Prefab not found at path: {prefabPath}");
                yield break;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.position = new Vector3(posX, posY, posZ);
            Selection.activeGameObject = instance;

            yield return CommandResult.Success(new
            {
                prefabPath,
                instanceName = instance.name,
                position = new { x = posX, y = posY, z = posZ }
            });
        }

        [AIBridge("将 GameObject 保存为预制体资源",
            "AIBridgeCLI PrefabCommand_Save --gameObjectPath \"Player\" --savePath \"Assets/Prefabs/Player.prefab\"")]
        public static IEnumerator Save(
            [Description("GameObject 的层级路径（如果省略则使用选择）")] string gameObjectPath = null,
            [Description("保存预制体的资源路径")] string savePath = null)
        {
            if (string.IsNullOrEmpty(savePath))
            {
                yield return CommandResult.Failure("Missing 'savePath' parameter");
                yield break;
            }

            GameObject go;
            if (!string.IsNullOrEmpty(gameObjectPath))
            {
                go = GameObject.Find(gameObjectPath);
                if (go == null) { yield return CommandResult.Failure($"GameObject not found: {gameObjectPath}"); yield break; }
            }
            else
            {
                go = Selection.activeGameObject;
                if (go == null) { yield return CommandResult.Failure("No GameObject selected and no 'gameObjectPath' provided"); yield break; }
            }

            if (!savePath.EndsWith(".prefab")) savePath += ".prefab";
            var savedPrefab = PrefabUtility.SaveAsPrefabAsset(go, savePath);
            AssetDatabase.Refresh();

            yield return CommandResult.Success(new { gameObjectName = go.name, prefabPath = savePath, saved = savedPrefab != null });
        }

        [AIBridge("解包预制体实例",
            "AIBridgeCLI PrefabCommand_Unpack --gameObjectPath \"Player(Clone)\"")]
        public static IEnumerator Unpack(
            [Description("预制体实例的层级路径（如果省略则使用选择）")] string gameObjectPath = null,
            [Description("完全解包（所有嵌套预制体）")] bool completely = false)
        {
            GameObject go;
            if (!string.IsNullOrEmpty(gameObjectPath))
            {
                go = GameObject.Find(gameObjectPath);
                if (go == null) { yield return CommandResult.Failure($"GameObject not found: {gameObjectPath}"); yield break; }
            }
            else
            {
                go = Selection.activeGameObject;
                if (go == null) { yield return CommandResult.Failure("No GameObject selected and no 'gameObjectPath' provided"); yield break; }
            }

            if (!PrefabUtility.IsPartOfAnyPrefab(go))
            {
                yield return CommandResult.Failure($"GameObject '{go.name}' is not part of a prefab");
                yield break;
            }

            PrefabUtility.UnpackPrefabInstance(go,
                completely ? PrefabUnpackMode.Completely : PrefabUnpackMode.OutermostRoot,
                InteractionMode.AutomatedAction);

            yield return CommandResult.Success(new { gameObjectName = go.name, unpacked = true, completely });
        }

        [AIBridge("获取资源或实例的预制体信息",
            "AIBridgeCLI PrefabCommand_GetInfo --prefabPath \"Assets/Prefabs/Player.prefab\"")]
        public static IEnumerator GetInfo(
            [Description("预制体的资源路径")] string prefabPath = null,
            [Description("预制体实例的层级路径")] string gameObjectPath = null)
        {
            GameObject target;
            if (!string.IsNullOrEmpty(prefabPath))
            {
                target = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (target == null) { yield return CommandResult.Failure($"Prefab not found at path: {prefabPath}"); yield break; }
            }
            else if (!string.IsNullOrEmpty(gameObjectPath))
            {
                target = GameObject.Find(gameObjectPath);
                if (target == null) { yield return CommandResult.Failure($"GameObject not found: {gameObjectPath}"); yield break; }
            }
            else
            {
                target = Selection.activeGameObject;
                if (target == null) { yield return CommandResult.Failure("Provide 'prefabPath', 'gameObjectPath', or select a GameObject"); yield break; }
            }

            yield return CommandResult.Success(new
            {
                name = target.name,
                isPrefabAsset = PrefabUtility.IsPartOfPrefabAsset(target),
                isPrefabInstance = PrefabUtility.IsPartOfPrefabInstance(target),
                prefabAssetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(target),
                prefabType = PrefabUtility.GetPrefabAssetType(target).ToString(),
                prefabStatus = PrefabUtility.GetPrefabInstanceStatus(target).ToString()
            });
        }

        [AIBridge("获取预制体层级结构树",
            "AIBridgeCLI PrefabCommand_GetHierarchy --prefabPath \"Assets/Prefabs/Player.prefab\"")]
        public static IEnumerator GetHierarchy(
            [Description("预制体的资源路径")] string prefabPath = null,
            [Description("最大遍历深度")] int depth = 5,
            [Description("是否包含未激活的子对象")] bool includeInactive = true,
            [Description("是否包含组件类型名")] bool includeComponents = true)
        {
            if (string.IsNullOrEmpty(prefabPath))
            {
                yield return CommandResult.Failure("Missing 'prefabPath' parameter");
                yield break;
            }

            var prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabRoot == null)
            {
                yield return CommandResult.Failure($"Prefab not found at path: {prefabPath}");
                yield break;
            }

            depth = Mathf.Max(0, depth);
            var hierarchy = new List<PrefabTreeNode>();
            var truncated = false;

            if (includeInactive || prefabRoot.activeSelf)
            {
                hierarchy.Add(BuildTreeNode(prefabRoot, prefabRoot.name, depth, includeInactive, includeComponents, ref truncated));
            }

            yield return CommandResult.Success(new
            {
                prefabPath,
                prefabName = prefabRoot.name,
                depth,
                includeInactive,
                includeComponents,
                truncated,
                hierarchy
            });
        }

        private static PrefabTreeNode BuildTreeNode(GameObject go, string path, int remainingDepth,
            bool includeInactive, bool includeComponents, ref bool truncated)
        {
            var node = new PrefabTreeNode
            {
                name = go.name,
                path = path,
                active = go.activeSelf,
                childCount = go.transform.childCount,
                components = new List<string>(),
                children = new List<PrefabTreeNode>()
            };

            if (includeComponents)
            {
                foreach (var component in go.GetComponents<Component>())
                {
                    if (component != null)
                        node.components.Add(component.GetType().Name);
                }
            }

            if (remainingDepth <= 0)
            {
                if (go.transform.childCount > 0)
                    truncated = true;
                return node;
            }

            foreach (Transform child in go.transform)
            {
                if (!includeInactive && !child.gameObject.activeSelf)
                    continue;

                node.children.Add(BuildTreeNode(
                    child.gameObject,
                    path + "/" + child.gameObject.name,
                    remainingDepth - 1,
                    includeInactive,
                    includeComponents,
                    ref truncated));
            }

            return node;
        }

        [System.Serializable]
        private class PrefabTreeNode
        {
            public string name;
            public string path;
            public bool active;
            public List<string> components;
            public int childCount;
            public List<PrefabTreeNode> children;
        }

        [AIBridge("将预制体实例的覆盖应用回预制体资源",
            "AIBridgeCLI PrefabCommand_Apply --gameObjectPath \"Player(Clone)\"")]
        public static IEnumerator Apply(
            [Description("预制体实例的层级路径（如果省略则使用选择）")] string gameObjectPath = null)
        {
            GameObject go;
            if (!string.IsNullOrEmpty(gameObjectPath))
            {
                go = GameObject.Find(gameObjectPath);
                if (go == null) { yield return CommandResult.Failure($"GameObject not found: {gameObjectPath}"); yield break; }
            }
            else
            {
                go = Selection.activeGameObject;
                if (go == null) { yield return CommandResult.Failure("No GameObject selected and no 'gameObjectPath' provided"); yield break; }
            }

            if (!PrefabUtility.IsPartOfPrefabInstance(go))
            {
                yield return CommandResult.Failure($"GameObject '{go.name}' is not a prefab instance");
                yield break;
            }

            var appliedPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
            PrefabUtility.ApplyPrefabInstance(go, InteractionMode.AutomatedAction);
            AssetDatabase.Refresh();

            yield return CommandResult.Success(new { gameObjectName = go.name, prefabPath = appliedPath, applied = true });
        }
    }
}
