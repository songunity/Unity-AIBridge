using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Component = UnityEngine.Component;

namespace AIBridge.Editor
{
    public static partial class InspectorCommand
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
            string parseError = null;
            try
            {
                var jObj = JObject.Parse(values);
                valueDict = new Dictionary<string, object>();
                foreach (var pair in jObj)
                    valueDict[pair.Key] = pair.Value.Type == JTokenType.Null ? null : pair.Value;
            }
            catch (Exception ex)
            {
                valueDict = null;
                parseError = ex.Message;
            }

            if (parseError != null)
            {
                yield return CommandResult.Failure($"Failed to parse 'values' JSON: {parseError}");
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
    }
}
