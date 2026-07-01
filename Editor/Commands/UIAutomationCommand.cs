using System.Collections;
using System.ComponentModel;
using AIBridge.Shared;
using UnityEngine;

namespace AIBridge.Editor
{
    public static class UIAutomationCommand
    {
        [AIBridge("获取 Play Mode UGUI 控件快照",
            "AIBridgeCLI UIAutomationCommand_Snapshot --maxResults 100 --raw")]
        public static IEnumerator Snapshot(
            [Description("最大返回按钮数量")] int maxResults = 100,
            [Description("是否包含未激活或不可交互控件")] bool includeDisabled = false)
        {
            if (!Application.isPlaying)
            {
                yield return CommandResult.Failure("UIAutomationCommand requires Play Mode.");
                yield break;
            }

            yield return CommandResult.Success(AIBridgeUiAutomation.Snapshot(maxResults, includeDisabled));
        }

        [AIBridge("按文本、名称、路径或 Canvas 查找 Play Mode UGUI Button",
            "AIBridgeCLI UIAutomationCommand_Find --keyword \"Start\" --raw")]
        public static IEnumerator Find(
            [Description("匹配文本、名称、路径或 Canvas 名称")] string keyword = null,
            [Description("最大返回按钮数量")] int maxResults = 100,
            [Description("是否包含未激活或不可交互控件")] bool includeDisabled = false)
        {
            if (!Application.isPlaying)
            {
                yield return CommandResult.Failure("UIAutomationCommand requires Play Mode.");
                yield break;
            }

            yield return CommandResult.Success(AIBridgeUiAutomation.Find(keyword, maxResults, includeDisabled));
        }

        [AIBridge("对 Play Mode UI 执行 EventSystem raycast",
            "AIBridgeCLI UIAutomationCommand_Raycast --path \"Canvas/Button\" --raw")]
        public static IEnumerator Raycast(
            [Description("屏幕 X 坐标；不传 path/instanceId 时使用")] float x = 0f,
            [Description("屏幕 Y 坐标；不传 path/instanceId 时使用")] float y = 0f,
            [Description("GameObject 层级路径")] string path = null,
            [Description("GameObject 实例 ID")] int instanceId = 0,
            [Description("最大返回命中数量")] int maxResults = 20)
        {
            if (!Application.isPlaying)
            {
                yield return CommandResult.Failure("UIAutomationCommand requires Play Mode.");
                yield break;
            }

            yield return CommandResult.Success(AIBridgeUiAutomation.Raycast(x, y, path, instanceId, maxResults));
        }

        [AIBridge("点击 Play Mode UI 控件或屏幕坐标",
            "AIBridgeCLI UIAutomationCommand_Click --path \"Canvas/Button\" --raw")]
        public static IEnumerator Click(
            [Description("屏幕 X 坐标；不传 path/instanceId 时使用")] float x = 0f,
            [Description("屏幕 Y 坐标；不传 path/instanceId 时使用")] float y = 0f,
            [Description("GameObject 层级路径")] string path = null,
            [Description("GameObject 实例 ID")] int instanceId = 0)
        {
            if (!Application.isPlaying)
            {
                yield return CommandResult.Failure("UIAutomationCommand requires Play Mode.");
                yield break;
            }

            yield return CommandResult.Success(AIBridgeUiAutomation.Click(x, y, path, instanceId));
        }
    }
}
