using System.Collections;
using System.ComponentModel;
using UnityEditor;
using UnityEngine;

namespace AIBridge.Editor
{
    public static class TransformCommand
    {
        [AIBridge("获取 GameObject 的 Transform 数据",
            "AIBridgeCLI TransformCommand_Get --path \"Player\"")]
        public static IEnumerator Get(
            [Description("GameObject 的层级路径")] string path = null,
            [Description("GameObject 的实例 ID")] int instanceId = 0)
        {
            var go = GameObjectHelper.GetTargetGameObject(path, instanceId);
            var t = go?.transform ?? Selection.activeTransform;
            if (t == null)
            {
                yield return CommandResult.Failure("Transform not found");
                yield break;
            }

            yield return CommandResult.Success(new
            {
                name = t.name,
                position = new { x = t.position.x, y = t.position.y, z = t.position.z },
                localPosition = new { x = t.localPosition.x, y = t.localPosition.y, z = t.localPosition.z },
                rotation = new { x = t.eulerAngles.x, y = t.eulerAngles.y, z = t.eulerAngles.z },
                localRotation = new { x = t.localEulerAngles.x, y = t.localEulerAngles.y, z = t.localEulerAngles.z },
                localScale = new { x = t.localScale.x, y = t.localScale.y, z = t.localScale.z },
                parent = t.parent?.name,
                childCount = t.childCount
            });
        }

        [AIBridge("设置 GameObject 的位置",
            "AIBridgeCLI TransformCommand_SetPosition --path \"Player\" --x 0 --y 1 --z 0")]
        public static IEnumerator SetPosition(
            [Description("层级路径")] string path = null,
            [Description("实例 ID")] int instanceId = 0,
            [Description("X 坐标（省略则保持当前值）")] float x = float.NaN,
            [Description("Y 坐标（省略则保持当前值）")] float y = float.NaN,
            [Description("Z 坐标（省略则保持当前值）")] float z = float.NaN,
            [Description("使用本地空间")] bool local = false)
        {
            var go = GameObjectHelper.GetTargetGameObject(path, instanceId);
            var t = go?.transform;
            if (t == null)
            {
                yield return CommandResult.Failure("Transform not found");
                yield break;
            }

            Undo.RecordObject(t, $"Set Position {t.name}");
            if (local)
            {
                t.localPosition = new Vector3(
                    float.IsNaN(x) ? t.localPosition.x : x,
                    float.IsNaN(y) ? t.localPosition.y : y,
                    float.IsNaN(z) ? t.localPosition.z : z);
            }
            else
            {
                t.position = new Vector3(
                    float.IsNaN(x) ? t.position.x : x,
                    float.IsNaN(y) ? t.position.y : y,
                    float.IsNaN(z) ? t.position.z : z);
            }

            yield return CommandResult.Success(new
            {
                name = t.name,
                position = new { x = t.position.x, y = t.position.y, z = t.position.z },
                localPosition = new { x = t.localPosition.x, y = t.localPosition.y, z = t.localPosition.z }
            });
        }

        [AIBridge("Set rotation of a GameObject (Euler angles)",
            "AIBridgeCLI TransformCommand_SetRotation --path \"Player\" --y 90")]
        public static IEnumerator SetRotation(
            [Description("Hierarchy path")] string path = null,
            [Description("Instance ID")] int instanceId = 0,
            [Description("X euler angle (omit to keep current)")] float x = float.NaN,
            [Description("Y euler angle (omit to keep current)")] float y = float.NaN,
            [Description("Z euler angle (omit to keep current)")] float z = float.NaN,
            [Description("Use local space")] bool local = false)
        {
            var go = GameObjectHelper.GetTargetGameObject(path, instanceId);
            var t = go?.transform;
            if (t == null)
            {
                yield return CommandResult.Failure("Transform not found");
                yield break;
            }

            Undo.RecordObject(t, $"Set Rotation {t.name}");
            if (local)
            {
                t.localEulerAngles = new Vector3(
                    float.IsNaN(x) ? t.localEulerAngles.x : x,
                    float.IsNaN(y) ? t.localEulerAngles.y : y,
                    float.IsNaN(z) ? t.localEulerAngles.z : z);
            }
            else
            {
                t.eulerAngles = new Vector3(
                    float.IsNaN(x) ? t.eulerAngles.x : x,
                    float.IsNaN(y) ? t.eulerAngles.y : y,
                    float.IsNaN(z) ? t.eulerAngles.z : z);
            }

            yield return CommandResult.Success(new
            {
                name = t.name,
                rotation = new { x = t.eulerAngles.x, y = t.eulerAngles.y, z = t.eulerAngles.z }
            });
        }

