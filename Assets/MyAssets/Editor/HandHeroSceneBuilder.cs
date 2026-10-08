using System.IO;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;

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
        private const string FxDir = "Assets/MyAssets/Generated/FX";
        public const string ImpactPrefabPath = FxDir + "/BeamImpact.prefab";

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
            Material heroMarkerMat = UnlitMaterial("GroundMarker_Player", new Color(0.15f, 0.3f, 0.55f));
            Material botMarkerMat = UnlitMaterial("GroundMarker_Bot", new Color(0.5f, 0.12f, 0.15f));
            Material impactMat = UnlitMaterial("BeamImpact", new Color(1f, 0.95f, 0.7f));
            Material cursorMat = UnlitMaterial("AimCursor", new Color(1f, 0.6f, 0.15f));
            Material cursorMarkerMat = UnlitMaterial("GroundMarker_Cursor", new Color(0.55f, 0.3f, 0.08f));
            GameObject impactPrefab = GetOrCreateImpactPrefab(impactMat);

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

            var flying = Hero("PlayerHero", arena.transform, Vector3.zero, heroMat, noseMat, barMat, isBot: false);
            GroundMarker(flying, heroMarkerMat, beamMat, new Color(0.4f, 0.7f, 1f, 0.5f));

            // Aim feedback: reticle without collider (else the aim ray hits it and jitters).
            GameObject reticle = Primitive(PrimitiveType.Sphere, "Reticle", null, ArenaCenter, Vector3.one * 0.4f, reticleMat);

            // CURSOR aim: orange marker (no collider) + its own floor disc for depth.
            GameObject cursorMarker = Primitive(PrimitiveType.Sphere, "AimCursor", null, ArenaCenter,
                Vector3.one * 0.6f, cursorMat);
            GameObject cursorGround = GroundMarker(flying, cursorMarkerMat, beamMat, new Color(1f, 0.6f, 0.15f, 0.5f),
                cursorMarker.transform);

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
                ("inputSource", playerInput), ("chargeIndicator", chargeOrb.transform),
                ("hitEffectPrefab", impactPrefab), ("audioSource", flying.GetComponent<AudioSource>()),
                ("reticleRenderer", reticle.GetComponent<Renderer>()), ("health", flying.GetComponent<HeroHealth>()),
                ("cursorMarker", cursorMarker.transform), ("cursorRenderer", cursorMarker.GetComponent<Renderer>()));
            SetArray(pointing, "assistOnlyVisuals", reticle);
            SetArray(pointing, "cursorOnlyVisuals", cursorMarker, cursorGround);

            // Palm push shockwave (T5): stuns the bot when it is within reach.
            var ringGo = new GameObject("ShockwaveRingRenderer");
            var ring = ringGo.AddComponent<LineRenderer>();
            ring.sharedMaterial = beamMat;
            ring.enabled = false;
            var shockwave = controllers.AddComponent<ShockwaveController>();
            SetRefs(shockwave, ("character", flying), ("inputSource", playerInput), ("ring", ring));

            // Bot opponent: same hero, same controllers, input from BotInputSource.
            BotDifficulty difficulty = GetOrCreateBotDifficulty();
            var botFlying = Hero("BotHero", arena.transform, new Vector3(6f, 3f, 12f), botMat, noseMat, barMat,
                isBot: true);
            GroundMarker(botFlying, botMarkerMat, beamMat, new Color(1f, 0.4f, 0.4f, 0.5f));

            var botBeamGo = new GameObject("BotBeamRenderer");
            var botBeam = botBeamGo.AddComponent<LineRenderer>();
            botBeam.sharedMaterial = beamMat;
            botBeam.enabled = false;

            var bot = new GameObject("Bot");
            var botInput = bot.AddComponent<BotInputSource>();
            var botPuppeteer = bot.AddComponent<HandPuppeteerController>();
            SetRefs(botPuppeteer, ("character", botFlying), ("inputSource", botInput));
            var botPointing = bot.AddComponent<PointingBeamController>();
            SetRefs(botPointing, ("character", botFlying), ("beam", botBeam), ("inputSource", botInput),
                ("hitEffectPrefab", impactPrefab), ("audioSource", botFlying.GetComponent<AudioSource>()));
            var beamSo = new SerializedObject(botPointing);
            beamSo.FindProperty("beamColor").colorValue = new Color(1f, 0.25f, 0.2f);
            // No assist for the bot: a snap at fire time would retarget the player's
            // current position and void the telegraph dodge (spec D10).
            beamSo.FindProperty("assistAngle").floatValue = 0f;
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

            // Seat-space UI (HUD, menus, wrist button, tutorial prompt) lives under the
            // Camera Offset, so the T10 tabletop scale keeps it at the same apparent
            // size and spot. World-fixed, never head-locked.
            Vector3 seat = camGo.transform.position;
            Transform seatSpace = trackingSpace;
            if (seatSpace == null)
            {
                seatSpace = new GameObject("SeatSpace").transform;
                seatSpace.position = seat;
            }
            Transform seatUI = new GameObject("SeatUI").transform;
            seatUI.SetParent(seatSpace, false);

            TextMeshPro scoreLine = WorldText("HUD_Score", seatUI, seat + new Vector3(0f, 0.9f, 4f), 1.5f);
            TextMeshPro banner = WorldText("HUD_Banner", seatUI, seat + new Vector3(0f, 0.15f, 4f), 3.5f);
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
            menuGo.transform.SetParent(seatUI, false);
            var pointer = menuGo.AddComponent<HandMenuPointer>();
            SetRefs(pointer, ("tracker", tracker), ("viewCamera", cam), ("ray", pointerRay));

            // Panels sit 2.5 m ahead, about 11 degrees below eye level, under the banner.
            Vector3 panelPos = seat + new Vector3(0f, -0.5f, 2.5f);
            GameObject mainPanel = Panel("MainPanel", menuGo.transform, panelPos);
            // Main menu is a 2x2 grid (0.15 m gaps): START / TUTORIAL on top, then the
            // passthrough view (Arena_Main only, T10) and the aim mode.
            const float gridX = 0.475f;
            const float gridY = 0.235f;
            MenuButton(mainPanel.transform, "START", MatchDirector.MenuAction.StartMatch, -gridX, gridY,
                director, buttonMat);
            MenuButton(mainPanel.transform, "TUTORIAL", MatchDirector.MenuAction.StartWithTutorial, gridX, gridY,
                director, buttonMat);
            HandMenuButton viewButton = xr
                ? MenuButton(mainPanel.transform, "MR TABLE", MatchDirector.MenuAction.ToggleViewMode, -gridX, -gridY,
                    director, buttonMat)
                : null;
            HandMenuButton aimButton = MenuButton(mainPanel.transform, "AIM: ASSIST",
                MatchDirector.MenuAction.ToggleAimMode, xr ? gridX : -gridX, -gridY, director, buttonMat);
            GameObject pausePanel = Panel("PausePanel", menuGo.transform, panelPos);
            MenuButton(pausePanel.transform, "RESUME", MatchDirector.MenuAction.Resume, -0.5f, 0f, director, buttonMat);
            MenuButton(pausePanel.transform, "MENU", MatchDirector.MenuAction.ReturnToMenu, 0.5f, 0f, director,
                buttonMat);
            GameObject tutorialPausePanel = Panel("TutorialPausePanel", menuGo.transform, panelPos);
            MenuButton(tutorialPausePanel.transform, "RESUME", MatchDirector.MenuAction.Resume, -0.95f, 0f, director,
                buttonMat);
            MenuButton(tutorialPausePanel.transform, "SKIP", MatchDirector.MenuAction.SkipTutorial, 0f, 0f, director,
                buttonMat);
            MenuButton(tutorialPausePanel.transform, "MENU", MatchDirector.MenuAction.ReturnToMenu, 0.95f, 0f, director,
                buttonMat);
            GameObject endPanel = Panel("MatchEndPanel", menuGo.transform, panelPos);
            MenuButton(endPanel.transform, "MENU", MatchDirector.MenuAction.ReturnToMenu, 0f, 0f, director, buttonMat);

            // Aim mode (ASSIST / CURSOR): the player's controller, tutorial and AIM button share it.
            var aimMode = match.AddComponent<AimModeSetting>();
            SetRefs(aimMode, ("toggleLabel", aimButton.GetComponentInChildren<TextMeshPro>()));
            SetRefs(director, ("aimMode", aimMode));
            SetRefs(pointing, ("aimModeSetting", aimMode));

            var handMenu = menuGo.AddComponent<HandMenu>();
            SetRefs(handMenu, ("director", director), ("pointer", pointer), ("mainPanel", mainPanel),
                ("pausePanel", pausePanel), ("tutorialPausePanel", tutorialPausePanel), ("matchEndPanel", endPanel));

            // Wrist pause button: left palm toward the face, pinch that hand.
            var wristButton = new GameObject("WristButton");
            wristButton.transform.SetParent(seatUI, false);
            Primitive(PrimitiveType.Cube, "Background", wristButton.transform, Vector3.zero,
                new Vector3(0.12f, 0.05f, 0.005f), buttonMat);
            TextMeshPro wristLabel = WorldText("Label", wristButton.transform, Vector3.zero, 0.25f);
            wristLabel.rectTransform.sizeDelta = new Vector2(0.12f, 0.05f);
            PlaceLocal(wristLabel, new Vector3(0f, 0f, -0.004f));
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
            practiceTarget.AddComponent<AimAssistTarget>(); // registers only while the tutorial shows it

            var telegraphOrigin = new GameObject("PracticeBeamOrigin");
            telegraphOrigin.transform.SetParent(tutorialRoot.transform, false);
            telegraphOrigin.transform.position = ArenaCenter + new Vector3(0f, 6f, 14f);
            var practiceBeam = telegraphOrigin.AddComponent<LineRenderer>();
            practiceBeam.sharedMaterial = beamMat;
            practiceBeam.enabled = false;

            TextMeshPro tutorialPrompt = WorldText("TutorialPrompt", seatUI, seat + new Vector3(0f, 0.6f, 4f), 2.5f);
            tutorialPrompt.gameObject.SetActive(false);
            GameObject ghost = Primitive(PrimitiveType.Sphere, "GhostHand", tutorialRoot.transform,
                seat + new Vector3(-0.2f, -0.35f, 0.4f), Vector3.one * 0.05f, ghostMat);
            tutorialRoot.SetActive(false);

            var tutorial = tutorialGo.AddComponent<TutorialDirector>();
            SetRefs(tutorial, ("director", director), ("playerHero", flying), ("playerInput", playerInput),
                ("head", camGo.transform), ("tutorialRoot", tutorialRoot), ("ring", ringGoal.transform),
                ("target", practiceReceiver), ("telegraphOrigin", telegraphOrigin.transform),
                ("telegraph", practiceBeam), ("prompt", tutorialPrompt), ("ghostHand", ghost.transform),
                ("playerAim", pointing), ("aimModeSetting", aimMode));
            var tutorialSo = new SerializedObject(tutorial);
            tutorialSo.FindProperty("ringRadius").floatValue = ringRadius;
            tutorialSo.ApplyModifiedPropertiesWithoutUndo();

            if (xr) ViewModeSwitch(cam, director, arena.transform, viewButton);

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

        // T10: VR arena <-> passthrough tabletop (main menu button, VR arena by default).
        // AR Session + AR Camera Manager (Meta OpenXR passthrough) stay disabled
        // until the tabletop mode turns them on.
        private static void ViewModeSwitch(Camera cam, MatchDirector director, Transform arena,
            HandMenuButton viewButton)
        {
            var sessionGo = new GameObject("AR Session");
            var session = sessionGo.AddComponent<ARSession>();
            session.enabled = false;
            var cameraManager = cam.gameObject.AddComponent<ARCameraManager>();
            cameraManager.enabled = false;

            var viewMode = director.gameObject.AddComponent<ArenaViewMode>();
            SetRefs(viewMode, ("origin", cam.GetComponentInParent<XROrigin>()), ("arenaCenter", arena),
                ("viewCamera", cam), ("toggleLabel", viewButton != null ? viewButton.GetComponentInChildren<TextMeshPro>() : null));
            SetArray(viewMode, "passthroughOnly", session, cameraManager);
            var so = new SerializedObject(viewMode);
            so.FindProperty("eyeHeight").floatValue = SeatEyeHeight;
            so.FindProperty("arenaWidth").floatValue = ArenaSize.x;
            so.ApplyModifiedPropertiesWithoutUndo();
            SetRefs(director, ("viewMode", viewMode));
        }

        // Greybox hero: collider + BeamHitReceiver + HeroHealth on the root (beams
        // hit it), visual pivot rotated by FlyingCharacter for facing/banking only.
        // The two heroes differ in shape as well as color (T12), so they stay apart
        // for color-blind players and as small far-away silhouettes: the player has
        // flat wings, the bot a raised V tail.
        private static FlyingCharacter Hero(string name, Transform arena, Vector3 localPos, Material bodyMat,
            Material noseMat, Material barMat, bool isBot)
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
            if (isBot)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    GameObject fin = Primitive(PrimitiveType.Cube, side < 0 ? "Fin_L" : "Fin_R", visual.transform,
                        new Vector3(side * 0.3f, 0.45f, -0.45f), new Vector3(0.08f, 0.8f, 0.5f), bodyMat);
                    fin.transform.localRotation = Quaternion.Euler(0f, 0f, -side * 30f);
                }
            }
            else
            {
                Primitive(PrimitiveType.Cube, "Wings", visual.transform,
                    new Vector3(0f, 0f, -0.1f), new Vector3(2.2f, 0.08f, 0.5f), bodyMat);
            }

            // Audio hook (T12): beam fire sounds play here once a clip is assigned.
            var audio = hero.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 1f;

            var flying = hero.AddComponent<FlyingCharacter>();
            SetRefs(flying, ("arenaCenter", arena), ("visual", visual.transform));

            // Root doesn't rotate (only Visual does), so the bar stays level.
            GameObject bar = Primitive(PrimitiveType.Cube, "HealthBar", hero.transform,
                new Vector3(0f, 1.4f, 0f), new Vector3(1.6f, 0.12f, 0.12f), barMat);
            var health = hero.AddComponent<HeroHealth>();
            SetRefs(health, ("character", flying), ("hitReceiver", hero.GetComponent<BeamHitReceiver>()),
                ("healthBarFill", bar.transform));
            // The aim assist may snap to this hero (the other side's controller).
            var assistTarget = hero.AddComponent<AimAssistTarget>();
            SetRefs(assistTarget, ("character", flying));
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
            float x, float y, MatchDirector director, Material mat)
        {
            var size = new Vector3(0.8f, 0.32f, 0.04f);
            var go = new GameObject($"Button_{text}");
            go.transform.SetParent(panel, false);
            go.transform.localPosition = new Vector3(x, y, 0f);
            var box = go.AddComponent<BoxCollider>();
            box.size = size;
            box.isTrigger = true;

            GameObject bg = Primitive(PrimitiveType.Cube, "Background", go.transform, Vector3.zero, size, mat);
            TextMeshPro label = WorldText("Label", go.transform, Vector3.zero, 1.6f);
            label.rectTransform.sizeDelta = new Vector2(size.x, size.y);
            PlaceLocal(label, new Vector3(0f, 0f, -size.z * 0.5f - 0.005f));
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

        // Floor disc + drop line under a hero (depth cue, T12). Lives outside the
        // hero so HeroHealth's hit flash and death hiding leave it alone.
        // With `follow`, the marker sits under that transform (the CURSOR aim marker)
        // and the hero only gives the arena bounds and visibility.
        private static GameObject GroundMarker(FlyingCharacter hero, Material discMat, Material lineMat, Color lineColor,
            Transform follow = null)
        {
            var go = new GameObject((follow != null ? follow.name : hero.name) + "_GroundMarker");
            GameObject disc = Primitive(PrimitiveType.Cylinder, "Disc", go.transform, Vector3.zero,
                new Vector3(2f, 0.01f, 2f), discMat);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = lineMat;
            line.useWorldSpace = true;
            line.widthMultiplier = 0.03f;
            line.startColor = line.endColor = lineColor;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var marker = go.AddComponent<HeroGroundMarker>();
            SetRefs(marker, ("character", hero), ("disc", disc.transform), ("dropLine", line), ("follow", follow));
            return go;
        }

        // Beam hit effect prefab (T12): a sphere that pops and shrinks. Rewritten
        // on every build so the generated asset always matches this code.
        private static GameObject GetOrCreateImpactPrefab(Material mat)
        {
            EnsureFolder(FxDir);
            GameObject temp = Primitive(PrimitiveType.Sphere, "BeamImpact", null, Vector3.zero, Vector3.one, mat);
            temp.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            temp.AddComponent<ImpactFlash>();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temp, ImpactPrefabPath);
            Object.DestroyImmediate(temp);
            return prefab;
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

        // A RectTransform under a plain Transform serializes anchoredPosition separately
        // and applies it on load; setting only localPosition left the menu labels at
        // their creation point (world origin, on the floor ahead), all stacked together.
        private static void PlaceLocal(TextMeshPro text, Vector3 localPosition)
        {
            RectTransform rt = text.rectTransform;
            rt.anchoredPosition = new Vector2(localPosition.x, localPosition.y);
            rt.localPosition = localPosition;
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
