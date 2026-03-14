using System.Collections;
using System.ComponentModel;
using UnityEditor;

namespace AIBridge.Editor
{
    public static class MenuItemCommand
    {
        [AIBridge("通过路径执行 Unity 编辑器菜单项",
            "AIBridgeCLI MenuItemCommand_Execute --menuPath \"GameObject/Create Empty\"")]
        public static IEnumerator Execute(
            [Description("菜单项路径（例如 GameObject/Create Empty）")] string menuPath = null)
        {
            if (string.IsNullOrEmpty(menuPath))
            {
                yield return CommandResult.Failure("Missing 'menuPath' parameter");
                yield break;
            }

            var executed = EditorApplication.ExecuteMenuItem(menuPath);
            if (executed)
                yield return CommandResult.Success(new { menuPath, executed = true });
            else
                yield return CommandResult.Failure($"Menu item not found or failed to execute: {menuPath}");
        }
    }
}
