using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AIBridge.Editor
{
    /// <summary>
    /// Component type resolver with fuzzy matching support.
    /// Allows using short type names instead of fully qualified names.
    /// </summary>
    public static class ComponentTypeResolver
    {
        // Common component namespace mappings
        private static readonly Dictionary<string, string> CommonComponentMap = new Dictionary<string, string>
        {
            // Unity UI
            { "Button", "UnityEngine.UI.Button" },
            { "Image", "UnityEngine.UI.Image" },
            { "RawImage", "UnityEngine.UI.RawImage" },
            { "Text", "UnityEngine.UI.Text" },
            { "InputField", "UnityEngine.UI.InputField" },
            { "Toggle", "UnityEngine.UI.Toggle" },
            { "Slider", "UnityEngine.UI.Slider" },
            { "Scrollbar", "UnityEngine.UI.Scrollbar" },
            { "ScrollRect", "UnityEngine.UI.ScrollRect" },
            { "Dropdown", "UnityEngine.UI.Dropdown" },
            { "Mask", "UnityEngine.UI.Mask" },
            { "RectMask2D", "UnityEngine.UI.RectMask2D" },
            { "LayoutGroup", "UnityEngine.UI.LayoutGroup" },
            { "HorizontalLayoutGroup", "UnityEngine.UI.HorizontalLayoutGroup" },
            { "VerticalLayoutGroup", "UnityEngine.UI.VerticalLayoutGroup" },
            { "GridLayoutGroup", "UnityEngine.UI.GridLayoutGroup" },
            { "ContentSizeFitter", "UnityEngine.UI.ContentSizeFitter" },
            { "AspectRatioFitter", "UnityEngine.UI.AspectRatioFitter" },

            // TextMeshPro
            { "TextMeshProUGUI", "TMPro.TextMeshProUGUI" },
            { "TMP_Text", "TMPro.TMP_Text" },
            { "TMP_InputField", "TMPro.TMP_InputField" },
            { "TMP_Dropdown", "TMPro.TMP_Dropdown" },

            // Common Unity Components
            { "RectTransform", "UnityEngine.RectTransform" },
            { "CanvasGroup", "UnityEngine.CanvasGroup" },
            { "Canvas", "UnityEngine.Canvas" },
            { "CanvasRenderer", "UnityEngine.CanvasRenderer" },
            { "Animator", "UnityEngine.Animator" },
            { "Animation", "UnityEngine.Animation" },
            { "AudioSource", "UnityEngine.AudioSource" },
            { "AudioListener", "UnityEngine.AudioListener" },
            { "Camera", "UnityEngine.Camera" },
            { "Light", "UnityEngine.Light" },
            { "MeshRenderer", "UnityEngine.MeshRenderer" },
            { "MeshFilter", "UnityEngine.MeshFilter" },
            { "SkinnedMeshRenderer", "UnityEngine.SkinnedMeshRenderer" },
            { "SpriteRenderer", "UnityEngine.SpriteRenderer" },
            { "ParticleSystem", "UnityEngine.ParticleSystem" },
            { "Rigidbody", "UnityEngine.Rigidbody" },
            { "Rigidbody2D", "UnityEngine.Rigidbody2D" },
            { "BoxCollider", "UnityEngine.BoxCollider" },
            { "BoxCollider2D", "UnityEngine.BoxCollider2D" },
            { "SphereCollider", "UnityEngine.SphereCollider" },
            { "CapsuleCollider", "UnityEngine.CapsuleCollider" },
            { "CircleCollider2D", "UnityEngine.CircleCollider2D" },
            { "CharacterController", "UnityEngine.CharacterController" },
            { "LineRenderer", "UnityEngine.LineRenderer" },
            { "TrailRenderer", "UnityEngine.TrailRenderer" },
        };

        /// <summary>
        /// Resolve component type name to Type.
        /// Supports short names (e.g., "Button") and full names (e.g., "UnityEngine.UI.Button").
        /// </summary>
        /// <param name="typeName">Type name (short or full)</param>
        /// <returns>Resolved Type, or null if not found</returns>
        public static Type Resolve(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return null;

            // 1. Try direct lookup (for full type names)
            var type = Type.GetType(typeName);
            if (type != null)
                return type;

            // 2. Check common component mappings
            if (CommonComponentMap.TryGetValue(typeName, out var fullName))
            {
                type = FindTypeInAllAssemblies(fullName);
                if (type != null)
                    return type;
            }

            // 3. Search in all assemblies by full name
            type = FindTypeInAllAssemblies(typeName);
            if (type != null)
                return type;

            // 4. Fuzzy match by class name only
            type = FindTypeByClassName(typeName);

            return type;
        }

        /// <summary>
        /// Get all registered common component short names.
        /// </summary>
        public static string[] GetCommonComponentNames()
        {
            return CommonComponentMap.Keys.ToArray();
        }

        /// <summary>
        /// Check if a type name is in the common components list.
        /// </summary>
        public static bool IsCommonComponent(string typeName)
        {
            return CommonComponentMap.ContainsKey(typeName);
        }

        /// <summary>
        /// Get full type name for a common component.
        /// </summary>
        public static string GetFullTypeName(string shortName)
        {
            if (CommonComponentMap.TryGetValue(shortName, out var fullName))
                return fullName;
            return shortName;
        }

        private static Type FindTypeInAllAssemblies(string typeName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var type = assembly.GetType(typeName);
                    if (type != null)
                        return type;
                }
                catch
                {
                    // Ignore assemblies that fail to load
                }
            }
            return null;
        }

        private static Type FindTypeByClassName(string typeName)
        {
            Type foundType = null;
            var foundCount = 0;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var types = assembly.GetTypes()
                        .Where(t => t.Name == typeName && typeof(Component).IsAssignableFrom(t))
                        .ToArray();

                    if (types.Length == 1)
                    {
                        // Prefer Unity types if multiple matches found later
                        if (foundType == null ||
                            types[0].Namespace?.StartsWith("UnityEngine") == true)
                        {
                            foundType = types[0];
                        }
                        foundCount++;
                    }
                    else if (types.Length > 1)
                    {
                        // Multiple types with same name in this assembly
                        // Prefer Unity namespace
                        var preferred = types.FirstOrDefault(t =>
                            t.Namespace?.StartsWith("UnityEngine") == true);

                        if (preferred != null)
                        {
                            foundType = preferred;
                            foundCount++;
                        }
                        else if (foundType == null)
                        {
                            foundType = types[0];
                            foundCount++;
                        }
                    }
                }
                catch
                {
                    // Ignore
                }
            }

            // Log warning if multiple types found
            if (foundCount > 1)
            {
                AIBridgeLogger.LogDebug($"Multiple types found for '{typeName}', using: {foundType?.FullName}");
            }

            return foundType;
        }
    }
}
