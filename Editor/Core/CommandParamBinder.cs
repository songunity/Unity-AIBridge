using System;
using System.Collections.Generic;
using System.Reflection;

namespace AIBridge.Editor
{
    public static class CommandParamBinder
    {
        /// <summary>
        /// 从 request.params 按 ParameterInfo 顺序提取并转型参数
        /// </summary>
        public static bool TryBind(CommandEntry entry, CommandRequest request, out object[] args, out string error)
        {
            var paramList = new List<object>();
            foreach (var param in entry.Parameters)
            {
                if (request.@params != null && request.@params.TryGetValue(param.Name, out var raw))
                {
                    try
                    {
                        object converted;
                        if (raw == null)
                            converted = null;
                        else if (param.ParameterType.IsAssignableFrom(raw.GetType()))
                            converted = raw;
                        else
                            converted = Convert.ChangeType(raw, param.ParameterType);
                        paramList.Add(converted);
                    }
                    catch
                    {
                        args = null;
                        error = $"Parameter '{param.Name}' cannot be converted to {param.ParameterType.Name}";
                        return false;
                    }
                }
                else if (param.HasDefaultValue)
                {
                    paramList.Add(param.DefaultValue);
                }
                else
                {
                    args = null;
                    error = $"Required parameter '{param.Name}' is missing";
                    return false;
                }
            }
            args = paramList.ToArray();
            error = null;
            return true;
        }
    }
}
