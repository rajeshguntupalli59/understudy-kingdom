using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace UnderstudyKingdom.EditorTools
{
    /// <summary>
    /// One-off batchmode build entry point for local emulator testing --
    /// not part of any CI/release pipeline. Sets the Android application
    /// identifier (unset by default in this project) and targets x86_64
    /// specifically to match the Pixel_6 AVD's system image
    /// (android-37.0, x86_64), avoiding ARM-translation overhead/flakiness
    /// entirely for this test build.
    /// </summary>
    public static class AndroidBuildTool
    {
        private const string PackageName = "com.DefaultCompany.understudykingdom";
        private const string OutputPath = "Builds/Android/understudy-kingdom.apk";

        public static void BuildApk()
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageName);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.X86_64;
            // Local backend runs on plain HTTP -- Unity blocks all non-HTTPS
            // UnityWebRequest traffic on Android by default (InvalidOperationException:
            // "Insecure connection not allowed"), which silently strands every
            // backend call (Council/History/Duel) on this test build otherwise.
            // AlwaysAllowed (not DevelopmentOnly) because this build uses
            // BuildOptions.None, not a Development build.
            PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;

            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/CoreLoop.unity" },
                locationPathName = OutputPath,
                target = BuildTarget.Android,
                options = BuildOptions.None,
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            Debug.Log($"AndroidBuildTool.BuildApk: result={summary.result}, totalErrors={summary.totalErrors}, " +
                      $"totalWarnings={summary.totalWarnings}, size={summary.totalSize} bytes, outputPath={summary.outputPath}");

            // -executeMethod does not auto-quit batchmode on return -- without an
            // explicit exit here, a successful run leaves the process alive
            // holding the project lock, causing the next invocation to fail
            // instantly instead of building.
            bool succeeded = summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded;
            EditorApplication.Exit(succeeded ? 0 : 1);
        }
    }
}
