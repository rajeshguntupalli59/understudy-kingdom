using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace UnderstudyKingdom.EditorTools
{
    /// <summary>
    /// Imports the TMP Essential Resources package (default font asset + TMP Settings).
    /// Without this, TMP_Settings.instance is null and TextMeshProUGUI.Awake() silently
    /// no-ops in the Editor, so every TextMeshProUGUI label in the project renders blank
    /// even though its .text property is set correctly.
    ///
    /// Uses the same public API the "Window > TextMeshPro > Import TMP Essential
    /// Resources" menu item calls internally
    /// (TMPro.TMP_PackageResourceImporter.ImportResources, defined in
    /// Library/PackageCache/com.unity.ugui@.../Runtime/TMP/TMP_PackageResourceImporter.cs),
    /// with interactive=false so it can run unattended in batch mode.
    ///
    /// Run via:
    ///   -executeMethod UnderstudyKingdom.EditorTools.TmpEssentialResourcesImporter.Import
    /// </summary>
    public static class TmpEssentialResourcesImporter
    {
        private const string SettingsAssetPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        [MenuItem("Understudy Kingdom/Import TMP Essential Resources")]
        public static void Import()
        {
            if (File.Exists(SettingsAssetPath))
            {
                Debug.Log("TmpEssentialResourcesImporter: TMP Essential Resources already present, skipping import.");
                FinishBatch(0);
                return;
            }

            AssetDatabase.importPackageCompleted += OnImportCompleted;
            AssetDatabase.importPackageFailed += OnImportFailed;
            TMP_PackageResourceImporter.ImportResources(importEssentials: true, importExamples: false, interactive: false);
        }

        private static void OnImportCompleted(string packageName)
        {
            AssetDatabase.importPackageCompleted -= OnImportCompleted;
            AssetDatabase.importPackageFailed -= OnImportFailed;

            bool settingsPresent = File.Exists(SettingsAssetPath);
            Debug.Log($"TmpEssentialResourcesImporter: imported '{packageName}'. Settings asset present: {settingsPresent}");
            FinishBatch(settingsPresent ? 0 : 1);
        }

        private static void OnImportFailed(string packageName, string errorMessage)
        {
            AssetDatabase.importPackageCompleted -= OnImportCompleted;
            AssetDatabase.importPackageFailed -= OnImportFailed;

            Debug.LogError($"TmpEssentialResourcesImporter: import of '{packageName}' failed: {errorMessage}");
            FinishBatch(1);
        }

        private static void FinishBatch(int exitCode)
        {
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(exitCode);
            }
        }
    }
}
