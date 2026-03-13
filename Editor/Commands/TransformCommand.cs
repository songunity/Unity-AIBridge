using System.Collections;
using System.ComponentModel;
using UnityEditor;
using UnityEngine;

namespace AIBridge.Editor
{
    public static class TransformCommand
    {
        [AIBridge("Get Transform data of a GameObject",
            "AIBridgeCLI TransformCommand_Get --path \"Player\"")]
        public static IEnumerator Get(
            [Description("Hierarchy path of the GameObject")] string path = null,
            [Description("Instance ID of the GameObject")] int instanceId = 0)
        {
            var t = GetTargetTransform(path, instanceId);
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

        [AIBridge("Set position of a GameObject",
            "AIBridgeCLI TransformCommand_SetPosition --path \"Player\" --x 0 --y 1 --z 0")]
        public static IEnumerator SetPosition(
            [Description("Hierarchy path")] string path = null,
            [Description("Instance ID")] int instanceId = 0,
            [Description("X coordinate (omit to keep current)")] float x = float.NaN,
            [Description("Y coordinate (omit to keep current)")] float y = float.NaN,
            [Description("Z coordinate (omit to keep current)")] float z = float.NaN,
            [Description("Use local space")] bool local = false)
        {
            var t = GetTargetTransform(path, instanceId);
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
            var t = GetTargetTransform(path, instanceId);
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

        [AIBridge("Set scale of a GameObject",
            "AIBridgeCLI TransformCommand_SetScale --path \"Player\" --uniform 2")]
        public static IEnumerator SetScale(
            [Description("Hierarchy path")] string path = null,
            [Description("Instance ID")] int instanceId = 0,
            [Description("X scale (omit to keep current)")] float x = float.NaN,
            [Description("Y scale (omit to keep current)")] float y = float.NaN,
            [Description("Z scale (omit to keep current)")] float z = float.NaN,
            [Description("Uniform scale for all axes")] float uniform = float.NaN)
        {
            var t = GetTargetTransform(path, instanceId);
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

        [AIBridge("Set parent of a GameObject",
            "AIBridgeCLI TransformCommand_SetParent --path \"Child\" --parentPath \"Parent\"")]
        public static IEnumerator SetParent(
            [Description("Hierarchy path of the child")] string path = null,
            [Description("Instance ID of the child")] int instanceId = 0,
            [Description("Hierarchy path of the new parent (empty to unparent)")] string parentPath = null,
            [Description("Instance ID of the new parent")] int parentInstanceId = 0,
            [Description("Keep world position after reparenting")] bool worldPositionStays = true)
        {
            var t = GetTargetTransform(path, instanceId);
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

        [AIBridge("Make a GameObject look at a target position",
            "AIBridgeCLI TransformCommand_LookAt --path \"Player\" --targetX 0 --targetY 0 --targetZ 10")]
        public static IEnumerator LookAt(
            [Description("Hierarchy path")] string path = null,
            [Description("Instance ID")] int instanceId = 0,
            [Description("Target X coordinate")] float targetX = float.NaN,
            [Description("Target Y coordinate")] float targetY = float.NaN,
            [Description("Target Z coordinate")] float targetZ = float.NaN)
        {
            var t = GetTargetTransform(path, instanceId);
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

        [AIBridge("Reset Transform to default values",
            "AIBridgeCLI TransformCommand_Reset --path \"Player\"")]
        public static IEnumerator Reset(
            [Description("Hierarchy path")] string path = null,
            [Description("Instance ID")] int instanceId = 0,
            [Description("Reset position")] bool position = true,
            [Description("Reset rotation")] bool rotation = true,
            [Description("Reset scale")] bool scale = true)
        {
            var t = GetTargetTransform(path, instanceId);
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

        private static Transform GetTargetTransform(string path, int instanceId)
        {
            if (instanceId != 0)
            {
                var go = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
                return go?.transform;
            }
            if (!string.IsNullOrEmpty(path))
            {
                var go = GameObject.Find(path);
                return go?.transform;
            }
            return Selection.activeTransform;
        }
    }
}
