using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using NSFGrant.Content;
using NSFGrant.Core;
using NSFGrant.Docent;
using NSFGrant.Gaze;
using NSFGrant.Interaction;
using NSFGrant.Logging;
using NSFGrant.Session;
using NSFGrant.Stations;
using NSFGrant.Survey;
using NSFGrant.Vera;

namespace NSFGrant.EditorTools
{
    /// <summary>
    /// Builds the UN SDG Discovery Hall study scene from the menu:
    /// NSF Grant &gt; Build Discovery Hall Scene.
    ///
    /// Station content comes from <see cref="SdgContentLibrary"/> — the
    /// curated copy, case studies, docent personas (MINERVA / HINA / BHUMI)
    /// and resource links produced by the SJSU LTI Lab UN SDG student team.
    /// Run NSF Grant &gt; Download SDG Media Assets first to pull the official
    /// goal icons and SDG 13 photos; the builder applies them when present.
    ///
    /// The scene carries both a VR rig (Quest) and a desktop rig (laptop /
    /// WebGL); the PlatformRigSwitcher picks one at runtime so the same
    /// build serves both participant groups.
    /// </summary>
    public static class DiscoveryHallBuilder
    {
        private const string TexturesDir = "Assets/StudyContent/Textures";

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
            var counterbalance = study.AddComponent<CounterbalanceManager>();
            var cbSo = new SerializedObject(counterbalance);
            cbSo.FindProperty("rigRoot").objectReferenceValue = rigs.transform;
            cbSo.ApplyModifiedPropertiesWithoutUndo();
            var sessionController = study.AddComponent<SessionController>();
            // StudyIntake owns the session lifecycle (URL params / intake
            // panel / VR auto-start), so the controller must not auto-start.
            var sessionSo = new SerializedObject(sessionController);
            sessionSo.FindProperty("autoStart").boolValue = false;
            sessionSo.ApplyModifiedPropertiesWithoutUndo();
            study.AddComponent<StudyIntake>();

            var gazeSo = new SerializedObject(gazeProvider);
            gazeSo.FindProperty("centerEyeAnchor").objectReferenceValue = centerEye;
            gazeSo.FindProperty("leftEyeGaze").objectReferenceValue = leftEye;
            gazeSo.FindProperty("rightEyeGaze").objectReferenceValue = rightEye;
            gazeSo.ApplyModifiedPropertiesWithoutUndo();

            // --- Pre/post knowledge quiz.
            var quizRunner = study.AddComponent<QuizRunner>();
            var quizAsset = CreateQuizAsset();
            var quizSo = new SerializedObject(quizRunner);
            quizSo.FindProperty("quiz").objectReferenceValue = quizAsset;
            quizSo.ApplyModifiedPropertiesWithoutUndo();

            // --- Hall geometry, lighting and ambience.
            CreateHallEnvironment();

            // --- Three exhibit rooms radiate from the central hub (the
            // ReadingNation Waterfall FrameVR layout): corridors at 120
            // degrees, room front walls 10 m out, hub walls at 6 m.
            var stationComponents = new List<SdgStation>();
            float[] angles = { -120f, 0f, 120f };
            const float radius = 16f;

            for (int i = 0; i < SdgContentLibrary.Stations.Length && i < angles.Length; i++)
            {
                Vector3 center = Quaternion.Euler(0f, angles[i], 0f) * (Vector3.forward * radius);
                stationComponents.Add(BuildStation(SdgContentLibrary.Stations[i], center));
            }

            // --- Docent route (active only in Condition C).
            var docentRoot = new GameObject("DocentGuide");
            var beacon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            beacon.name = "DocentBeacon";
            beacon.transform.SetParent(docentRoot.transform);
            beacon.transform.localScale = Vector3.one * 0.6f;
            Object.DestroyImmediate(beacon.GetComponent<Collider>());
            // Pulsing glow (Condition C navigation aid) on the GPU; falls
            // back to a static emissive material if the shader is missing.
            var pulseShader = Shader.Find("NSFGrant/EmissivePulse");
            Material beaconMat;
            if (pulseShader != null)
            {
                beaconMat = new Material(pulseShader);
                beaconMat.SetColor("_Color", Color.cyan);
            }
            else
            {
                beaconMat = new Material(beacon.GetComponent<Renderer>().sharedMaterial)
                {
                    color = Color.cyan
                };
                beaconMat.EnableKeyword("_EMISSION");
                beaconMat.SetColor("_EmissionColor", Color.cyan);
            }
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

        private static SdgStation BuildStation(SdgStationContent content, Vector3 center)
        {
            Color themeColor = Color.gray;
            ColorUtility.TryParseHtmlString(content.ThemeColorHex, out themeColor);

            var root = new GameObject(content.StationId);
            root.transform.position = center;
            // Stations face the spawn point at the hall center.
            root.transform.rotation = Quaternion.LookRotation(-center.normalized, Vector3.up);

            // Trigger volume covering the room and its corridor mouth.
            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 2.5f, 2.5f);
            trigger.size = new Vector3(13f, 5f, 12f);

            var station = root.AddComponent<SdgStation>();
            var stationSo = new SerializedObject(station);
            stationSo.FindProperty("stationId").stringValue = content.StationId;
            stationSo.ApplyModifiedPropertiesWithoutUndo();

