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

            // --- Stations arranged in an arc in front of the spawn point.
            var stationComponents = new List<SdgStation>();
            float[] angles = { -55f, 0f, 55f };
            const float radius = 18f;

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
            // Copy the primitive's default material so the beacon follows
            // the active render pipeline (Built-in or URP).
            var beaconMat = new Material(beacon.GetComponent<Renderer>().sharedMaterial)
            {
                color = Color.cyan
            };
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

        private static SdgStation BuildStation(SdgStationContent content, Vector3 center)
        {
            Color themeColor = Color.gray;
            ColorUtility.TryParseHtmlString(content.ThemeColorHex, out themeColor);

            var root = new GameObject(content.StationId);
            root.transform.position = center;
            // Stations face the spawn point at the hall center.
            root.transform.rotation = Quaternion.LookRotation(-center.normalized, Vector3.up);

            // Trigger volume defining the station footprint.
            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 2f, 0.5f);
            trigger.size = new Vector3(13f, 5f, 9f);

            var station = root.AddComponent<SdgStation>();
            var stationSo = new SerializedObject(station);
            stationSo.FindProperty("stationId").stringValue = content.StationId;
            stationSo.ApplyModifiedPropertiesWithoutUndo();

            // Booth dressing: a theme-tinted platform disc and a muted back
            // wall so panels read against a wall instead of open sky.
            // Thin and colliderless so it neither trips the character
            // controller nor catches gaze rays.
            CreateVisualPrimitive(root.transform, PrimitiveType.Cylinder,
                $"{content.StationId}_Platform",
                new Vector3(0f, 0.02f, 0.8f), new Vector3(13f, 0.015f, 8.5f),
                Color.Lerp(themeColor, FloorColor, 0.78f));

            var backWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            backWall.name = $"{content.StationId}_BackWall";
            backWall.transform.SetParent(root.transform, false);
            backWall.transform.localPosition = new Vector3(0f, 2.5f, -1.2f);
            backWall.transform.localScale = new Vector3(12.6f, 5f, 0.15f);
            ApplyColor(backWall, Color.Lerp(themeColor, Color.black, 0.65f));

            // Icon spans y 3.5-5.0; a two-line title tops out near 3.4.
            CreateLabel(root.transform, content.Title, new Vector3(0f, 3.0f, 0f), 0.04f, 26);
            CreateGoalIcon(root.transform, content, new Vector3(0f, 4.25f, 0f));
            CreatePhotoBoards(root.transform, content);

            // --- Format zones (the study's comparison conditions). Slot
            // positions are authored here; the CounterbalanceManager permutes
            // which zone occupies which slot per participant at runtime.
            var zoneTextPanel = CreateZone(root.transform, content.StationId, "TextPanel",
                "Overview", AttentionTarget.ContentFormat.TextPanel,
                new Vector3(-3.6f, 1.5f, 1f), new Vector2(1.8f, 1.3f), themeColor,
                null, content.OverviewText, 0.015f, 48);
            AddCounterbalanceMarker(zoneTextPanel, 0);

            var zoneDataViz = CreateZone(root.transform, content.StationId, "DataViz",
                "Progress data", AttentionTarget.ContentFormat.DataVisualization,
                new Vector3(-1.8f, 1.5f, 0.3f), new Vector2(1.6f, 1.2f), themeColor,
                content.DataVizUrl, content.DataVizText, 0.016f, 38,
                content.DataVizImageFileName);
            AddCounterbalanceMarker(zoneDataViz, 1);

            var zoneVideo = CreateZone(root.transform, content.StationId, "VideoKiosk",
                "Video story", AttentionTarget.ContentFormat.VideoStory,
                new Vector3(0f, 1.5f, 0f), new Vector2(1.8f, 1.2f), themeColor,
                content.VideoUrl, content.VideoText, 0.016f, 42);
            AddCounterbalanceMarker(zoneVideo, 2);

            var zoneInteractive = CreateZone(root.transform, content.StationId, "Interactive",
                "Case study", AttentionTarget.ContentFormat.InteractiveObject,
                new Vector3(1.8f, 1.5f, 0.3f), new Vector2(1.6f, 1.2f), themeColor,
                content.InteractiveUrl, content.InteractiveText, 0.016f, 38);
            AddCounterbalanceMarker(zoneInteractive, 3);

            var ctaWall = CreateCallToActionWall(root.transform, content, themeColor,
                new Vector3(3.6f, 1.5f, 1f));
            AddCounterbalanceMarker(ctaWall, 4);

            CreateDocent(root.transform, content, themeColor, new Vector3(0f, 0f, 2.4f));

            CreateZone(root.transform, content.StationId, "References",
                "References", AttentionTarget.ContentFormat.Other,
                new Vector3(-5.2f, 1.5f, 2f), new Vector2(1.8f, 1.3f), themeColor,
                null, string.Join("\n", content.References), 0.009f, 80);

            return station;
        }

        private static void AddCounterbalanceMarker(GameObject zone, int slotIndex)
        {
            var marker = zone.AddComponent<CounterbalancedZone>();
            marker.SlotIndex = slotIndex;
        }

        // Front surface of a zone panel's face slab, in zone-local space.
        private const float PanelFaceZ = 0.04f;
        private static readonly Color FrameColor = new Color(0.10f, 0.10f, 0.12f);
        private static readonly Color PanelFaceColor = new Color(0.13f, 0.14f, 0.17f);
        private static readonly Color FloorColor = new Color(0.24f, 0.24f, 0.26f);

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
            box.center = new Vector3(0f, 0.16f, 0f);
            box.size = new Vector3(panelSize.x + 0.16f, panelSize.y + 0.6f, 0.2f);

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
                new Vector3(0f, panelSize.y / 2f + 0.17f, -0.01f),
                new Vector3(panelSize.x + 0.14f, 0.28f, 0.05f), themeColor);
            CreateText(go.transform, headerText,
                new Vector3(0f, panelSize.y / 2f + 0.17f, 0.02f), 0.018f, 40,
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
                    new Vector3(0f, 0.13f, PanelFaceZ), new Vector2(0.82f, 0.82f)) != null)
            {
                textY = -(panelSize.y / 2f) + 0.18f;
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
            CreateVisualCube(wall.transform, "Backing", new Vector3(0f, 0.35f, -0.06f),
                new Vector3(2.5f, 2.5f, 0.05f), PanelFaceColor);
            CreateVisualCube(wall.transform, "Header", new Vector3(0f, 1.55f, -0.07f),
                new Vector3(2.5f, 0.5f, 0.05f), color);
            CreateText(wall.transform, "What will your library do?\nPick an action:",
                new Vector3(0f, 1.55f, -0.03f), 0.018f, 40, TextAnchor.MiddleCenter);
            foreach (float x in new[] { -1.05f, 1.05f })
            {
                CreateVisualCube(wall.transform, "Leg", new Vector3(x, -1.2f, -0.06f),
                    new Vector3(0.08f, 0.6f, 0.08f), FrameColor);
            }

            for (int i = 0; i < content.CallToActionOptions.Length; i++)
            {
                var button = GameObject.CreatePrimitive(PrimitiveType.Cube);
                string id = $"{content.StationId}_CTA_{i + 1}";
                button.name = id;
                button.transform.SetParent(wall.transform, false);
                button.transform.localPosition = new Vector3(0f, 0.7f - i * 0.45f, 0f);
                button.transform.localScale = new Vector3(2f, 0.32f, 0.06f);

                // Darken toward black so the white option text stays legible
                // on light theme colors (e.g. SDG 11 orange).
                ApplyColor(button, Color.Lerp(color, Color.black, 0.25f));
                AddAttentionTarget(button, id,
                    AttentionTarget.ContentFormat.CallToActionWall, content.StationId);

                var interactable = button.AddComponent<InteractableObject>();
                var interactableSo = new SerializedObject(interactable);
                interactableSo.FindProperty("objectId").stringValue = id;
                interactableSo.ApplyModifiedPropertiesWithoutUndo();

                CreateBodyText(button.transform, content.CallToActionOptions[i], 0.018f, 50);
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
                new Vector3(0f, 1.85f, 0f), 0.035f, 20, TextAnchor.LowerCenter);

            // Greeting on a small framed speech panel beside the figure.
            CreateVisualCube(docent.transform, "SpeechFrame",
                new Vector3(1.2f, 1.4f, 0f), new Vector3(1.55f, 0.8f, 0.04f),
                PanelFaceColor);
            CreateText(docent.transform, content.DocentGreeting,
                new Vector3(1.2f, 1.4f, 0.03f), 0.013f, 46, TextAnchor.MiddleCenter);
        }

        private static void CreateGoalIcon(Transform parent,
            SdgStationContent content, Vector3 localPos)
        {
            string id = $"{content.StationId}_GoalIcon";
            var icon = CreateTexturedQuad(parent, content.IconFileName, id,
                localPos, Vector2.one * 1.5f);
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
                float x = (i % 2 == 0 ? -1f : 1f) * (2.6f + 2.2f * (i / 2));
                string id = $"{content.StationId}_Photo{i + 1}";
                var board = new GameObject(id);
                board.transform.SetParent(parent, false);
                board.transform.localPosition = new Vector3(x, 3.35f, 0.2f);

                var box = board.AddComponent<BoxCollider>();
                box.size = new Vector3(1.75f, 1.25f, 0.1f);
                AddAttentionTarget(board, id,
                    AttentionTarget.ContentFormat.Other, content.StationId);

                CreateVisualCube(board.transform, "Frame",
                    new Vector3(0f, 0f, -0.015f), new Vector3(1.72f, 1.2f, 0.04f),
                    FrameColor);
                CreateTexturedQuad(board.transform, content.PhotoFileNames[i], "Photo",
                    new Vector3(0f, 0f, 0.012f), new Vector2(1.6f, 1.08f));

                if (content.PhotoCaptions != null && i < content.PhotoCaptions.Length)
                {
                    CreateText(board.transform, content.PhotoCaptions[i],
                        new Vector3(0f, -0.66f, 0.012f), 0.008f, 70,
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
            RenderSettings.fogColor = new Color(0.70f, 0.75f, 0.82f);

            // Low info plinth at the hall center; the sign sits below eye
            // level so it never occludes the stations from spawn.
            var plinth = new GameObject("WelcomePlinth");
            plinth.transform.position = new Vector3(0f, 0f, -0.5f);
            // Spawn is at -Z, so the plinth faces backward toward it.
            plinth.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            CreateVisualPrimitive(plinth.transform, PrimitiveType.Cylinder, "Dais",
                new Vector3(0f, 0.03f, 0f), new Vector3(1.6f, 0.03f, 1.6f), FrameColor);
            CreateVisualCube(plinth.transform, "Column",
                new Vector3(0f, 0.5f, 0f), new Vector3(0.12f, 1f, 0.12f), FrameColor);

            var sign = CreateVisualCube(plinth.transform, "Sign",
                new Vector3(0f, 1.1f, 0f), new Vector3(1.3f, 0.55f, 0.04f),
                PanelFaceColor);
            sign.transform.localRotation = Quaternion.Euler(-22f, 0f, 0f);
            CreateText(sign.transform, "UN SDG Discovery Hall\n\n" +
                "Visit all three stations. Look closely, explore the exhibits, " +
                "and pick an action for your library.",
                new Vector3(0f, 0f, 0.6f), 0.012f, 36, TextAnchor.MiddleCenter);
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
            return mesh;
        }
    }
}
