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
    public static class InspectorCommand
    {
        [AIBridge("获取 GameObject 上的所有组件",
            "AIBridgeCLI InspectorCommand_GetComponents --path \"Player\"")]
        public static IEnumerator GetComponents(
            [Description("GameObject 的层级路径")] string path = null,
            [Description("GameObject 的实例 ID")] int instanceId = 0)
        {
            var go = GameObjectHelper.GetTargetGameObject(path, instanceId);
            if (go == null)
            {
                yield return CommandResult.Failure("GameObject not found");
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
                components,
                count = components.Count
            });
        }

        [AIBridge("获取组件的序列化属性",
            "AIBridgeCLI InspectorCommand_GetProperties --path \"Player\" --componentName \"Transform\"")]
        public static IEnumerator GetProperties(
            [Description("GameObject 的层级路径")] string path = null,
            [Description("GameObject 的实例 ID")] int instanceId = 0,
            [Description("组件类型名称")] string componentName = null,
            [Description("组件索引（替代 componentName）")] int componentIndex = -1,
            [Description("是否展开子属性")] bool includeChildren = false)
        {
            var go = GameObjectHelper.GetTargetGameObject(path, instanceId);
            if (go == null)
            {
                yield return CommandResult.Failure("GameObject not found");
                yield break;
            }

            var component = FindComponent(go, componentName, componentIndex);
            if (component == null)
            {
                yield return CommandResult.Failure("Component not found. Provide 'componentName' or 'componentIndex'");
                yield break;
            }

            var properties = new List<PropInfo>();
            var so = new SerializedObject(component);
            var iterator = so.GetIterator();
            var enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = includeChildren;
                properties.Add(new PropInfo
                {
                    name = iterator.name,
                    displayName = iterator.displayName,
                    propertyType = iterator.propertyType.ToString(),
                    value = GetPropertyValue(iterator),
                    editable = iterator.editable,
                    isExpanded = iterator.isExpanded,
                    hasChildren = iterator.hasChildren,
                    depth = iterator.depth
                });
            }

            yield return CommandResult.Success(new
            {
                gameObjectName = go.name,
                componentName = component.GetType().Name,
                properties
            });
        }

        [AIBridge("获取组件的单个序列化属性值",
            "AIBridgeCLI InspectorCommand_GetProperty --path \"Player\" --componentName \"Rigidbody\" --propertyName \"mass\"")]
        public static IEnumerator GetProperty(
            [Description("GameObject 的层级路径")] string path = null,
            [Description("GameObject 的实例 ID")] int instanceId = 0,
            [Description("组件类型名称")] string componentName = null,
            [Description("组件索引（替代 componentName）")] int componentIndex = -1,
            [Description("序列化属性名称")] string propertyName = null)
        {
            var go = GameObjectHelper.GetTargetGameObject(path, instanceId);
            if (go == null)
            {
                yield return CommandResult.Failure("GameObject not found");
                yield break;
            }
            if (string.IsNullOrEmpty(propertyName))
            {
                yield return CommandResult.Failure("Missing 'propertyName' parameter");
                yield break;
            }

            var component = FindComponent(go, componentName, componentIndex);
            if (component == null)
            {
                yield return CommandResult.Failure("Component not found");
                yield break;
            }

            var so = new SerializedObject(component);
            var prop = so.FindProperty(propertyName);
            if (prop == null)
            {
                yield return CommandResult.Failure($"Property not found: {propertyName}");
                yield break;
            }

            yield return CommandResult.Success(new
            {
                gameObjectName = go.name,
                componentName = component.GetType().Name,
                propertyName,
                propertyType = prop.propertyType.ToString(),
                value = GetPropertyValue(prop),
                editable = prop.editable
            });
        }

        [AIBridge("按关键字搜索组件的序列化属性",
            "AIBridgeCLI InspectorCommand_FindProperty --path \"Player\" --componentName \"Rigidbody\" --keyword \"mass\"")]
        public static IEnumerator FindProperty(
            [Description("GameObject 的层级路径")] string path = null,
            [Description("GameObject 的实例 ID")] int instanceId = 0,
            [Description("组件类型名称")] string componentName = null,
            [Description("组件索引（替代 componentName）")] int componentIndex = -1,
            [Description("搜索关键字")] string keyword = null)
        {
            var go = GameObjectHelper.GetTargetGameObject(path, instanceId);
            if (go == null)
            {
                yield return CommandResult.Failure("GameObject not found");
                yield break;
            }
            if (string.IsNullOrEmpty(keyword))
            {
                yield return CommandResult.Failure("Missing 'keyword' parameter");
                yield break;
            }

            var component = FindComponent(go, componentName, componentIndex);
            if (component == null)
            {
                yield return CommandResult.Failure("Component not found");
                yield break;
            }

            var matches = new List<PropInfo>();
            var so = new SerializedObject(component);
            var iterator = so.GetIterator();
            while (iterator.NextVisible(true))
            {
                if (iterator.propertyPath.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) < 0
                    && iterator.name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) < 0
                    && iterator.displayName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                matches.Add(new PropInfo
                {
                    name = iterator.propertyPath,
                    displayName = iterator.displayName,
                    propertyType = iterator.propertyType.ToString(),
                    value = GetPropertyValue(iterator),
                    editable = iterator.editable,
                    hasChildren = iterator.hasChildren,
                    depth = iterator.depth
                });
            }

            yield return CommandResult.Success(new
            {
                gameObjectName = go.name,
                componentName = component.GetType().Name,
                keyword,
                count = matches.Count,
                matches
            });
        }

        [AIBridge("设置组件上的序列化属性",
            "AIBridgeCLI InspectorCommand_SetProperty --path \"Player\" --componentName \"Rigidbody\" --propertyName \"mass\" --value 10")]
        public static IEnumerator SetProperty(
            [Description("GameObject 的层级路径")] string path = null,
            [Description("GameObject 的实例 ID")] int instanceId = 0,
            [Description("组件类型名称")] string componentName = null,
            [Description("组件索引（替代 componentName）")] int componentIndex = -1,
            [Description("序列化属性名称")] string propertyName = null,
            [Description("属性的新值")] string value = null)
        {
            var go = GameObjectHelper.GetTargetGameObject(path, instanceId);
            if (go == null)
            {
                yield return CommandResult.Failure("GameObject not found");
                yield break;
            }
            if (string.IsNullOrEmpty(propertyName))
            {
                yield return CommandResult.Failure("Missing 'propertyName' parameter");
                yield break;
            }

            var component = FindComponent(go, componentName, componentIndex);
            if (component == null)
            {
                yield return CommandResult.Failure("Component not found");
                yield break;
            }

            var so = new SerializedObject(component);
            var prop = so.FindProperty(propertyName);
            if (prop == null)
            {
                yield return CommandResult.Failure($"Property not found: {propertyName}");
                yield break;
            }

            UnityEditor.Undo.RecordObject(component, $"Set Property {propertyName}");
            if (!SetPropertyValue(prop, value))
            {
                yield return CommandResult.Failure($"Failed to set property of type: {prop.propertyType}");
                yield break;
            }
            so.ApplyModifiedProperties();

            yield return CommandResult.Success(new
            {
                gameObjectName = go.name,
                componentName = component.GetType().Name,
                propertyName,
                newValue = GetPropertyValue(prop)
            });
        }

        [AIBridge("批量设置组件上的多个序列化属性",
            "AIBridgeCLI InspectorCommand_SetProperties --path \"Player\" --componentName \"Transform\" --json \"{\\\"values\\\":{\\\"m_LocalPosition.x\\\":1,\\\"m_LocalPosition.y\\\":2}}\"")]
        public static IEnumerator SetProperties(
            [Description("GameObject 的层级路径")] string path = null,
            [Description("GameObject 的实例 ID")] int instanceId = 0,
            [Description("组件类型名称")] string componentName = null,
            [Description("组件索引（替代 componentName）")] int componentIndex = -1,
            [Description("属性名到值的映射 JSON 字符串")] string values = null)
        {
            var go = GameObjectHelper.GetTargetGameObject(path, instanceId);
            if (go == null)
            {
                yield return CommandResult.Failure("GameObject not found");
                yield break;
            }
            if (string.IsNullOrEmpty(values))
            {
                yield return CommandResult.Failure("Missing 'values' parameter");
                yield break;
            }

            var component = FindComponent(go, componentName, componentIndex);
            if (component == null)
            {
                yield return CommandResult.Failure("Component not found");
                yield break;
            }

            Dictionary<string, object> valueDict;
            try
            {
                var jObj = JObject.Parse(values);
                valueDict = new Dictionary<string, object>();
                foreach (var pair in jObj)
                {
                    valueDict[pair.Key] = pair.Value.Type == JTokenType.Null ? null : ((JValue)pair.Value).Value;
                }
            }
            catch (Exception ex)
            {
                yield return CommandResult.Failure($"Failed to parse 'values' JSON: {ex.Message}");
                yield break;
            }

            var so = new SerializedObject(component);
            so.Update();
            UnityEditor.Undo.RecordObject(component, "Set Properties");

            var changes = new List<object>();
            foreach (var pair in valueDict)
            {
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
                    yield return CommandResult.Failure($"Failed to set property '{pair.Key}' of type: {prop.propertyType}");
                    yield break;
                }
                changes.Add(new { propertyName = pair.Key, oldValue, newValue = GetPropertyValue(prop) });
            }

            so.ApplyModifiedProperties();

            yield return CommandResult.Success(new
            {
                gameObjectName = go.name,
                componentName = component.GetType().Name,
                changed = changes.Count > 0,
                changes
            });
        }

        [AIBridge("向 GameObject 添加组件",
            "AIBridgeCLI InspectorCommand_AddComponent --path \"Player\" --typeName \"Rigidbody\"")]
        public static IEnumerator AddComponent(
            [Description("GameObject 的层级路径")] string path = null,
            [Description("GameObject 的实例 ID")] int instanceId = 0,
            [Description("组件类型名称（例如 Rigidbody, BoxCollider）")] string typeName = null)
        {
            var go = GameObjectHelper.GetTargetGameObject(path, instanceId);
            if (go == null)
            {
                yield return CommandResult.Failure("GameObject not found");
                yield break;
            }
            if (string.IsNullOrEmpty(typeName))
            {
                yield return CommandResult.Failure("Missing 'typeName' parameter");
                yield break;
            }

            System.Type componentType = null;
            var possibleNames = new[] { typeName, $"UnityEngine.{typeName}", $"UnityEngine.UI.{typeName}", $"TMPro.{typeName}" };
            foreach (var candidateName in possibleNames)
            {
                componentType = System.Type.GetType(candidateName);
                if (componentType != null) break;
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    componentType = assembly.GetType(candidateName);
                    if (componentType != null) break;
                }
                if (componentType != null) break;
            }

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

            var newComponent = UnityEditor.Undo.AddComponent(go, componentType);
            yield return CommandResult.Success(new
            {
                gameObjectName = go.name,
                addedComponent = newComponent.GetType().Name,
                instanceId = newComponent.GetInstanceID()
            });
        }

        [AIBridge("从 GameObject 移除组件",
            "AIBridgeCLI InspectorCommand_RemoveComponent --path \"Player\" --componentName \"Rigidbody\"")]
        public static IEnumerator RemoveComponent(
            [Description("GameObject 的层级路径")] string path = null,
            [Description("GameObject 的实例 ID")] int instanceId = 0,
            [Description("组件类型名称")] string componentName = null,
            [Description("组件索引")] int componentIndex = -1,
            [Description("组件的实例 ID")] int componentInstanceId = 0)
        {
            var go = GameObjectHelper.GetTargetGameObject(path, instanceId);
            if (go == null)
            {
                yield return CommandResult.Failure("GameObject not found");
                yield break;
            }

            Component component = null;
            if (componentInstanceId != 0)
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
            UnityEditor.Undo.DestroyObjectImmediate(component);

            yield return CommandResult.Success(new
            {
                gameObjectName = go.name,
                removedComponent = removedTypeName
            });
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

        private static object GetPropertyValue(SerializedProperty prop)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer: return prop.intValue;
                case SerializedPropertyType.Boolean: return prop.boolValue;
                case SerializedPropertyType.Float: return prop.floatValue;
                case SerializedPropertyType.String: return prop.stringValue;
                case SerializedPropertyType.Color: return $"({prop.colorValue.r}, {prop.colorValue.g}, {prop.colorValue.b}, {prop.colorValue.a})";
                case SerializedPropertyType.ObjectReference: return prop.objectReferenceValue?.name;
                case SerializedPropertyType.Enum: return prop.enumNames[prop.enumValueIndex];
                case SerializedPropertyType.Vector2: return $"({prop.vector2Value.x}, {prop.vector2Value.y})";
                case SerializedPropertyType.Vector3: return $"({prop.vector3Value.x}, {prop.vector3Value.y}, {prop.vector3Value.z})";
                case SerializedPropertyType.Vector4: return $"({prop.vector4Value.x}, {prop.vector4Value.y}, {prop.vector4Value.z}, {prop.vector4Value.w})";
                case SerializedPropertyType.Rect: return $"({prop.rectValue.x}, {prop.rectValue.y}, {prop.rectValue.width}, {prop.rectValue.height})";
                case SerializedPropertyType.ArraySize: return prop.intValue;
                case SerializedPropertyType.Bounds: return $"Center: {prop.boundsValue.center}, Size: {prop.boundsValue.size}";
                case SerializedPropertyType.Quaternion: return $"({prop.quaternionValue.x}, {prop.quaternionValue.y}, {prop.quaternionValue.z}, {prop.quaternionValue.w})";
                default: return prop.propertyType.ToString();
            }
        }

        private static bool SetPropertyValue(SerializedProperty prop, object value)
        {
            if (value == null) return false;
            try
            {
                switch (prop.propertyType)
                {
                    case SerializedPropertyType.Integer: prop.intValue = Convert.ToInt32(value); return true;
                    case SerializedPropertyType.Boolean: prop.boolValue = Convert.ToBoolean(value); return true;
                    case SerializedPropertyType.Float: prop.floatValue = Convert.ToSingle(value); return true;
                    case SerializedPropertyType.String: prop.stringValue = value.ToString(); return true;
                    case SerializedPropertyType.Enum:
                        if (value is double dVal) prop.enumValueIndex = (int)dVal;
                        else if (value is int iVal) prop.enumValueIndex = iVal;
                        else
                        {
                            var enumName = value.ToString();
                            for (var i = 0; i < prop.enumNames.Length; i++)
                            {
                                if (prop.enumNames[i] == enumName) { prop.enumValueIndex = i; return true; }
                            }
                        }
                        return true;
                    default: return false;
                }
            }
            catch { return false; }
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
            public string displayName;
            public string propertyType;
            public object value;
            public bool editable;
            public bool isExpanded;
            public bool hasChildren;
            public int depth;
        }
    }
}
