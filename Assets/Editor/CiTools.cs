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
                UnityEditor.Build.NamedBuildTarget.Android, "edu.sjsu.ltilab.sdghall");
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.SetScriptingBackend(
                UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            EnableXrLoader(BuildTargetGroup.Android, "Unity.XR.Oculus.OculusLoader");
            EnableBothInputBackends();

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

        /// <summary>
        /// Browser VR build (WebXR, via the De-Panther WebXR Export package)
        /// to Builds/WebXR, ready for GitHub Pages - see scripts/deploy-pages.sh.
        /// </summary>
        public static void BuildWebXR()
        {
            ConfigureWebXR();
            PrepareScene();
            Run(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                locationPathName = "Builds/WebXR"
            });
        }

        /// <summary>
        /// Idempotent WebXR player setup: WebXR page template, WebXR XR
        /// loader for WebGL, and compression off. GitHub Pages can't send
        /// the Content-Encoding headers Unity's gzip/brotli builds need,
        /// and the JS decompression fallback is slow, so ship uncompressed
        /// (Pages still gzips on the wire).
        /// </summary>
        public static void ConfigureWebXR()
        {
            var pkg = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(
                "Packages/com.de-panther.webxr");
            if (pkg == null)
            {
                Debug.LogError("[CiTools] com.de-panther.webxr is not installed.");
                EditorApplication.Exit(1);
                return;
            }
            // Non-interactive equivalent of Window > WebXR > Copy WebGLTemplates.
            string src = Path.Combine(pkg.resolvedPath, "Hidden~", "WebGLTemplates");
            CopyDirectory(src, Path.Combine("Assets", "WebGLTemplates"));
            AssetDatabase.Refresh();

            PlayerSettings.WebGL.template = "PROJECT:WebXR2020";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = false;
            PlayerSettings.WebGL.dataCaching = true;

            EnableXrLoader(BuildTargetGroup.WebGL, "WebXR.WebXRLoader");
            RegisterWebXRSettings();
            if (EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey,
                    out XRGeneralSettingsPerBuildTarget perTarget) && perTarget != null)
            {
                var webgl = perTarget.SettingsForBuildTarget(BuildTargetGroup.WebGL);
                if (webgl != null)
                {
                    webgl.InitManagerOnStart = true;
                    EditorUtility.SetDirty(webgl);
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[CiTools] WebXR configured (template WebXR2020, compression off, WebXR loader on).");
        }

        /// <summary>
        /// WebXR Export's build step only ships its settings if they are
        /// registered under the "WebXR.Settings" config key - which the
        /// Project Settings UI does, but enabling the loader from code does
        /// not. Unregistered, the page's JS gets no settings and "Enter VR"
        /// crashes (reading 'VRRequiredReferenceSpace' of undefined) - found
        /// with the IWER emulator. Created by type name so this editor
        /// assembly needs no reference to the (non-auto-referenced) package.
        /// </summary>
        private static void RegisterWebXRSettings()
        {
            const string path = "Assets/XR/Settings/WebXRSettings.asset";
            var settings = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance("WebXR.WebXRSettings");
                if (settings == null)
                {
                    Debug.LogError("[CiTools] WebXR.WebXRSettings type not found - is the package installed?");
                    return;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                AssetDatabase.CreateAsset(settings, path);
            }
            EditorBuildSettings.AddConfigObject("WebXR.Settings", settings, true);
            Debug.Log("[CiTools] WebXR settings registered (" + path + ").");
        }

        private static void CopyDirectory(string from, string to)
        {
            Directory.CreateDirectory(to);
            foreach (string file in Directory.GetFiles(from))
            {
                File.Copy(file, Path.Combine(to, Path.GetFileName(file)), true);
            }
            foreach (string dir in Directory.GetDirectories(from))
            {
                CopyDirectory(dir, Path.Combine(to, Path.GetFileName(dir)));
            }
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
        /// <summary>
        /// Sets Player > Active Input Handling to "Both". The VERA package
        /// imports UnityEngine.InputSystem only under ENABLE_INPUT_SYSTEM but
        /// uses InputActionProperty unguarded, so with "Input Manager (Old)"
        /// it fails with CS0246. Our own scripts use the legacy Input class,
        /// so "Input System Package" alone would break them - it must be Both.
        /// There is no public PlayerSettings API for this, hence the
        /// SerializedObject route. Takes effect after an editor restart.
        /// </summary>
        private static void EnableBothInputBackends()
        {
            const int Both = 2;
            var playerSettings = AssetDatabase.LoadAllAssetsAtPath(
                "ProjectSettings/ProjectSettings.asset")[0];
            var so = new SerializedObject(playerSettings);
            var prop = so.FindProperty("activeInputHandler");
            if (prop != null && prop.intValue != Both)
            {
                prop.intValue = Both;
                so.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("[CiTools] Active Input Handling set to Both (restart the editor to apply).");
            }
        }

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
