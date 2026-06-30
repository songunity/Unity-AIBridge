using System;
using System.IO;
using System.Reflection;
using UnityEditor.PackageManager;

namespace AIBridge.Editor
{
    internal static class AIBridgeHybridClrUtility
    {
        public const string PackageName = "com.code-philosophy.hybridclr";
        public const string HybridClrAvailableDefine = "AIBRIDGE_HYBRIDCLR_AVAILABLE";

        public static bool IsHybridClrInstalled()
        {
            if (PackageInfoContainsHybridClrPackage())
            {
                return true;
            }

            return ManifestContainsHybridClrPackage();
        }

        private static bool PackageInfoContainsHybridClrPackage()
        {
            try
            {
                var findForPackageName = typeof(PackageInfo).GetMethod(
                    "FindForPackageName",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(string) },
                    null);

                return findForPackageName != null
                    && findForPackageName.Invoke(null, new object[] { PackageName }) != null;
            }
            catch
            {
                return false;
            }
        }

        private static bool ManifestContainsHybridClrPackage()
        {
            var manifestPath = Path.Combine(Directory.GetCurrentDirectory(), "Packages", "manifest.json");
            if (!File.Exists(manifestPath))
            {
                return false;
            }

            try
            {
                var manifest = File.ReadAllText(manifestPath);
                return manifest.IndexOf("\"" + PackageName + "\"", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
