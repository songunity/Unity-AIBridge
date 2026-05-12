using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Component = UnityEngine.Component;

namespace AIBridge.Editor
{
    public static class InspectorCommand
    {
        private const string AssetsPathPrefix = "Assets/";
        private const string PackagesPathPrefix = "Packages/";
        private const string PrefabExtension = ".prefab";

        [AIBridge("获取 GameObject 上的所有组件",
            "AIBridgeCLI InspectorCommand_GetComponents --path \"Player\"")]
        public static IEnumerator GetComponents(
            [Description("GameObject 的层级路径")] string path = null,
            [Description("GameObject 的实例 ID")] int instanceId = 0,
            [Description("资产路径（如 Assets/UI/Panel.prefab）")] string assetPath = null,
            [Description("Prefab 内对象路径")] string objectPath = null)
        {
            TargetContext context;
            string error;
            if (!TryResolveTargetContext(path, instanceId, assetPath, objectPath, false, out context, out error))
            {
                yield return CommandResult.Failure(error);
                yield break;
            }

            try
            {
                var go = context.GameObject;
                if (go == null)
                {
                    yield return CommandResult.Failure("Target does not contain components");
                    yield break;
                }

                var components = new List<ComponentInfo>();
                var allComponents = go.GetComponents<Component>();
                for (var i = 0; i < allComponents.Length; i++)
                {
                    var comp = allComponents[i];
                    if (comp == null) continue;
                    var info = new ComponentInfo
                    {
                        index = i,
                        typeName = comp.GetType().Name,
                        fullTypeName = comp.GetType().FullName,
                        instanceId = comp.GetInstanceID()
                    };
                    if (comp is Behaviour behaviour)
                        info.enabled = behaviour.enabled;
                    components.Add(info);
                }

                yield return CommandResult.Success(new
                {
                    gameObjectName = go.name,
                    assetPath = context.AssetPath,
                    objectPath = context.ObjectPath,
                    components,
                    count = components.Count
                });
            }
            finally
            {
                context.Dispose();
            }
        }

        [AIBridge("获取组件的序列化属性",
            "AIBridgeCLI InspectorCommand_GetProperties --path \"Player\" --componentName \"Transform\"")]
        public static IEnumerator GetProperties(
            [Description("GameObject 的层级路径")] string path = null,
            [Description("GameObject 的实例 ID")] int instanceId = 0,
            [Description("组件类型名称")] string componentName = null,
            [Description("组件索引（替代 componentName）")] int componentIndex = -1,
            [Description("是否展开子属性")] bool includeChildren = false,
            [Description("资产路径（如 Assets/UI/Panel.prefab）")] string assetPath = null,
            [Description("Prefab 内对象路径")] string objectPath = null)
        {
            TargetContext context;
            string error;
            if (!TryResolveTargetContext(path, instanceId, assetPath, objectPath, false, out context, out error))
            {
                yield return CommandResult.Failure(error);
                yield break;
            }

            try
            {
                UnityEngine.Object serializedTarget;
                Component component;
                if (!TryResolveSerializedTarget(context, componentName, componentIndex, out serializedTarget, out component, out error))
                {
                    yield return CommandResult.Failure(error);
                    yield break;
                }

                var properties = new List<PropInfo>();
                var so = new SerializedObject(serializedTarget);
                var iterator = so.GetIterator();
                var enterChildren = true;
                while (iterator.NextVisible(enterChildren))
                {
                    enterChildren = includeChildren;
                    properties.Add(BuildPropertyInfo(iterator));
                }

                yield return CommandResult.Success(new
                {
                    targetName = serializedTarget.name,
                    targetType = serializedTarget.GetType().Name,
                    gameObjectName = context.GameObject != null ? context.GameObject.name : null,
                    componentName = component != null ? component.GetType().Name : null,
                    assetPath = context.AssetPath,
                    objectPath = context.ObjectPath,
                    properties
                });
            }
            finally
            {
                context.Dispose();
            }
        }

