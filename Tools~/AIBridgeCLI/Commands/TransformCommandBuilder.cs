using System.Collections.Generic;

namespace AIBridgeCLI.Commands
{
    /// <summary>
    /// Transform command builder: get, set_position, set_rotation, set_scale, set_parent, look_at, reset, set_sibling_index
    /// </summary>
    public class TransformCommandBuilder : BaseCommandBuilder
    {
        public override string Type => "transform";
        public override string Description => "Transform operations (position, rotation, scale, parent)";

        public override string[] Actions => new[]
        {
            "get", "set_position", "set_rotation", "set_scale", "set_parent", "look_at", "reset", "set_sibling_index"
        };

        protected override Dictionary<string, List<ParameterInfo>> ActionParameters => new Dictionary<string, List<ParameterInfo>>
        {
            ["get"] = new List<ParameterInfo>
            {
                new ParameterInfo("path", "Path to the GameObject", false),
                new ParameterInfo("instanceId", "Instance ID of the GameObject", false)
            },
            ["set_position"] = new List<ParameterInfo>
            {
                new ParameterInfo("path", "Path to the GameObject", false),
                new ParameterInfo("instanceId", "Instance ID of the GameObject", false),
                new ParameterInfo("x", "X position", true),
                new ParameterInfo("y", "Y position", true),
                new ParameterInfo("z", "Z position", true),
                new ParameterInfo("local", "Use local position instead of world", false, "false")
            },
            ["set_rotation"] = new List<ParameterInfo>
            {
                new ParameterInfo("path", "Path to the GameObject", false),
                new ParameterInfo("instanceId", "Instance ID of the GameObject", false),
                new ParameterInfo("x", "X rotation (euler)", true),
                new ParameterInfo("y", "Y rotation (euler)", true),
                new ParameterInfo("z", "Z rotation (euler)", true),
                new ParameterInfo("local", "Use local rotation instead of world", false, "false")
            },
            ["set_scale"] = new List<ParameterInfo>
            {
                new ParameterInfo("path", "Path to the GameObject", false),
                new ParameterInfo("instanceId", "Instance ID of the GameObject", false),
                new ParameterInfo("x", "X scale (or uniform scale if only x is provided)", false),
                new ParameterInfo("y", "Y scale", false),
                new ParameterInfo("z", "Z scale", false),
                new ParameterInfo("uniform", "Uniform scale value", false)
            },
            ["set_parent"] = new List<ParameterInfo>
            {
                new ParameterInfo("path", "Path to the GameObject", false),
                new ParameterInfo("instanceId", "Instance ID of the GameObject", false),
                new ParameterInfo("parentPath", "Path to the parent GameObject", false),
                new ParameterInfo("parentInstanceId", "Instance ID of the parent", false),
                new ParameterInfo("worldPositionStays", "Maintain world position", false, "true")
            },
            ["look_at"] = new List<ParameterInfo>
            {
                new ParameterInfo("path", "Path to the GameObject", false),
                new ParameterInfo("instanceId", "Instance ID of the GameObject", false),
                new ParameterInfo("targetPath", "Path to the target GameObject", false),
                new ParameterInfo("targetInstanceId", "Instance ID of the target", false),
                new ParameterInfo("targetX", "Target X position", false),
                new ParameterInfo("targetY", "Target Y position", false),
                new ParameterInfo("targetZ", "Target Z position", false)
            },
            ["reset"] = new List<ParameterInfo>
            {
                new ParameterInfo("path", "Path to the GameObject", false),
                new ParameterInfo("instanceId", "Instance ID of the GameObject", false),
                new ParameterInfo("position", "Reset position", false, "true"),
                new ParameterInfo("rotation", "Reset rotation", false, "true"),
                new ParameterInfo("scale", "Reset scale", false, "true")
            },
            ["set_sibling_index"] = new List<ParameterInfo>
            {
                new ParameterInfo("path", "Path to the GameObject", false),
                new ParameterInfo("instanceId", "Instance ID of the GameObject", false),
                new ParameterInfo("index", "Sibling index", false),
                new ParameterInfo("first", "Move to first", false),
                new ParameterInfo("last", "Move to last", false)
            }
        };
    }
}
