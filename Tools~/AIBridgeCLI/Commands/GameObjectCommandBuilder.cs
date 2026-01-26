using System.Collections.Generic;

namespace AIBridgeCLI.Commands
{
    /// <summary>
    /// GameObject command builder: create, destroy, find, set_active, rename, duplicate, get_info
    /// </summary>
    public class GameObjectCommandBuilder : BaseCommandBuilder
    {
        public override string Type => "gameobject";
        public override string Description => "GameObject operations (create, destroy, find, etc.)";

        public override string[] Actions => new[]
        {
            "create", "destroy", "find", "set_active", "rename", "duplicate", "get_info"
        };

        protected override Dictionary<string, List<ParameterInfo>> ActionParameters => new Dictionary<string, List<ParameterInfo>>
        {
            ["create"] = new List<ParameterInfo>
            {
                new ParameterInfo("name", "Name of the new GameObject", true),
                new ParameterInfo("primitiveType", "Primitive type: Cube, Sphere, Capsule, Cylinder, Plane, Quad", false),
                new ParameterInfo("parentPath", "Parent object path in hierarchy", false)
            },
            ["destroy"] = new List<ParameterInfo>
            {
                new ParameterInfo("path", "Path to the GameObject in hierarchy", false),
                new ParameterInfo("instanceId", "Instance ID of the GameObject", false)
            },
            ["find"] = new List<ParameterInfo>
            {
                new ParameterInfo("name", "Name to search for", false),
                new ParameterInfo("tag", "Tag to search for", false),
                new ParameterInfo("withComponent", "Component type to filter by", false),
                new ParameterInfo("maxResults", "Maximum number of results", false, "10")
            },
            ["set_active"] = new List<ParameterInfo>
            {
                new ParameterInfo("path", "Path to the GameObject", false),
                new ParameterInfo("instanceId", "Instance ID of the GameObject", false),
                new ParameterInfo("active", "Set active state", false),
                new ParameterInfo("toggle", "Toggle active state", false)
            },
            ["rename"] = new List<ParameterInfo>
            {
                new ParameterInfo("path", "Path to the GameObject", false),
                new ParameterInfo("instanceId", "Instance ID of the GameObject", false),
                new ParameterInfo("newName", "New name for the GameObject", true)
            },
            ["duplicate"] = new List<ParameterInfo>
            {
                new ParameterInfo("path", "Path to the GameObject to duplicate", false),
                new ParameterInfo("instanceId", "Instance ID of the GameObject", false)
            },
            ["get_info"] = new List<ParameterInfo>
            {
                new ParameterInfo("path", "Path to the GameObject", false),
                new ParameterInfo("instanceId", "Instance ID of the GameObject", false)
            }
        };
    }
}