        [AIBridge("获取组件的单个序列化属性值",
            "AIBridgeCLI InspectorCommand_GetProperty --path \"Player\" --componentName \"Rigidbody\" --propertyName \"mass\"")]
        public static IEnumerator GetProperty(
            [Description("GameObject 的层级路径")] string path = null,
            [Description("GameObject 的实例 ID")] int instanceId = 0,
            [Description("组件类型名称")] string componentName = null,
            [Description("组件索引（替代 componentName）")] int componentIndex = -1,
            [Description("序列化属性名称")] string propertyName = null,
            [Description("资产路径（如 Assets/UI/Panel.prefab）")] string assetPath = null,
            [Description("Prefab 内对象路径")] string objectPath = null)
        {
            if (string.IsNullOrEmpty(propertyName))
            {
                yield return CommandResult.Failure("Missing 'propertyName' parameter");
                yield break;
            }

            TargetContext context;
            string error;
            if (!TryResolveTargetContext(path, instanceId, assetPath, objectPath, false, out context, out error))
            {
                yield return CommandResult.Failure(error);
                yield break;
            }

            try
            {
                UnityEngine.Object serializedTarget;
                Component component;
                if (!TryResolveSerializedTarget(context, componentName, componentIndex, out serializedTarget, out component, out error))
                {
                    yield return CommandResult.Failure(error);
                    yield break;
                }

                var so = new SerializedObject(serializedTarget);
                var prop = so.FindProperty(propertyName);
                if (prop == null)
                {
                    yield return CommandResult.Failure($"Property not found: {propertyName}");
                    yield break;
                }

                yield return CommandResult.Success(new
                {
                    targetName = serializedTarget.name,
                    targetType = serializedTarget.GetType().Name,
                    componentName = component != null ? component.GetType().Name : null,
                    propertyName,
                    propertyType = prop.propertyType.ToString(),
                    value = GetPropertyValue(prop),
                    editable = prop.editable,
                    assetPath = context.AssetPath,
                    objectPath = context.ObjectPath
                });
            }
            finally
            {
                context.Dispose();
            }
        }

        [AIBridge("按关键字搜索组件的序列化属性",
            "AIBridgeCLI InspectorCommand_FindProperty --path \"Player\" --componentName \"Rigidbody\" --keyword \"mass\"")]
        public static IEnumerator FindProperty(
            [Description("GameObject 的层级路径")] string path = null,
            [Description("GameObject 的实例 ID")] int instanceId = 0,
            [Description("组件类型名称")] string componentName = null,
            [Description("组件索引（替代 componentName）")] int componentIndex = -1,
            [Description("搜索关键字")] string keyword = null,
            [Description("资产路径（如 Assets/UI/Panel.prefab）")] string assetPath = null,
            [Description("Prefab 内对象路径")] string objectPath = null)
        {
            if (string.IsNullOrEmpty(keyword))
            {
                yield return CommandResult.Failure("Missing 'keyword' parameter");
                yield break;
            }

            TargetContext context;
            string error;
            if (!TryResolveTargetContext(path, instanceId, assetPath, objectPath, false, out context, out error))
            {
                yield return CommandResult.Failure(error);
                yield break;
            }

            try
            {
                UnityEngine.Object serializedTarget;
                Component component;
                if (!TryResolveSerializedTarget(context, componentName, componentIndex, out serializedTarget, out component, out error))
                {
                    yield return CommandResult.Failure(error);
                    yield break;
                }

                var matches = new List<PropInfo>();
                var so = new SerializedObject(serializedTarget);
                var iterator = so.GetIterator();
                while (iterator.NextVisible(true))
                {
                    if (iterator.propertyPath.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) < 0
                        && iterator.name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) < 0
                        && iterator.displayName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    matches.Add(BuildPropertyInfo(iterator));
                }

                yield return CommandResult.Success(new
                {
                    targetName = serializedTarget.name,
                    targetType = serializedTarget.GetType().Name,
                    componentName = component != null ? component.GetType().Name : null,
                    keyword,
                    count = matches.Count,
                    matches,
                    assetPath = context.AssetPath,
                    objectPath = context.ObjectPath
                });
            }
            finally
            {
                context.Dispose();
            }
        }

