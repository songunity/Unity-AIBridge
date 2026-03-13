using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace AIBridge.Editor
{
    [InitializeOnLoad]
    public static class EditorCoroutineRunner
    {
        private class CoroutineHandle
        {
            public IEnumerator Coroutine;
            public Action<CommandResult> OnComplete;
            public string RequestId;
            public Stopwatch Timer;
            public IEnumerator CurrentEnumerator;

            private double _waitStartTime;
            private float _waitDuration;

            public bool Step()
            {
                try
                {
                    var currentCoroutine = CurrentEnumerator ?? Coroutine;

                    while (true)
                    {
                        if (_waitDuration > 0)
                        {
                            double elapsed = EditorApplication.timeSinceStartup - _waitStartTime;
                            if (elapsed < _waitDuration)
                            {
                                return false;
                            }
                            _waitDuration = 0;
                        }

                        if (!currentCoroutine.MoveNext())
                        {
                            if (CurrentEnumerator != null)
                            {
                                CurrentEnumerator = null;
                                return false;
                            }

                            var result = CommandResult.SuccessWithId(RequestId);
                            result.executionTime = Timer.ElapsedMilliseconds;
                            OnComplete?.Invoke(result);
                            return true;
                        }

                        var current = currentCoroutine.Current;

                        if (current is WaitForSeconds waitSeconds)
                        {
                            var seconds = GetWaitForSecondsValue(waitSeconds);
                            _waitStartTime = EditorApplication.timeSinceStartup;
                            _waitDuration = seconds;
                            if (_waitDuration > 0)
                            {
                                return false;
                            }
                            continue;
                        }

                        if (current is WaitForFixedUpdate || current is WaitForEndOfFrame)
                        {
                            continue;
                        }

                        if (current is CustomYieldInstruction instruction)
                        {
                            if (instruction.keepWaiting)
                            {
                                return false;
                            }
                            continue;
                        }

                        if (current is IEnumerator nestedCoroutine)
                        {
                            CurrentEnumerator = nestedCoroutine;
                            return false;
                        }

                        if (current is CommandResult commandResult)
                        {
                            commandResult.id = RequestId;
                            commandResult.executionTime = Timer.ElapsedMilliseconds;
                            OnComplete?.Invoke(commandResult);
                            return true;
                        }

                        return false;
                    }
                }
                catch (Exception ex)
                {
                    var result = CommandResult.FromException(RequestId, ex);
                    result.executionTime = Timer.ElapsedMilliseconds;
                    OnComplete?.Invoke(result);
                    return true;
                }
            }
        }

        private static readonly List<CoroutineHandle> _running = new List<CoroutineHandle>();
        private static readonly FieldInfo _waitForSecondsField;

        static EditorCoroutineRunner()
        {
            EditorApplication.update += Tick;

            var waitForSecondsType = typeof(WaitForSeconds);
            _waitForSecondsField = waitForSecondsType.GetField("m_Seconds",
                BindingFlags.NonPublic | BindingFlags.Instance);
        }

        private static float GetWaitForSecondsValue(WaitForSeconds waitForSeconds)
        {
            if (_waitForSecondsField != null && _waitForSecondsField.GetValue(waitForSeconds) is float seconds)
            {
                return seconds;
            }
            return 0f;
        }

        public static void Start(IEnumerator coroutine, Action<CommandResult> onComplete, string requestId)
        {
            _running.Add(new CoroutineHandle
            {
                Coroutine = coroutine,
                OnComplete = onComplete,
                RequestId = requestId,
                Timer = Stopwatch.StartNew()
            });
        }

        private static void Tick()
        {
            for (int i = _running.Count - 1; i >= 0; i--)
            {
                if (_running[i].Step())
                    _running.RemoveAt(i);
            }
        }
    }
}
