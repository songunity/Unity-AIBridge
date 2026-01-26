using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace AIBridge.Editor
{
    /// <summary>
    /// Inspector operations: get_properties, set_property, get_components
    /// Supports multiple sub-commands via "action" parameter
    /// </summary>
    public class InspectorCommand : ICommand
    {
        public string Type => "inspector";
        public bool RequiresRefresh => true;

        public CommandResult Execute(CommandRequest request)
        {
            var action = request.GetParam("action", "get_components");

            try
            {
                switch (action.ToLower())
                {
                    case "get_components":
                        return GetComponents(request);
                    case "get_properties":
                        return GetProperties(request);
                    case "set_property":
                        return SetProperty(request);
                    case "add_component":
                        return AddComponent(request);
                    case "remove_component":
                        return RemoveComponent(request);
                    default:
                        return CommandResult.Failure(request.id, $"Unknown action: {action}. Supported: get_components, get_properties, set_property, add_component, remove_component");
                }
            }
            catch (Exception ex)
            {
                return CommandResult.FromException(request.id, ex);
            }
        }

        private CommandResult GetComponents(CommandRequest request)
        {
            var go = GetTargetGameObject(request);
            if (go == null)
            {
                return CommandResult.Failure(request.id, "GameObject not found");
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

                // Check if it's enabled (for Behaviour types)
                if (comp is Behaviour behaviour)
                {
                    info.enabled = behaviour.enabled;
                }

                components.Add(info);
            }

            return CommandResult.Success(request.id, new
            {
                gameObjectName = go.name,
                components = components,
                count = components.Count
            });
        }

        private CommandResult GetProperties(CommandRequest request)
        {
            var go = GetTargetGameObject(request);
            if (go == null)
            {
                return CommandResult.Failure(request.id, "GameObject not found");
            }

            var componentName = request.GetParam<string>("componentName", null);
            var componentIndex = request.GetParam("componentIndex", -1);

            Component component = null;

            if (componentIndex >= 0)
            {
                var components = go.GetComponents<Component>();
                if (componentIndex < components.Length)
                {
                    component = components[componentIndex];
                }
            }
            else if (!string.IsNullOrEmpty(componentName))
            {
                foreach (var comp in go.GetComponents<Component>())
                {
                    if (comp != null && (comp.GetType().Name == componentName || comp.GetType().FullName == componentName))
                    {
                        component = comp;
                        break;
                    }
                }
            }

            if (component == null)
            {
                return CommandResult.Failure(request.id, "Component not found. Provide 'componentName' or 'componentIndex'");
            }

            var properties = new List<PropertyInfo>();
            var so = new SerializedObject(component);
            var iterator = so.GetIterator();
            var enterChildren = true;

            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;

                var propInfo = new PropertyInfo
                {
                    name = iterator.name,
                    displayName = iterator.displayName,
                    propertyType = iterator.propertyType.ToString(),
                    editable = iterator.editable,
                    isExpanded = iterator.isExpanded,
                    hasChildren = iterator.hasChildren,
                    depth = iterator.depth
                };

                // Get value based on type
                propInfo.value = GetPropertyValue(iterator);

                properties.Add(propInfo);
            }

            return CommandResult.Success(request.id, new
            {
                gameObjectName = go.name,
                componentName = component.GetType().Name,
                properties = properties
            });
        }

        private CommandResult SetProperty(CommandRequest request)
        {
            var go = GetTargetGameObject(request);
            if (go == null)
            {
                return CommandResult.Failure(request.id, "GameObject not found");
            }

            var componentName = request.GetParam<string>("componentName", null);
            var componentIndex = request.GetParam("componentIndex", -1);
            var propertyName = request.GetParam<string>("propertyName");
            var value = request.GetParam<object>("value", null);

            if (string.IsNullOrEmpty(propertyName))
            {
                return CommandResult.Failure(request.id, "Missing 'propertyName' parameter");
            }

            Component component = null;

            if (componentIndex >= 0)
            {
                var components = go.GetComponents<Component>();
                if (componentIndex < components.Length)
                {
                    component = components[componentIndex];
                }
            }
            else if (!string.IsNullOrEmpty(componentName))
            {
                foreach (var comp in go.GetComponents<Component>())
                {
                    if (comp != null && (comp.GetType().Name == componentName || comp.GetType().FullName == componentName))
                    {
                        component = comp;
                        break;
                    }
                }
            }

            if (component == null)
            {
                return CommandResult.Failure(request.id, "Component not found");
            }

            var so = new SerializedObject(component);
            var prop = so.FindProperty(propertyName);

            if (prop == null)
            {
                return CommandResult.Failure(request.id, $"Property not found: {propertyName}");
            }

            UnityEditor.Undo.RecordObject(component, $"Set Property {propertyName}");

            var success = SetPropertyValue(prop, value);
            if (!success)
            {
                return CommandResult.Failure(request.id, $"Failed to set property value for type: {prop.propertyType}");
            }

            so.ApplyModifiedProperties();

            return CommandResult.Success(request.id, new
            {
                gameObjectName = go.name,
                componentName = component.GetType().Name,
                propertyName = propertyName,
                newValue = GetPropertyValue(prop)
            });
        }

        private CommandResult AddComponent(CommandRequest request)
        {
            var go = GetTargetGameObject(request);
            if (go == null)
            {
                return CommandResult.Failure(request.id, "GameObject not found");
            }

            var typeName = request.GetParam<string>("typeName");
            if (string.IsNullOrEmpty(typeName))
            {
                return CommandResult.Failure(request.id, "Missing 'typeName' parameter");
            }

            // Try to find the type
            System.Type componentType = null;

            // Try common Unity namespaces first
            var possibleNames = new[]
            {
                typeName,
                $"UnityEngine.{typeName}",
                $"UnityEngine.UI.{typeName}",
                $"TMPro.{typeName}"
            };

            foreach (var name in possibleNames)
            {
                componentType = System.Type.GetType(name);
                if (componentType != null) break;

                // Search in all assemblies
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    componentType = assembly.GetType(name);
                    if (componentType != null) break;
                }

                if (componentType != null) break;
            }

            if (componentType == null)
            {
                return CommandResult.Failure(request.id, $"Component type not found: {typeName}");
            }

            if (!typeof(Component).IsAssignableFrom(componentType))
            {
                return CommandResult.Failure(request.id, $"Type is not a Component: {typeName}");
            }

            var newComponent = UnityEditor.Undo.AddComponent(go, componentType);

            return CommandResult.Success(request.id, new
            {
                gameObjectName = go.name,
                addedComponent = newComponent.GetType().Name,
                instanceId = newComponent.GetInstanceID()
            });
        }

        private CommandResult RemoveComponent(CommandRequest request)
        {
            var go = GetTargetGameObject(request);
            if (go == null)
            {
                return CommandResult.Failure(request.id, "GameObject not found");
            }

            var componentName = request.GetParam<string>("componentName", null);
            var componentIndex = request.GetParam("componentIndex", -1);
            var componentInstanceId = request.GetParam("componentInstanceId", 0);

            Component component = null;

            if (componentInstanceId != 0)
            {
                component = EditorUtility.InstanceIDToObject(componentInstanceId) as Component;
            }
            else if (componentIndex >= 0)
            {
                var components = go.GetComponents<Component>();
                if (componentIndex < components.Length)
                {
                    component = components[componentIndex];
                }
            }
            else if (!string.IsNullOrEmpty(componentName))
            {
                foreach (var comp in go.GetComponents<Component>())
                {
                    if (comp != null && (comp.GetType().Name == componentName || comp.GetType().FullName == componentName))
                    {
                        component = comp;
                        break;
                    }
                }
            }

            if (component == null)
            {
                return CommandResult.Failure(request.id, "Component not found");
            }

            // Prevent removing Transform
            if (component is Transform)
            {
                return CommandResult.Failure(request.id, "Cannot remove Transform component");
            }

            var removedTypeName = component.GetType().Name;
            UnityEditor.Undo.DestroyObjectImmediate(component);

            return CommandResult.Success(request.id, new
            {
                gameObjectName = go.name,
                removedComponent = removedTypeName
            });
        }

        private GameObject GetTargetGameObject(CommandRequest request)
        {
            var path = request.GetParam<string>("path", null);
            var instanceId = request.GetParam("instanceId", 0);

            if (instanceId != 0)
            {
                return EditorUtility.InstanceIDToObject(instanceId) as GameObject;
            }

            if (!string.IsNullOrEmpty(path))
            {
                return GameObject.Find(path);
            }

            return Selection.activeGameObject;
        }

        private object GetPropertyValue(SerializedProperty prop)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                    return prop.intValue;
                case SerializedPropertyType.Boolean:
                    return prop.boolValue;
                case SerializedPropertyType.Float:
                    return prop.floatValue;
                case SerializedPropertyType.String:
                    return prop.stringValue;
                case SerializedPropertyType.Color:
                    return $"({prop.colorValue.r}, {prop.colorValue.g}, {prop.colorValue.b}, {prop.colorValue.a})";
                case SerializedPropertyType.ObjectReference:
                    return prop.objectReferenceValue?.name;
                case SerializedPropertyType.Enum:
                    return prop.enumNames[prop.enumValueIndex];
                case SerializedPropertyType.Vector2:
                    return $"({prop.vector2Value.x}, {prop.vector2Value.y})";
                case SerializedPropertyType.Vector3:
                    return $"({prop.vector3Value.x}, {prop.vector3Value.y}, {prop.vector3Value.z})";
                case SerializedPropertyType.Vector4:
                    return $"({prop.vector4Value.x}, {prop.vector4Value.y}, {prop.vector4Value.z}, {prop.vector4Value.w})";
                case SerializedPropertyType.Rect:
                    return $"({prop.rectValue.x}, {prop.rectValue.y}, {prop.rectValue.width}, {prop.rectValue.height})";
                case SerializedPropertyType.ArraySize:
                    return prop.intValue;
                case SerializedPropertyType.Bounds:
                    return $"Center: {prop.boundsValue.center}, Size: {prop.boundsValue.size}";
                case SerializedPropertyType.Quaternion:
                    return $"({prop.quaternionValue.x}, {prop.quaternionValue.y}, {prop.quaternionValue.z}, {prop.quaternionValue.w})";
                default:
                    return prop.propertyType.ToString();
            }
        }

        private bool SetPropertyValue(SerializedProperty prop, object value)
        {
            if (value == null) return false;

            try
            {
                switch (prop.propertyType)
                {
                    case SerializedPropertyType.Integer:
                        prop.intValue = Convert.ToInt32(value);
                        return true;
                    case SerializedPropertyType.Boolean:
                        prop.boolValue = Convert.ToBoolean(value);
                        return true;
                    case SerializedPropertyType.Float:
                        prop.floatValue = Convert.ToSingle(value);
                        return true;
                    case SerializedPropertyType.String:
                        prop.stringValue = value.ToString();
                        return true;
                    case SerializedPropertyType.Enum:
                        if (value is double dVal)
                        {
                            prop.enumValueIndex = (int)dVal;
                        }
                        else if (value is int intVal)
                        {
                            prop.enumValueIndex = intVal;
                        }
                        else
                        {
                            // Try to find by name
                            var enumName = value.ToString();
                            for (var i = 0; i < prop.enumNames.Length; i++)
                            {
                                if (prop.enumNames[i] == enumName)
                                {
                                    prop.enumValueIndex = i;
                                    return true;
                                }
                            }
                        }
                        return true;
                    default:
                        return false;
                }
            }
            catch
            {
                return false;
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
        private class PropertyInfo
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