        [AIBridge("设置组件上的序列化属性",
            "AIBridgeCLI InspectorCommand_SetProperty --path \"Player\" --componentName \"Rigidbody\" --propertyName \"mass\" --value 10")]
        public static IEnumerator SetProperty(
            [Description("GameObject 的层级路径")] string path = null,
            [Description("GameObject 的实例 ID")] int instanceId = 0,
            [Description("组件类型名称")] string componentName = null,
            [Description("组件索引（替代 componentName）")] int componentIndex = -1,
            [Description("序列化属性名称")] string propertyName = null,
            [Description("属性的新值")] string value = null,
            [Description("资产路径（如 Assets/UI/Panel.prefab）")] string assetPath = null,
            [Description("Prefab 内对象路径")] string objectPath = null)
        {
            if (string.IsNullOrEmpty(propertyName))
            {
                yield return CommandResult.Failure("Missing 'propertyName' parameter");
                yield break;
            }

            var values = new Dictionary<string, object> { { propertyName, value } };
            var enumerator = SetPropertiesInternal(path, instanceId, componentName, componentIndex, values, assetPath, objectPath);
            while (enumerator.MoveNext())
                yield return enumerator.Current;
        }

        [AIBridge("批量设置组件上的多个序列化属性",
            "AIBridgeCLI InspectorCommand_SetProperties --path \"Player\" --componentName \"Transform\" --json \"{\\\"values\\\":{\\\"m_LocalPosition.x\\\":1,\\\"m_LocalPosition.y\\\":2}}\"")]
        public static IEnumerator SetProperties(
            [Description("GameObject 的层级路径")] string path = null,
            [Description("GameObject 的实例 ID")] int instanceId = 0,
            [Description("组件类型名称")] string componentName = null,
            [Description("组件索引（替代 componentName）")] int componentIndex = -1,
            [Description("属性名到值的映射 JSON 字符串")] string values = null,
            [Description("资产路径（如 Assets/UI/Panel.prefab）")] string assetPath = null,
            [Description("Prefab 内对象路径")] string objectPath = null)
        {
            if (string.IsNullOrEmpty(values))
            {
                yield return CommandResult.Failure("Missing 'values' parameter");
                yield break;
            }

            Dictionary<string, object> valueDict;
            try
            {
                var jObj = JObject.Parse(values);
                valueDict = new Dictionary<string, object>();
                foreach (var pair in jObj)
                    valueDict[pair.Key] = pair.Value.Type == JTokenType.Null ? null : pair.Value;
            }
            catch (Exception ex)
            {
                yield return CommandResult.Failure($"Failed to parse 'values' JSON: {ex.Message}");
                yield break;
            }

            var enumerator = SetPropertiesInternal(path, instanceId, componentName, componentIndex, valueDict, assetPath, objectPath);
            while (enumerator.MoveNext())
                yield return enumerator.Current;
        }

