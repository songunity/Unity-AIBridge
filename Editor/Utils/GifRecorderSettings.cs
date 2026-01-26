using UnityEditor;

namespace AIBridge.Editor
{
    /// <summary>
    /// Persistent settings for GIF recording.
    /// </summary>
    public static class GifRecorderSettings
    {
        private const string KeyPrefix = "AIBridge_GifRecorder_";

        public static int DefaultFrameCount
        {
            get => EditorPrefs.GetInt(KeyPrefix + "FrameCount", 50);
            set => EditorPrefs.SetInt(KeyPrefix + "FrameCount", value);
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

        public static void ResetToDefaults()
        {
            DefaultFrameCount = 50;
            DefaultFps = 20;
            DefaultScale = 0.5f;
            DefaultColorCount = 128;
        }
    }
}
