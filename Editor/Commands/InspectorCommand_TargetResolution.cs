using System;
using UnityEditor;
using UnityEngine;
using Component = UnityEngine.Component;

namespace AIBridge.Editor
{
    public static partial class InspectorCommand
    {
        #region Target Resolution

        private static bool TryResolveTargetContext(string path, int instanceId, string assetPath, string objectPath, bool forWrite, out TargetContext context, out string error)
        {
            context = null;
            error = null;

            if (!string.IsNullOrEmpty(assetPath))
            {
                if (!IsUnityAssetPath(assetPath))
                {
                    error = "assetPath must start with Assets/ or Packages/";
                    return false;
                }
                if (forWrite && assetPath.StartsWith(PackagesPathPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    error = "Editing package assets is not supported. Copy the asset into Assets/ first.";
                    return false;
                }

                if (System.IO.Path.GetExtension(assetPath).Equals(PrefabExtension, StringComparison.OrdinalIgnoreCase))
                    return TryResolvePrefabAssetContext(assetPath, objectPath, out context, out error);

                if (!string.IsNullOrEmpty(objectPath))
                {
                    error = "objectPath is only supported for prefab assets";
                    return false;
                }

                var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
                if (asset == null)
                {
                    error = $"Asset not found at path: {assetPath}";
                    return false;
                }

                context = new TargetContext
                {
                    AssetPath = assetPath,
                    SerializedTarget = asset,
                    GameObject = asset as GameObject,
                    IsSceneObject = false,
                    IsAssetObject = true
                };
                return true;
            }

            var go = GameObjectHelper.GetTargetGameObject(path, instanceId);
            if (go == null)
            {
                error = "GameObject not found";
                return false;
            }

            context = new TargetContext
            {
                GameObject = go,
                SerializedTarget = go,
                IsSceneObject = true
            };
            return true;
        }

        private static bool TryResolvePrefabAssetContext(string assetPath, string objectPath, out TargetContext context, out string error)
        {
            context = null;
            error = null;

            GameObject root;
            try
            {
                root = PrefabUtility.LoadPrefabContents(assetPath);
            }
            catch (Exception ex)
            {
                error = $"Failed to load prefab contents: {ex.Message}";
                return false;
            }

            if (root == null)
            {
                error = $"Prefab not found at path: {assetPath}";
                return false;
            }

            var target = ResolvePrefabObject(root, objectPath);
            if (target == null)
            {
                PrefabUtility.UnloadPrefabContents(root);
                error = $"Object not found in prefab: {objectPath}";
                return false;
            }

            context = new TargetContext
            {
                AssetPath = assetPath,
                ObjectPath = string.IsNullOrEmpty(objectPath) ? target.name : objectPath,
                PrefabRoot = root,
                GameObject = target,
                SerializedTarget = target,
                IsPrefabAsset = true,
                IsAssetObject = true,
                IsSceneObject = false
            };
            return true;
        }

        private static bool TryResolveSerializedTarget(TargetContext context, string componentName, int componentIndex, out UnityEngine.Object serializedTarget, out Component component, out string error)
        {
            serializedTarget = null;
            component = null;
            error = null;

            if (context.GameObject != null)
            {
                component = FindComponent(context.GameObject, componentName, componentIndex);
                if (component == null && (!string.IsNullOrEmpty(componentName) || componentIndex >= 0))
                {
                    error = "Component not found. Provide 'componentName' or 'componentIndex'";
                    return false;
                }

                if (component != null)
                {
                    serializedTarget = component;
                    return true;
                }

                if (context.IsAssetObject && !(context.SerializedTarget is GameObject))
                {
                    serializedTarget = context.SerializedTarget;
                    return true;
                }

                error = "Component not found. Provide 'componentName' or 'componentIndex'";
                return false;
            }

            if (context.SerializedTarget != null)
            {
                serializedTarget = context.SerializedTarget;
                return true;
            }

            error = "Serialized target not found";
            return false;
        }

        private static GameObject ResolvePrefabObject(GameObject root, string objectPath)
        {
            if (root == null) return null;
            if (string.IsNullOrEmpty(objectPath) || objectPath == "." || objectPath == "/" || objectPath == root.name)
                return root;

            var normalized = objectPath.Replace('\\', '/').Trim('/');
            if (normalized.StartsWith(root.name + "/", StringComparison.Ordinal))
                normalized = normalized.Substring(root.name.Length + 1);

            var child = root.transform.Find(normalized);
            if (child != null) return child.gameObject;

            return FindChildByName(root.transform, normalized);
        }

        private static GameObject FindChildByName(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name)) return null;
            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.name == name) return child.gameObject;
                var found = FindChildByName(child, name);
                if (found != null) return found;
            }
            return null;
        }

        #endregion

        #region Save & Helpers

        private static void SaveModifiedTarget(TargetContext context, UnityEngine.Object serializedTarget)
        {
            if (context == null) return;

            if (context.IsPrefabAsset && context.PrefabRoot != null)
            {
                PrefabUtility.SaveAsPrefabAsset(context.PrefabRoot, context.AssetPath);
                AssetDatabase.ImportAsset(context.AssetPath);
                return;
            }

            if (context.IsAssetObject && serializedTarget != null)
            {
                EditorUtility.SetDirty(serializedTarget);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(context.AssetPath);
            }
        }

        private static bool IsUnityAssetPath(string assetPath)
        {
            return assetPath.StartsWith(AssetsPathPrefix, StringComparison.OrdinalIgnoreCase)
                   || assetPath.StartsWith(PackagesPathPrefix, StringComparison.OrdinalIgnoreCase);
        }

        private static Component FindComponent(GameObject go, string componentName, int componentIndex)
        {
            if (componentIndex >= 0)
            {
                var comps = go.GetComponents<Component>();
                return componentIndex < comps.Length ? comps[componentIndex] : null;
            }
            if (!string.IsNullOrEmpty(componentName))
            {
                foreach (var comp in go.GetComponents<Component>())
                {
                    if (comp != null && (comp.GetType().Name == componentName || comp.GetType().FullName == componentName))
                        return comp;
                }
            }
            return null;
        }

        private static Type ResolveComponentType(string typeName)
        {
            var possibleNames = new[] { typeName, $"UnityEngine.{typeName}", $"UnityEngine.UI.{typeName}", $"TMPro.{typeName}" };
            foreach (var name in possibleNames)
            {
                var componentType = Type.GetType(name);
                if (componentType != null) return componentType;
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    componentType = assembly.GetType(name);
                    if (componentType != null) return componentType;
                }
            }
            return null;
        }

        #endregion
    }
}
