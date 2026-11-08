using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace UndeadOvertime.LanPrototype.Editor
{
    public static class LanRangeBuild
    {
        // Explicit opt-in build only. Does not rewrite EditorBuildSettings, launch a player or open a port.
        [MenuItem("Undead Overtime/Build LAN test player (Windows)")]
        public static void BuildWindows()
        {
            DepotFloorImport.ValidateMaterialAssets();
            DepotConsoleImport.ValidateAssets();
            string directory = Path.GetFullPath(Path.Combine("Builds", "LanRange"));
            Directory.CreateDirectory(directory);
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/UndeadOvertime/Scenes/LanRange.unity" },
                locationPathName = Path.Combine(directory, "UndeadOvertimeLanRange.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("LAN test build failed: " + report.summary.result);
            UnityEngine.Debug.Log("UO_LAN_BUILD_OK " + report.summary.outputPath);
        }
    }
}
