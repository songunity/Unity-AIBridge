using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace AIBridge.Editor
{
    public static class GameViewCommand
    {
        private static bool _reflectionInitialized;
        private static string _reflectionError;

        private static Type _gameViewType;
        private static Type _gameViewSizesType;
        private static Type _gameViewSizeType;
        private static Type _gameViewSizeGroupType;
        private static Type _gameViewSizeTypeEnum;

        private static MethodInfo _getMainGameView;
        private static PropertyInfo _selectedSizeIndex;
        private static PropertyInfo _sizesInstance;
        private static PropertyInfo _currentGroupType;
        private static PropertyInfo _currentGroupProp;
        private static MethodInfo _getGroup;
        private static MethodInfo _getTotalCount;
        private static MethodInfo _getGameViewSize;
        private static MethodInfo _addCustomSize;
        private static MethodInfo _saveToHardDisk;

        private static PropertyInfo _sizeWidth;
        private static PropertyInfo _sizeHeight;
        private static PropertyInfo _sizeBaseText;
        private static PropertyInfo _sizeSizeType;

        private static object _fixedResolutionValue;

        [AIBridge("获取当前 Game 视图分辨率",
            "AIBridgeCLI GameViewCommand_GetResolution")]
        public static IEnumerator GetResolution()
        {
            if (!TryInitReflection(out var error))
            {
                yield return CommandResult.Failure(error);
                yield break;
            }

            var gameView = GetMainGameView();
            var group = GetCurrentSizeGroup();

            if (group == null)
            {
                yield return CommandResult.Failure("Could not access Game view size group.");
                yield break;
            }

            int selectedIndex = gameView != null ? GetSelectedSizeIndex(gameView) : -1;
            int width = 0, height = 0;
            string name = "";
            string sizeType = "";

            if (selectedIndex >= 0 && selectedIndex < GetTotalCount(group))
            {
                var size = GetGameViewSize(group, selectedIndex);
                width = GetSizeWidth(size);
                height = GetSizeHeight(size);
                name = GetSizeBaseText(size);
                sizeType = GetSizeType(size);
            }
            else if (gameView != null)
            {
                var targetSizeProp = _gameViewType.GetProperty("targetSize",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (targetSizeProp != null)
                {
                    var targetSize = (Vector2)targetSizeProp.GetValue(gameView);
                    width = (int)targetSize.x;
                    height = (int)targetSize.y;
                    name = "Unknown";
                    sizeType = "Unknown";
                }
            }

            if (width <= 0 || height <= 0)
            {
                yield return CommandResult.Failure("No Game view window found. Open the Game view first.");
                yield break;
            }

            yield return CommandResult.Success(new { action = "get_resolution", width, height, name, selectedIndex, sizeType });
        }

        [AIBridge("设置 Game 视图分辨率",
            "AIBridgeCLI GameViewCommand_SetResolution --width 1920 --height 1080")]
        public static IEnumerator SetResolution(
            [Description("分辨率宽度（1-8192）")] int width,
            [Description("分辨率高度（1-8192）")] int height)
        {
            if (!TryInitReflection(out var error))
            {
                yield return CommandResult.Failure(error);
                yield break;
            }

            if (width <= 0 || height <= 0)
            {
                yield return CommandResult.Failure("Parameters 'width' and 'height' must be positive integers.");
                yield break;
            }

            if (width > 8192 || height > 8192)
            {
                yield return CommandResult.Failure($"Resolution {width}x{height} exceeds maximum allowed (8192x8192).");
                yield break;
            }

            var gameView = GetMainGameView();
            if (gameView == null)
            {
                yield return CommandResult.Failure("No Game view window found. Open the Game view first.");
                yield break;
            }

            var group = GetCurrentSizeGroup();
            if (group == null)
            {
                yield return CommandResult.Failure("Could not access Game view size group.");
                yield break;
            }

            int foundIndex = FindResolution(group, width, height);
            bool wasAdded = false;
            string label = $"AIBridge {width}x{height}";

            if (foundIndex < 0)
            {
                foundIndex = AddCustomResolution(group, width, height, label);
                wasAdded = true;
            }
            else
            {
                var size = GetGameViewSize(group, foundIndex);
                label = GetSizeBaseText(size);
                if (string.IsNullOrEmpty(label))
                    label = $"{width}x{height}";
            }

            SetSelectedSizeIndex(gameView, foundIndex);
            ((EditorWindow)gameView).Repaint();

            yield return CommandResult.Success(new { action = "set_resolution", width, height, selectedIndex = foundIndex, wasAdded, label });
        }

        [AIBridge("列出所有可用的 Game 视图分辨率",
            "AIBridgeCLI GameViewCommand_ListResolutions")]
        public static IEnumerator ListResolutions()
        {
            if (!TryInitReflection(out var error))
            {
                yield return CommandResult.Failure(error);
                yield break;
            }

            var group = GetCurrentSizeGroup();
            if (group == null)
            {
                yield return CommandResult.Failure("Could not access Game view size group.");
                yield break;
            }

            int totalCount = GetTotalCount(group);
            var resolutions = new List<object>();

            for (int i = 0; i < totalCount; i++)
            {
                var size = GetGameViewSize(group, i);
                resolutions.Add(new
                {
                    index = i,
                    width = GetSizeWidth(size),
                    height = GetSizeHeight(size),
                    name = GetSizeBaseText(size),
                    sizeType = GetSizeType(size)
                });
            }

            var gameView = GetMainGameView();
            int currentIndex = gameView != null ? GetSelectedSizeIndex(gameView) : -1;

            yield return CommandResult.Success(new { action = "list_resolutions", resolutions, count = totalCount, currentIndex });
        }

        #region Reflection Helpers

        private static bool TryInitReflection(out string error)
        {
            error = null;
            if (_reflectionInitialized)
            {
                error = _reflectionError;
                return _reflectionError == null;
            }

            _reflectionInitialized = true;

            try
            {
                _gameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
                _gameViewSizesType = Type.GetType("UnityEditor.GameViewSizes,UnityEditor");
                _gameViewSizeType = Type.GetType("UnityEditor.GameViewSize,UnityEditor");
                _gameViewSizeGroupType = Type.GetType("UnityEditor.GameViewSizeGroup,UnityEditor");
                _gameViewSizeTypeEnum = Type.GetType("UnityEditor.GameViewSizeType,UnityEditor");

                if (_gameViewType == null || _gameViewSizesType == null ||
                    _gameViewSizeType == null || _gameViewSizeGroupType == null ||
                    _gameViewSizeTypeEnum == null)
                {
                    _reflectionError = "GameView internals not available in this Unity version.";
                    error = _reflectionError;
                    return false;
                }

                var allFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

                _getMainGameView = _gameViewType.GetMethod("GetMainGameView", allFlags)
                    ?? _gameViewType.GetMethod("GetMainPlayModeView", allFlags);
                _selectedSizeIndex = _gameViewType.GetProperty("selectedSizeIndex", allFlags);

                _sizesInstance = _gameViewSizesType.GetProperty("instance", allFlags);
                if (_sizesInstance == null)
                {
                    var baseType = _gameViewSizesType.BaseType;
                    while (baseType != null && _sizesInstance == null)
                    {
                        _sizesInstance = baseType.GetProperty("instance", allFlags);
                        baseType = baseType.BaseType;
                    }
                }

                _currentGroupType = _gameViewSizesType.GetProperty("currentGroupType", allFlags);
                _getGroup = _gameViewSizesType.GetMethod("GetGroup", allFlags);
                _currentGroupProp = _gameViewSizesType.GetProperty("currentGroup", allFlags);
                _saveToHardDisk = _gameViewSizesType.GetMethod("SaveToHDD", BindingFlags.Public | BindingFlags.Instance);

                _getTotalCount = _gameViewSizeGroupType.GetMethod("GetTotalCount", BindingFlags.Public | BindingFlags.Instance);
                _getGameViewSize = _gameViewSizeGroupType.GetMethod("GetGameViewSize", BindingFlags.Public | BindingFlags.Instance);
                _addCustomSize = _gameViewSizeGroupType.GetMethod("AddCustomSize", BindingFlags.Public | BindingFlags.Instance);

                _sizeWidth = _gameViewSizeType.GetProperty("width", BindingFlags.Public | BindingFlags.Instance);
                _sizeHeight = _gameViewSizeType.GetProperty("height", BindingFlags.Public | BindingFlags.Instance);
                _sizeBaseText = _gameViewSizeType.GetProperty("baseText", BindingFlags.Public | BindingFlags.Instance);
                _sizeSizeType = _gameViewSizeType.GetProperty("sizeType", BindingFlags.Public | BindingFlags.Instance);

                _fixedResolutionValue = Enum.Parse(_gameViewSizeTypeEnum, "FixedResolution");

                var missing = new List<string>();
                if (_sizesInstance == null) missing.Add("GameViewSizes.instance");
                if (_getGroup == null && _currentGroupProp == null) missing.Add("GameViewSizes.GetGroup/currentGroup");
                if (_getTotalCount == null) missing.Add("GameViewSizeGroup.GetTotalCount");
                if (_getGameViewSize == null) missing.Add("GameViewSizeGroup.GetGameViewSize");
                if (_sizeWidth == null) missing.Add("GameViewSize.width");
                if (_sizeHeight == null) missing.Add("GameViewSize.height");
                if (_selectedSizeIndex == null) missing.Add("GameView.selectedSizeIndex");

                if (missing.Count > 0)
                {
                    _reflectionError = $"Missing GameView internal members: {string.Join(", ", missing)}. This Unity version may not be supported.";
                    error = _reflectionError;
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _reflectionError = $"Failed to initialize GameView reflection: {ex.Message}";
                error = _reflectionError;
                return false;
            }
        }

        private static object GetSizesInstance()
        {
            return _sizesInstance.GetValue(null);
        }

        private static object GetCurrentSizeGroup()
        {
            var instance = GetSizesInstance();
            if (instance == null) return null;

            if (_currentGroupProp != null)
                return _currentGroupProp.GetValue(instance);

            if (_currentGroupType != null && _getGroup != null)
            {
                var groupType = _currentGroupType.GetValue(null);
                return _getGroup.Invoke(instance, new[] { (object)(int)groupType });
            }

            return null;
        }

        private static object GetMainGameView()
        {
            if (_getMainGameView != null)
                return _getMainGameView.Invoke(null, null);
            var views = Resources.FindObjectsOfTypeAll(_gameViewType);
            return views != null && views.Length > 0 ? views[0] : null;
        }

        private static int GetSelectedSizeIndex(object gameView)
        {
            if (_selectedSizeIndex == null || gameView == null) return -1;
            return (int)_selectedSizeIndex.GetValue(gameView);
        }

        private static void SetSelectedSizeIndex(object gameView, int index)
        {
            if (_selectedSizeIndex != null && gameView != null)
                _selectedSizeIndex.SetValue(gameView, index);
        }

        private static int GetTotalCount(object group)
        {
            return (int)_getTotalCount.Invoke(group, null);
        }

        private static object GetGameViewSize(object group, int index)
        {
            return _getGameViewSize.Invoke(group, new object[] { index });
        }

        private static int GetSizeWidth(object size) => (int)_sizeWidth.GetValue(size);
        private static int GetSizeHeight(object size) => (int)_sizeHeight.GetValue(size);
        private static string GetSizeBaseText(object size) => _sizeBaseText != null ? (string)_sizeBaseText.GetValue(size) : "";
        private static string GetSizeType(object size) => _sizeSizeType != null ? _sizeSizeType.GetValue(size).ToString() : "Unknown";

        private static int FindResolution(object group, int width, int height)
        {
            int totalCount = GetTotalCount(group);
            for (int i = 0; i < totalCount; i++)
            {
                var size = GetGameViewSize(group, i);
                if (GetSizeType(size) == "FixedResolution" && GetSizeWidth(size) == width && GetSizeHeight(size) == height)
                    return i;
            }
            return -1;
        }

        private static int AddCustomResolution(object group, int width, int height, string label)
        {
            var ctor = _gameViewSizeType.GetConstructor(new[] { _gameViewSizeTypeEnum, typeof(int), typeof(int), typeof(string) });

            object newSize;
            if (ctor != null)
            {
                newSize = ctor.Invoke(new[] { _fixedResolutionValue, width, height, label });
            }
            else
            {
                newSize = Activator.CreateInstance(_gameViewSizeType);
                _sizeSizeType?.SetValue(newSize, _fixedResolutionValue);
                _sizeWidth.SetValue(newSize, width);
                _sizeHeight.SetValue(newSize, height);
                _sizeBaseText?.SetValue(newSize, label);
            }

            _addCustomSize.Invoke(group, new[] { newSize });

            if (_saveToHardDisk != null)
            {
                var instance = GetSizesInstance();
                _saveToHardDisk.Invoke(instance, null);
            }

            return GetTotalCount(group) - 1;
        }

        #endregion
    }
}