            // Room floor tint: thin and colliderless so it neither trips the
            // character controller nor catches gaze rays.
            CreateVisualPrimitive(root.transform, PrimitiveType.Cylinder,
                $"{content.StationId}_Platform",
                new Vector3(0f, 0.02f, 2.4f), new Vector3(12.8f, 0.015f, 7.6f),
                Color.Lerp(themeColor, FloorColor, 0.78f));

            // Room shell. The back wall carries the theme; sides and front
            // stay neutral so the color identifies the room without
            // overwhelming it. Front wall has a 3 m doorway to the corridor.
            var neutralWall = new Color(0.30f, 0.30f, 0.33f);
            CreateWall(root.transform, $"{content.StationId}_BackWall",
                new Vector3(0f, 2.5f, -1.2f), new Vector3(12.6f, 5f, 0.15f),
                Color.Lerp(themeColor, Color.black, 0.65f));
            CreateWall(root.transform, "SideWall_L",
                new Vector3(-6.3f, 2.5f, 2.4f), new Vector3(0.15f, 5f, 7.5f), neutralWall);
            CreateWall(root.transform, "SideWall_R",
                new Vector3(6.3f, 2.5f, 2.4f), new Vector3(0.15f, 5f, 7.5f), neutralWall);
            float segW = (12.6f - DoorWidth) / 2f;
            CreateWall(root.transform, "FrontWall_L",
                new Vector3(-(DoorWidth + segW) / 2f, 2.5f, 6f),
                new Vector3(segW, 5f, 0.15f), neutralWall);
            CreateWall(root.transform, "FrontWall_R",
                new Vector3((DoorWidth + segW) / 2f, 2.5f, 6f),
                new Vector3(segW, 5f, 0.15f), neutralWall);
            CreateWall(root.transform, "DoorLintel",
                new Vector3(0f, 4.1f, 6f), new Vector3(DoorWidth, 1.8f, 0.15f),
                Color.Lerp(themeColor, Color.black, 0.35f));

            // Corridor to the hub (room front at 10 m from center, hub wall
            // at 6 m; the hub doorway gap lines up with these walls).
            CreateWall(root.transform, "Corridor_L",
                new Vector3(-1.575f, 1.75f, 8f), new Vector3(0.15f, 3.5f, 4.2f), neutralWall);
            CreateWall(root.transform, "Corridor_R",
                new Vector3(1.575f, 1.75f, 8f), new Vector3(0.15f, 3.5f, 4.2f), neutralWall);

            // Soft, faintly theme-tinted fill light so each room reads as its
            // own space. Realtime placeholder (range stays inside the room so
            // rooms don't cross-light); bake before Quest trials - see
            // docs/AESTHETICS_PLAN.md Part D.
            CreatePointLight(root.transform, $"{content.StationId}_FillLight",
                new Vector3(0f, 4.2f, 2.4f),
                Color.Lerp(Color.white, themeColor, 0.25f), 0.9f, 12f);

            // Room name on the door lintel, readable from the hub side.
            CreateText(root.transform, content.Title,
                new Vector3(0f, 3.5f, 6.1f), 0.035f, 30, TextAnchor.LowerCenter);

            // Icon spans y 3.35-4.95; a two-line title tops out near 3.33.
            CreateLabel(root.transform, content.Title, new Vector3(0f, 2.85f, 0f), 0.05f, 26);
            CreateGoalIcon(root.transform, content, new Vector3(0f, 4.15f, 0f));
            CreatePhotoBoards(root.transform, content);

            // --- Format zones (the study's comparison conditions). Slot
            // positions are authored here; the CounterbalanceManager permutes
            // which zone occupies which slot per participant at runtime.
            var zoneTextPanel = CreateZone(root.transform, content.StationId, "TextPanel",
                "Overview", AttentionTarget.ContentFormat.TextPanel,
                new Vector3(-4.2f, 1.5f, 1f), new Vector2(2.1f, 1.5f), themeColor,
                null, content.OverviewText, 0.019f, 42);
            AddCounterbalanceMarker(zoneTextPanel, 0);

            var zoneDataViz = CreateZone(root.transform, content.StationId, "DataViz",
                "Progress data", AttentionTarget.ContentFormat.DataVisualization,
                new Vector3(-2.1f, 1.5f, 0.3f), new Vector2(1.9f, 1.4f), themeColor,
                content.DataVizUrl, content.DataVizText, 0.019f, 38,
                content.DataVizImageFileName);
            AddCounterbalanceMarker(zoneDataViz, 1);

            var zoneVideo = CreateZone(root.transform, content.StationId, "VideoKiosk",
                "Video story", AttentionTarget.ContentFormat.VideoStory,
                new Vector3(0f, 1.5f, 0f), new Vector2(2.1f, 1.4f), themeColor,
                content.VideoUrl, content.VideoText, 0.019f, 42);
            AddCounterbalanceMarker(zoneVideo, 2);

            var zoneInteractive = CreateZone(root.transform, content.StationId, "Interactive",
                "Case study", AttentionTarget.ContentFormat.InteractiveObject,
                new Vector3(2.1f, 1.5f, 0.3f), new Vector2(1.9f, 1.4f), themeColor,
                content.InteractiveUrl, content.InteractiveText, 0.019f, 36);
            AddCounterbalanceMarker(zoneInteractive, 3);

            var ctaWall = CreateCallToActionWall(root.transform, content, themeColor,
                new Vector3(4.2f, 1.5f, 1f));
            AddCounterbalanceMarker(ctaWall, 4);

            CreateDocent(root.transform, content, themeColor, new Vector3(0f, 0f, 3.4f));

