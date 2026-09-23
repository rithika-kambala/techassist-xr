#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TechAssistXR.Hybrid.Editor
{
    public static class TechnicianDesktopBuild
    {
        [MenuItem("TechAssist XR/Build Desktop macOS")]
        public static void BuildMac()
        {
            if (EditorApplication.isPlaying) throw new BuildFailedException("Stop Play mode before building.");
            const string scene = "Assets/TechAssistXRHybrid/Generated/TechnicianAIWorkbench 1.unity";
            if (!File.Exists(scene)) throw new BuildFailedException("The machine workbench scene is missing.");
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneOSX))
                throw new BuildFailedException("Install macOS Build Support in Unity Hub.");
            var path = Environment.GetEnvironmentVariable("TECHASSIST_BUILD_PATH");
            if (string.IsNullOrEmpty(path)) path = "Builds/macOS/TechAssistXR.app";
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { scene }, locationPathName = path,
                target = BuildTarget.StandaloneOSX, options = BuildOptions.None
            });
            var summary = report.summary;
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path)), "BuildResult.txt"),
                $"Result: {summary.result}\nErrors: {summary.totalErrors}\nWarnings: {summary.totalWarnings}\nBytes: {summary.totalSize}\nUTC: {DateTime.UtcNow:O}\n");
            if (summary.result != BuildResult.Succeeded) throw new BuildFailedException("Desktop build failed. See BuildResult.txt and Editor log.");
            Debug.Log("TechAssist desktop build completed: " + path);
        }
    }
}
#endif
