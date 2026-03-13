using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AIBridge.Editor
{
    public static class ScreenshotCommand
    {
        [AIBridge("Capture a screenshot of the Game view",
            "AIBridgeCLI ScreenshotCommand_Image")]
        public static IEnumerator Image()
        {
            ScreenshotResult result = null;
            yield return ScreenshotHelper.CaptureGameView(r => { result = r; });
            if (!result.Success)
            {
                yield return CommandResult.Failure(result.Error);
            }

            yield return CommandResult.Success(new
            {
                action = "game",
                imagePath = result.ImagePath,
                width = result.Width,
                height = result.Height,
                timestamp = result.Timestamp,
                filename = result.Filename
            });
        }

        [AIBridge("Capture multiple screenshots and combine into GIF, need least 15s timeout",
            "AIBridgeCLI ScreenshotCommand_Gif --frameCount 30 --fps 15")]
        public static IEnumerator Gif(
            [Description("Number of frames to capture (1-200)")]
            int frameCount = 30,
            [Description("Delay between frames in seconds (0.1-2.0)")]
            float delay = 0.1f,
            [Description("Scale factor (0.25-1.0)")]
            float scale = 0.5f,
            [Description("Color count (64-256)")] int colorCount = 128,
            [Description("FPS for GIF playback (10-30)")]
            int fps = 15)
        {
            if (frameCount <= 0)
            {
                yield return CommandResult.Failure("Parameter 'frameCount' must be > 0");
                yield break;
            }

            frameCount = Mathf.Clamp(frameCount, 1, 200);
            delay = Mathf.Clamp(delay, 0.1f, 2.0f);

            // Create temp directory for frames
            string tempDir = Path.Combine(Path.GetTempPath(), $"aibridge_frames_{Guid.NewGuid():N}");
            try
            {
                Directory.CreateDirectory(tempDir);

                var framePaths = new List<string>();

                // Capture frames one by one
                for (int i = 0; i < frameCount; i++)
                {
                    // Capture screenshot
                    ScreenshotResult result = null;
                    yield return ScreenshotHelper.CaptureGameView(r => { result = r; });

                    if (!result.Success)
                    {
                        yield return CommandResult.Failure($"Failed to capture frame {i + 1}: {result.Error}");
                        yield break;
                    }

                    // Copy to temp directory with frame number
                    string framePath = Path.Combine(tempDir, $"frame_{i:D4}.png");
                    File.Copy(result.ImagePath, framePath, true);
                    framePaths.Add(framePath);

                    // Delete original
                    try
                    {
                        File.Delete(result.ImagePath);
                    }
                    catch
                    {
                    }

                    // Show progress
                    if (i % 5 == 0)
                    {
                        EditorUtility.DisplayProgressBar(
                            "Capturing Frames",
                            $"Frame {i + 1}/{frameCount}",
                            (float)(i + 1) / frameCount);
                    }

                    // Wait before next frame (skip on last frame)
                    if (i < frameCount - 1)
                    {
                        yield return new WaitForSeconds(delay);
                    }
                }

                EditorUtility.ClearProgressBar();

                // Create GIF from captured frames
                EditorUtility.DisplayProgressBar("Creating GIF", "Converting frames to GIF...", 0.5f);
                var gifResult = ScreenshotHelper.ConvertFramesToGif(framePaths, scale, fps, colorCount);

                // Cleanup temp frames
                try
                {
                    if (Directory.Exists(tempDir))
                        Directory.Delete(tempDir, true);
                }
                catch
                {
                }

                EditorUtility.ClearProgressBar();

                if (!gifResult.Success)
                {
                    Debug.LogError($"[AIBridge] GIF conversion failed: {gifResult.Error}");
                    yield return CommandResult.Failure(gifResult.Error);
                    yield break;
                }

                AIBridgeLogger.LogInfo(
                    $"GIF created: {gifResult.GifPath} ({gifResult.FileSize / 1024}KB, {gifResult.FrameCount} frames)");

                yield return CommandResult.Success(new
                {
                    action = "gif",
                    gifPath = gifResult.GifPath,
                    filename = gifResult.Filename,
                    frameCount = gifResult.FrameCount,
                    width = gifResult.Width,
                    height = gifResult.Height,
                    duration = gifResult.Duration,
                    fileSize = gifResult.FileSize,
                    timestamp = gifResult.Timestamp
                });
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                // Cleanup temp directory on error
                if (Directory.Exists(tempDir))
                {
                    try
                    {
                        Directory.Delete(tempDir, true);
                    }
                    catch
                    {
                    }
                }
            }
        }
    }
}
