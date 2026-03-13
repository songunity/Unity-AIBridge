using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AIBridge.Editor
{
    /// <summary>
    /// 输入模拟命令，支持点击、拖拽、长按。
    /// </summary>
    public static class InputSimulationCommand
    {
        [AIBridge("Simulate click on a GameObject by path",
            "AIBridgeCLI InputSimulationCommand_Click --path \"Canvas/Button\"")]
        public static IEnumerator Click(
            [Description("Hierarchy path of the GameObject")] string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                yield return CommandResult.Failure("参数 'path' 不能为空");
                yield break;
            }

            var go = GameObject.Find(path);
            if (go == null)
            {
                yield return CommandResult.Failure($"找不到 GameObject: {path}");
                yield break;
            }

            yield return PerformClick(go);
        }

        [AIBridge("Simulate click on a GameObject by instance ID",
            "AIBridgeCLI InputSimulationCommand_ClickByInstanceId --instanceId 12345")]
        public static IEnumerator ClickByInstanceId(
            [Description("Instance ID of the GameObject")] int instanceId)
        {
            var go = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
            if (go == null)
            {
                yield return CommandResult.Failure($"找不到 GameObject with instanceId: {instanceId}");
                yield break;
            }

            yield return PerformClick(go);
        }

        private static IEnumerator PerformClick(GameObject go)
        {

            if (!go.activeInHierarchy)
            {
                yield return CommandResult.Failure($"GameObject 未激活");
                yield break;
            }

            var screenPos = GetScreenPosition(go);
            if (screenPos == null)
            {
                yield return CommandResult.Failure($"无法获取屏幕坐标");
                yield break;
            }

            var result = SimulateClick(go, screenPos.Value);
            yield return CommandResult.Success(new
            {
                action = "click",
                path = GameObjectHelper.GetGameObjectPath(go),
                instanceId = go.GetInstanceID(),
                screenPosition = screenPos.Value,
                eventSent = result
            });
        }

        [AIBridge("Simulate click at screen coordinates",
            "AIBridgeCLI InputSimulationCommand_ClickAt --x 100 --y 200")]
        public static IEnumerator ClickAt(
            [Description("Screen X coordinate")] float x,
            [Description("Screen Y coordinate")] float y)
        {
            if (x < 0 || y < 0)
            {
                yield return CommandResult.Failure("参数 'x' 和 'y' 必须为非负数");
                yield break;
            }

            var screenPos = new Vector2(x, y);
            var hitObject = RaycastUI(screenPos);
            var targetName = hitObject != null ? hitObject.name : "none";

            if (hitObject != null)
            {
                SimulateClick(hitObject, screenPos);
            }

            yield return CommandResult.Success(new
            {
                action = "click_at",
                screenPosition = new { x, y },
                hitObject = targetName,
                eventSent = hitObject != null ? "PointerClick" : "NoTarget"
            });
        }

        [AIBridge("Simulate drag from one object to another by path",
            "AIBridgeCLI InputSimulationCommand_Drag --path \"Canvas/Item\" --toPath \"Canvas/Slot\" --frames 10")]
        public static IEnumerator Drag(
            [Description("Source GameObject path")] string path,
            [Description("Target GameObject path (optional)")] string toPath = null,
            [Description("Target screen X coordinate (if toPath not provided)")] float toX = -1,
            [Description("Target screen Y coordinate (if toPath not provided)")] float toY = -1,
            [Description("Number of frames for drag animation")] int frames = 10)
        {
            if (string.IsNullOrEmpty(path))
            {
                yield return CommandResult.Failure("参数 'path' 不能为空");
                yield break;
            }

            var go = GameObject.Find(path);
            if (go == null)
            {
                yield return CommandResult.Failure($"找不到 GameObject: {path}");
                yield break;
            }

            var startPos = GetScreenPosition(go);
            if (startPos == null)
            {
                yield return CommandResult.Failure($"无法获取起始屏幕坐标: {path}");
                yield break;
            }

            Vector2 endPos;
            if (!string.IsNullOrEmpty(toPath))
            {
                var toGo = GameObject.Find(toPath);
                if (toGo == null)
                {
                    yield return CommandResult.Failure($"找不到目标 GameObject: {toPath}");
                    yield break;
                }
                var toScreenPos = GetScreenPosition(toGo);
                if (toScreenPos == null)
                {
                    yield return CommandResult.Failure($"无法获取目标屏幕坐标: {toPath}");
                    yield break;
                }
                endPos = toScreenPos.Value;
            }
            else
            {
                if (toX < 0 || toY < 0)
                {
                    yield return CommandResult.Failure("拖拽需要 'toPath' 或 'toX'+'toY' 参数");
                    yield break;
                }
                endPos = new Vector2(toX, toY);
            }

            yield return PerformDrag(go, startPos.Value, endPos, frames);
        }

        [AIBridge("Simulate drag from one object to another by instance ID",
            "AIBridgeCLI InputSimulationCommand_DragByInstanceId --instanceId 12345 --toInstanceId 67890 --frames 10")]
        public static IEnumerator DragByInstanceId(
            [Description("Source GameObject instance ID")] int instanceId,
            [Description("Target GameObject instance ID (optional)")] int toInstanceId = 0,
            [Description("Target screen X coordinate (if toInstanceId not provided)")] float toX = -1,
            [Description("Target screen Y coordinate (if toInstanceId not provided)")] float toY = -1,
            [Description("Number of frames for drag animation")] int frames = 10)
        {
            var go = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
            if (go == null)
            {
                yield return CommandResult.Failure($"找不到 GameObject with instanceId: {instanceId}");
                yield break;
            }

            var startPos = GetScreenPosition(go);
            if (startPos == null)
            {
                yield return CommandResult.Failure($"无法获取起始屏幕坐标");
                yield break;
            }

            Vector2 endPos;
            if (toInstanceId != 0)
            {
                var toGo = EditorUtility.InstanceIDToObject(toInstanceId) as GameObject;
                if (toGo == null)
                {
                    yield return CommandResult.Failure($"找不到目标 GameObject with instanceId: {toInstanceId}");
                    yield break;
                }
                var toScreenPos = GetScreenPosition(toGo);
                if (toScreenPos == null)
                {
                    yield return CommandResult.Failure($"无法获取目标屏幕坐标");
                    yield break;
                }
                endPos = toScreenPos.Value;
            }
            else
            {
                if (toX < 0 || toY < 0)
                {
                    yield return CommandResult.Failure("拖拽需要 'toInstanceId' 或 'toX'+'toY' 参数");
                    yield break;
                }
                endPos = new Vector2(toX, toY);
            }

            yield return PerformDrag(go, startPos.Value, endPos, frames);
        }

        private static IEnumerator PerformDrag(GameObject go, Vector2 startPos, Vector2 endPos, int frames)
        {

            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                yield return CommandResult.Failure("场景中没有 EventSystem");
                yield break;
            }

            var pointerData = new PointerEventData(eventSystem)
            {
                position = startPos,
                button = PointerEventData.InputButton.Left
            };

            ExecuteEvents.Execute(go, pointerData, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(go, pointerData, ExecuteEvents.beginDragHandler);

            frames = Mathf.Clamp(frames, 3, 60);

            for (int i = 0; i <= frames; i++)
            {
                float t = i / (float)frames;
                pointerData.position = Vector2.Lerp(startPos, endPos, t);
                ExecuteEvents.Execute(go, pointerData, ExecuteEvents.dragHandler);
                yield return null;
            }

            pointerData.position = endPos;
            ExecuteEvents.Execute(go, pointerData, ExecuteEvents.endDragHandler);
            ExecuteEvents.Execute(go, pointerData, ExecuteEvents.pointerUpHandler);

            var dropTarget = RaycastUI(endPos);
            if (dropTarget != null)
            {
                ExecuteEvents.Execute(dropTarget, pointerData, ExecuteEvents.dropHandler);
            }

            yield return CommandResult.Success(new
            {
                action = "drag",
                from = startPos,
                to = endPos,
                frames,
                dropTarget = dropTarget != null ? dropTarget.name : "none"
            });
        }

        [AIBridge("Simulate long press on a GameObject by path",
            "AIBridgeCLI InputSimulationCommand_LongPress --path \"Canvas/Button\" --duration 1000")]
        public static IEnumerator LongPress(
            [Description("Hierarchy path of the GameObject")] string path,
            [Description("Press duration in milliseconds")] int duration = 1000)
        {
            if (string.IsNullOrEmpty(path))
            {
                yield return CommandResult.Failure("参数 'path' 不能为空");
                yield break;
            }

            var go = GameObject.Find(path);
            if (go == null)
            {
                yield return CommandResult.Failure($"找不到 GameObject: {path}");
                yield break;
            }

            yield return PerformLongPress(go, duration);
        }

        [AIBridge("Simulate long press on a GameObject by instance ID",
            "AIBridgeCLI InputSimulationCommand_LongPressByInstanceId --instanceId 12345 --duration 1000")]
        public static IEnumerator LongPressByInstanceId(
            [Description("Instance ID of the GameObject")] int instanceId,
            [Description("Press duration in milliseconds")] int duration = 1000)
        {
            var go = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
            if (go == null)
            {
                yield return CommandResult.Failure($"找不到 GameObject with instanceId: {instanceId}");
                yield break;
            }

            yield return PerformLongPress(go, duration);
        }

        private static IEnumerator PerformLongPress(GameObject go, int duration)
        {

            var screenPos = GetScreenPosition(go);
            if (screenPos == null)
            {
                yield return CommandResult.Failure($"无法获取屏幕坐标");
                yield break;
            }

            float durationSec = duration / 1000f;

            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                yield return CommandResult.Failure("场景中没有 EventSystem");
                yield break;
            }

            var pointerData = new PointerEventData(eventSystem)
            {
                position = screenPos.Value,
                button = PointerEventData.InputButton.Left
            };

            ExecuteEvents.Execute(go, pointerData, ExecuteEvents.pointerDownHandler);

            yield return new WaitForSeconds(durationSec);

            ExecuteEvents.Execute(go, pointerData, ExecuteEvents.pointerUpHandler);

            yield return CommandResult.Success(new
            {
                action = "long_press",
                path = GameObjectHelper.GetGameObjectPath(go),
                instanceId = go.GetInstanceID(),
                screenPosition = screenPos.Value,
                durationMs = duration
            });
        }

        #region Private Helper Methods

        private static string SimulateClick(GameObject target, Vector2 screenPos)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return "NoEventSystem";
            }

            var pointerData = new PointerEventData(eventSystem)
            {
                position = screenPos,
                button = PointerEventData.InputButton.Left,
                clickCount = 1
            };

            var raycastResults = new List<RaycastResult>();
            eventSystem.RaycastAll(pointerData, raycastResults);
            if (raycastResults.Count > 0)
            {
                pointerData.pointerCurrentRaycast = raycastResults[0];
            }

            ExecuteEvents.Execute(target, pointerData, ExecuteEvents.pointerEnterHandler);
            ExecuteEvents.Execute(target, pointerData, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(target, pointerData, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(target, pointerData, ExecuteEvents.pointerClickHandler);

            return "PointerClick";
        }

        private static Vector2? GetScreenPosition(GameObject go)
        {
            var rectTransform = go.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                var canvas = go.GetComponentInParent<Canvas>();
                if (canvas != null)
                {
                    var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                    return RectTransformUtility.WorldToScreenPoint(cam, rectTransform.position);
                }
            }

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    Vector3 screenPoint = cam.WorldToScreenPoint(renderer.bounds.center);
                    return new Vector2(screenPoint.x, screenPoint.y);
                }
            }

            if (go.transform != null)
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    Vector3 screenPoint = cam.WorldToScreenPoint(go.transform.position);
                    return new Vector2(screenPoint.x, screenPoint.y);
                }
            }

            return null;
        }

        private static GameObject RaycastUI(Vector2 screenPos)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return null;

            var pointerData = new PointerEventData(eventSystem) { position = screenPos };
            var results = new List<RaycastResult>();
            eventSystem.RaycastAll(pointerData, results);

            return results.Count > 0 ? results[0].gameObject : null;
        }

        #endregion
    }
}
