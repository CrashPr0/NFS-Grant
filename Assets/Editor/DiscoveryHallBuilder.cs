using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using NSFGrant.Core;
using NSFGrant.Docent;
using NSFGrant.Gaze;
using NSFGrant.Interaction;
using NSFGrant.Logging;
using NSFGrant.Session;
using NSFGrant.Stations;
using NSFGrant.Vera;

namespace NSFGrant.EditorTools
{
    /// <summary>
    /// Builds the UN SDG Discovery Hall study scene from the menu:
    /// NSF Grant &gt; Build Discovery Hall Scene.
    ///
    /// Layout follows Dr. Chow's research proposal and the VERA kickoff
    /// meeting: a three-station prototype (SDG 4 Quality Education,
    /// SDG 11 Sustainable Cities, SDG 13 Climate Action), each station
    /// presenting the same content in five formats — text panel, data
    /// visualization, video/audio story kiosk, interactive object, and
    /// call-to-action wall — plus a docent placeholder. The scene carries
    /// both a VR rig (Quest) and a desktop rig (laptop/WebGL); the
    /// PlatformRigSwitcher picks one at runtime so the same build serves
    /// both participant groups.
    ///
    /// Every placeholder primitive is meant to be replaced with real content
    /// by the design team; the AttentionTarget / InteractableObject /
    /// SdgStation components on them are the data-collection contract and
    /// should be kept.
    /// </summary>
    public static class DiscoveryHallBuilder
    {
        private class StationSpec
        {
            public string Id;
            public string Title;
            public Color Color;
        }

        // Official UN SDG goal colors.
        private static readonly StationSpec[] Stations =
        {
            new StationSpec { Id = "SDG04_QualityEducation", Title = "SDG 4 - Quality Education", Color = new Color(0.77f, 0.10f, 0.18f) },
            new StationSpec { Id = "SDG11_SustainableCities", Title = "SDG 11 - Sustainable Cities", Color = new Color(0.99f, 0.62f, 0.14f) },
            new StationSpec { Id = "SDG13_ClimateAction", Title = "SDG 13 - Climate Action", Color = new Color(0.25f, 0.49f, 0.27f) }
        };