        private static IEnumerator SetPropertiesInternal(
            string path, int instanceId, string componentName, int componentIndex,
            Dictionary<string, object> values, string assetPath, string objectPath)
        {
            TargetContext context;
            string error;
            if (!TryResolveTargetContext(path, instanceId, assetPath, objectPath, true, out context, out error))
            {
                yield return CommandResult.Failure(error);
                yield break;
            }

            try
            {
                UnityEngine.Object serializedTarget;
                Component component;
                if (!TryResolveSerializedTarget(context, componentName, componentIndex, out serializedTarget, out component, out error))
                {
                    yield return CommandResult.Failure(error);
                    yield break;
                }

                var so = new SerializedObject(serializedTarget);
                so.Update();

                if (context.IsSceneObject)
                    Undo.RecordObject(serializedTarget, "Set Serialized Properties");

                var changes = new List<object>();
                foreach (var pair in values)
                {
                    if (string.IsNullOrEmpty(pair.Key))
                    {
                        yield return CommandResult.Failure("Property name cannot be empty");
                        yield break;
                    }

                    var prop = so.FindProperty(pair.Key);
                    if (prop == null)
                    {
                        yield return CommandResult.Failure($"Property not found: {pair.Key}");
                        yield break;
                    }
                    if (!prop.editable)
                    {
                        yield return CommandResult.Failure($"Property is not editable: {pair.Key}");
                        yield break;
                    }

                    var oldValue = GetPropertyValue(prop);
                    if (!SetPropertyValue(prop, pair.Value))
                    {
                        yield return CommandResult.Failure($"Failed to set property '{pair.Key}' ({prop.propertyType})");
                        yield break;
                    }
                    changes.Add(new { propertyName = pair.Key, propertyType = prop.propertyType.ToString(), oldValue, newValue = GetPropertyValue(prop) });
                }

                var changed = so.ApplyModifiedProperties();
                if (changed && !context.IsSceneObject)
                    SaveModifiedTarget(context, serializedTarget);

                yield return CommandResult.Success(new
                {
                    targetName = serializedTarget.name,
                    targetType = serializedTarget.GetType().Name,
                    gameObjectName = context.GameObject != null ? context.GameObject.name : null,
                    componentName = component != null ? component.GetType().Name : null,
                    assetPath = context.AssetPath,
                    objectPath = context.ObjectPath,
                    changed,
                    changes
                });
            }
            finally
            {
                context.Dispose();
            }
        }

        [AIBridge("向 GameObject 添加组件",
            "AIBridgeCLI InspectorCommand_AddComponent --path \"Player\" --typeName \"Rigidbody\"")]
        public static IEnumerator AddComponent(
            [Description("GameObject 的层级路径")] string path = null,
            [Description("GameObject 的实例 ID")] int instanceId = 0,
            [Description("组件类型名称（例如 Rigidbody, BoxCollider）")] string typeName = null,
            [Description("资产路径（如 Assets/UI/Panel.prefab）")] string assetPath = null,
            [Description("Prefab 内对象路径")] string objectPath = null)
        {
            if (string.IsNullOrEmpty(typeName))
            {
                yield return CommandResult.Failure("Missing 'typeName' parameter");
                yield break;
            }

            TargetContext context;
            string error;
            if (!TryResolveTargetContext(path, instanceId, assetPath, objectPath, true, out context, out error))
            {
                yield return CommandResult.Failure(error);
                yield break;
            }

            try
            {
                var go = context.GameObject;
                if (go == null)
                {
                    yield return CommandResult.Failure("Target does not contain components");
                    yield break;
                }

                var componentType = ResolveComponentType(typeName);
                if (componentType == null)
                {
                    yield return CommandResult.Failure($"Component type not found: {typeName}");
                    yield break;
                }
                if (!typeof(Component).IsAssignableFrom(componentType))
                {
                    yield return CommandResult.Failure($"Type is not a Component: {typeName}");
                    yield break;
                }

                Component newComponent;
                if (context.IsSceneObject)
                {
                    newComponent = Undo.AddComponent(go, componentType);
                }
                else
                {
                    newComponent = go.AddComponent(componentType);
                    SaveModifiedTarget(context, newComponent);
                }

                yield return CommandResult.Success(new
                {
                    gameObjectName = go.name,
                    assetPath = context.AssetPath,
                    objectPath = context.ObjectPath,
                    addedComponent = newComponent.GetType().Name,
                    instanceId = newComponent.GetInstanceID()
                });
            }
            finally
            {
                context.Dispose();
            }
        }

