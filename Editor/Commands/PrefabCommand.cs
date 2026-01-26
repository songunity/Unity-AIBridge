using System;
using UnityEditor;
using UnityEngine;

namespace AIBridge.Editor
{
    /// <summary>
    /// Prefab operations: instantiate, save, unpack
    /// Supports multiple sub-commands via "action" parameter
    /// </summary>
    public class PrefabCommand : ICommand
    {
        public string Type => "prefab";
        public bool RequiresRefresh => true;

        public CommandResult Execute(CommandRequest request)
        {
            var action = request.GetParam("action", "instantiate");

            try
            {
                switch (action.ToLower())
                {
                    case "instantiate":
                        return InstantiatePrefab(request);
                    case "save":
                        return SaveAsPrefab(request);
                    case "unpack":
                        return UnpackPrefab(request);
                    case "get_info":
                        return GetPrefabInfo(request);
                    case "apply":
                        return ApplyPrefab(request);
                    default:
                        return CommandResult.Failure(request.id, $"Unknown action: {action}. Supported: instantiate, save, unpack, get_info, apply");
                }
            }
            catch (Exception ex)
            {
                return CommandResult.FromException(request.id, ex);
            }
        }

        private CommandResult InstantiatePrefab(CommandRequest request)
        {
            var prefabPath = request.GetParam<string>("prefabPath");
            if (string.IsNullOrEmpty(prefabPath))
            {
                return CommandResult.Failure(request.id, "Missing 'prefabPath' parameter");
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                return CommandResult.Failure(request.id, $"Prefab not found at path: {prefabPath}");
            }

            // Get optional position
            var posX = request.GetParam("posX", 0f);
            var posY = request.GetParam("posY", 0f);
            var posZ = request.GetParam("posZ", 0f);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.position = new Vector3(posX, posY, posZ);

            // Select the new instance
            Selection.activeGameObject = instance;

            return CommandResult.Success(request.id, new
            {
                prefabPath = prefabPath,
                instanceName = instance.name,
                position = new { x = posX, y = posY, z = posZ }
            });
        }

        private CommandResult SaveAsPrefab(CommandRequest request)
        {
            var gameObjectPath = request.GetParam<string>("gameObjectPath");
            var savePath = request.GetParam<string>("savePath");

            if (string.IsNullOrEmpty(savePath))
            {
                return CommandResult.Failure(request.id, "Missing 'savePath' parameter");
            }

            GameObject go;

            if (!string.IsNullOrEmpty(gameObjectPath))
            {
                go = GameObject.Find(gameObjectPath);
                if (go == null)
                {
                    return CommandResult.Failure(request.id, $"GameObject not found: {gameObjectPath}");
                }
            }
            else
            {
                go = Selection.activeGameObject;
                if (go == null)
                {
                    return CommandResult.Failure(request.id, "No GameObject selected and no 'gameObjectPath' provided");
                }
            }

            // Ensure path ends with .prefab
            if (!savePath.EndsWith(".prefab"))
            {
                savePath += ".prefab";
            }

            var savedPrefab = PrefabUtility.SaveAsPrefabAsset(go, savePath);

            // Refresh AssetDatabase to ensure changes are visible
            AssetDatabase.Refresh();

            return CommandResult.Success(request.id, new
            {
                gameObjectName = go.name,
                prefabPath = savePath,
                saved = savedPrefab != null
            });
        }

        private CommandResult UnpackPrefab(CommandRequest request)
        {
            var gameObjectPath = request.GetParam<string>("gameObjectPath", null);
            var completely = request.GetParam("completely", false);

            GameObject go;

            if (!string.IsNullOrEmpty(gameObjectPath))
            {
                go = GameObject.Find(gameObjectPath);
                if (go == null)
                {
                    return CommandResult.Failure(request.id, $"GameObject not found: {gameObjectPath}");
                }
            }
            else
            {
                go = Selection.activeGameObject;
                if (go == null)
                {
                    return CommandResult.Failure(request.id, "No GameObject selected and no 'gameObjectPath' provided");
                }
            }

            if (!PrefabUtility.IsPartOfAnyPrefab(go))
            {
                return CommandResult.Failure(request.id, $"GameObject '{go.name}' is not part of a prefab");
            }

            var mode = completely
                ? PrefabUnpackMode.Completely
                : PrefabUnpackMode.OutermostRoot;

            PrefabUtility.UnpackPrefabInstance(go, mode, InteractionMode.AutomatedAction);

            return CommandResult.Success(request.id, new
            {
                gameObjectName = go.name,
                unpacked = true,
                completely = completely
            });
        }

        private CommandResult GetPrefabInfo(CommandRequest request)
        {
            var prefabPath = request.GetParam<string>("prefabPath", null);
            var gameObjectPath = request.GetParam<string>("gameObjectPath", null);

            GameObject target = null;

            if (!string.IsNullOrEmpty(prefabPath))
            {
                target = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (target == null)
                {
                    return CommandResult.Failure(request.id, $"Prefab not found at path: {prefabPath}");
                }
            }
            else if (!string.IsNullOrEmpty(gameObjectPath))
            {
                target = GameObject.Find(gameObjectPath);
                if (target == null)
                {
                    return CommandResult.Failure(request.id, $"GameObject not found: {gameObjectPath}");
                }
            }
            else
            {
                target = Selection.activeGameObject;
                if (target == null)
                {
                    return CommandResult.Failure(request.id, "No target specified. Provide 'prefabPath', 'gameObjectPath', or select a GameObject");
                }
            }

            var isPrefabAsset = PrefabUtility.IsPartOfPrefabAsset(target);
            var isPrefabInstance = PrefabUtility.IsPartOfPrefabInstance(target);
            var prefabAssetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(target);
            var prefabType = PrefabUtility.GetPrefabAssetType(target).ToString();
            var prefabStatus = PrefabUtility.GetPrefabInstanceStatus(target).ToString();

            return CommandResult.Success(request.id, new
            {
                name = target.name,
                isPrefabAsset = isPrefabAsset,
                isPrefabInstance = isPrefabInstance,
                prefabAssetPath = prefabAssetPath,
                prefabType = prefabType,
                prefabStatus = prefabStatus
            });
        }

        private CommandResult ApplyPrefab(CommandRequest request)
        {
            var gameObjectPath = request.GetParam<string>("gameObjectPath", null);

            GameObject go;

            if (!string.IsNullOrEmpty(gameObjectPath))
            {
                go = GameObject.Find(gameObjectPath);
                if (go == null)
                {
                    return CommandResult.Failure(request.id, $"GameObject not found: {gameObjectPath}");
                }
            }
            else
            {
                go = Selection.activeGameObject;
                if (go == null)
                {
                    return CommandResult.Failure(request.id, "No GameObject selected and no 'gameObjectPath' provided");
                }
            }

            if (!PrefabUtility.IsPartOfPrefabInstance(go))
            {
                return CommandResult.Failure(request.id, $"GameObject '{go.name}' is not a prefab instance");
            }

            var prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
            PrefabUtility.ApplyPrefabInstance(go, InteractionMode.AutomatedAction);

            // Refresh AssetDatabase to ensure changes are visible
            AssetDatabase.Refresh();

            return CommandResult.Success(request.id, new
            {
                gameObjectName = go.name,
                prefabPath = prefabPath,
                applied = true
            });
        }
    }
}
