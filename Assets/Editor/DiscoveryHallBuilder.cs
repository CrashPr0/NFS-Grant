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

            // --- Hall geometry.
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(4f, 1f, 4f); // 40 x 40 m

            // --- Stations arranged in an arc in front of the spawn point.
            var stationComponents = new List<SdgStation>();
            float[] angles = { -55f, 0f, 55f };
            const float radius = 10f;

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
            trigger.center = new Vector3(0f, 2f, 0f);
            trigger.size = new Vector3(10f, 4f, 7f);

            var station = root.AddComponent<SdgStation>();
            var stationSo = new SerializedObject(station);
            stationSo.FindProperty("stationId").stringValue = content.StationId;
            stationSo.ApplyModifiedPropertiesWithoutUndo();

            CreateLabel(root.transform, content.Title, new Vector3(0f, 3.2f, 0f), 0.04f, 26);
            CreateGoalIcon(root.transform, content, new Vector3(0f, 4.2f, 0f));

            // --- Format zones (the study's comparison conditions). Slot
            // positions are authored here; the CounterbalanceManager permutes
            // which zone occupies which slot per participant at runtime.
            var zoneTextPanel = CreateZone(root.transform, content.StationId, "TextPanel",
                AttentionTarget.ContentFormat.TextPanel, PrimitiveType.Cube,
                new Vector3(-3.6f, 1.5f, 1f), new Vector3(1.8f, 1.3f, 0.08f), themeColor,
                null, content.OverviewText, 0.015f, 48);
            AddCounterbalanceMarker(zoneTextPanel, 0);

            var zoneDataViz = CreateZone(root.transform, content.StationId, "DataViz",
                AttentionTarget.ContentFormat.DataVisualization, PrimitiveType.Cube,
                new Vector3(-1.8f, 1.5f, 0.3f), new Vector3(1.6f, 1.2f, 0.08f), themeColor,
                content.DataVizUrl, content.DataVizText, 0.016f, 38);
            AddCounterbalanceMarker(zoneDataViz, 1);

            var zoneVideo = CreateZone(root.transform, content.StationId, "VideoKiosk",
                AttentionTarget.ContentFormat.VideoStory, PrimitiveType.Cube,
                new Vector3(0f, 1.5f, 0f), new Vector3(1.8f, 1.2f, 0.08f), themeColor,
                content.VideoUrl, content.VideoText, 0.016f, 42);
            AddCounterbalanceMarker(zoneVideo, 2);

            var zoneInteractive = CreateZone(root.transform, content.StationId, "Interactive",
                AttentionTarget.ContentFormat.InteractiveObject, PrimitiveType.Cube,
                new Vector3(1.8f, 1.5f, 0.3f), new Vector3(1.6f, 1.2f, 0.08f), themeColor,
                content.InteractiveUrl, content.InteractiveText, 0.016f, 38);
            AddCounterbalanceMarker(zoneInteractive, 3);

            var ctaWall = CreateCallToActionWall(root.transform, content, themeColor,
                new Vector3(3.6f, 1.5f, 1f));
            AddCounterbalanceMarker(ctaWall, 4);

            CreateDocent(root.transform, content, themeColor, new Vector3(0f, 0f, 2.4f));

            CreateReferencesBoard(root.transform, content, new Vector3(-5.2f, 1.5f, 2f));

            return station;
        }

        private static void AddCounterbalanceMarker(GameObject zone, int slotIndex)
        {
            var marker = zone.AddComponent<CounterbalancedZone>();
            marker.SlotIndex = slotIndex;
        }

        private static GameObject CreateZone(Transform parent, string stationId, string zoneName,
            AttentionTarget.ContentFormat format, PrimitiveType primitive,
            Vector3 localPos, Vector3 localScale, Color color,
            string linkUrl, string bodyText, float bodyCharSize, int bodyWrapChars)
        {
            var go = GameObject.CreatePrimitive(primitive);
            string id = $"{stationId}_{zoneName}";
            go.name = id;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;

            ApplyColor(go, color);
            AddAttentionTarget(go, id, format, stationId);

            // Body copy floats just in front of the panel face.
            CreateBodyText(go.transform, bodyText, bodyCharSize, bodyWrapChars);

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

            CreateLabel(wall.transform, "What will your library do?\nPick an action:",
                new Vector3(0f, 1.1f, 0f), 0.025f, 30);

            for (int i = 0; i < content.CallToActionOptions.Length; i++)
            {
                var button = GameObject.CreatePrimitive(PrimitiveType.Cube);
                string id = $"{content.StationId}_CTA_{i + 1}";
                button.name = id;
                button.transform.SetParent(wall.transform, false);
                button.transform.localPosition = new Vector3(0f, 0.7f - i * 0.45f, 0f);
                button.transform.localScale = new Vector3(2f, 0.32f, 0.06f);

                ApplyColor(button, Color.Lerp(color, Color.white, 0.25f));
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
            var docent = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            string id = $"{content.StationId}_Docent_{content.DocentName}";
            docent.name = id;
            docent.transform.SetParent(parent, false);
            docent.transform.localPosition = localPos + Vector3.up;
            docent.transform.localScale = new Vector3(0.5f, 1f, 0.5f);

            ApplyColor(docent, Color.Lerp(color, Color.white, 0.5f));
            AddAttentionTarget(docent, id,
                AttentionTarget.ContentFormat.Docent, content.StationId);

            var interactable = docent.AddComponent<InteractableObject>();
            var interactableSo = new SerializedObject(interactable);
            interactableSo.FindProperty("objectId").stringValue = id;
            interactableSo.ApplyModifiedPropertiesWithoutUndo();

            CreateLabel(docent.transform, content.DocentName, new Vector3(0f, 1.3f, 0f), 0.04f, 20);
            CreateBodyText(docent.transform, content.DocentGreeting, 0.015f, 44);
        }

        private static void CreateReferencesBoard(Transform parent,
            SdgStationContent content, Vector3 localPos)
        {
            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            string id = $"{content.StationId}_References";
            board.name = id;
            board.transform.SetParent(parent, false);
            board.transform.localPosition = localPos;
            board.transform.localScale = new Vector3(1.8f, 1.3f, 0.06f);

            ApplyColor(board, new Color(0.15f, 0.15f, 0.15f));
            AddAttentionTarget(board, id,
                AttentionTarget.ContentFormat.Other, content.StationId);

            string text = "References\n" + string.Join("\n", content.References);
            CreateBodyText(board.transform, text, 0.009f, 80);
        }

        private static void CreateGoalIcon(Transform parent,
            SdgStationContent content, Vector3 localPos)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                $"{TexturesDir}/{content.IconFileName}");
            if (texture == null)
            {
                // Icons not downloaded yet; the builder logs a hint instead.
                Debug.Log($"[DiscoveryHallBuilder] Icon {content.IconFileName} not found - " +
                          "run NSF Grant > Download SDG Media Assets and rebuild the scene to apply icons.");
                return;
            }

            var icon = GameObject.CreatePrimitive(PrimitiveType.Quad);
            string id = $"{content.StationId}_GoalIcon";
            icon.name = id;
            icon.transform.SetParent(parent, false);
            icon.transform.localPosition = localPos;
            icon.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            icon.transform.localScale = Vector3.one * 1.5f;

            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit == null)
            {
                unlit = Shader.Find("Unlit/Texture");
            }
            var material = new Material(unlit) { mainTexture = texture };
            icon.GetComponent<Renderer>().sharedMaterial = material;

            AddAttentionTarget(icon, id,
                AttentionTarget.ContentFormat.Other, content.StationId);
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

        /// <summary>Body copy centered on the front face of a panel.</summary>
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
