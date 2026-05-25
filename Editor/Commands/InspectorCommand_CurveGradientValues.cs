using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AIBridge.Editor
{
    public static partial class InspectorCommand
    {
        private static bool TryGetEnumValue<TEnum>(object value, out TEnum result) where TEnum : struct
        {
            result = default;
            if (value == null) return false;
            if (value is TEnum enumValue) { result = enumValue; return true; }
            if (TryGetInt(value, out var intValue))
            {
                result = (TEnum)Enum.ToObject(typeof(TEnum), intValue);
                return true;
            }
            return Enum.TryParse(value.ToString(), true, out result);
        }

        private static bool TryGetAnimationCurve(object value, out AnimationCurve result)
        {
            result = null;
            if (value is AnimationCurve curve)
            {
                result = curve;
                return true;
            }

            object keysValue = value;
            WrapMode preWrapMode = WrapMode.Default;
            WrapMode postWrapMode = WrapMode.Default;

            if (value is IDictionary dictionary)
            {
                keysValue = GetDictionaryValue(dictionary, "keys");
                var preWrapValue = GetDictionaryValue(dictionary, "preWrapMode");
                if (preWrapValue != null && !TryGetEnumValue(preWrapValue, out preWrapMode))
                    return false;
                var postWrapValue = GetDictionaryValue(dictionary, "postWrapMode");
                if (postWrapValue != null && !TryGetEnumValue(postWrapValue, out postWrapMode))
                    return false;
            }

            if (!(keysValue is IList keyList))
                return false;

            var keys = new List<Keyframe>();
            for (var i = 0; i < keyList.Count; i++)
            {
                if (!TryGetKeyframe(keyList[i], out var keyframe))
                    return false;
                keys.Add(keyframe);
            }

            result = new AnimationCurve(keys.ToArray())
            {
                preWrapMode = preWrapMode,
                postWrapMode = postWrapMode
            };
            return true;
        }

        private static bool TryGetKeyframe(object value, out Keyframe result)
        {
            result = new Keyframe();
            if (!(value is IDictionary dictionary))
                return false;

            if (!TryGetFloat(GetDictionaryValue(dictionary, "time"), out var time)
                || !TryGetFloat(GetDictionaryValue(dictionary, "value"), out var keyValue))
                return false;

            var inTangent = 0f;
            var outTangent = 0f;
            var inWeight = 0f;
            var outWeight = 0f;
            var weightedMode = WeightedMode.None;

            var inTangentValue = GetDictionaryValue(dictionary, "inTangent");
            var outTangentValue = GetDictionaryValue(dictionary, "outTangent");
            var inWeightValue = GetDictionaryValue(dictionary, "inWeight");
            var outWeightValue = GetDictionaryValue(dictionary, "outWeight");
            var weightedModeValue = GetDictionaryValue(dictionary, "weightedMode");

            if (inTangentValue != null && !TryGetFloat(inTangentValue, out inTangent)) return false;
            if (outTangentValue != null && !TryGetFloat(outTangentValue, out outTangent)) return false;
            if (inWeightValue != null && !TryGetFloat(inWeightValue, out inWeight)) return false;
            if (outWeightValue != null && !TryGetFloat(outWeightValue, out outWeight)) return false;
            if (weightedModeValue != null && !TryGetEnumValue(weightedModeValue, out weightedMode)) return false;

            result = new Keyframe(time, keyValue, inTangent, outTangent, inWeight, outWeight)
            {
                weightedMode = weightedMode
            };
            return true;
        }

        private static bool TryGetGradient(object value, out Gradient result)
        {
            result = null;
            if (value is Gradient gradient)
            {
                result = gradient;
                return true;
            }

            if (!(value is IDictionary dictionary))
                return false;

            var colorKeysValue = GetDictionaryValue(dictionary, "colorKeys")
                                 ?? GetDictionaryValue(dictionary, "colors");
            var alphaKeysValue = GetDictionaryValue(dictionary, "alphaKeys")
                                 ?? GetDictionaryValue(dictionary, "alphas");

            if (!TryGetGradientColorKeys(colorKeysValue, out var colorKeys)
                || !TryGetGradientAlphaKeys(alphaKeysValue, out var alphaKeys))
                return false;

            var mode = GradientMode.Blend;
            var modeValue = GetDictionaryValue(dictionary, "mode");
            if (modeValue != null && !TryGetEnumValue(modeValue, out mode))
                return false;

            result = new Gradient();
            result.SetKeys(colorKeys, alphaKeys);
            result.mode = mode;
            return true;
        }

        private static bool TryGetGradientColorKeys(object value, out GradientColorKey[] result)
        {
            result = null;
            if (!(value is IList list) || list.Count == 0)
                return false;

            var keys = new List<GradientColorKey>();
            for (var i = 0; i < list.Count; i++)
            {
                if (!(list[i] is IDictionary dictionary))
                    return false;
                if (!TryGetFloat(GetDictionaryValue(dictionary, "time"), out var time))
                    return false;
                var colorValue = GetDictionaryValue(dictionary, "color") ?? dictionary;
                if (!TryGetColor(colorValue, out var color))
                    return false;
                keys.Add(new GradientColorKey(color, time));
            }

            result = keys.ToArray();
            return true;
        }

        private static bool TryGetGradientAlphaKeys(object value, out GradientAlphaKey[] result)
        {
            result = null;
            if (!(value is IList list) || list.Count == 0)
                return false;

            var keys = new List<GradientAlphaKey>();
            for (var i = 0; i < list.Count; i++)
            {
                if (!(list[i] is IDictionary dictionary))
                    return false;
                if (!TryGetFloat(GetDictionaryValue(dictionary, "time"), out var time))
                    return false;
                var alphaValue = GetDictionaryValue(dictionary, "alpha")
                                 ?? GetDictionaryValue(dictionary, "a");
                if (!TryGetFloat(alphaValue, out var alpha))
                    return false;
                keys.Add(new GradientAlphaKey(alpha, time));
            }

            result = keys.ToArray();
            return true;
        }
    }
}
