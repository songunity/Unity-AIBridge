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
        [AIBridge("Load a scene in the Editor",
            "AIBridgeCLI SceneCommand_Load --scenePath \"Assets/Scenes/Main.unity\"")]
        public static IEnumerator Load(
            [Description("Asset path to the scene file")] string scenePath = null,
            [Description("Load mode: single or additive")] string mode = "single")
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

        [AIBridge("Save the current open scene(s)",
            "AIBridgeCLI SceneCommand_Save")]
        public static IEnumerator Save(
            [Description("Save to a new path (save-as)")] string saveAs = null)
        {
            bool saved;
            var scene = SceneManager.GetActiveScene();
            if (!string.IsNullOrEmpty(saveAs))
                saved = EditorSceneManager.SaveScene(scene, saveAs);
            else
                saved = EditorSceneManager.SaveOpenScenes();

            yield return CommandResult.Success(new { sceneName = scene.name, scenePath = scene.path, saved });
        }

        [AIBridge("Get the scene hierarchy as a tree",
            "AIBridgeCLI SceneCommand_GetHierarchy --depth 3")]
        public static IEnumerator GetHierarchy(
            [Description("Maximum depth to traverse")] int depth = 3,
            [Description("Include inactive GameObjects")] bool includeInactive = true)
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

        [AIBridge("Get info about the active scene",
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

        [AIBridge("Create a new empty scene",
            "AIBridgeCLI SceneCommand_New --setup empty")]
        public static IEnumerator New(
            [Description("Scene setup: default or empty")] string setup = "default")
        {
            var newSceneSetup = setup.ToLower() == "empty" ? NewSceneSetup.EmptyScene : NewSceneSetup.DefaultGameObjects;
            var scene = EditorSceneManager.NewScene(newSceneSetup, NewSceneMode.Single);
            yield return CommandResult.Success(new { sceneName = scene.name, created = true });
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
