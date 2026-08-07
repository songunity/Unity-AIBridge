using System;
using System.Collections.Generic;
#if AIBRIDGE_RUNTIME_ENABLED
using AIBridge.Runtime;
#endif
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AIBridge.Editor
{
    [InitializeOnLoad]
    internal sealed class AIBridgeRuntimeBuildProcessor : IPreprocessBuildWithReport, IProcessSceneWithReport
    {
        internal const string RuntimeEnabledDefine = "AIBRIDGE_RUNTIME_ENABLED";

        private static bool _runtimeSettingsCarrierInjected;

        static AIBridgeRuntimeBuildProcessor()
        {
            EditorApplication.delayCall += SyncRuntimeBootstrapDefinesForActiveTarget;
        }

        public int callbackOrder
        {
            get { return 0; }
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            _runtimeSettingsCarrierInjected = false;
            var settings = AIBridgeProjectSettings.Instance.RuntimeBridge;
            var buildTargetGroup = report != null
                ? BuildPipeline.GetBuildTargetGroup(report.summary.platform)
                : BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);

            var symbolsChanged = SyncRuntimeBootstrapDefines(buildTargetGroup, settings, true);
            if (symbolsChanged)
            {
                throw new BuildFailedException(AIBridgeEditorText.T(
                    "AIBridge Runtime Bridge build symbols changed. Unity must recompile scripts before this build can be trusted; please build again.",
                    "AIBridge Runtime Bridge 构建宏已变化。Unity 需要先重新编译脚本，请重新执行构建。"));
            }

            LogBuildInjectionState(settings);
        }

        public void OnProcessScene(Scene scene, BuildReport report)
        {
#if AIBRIDGE_RUNTIME_ENABLED
            if (_runtimeSettingsCarrierInjected)
            {
                return;
            }

            var settings = AIBridgeProjectSettings.Instance.RuntimeBridge;
            if (!ShouldInjectRuntimeSettingsCarrier(settings))
            {
                return;
            }

            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            var carrier = FindCarrierInScene(scene);
            if (carrier == null)
            {
                var gameObject = new GameObject(AIBridgeRuntimeSettingsCarrier.CarrierObjectName);
                gameObject.hideFlags = HideFlags.HideInHierarchy;
                SceneManager.MoveGameObjectToScene(gameObject, scene);
                carrier = gameObject.AddComponent<AIBridgeRuntimeSettingsCarrier>();
            }

            // 构建管线处理的是场景副本，这里注入的 carrier 不会写回用户场景或项目文件。
            carrier.RuntimeSettings = AIBridgeRuntimeBridgeEditorUtility.CreateRuntimeSettingsFromProjectSettings();
            carrier.GeneratedForBuild = true;
            _runtimeSettingsCarrierInjected = true;
#endif
        }

        internal static void SyncRuntimeBootstrapDefinesForActiveTarget()
        {
            SyncRuntimeBootstrapDefines(
                BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget),
                AIBridgeProjectSettings.Instance.RuntimeBridge,
                false);
        }

        private static bool SyncRuntimeBootstrapDefines(
            BuildTargetGroup buildTargetGroup,
            AIBridgeProjectSettings.RuntimeBridgeSettingsData settings,
            bool logChanges)
        {
            if (buildTargetGroup == BuildTargetGroup.Unknown || settings == null)
            {
                return false;
            }

            var symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(buildTargetGroup);
            var defines = ParseDefines(symbols);

            var changed = false;
            changed |= SetDefine(
                defines,
                RuntimeEnabledDefine,
                settings.EnableRuntimeBridge);
            changed |= SetDefine(
                defines,
                AIBridgeHybridClrUtility.HybridClrAvailableDefine,
                AIBridgeHybridClrUtility.IsHybridClrInstalled());

            if (!changed)
            {
                return false;
            }

            PlayerSettings.SetScriptingDefineSymbolsForGroup(buildTargetGroup, string.Join(";", defines.ToArray()));
            if (logChanges)
            {
                Debug.Log(AIBridgeEditorText.T(
                    "[AIBridge] Runtime Bridge bootstrap scripting symbols synchronized.",
                    "[AIBridge] Runtime Bridge bootstrap 脚本宏已同步。"));
            }

            return true;
        }

#if AIBRIDGE_RUNTIME_ENABLED
        private static bool ShouldInjectRuntimeSettingsCarrier(
            AIBridgeProjectSettings.RuntimeBridgeSettingsData settings)
        {
            return settings != null && settings.EnableRuntimeBridge;
        }

        private static AIBridgeRuntimeSettingsCarrier FindCarrierInScene(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                var root = roots[i];
                if (root == null)
                {
                    continue;
                }

                var carriers = root.GetComponentsInChildren<AIBridgeRuntimeSettingsCarrier>(true);
                for (var j = 0; j < carriers.Length; j++)
                {
                    if (carriers[j] != null)
                    {
                        return carriers[j];
                    }
                }
            }

            return null;
        }
#endif

        private static List<string> ParseDefines(string symbols)
        {
            var defines = new List<string>();
            if (string.IsNullOrEmpty(symbols))
            {
                return defines;
            }

            var parts = symbols.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < parts.Length; i++)
            {
                var define = parts[i].Trim();
                if (!string.IsNullOrEmpty(define) && !defines.Contains(define))
                {
                    defines.Add(define);
                }
            }

            return defines;
        }

        private static bool SetDefine(List<string> defines, string define, bool enabled)
        {
            var index = defines.IndexOf(define);
            if (enabled)
            {
                if (index >= 0)
                {
                    return false;
                }

                defines.Add(define);
                return true;
            }

            if (index < 0)
            {
                return false;
            }

            defines.RemoveAt(index);
            return true;
        }

        private static void LogBuildInjectionState(
            AIBridgeProjectSettings.RuntimeBridgeSettingsData settings)
        {
            if (settings == null || !settings.EnableRuntimeBridge)
            {
                Debug.Log(AIBridgeEditorText.T(
                    "[AIBridge] Runtime Bridge is disabled; bootstrap auto injection will not run.",
                    "[AIBridge] Runtime Bridge 已关闭，bootstrap 自动注入不会运行。"));
                return;
            }

            Debug.Log(AIBridgeEditorText.T(
                "[AIBridge] Runtime Bridge is enabled; AIBridgeRuntime will be auto-injected.",
                "[AIBridge] Runtime Bridge 已启用，将自动注入 AIBridgeRuntime。"));
        }
    }
}
