using System;

namespace AIBridge.Editor
{
    [Serializable]
    internal class EditorInstanceMetadata
    {
        public int schemaVersion;
        public int processId;
        public string projectRoot;
        public string projectName;
        public string windowTitle;
        public string lastUpdatedUtc;
    }
}
