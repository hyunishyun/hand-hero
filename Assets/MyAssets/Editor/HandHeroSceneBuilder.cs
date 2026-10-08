using System.IO;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;

namespace HandHero.EditorTools
{
    // Generates the HandHero scenes from code (never hand-edit scene YAML).
    // Idempotent: every run rebuilds the scenes and reuses/updates the generated materials.
    // Batch mode: -executeMethod HandHero.EditorTools.HandHeroSceneBuilder.BuildAll
    public static class HandHeroSceneBuilder
    {
        private const string SceneDir = "Assets/MyAssets/Scenes";
        private const string MaterialDir = "Assets/MyAssets/Generated/Materials";
        private const string BotDir = "Assets/MyAssets/Generated/Bot";
        public const string SandboxScenePath = SceneDir + "/HandHero_Sandbox.unity";
        public const string ArenaMainScenePath = SceneDir + "/Arena_Main.unity";
        public const string BotDifficultyPath = BotDir + "/BotDifficulty_Normal.asset";

        // Heroes stay on the Default layer: PointingBeamController skips its own
        // hero's colliders, so player and bot can hit each other without a
        // FlyingHero layer / aimMask rule. The reticle has no collider (else the
        // aim ray hits it and jitters).

        private static readonly Vector3 ArenaCenter = new Vector3(0f, 2f, 20f);
        private static readonly Vector3 ArenaSize = new Vector3(35f, 20f, 35f);

        // Seated eye height above the XR Origin (Device tracking origin: the
        // headset's start pose is the origin, so every player sees the same layout).
        private const float SeatEyeHeight = 1.2f;

        [MenuItem("HandHero/Build All Scenes")]
        public static void BuildAll()
        {
            BuildSandbox();
            BuildArenaMain();
            RegisterBuildScenes();
        }

        // Headset-free test scene: plain camera, greybox arena, player hero,
        // targets, and debug keyboard/mouse input (XR hand input is present too,
        // swap the controllers' Input Source field to use it).
        [MenuItem("HandHero/Build Sandbox Scene")]
        public static void BuildSandbox()
        {
            BuildScene(SandboxScenePath, xr: false);
        }

        // Headset scene for the APK: fixed XR Origin (never moved, ADR 4/5), XR
        // hand input drives the player hero, tutorial and menus.
        [MenuItem("HandHero/Build Arena_Main Scene")]
        public static void BuildArenaMain()
        {
            BuildScene(ArenaMainScenePath, xr: true);
        }