        [MenuItem("NSF Grant/Build Discovery Hall Scene")]
        public static void BuildDiscoveryHall()
        {
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var defaultCamera = GameObject.Find("Main Camera");
            if (defaultCamera != null)
            {
                Object.DestroyImmediate(defaultCamera);
            }

            // --- Rigs (one active at runtime, chosen by PlatformRigSwitcher).
            GameObject vrRig = SampleSceneBuilder.CreateCameraRig(
                out Transform centerEye, out OVREyeGaze leftEye, out OVREyeGaze rightEye);
            vrRig.AddComponent<VRInteractor>();

            GameObject desktopRig = CreateDesktopRig();

            var rigs = new GameObject("Rigs");
            vrRig.transform.SetParent(rigs.transform);
            desktopRig.transform.SetParent(rigs.transform);
            var switcher = rigs.AddComponent<PlatformRigSwitcher>();
            var switcherSo = new SerializedObject(switcher);
            switcherSo.FindProperty("vrRig").objectReferenceValue = vrRig;
            switcherSo.FindProperty("desktopRig").objectReferenceValue = desktopRig;
            switcherSo.ApplyModifiedPropertiesWithoutUndo();

            // --- Study stack.
            var study = new GameObject("AttentionStudy");
            var gazeProvider = study.AddComponent<GazeProvider>();
            study.AddComponent<FixationDetector>();
            study.AddComponent<GazeRaycaster>();
            study.AddComponent<AttentionDataLogger>();
            study.AddComponent<StudyEventLogger>();
            study.AddComponent<ScreenshotCapture>();
            study.AddComponent<RemoteDataUploader>();
            study.AddComponent<StudyConditionManager>();
            study.AddComponent<VeraBridge>();
            study.AddComponent<SessionController>();

            var gazeSo = new SerializedObject(gazeProvider);
            gazeSo.FindProperty("centerEyeAnchor").objectReferenceValue = centerEye;
            gazeSo.FindProperty("leftEyeGaze").objectReferenceValue = leftEye;
            gazeSo.FindProperty("rightEyeGaze").objectReferenceValue = rightEye;
            gazeSo.ApplyModifiedPropertiesWithoutUndo();

            // --- Hall geometry.
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(4f, 1f, 4f); // 40 x 40 m

            // --- Stations arranged in an arc in front of the spawn point.
            var stationComponents = new List<SdgStation>();
            float[] angles = { -55f, 0f, 55f };
            const float radius = 10f;

            for (int i = 0; i < Stations.Length; i++)
            {
                Vector3 center = Quaternion.Euler(0f, angles[i], 0f) * (Vector3.forward * radius);
                stationComponents.Add(BuildStation(Stations[i], center));
            }

            // --- Docent (active only in Condition C).
            var docentRoot = new GameObject("DocentGuide");
            var beacon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            beacon.name = "DocentBeacon";
            beacon.transform.SetParent(docentRoot.transform);
            beacon.transform.localScale = Vector3.one * 0.6f;
            Object.DestroyImmediate(beacon.GetComponent<Collider>());
            var beaconMat = new Material(Shader.Find("Standard")) { color = Color.cyan };
            beaconMat.EnableKeyword("_EMISSION");
            beaconMat.SetColor("_EmissionColor", Color.cyan);
            beacon.GetComponent<Renderer>().sharedMaterial = beaconMat;

            var docent = docentRoot.AddComponent<DocentGuide>();
            var docentSo = new SerializedObject(docent);
            docentSo.FindProperty("beacon").objectReferenceValue = beacon;
            var routeProp = docentSo.FindProperty("route");
            routeProp.arraySize = stationComponents.Count;
            for (int i = 0; i < stationComponents.Count; i++)
            {
                routeProp.GetArrayElementAtIndex(i).objectReferenceValue = stationComponents[i];
            }
            docentSo.ApplyModifiedPropertiesWithoutUndo();

            // --- Save.
            const string path = "Assets/Scenes/DiscoveryHall.unity";
            System.IO.Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, path);
            Debug.Log($"[DiscoveryHallBuilder] Discovery Hall saved to {path}");
        }

