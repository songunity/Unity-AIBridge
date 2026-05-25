using UnityEditor;

namespace AIBridge.Editor
{
    public static class GifRecorderSettings
    {
        private const string KeyPrefix = "AIBridge_GifRecorder_";

        public static float DefaultDuration
        {
            get => EditorPrefs.GetFloat(KeyPrefix + "Duration", 2.5f);
            set => EditorPrefs.SetFloat(KeyPrefix + "Duration", value);
        }

        public static int DefaultFps
        {
            get => EditorPrefs.GetInt(KeyPrefix + "Fps", 20);
            set => EditorPrefs.SetInt(KeyPrefix + "Fps", value);
        }

        public static float DefaultScale
        {
            get => EditorPrefs.GetFloat(KeyPrefix + "Scale", 0.5f);
            set => EditorPrefs.SetFloat(KeyPrefix + "Scale", value);
        }

        public static int DefaultColorCount
        {
            get => EditorPrefs.GetInt(KeyPrefix + "ColorCount", 128);
            set => EditorPrefs.SetInt(KeyPrefix + "ColorCount", value);
        }

        public static float DefaultStartDelay
        {
            get => EditorPrefs.GetFloat(KeyPrefix + "StartDelay", 0.1f);
            set => EditorPrefs.SetFloat(KeyPrefix + "StartDelay", value);
        }

        public static int CalculateFrameCount()
        {
            return UnityEngine.Mathf.Max(1, UnityEngine.Mathf.RoundToInt(DefaultDuration * DefaultFps));
        }

        public static void ResetToDefaults()
        {
            DefaultDuration = 2.5f;
            DefaultFps = 20;
            DefaultScale = 0.5f;
            DefaultColorCount = 128;
        }
    }
}