            // References hang on the left side wall, facing into the room.
            var references = CreateZone(root.transform, content.StationId, "References",
                "References", AttentionTarget.ContentFormat.Other,
                new Vector3(-6.1f, 1.6f, 3.5f), new Vector2(1.9f, 1.5f), themeColor,
                null, string.Join("\n", content.References), 0.011f, 66);
            references.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);

            return station;
        }

        private static void AddCounterbalanceMarker(GameObject zone, int slotIndex)
        {
            var marker = zone.AddComponent<CounterbalancedZone>();
            marker.SlotIndex = slotIndex;
        }

        // Front surface of a zone panel's face slab, in zone-local space.
        private const float PanelFaceZ = 0.04f;
        // Clear width of every doorway (hub exits and room doors).
        private const float DoorWidth = 3f;
        private static readonly Color FrameColor = new Color(0.10f, 0.10f, 0.12f);
        private static readonly Color PanelFaceColor = new Color(0.13f, 0.14f, 0.17f);
        private static readonly Color FloorColor = new Color(0.24f, 0.24f, 0.26f);

        /// <summary>Structural wall: keeps its collider so players cannot walk through.</summary>
        private static GameObject CreateWall(Transform parent, string name,
            Vector3 localPos, Vector3 localScale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            ApplyColor(go, color);
            return go;
        }

        /// <summary>Shadowless realtime point light (bake before Quest trials).</summary>
        private static void CreatePointLight(Transform parent, string name,
            Vector3 localPos, Color color, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
        }

        /// <summary>Colliderless, steadily glowing trim bar (wayfinding accent).</summary>
        private static void CreateGlowBar(Transform parent, string name,
            Vector3 localPos, Vector3 localScale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = SteadyGlowMaterial(color, 1.4f);
        }

        /// <summary>
        /// Constant (non-pulsing) bright emissive material, via the
        /// EmissivePulse shader with equal min/max intensity. Falls back to a
        /// plain colored material if the shader is missing.
        /// </summary>
        private static Material SteadyGlowMaterial(Color color, float intensity)
        {
            var shader = Shader.Find("NSFGrant/EmissivePulse");
            if (shader == null)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                var fallback = new Material(cube.GetComponent<Renderer>().sharedMaterial)
                {
                    color = color
                };
                Object.DestroyImmediate(cube);
                return fallback;
            }
            var material = new Material(shader);
            material.SetColor("_Color", color);
            material.SetFloat("_MinIntensity", intensity);
            material.SetFloat("_MaxIntensity", intensity);
            return material;
        }

        private static GameObject CreateZone(Transform parent, string stationId, string zoneName,
            string headerText, AttentionTarget.ContentFormat format,
            Vector3 localPos, Vector2 panelSize, Color themeColor,
            string linkUrl, string bodyText, float bodyCharSize, int bodyWrapChars,
            string imageFileName = null)
        {
            string id = $"{stationId}_{zoneName}";
            var go = new GameObject(id);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;

            // One collider on the zone root covers panel + header; the
            // raycasters resolve hits with GetComponentInParent, and the
            // decorative children carry no colliders of their own.
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.2f, 0f);
            box.size = new Vector3(panelSize.x + 0.16f, panelSize.y + 0.7f, 0.2f);

            AddAttentionTarget(go, id, format, stationId);

            // Framed exhibit panel: dark face for legible white copy, theme
            // header bar, thin frame, and legs down to the platform.
            CreateVisualCube(go.transform, "Frame",
                new Vector3(0f, 0f, -0.025f),
                new Vector3(panelSize.x + 0.14f, panelSize.y + 0.14f, 0.05f), FrameColor);
            CreateVisualCube(go.transform, "Face",
                new Vector3(0f, 0f, 0.005f),
                new Vector3(panelSize.x, panelSize.y, 0.06f), PanelFaceColor);
            CreateVisualCube(go.transform, "Header",
                new Vector3(0f, panelSize.y / 2f + 0.2f, -0.01f),
                new Vector3(panelSize.x + 0.14f, 0.34f, 0.05f), themeColor);
            CreateText(go.transform, headerText,
                new Vector3(0f, panelSize.y / 2f + 0.2f, 0.02f), 0.024f, 40,
                TextAnchor.MiddleCenter);

            float legHeight = localPos.y - panelSize.y / 2f;
            if (legHeight > 0.05f)
            {
                foreach (float x in new[] { -(panelSize.x / 2f - 0.12f), panelSize.x / 2f - 0.12f })
                {
                    CreateVisualCube(go.transform, "Leg",
                        new Vector3(x, -(panelSize.y / 2f + legHeight / 2f), -0.02f),
                        new Vector3(0.07f, legHeight, 0.07f), FrameColor);
                }
            }

            // Optional image (e.g. the 2025 progress-report card); the body
            // copy drops to a caption strip under it.
            float textY = 0f;
            if (!string.IsNullOrEmpty(imageFileName) &&
                CreateTexturedQuad(go.transform, imageFileName, "Image",
                    new Vector3(0f, 0.16f, PanelFaceZ), new Vector2(0.95f, 0.95f)) != null)
            {
                textY = -(panelSize.y / 2f) + 0.2f;
            }

            CreateText(go.transform, bodyText,
                new Vector3(0f, textY, PanelFaceZ + 0.005f),
                bodyCharSize, bodyWrapChars, TextAnchor.MiddleCenter);

            if (!string.IsNullOrEmpty(linkUrl))
            {
                var interactable = go.AddComponent<InteractableObject>();
                var interactableSo = new SerializedObject(interactable);
                interactableSo.FindProperty("objectId").stringValue = id;
                interactableSo.ApplyModifiedPropertiesWithoutUndo();

                var link = go.AddComponent<ContentLink>();
                var linkSo = new SerializedObject(link);
                linkSo.FindProperty("url").stringValue = linkUrl;
                linkSo.ApplyModifiedPropertiesWithoutUndo();
            }

            return go;
        }

        private static GameObject CreateCallToActionWall(Transform parent,
            SdgStationContent content, Color color, Vector3 localPos)
        {
            var wall = new GameObject($"{content.StationId}_CallToAction");
            wall.transform.SetParent(parent, false);
            wall.transform.localPosition = localPos;

            // Backing board + theme header bar, matching the framed zone
            // panels; the option buttons keep their own colliders in front.
            CreateVisualCube(wall.transform, "Backing", new Vector3(0f, 0.3f, -0.06f),
                new Vector3(2.9f, 2.9f, 0.05f), PanelFaceColor);
            CreateVisualCube(wall.transform, "Header", new Vector3(0f, 1.45f, -0.07f),
                new Vector3(2.9f, 0.55f, 0.05f), color);
            CreateText(wall.transform, "What will your library do?\nPick an action:",
                new Vector3(0f, 1.45f, -0.03f), 0.022f, 30, TextAnchor.MiddleCenter);
            foreach (float x in new[] { -1.25f, 1.25f })
            {
                CreateVisualCube(wall.transform, "Leg", new Vector3(x, -1.325f, -0.06f),
                    new Vector3(0.08f, 0.35f, 0.08f), FrameColor);
            }

            for (int i = 0; i < content.CallToActionOptions.Length; i++)
            {
                var button = GameObject.CreatePrimitive(PrimitiveType.Cube);
                string id = $"{content.StationId}_CTA_{i + 1}";
                button.name = id;
                button.transform.SetParent(wall.transform, false);
                button.transform.localPosition = new Vector3(0f, 0.85f - i * 0.55f, 0f);
                button.transform.localScale = new Vector3(2.4f, 0.4f, 0.06f);

                // Darken toward black so the white option text stays legible
                // on light theme colors (e.g. SDG 11 orange).
                ApplyColor(button, Color.Lerp(color, Color.black, 0.25f));
                AddAttentionTarget(button, id,
                    AttentionTarget.ContentFormat.CallToActionWall, content.StationId);

                var interactable = button.AddComponent<InteractableObject>();
                var interactableSo = new SerializedObject(interactable);
                interactableSo.FindProperty("objectId").stringValue = id;
                interactableSo.ApplyModifiedPropertiesWithoutUndo();

                CreateBodyText(button.transform, content.CallToActionOptions[i], 0.022f, 40);
            }

            return wall;
        }

        private static void CreateDocent(Transform parent,
            SdgStationContent content, Color color, Vector3 localPos)
        {
            string id = $"{content.StationId}_Docent_{content.DocentName}";
            var docent = new GameObject(id);
            docent.transform.SetParent(parent, false);
            docent.transform.localPosition = localPos;

            // One capsule collider over the whole figure for gaze + selection.
            var capsule = docent.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.95f, 0f);
            capsule.height = 1.9f;
            capsule.radius = 0.35f;

            AddAttentionTarget(docent, id,
                AttentionTarget.ContentFormat.Docent, content.StationId);

            var interactable = docent.AddComponent<InteractableObject>();
            var interactableSo = new SerializedObject(interactable);
            interactableSo.FindProperty("objectId").stringValue = id;
            interactableSo.ApplyModifiedPropertiesWithoutUndo();

            // Simple primitive figure — robe, head, base ring — standing in
            // for the avatar planned for the Guided condition.
            CreateVisualPrimitive(docent.transform, PrimitiveType.Cylinder, "BaseRing",
                new Vector3(0f, 0.03f, 0f), new Vector3(0.9f, 0.03f, 0.9f),
                Color.Lerp(color, Color.black, 0.4f));
            CreateVisualPrimitive(docent.transform, PrimitiveType.Capsule, "Robe",
                new Vector3(0f, 0.75f, 0f), new Vector3(0.5f, 0.6f, 0.5f),
                Color.Lerp(color, Color.white, 0.35f));
            CreateVisualPrimitive(docent.transform, PrimitiveType.Sphere, "Head",
                new Vector3(0f, 1.55f, 0f), Vector3.one * 0.32f,
                Color.Lerp(color, Color.white, 0.7f));

            CreateText(docent.transform, content.DocentName,
                new Vector3(0f, 1.85f, 0f), 0.03f, 20, TextAnchor.LowerCenter);

            // Greeting on a small framed speech panel beside the figure.
            CreateVisualCube(docent.transform, "SpeechFrame",
                new Vector3(1.35f, 1.45f, 0f), new Vector3(1.9f, 0.95f, 0.04f),
                PanelFaceColor);
            CreateText(docent.transform, content.DocentGreeting,
                new Vector3(1.35f, 1.45f, 0.03f), 0.016f, 44, TextAnchor.MiddleCenter);
        }

        private static void CreateGoalIcon(Transform parent,
            SdgStationContent content, Vector3 localPos)
        {
            string id = $"{content.StationId}_GoalIcon";
            var icon = CreateTexturedQuad(parent, content.IconFileName, id,
                localPos, Vector2.one * 1.6f);
            if (icon == null)
            {
                return;
            }

            // The icon is itself an AOI, so it needs its own collider; give
            // the flat quad explicit depth so gaze rays hit it reliably.
            var box = icon.AddComponent<BoxCollider>();
            box.size = new Vector3(1f, 1f, 0.05f);
            AddAttentionTarget(icon, id,
                AttentionTarget.ContentFormat.Other, content.StationId);
        }

        /// <summary>
        /// Framed UN photos (with attribution captions) hung above the side
        /// panels, flanking the goal icon.
        /// </summary>
        private static void CreatePhotoBoards(Transform parent, SdgStationContent content)
        {
            if (content.PhotoFileNames == null)
            {
                return;
            }

            for (int i = 0; i < content.PhotoFileNames.Length; i++)
            {
                float x = (i % 2 == 0 ? -1f : 1f) * (2.9f + 2.2f * (i / 2));
                string id = $"{content.StationId}_Photo{i + 1}";
                var board = new GameObject(id);
                board.transform.SetParent(parent, false);
                board.transform.localPosition = new Vector3(x, 3.45f, 0.2f);

                var box = board.AddComponent<BoxCollider>();
                box.size = new Vector3(2.15f, 1.5f, 0.1f);
                AddAttentionTarget(board, id,
                    AttentionTarget.ContentFormat.Other, content.StationId);

                CreateVisualCube(board.transform, "Frame",
                    new Vector3(0f, 0f, -0.015f), new Vector3(2.12f, 1.45f, 0.04f),
                    FrameColor);
                CreateTexturedQuad(board.transform, content.PhotoFileNames[i], "Photo",
                    new Vector3(0f, 0f, 0.012f), new Vector2(2f, 1.33f));

                if (content.PhotoCaptions != null && i < content.PhotoCaptions.Length)
                {
                    CreateText(board.transform, content.PhotoCaptions[i],
                        new Vector3(0f, -0.8f, 0.012f), 0.01f, 60,
                        TextAnchor.UpperCenter);
                }
            }
        }

        /// <summary>
        /// Hall floor, lighting, fog and the welcome plinth at the spawn
        /// point. Sized for the 18 m station arc.
        /// </summary>
        private static void CreateHallEnvironment()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(6f, 1f, 6f); // 60 x 60 m
            ApplyColor(floor, FloorColor);

            // Museum-style light: warm key light, cool tri-light ambient, and
            // gentle fog so distant stations recede instead of popping
            // against the skyline.
            var lightGo = GameObject.Find("Directional Light");
            if (lightGo != null)
            {
                var light = lightGo.GetComponent<Light>();
                light.color = new Color(1f, 0.96f, 0.88f);
                light.intensity = 1.05f;
                light.shadows = LightShadows.Soft;
                lightGo.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.68f, 0.78f);
            RenderSettings.ambientEquatorColor = new Color(0.42f, 0.44f, 0.50f);
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.22f, 0.24f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 30f;
            RenderSettings.fogEndDistance = 110f;
            RenderSettings.fogColor = new Color(0.72f, 0.79f, 0.87f);

            ApplyGradientSky();

            CreateHub();
        }

        /// <summary>
        /// Procedural gradient skybox (NSFGrant/GradientSky), saved as a
        /// project asset so the RenderSettings.skybox reference survives scene
        /// save/reload. No-op (default sky kept) if the shader is missing.
        /// </summary>
        private static void ApplyGradientSky()
        {
            var shader = Shader.Find("NSFGrant/GradientSky");
            if (shader == null)
            {
                Debug.LogWarning("[DiscoveryHallBuilder] NSFGrant/GradientSky shader " +
                                 "not found; keeping the default skybox.");
                return;
            }
            const string skyPath = "Assets/StudyContent/DiscoveryHallSky.mat";
            System.IO.Directory.CreateDirectory("Assets/StudyContent");
            AssetDatabase.DeleteAsset(skyPath);
            var skyMat = new Material(shader) { name = "DiscoveryHallSky" };
            AssetDatabase.CreateAsset(skyMat, skyPath);
            RenderSettings.skybox = skyMat;
        }

        /// <summary>
        /// Central hub the player spawns in, modeled on the team's
        /// ReadingNation Waterfall FrameVR room: a hexagonal court with a
        /// stylized waterfall feature and three doorways leading out to the
        /// exhibit rooms (at -120, 0 and +120 degrees).
        /// </summary>
        private static void CreateHub()
        {
            var hub = new GameObject("CentralHub");
            const float apothem = 6f;
            float wallWidth = 2f * apothem * Mathf.Tan(Mathf.PI / 6f) + 0.3f;
            var hubWallColor = new Color(0.30f, 0.30f, 0.33f);

            // Hexagon walls at 60-degree steps; every other side (the three
            // at the room angles) becomes a doorway into a corridor.
            for (int i = 0; i < 6; i++)
            {
                float angle = i * 60f - 180f;
                bool isDoorway = angle == -120f || angle == 0f || angle == 120f;
                var holder = new GameObject(isDoorway
                    ? $"HubDoorway_{angle:F0}" : $"HubWall_{angle:F0}");
                holder.transform.SetParent(hub.transform, false);
                holder.transform.rotation = Quaternion.Euler(0f, angle, 0f);
                holder.transform.position = holder.transform.forward * apothem;

                if (isDoorway)
                {
                    float segW = (wallWidth - DoorWidth) / 2f;
                    foreach (float x in new[] { -(DoorWidth + segW) / 2f, (DoorWidth + segW) / 2f })
                    {
                        CreateWall(holder.transform, "Side",
                            new Vector3(x, 2f, 0f), new Vector3(segW, 4f, 0.2f), hubWallColor);
                    }
                    CreateWall(holder.transform, "Lintel",
                        new Vector3(0f, 3.6f, 0f), new Vector3(DoorWidth, 0.8f, 0.2f),
                        hubWallColor);

                    // Steady warm wayfinding trim framing the opening, facing
                    // the hub interior (local -z). Identical on all three
                    // doorways, so it aids navigation without privileging one
                    // room - distinct from the Condition-C beacon.
                    var trimColor = new Color(1f, 0.93f, 0.78f);
                    foreach (float x in new[] { -DoorWidth / 2f, DoorWidth / 2f })
                    {
                        CreateGlowBar(holder.transform, "DoorTrim",
                            new Vector3(x, 1.6f, -0.12f), new Vector3(0.1f, 3.0f, 0.06f),
                            trimColor);
                    }
                    CreateGlowBar(holder.transform, "DoorTrimTop",
                        new Vector3(0f, 3.1f, -0.12f), new Vector3(DoorWidth + 0.1f, 0.1f, 0.06f),
                        trimColor);
                }
                else
                {
                    CreateWall(holder.transform, "Wall",
                        new Vector3(0f, 2f, 0f), new Vector3(wallWidth, 4f, 0.2f), hubWallColor);
                }
            }

            // Hub floor disc.
            CreateVisualPrimitive(hub.transform, PrimitiveType.Cylinder, "HubFloor",
                new Vector3(0f, 0.02f, 0f), new Vector3(13.8f, 0.015f, 13.8f),
                new Color(0.30f, 0.30f, 0.33f));

            // Cool accent light picking out the waterfall (realtime
            // placeholder; bake before Quest trials).
            CreatePointLight(hub.transform, "WaterfallAccentLight",
                new Vector3(0f, 3.2f, -4.6f), new Color(0.6f, 0.8f, 1f), 1.1f, 9f);

            // Stylized waterfall against the solid south wall — the hub's
            // namesake centerpiece. The two sheets scroll downward (the
            // inner one faster) and the basin ripples slowly, all on the GPU
            // via NSFGrant/AnimatedWater.
            var stone = new Color(0.36f, 0.38f, 0.42f);
            CreateWall(hub.transform, "WaterfallMonolith",
                new Vector3(0f, 2f, -5.55f), new Vector3(2.4f, 4f, 0.5f), stone);
            CreateWaterQuad(hub.transform, "WaterSheet",
                new Vector3(0f, 2.05f, -5.28f), new Vector2(1.9f, 3.7f),
                new Color(0.5f, 0.75f, 0.95f, 0.45f), new Vector2(0f, -0.55f), 2.2f);
            CreateWaterQuad(hub.transform, "WaterSheetInner",
                new Vector3(0f, 1.9f, -5.24f), new Vector2(1.5f, 3.3f),
                new Color(0.65f, 0.85f, 1f, 0.3f), new Vector2(0f, -0.95f), 3.1f);
            CreateVisualPrimitive(hub.transform, PrimitiveType.Cylinder, "BasinRim",
                new Vector3(0f, 0.18f, -4.7f), new Vector3(3.4f, 0.18f, 2.4f), stone);
            var pond = CreateVisualPrimitive(hub.transform, PrimitiveType.Cylinder, "BasinWater",
                new Vector3(0f, 0.3f, -4.7f), new Vector3(3.1f, 0.03f, 2.1f), Color.white);
            pond.GetComponent<Renderer>().sharedMaterial = AnimatedWaterMaterial(
                new Color(0.5f, 0.75f, 0.95f, 0.55f), new Vector2(0.04f, 0.05f), 1.1f);

            // Low info plinth between spawn and the center; the sign sits
            // below eye level so it never occludes the doorways.
            var plinth = new GameObject("WelcomePlinth");
            plinth.transform.SetParent(hub.transform, false);
            plinth.transform.localPosition = new Vector3(0f, 0f, -0.5f);
            // Spawn is at -Z, so the plinth faces backward toward it.
            plinth.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            CreateVisualPrimitive(plinth.transform, PrimitiveType.Cylinder, "Dais",
                new Vector3(0f, 0.03f, 0f), new Vector3(1.6f, 0.03f, 1.6f), FrameColor);
            CreateVisualCube(plinth.transform, "Column",
                new Vector3(0f, 0.5f, 0f), new Vector3(0.12f, 1f, 0.12f), FrameColor);

            var sign = CreateVisualCube(plinth.transform, "Sign",
                new Vector3(0f, 1.1f, 0f), new Vector3(1.5f, 0.6f, 0.04f),
                PanelFaceColor);
            sign.transform.localRotation = Quaternion.Euler(-22f, 0f, 0f);
            CreateText(sign.transform, "UN SDG Discovery Hall\n\n" +
                "Three exhibit rooms lead off this hub. Look closely, explore " +
                "the exhibits, and pick an action for your library.",
                new Vector3(0f, 0f, 0.6f), 0.015f, 34, TextAnchor.MiddleCenter);
        }

        /// <summary>Animated translucent water sheet facing the hub center (+Z).</summary>
        private static void CreateWaterQuad(Transform parent, string name,
            Vector3 localPos, Vector2 size, Color color, Vector2 scrollSpeed, float waveSpeed)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(parent, false);
            quad.transform.localPosition = localPos;
            quad.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            quad.transform.localScale = new Vector3(size.x, size.y, 1f);
            quad.GetComponent<Renderer>().sharedMaterial =
                AnimatedWaterMaterial(color, scrollSpeed, waveSpeed);
        }

        /// <summary>
        /// GPU-animated water material (NSFGrant/AnimatedWater). Falls back to
        /// the static translucent shader if the animated one is missing, so
        /// the build never breaks.
        /// </summary>
        private static Material AnimatedWaterMaterial(Color color, Vector2 scrollSpeed,
            float waveSpeed)
        {
            var shader = Shader.Find("NSFGrant/AnimatedWater");
            if (shader == null)
            {
                Debug.LogWarning("[DiscoveryHallBuilder] NSFGrant/AnimatedWater shader " +
                                 "not found; falling back to static water.");
                return TransparentMaterial(color);
            }
            var material = new Material(shader);
            material.SetColor("_Color", color);
            material.SetVector("_ScrollSpeed", new Vector4(scrollSpeed.x, scrollSpeed.y, 0f, 0f));
            material.SetFloat("_WaveSpeed", waveSpeed);
            return material;
        }

        private static Material TransparentMaterial(Color color)
        {
            var shader = Shader.Find("NSFGrant/UnlitTransparentColor");
            if (shader == null)
            {
                // Fallback keeps the build working, just opaque.
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                var fallback = new Material(cube.GetComponent<Renderer>().sharedMaterial)
                {
                    color = color
                };
                Object.DestroyImmediate(cube);
                return fallback;
            }
            var material = new Material(shader);
            material.SetColor("_Color", color);
            return material;
        }

        /// <summary>
        /// Decorative primitive: its collider is removed so it never
        /// intercepts gaze rays or clicks meant for AOI colliders.
        /// </summary>
        private static GameObject CreateVisualPrimitive(Transform parent, PrimitiveType type,
            string name, Vector3 localPos, Vector3 localScale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            ApplyColor(go, color);
            return go;
        }

        private static GameObject CreateVisualCube(Transform parent, string name,
            Vector3 localPos, Vector3 localScale, Color color)
        {
            return CreateVisualPrimitive(parent, PrimitiveType.Cube, name,
                localPos, localScale, color);
        }

        /// <summary>
        /// Unlit textured quad (colliderless), facing the station's readable
        /// +Z side. Returns null (with a hint) when the texture is missing.
        /// </summary>
        private static GameObject CreateTexturedQuad(Transform parent, string fileName,
            string name, Vector3 localPos, Vector2 size)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                $"{TexturesDir}/{fileName}");
            if (texture == null)
            {
                Debug.Log($"[DiscoveryHallBuilder] Texture {fileName} not found - run " +
                          "NSF Grant > Download SDG Media Assets and rebuild the scene.");
                return null;
            }

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(parent, false);
            quad.transform.localPosition = localPos;
            quad.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            quad.transform.localScale = new Vector3(size.x, size.y, 1f);

            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit == null)
            {
                unlit = Shader.Find("Unlit/Texture");
            }
            var material = new Material(unlit) { mainTexture = texture };
            quad.GetComponent<Renderer>().sharedMaterial = material;
            return quad;
        }

        private static QuizDefinition CreateQuizAsset()
        {
            var quiz = ScriptableObject.CreateInstance<QuizDefinition>();
            quiz.questions = new[]
            {
                new QuizDefinition.Question
                {
                    questionId = "sdg4_q1",
                    prompt = "What is the aim of SDG 4?",
                    options = new[]
                    {
                        "Ensure inclusive, equitable quality education and lifelong learning for all",
                        "End hunger and improve nutrition",
                        "Make cities inclusive, safe, resilient and sustainable",
                        "Conserve the oceans and marine resources"
                    },
                    correctIndex = 0
                },
                new QuizDefinition.Question
                {
                    questionId = "sdg4_q2",
                    prompt = "According to the UN, accelerating progress on Goal 4 would have what effect?",
                    options = new[]
                    {
                        "A catalytic effect on the whole 2030 Agenda",
                        "No effect on other goals",
                        "It only affects high-income countries",
                        "It would slow progress on climate action"
                    },
                    correctIndex = 0
                },
                new QuizDefinition.Question
                {
                    questionId = "sdg11_q1",
                    prompt = "SDG 11 aims to make cities and human settlements...",
                    options = new[]
                    {
                        "inclusive, safe, resilient and sustainable",
                        "larger and denser",
                        "centers of industrial production",
                        "car-free by 2030"
                    },
                    correctIndex = 0
                },
                new QuizDefinition.Question
                {
                    questionId = "sdg11_q2",
                    prompt = "Helsinki's Oodi Central Library is notable as...",
                    options = new[]
                    {
                        "a nearly zero-energy public space co-designed with residents",
                        "the world's largest book archive",
                        "a private members-only library",
                        "a university research library"
                    },
                    correctIndex = 0
                },
                new QuizDefinition.Question
                {
                    questionId = "sdg13_q1",
                    prompt = "What does SDG 13 call for?",
                    options = new[]
                    {
                        "Urgent action to combat climate change and its impacts",
                        "Universal access to clean water",
                        "Gender equality in education",
                        "Reduced inequality among countries"
                    },
                    correctIndex = 0
                },
                new QuizDefinition.Question
                {
                    questionId = "sdg13_q2",
                    prompt = "Thammasat University Library's award-winning green program is built around...",
                    options = new[]
                    {
                        "the circular economy (From Waste to Wealth)",
                        "banning printed books",
                        "solar-powered bookmobiles only",
                        "closing the library to save energy"
                    },
                    correctIndex = 0
                },
                new QuizDefinition.Question
                {
                    questionId = "sdg13_q3",
                    prompt = "A seed library primarily helps a community by...",
                    options = new[]
                    {
                        "sharing and tracking seeds for local growing",
                        "selling rare plants",
                        "storing grain reserves",
                        "replacing public gardens"
                    },
                    correctIndex = 0
                }
            };

            System.IO.Directory.CreateDirectory("Assets/StudyContent");
            const string assetPath = "Assets/StudyContent/SdgKnowledgeQuiz.asset";
            AssetDatabase.DeleteAsset(assetPath);
            AssetDatabase.CreateAsset(quiz, assetPath);
            return quiz;
        }

        private static void AddAttentionTarget(GameObject go, string id,
            AttentionTarget.ContentFormat format, string stationId)
        {
            var target = go.AddComponent<AttentionTarget>();
            var so = new SerializedObject(target);
            so.FindProperty("targetId").stringValue = id;
            so.FindProperty("format").enumValueIndex = (int)format;
            so.FindProperty("stationId").stringValue = stationId;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ApplyColor(GameObject go, Color color)
        {
            var renderer = go.GetComponent<Renderer>();
            var material = new Material(renderer.sharedMaterial) { color = color };
            renderer.sharedMaterial = material;
        }

        /// <summary>Floating title text above an object.</summary>
        private static void CreateLabel(Transform parent, string text, Vector3 localPos,
            float size, int maxLineChars)
        {
            var mesh = CreateTextMesh(parent, localPos, size);
            mesh.anchor = TextAnchor.LowerCenter;
            mesh.text = Wrap(text, maxLineChars);
        }

        /// <summary>Wrapped white text at an explicit local position.</summary>
        private static void CreateText(Transform parent, string text, Vector3 localPos,
            float size, int maxLineChars, TextAnchor anchor)
        {
            var mesh = CreateTextMesh(parent, localPos, size);
            mesh.anchor = anchor;
            mesh.text = Wrap(text, maxLineChars);
        }

        /// <summary>Body copy centered on the front face of a scaled panel primitive.</summary>
        private static void CreateBodyText(Transform parent, string text, float size,
            int maxLineChars)
        {
            // Stations face the visitor along local +Z, so the readable face
            // of each panel is its +Z side.
            var mesh = CreateTextMesh(parent, new Vector3(0f, 0f, 0.51f), size);
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.text = Wrap(text, maxLineChars);
        }

        /// <summary>
        /// TextMesh has no word wrapping; long lines render meters wide and
        /// collide with neighboring stations. Re-break each line on word
        /// boundaries so no line exceeds the panel's character budget.
        /// </summary>
        private static string Wrap(string text, int maxLineChars)
        {
            var result = new System.Text.StringBuilder();
            foreach (string line in text.Split('\n'))
            {
                int lineLength = 0;
                foreach (string word in line.Split(' '))
                {
                    if (lineLength > 0 && lineLength + 1 + word.Length > maxLineChars)
                    {
                        result.Append('\n');
                        lineLength = 0;
                    }
                    else if (lineLength > 0)
                    {
                        result.Append(' ');
                        lineLength++;
                    }
                    result.Append(word);
                    lineLength += word.Length;
                }
                result.Append('\n');
            }
            return result.ToString().TrimEnd('\n');
        }

        private static TextMesh CreateTextMesh(Transform parent, Vector3 localPos, float size)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            // Stations face the visitor along local +Z; flip the TextMesh so
            // it reads correctly from that side.
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            // Counteract parent scaling so text is not distorted.
            Vector3 lossy = parent.lossyScale;
            go.transform.localScale = new Vector3(
                lossy.x != 0f ? 1f / lossy.x : 1f,
                lossy.y != 0f ? 1f / lossy.y : 1f,
                lossy.z != 0f ? 1f / lossy.z : 1f);

            // World-space line height is fontSize * characterSize / 10, so at
            // fontSize 48 a characterSize of 0.015 gives ~7 cm lines; average
            // glyph width is roughly half the line height. Size text budgets
            // against the 1.6-1.8 m panels accordingly.
            var mesh = go.AddComponent<TextMesh>();
            mesh.characterSize = size;
            mesh.fontSize = 48;
            mesh.alignment = TextAlignment.Center;
            mesh.color = Color.white;

            // The built-in font material draws with ZTest Always, so text
            // renders through panels and walls; swap in the depth-tested
            // variant so geometry occludes text naturally.
            var font = mesh.font;
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                mesh.font = font;
            }
            go.GetComponent<MeshRenderer>().sharedMaterial = GetOccludedTextMaterial(font);

            return mesh;
        }

        private static Material occludedTextMaterial;

        private static Material GetOccludedTextMaterial(Font font)
        {
            // One shared material per build; the null check also covers the
            // previous build's material destroyed by NewScene.
            if (occludedTextMaterial == null)
            {
                var shader = Shader.Find("NSFGrant/TextOccluded");
                if (shader == null)
                {
                    Debug.LogWarning("[DiscoveryHallBuilder] NSFGrant/TextOccluded shader " +
                                     "not found; text will render through geometry.");
                    return font.material;
                }
                occludedTextMaterial = new Material(shader)
                {
                    mainTexture = font.material.mainTexture
                };
            }
            return occludedTextMaterial;
        }
    }
}
