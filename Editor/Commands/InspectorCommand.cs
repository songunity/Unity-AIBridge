using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEditor;
using UnityEngine;
using Component = UnityEngine.Component;

namespace AIBridge.Editor
{
    public static class InspectorCommand
    {
        [AIBridge("Get all components on a GameObject",
            "AIBridgeCLI InspectorCommand_GetComponents --path \"Player\"")]
        public static IEnumerator GetComponents(
            [Description("Hierarchy path of the GameObject")] string path = null,
            [Description("Instance ID of the GameObject")] int instanceId = 0)
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

        [AIBridge("Get serialized properties of a component",
            "AIBridgeCLI InspectorCommand_GetProperties --path \"Player\" --componentName \"Transform\"")]
        public static IEnumerator GetProperties(
            [Description("Hierarchy path of the GameObject")] string path = null,
            [Description("Instance ID of the GameObject")] int instanceId = 0,
            [Description("Component type name")] string componentName = null,
            [Description("Component index (alternative to componentName)")] int componentIndex = -1)
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
                enterChildren = false;
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

        [AIBridge("Set a serialized property on a component",
            "AIBridgeCLI InspectorCommand_SetProperty --path \"Player\" --componentName \"Rigidbody\" --propertyName \"mass\" --value 10")]
        public static IEnumerator SetProperty(
            [Description("Hierarchy path of the GameObject")] string path = null,
            [Description("Instance ID of the GameObject")] int instanceId = 0,
            [Description("Component type name")] string componentName = null,
            [Description("Component index (alternative to componentName)")] int componentIndex = -1,
            [Description("Serialized property name")] string propertyName = null,
            [Description("New value for the property")] string value = null)
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

        [AIBridge("Add a component to a GameObject",
            "AIBridgeCLI InspectorCommand_AddComponent --path \"Player\" --typeName \"Rigidbody\"")]
        public static IEnumerator AddComponent(
            [Description("Hierarchy path of the GameObject")] string path = null,
            [Description("Instance ID of the GameObject")] int instanceId = 0,
            [Description("Component type name (e.g. Rigidbody, BoxCollider)")] string typeName = null)
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

        [AIBridge("Remove a component from a GameObject",
            "AIBridgeCLI InspectorCommand_RemoveComponent --path \"Player\" --componentName \"Rigidbody\"")]
        public static IEnumerator RemoveComponent(
            [Description("Hierarchy path of the GameObject")] string path = null,
            [Description("Instance ID of the GameObject")] int instanceId = 0,
            [Description("Component type name")] string componentName = null,
            [Description("Component index")] int componentIndex = -1,
            [Description("Instance ID of the component")] int componentInstanceId = 0)
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