        [AIBridge("从 GameObject 移除组件",
            "AIBridgeCLI InspectorCommand_RemoveComponent --path \"Player\" --componentName \"Rigidbody\"")]
        public static IEnumerator RemoveComponent(
            [Description("GameObject 的层级路径")] string path = null,
            [Description("GameObject 的实例 ID")] int instanceId = 0,
            [Description("组件类型名称")] string componentName = null,
            [Description("组件索引")] int componentIndex = -1,
            [Description("组件的实例 ID")] int componentInstanceId = 0,
            [Description("资产路径（如 Assets/UI/Panel.prefab）")] string assetPath = null,
            [Description("Prefab 内对象路径")] string objectPath = null)
        {
            TargetContext context;
            string error;
            if (!TryResolveTargetContext(path, instanceId, assetPath, objectPath, true, out context, out error))
            {
                yield return CommandResult.Failure(error);
                yield break;
            }

            try
            {
                var go = context.GameObject;
                if (go == null)
                {
                    yield return CommandResult.Failure("Target does not contain components");
                    yield break;
                }

                Component component = null;
                if (componentInstanceId != 0 && context.IsSceneObject)
                    component = EditorUtility.InstanceIDToObject(componentInstanceId) as Component;
                else
                    component = FindComponent(go, componentName, componentIndex);

                if (component == null)
                {
                    yield return CommandResult.Failure("Component not found");
                    yield break;
                }
                if (component is Transform)
                {
                    yield return CommandResult.Failure("Cannot remove Transform component");
                    yield break;
                }

                var removedTypeName = component.GetType().Name;
                if (context.IsSceneObject)
                {
                    Undo.DestroyObjectImmediate(component);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(component, true);
                    SaveModifiedTarget(context, go);
                }

                yield return CommandResult.Success(new
                {
                    gameObjectName = go.name,
                    assetPath = context.AssetPath,
                    objectPath = context.ObjectPath,
                    removedComponent = removedTypeName
                });
            }
            finally
            {
                context.Dispose();
            }
        }

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

                if (Path.GetExtension(assetPath).Equals(PrefabExtension, StringComparison.OrdinalIgnoreCase))
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

        #region Property Value Read/Write

        private static PropInfo BuildPropertyInfo(SerializedProperty prop)
        {
            return new PropInfo
            {
                name = prop.name,
                propertyPath = prop.propertyPath,
                displayName = prop.displayName,
                propertyType = prop.propertyType.ToString(),
                value = GetPropertyValue(prop),
                editable = prop.editable,
                isExpanded = prop.isExpanded,
                hasChildren = prop.hasChildren,
                depth = prop.depth
            };
        }