        // Arena_Main first and enabled; older class-project scenes stay listed but disabled.
        public static void RegisterBuildScenes()
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(ArenaMainScenePath, true),
            };
            foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
            {
                if (existing.path == ArenaMainScenePath) continue;
                scenes.Add(new EditorBuildSettingsScene(existing.path, false));
            }
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"[HandHeroSceneBuilder] Build scenes: {ArenaMainScenePath} (+{scenes.Count - 1} disabled)");
        }

        private static void BuildScene(string scenePath, bool xr)
        {
            EnsureFolder(SceneDir);
            EnsureFolder(MaterialDir);

            Material floorMat = LitMaterial("Greybox_Floor", new Color(0.35f, 0.37f, 0.4f));
            Material wallMat = LitMaterial("Greybox_Wall", new Color(0.5f, 0.52f, 0.56f));
            Material heroMat = LitMaterial("Hero_Player", new Color(0.2f, 0.55f, 1f));
            Material botMat = LitMaterial("Hero_Bot", new Color(0.9f, 0.15f, 0.2f));
            Material noseMat = LitMaterial("Hero_Nose", new Color(1f, 0.85f, 0.2f));
            Material targetMat = LitMaterial("Target", new Color(1f, 0.5f, 0.15f));
            Material reticleMat = UnlitMaterial("Reticle", new Color(1f, 1f, 0.3f));
            Material barMat = UnlitMaterial("HealthBar", new Color(0.3f, 1f, 0.4f));
            Material chargeMat = UnlitMaterial("ChargeOrb", new Color(0.55f, 0.95f, 1f));
            Material beamMat = BeamMaterial("Beam");
            Material buttonMat = UnlitMaterial("MenuButton", new Color(0.15f, 0.2f, 0.3f));
            Material tutorialTargetMat = LitMaterial("TutorialTarget", new Color(0.3f, 1f, 0.45f));
            Material ghostMat = UnlitMaterial("GhostHand", new Color(0.85f, 0.95f, 1f));

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Seat: fixed camera / XR Origin, never moves (ADR 4/5).
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 200f;
            camGo.AddComponent<AudioListener>();
            Transform trackingSpace = null;
            if (xr) trackingSpace = XRRig(cam);
            else camGo.transform.position = new Vector3(0f, SeatEyeHeight, 0f);

            var lightGo = new GameObject("Directional Light");
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;

            // Arena greybox.
            var arena = new GameObject("Arena");
            arena.transform.position = ArenaCenter;

            Cube("Floor", arena.transform, new Vector3(0f, -ArenaSize.y * 0.5f - 0.1f, 0f),
                new Vector3(ArenaSize.x, 0.2f, ArenaSize.z), floorMat);
            Cube("BackWall", arena.transform, new Vector3(0f, 0f, ArenaSize.z * 0.5f + 0.1f),
                new Vector3(ArenaSize.x, ArenaSize.y, 0.2f), wallMat);
            Cube("Pillar_L", arena.transform, new Vector3(-7f, -5f, 4f), new Vector3(2f, 10f, 2f), wallMat);
            Cube("Pillar_R", arena.transform, new Vector3(9f, -4f, 9f), new Vector3(2f, 12f, 2f), wallMat);

            Vector3[] targetSpots =
            {
                new Vector3(-8f, 0f, 6f),
                new Vector3(8f, 3f, 0f),
                new Vector3(0f, -4f, 11f),
                new Vector3(-4f, 6f, -5f),
            };
            for (int i = 0; i < targetSpots.Length; i++)
            {
                GameObject target = Cube($"Target_{i + 1}", arena.transform, targetSpots[i], Vector3.one * 2f, targetMat);
                target.AddComponent<PrototypeTarget>();
            }

            var flying = Hero("PlayerHero", arena.transform, Vector3.zero, heroMat, noseMat, barMat);

            // Aim feedback: reticle without collider (else the aim ray hits it and jitters).
            GameObject reticle = Primitive(PrimitiveType.Sphere, "Reticle", null, ArenaCenter, Vector3.one * 0.4f, reticleMat);

            var beamGo = new GameObject("BeamRenderer");
            var beam = beamGo.AddComponent<LineRenderer>();
            beam.sharedMaterial = beamMat;
            beam.enabled = false;

            // Input: XR hands drive the controllers in Arena_Main; the sandbox uses
            // debug keyboard/mouse (XR hand input is present there too).
            var inputGo = new GameObject("HandInput");
            var tracker = inputGo.AddComponent<HandGestureTracker>();
            SetRefs(tracker, ("headCamera", camGo.transform), ("xrOrigin", trackingSpace));
            var xrInput = inputGo.AddComponent<XRHandsInputSource>();
            SetRefs(xrInput, ("tracker", tracker));
            HandInputSourceBehaviour playerInput = xrInput;
            if (!xr)
            {
                var debugInput = inputGo.AddComponent<DebugKeyboardMouseInputSource>();
                SetRefs(debugInput, ("viewCamera", cam));
                playerInput = debugInput;
            }

            var controllers = new GameObject("Controllers");
            var puppeteer = controllers.AddComponent<HandPuppeteerController>();
            SetRefs(puppeteer, ("character", flying), ("inputSource", playerInput));
            // Charge orb lives outside the hero so HeroHealth's flash/visibility leave it alone.
            GameObject chargeOrb = Primitive(PrimitiveType.Sphere, "ChargeOrb", null, ArenaCenter, Vector3.one, chargeMat);
            var pointing = controllers.AddComponent<PointingBeamController>();
            SetRefs(pointing, ("character", flying), ("reticle", reticle.transform), ("beam", beam),
                ("inputSource", playerInput), ("chargeIndicator", chargeOrb.transform));

            // Palm push shockwave (T5): stuns the bot when it is within reach.
            var ringGo = new GameObject("ShockwaveRingRenderer");
            var ring = ringGo.AddComponent<LineRenderer>();
            ring.sharedMaterial = beamMat;
            ring.enabled = false;
            var shockwave = controllers.AddComponent<ShockwaveController>();
            SetRefs(shockwave, ("character", flying), ("inputSource", playerInput), ("ring", ring));

            // Bot opponent: same hero, same controllers, input from BotInputSource.
            BotDifficulty difficulty = GetOrCreateBotDifficulty();
            var botFlying = Hero("BotHero", arena.transform, new Vector3(6f, 3f, 12f), botMat, noseMat, barMat);

            var botBeamGo = new GameObject("BotBeamRenderer");
            var botBeam = botBeamGo.AddComponent<LineRenderer>();
            botBeam.sharedMaterial = beamMat;
            botBeam.enabled = false;

            var bot = new GameObject("Bot");
            var botInput = bot.AddComponent<BotInputSource>();
            var botPuppeteer = bot.AddComponent<HandPuppeteerController>();
            SetRefs(botPuppeteer, ("character", botFlying), ("inputSource", botInput));
            var botPointing = bot.AddComponent<PointingBeamController>();
            SetRefs(botPointing, ("character", botFlying), ("beam", botBeam), ("inputSource", botInput));
            var beamSo = new SerializedObject(botPointing);
            beamSo.FindProperty("beamColor").colorValue = new Color(1f, 0.25f, 0.2f);
            beamSo.ApplyModifiedPropertiesWithoutUndo();
            SetRefs(botInput, ("self", botFlying), ("puppeteer", botPuppeteer), ("enemy", flying.transform),
                ("hitReceiver", botFlying.GetComponent<BeamHitReceiver>()), ("difficulty", difficulty));

            // Enemy beam warning line (aim locked -> thickens, yellow -> red -> fire).
            var telegraphGo = new GameObject("BotTelegraphRenderer");
            var telegraph = telegraphGo.AddComponent<LineRenderer>();
            telegraph.sharedMaterial = beamMat;
            telegraph.enabled = false;
            var telegraphLine = bot.AddComponent<BotTelegraphLine>();
            SetRefs(telegraphLine, ("bot", botInput), ("botHero", botFlying), ("line", telegraph));

            // Match loop (T6): heroes act only during the fight; HUD in front of the seat.
            var match = new GameObject("Match");
            var director = match.AddComponent<MatchDirector>();
            SetRefs(director, ("playerHealth", flying.GetComponent<HeroHealth>()),
                ("opponentHealth", botFlying.GetComponent<HeroHealth>()));
            if (xr) SetArray(director, "fightOnly", xrInput);
            else SetArray(director, "fightOnly", playerInput, xrInput);
            SetArray(director, "opponentOnly", botInput);

            Vector3 seat = camGo.transform.position;
            TextMeshPro scoreLine = WorldText("HUD_Score", match.transform, seat + new Vector3(0f, 0.9f, 4f), 1.5f);
            TextMeshPro banner = WorldText("HUD_Banner", match.transform, seat + new Vector3(0f, 0.15f, 4f), 3.5f);
            var hud = match.AddComponent<MatchHud>();
            SetRefs(hud, ("director", director), ("scoreLine", scoreLine), ("banner", banner));

            // Hands-only menus (T7): point with the right hand + pinch; mouse stands in without a headset.
            var pointerRayGo = new GameObject("MenuPointerRay");
            var pointerRay = pointerRayGo.AddComponent<LineRenderer>();
            pointerRay.sharedMaterial = beamMat;
            pointerRay.widthMultiplier = 0.01f;
            pointerRay.startColor = new Color(1f, 1f, 1f, 0.15f);
            pointerRay.endColor = new Color(1f, 1f, 1f, 0.8f);
            pointerRay.enabled = false;

            var menuGo = new GameObject("HandMenu");
            menuGo.transform.SetParent(match.transform, false);
            var pointer = menuGo.AddComponent<HandMenuPointer>();
            SetRefs(pointer, ("tracker", tracker), ("viewCamera", cam), ("ray", pointerRay));

            // Panels sit 2.5 m ahead, about 11 degrees below eye level, under the banner.
            Vector3 panelPos = seat + new Vector3(0f, -0.5f, 2.5f);
            GameObject mainPanel = Panel("MainPanel", menuGo.transform, panelPos);
            MenuButton(mainPanel.transform, "START", MatchDirector.MenuAction.StartMatch, -0.5f, director, buttonMat);
            MenuButton(mainPanel.transform, "TUTORIAL", MatchDirector.MenuAction.StartWithTutorial, 0.5f, director,
                buttonMat);
            GameObject pausePanel = Panel("PausePanel", menuGo.transform, panelPos);
            MenuButton(pausePanel.transform, "RESUME", MatchDirector.MenuAction.Resume, -0.5f, director, buttonMat);
            MenuButton(pausePanel.transform, "MENU", MatchDirector.MenuAction.ReturnToMenu, 0.5f, director, buttonMat);
            GameObject tutorialPausePanel = Panel("TutorialPausePanel", menuGo.transform, panelPos);
            MenuButton(tutorialPausePanel.transform, "RESUME", MatchDirector.MenuAction.Resume, -0.95f, director,
                buttonMat);
            MenuButton(tutorialPausePanel.transform, "SKIP", MatchDirector.MenuAction.SkipTutorial, 0f, director,
                buttonMat);
            MenuButton(tutorialPausePanel.transform, "MENU", MatchDirector.MenuAction.ReturnToMenu, 0.95f, director,
                buttonMat);
            GameObject endPanel = Panel("MatchEndPanel", menuGo.transform, panelPos);
            MenuButton(endPanel.transform, "MENU", MatchDirector.MenuAction.ReturnToMenu, 0f, director, buttonMat);

            var handMenu = menuGo.AddComponent<HandMenu>();
            SetRefs(handMenu, ("director", director), ("pointer", pointer), ("mainPanel", mainPanel),
                ("pausePanel", pausePanel), ("tutorialPausePanel", tutorialPausePanel), ("matchEndPanel", endPanel));

            // Wrist pause button: left palm toward the face, pinch that hand.
            var wristButton = new GameObject("WristButton");
            wristButton.transform.SetParent(match.transform, false);
            Primitive(PrimitiveType.Cube, "Background", wristButton.transform, Vector3.zero,
                new Vector3(0.12f, 0.05f, 0.005f), buttonMat);
            TextMeshPro wristLabel = WorldText("Label", wristButton.transform, Vector3.zero, 0.25f);
            wristLabel.rectTransform.sizeDelta = new Vector2(0.12f, 0.05f);
            wristLabel.transform.localPosition = new Vector3(0f, 0f, -0.004f);
            wristButton.SetActive(false);
            var wrist = match.AddComponent<WristMenu>();
            SetRefs(wrist, ("director", director), ("tracker", tracker), ("head", camGo.transform),
                ("button", wristButton.transform), ("label", wristLabel));

            // 30-second tutorial (T8): ring, practice target, practice telegraph beam, prompt, ghost hand.
            var tutorialGo = new GameObject("Tutorial");
            tutorialGo.transform.SetParent(match.transform, false);
            var tutorialRoot = new GameObject("TutorialObjects");
            tutorialRoot.transform.SetParent(tutorialGo.transform, false);

            const float ringRadius = 2f;
            var ringGoal = new GameObject("GoalRing");
            ringGoal.transform.SetParent(tutorialRoot.transform, false);
            ringGoal.transform.position = ArenaCenter + new Vector3(6f, 2f, -5f);
            LineRenderer ringLine = ringGoal.AddComponent<LineRenderer>();
            ringLine.sharedMaterial = beamMat;
            ringLine.useWorldSpace = false;
            ringLine.loop = true;
            ringLine.widthMultiplier = 0.08f;
            ringLine.startColor = ringLine.endColor = new Color(0.4f, 1f, 0.5f, 0.9f);
            const int ringSegments = 48;
            ringLine.positionCount = ringSegments;
            for (int i = 0; i < ringSegments; i++)
            {
                float a = i * Mathf.PI * 2f / ringSegments;
                ringLine.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * ringRadius);
            }

            GameObject practiceTarget = Primitive(PrimitiveType.Sphere, "PracticeTarget", tutorialRoot.transform,
                Vector3.zero, Vector3.one * 2f, tutorialTargetMat, keepCollider: true);
            practiceTarget.transform.position = ArenaCenter + new Vector3(-6f, 1f, 2f);
            var practiceReceiver = practiceTarget.AddComponent<BeamHitReceiver>();

            var telegraphOrigin = new GameObject("PracticeBeamOrigin");
            telegraphOrigin.transform.SetParent(tutorialRoot.transform, false);
            telegraphOrigin.transform.position = ArenaCenter + new Vector3(0f, 6f, 14f);
            var practiceBeam = telegraphOrigin.AddComponent<LineRenderer>();
            practiceBeam.sharedMaterial = beamMat;
            practiceBeam.enabled = false;

            TextMeshPro tutorialPrompt = WorldText("Prompt", tutorialRoot.transform, seat + new Vector3(0f, 0.6f, 4f), 2.5f);
            GameObject ghost = Primitive(PrimitiveType.Sphere, "GhostHand", tutorialRoot.transform,
                seat + new Vector3(-0.2f, -0.35f, 0.4f), Vector3.one * 0.05f, ghostMat);
            tutorialRoot.SetActive(false);

            var tutorial = tutorialGo.AddComponent<TutorialDirector>();
            SetRefs(tutorial, ("director", director), ("playerHero", flying), ("playerInput", playerInput),
                ("head", camGo.transform), ("tutorialRoot", tutorialRoot), ("ring", ringGoal.transform),
                ("target", practiceReceiver), ("telegraphOrigin", telegraphOrigin.transform),
                ("telegraph", practiceBeam), ("prompt", tutorialPrompt), ("ghostHand", ghost.transform));
            var tutorialSo = new SerializedObject(tutorial);
            tutorialSo.FindProperty("ringRadius").floatValue = ringRadius;
            tutorialSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[HandHeroSceneBuilder] Built {scenePath}");
        }

        // XR Origin > Camera Offset > Main Camera. The origin sits at the world
        // origin and nothing ever moves or rotates it (ADR 4/5). Device tracking
        // origin + a fixed eye height keeps the seated layout identical for every
        // player. Returns the tracking space (Camera Offset): XR Hands reports
        // joints relative to it.
        private static Transform XRRig(Camera cam)
        {
            var originGo = new GameObject("XR Origin");
            var offsetGo = new GameObject("Camera Offset");
            offsetGo.transform.SetParent(originGo.transform, false);
            offsetGo.transform.localPosition = new Vector3(0f, SeatEyeHeight, 0f);
            cam.transform.SetParent(offsetGo.transform, false);
            cam.transform.localPosition = Vector3.zero;

            var origin = originGo.AddComponent<XROrigin>();
            origin.Camera = cam;
            origin.CameraFloorOffsetObject = offsetGo;
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Device;
            origin.CameraYOffset = SeatEyeHeight;

            var pose = cam.gameObject.AddComponent<TrackedPoseDriver>();
            pose.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            pose.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            pose.positionInput = new InputActionProperty(new InputAction("Head Position",
                InputActionType.Value, "<XRHMD>/centerEyePosition", expectedControlType: "Vector3"));
            pose.rotationInput = new InputActionProperty(new InputAction("Head Rotation",
                InputActionType.Value, "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion"));
            pose.trackingStateInput = new InputActionProperty(new InputAction("Head Tracking State",
                InputActionType.Value, "<XRHMD>/trackingState", expectedControlType: "Integer"));
            return offsetGo.transform;
        }

        // Greybox hero: collider + BeamHitReceiver + HeroHealth on the root (beams
        // hit it), visual pivot rotated by FlyingCharacter for facing/banking only.
        private static FlyingCharacter Hero(string name, Transform arena, Vector3 localPos, Material bodyMat,
            Material noseMat, Material barMat)
        {
            var hero = new GameObject(name);
            hero.transform.SetParent(arena, false);
            hero.transform.localPosition = localPos;
            hero.AddComponent<SphereCollider>().radius = 0.9f;
            hero.AddComponent<BeamHitReceiver>();

            var visual = new GameObject("Visual");
            visual.transform.SetParent(hero.transform, false);
            GameObject body = Primitive(PrimitiveType.Capsule, "Body", visual.transform,
                Vector3.zero, new Vector3(0.8f, 0.9f, 0.8f), bodyMat);
            body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Primitive(PrimitiveType.Sphere, "Nose", visual.transform,
                new Vector3(0f, 0f, 0.9f), Vector3.one * 0.35f, noseMat);

            var flying = hero.AddComponent<FlyingCharacter>();
            SetRefs(flying, ("arenaCenter", arena), ("visual", visual.transform));

            // Root doesn't rotate (only Visual does), so the bar stays level.
            GameObject bar = Primitive(PrimitiveType.Cube, "HealthBar", hero.transform,
                new Vector3(0f, 1.4f, 0f), new Vector3(1.6f, 0.12f, 0.12f), barMat);
            var health = hero.AddComponent<HeroHealth>();
            SetRefs(health, ("character", flying), ("hitReceiver", hero.GetComponent<BeamHitReceiver>()),
                ("healthBarFill", bar.transform));
            return flying;
        }

        private static GameObject Panel(string name, Transform parent, Vector3 worldPos)
        {
            var panel = new GameObject(name);
            panel.transform.SetParent(parent, false);
            panel.transform.position = worldPos;
            panel.SetActive(false);
            return panel;
        }

        // Big pinch target (0.8 x 0.32 m at 2.5 m, about 18 x 7 degrees). The trigger
        // collider is on the root so the hover scale applies to the label too.
        private static HandMenuButton MenuButton(Transform panel, string text, MatchDirector.MenuAction action,
            float x, MatchDirector director, Material mat)
        {
            var size = new Vector3(0.8f, 0.32f, 0.04f);
            var go = new GameObject($"Button_{text}");
            go.transform.SetParent(panel, false);
            go.transform.localPosition = new Vector3(x, 0f, 0f);
            var box = go.AddComponent<BoxCollider>();
            box.size = size;
            box.isTrigger = true;

            GameObject bg = Primitive(PrimitiveType.Cube, "Background", go.transform, Vector3.zero, size, mat);
            TextMeshPro label = WorldText("Label", go.transform, Vector3.zero, 1.6f);
            label.rectTransform.sizeDelta = new Vector2(size.x, size.y);
            label.transform.localPosition = new Vector3(0f, 0f, -size.z * 0.5f - 0.005f);
            label.text = text;

            var button = go.AddComponent<HandMenuButton>();
            SetRefs(button, ("director", director), ("background", bg.GetComponent<Renderer>()), ("label", label));
            var so = new SerializedObject(button);
            so.FindProperty("action").enumValueIndex = (int)action;
            so.ApplyModifiedPropertiesWithoutUndo();
            return button;
        }

        // Kept if it already exists so tuning done in the editor survives rebuilds.
        private static BotDifficulty GetOrCreateBotDifficulty()
        {
            EnsureFolder(BotDir);
            var asset = AssetDatabase.LoadAssetAtPath<BotDifficulty>(BotDifficultyPath);
            if (asset != null) return asset;

            asset = ScriptableObject.CreateInstance<BotDifficulty>();
            AssetDatabase.CreateAsset(asset, BotDifficultyPath);
            return asset;
        }

        private static GameObject Cube(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            return Primitive(PrimitiveType.Cube, name, parent, localPos, scale, mat, keepCollider: true);
        }

        private static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 localPos,
            Vector3 scale, Material mat, bool keepCollider = false)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        // TextMeshPro (3D): font size 10 is about 1 m tall. Faces the seat (looks down +Z).
        private static TextMeshPro WorldText(string name, Transform parent, Vector3 worldPos, float fontSize)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;
            var text = go.AddComponent<TextMeshPro>();
            text.rectTransform.sizeDelta = new Vector2(8f, 2f);
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.text = "";
            return text;
        }

        private static void SetArray(Object target, string field, params Object[] values)
        {
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(field);
            if (prop == null || !prop.isArray)
            {
                Debug.LogError($"[HandHeroSceneBuilder] {target.GetType().Name} has no array field '{field}'");
                return;
            }
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetRefs(Object target, params (string field, Object value)[] refs)
        {
            var so = new SerializedObject(target);
            foreach (var (field, value) in refs)
            {
                SerializedProperty prop = so.FindProperty(field);
                if (prop == null)
                {
                    Debug.LogError($"[HandHeroSceneBuilder] {target.GetType().Name} has no field '{field}'");
                    continue;
                }
                prop.objectReferenceValue = value;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Material LitMaterial(string name, Color color)
        {
            return GetOrCreateMaterial(name, "Universal Render Pipeline/Lit", color);
        }

        private static Material UnlitMaterial(string name, Color color)
        {
            return GetOrCreateMaterial(name, "Universal Render Pipeline/Unlit", color);
        }

        // Line renderers tint through vertex colors, so use a particle shader.
        private static Material BeamMaterial(string name)
        {
            return GetOrCreateMaterial(name, "Universal Render Pipeline/Particles/Unlit", Color.white);
        }

        private static Material GetOrCreateMaterial(string name, string shaderName, Color color)
        {
            string path = $"{MaterialDir}/{name}.mat";
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogWarning($"[HandHeroSceneBuilder] Shader '{shaderName}' not found, using Sprites/Default");
                shader = Shader.Find("Sprites/Default");
            }

            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
            }

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            mat.color = color;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
