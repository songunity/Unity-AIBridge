using UnityEditor;
using UnityEngine;

namespace AIBridge.Editor
{
    /// <summary>
    /// GameObject 操作的通用辅助类
    /// </summary>
    public static class GameObjectHelper
    {
        /// <summary>
        /// 获取目标 GameObject，优先级：instanceId > path
        /// </summary>
        /// <param name="path">层级路径</param>
        /// <param name="instanceId">实例ID</param>
        /// <returns>找到的GameObject，如果都没有则返回null</returns>
        public static GameObject GetTargetGameObject(string path = null, int instanceId = 0)
        {
            if (instanceId != 0)
                return EditorUtility.InstanceIDToObject(instanceId) as GameObject;

            if (!string.IsNullOrEmpty(path))
                return GameObject.Find(path);

            return null;
        }

        /// <summary>
        /// 获取 GameObject 的完整层级路径
        /// </summary>
        public static string GetGameObjectPath(GameObject go)
        {
            if (go == null) return null;
            
            var goPath = go.name;
            var parent = go.transform.parent;
            while (parent != null)
            {
                goPath = parent.name + "/" + goPath;
                parent = parent.parent;
            }
            return goPath;
        }

        /// <summary>
        /// 创建 GameObject 的基本信息对象
        /// </summary>
        public static GameObjectInfo CreateGameObjectInfo(GameObject go)
        {
            if (go == null) return null;
            
            return new GameObjectInfo
            {
                name = go.name,
                path = GetGameObjectPath(go),
                instanceId = go.GetInstanceID(),
                activeSelf = go.activeSelf,
                activeInHierarchy = go.activeInHierarchy
            };
        }
    }

    /// <summary>
    /// GameObject 基本信息
    /// </summary>
    [System.Serializable]
    public class GameObjectInfo
    {
        public string name;
        public string path;
        public int instanceId;
        public bool activeSelf;
        public bool activeInHierarchy;
    }
}