        private static object GetPropertyValue(SerializedProperty prop)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer: return prop.intValue;
                case SerializedPropertyType.Boolean: return prop.boolValue;
                case SerializedPropertyType.Float: return prop.floatValue;
                case SerializedPropertyType.String: return prop.stringValue;
                case SerializedPropertyType.Color:
                    return new { r = prop.colorValue.r, g = prop.colorValue.g, b = prop.colorValue.b, a = prop.colorValue.a };
                case SerializedPropertyType.ObjectReference:
                    var objRef = prop.objectReferenceValue;
                    if (objRef == null) return null;
                    return new
                    {
                        name = objRef.name,
                        type = objRef.GetType().Name,
                        instanceId = objRef.GetInstanceID(),
                        assetPath = AssetDatabase.GetAssetPath(objRef)
                    };
                case SerializedPropertyType.Enum: return prop.enumNames[prop.enumValueIndex];
                case SerializedPropertyType.Vector2:
                    return new { x = prop.vector2Value.x, y = prop.vector2Value.y };
                case SerializedPropertyType.Vector3:
                    return new { x = prop.vector3Value.x, y = prop.vector3Value.y, z = prop.vector3Value.z };
                case SerializedPropertyType.Vector4:
                    return new { x = prop.vector4Value.x, y = prop.vector4Value.y, z = prop.vector4Value.z, w = prop.vector4Value.w };
                case SerializedPropertyType.Rect:
                    return new { x = prop.rectValue.x, y = prop.rectValue.y, width = prop.rectValue.width, height = prop.rectValue.height };
                case SerializedPropertyType.ArraySize: return prop.intValue;
                case SerializedPropertyType.Bounds:
                    return new
                    {
                        center = new { x = prop.boundsValue.center.x, y = prop.boundsValue.center.y, z = prop.boundsValue.center.z },
                        size = new { x = prop.boundsValue.size.x, y = prop.boundsValue.size.y, z = prop.boundsValue.size.z }
                    };
                case SerializedPropertyType.Quaternion:
                    return new { x = prop.quaternionValue.x, y = prop.quaternionValue.y, z = prop.quaternionValue.z, w = prop.quaternionValue.w };
                default: return prop.propertyType.ToString();
            }
        }

        private static bool SetPropertyValue(SerializedProperty prop, object value)
        {
            if (value == null && prop.propertyType != SerializedPropertyType.ObjectReference)
                return false;
            try
            {
                switch (prop.propertyType)
                {
                    case SerializedPropertyType.Integer:
                        prop.intValue = Convert.ToInt32(value, CultureInfo.InvariantCulture);
                        return true;
                    case SerializedPropertyType.Boolean:
                        prop.boolValue = Convert.ToBoolean(value, CultureInfo.InvariantCulture);
                        return true;
                    case SerializedPropertyType.Float:
                        if (!TryGetFloat(value, out var floatVal)) return false;
                        prop.floatValue = floatVal;
                        return true;
                    case SerializedPropertyType.String:
                        prop.stringValue = value != null ? value.ToString() : string.Empty;
                        return true;
                    case SerializedPropertyType.Enum:
                        return SetEnumValue(prop, value);
                    case SerializedPropertyType.ObjectReference:
                        prop.objectReferenceValue = ResolveObjectReference(value, prop);
                        return true;
                    case SerializedPropertyType.Vector2:
                        if (!TryGetVector2(value, out var v2)) return false;
                        prop.vector2Value = v2;
                        return true;
                    case SerializedPropertyType.Vector3:
                        if (!TryGetVector3(value, out var v3)) return false;
                        prop.vector3Value = v3;
                        return true;
                    case SerializedPropertyType.Vector4:
                        if (!TryGetVector4(value, out var v4)) return false;
                        prop.vector4Value = v4;
                        return true;
                    case SerializedPropertyType.Color:
                        if (!TryGetColor(value, out var color)) return false;
                        prop.colorValue = color;
                        return true;
                    case SerializedPropertyType.Rect:
                        if (!TryGetRect(value, out var rect)) return false;
                        prop.rectValue = rect;
                        return true;
                    case SerializedPropertyType.ArraySize:
                        prop.intValue = Convert.ToInt32(value, CultureInfo.InvariantCulture);
                        return true;
                    case SerializedPropertyType.Bounds:
                        if (!TryGetBounds(value, out var bounds)) return false;
                        prop.boundsValue = bounds;
                        return true;
                    case SerializedPropertyType.Quaternion:
                        if (!TryGetQuaternion(value, out var quat)) return false;
                        prop.quaternionValue = quat;
                        return true;
                    default: return false;
                }
            }
            catch { return false; }
        }

        private static bool SetEnumValue(SerializedProperty prop, object value)
        {
            if (TryGetInt(value, out var enumIndex))
            {
                if (enumIndex < 0 || enumIndex >= prop.enumNames.Length) return false;
                prop.enumValueIndex = enumIndex;
                return true;
            }
            var enumName = value != null ? value.ToString() : null;
            for (var i = 0; i < prop.enumNames.Length; i++)
            {
                if (prop.enumNames[i] == enumName) { prop.enumValueIndex = i; return true; }
            }
            return false;
        }

        private static UnityEngine.Object ResolveObjectReference(object value, SerializedProperty prop = null)
        {
            if (value == null) return null;

            if (value is double doubleId) { var id = (int)doubleId; return id != 0 ? EditorUtility.InstanceIDToObject(id) : null; }
            if (value is long longId) { var id = (int)longId; return id != 0 ? EditorUtility.InstanceIDToObject(id) : null; }
            if (value is int intId) { return intId != 0 ? EditorUtility.InstanceIDToObject(intId) : null; }

            var str = value.ToString();
            if (string.IsNullOrEmpty(str) || string.Equals(str, "null", StringComparison.OrdinalIgnoreCase))
                return null;

            var resolvedPath = str;
            if (!str.StartsWith(AssetsPathPrefix, StringComparison.OrdinalIgnoreCase)
                && !str.StartsWith(PackagesPathPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var guidPath = AssetDatabase.GUIDToAssetPath(str);
                if (!string.IsNullOrEmpty(guidPath))
                    resolvedPath = guidPath;
            }

            if (prop != null)
            {
                var expectedType = GetExpectedTypeFromProperty(prop);
                if (expectedType != null)
                {
                    var typed = AssetDatabase.LoadAssetAtPath(resolvedPath, expectedType);
                    if (typed != null) return typed;
                }
            }

            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(resolvedPath);
            if (asset != null) return asset;

            var go = GameObject.Find(str);
            if (go != null) return go;

            return null;
        }

        private static Type GetExpectedTypeFromProperty(SerializedProperty prop)
        {
            var typeName = prop.type;
            var start = typeName.IndexOf('<');
            var end = typeName.IndexOf('>');
            if (start < 0 || end <= start) return null;

            var inner = typeName.Substring(start + 1, end - start - 1);
            if (inner.StartsWith("$")) inner = inner.Substring(1);

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType($"UnityEngine.{inner}");
                if (type != null) return type;
                type = assembly.GetType($"UnityEngine.UI.{inner}");
                if (type != null) return type;
                type = assembly.GetType(inner);
                if (type != null) return type;
            }
            return null;
        }

        #endregion

        #region Type Conversion Helpers

        private static bool TryGetInt(object value, out int result)
        {
            result = 0;
            if (value == null) return false;
            if (value is int i) { result = i; return true; }
            if (value is long l) { result = (int)l; return true; }
            if (value is double d) { result = (int)d; return true; }
            return int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
        }

        private static bool TryGetFloat(object value, out float result)
        {
            result = 0f;
            if (value == null) return false;
            if (value is float f) { result = f; return true; }
            if (value is double d) { result = (float)d; return true; }
            if (value is long l) { result = l; return true; }
            if (value is int i) { result = i; return true; }
            return float.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out result);
        }

        private static bool TryGetVector2(object value, out Vector2 result)
        {
            result = Vector2.zero;
            if (!TryReadFloatList(value, 2, out var values)) return false;
            result = new Vector2(values[0], values[1]);
            return true;
        }

        private static bool TryGetVector3(object value, out Vector3 result)
        {
            result = Vector3.zero;
            if (!TryReadFloatList(value, 3, out var values)) return false;
            result = new Vector3(values[0], values[1], values[2]);
            return true;
        }

        private static bool TryGetVector4(object value, out Vector4 result)
        {
            result = Vector4.zero;
            if (!TryReadFloatList(value, 4, out var values)) return false;
            result = new Vector4(values[0], values[1], values[2], values[3]);
            return true;
        }

        private static bool TryGetColor(object value, out Color result)
        {
            result = Color.white;
            if (!TryReadFloatList(value, 4, out var values)) return false;
            result = new Color(values[0], values[1], values[2], values[3]);
            return true;
        }

        private static bool TryGetRect(object value, out Rect result)
        {
            result = Rect.zero;
            if (!TryReadFloatList(value, 4, out var values)) return false;
            result = new Rect(values[0], values[1], values[2], values[3]);
            return true;
        }

        private static bool TryGetQuaternion(object value, out Quaternion result)
        {
            result = Quaternion.identity;
            if (!TryReadFloatList(value, 4, out var values)) return false;
            result = new Quaternion(values[0], values[1], values[2], values[3]);
            return true;
        }

        private static bool TryGetBounds(object value, out Bounds result)
        {
            result = new Bounds();
            if (value is IDictionary dictionary)
            {
                var centerValue = GetDictionaryValue(dictionary, "center");
                var sizeValue = GetDictionaryValue(dictionary, "size");
                if (TryGetVector3(centerValue, out var center) && TryGetVector3(sizeValue, out var size))
                {
                    result = new Bounds(center, size);
                    return true;
                }
            }
            if (TryReadFloatList(value, 6, out var values))
            {
                result = new Bounds(
                    new Vector3(values[0], values[1], values[2]),
                    new Vector3(values[3], values[4], values[5]));
                return true;
            }
            return false;
        }

        private static bool TryReadFloatList(object value, int expectedCount, out float[] values)
        {
            values = null;
            var collected = new List<float>();

            if (value is IDictionary dictionary)
            {
                var keySets = GetFloatKeySets(expectedCount);
                for (var setIndex = 0; setIndex < keySets.Length; setIndex++)
                {
                    collected.Clear();
                    var keys = keySets[setIndex];
                    var success = true;
                    foreach (var key in keys)
                    {
                        if (!TryGetFloat(GetDictionaryValue(dictionary, key), out var number))
                        { success = false; break; }
                        collected.Add(number);
                    }
                    if (success) { values = collected.ToArray(); return true; }
                }
            }

            if (value is IList list)
            {
                if (list.Count != expectedCount) return false;
                for (var i = 0; i < list.Count; i++)
                {
                    if (!TryGetFloat(list[i], out var number)) return false;
                    collected.Add(number);
                }
                values = collected.ToArray();
                return true;
            }

            if (value is JObject jObj)
            {
                var keySets = GetFloatKeySets(expectedCount);
                for (var setIndex = 0; setIndex < keySets.Length; setIndex++)
                {
                    collected.Clear();
                    var keys = keySets[setIndex];
                    var success = true;
                    foreach (var key in keys)
                    {
                        var token = jObj[key];
                        if (token == null || !TryGetFloat(token.ToObject<object>(), out var number))
                        { success = false; break; }
                        collected.Add(number);
                    }
                    if (success) { values = collected.ToArray(); return true; }
                }
            }

            if (value is JArray jArr)
            {
                if (jArr.Count != expectedCount) return false;
                for (var i = 0; i < jArr.Count; i++)
                {
                    if (!TryGetFloat(jArr[i].ToObject<object>(), out var number)) return false;
                    collected.Add(number);
                }
                values = collected.ToArray();
                return true;
            }

            var text = value != null ? value.ToString() : null;
            if (!string.IsNullOrEmpty(text))
            {
                text = text.Trim().Trim('(', ')', '[', ']');
                var parts = text.Split(',');
                if (parts.Length != expectedCount) return false;
                for (var i = 0; i < parts.Length; i++)
                {
                    if (!TryGetFloat(parts[i].Trim(), out var number)) return false;
                    collected.Add(number);
                }
                values = collected.ToArray();
                return true;
            }

            return false;
        }

        private static string[][] GetFloatKeySets(int expectedCount)
        {
            if (expectedCount == 2) return new[] { new[] { "x", "y" } };
            if (expectedCount == 3) return new[] { new[] { "x", "y", "z" } };
            if (expectedCount == 4) return new[]
            {
                new[] { "x", "y", "z", "w" },
                new[] { "r", "g", "b", "a" },
                new[] { "x", "y", "width", "height" }
            };
            return new string[0][];
        }

        private static object GetDictionaryValue(IDictionary dictionary, string key)
        {
            if (dictionary == null || string.IsNullOrEmpty(key)) return null;
            foreach (DictionaryEntry entry in dictionary)
            {
                if (entry.Key != null && string.Equals(entry.Key.ToString(), key, StringComparison.OrdinalIgnoreCase))
                    return entry.Value;
            }
            return null;
        }

        #endregion

        #region Data Classes

        private sealed class TargetContext : IDisposable
        {
            public string AssetPath;
            public string ObjectPath;
            public GameObject PrefabRoot;
            public GameObject GameObject;
            public UnityEngine.Object SerializedTarget;
            public bool IsPrefabAsset;
            public bool IsAssetObject;
            public bool IsSceneObject;

            public void Dispose()
            {
                if (IsPrefabAsset && PrefabRoot != null)
                {
                    PrefabUtility.UnloadPrefabContents(PrefabRoot);
                    PrefabRoot = null;
                }
            }
        }

        [Serializable]
        private class ComponentInfo
        {
            public int index;
            public string typeName;
            public string fullTypeName;
            public int instanceId;
            public bool enabled = true;
        }

        [Serializable]
        private class PropInfo
        {
            public string name;
            public string propertyPath;
            public string displayName;
            public string propertyType;
            public object value;
            public bool editable;
            public bool isExpanded;
            public bool hasChildren;
            public int depth;
        }

        #endregion
    }
}