        private static GameObject CreateDesktopRig()
        {
            var player = new GameObject("DesktopPlayer");
            player.transform.position = new Vector3(0f, 0f, -2f);

            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.center = new Vector3(0f, 0.9f, 0f);

            var camGo = new GameObject("DesktopCamera");
            camGo.transform.SetParent(player.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            camGo.tag = "MainCamera";
            camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();

            player.AddComponent<DesktopPlayerController>();
            player.AddComponent<DesktopInteractor>();
            return player;
        }

        private static SdgStation BuildStation(StationSpec spec, Vector3 center)
        {
            var root = new GameObject(spec.Id);
            root.transform.position = center;
            // Stations face the spawn point at the hall center.
            root.transform.rotation = Quaternion.LookRotation(-center.normalized, Vector3.up);

            // Trigger volume defining the station footprint.
            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 2f, 0f);
            trigger.size = new Vector3(9f, 4f, 7f);

            var station = root.AddComponent<SdgStation>();
            var stationSo = new SerializedObject(station);
            stationSo.FindProperty("stationId").stringValue = spec.Id;
            stationSo.ApplyModifiedPropertiesWithoutUndo();

            CreateLabel(root.transform, spec.Title, new Vector3(0f, 3.2f, 0f), 0.5f);

            // Five format zones in a shallow arc, all facing the visitor.
            CreateZone(root.transform, spec, "TextPanel",
                AttentionTarget.ContentFormat.TextPanel, PrimitiveType.Cube,
                new Vector3(-3.2f, 1.5f, 1f), new Vector3(1.6f, 1.1f, 0.08f), false,
                "Text Panel\n(summary + key facts)");

            CreateZone(root.transform, spec, "DataViz",
                AttentionTarget.ContentFormat.DataVisualization, PrimitiveType.Cube,
                new Vector3(-1.6f, 1.5f, 0.3f), new Vector3(1.6f, 1.1f, 0.08f), true,
                "Data Visualization\n(charts / dashboards)");

            CreateZone(root.transform, spec, "VideoKiosk",
                AttentionTarget.ContentFormat.VideoStory, PrimitiveType.Cube,
                new Vector3(0f, 1.5f, 0f), new Vector3(1.8f, 1.1f, 0.08f), true,
                "Video / Audio Story\n(human-centered)");

            CreateZone(root.transform, spec, "Interactive",
                AttentionTarget.ContentFormat.InteractiveObject, PrimitiveType.Sphere,
                new Vector3(1.8f, 1.2f, 0.3f), Vector3.one * 0.8f, true,
                "Interactive Simulation");

            CreateZone(root.transform, spec, "CallToAction",
                AttentionTarget.ContentFormat.CallToActionWall, PrimitiveType.Cube,
                new Vector3(3.2f, 1.5f, 1f), new Vector3(1.6f, 1.1f, 0.08f), true,
                "Call To Action Wall\n(choose what matters)");

            // Docent placeholder (a future conversational avatar position).
            CreateZone(root.transform, spec, "Docent",
                AttentionTarget.ContentFormat.Docent, PrimitiveType.Capsule,
                new Vector3(0f, 1f, 2.2f), new Vector3(0.5f, 1f, 0.5f), true,
                "AI Docent");

            return station;
        }

        private static void CreateZone(Transform parent, StationSpec spec, string zoneName,
            AttentionTarget.ContentFormat format, PrimitiveType primitive,
            Vector3 localPos, Vector3 localScale, bool interactable, string labelText)
        {
            var go = GameObject.CreatePrimitive(primitive);
            string id = $"{spec.Id}_{zoneName}";
            go.name = id;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;

            var renderer = go.GetComponent<Renderer>();
            var material = new Material(renderer.sharedMaterial) { color = spec.Color };
            renderer.sharedMaterial = material;

            var target = go.AddComponent<AttentionTarget>();
            var targetSo = new SerializedObject(target);
            targetSo.FindProperty("targetId").stringValue = id;
            targetSo.FindProperty("format").enumValueIndex = (int)format;
            targetSo.FindProperty("stationId").stringValue = spec.Id;
            targetSo.ApplyModifiedPropertiesWithoutUndo();

            if (interactable)
            {
                var interactableComponent = go.AddComponent<InteractableObject>();
                var interactableSo = new SerializedObject(interactableComponent);
                interactableSo.FindProperty("objectId").stringValue = id;
                interactableSo.ApplyModifiedPropertiesWithoutUndo();
            }

            CreateLabel(go.transform, labelText,
                new Vector3(0f, localScale.y * 0.5f + 0.35f, 0f), 0.15f);
        }

        private static void CreateLabel(Transform parent, string text, Vector3 localPos, float size)
        {
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(parent, false);
            labelGo.transform.localPosition = localPos;
            // Stations face the visitor along local +Z; flip the TextMesh so
            // it reads correctly from that side.
            labelGo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            // Counteract parent scaling so text is not distorted.
            Vector3 lossy = parent.lossyScale;
            labelGo.transform.localScale = new Vector3(
                lossy.x != 0f ? 1f / lossy.x : 1f,
                lossy.y != 0f ? 1f / lossy.y : 1f,
                lossy.z != 0f ? 1f / lossy.z : 1f);

            var mesh = labelGo.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.characterSize = size;
            mesh.fontSize = 48;
            mesh.anchor = TextAnchor.LowerCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = Color.white;
        }
    }
}
