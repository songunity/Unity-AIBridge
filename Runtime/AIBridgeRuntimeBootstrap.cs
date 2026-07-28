using UnityEngine;
using UnityEngine.SceneManagement;

namespace AIBridge.Runtime
{
    /// <summary>
    /// Automatically creates AIBridgeRuntime for built Players when the project opts in.
    /// </summary>
    public static class AIBridgeRuntimeBootstrap
    {
        private const string BootstrapObjectName = "AIBridgeRuntime (Bootstrap)";

        private static bool _initialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeBeforeSceneLoad()
        {
#if UNITY_EDITOR
            return;
#else
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            // BeforeSceneLoad 阶段还看不到首场景内的组件，等 sceneLoaded 后再判重创建。
            SceneManager.sceneLoaded += HandleFirstSceneLoaded;
#endif
        }

#if !UNITY_EDITOR
        private static void HandleFirstSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= HandleFirstSceneLoaded;
            CreateRuntimeIfNeeded(scene.name);
        }

        private static void CreateRuntimeIfNeeded(string sceneName)
        {
            var injectedSettings = TakeInjectedRuntimeSettings();
            var existingRuntime = FindExistingRuntime();
            if (existingRuntime != null)
            {
                Debug.Log("[AIBridgeRuntimeBootstrap] Existing AIBridgeRuntime found in scene '" + sceneName + "'; bootstrap creation skipped. / 场景 '" + sceneName + "' 已存在 AIBridgeRuntime，跳过自动创建。");
                return;
            }

            if (injectedSettings == null || !injectedSettings.enableRuntimeBridge)
            {
                Debug.Log("[AIBridgeRuntimeBootstrap] No enabled build settings were injected; bootstrap creation skipped. / 当前构建未注入已启用的 Runtime Bridge 设置，跳过自动创建。");
                return;
            }

            var gameObject = new GameObject(BootstrapObjectName);
            gameObject.SetActive(false);
            gameObject.hideFlags = HideFlags.HideInHierarchy;

            var runtime = gameObject.AddComponent<AIBridgeRuntime>();
            runtime.runtimeSettings = BuildBootstrapRuntimeSettings(injectedSettings);
            gameObject.SetActive(true);

            Debug.Log("[AIBridgeRuntimeBootstrap] AIBridgeRuntime created by bootstrap. http="
                + runtime.runtimeSettings.enableHttpTransport
                + ", port=" + runtime.runtimeSettings.httpPort
                + ", lanDiscovery=" + runtime.runtimeSettings.enableLanDiscovery
                + ". / 已通过 bootstrap 创建 AIBridgeRuntime。");
        }

        private static AIBridgeRuntimeSettings BuildBootstrapRuntimeSettings(AIBridgeRuntimeSettings injectedSettings)
        {
            var settings = injectedSettings.Clone();
            settings.enableRuntimeCodeExecution = settings.enableRuntimeCodeExecution && IsRuntimeCodeExecutionAvailableByBuild();
            return settings;
        }

        private static AIBridgeRuntimeSettings TakeInjectedRuntimeSettings()
        {
            // 构建期 carrier 只存在于 Player 场景副本中，读取后立即销毁，避免运行时层级留下额外对象。
            var carriers = Resources.FindObjectsOfTypeAll<AIBridgeRuntimeSettingsCarrier>();
            AIBridgeRuntimeSettings settings = null;
            for (var i = 0; i < carriers.Length; i++)
            {
                var carrier = carriers[i];
                if (carrier == null
                    || carrier.gameObject == null
                    || !carrier.gameObject.scene.IsValid())
                {
                    continue;
                }

                if (settings == null && carrier.RuntimeSettings != null)
                {
                    settings = carrier.RuntimeSettings.Clone();
                }

                DestroyRuntimeSettingsCarrier(carrier);
            }

            return settings;
        }

        private static void DestroyRuntimeSettingsCarrier(AIBridgeRuntimeSettingsCarrier carrier)
        {
            if (carrier == null || carrier.gameObject == null)
            {
                return;
            }

            UnityEngine.Object.Destroy(carrier.gameObject);
        }

        private static bool IsRuntimeCodeExecutionAvailableByBuild()
        {
#if AIBRIDGE_HYBRIDCLR_AVAILABLE
            return true;
#else
            return false;
#endif
        }

        private static AIBridgeRuntime FindExistingRuntime()
        {
            if (AIBridgeRuntime.Instance != null)
            {
                return AIBridgeRuntime.Instance;
            }

            var runtimes = Resources.FindObjectsOfTypeAll<AIBridgeRuntime>();
            for (var i = 0; i < runtimes.Length; i++)
            {
                var runtime = runtimes[i];
                if (runtime != null
                    && runtime.gameObject != null
                    && runtime.gameObject.scene.IsValid())
                {
                    return runtime;
                }
            }

            return null;
        }
#endif
    }
}