        [AIBridge("设置 GameObject 的缩放",
            "AIBridgeCLI TransformCommand_SetScale --path \"Player\" --uniform 2")]
        public static IEnumerator SetScale(
            [Description("层级路径")] string path = null,
            [Description("实例 ID")] int instanceId = 0,
            [Description("X 缩放（省略则保持当前值）")] float x = float.NaN,
            [Description("Y 缩放（省略则保持当前值）")] float y = float.NaN,
            [Description("Z 缩放（省略则保持当前值）")] float z = float.NaN,
            [Description("统一缩放所有轴")] float uniform = float.NaN)
        {
            var go = GameObjectHelper.GetTargetGameObject(path, instanceId);
            var t = go?.transform;
            if (t == null)
            {
                yield return CommandResult.Failure("Transform not found");
                yield break;
            }

            Undo.RecordObject(t, $"Set Scale {t.name}");
            if (!float.IsNaN(uniform))
            {
                t.localScale = new Vector3(uniform, uniform, uniform);
            }
            else
            {
                t.localScale = new Vector3(
                    float.IsNaN(x) ? t.localScale.x : x,
                    float.IsNaN(y) ? t.localScale.y : y,
                    float.IsNaN(z) ? t.localScale.z : z);
            }

            yield return CommandResult.Success(new
            {
                name = t.name,
                localScale = new { x = t.localScale.x, y = t.localScale.y, z = t.localScale.z }
            });
        }

        [AIBridge("设置 GameObject 的父级",
            "AIBridgeCLI TransformCommand_SetParent --path \"Child\" --parentPath \"Parent\"")]
        public static IEnumerator SetParent(
            [Description("子对象的层级路径")] string path = null,
            [Description("子对象的实例 ID")] int instanceId = 0,
            [Description("新父级的层级路径（空表示取消父级）")] string parentPath = null,
            [Description("新父级的实例 ID")] int parentInstanceId = 0,
            [Description("重新设置父级后保持世界位置")] bool worldPositionStays = true)
        {
            var go = GameObjectHelper.GetTargetGameObject(path, instanceId);
            var t = go?.transform;
            if (t == null)
            {
                yield return CommandResult.Failure("Transform not found");
                yield break;
            }

            Transform newParent = null;
            if (parentInstanceId != 0)
            {
                var parentGo = EditorUtility.InstanceIDToObject(parentInstanceId) as GameObject;
                newParent = parentGo?.transform;
            }
            else if (!string.IsNullOrEmpty(parentPath))
            {
                var parentGo = GameObject.Find(parentPath);
                newParent = parentGo?.transform;
            }

            Undo.SetTransformParent(t, newParent, $"Set Parent {t.name}");
            t.SetParent(newParent, worldPositionStays);

            yield return CommandResult.Success(new
            {
                name = t.name,
                parent = t.parent?.name
            });
        }

        [AIBridge("使 GameObject 朝向目标位置",
            "AIBridgeCLI TransformCommand_LookAt --path \"Player\" --targetX 0 --targetY 0 --targetZ 10")]
        public static IEnumerator LookAt(
            [Description("层级路径")] string path = null,
            [Description("实例 ID")] int instanceId = 0,
            [Description("目标 X 坐标")] float targetX = float.NaN,
            [Description("目标 Y 坐标")] float targetY = float.NaN,
            [Description("目标 Z 坐标")] float targetZ = float.NaN)
        {
            var go = GameObjectHelper.GetTargetGameObject(path, instanceId);
            var t = go?.transform;
            if (t == null)
            {
                yield return CommandResult.Failure("Transform not found");
                yield break;
            }
            if (float.IsNaN(targetX) || float.IsNaN(targetY) || float.IsNaN(targetZ))
            {
                yield return CommandResult.Failure("Missing target coordinates (targetX, targetY, targetZ)");
                yield break;
            }

            Undo.RecordObject(t, $"LookAt {t.name}");
            t.LookAt(new Vector3(targetX, targetY, targetZ));

            yield return CommandResult.Success(new
            {
                name = t.name,
                rotation = new { x = t.eulerAngles.x, y = t.eulerAngles.y, z = t.eulerAngles.z }
            });
        }

        [AIBridge("重置 Transform 为默认值",
            "AIBridgeCLI TransformCommand_Reset --path \"Player\"")]
        public static IEnumerator Reset(
            [Description("层级路径")] string path = null,
            [Description("实例 ID")] int instanceId = 0,
            [Description("重置位置")] bool position = true,
            [Description("重置旋转")] bool rotation = true,
            [Description("重置缩放")] bool scale = true)
        {
            var go = GameObjectHelper.GetTargetGameObject(path, instanceId);
            var t = go?.transform;
            if (t == null)
            {
                yield return CommandResult.Failure("Transform not found");
                yield break;
            }

            Undo.RecordObject(t, $"Reset Transform {t.name}");
            if (position) t.localPosition = Vector3.zero;
            if (rotation) t.localRotation = Quaternion.identity;
            if (scale) t.localScale = Vector3.one;

            yield return CommandResult.Success(new
            {
                name = t.name,
                localPosition = new { x = t.localPosition.x, y = t.localPosition.y, z = t.localPosition.z },
                localRotation = new { x = t.localEulerAngles.x, y = t.localEulerAngles.y, z = t.localEulerAngles.z },
                localScale = new { x = t.localScale.x, y = t.localScale.y, z = t.localScale.z }
            });
        }
    }
}
