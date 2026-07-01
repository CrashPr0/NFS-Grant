using System.IO;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.XR.Management;

namespace NSFGrant.EditorTools
{
    /// <summary>
    /// Headless (batch-mode) entry points so the project can be configured
    /// and built from the command line instead of the editor UI. Driven by
    /// scripts/unity-tasks.sh (or .bat):
    ///
    ///   Unity -batchmode -nographics -quit -projectPath . \
    ///         -executeMethod NSFGrant.EditorTools.CiTools.SetupProject
    ///
    /// Methods:
    ///   SetupProject  - URP pipeline, media download, Discovery Hall scene,
    ///                   player settings, Oculus XR loader for Android.
    ///   BuildQuest    - Android APK to Builds/SDGDiscoveryHall.apk.
    ///   BuildWebGL    - WebGL player to Builds/WebGL/.
    /// </summary>
    public static class CiTools
    {
        private const string ScenePath = "Assets/Scenes/DiscoveryHall.unity";

        public static void SetupProject()
        {
            Debug.Log("[CiTools] === SetupProject ===");

            UrpSetup.Setup();
            SdgAssetDownloader.DownloadAll();
            DiscoveryHallBuilder.BuildDiscoveryHall();

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };

            PlayerSettings.companyName = "SJSU LTI Lab";
            PlayerSettings.productName = "UN SDG Discovery Hall";
            PlayerSettings.SetApplicationIdentifier(
                BuildTargetGroup.Android, "edu.sjsu.ltilab.sdghall");
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.SetScriptingBackend(
                BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            EnableXrLoader(BuildTargetGroup.Android, "Unity.XR.Oculus.OculusLoader");

            AssetDatabase.SaveAssets();
            Debug.Log("[CiTools] Setup complete. Note: run the Meta Project Setup Tool " +
                      "once in the editor before shipping lab builds; it validates " +
                      "manifest/permission details CiTools does not cover.");
        }

        public static void BuildQuest()
        {
            PrepareScene();
            EditorUserBuildSettings.buildAppBundle = false;
            Run(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                locationPathName = "Builds/SDGDiscoveryHall.apk"
            });
        }

        public static void BuildWebGL()
        {
            PrepareScene();
            Run(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                locationPathName = "Builds/WebGL"
            });
        }

        private static void PrepareScene()
        {
            if (!File.Exists(ScenePath))
            {
                Debug.Log("[CiTools] Scene missing - running SetupProject first.");
                SetupProject();
                return;
            }
            // ALWAYS regenerate the scene before a player build. The scene
            // file is generated output, not source: building a player from
            // an existing (possibly stale) scene once shipped an APK
            // without the then-new VRLocomotion component. Regeneration is
            // deterministic and cheap next to the player build itself.
            Debug.Log("[CiTools] Regenerating scene so the build can't ship a stale one.");
            DiscoveryHallBuilder.BuildDiscoveryHall();
        }

        private static void Run(BuildPlayerOptions options)
        {
            Directory.CreateDirectory("Builds");
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.LogError($"[CiTools] Build FAILED: {report.summary.result}, " +
                               $"{report.summary.totalErrors} errors.");
                EditorApplication.Exit(1);
                return;
            }
            Debug.Log($"[CiTools] Build succeeded -> {options.locationPathName} " +
                      $"({report.summary.totalSize / (1024 * 1024)} MB)");
        }

        /// <summary>
        /// Programmatic equivalent of ticking the loader checkbox in
        /// Project Settings &gt; XR Plug-in Management.
        /// </summary>
        private static void EnableXrLoader(BuildTargetGroup group, string loaderTypeName)
        {
            try
            {
                if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey,
                        out XRGeneralSettingsPerBuildTarget perTarget) || perTarget == null)
                {
                    perTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                    Directory.CreateDirectory("Assets/XR");
                    AssetDatabase.CreateAsset(perTarget, "Assets/XR/XRGeneralSettings.asset");
                    EditorBuildSettings.AddConfigObject(
                        XRGeneralSettings.k_SettingsKey, perTarget, true);
                }

                var settings = perTarget.SettingsForBuildTarget(group);
                if (settings == null)
                {
                    settings = ScriptableObject.CreateInstance<XRGeneralSettings>();
                    perTarget.SetSettingsForBuildTarget(group, settings);
                    AssetDatabase.AddObjectToAsset(settings, perTarget);

                    var manager = ScriptableObject.CreateInstance<XRManagerSettings>();
                    settings.Manager = manager;
                    AssetDatabase.AddObjectToAsset(manager, perTarget);
                }

                if (XRPackageMetadataStore.AssignLoader(settings.Manager, loaderTypeName, group))
                {
                    Debug.Log($"[CiTools] XR loader '{loaderTypeName}' enabled for {group}.");
                }
                else
                {
                    Debug.LogWarning($"[CiTools] Could not assign XR loader '{loaderTypeName}' - " +
                                     "enable it in Project Settings > XR Plug-in Management.");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[CiTools] XR loader setup failed ({e.Message}) - " +
                                 "enable Oculus in XR Plug-in Management manually.");
            }
        }
    }
}
