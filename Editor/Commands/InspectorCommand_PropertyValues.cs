using System;
using UnityEditor;
using UnityEngine;

namespace AIBridge.Editor
{
    public static partial class InspectorCommand
    {
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
                case SerializedPropertyType.AnimationCurve:
                    var curve = prop.animationCurveValue;
                    var curveKeys = new object[curve.length];
                    for (var i = 0; i < curve.length; i++)
                    {
                        var k = curve[i];
                        curveKeys[i] = new { time = k.time, value = k.value, inTangent = k.inTangent, outTangent = k.outTangent, inWeight = k.inWeight, outWeight = k.outWeight, weightedMode = k.weightedMode.ToString() };
                    }
                    return new { keys = curveKeys, preWrapMode = curve.preWrapMode.ToString(), postWrapMode = curve.postWrapMode.ToString() };
                case SerializedPropertyType.Gradient:
                    var grad = prop.gradientValue;
                    var colorKeys = new object[grad.colorKeys.Length];
                    for (var i = 0; i < grad.colorKeys.Length; i++)
                    {
                        var ck = grad.colorKeys[i];
                        colorKeys[i] = new { time = ck.time, color = new { r = ck.color.r, g = ck.color.g, b = ck.color.b, a = ck.color.a } };
                    }
                    var alphaKeys = new object[grad.alphaKeys.Length];
                    for (var i = 0; i < grad.alphaKeys.Length; i++)
                    {
                        var ak = grad.alphaKeys[i];
                        alphaKeys[i] = new { time = ak.time, alpha = ak.alpha };
                    }
                    return new { colorKeys, alphaKeys, mode = grad.mode.ToString() };
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
                        prop.intValue = Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
                        return true;
                    case SerializedPropertyType.Boolean:
                        prop.boolValue = Convert.ToBoolean(value, System.Globalization.CultureInfo.InvariantCulture);
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
                        prop.intValue = Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
                        return true;
                    case SerializedPropertyType.Bounds:
                        if (!TryGetBounds(value, out var bounds)) return false;
                        prop.boundsValue = bounds;
                        return true;
                    case SerializedPropertyType.Quaternion:
                        if (!TryGetQuaternion(value, out var quat)) return false;
                        prop.quaternionValue = quat;
                        return true;
                    case SerializedPropertyType.AnimationCurve:
                        if (!TryGetAnimationCurve(value, out var animCurve)) return false;
                        prop.animationCurveValue = animCurve;
                        return true;
                    case SerializedPropertyType.Gradient:
                        if (!TryGetGradient(value, out var gradient)) return false;
                        prop.gradientValue = gradient;
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
    }
}
