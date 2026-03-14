using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Component = UnityEngine.Component;

namespace AIBridge.Editor
{
    public static class SceneCommand
    {
        [AIBridge("在编辑器中加载场景",
            "AIBridgeCLI SceneCommand_Load --scenePath \"Assets/Scenes/Main.unity\"")]
        public static IEnumerator Load(
            [Description("场景文件的资源路径")] string scenePath = null,
            [Description("加载模式：single 或 additive")] string mode = "single")
        {
            if (string.IsNullOrEmpty(scenePath))
            {
                yield return CommandResult.Failure("Missing 'scenePath' parameter");
                yield break;
            }
            var openMode = mode.ToLower() == "additive" ? OpenSceneMode.Additive : OpenSceneMode.Single;
            var scene = EditorSceneManager.OpenScene(scenePath, openMode);
            yield return CommandResult.Success(new { scenePath, sceneName = scene.name, loaded = scene.isLoaded });
        }

        [AIBridge("获取场景层级结构树",
            "AIBridgeCLI SceneCommand_GetHierarchy --depth 3")]
        public static IEnumerator GetHierarchy(
            [Description("遍历的最大深度")] int depth = 3,
            [Description("包含未激活的 GameObject")] bool includeInactive = true)
        {
            var scene = SceneManager.GetActiveScene();
            var rootObjects = scene.GetRootGameObjects();
            var hierarchy = new List<HierarchyNode>();

            foreach (var root in rootObjects)
            {
                if (!includeInactive && !root.activeInHierarchy) continue;
                hierarchy.Add(BuildHierarchyNode(root, depth, includeInactive));
            }

            yield return CommandResult.Success(new
            {
                sceneName = scene.name,
                scenePath = scene.path,
                rootCount = hierarchy.Count,
                hierarchy
            });
        }

        [AIBridge("获取当前激活场景的信息",
            "AIBridgeCLI SceneCommand_GetActive")]
        public static IEnumerator GetActive()
        {
            var scene = SceneManager.GetActiveScene();
            yield return CommandResult.Success(new
            {
                name = scene.name,
                path = scene.path,
                isLoaded = scene.isLoaded,
                isDirty = scene.isDirty,
                rootCount = scene.rootCount
            });
        }

        private static HierarchyNode BuildHierarchyNode(GameObject go, int remainingDepth, bool includeInactive)
        {
            var node = new HierarchyNode
            {
                name = go.name,
                active = go.activeSelf,
                components = new List<string>()
            };
            foreach (var component in go.GetComponents<Component>())
            {
                if (component != null) node.components.Add(component.GetType().Name);
            }
            if (remainingDepth > 0 && go.transform.childCount > 0)
            {
                node.children = new List<HierarchyNode>();
                foreach (Transform child in go.transform)
                {
                    if (!includeInactive && !child.gameObject.activeInHierarchy) continue;
                    node.children.Add(BuildHierarchyNode(child.gameObject, remainingDepth - 1, includeInactive));
                }
            }
            return node;
        }

        [Serializable]
        private class HierarchyNode
        {
            public string name;
            public bool active;
            public List<string> components;
            public List<HierarchyNode> children;
        }
    }
}
