using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace AIBridge.Editor
{
    public static partial class InspectorCommand
    {
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
