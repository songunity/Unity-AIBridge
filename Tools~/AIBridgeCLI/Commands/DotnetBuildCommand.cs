using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace AIBridgeCLI.Commands
{
    /// <summary>
    /// Dotnet build command - CLI-only command that executes dotnet build directly.
    /// Does not require Unity Editor to be running.
    /// Supports intelligent error filtering to show only relevant errors.
    /// Filter configuration is loaded from compile-filter.json (hot-reload on each execution).
    /// </summary>
    public static class DotnetBuildCommand
    {
        // Regex to parse MSBuild error format: path(line,column): error CS0001: message
        private static readonly Regex MsBuildErrorRegex = new Regex(
            @"^\s*(?<file>.+?)\((?<line>\d+),(?<column>\d+)\):\s*(?<type>error|warning)\s+(?<code>\w+):\s*(?<message>.+?)(?:\s*\[(?<project>.+?)\])?$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private const string CONFIG_FILE_NAME = "compile-filter.json";

        /// <summary>
        /// Load filter configuration from compile-filter.json.
        /// Returns default config if file doesn't exist or is invalid.
        /// </summary>
        private static FilterConfig LoadFilterConfig()
        {
            var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, CONFIG_FILE_NAME);

            if (File.Exists(configPath))
            {
                try
                {
                    var json = File.ReadAllText(configPath, Encoding.UTF8);
                    var config = JsonConvert.DeserializeObject<FilterConfig>(json);
                    if (config != null && config.ExcludePaths != null && config.ExcludeCodes != null)
                    {
                        return config;
                    }
                }
                catch
                {
                    // If config file is invalid, use defaults
                }
            }

            return FilterConfig.GetDefault();
        }

        /// <summary>
        /// Execute dotnet build and return filtered results
        /// </summary>
        public static DotnetBuildResult Execute(DotnetBuildOptions options)
        {
            var result = new DotnetBuildResult();

            try
            {
                // Find project root
                var projectRoot = FindProjectRoot(options.Solution);
                if (projectRoot == null)
                {
                    result.Success = false;
                    result.Error = $"Could not find solution file: {options.Solution}";
                    return result;
                }

                var solutionPath = Path.Combine(projectRoot, options.Solution);
                if (!File.Exists(solutionPath))
                {
                    result.Success = false;
                    result.Error = $"Solution file not found: {solutionPath}";
                    return result;
                }

                result.SolutionPath = solutionPath;
                result.ProjectRoot = projectRoot;

                var stopwatch = Stopwatch.StartNew();
                var allErrors = new List<BuildError>();
                var allWarnings = new List<BuildError>();
                var outputBuilder = new StringBuilder();

                var startInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"build \"{solutionPath}\" --configuration {options.Configuration} --verbosity {options.Verbosity} --no-incremental",
                    WorkingDirectory = projectRoot,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using (var process = new Process())
                {
                    process.StartInfo = startInfo;

                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            outputBuilder.AppendLine(e.Data);
                            ParseMsBuildOutput(e.Data, allErrors, allWarnings, projectRoot);
                        }
                    };

                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            outputBuilder.AppendLine(e.Data);
                            ParseMsBuildOutput(e.Data, allErrors, allWarnings, projectRoot);
                        }
                    };

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    var completed = process.WaitForExit(options.TimeoutMs);
                    stopwatch.Stop();

                    if (!completed)
                    {
                        try
                        {
                            process.Kill();
                        }
                        catch
                        {
                            // Ignore kill errors
                        }

                        result.Success = false;
                        result.Error = $"Build timed out after {options.TimeoutMs}ms";
                        return result;
                    }

                    result.ExitCode = process.ExitCode;
                    result.Duration = stopwatch.Elapsed.TotalSeconds;
                    result.RawOutput = outputBuilder.ToString();

                    // Apply filtering
                    if (options.EnableFilter)
                    {
                        // Load filter config from file (hot-reload)
                        var filterConfig = LoadFilterConfig();

                        // Use command-line options if provided, otherwise use config file
                        var excludePaths = options.ExcludePaths ?? filterConfig.ExcludePaths.ToArray();
                        var excludeCodes = options.ExcludeCodes ?? filterConfig.ExcludeCodes.ToArray();
                        var hideWarnings = options.HideWarnings && filterConfig.HideWarnings;

                        result.Errors = FilterErrors(allErrors, excludePaths, excludeCodes, projectRoot);
                        result.Warnings = hideWarnings
                            ? new List<BuildError>()
                            : FilterErrors(allWarnings, excludePaths, excludeCodes, projectRoot);
                        result.FilteredErrorCount = allErrors.Count - result.Errors.Count;
                        result.FilteredWarningCount = hideWarnings
                            ? allWarnings.Count
                            : allWarnings.Count - result.Warnings.Count;
                    }
                    else
                    {
                        result.Errors = allErrors;
                        result.Warnings = options.HideWarnings ? new List<BuildError>() : allWarnings;
                        result.FilteredErrorCount = 0;
                        result.FilteredWarningCount = options.HideWarnings ? allWarnings.Count : 0;
                    }

                    result.TotalErrorCount = allErrors.Count;
                    result.TotalWarningCount = allWarnings.Count;
                    result.Success = result.Errors.Count == 0;
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = $"Failed to run dotnet build: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// Find project root by searching for solution file
        /// </summary>
        private static string FindProjectRoot(string solutionName)
        {
            // Start from CLI exe location
            var exeDir = AppDomain.CurrentDomain.BaseDirectory;
            var currentDir = new DirectoryInfo(exeDir);

            // Search up to 10 levels
            for (int i = 0; i < 10 && currentDir != null; i++)
            {
                var solutionPath = Path.Combine(currentDir.FullName, solutionName);
                if (File.Exists(solutionPath))
                {
                    return currentDir.FullName;
                }
                currentDir = currentDir.Parent;
            }

            // Try common Unity project structure: Tools~/CLI -> Package -> Packages -> ProjectRoot
            currentDir = new DirectoryInfo(exeDir);
            for (int i = 0; i < 6 && currentDir != null; i++)
            {
                currentDir = currentDir.Parent;
            }

            if (currentDir != null)
            {
                var solutionPath = Path.Combine(currentDir.FullName, solutionName);
                if (File.Exists(solutionPath))
                {
                    return currentDir.FullName;
                }
            }

            return null;
        }

        /// <summary>
        /// Parse MSBuild output line for errors/warnings
        /// </summary>
        private static void ParseMsBuildOutput(string line, List<BuildError> errors, List<BuildError> warnings, string projectRoot)
        {
            var match = MsBuildErrorRegex.Match(line);
            if (match.Success)
            {
                var filePath = match.Groups["file"].Value;

                // Make path relative if possible
                if (projectRoot != null && filePath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
                {
                    filePath = filePath.Substring(projectRoot.Length).TrimStart('\\', '/');
                }

                var errorInfo = new BuildError
                {
                    File = filePath,
                    Line = int.Parse(match.Groups["line"].Value),
                    Column = int.Parse(match.Groups["column"].Value),
                    Code = match.Groups["code"].Value,
                    Message = match.Groups["message"].Value.Trim(),
                    Project = match.Groups["project"].Success ? match.Groups["project"].Value : null,
                    RawLine = line
                };

                if (match.Groups["type"].Value.Equals("error", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add(errorInfo);
                }
                else
                {
                    warnings.Add(errorInfo);
                }
            }
        }

        /// <summary>
        /// Filter errors based on exclude paths and codes
        /// </summary>
        private static List<BuildError> FilterErrors(List<BuildError> errors, string[] excludePaths, string[] excludeCodes, string projectRoot)
        {
            var filtered = new List<BuildError>();

            foreach (var error in errors)
            {
                bool shouldExclude = false;

                // Check exclude paths
                foreach (var excludePath in excludePaths)
                {
                    if (error.File.IndexOf(excludePath, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        shouldExclude = true;
                        break;
                    }

                    if (error.Project != null && error.Project.IndexOf(excludePath, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        shouldExclude = true;
                        break;
                    }

                    if (error.RawLine != null && error.RawLine.IndexOf(excludePath, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        shouldExclude = true;
                        break;
                    }
                }

                // Check exclude codes
                if (!shouldExclude)
                {
                    foreach (var excludeCode in excludeCodes)
                    {
                        if (error.Code.Equals(excludeCode, StringComparison.OrdinalIgnoreCase))
                        {
                            shouldExclude = true;
                            break;
                        }
                    }
                }

                if (!shouldExclude)
                {
                    filtered.Add(error);
                }
            }

            return filtered;
        }
    }

    /// <summary>
    /// Options for dotnet build command
    /// </summary>
    public class DotnetBuildOptions
    {
        public string Solution { get; set; } = "ET.sln";
        public string Configuration { get; set; } = "Debug";
        public string Verbosity { get; set; } = "minimal";
        public int TimeoutMs { get; set; } = 300000; // 5 minutes default
        public bool EnableFilter { get; set; } = true;
        public string[] ExcludePaths { get; set; }
        public string[] ExcludeCodes { get; set; }
        public bool HideWarnings { get; set; } = true;
    }

    /// <summary>
    /// Result of dotnet build command
    /// </summary>
    public class DotnetBuildResult
    {
        public bool Success { get; set; }
        public string Error { get; set; }
        public int ExitCode { get; set; }
        public double Duration { get; set; }
        public string SolutionPath { get; set; }
        public string ProjectRoot { get; set; }

        // Filtered results (what AI sees)
        public List<BuildError> Errors { get; set; } = new List<BuildError>();
        public List<BuildError> Warnings { get; set; } = new List<BuildError>();

        // Statistics
        public int TotalErrorCount { get; set; }
        public int TotalWarningCount { get; set; }
        public int FilteredErrorCount { get; set; }
        public int FilteredWarningCount { get; set; }

        // Raw output (for debugging)
        public string RawOutput { get; set; }
    }

    /// <summary>
    /// Build error/warning information
    /// </summary>
    public class BuildError
    {
        public string File { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }
        public string Code { get; set; }
        public string Message { get; set; }
        public string Project { get; set; }
        public string RawLine { get; set; }
    }

    /// <summary>
    /// Filter configuration for dotnet build
    /// </summary>
    public class FilterConfig
    {
        public List<string> ExcludePaths { get; set; } = new List<string>();
        public List<string> ExcludeCodes { get; set; } = new List<string>();
        public bool HideWarnings { get; set; } = true;

        public static FilterConfig GetDefault()
        {
            return new FilterConfig
            {
                ExcludePaths = new List<string>
                {
                    "ThirdParty",
                    "Plugins",
                    "Tests",
                    "Editor/Test"
                },
                ExcludeCodes = new List<string>(),
                HideWarnings = true
            };
        }
    }
}
