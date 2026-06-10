using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using NSFGrant.Gaze;
using NSFGrant.Logging;
using NSFGrant.Session;

namespace NSFGrant.EditorTools
{
    /// <summary>
    /// Builds a ready-to-run attention-study scene from the menu:
    /// NSF Grant &gt; Build Sample Scene. The scene contains an OVRCameraRig
    /// with eye-gaze components, the full data-collection stack, and a few
    /// AttentionTarget objects arranged in front of the participant.
    /// </summary>
    public static class SampleSceneBuilder
    {
        [MenuItem("NSF Grant/Build Sample Scene")]
        public static void BuildSampleScene()
        {
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // The default Main Camera is replaced by the OVRCameraRig.
            var defaultCamera = GameObject.Find("Main Camera");
            if (defaultCamera != null)
            {
                Object.DestroyImmediate(defaultCamera);
            }

            GameObject rig = CreateCameraRig(out Transform centerEye,
                out OVREyeGaze leftEye, out OVREyeGaze rightEye);

            // Data-collection stack.
            var study = new GameObject("AttentionStudy");
            var gazeProvider = study.AddComponent<GazeProvider>();
            study.AddComponent<FixationDetector>();
            study.AddComponent<GazeRaycaster>();
            study.AddComponent<AttentionDataLogger>();
            study.AddComponent<SessionController>();

            var so = new SerializedObject(gazeProvider);
            so.FindProperty("centerEyeAnchor").objectReferenceValue = centerEye;
            so.FindProperty("leftEyeGaze").objectReferenceValue = leftEye;
            so.FindProperty("rightEyeGaze").objectReferenceValue = rightEye;
            so.ApplyModifiedPropertiesWithoutUndo();

            // A small set of areas of interest in front of the participant.
            CreateTarget("Target_Left", PrimitiveType.Cube, new Vector3(-1.5f, 1.6f, 3f), Color.red);
            CreateTarget("Target_Center", PrimitiveType.Sphere, new Vector3(0f, 1.6f, 3f), Color.green);
            CreateTarget("Target_Right", PrimitiveType.Cube, new Vector3(1.5f, 1.6f, 3f), Color.blue);
            CreateTarget("Target_High", PrimitiveType.Capsule, new Vector3(0f, 2.6f, 3.5f), Color.yellow);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(2f, 1f, 2f);

            string path = "Assets/Scenes/AttentionStudy.unity";
            System.IO.Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, path);
            Debug.Log($"[SampleSceneBuilder] Sample scene saved to {path}");
        }

        private static GameObject CreateCameraRig(out Transform centerEye,
            out OVREyeGaze leftEye, out OVREyeGaze rightEye)
        {
            // Build the rig in code rather than referencing the OVRCameraRig
            // prefab by GUID, so this works across Meta XR SDK versions.
            var rig = new GameObject("OVRCameraRig");
            var ovrRig = rig.AddComponent<OVRCameraRig>();
            var manager = rig.AddComponent<OVRManager>();
            manager.trackingOriginType = OVRManager.TrackingOrigin.FloorLevel;

            // OVRCameraRig creates its anchor hierarchy on Awake; force it now
            // so we can attach to the center eye in the editor.
            var ensureMethod = typeof(OVRCameraRig).GetMethod("EnsureGameObjectIntegrity",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            ensureMethod?.Invoke(ovrRig, null);

            centerEye = ovrRig.centerEyeAnchor != null
                ? ovrRig.centerEyeAnchor
                : rig.transform;

            var leftGazeGo = new GameObject("LeftEyeGaze");
            leftGazeGo.transform.SetParent(centerEye, false);
            leftEye = leftGazeGo.AddComponent<OVREyeGaze>();
            leftEye.Eye = OVREyeGaze.EyeId.Left;

            var rightGazeGo = new GameObject("RightEyeGaze");
            rightGazeGo.transform.SetParent(centerEye, false);
            rightEye = rightGazeGo.AddComponent<OVREyeGaze>();
            rightEye.Eye = OVREyeGaze.EyeId.Right;

            return rig;
        }

        private static void CreateTarget(string name, PrimitiveType type,
            Vector3 position, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.position = position;
            go.transform.localScale = Vector3.one * 0.5f;
            go.AddComponent<AttentionTarget>();

            var renderer = go.GetComponent<Renderer>();
            var material = new Material(renderer.sharedMaterial) { color = color };
            renderer.sharedMaterial = material;
        }
    }
}
