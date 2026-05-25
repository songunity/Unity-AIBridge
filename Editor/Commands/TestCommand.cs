using System.Collections;
using System.ComponentModel;
using UnityEditor.TestTools.TestRunner.Api;

namespace AIBridge.Editor
{
    public static class TestCommand
    {
        [AIBridge("运行 Unity 测试（EditMode）",
            @"运行项目中的单元测试并返回结果。支持按测试名、分组、程序集过滤。

示例：
AIBridgeCLI TestCommand_Run --mode EditMode --raw
AIBridgeCLI TestCommand_Run --testName ""MyNamespace.MyFixture.MyTest"" --raw
AIBridgeCLI TestCommand_Run --assemblyName ""Tests"" --raw

返回值包含：status, total, passed, failed, skipped, failedTests（含名称、消息、堆栈）",
            "Run")]
        public static IEnumerator Run(
            [Description("测试模式：EditMode 或 PlayMode")] string mode = "EditMode",
            [Description("指定测试名（完整限定名）")] string testName = null,
            [Description("测试分组名")] string groupName = null,
            [Description("程序集名")] string assemblyName = null,
            [Description("超时毫秒数")] int timeout = 120000)
        {
            if (!TryParseMode(mode, out var testMode))
            {
                yield return CommandResult.Failure($"不支持的测试模式: {mode}。支持: EditMode, PlayMode");
                yield break;
            }

            if (testMode == TestMode.PlayMode)
            {
                yield return CommandResult.Failure("PlayMode 测试暂不支持，请使用 EditMode。");
                yield break;
            }

            var result = TestRunTracker.StartRun(testMode, testName, groupName, assemblyName, timeout);
            var snapshot = result.snapshot;

            yield return CommandResult.Success(new
            {
                action = "run",
                status = snapshot.status,
                mode = snapshot.mode,
                startedAt = snapshot.startedAt,
                duration = snapshot.duration,
                total = snapshot.total,
                passed = snapshot.passed,
                failed = snapshot.failed,
                skipped = snapshot.skipped,
                inconclusive = snapshot.inconclusive,
                failedTests = snapshot.failedTests,
                startedByInvocation = result.startedByInvocation,
                attachedToExistingRun = result.attachedToExistingRun
            });
        }

        [AIBridge("查询当前测试运行状态",
            @"查询最近一次测试运行的状态和结果。

示例：
AIBridgeCLI TestCommand_Status --raw

返回值：status(idle/running/passed/failed/timeout), total, passed, failed, failedTests",
            "Status")]
        public static IEnumerator Status()
        {
            var snapshot = TestRunTracker.GetSnapshot();

            yield return CommandResult.Success(new
            {
                action = "status",
                status = snapshot.status,
                mode = snapshot.mode,
                startedAt = snapshot.startedAt,
                duration = snapshot.duration,
                total = snapshot.total,
                passed = snapshot.passed,
                failed = snapshot.failed,
                skipped = snapshot.skipped,
                inconclusive = snapshot.inconclusive,
                failedTests = snapshot.failedTests,
                startedByInvocation = snapshot.startedByInvocation,
                attachedToExistingRun = snapshot.attachedToExistingRun
            });
        }

        private static bool TryParseMode(string modeText, out TestMode testMode)
        {
            if (string.Equals(modeText, "PlayMode", System.StringComparison.OrdinalIgnoreCase))
            {
                testMode = TestMode.PlayMode;
                return true;
            }

            if (string.Equals(modeText, "EditMode", System.StringComparison.OrdinalIgnoreCase))
            {
                testMode = TestMode.EditMode;
                return true;
            }

            testMode = TestMode.EditMode;
            return false;
        }
    }
}
