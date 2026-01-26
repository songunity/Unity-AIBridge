using System.Collections.Generic;

namespace AIBridgeCLI.Commands
{
    /// <summary>
    /// Inspector command builder: get_components, get_properties, set_property, add_component, remove_component
    /// </summary>
    public class InspectorCommandBuilder : BaseCommandBuilder
    {
        public override string Type => "inspector";
        public override string Description => "Component/Inspector operations (get, set, add, remove)";

        public override string[] Actions => new[]
        {
            "get_components", "get_properties", "set_property", "add_component", "remove_component"
        };

        protected override Dictionary<string, List<ParameterInfo>> ActionParameters => new Dictionary<string, List<ParameterInfo>>
        {
            ["get_components"] = new List<ParameterInfo>
            {
                new ParameterInfo("path", "Path to the GameObject", false),
                new ParameterInfo("instanceId", "Instance ID of the GameObject", false)
            },
            ["get_properties"] = new List<ParameterInfo>
            {
                new ParameterInfo("path", "Path to the GameObject", false),
                new ParameterInfo("instanceId", "Instance ID of the GameObject", false),
                new ParameterInfo("componentName", "Name of the component", false),
                new ParameterInfo("componentIndex", "Index of the component", false)
            },
            ["set_property"] = new List<ParameterInfo>
            {
                new ParameterInfo("path", "Path to the GameObject", false),
                new ParameterInfo("instanceId", "Instance ID of the GameObject", false),
                new ParameterInfo("componentName", "Name of the component", false),
                new ParameterInfo("componentIndex", "Index of the component", false),
                new ParameterInfo("propertyName", "Name of the property to set", true),
                new ParameterInfo("value", "Value to set", true)
            },
            ["add_component"] = new List<ParameterInfo>
            {
                new ParameterInfo("path", "Path to the GameObject", false),
                new ParameterInfo("instanceId", "Instance ID of the GameObject", false),
                new ParameterInfo("typeName", "Type name of the component (e.g., BoxCollider, Rigidbody)", true)
            },
            ["remove_component"] = new List<ParameterInfo>
            {
                new ParameterInfo("path", "Path to the GameObject", false),
                new ParameterInfo("instanceId", "Instance ID of the GameObject", false),
                new ParameterInfo("componentName", "Name of the component", false),
                new ParameterInfo("componentIndex", "Index of the component", false),
                new ParameterInfo("componentInstanceId", "Instance ID of the component", false)
            }
        };
    }
}
