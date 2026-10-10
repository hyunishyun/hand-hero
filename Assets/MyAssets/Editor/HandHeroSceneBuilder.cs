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
        // Built-in UI layer: menu button colliders, the only layer the menu ray tests.
        private const int MenuButtonLayer = 5;
        public const string BotDifficultyPath = BotDir + "/BotDifficulty_Normal.asset";
        public const string RunBotPrefabPath = BotDir + "/RunBot.prefab";
        private const string FxDir = "Assets/MyAssets/Generated/FX";
        public const string ImpactPrefabPath = FxDir + "/BeamImpact.prefab";
        public const string KillBurstPrefabPath = FxDir + "/KillBurst.prefab";

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
            Material killBurstMat = UnlitMaterial("KillBurst", new Color(1f, 0.5f, 0.2f));
            GameObject killBurstPrefab = GetOrCreateKillBurstPrefab(killBurstMat);
            Material vignetteMat = TransparentUnlitMaterial("DamageVignette");

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
            GameObject pillarL = Cube("Pillar_L", arena.transform, new Vector3(-7f, -5f, 4f), new Vector3(2f, 10f, 2f), wallMat);
            GameObject pillarR = Cube("Pillar_R", arena.transform, new Vector3(9f, -4f, 9f), new Vector3(2f, 12f, 2f), wallMat);

            Vector3[] targetSpots =
            {
                new Vector3(-8f, 0f, 6f),
                new Vector3(8f, 3f, 0f),
                new Vector3(0f, -4f, 11f),
                new Vector3(-4f, 6f, -5f),
            };
            var targets = new Object[targetSpots.Length];
            for (int i = 0; i < targetSpots.Length; i++)
            {
                GameObject target = Cube($"Target_{i + 1}", arena.transform, targetSpots[i], Vector3.one * 2f, targetMat);
                target.AddComponent<PrototypeTarget>();
                targets[i] = target.transform;
            }

            var flying = Hero("PlayerHero", arena.transform, Vector3.zero, heroMat, noseMat, barMat, isBot: false);
            GameObject playerGround = GroundMarker(flying, heroMarkerMat, beamMat, new Color(0.4f, 0.7f, 1f, 0.5f));
            // RUN mode items (R7): neutral until a run binds its inventory.
            var playerStats = flying.gameObject.AddComponent<RunHeroStats>();

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
            // Tracking-lost cue (P9): the player's floor disc greys while the clutch hand is lost.
            SetRefs(playerGround.GetComponent<HeroGroundMarker>(), ("clutchInput", playerInput));

            var controllers = new GameObject("Controllers");
            var puppeteer = controllers.AddComponent<HandPuppeteerController>();
            SetRefs(puppeteer, ("character", flying), ("inputSource", playerInput));
            // Charge orb lives outside the hero so HeroHealth's flash/visibility leave it alone.
            GameObject chargeOrb = Primitive(PrimitiveType.Sphere, "ChargeOrb", null, ArenaCenter, Vector3.one, chargeMat);
            var pointing = controllers.AddComponent<PointingBeamController>();
            SetRefs(pointing, ("character", flying), ("reticle", reticle.transform), ("beam", beam),
                ("inputSource", playerInput), ("chargeIndicator", chargeOrb.transform),
                ("hitEffectPrefab", impactPrefab), ("audioSource", flying.GetComponent<AudioSource>()),
                ("health", flying.GetComponent<HeroHealth>()), ("cursorMarker", cursorMarker.transform));
            // Lock-on ring around the target the player's aim snapped to (the bot has none).
            var lockOnGo = new GameObject("LockOnRing");
            var lockOnLine = lockOnGo.AddComponent<LineRenderer>();
            lockOnLine.sharedMaterial = beamMat;
            lockOnLine.useWorldSpace = false;
            lockOnLine.loop = true;
            lockOnLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lockOnLine.enabled = false;
            var lockOnRing = lockOnGo.AddComponent<LockOnRing>();
            SetRefs(lockOnRing, ("line", lockOnLine));
            SetRefs(pointing, ("lockOnRing", lockOnRing));
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
            GameObject botMarker = GroundMarker(botFlying, botMarkerMat, beamMat, new Color(1f, 0.4f, 0.4f, 0.5f));
            var bot = new GameObject("Bot");
            BotInputSource botInput = BotControllers(bot, botFlying, flying.transform, difficulty, beamMat, impactPrefab,
                killBurstPrefab);

            // RUN mode (R8): island bots come from a generated prefab, the Quick
            // Match bot hides while a run is on.
            RunBot runBotPrefab = GetOrCreateRunBotPrefab(botMat, noseMat, barMat, botMarkerMat, beamMat,
                impactPrefab, killBurstPrefab, difficulty);

            // Match loop (T6): heroes act only during the fight; HUD in front of the seat.
            var match = new GameObject("Match");
            var director = match.AddComponent<MatchDirector>();
            SetRefs(director, ("playerHealth", flying.GetComponent<HeroHealth>()),
                ("opponentHealth", botFlying.GetComponent<HeroHealth>()));
            if (xr) SetArray(director, "fightOnly", xrInput);
            else SetArray(director, "fightOnly", playerInput, xrInput);
            SetArray(director, "opponentOnly", botInput);

            var runDirector = match.AddComponent<RunDirector>();
            SetRefs(runDirector, ("match", director), ("playerHealth", flying.GetComponent<HeroHealth>()),
                ("playerStats", playerStats), ("botPrefab", runBotPrefab), ("arena", arena.transform));
            Vector3[] runSpawnSpots =
            {
                new Vector3(6f, 3f, 12f),
                new Vector3(-7f, 5f, 12f),
                new Vector3(1f, 7f, 15f),
            };
            var runSpawns = new Object[runSpawnSpots.Length];
            for (int i = 0; i < runSpawnSpots.Length; i++)
            {
                var spawn = new GameObject($"RunBotSpawn_{i + 1}");
                spawn.transform.SetParent(arena.transform, false);
                spawn.transform.localPosition = runSpawnSpots[i];
                runSpawns[i] = spawn.transform;
            }
            SetArray(runDirector, "botSpawnPoints", runSpawns);

            // Terrain variety, first slice (round 4, S7): the pillars and the run spawn
            // points get a seeded layout per island; the targets stay put and stay clear.
            var layoutApplier = match.AddComponent<ArenaLayoutApplier>();
            var layoutSo = new SerializedObject(layoutApplier);
            Prop(layoutSo, "arenaSize").vector3Value = ArenaSize;
            Prop(layoutSo, "playerStart").vector3Value = Vector3.zero; // PlayerHero's local position
            layoutSo.ApplyModifiedPropertiesWithoutUndo();
            SetArray(layoutApplier, "pieces", pillarL.transform, pillarR.transform);
            SetArray(layoutApplier, "spawnPoints", runSpawns);
            SetArray(layoutApplier, "keepClear", targets);
            SetRefs(runDirector, ("layout", layoutApplier));
            SetArray(runDirector, "hideDuringRun", botFlying.gameObject, botMarker);

            // Round 5 T3: terrain stage 2 - pooled low walls, floating platforms and thin
            // pillars (disabled; a run island turns some on) and the island colour themes
            // (floor, back wall, pieces, sun). Quick Match and the tutorial see neither.
            Transform[] lowWalls = PiecePool("LowWall", arena.transform, Core.ArenaPieceTable.LowWallSize, wallMat);
            Transform[] platforms = PiecePool("Platform", arena.transform, Core.ArenaPieceTable.PlatformSize, wallMat);
            Transform[] thinPillars = PiecePool("ThinPillar", arena.transform, Core.ArenaPieceTable.ThinPillarSize,
                wallMat);
            SetArray(layoutApplier, "lowWalls", lowWalls);
            SetArray(layoutApplier, "platforms", platforms);
            SetArray(layoutApplier, "thinPillars", thinPillars);
            var themeApplier = match.AddComponent<ArenaThemeApplier>();
            SetArray(themeApplier, "floor", arena.transform.Find("Floor").GetComponent<Renderer>());
            SetArray(themeApplier, "walls", arena.transform.Find("BackWall").GetComponent<Renderer>());
            var pieceRenderers = new System.Collections.Generic.List<Object>
            {
                pillarL.GetComponent<Renderer>(), pillarR.GetComponent<Renderer>(),
            };
            foreach (Transform[] pool in new[] { lowWalls, platforms, thinPillars })
                foreach (Transform piece in pool)
                    pieceRenderers.Add(piece.GetComponent<Renderer>());
            SetArray(themeApplier, "pieces", pieceRenderers.ToArray());
            SetRefs(themeApplier, ("sun", light));
            SetRefs(layoutApplier, ("themes", themeApplier));
            // Round 5 T3 end.

            // Freeze hunt (P1): frame spikes and tracking / focus / phase edges -> perf_log.txt.
            var perfLogger = match.AddComponent<PerfSpikeLogger>();
            SetRefs(perfLogger, ("match", director), ("run", runDirector), ("tracker", tracker));

            // Freeze hunt (P5): pooled beam hit effects shared by every hero, kept at the
            // scene root so despawned run bots never take them along.
            var impactPool = new GameObject("BeamImpactPool").AddComponent<BeamImpactPool>();
            SetRefs(impactPool, ("prefab", impactPrefab));

            // P11 (D14): synthesized sound effects, built once at scene load, plus the
            // match / run flow cues (countdown, FIGHT, cleared, results, pause).
            var sfxGo = new GameObject("Sfx");
            sfxGo.AddComponent<SfxPlayer>();
            var flowCues = sfxGo.AddComponent<GameSfxCues>();
            SetRefs(flowCues, ("match", director), ("run", runDirector));

            // P12 (D15): pooled kill bursts (same pool component as the beam hits) and
            // the red edge flash on a quad parented to the camera (the rig never moves).
            var burstPool = new GameObject("KillBurstPool").AddComponent<BeamImpactPool>();
            SetRefs(burstPool, ("prefab", killBurstPrefab));
            var burstSo = new SerializedObject(burstPool);
            Prop(burstSo, "prewarmCount").intValue = 4;
            Prop(burstSo, "maxCount").intValue = 8;
            burstSo.ApplyModifiedPropertiesWithoutUndo();

            GameObject vignetteQuad = Primitive(PrimitiveType.Quad, "DamageVignette", camGo.transform,
                new Vector3(0f, 0f, 0.3f), new Vector3(1.3f, 1.3f, 1f), vignetteMat);
            var vignetteRenderer = vignetteQuad.GetComponent<Renderer>();
            vignetteRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            vignetteRenderer.receiveShadows = false;
            var vignette = camGo.AddComponent<DamageVignette>();
            SetRefs(vignette, ("playerHealth", flying.GetComponent<HeroHealth>()), ("quad", vignetteRenderer));

            // Round 4 (S1): draw a pooled run bot, beams, hit effects, the vignette and the
            // charge orb for a few frames at scene load so the first run starts warm.
            var warmup = match.AddComponent<RenderWarmup>();
            SetRefs(warmup, ("viewCamera", cam), ("run", runDirector), ("beamMaterial", beamMat),
                ("vignette", vignette), ("chargeOrb", chargeOrb.transform));
            SetArray(warmup, "effectPrefabs", impactPrefab, killBurstPrefab);

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
            SetRefs(hud, ("director", director), ("scoreLine", scoreLine), ("banner", banner), ("run", runDirector));

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
            // The menu ray tests only the button colliders (built-in UI layer, GM-12).
            var pointerSo = new SerializedObject(pointer);
            Prop(pointerSo, "buttonLayers").intValue = 1 << MenuButtonLayer;
            pointerSo.ApplyModifiedPropertiesWithoutUndo();

            // Panels sit 2.5 m ahead, about 11 degrees below eye level, under the banner.
            Vector3 panelPos = seat + new Vector3(0f, -0.5f, 2.5f);
            GameObject mainPanel = Panel("MainPanel", menuGo.transform, panelPos);
            // Main menu is a 3x2 grid (0.15 m gaps, R10): QUICK MATCH / RUN / TUTORIAL
            // on top, then DEMO, the passthrough view (Arena_Main only, T10) and the aim mode.
            const float gridX = 0.95f;
            const float gridY = 0.235f;
            MenuButton(mainPanel.transform, "QUICK MATCH", MatchDirector.MenuAction.StartMatch, -gridX, gridY,
                director, buttonMat);
            MenuButton(mainPanel.transform, "RUN", MatchDirector.MenuAction.StartRun, 0f, gridY, director, buttonMat);
            MenuButton(mainPanel.transform, "TUTORIAL", MatchDirector.MenuAction.StartWithTutorial, gridX, gridY,
                director, buttonMat);
            // Round 5 T4: DEMO opens the bottom row, a full row of three in Arena_Main
            // (DEMO / MR TABLE / AIM) and a centered pair in the sandbox (DEMO / AIM).
            MenuButton(mainPanel.transform, "DEMO", MatchDirector.MenuAction.StartDemo, xr ? -gridX : -gridX * 0.5f,
                -gridY, director, buttonMat);
            HandMenuButton viewButton = xr
                ? MenuButton(mainPanel.transform, "MR TABLE", MatchDirector.MenuAction.ToggleViewMode, 0f,
                    -gridY, director, buttonMat)
                : null;
            HandMenuButton aimButton = MenuButton(mainPanel.transform, "AIM: ASSIST",
                MatchDirector.MenuAction.ToggleAimMode, xr ? gridX : gridX * 0.5f, -gridY, director, buttonMat);
            GameObject pausePanel = Panel("PausePanel", menuGo.transform, panelPos);
            MenuButton(pausePanel.transform, "RESUME", MatchDirector.MenuAction.Resume, -0.5f, 0f, director, buttonMat);
            MenuButton(pausePanel.transform, "MENU", MatchDirector.MenuAction.ReturnToMenu, 0.5f, 0f, director,
                buttonMat);
            // Round 4 (S5 / D5): RESET PROGRESS on a second row, dark red; a second press confirms.
            HandMenuButton resetButton = Button(pausePanel.transform, "Button_RESET PROGRESS", "RESET PROGRESS", 0f,
                -0.42f, MenuButtonSize, buttonMat);
            var resetButtonSo = new SerializedObject(resetButton);
            Prop(resetButtonSo, "idleColor").colorValue = new Color(0.35f, 0.1f, 0.1f);
            resetButtonSo.ApplyModifiedPropertiesWithoutUndo();
            var resetProgress = resetButton.gameObject.AddComponent<ResetProgressButton>();
            SetRefs(resetProgress, ("run", runDirector));
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
            SetRefs(runDirector, ("playerAim", pointing), ("aimModeSetting", aimMode));

            RunChoiceMenu runChoices = RunChoicePanels(menuGo, panelPos, runDirector, buttonMat);

            var handMenu = menuGo.AddComponent<HandMenu>();
            SetRefs(handMenu, ("director", director), ("pointer", pointer), ("mainPanel", mainPanel),
                ("pausePanel", pausePanel), ("tutorialPausePanel", tutorialPausePanel), ("matchEndPanel", endPanel),
                ("runChoices", runChoices), ("run", runDirector));

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
            Prop(tutorialSo, "ringRadius").floatValue = ringRadius;
            tutorialSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- Round 5 T4: demo mode (D6) ----
            // DemoDirector runs MatchPhase.Demo: the player invulnerable, two slow
            // Strikers made from the run bot prefab, the arena's practice targets
            // snappable by the aim assist (their AimAssistTarget is off outside the
            // demo), the Quick Match bot hidden. GhostHands draws the tracked hands (one
            // translucent material, a property block per hand); GestureCaptions names
            // each gesture beside them (seat space: the tabletop view keeps their size).
            var demoGo = new GameObject("Demo");
            demoGo.transform.SetParent(match.transform, false);
            var demoAssistTargets = new Object[targets.Length];
            for (int i = 0; i < targets.Length; i++)
            {
                var assist = ((Transform)targets[i]).gameObject.AddComponent<AimAssistTarget>();
                assist.enabled = false;
                demoAssistTargets[i] = assist;
            }
            var demo = demoGo.AddComponent<DemoDirector>();
            SetRefs(demo, ("match", director), ("playerHealth", flying.GetComponent<HeroHealth>()),
                ("botPrefab", runBotPrefab), ("arena", arena.transform));
            SetArray(demo, "botSpawnPoints", runSpawns);
            SetArray(demo, "hideDuringDemo", botFlying.gameObject, botMarker);
            SetArray(demo, "demoAssistTargets", demoAssistTargets);

            Material ghostHandsMat = TransparentUnlitMaterial("GhostHands");
            // Depth-tested like the world (the vignette's material draws over everything).
            if (ghostHandsMat.HasProperty("_ZTest"))
                ghostHandsMat.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.LessEqual);
            ghostHandsMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(ghostHandsMat);
            GameObject sphereProbe = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Mesh sphereMesh = sphereProbe.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(sphereProbe);
            var ghostHands = demoGo.AddComponent<GhostHands>();
            SetRefs(ghostHands, ("demo", demo), ("tracker", tracker), ("playerInput", playerInput),
                ("aimModeSetting", aimMode), ("material", ghostHandsMat), ("jointMesh", sphereMesh));

            // Pre-review T4-P1: the dark outline needs the OUTLINE_ON variant of TMP's
            // mobile SDF shader, and TMP's outlineWidth setter never turns that keyword
            // on. Both labels share one copy of TMP's LiberationSans outline material
            // (same font atlas): no runtime material instance, and the variant ships.
            const string tmpOutlinePath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Outline.mat";
            var tmpOutline = AssetDatabase.LoadAssetAtPath<Material>(tmpOutlinePath);
            if (tmpOutline == null)
                throw new System.InvalidOperationException($"[HandHeroSceneBuilder] Missing {tmpOutlinePath}");
            string captionMatPath = $"{MaterialDir}/DemoCaption.mat";
            var captionMat = AssetDatabase.LoadAssetAtPath<Material>(captionMatPath);
            if (captionMat == null)
            {
                captionMat = new Material(tmpOutline);
                AssetDatabase.CreateAsset(captionMat, captionMatPath);
            }
            else
            {
                captionMat.shader = tmpOutline.shader;
                captionMat.CopyPropertiesFromMaterial(tmpOutline);
            }
            captionMat.EnableKeyword(ShaderUtilities.Keyword_Outline);
            captionMat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.25f);
            captionMat.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
            EditorUtility.SetDirty(captionMat);

            var captionLabels = new TextMeshPro[2];
            for (int i = 0; i < captionLabels.Length; i++)
            {
                TextMeshPro label = WorldText(i == 0 ? "DemoCaption_L" : "DemoCaption_R", seatUI,
                    seat + new Vector3(i == 0 ? -0.3f : 0.3f, -0.3f, 0.5f), 0.26f);
                label.fontSharedMaterial = captionMat;
                label.rectTransform.sizeDelta = new Vector2(0.5f, 0.08f);
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.fontStyle = FontStyles.Bold;
                label.gameObject.SetActive(false);
                captionLabels[i] = label;
            }
            var captions = demoGo.AddComponent<GestureCaptions>();
            SetRefs(captions, ("demo", demo), ("tracker", tracker), ("head", camGo.transform),
                ("playerInput", playerInput), ("playerAim", pointing), ("playerShockwave", shockwave),
                ("aimModeSetting", aimMode), ("playerHero", flying.transform), ("leftLabel", captionLabels[0]),
                ("rightLabel", captionLabels[1]));
            // Pre-review T4-P4: the ghost hands' and captions' first draws are warmed
            // up at scene load with the rest (round 4 S1).
            SetRefs(warmup, ("ghostHands", ghostHands), ("captions", captions));
            // ---- end Round 5 T4 ----

            if (xr) ViewModeSwitch(cam, director, arena.transform, viewButton, beamMat);

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
            HandMenuButton viewButton, Material wireMat)
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
            RunDirector runDirector = director.GetComponent<RunDirector>();
            if (runDirector != null) SetRefs(runDirector, ("viewMode", viewMode));
            Transform backWall = arena.Find("BackWall");
            if (backWall != null) SetArray(viewMode, "arenaOnly", backWall.gameObject);
            var so = new SerializedObject(viewMode);
            Prop(so, "eyeHeight").floatValue = SeatEyeHeight;
            Prop(so, "arenaWidth").floatValue = ArenaSize.x;
            so.ApplyModifiedPropertiesWithoutUndo();
            SetRefs(director, ("viewMode", viewMode));

            // Round 4 (S8, D9): MR room-scan spike. The plane and bounding box managers
            // must sit on the XR Origin; both start disabled and only RoomScanProbe turns
            // them on (MR TABLE, main menu, scene permission granted, subsystem present).
            XROrigin xrOrigin = cam.GetComponentInParent<XROrigin>();
            var planeManager = xrOrigin.gameObject.AddComponent<ARPlaneManager>();
            planeManager.enabled = false;
            var boxManager = xrOrigin.gameObject.AddComponent<ARBoundingBoxManager>();
            boxManager.enabled = false;
            var probe = director.gameObject.AddComponent<RoomScanProbe>();
            SetRefs(probe, ("viewMode", viewMode), ("match", director), ("origin", xrOrigin),
                ("planeManager", planeManager), ("boxManager", boxManager), ("arena", arena),
                ("wireframeMaterial", wireMat));
            var probeSo = new SerializedObject(probe);
            Prop(probeSo, "arenaFloorLocalY").floatValue = -ArenaSize.y * 0.5f;
            probeSo.ApplyModifiedPropertiesWithoutUndo();
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
            // Bots never hit each other (BR-6); the Quick Match bot is still the player's enemy.
            SetEnum(flying, "team", (int)(isBot ? HandHero.Core.HeroTeam.Bot : HandHero.Core.HeroTeam.Player));

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
            HandMenuButton button = Button(panel, $"Button_{text}", text, x, y, MenuButtonSize, mat);
            SetRefs(button, ("director", director));
            var so = new SerializedObject(button);
            Prop(so, "action").enumValueIndex = (int)action;
            so.ApplyModifiedPropertiesWithoutUndo();
            return button;
        }

        private static readonly Vector3 MenuButtonSize = new Vector3(0.8f, 0.32f, 0.04f);

        // A HandMenuButton without a MenuAction; RunChoiceMenu sets its action and text.
        // `wrap` lets long card text (item descriptions) break into lines before it shrinks.
        private static HandMenuButton Button(Transform panel, string name, string text, float x, float y,
            Vector3 size, Material mat, bool wrap = false, float fontMin = 0.8f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(panel, false);
            go.transform.localPosition = new Vector3(x, y, 0f);
            var box = go.AddComponent<BoxCollider>();
            box.size = size;
            box.isTrigger = true;
            go.layer = MenuButtonLayer;

            GameObject bg = Primitive(PrimitiveType.Cube, "Background", go.transform, Vector3.zero, size, mat);
            TextMeshPro label = WorldText("Label", go.transform, Vector3.zero, 1.6f);
            label.rectTransform.sizeDelta = new Vector2(size.x, size.y);
            PlaceLocal(label, new Vector3(0f, 0f, -size.z * 0.5f - 0.005f));
            label.text = text;
            // One line that shrinks to fit the button ("AIM: CURSOR" is wider than 0.8 m at 1.6).
            label.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            label.enableAutoSizing = true;
            label.fontSizeMax = 1.6f;
            label.fontSizeMin = fontMin;
            label.margin = new Vector4(0.04f, 0f, 0.04f, 0f);

            var button = go.AddComponent<HandMenuButton>();
            SetRefs(button, ("background", bg.GetComponent<Renderer>()), ("label", label));
            return button;
        }

        // Title line above a panel's buttons, at `y` m above the panel center.
        private static TextMeshPro PanelHeader(GameObject panel, float y)
        {
            TextMeshPro header = WorldText("Header", panel.transform, panel.transform.position, 1.4f);
            header.rectTransform.sizeDelta = new Vector2(3f, 0.25f);
            PlaceLocal(header, new Vector3(0f, y, 0f));
            header.textWrappingMode = TextWrappingModes.NoWrap;
            header.enableAutoSizing = true;
            header.fontSizeMax = 1.4f;
            header.fontSizeMin = 0.6f;
            return header;
        }

        // RUN choice panels (R9) at the menu spot: portal buttons, chest cards and
        // the shop. RunChoiceMenu lays out and labels the portal / chest buttons.
        private static RunChoiceMenu RunChoicePanels(GameObject menuGo, Vector3 panelPos, RunDirector runDirector,
            Material mat)
        {
            var portalSize = new Vector3(0.85f, 0.4f, 0.04f);
            var cardSize = new Vector3(0.85f, 0.6f, 0.04f);
            var slotSize = new Vector3(0.85f, 0.46f, 0.04f);

            GameObject portalPanel = Panel("RunPortalPanel", menuGo.transform, panelPos);
            TextMeshPro portalHeader = PanelHeader(portalPanel, 0.42f);
            var portals = new Object[3];
            for (int i = 0; i < portals.Length; i++)
                portals[i] = Button(portalPanel.transform, $"Portal_{i + 1}", "", 0f, 0f, portalSize, mat);

            GameObject chestPanel = Panel("RunChestPanel", menuGo.transform, panelPos);
            TextMeshPro chestHeader = PanelHeader(chestPanel, 0.75f);
            var cards = new Object[4]; // 3 + Big Chests
            for (int i = 0; i < cards.Length; i++)
                cards[i] = Button(chestPanel.transform, $"Card_{i + 1}", "", 0f, 0f, cardSize, mat, wrap: true,
                    fontMin: 0.5f);

            // Shop: 2x2 pedestals, REROLL / LEAVE below.
            GameObject shopPanel = Panel("RunShopPanel", menuGo.transform, panelPos);
            TextMeshPro shopHeader = PanelHeader(shopPanel, 0.85f);
            var slots = new Object[Core.Shop.PedestalCount];
            for (int i = 0; i < slots.Length; i++)
                slots[i] = Button(shopPanel.transform, $"Slot_{i + 1}", "", (i % 2 - 0.5f) * 0.95f,
                    0.53f - (i / 2) * 0.52f, slotSize, mat, wrap: true, fontMin: 0.5f);
            HandMenuButton reroll = Button(shopPanel.transform, "Button_REROLL", "REROLL", -0.475f, -0.5f,
                MenuButtonSize, mat);
            HandMenuButton leave = Button(shopPanel.transform, "Button_LEAVE", "LEAVE", 0.475f, -0.5f,
                MenuButtonSize, mat);

            var menu = menuGo.AddComponent<RunChoiceMenu>();
            SetRefs(menu, ("run", runDirector), ("portalPanel", portalPanel), ("portalHeader", portalHeader),
                ("chestPanel", chestPanel), ("chestHeader", chestHeader), ("shopPanel", shopPanel),
                ("shopHeader", shopHeader), ("rerollButton", reroll), ("leaveButton", leave));
            SetArray(menu, "portalButtons", portals);
            SetArray(menu, "chestCards", cards);
            SetArray(menu, "shopSlots", slots);
            return menu;
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

        // Bot input + puppeteer + beam + telegraph on `bot`, flying `botHero` at `enemy`.
        // Shared by the Quick Match bot and the RUN bot prefab. `renderParent` holds
        // the line renderers (null = scene root).
        private static BotInputSource BotControllers(GameObject bot, FlyingCharacter botHero, Transform enemy,
            BotDifficulty difficulty, Material beamMat, GameObject impactPrefab, GameObject deathEffectPrefab,
            Transform renderParent = null)
        {
            SetRefs(botHero.GetComponent<HeroHealth>(), ("deathEffectPrefab", deathEffectPrefab));

            var botBeamGo = new GameObject("BotBeamRenderer");
            botBeamGo.transform.SetParent(renderParent, false);
            var botBeam = botBeamGo.AddComponent<LineRenderer>();
            botBeam.sharedMaterial = beamMat;
            botBeam.enabled = false;

            var botInput = bot.AddComponent<BotInputSource>();
            var botPuppeteer = bot.AddComponent<HandPuppeteerController>();
            SetRefs(botPuppeteer, ("character", botHero), ("inputSource", botInput));
            var botPointing = bot.AddComponent<PointingBeamController>();
            SetRefs(botPointing, ("character", botHero), ("beam", botBeam), ("inputSource", botInput),
                ("hitEffectPrefab", impactPrefab), ("audioSource", botHero.GetComponent<AudioSource>()));
            var beamSo = new SerializedObject(botPointing);
            Prop(beamSo, "beamColor").colorValue = new Color(1f, 0.25f, 0.2f);
            // No assist for the bot: a snap at fire time would retarget the player's
            // current position and void the telegraph dodge (spec D10).
            Prop(beamSo, "assistAngle").floatValue = 0f;
            beamSo.ApplyModifiedPropertiesWithoutUndo();
            SetRefs(botInput, ("self", botHero), ("puppeteer", botPuppeteer), ("enemy", enemy),
                ("hitReceiver", botHero.GetComponent<BeamHitReceiver>()), ("difficulty", difficulty));

            // Enemy beam warning line (aim locked -> thickens, yellow -> red -> fire).
            var telegraphGo = new GameObject("BotTelegraphRenderer");
            telegraphGo.transform.SetParent(renderParent, false);
            var telegraph = telegraphGo.AddComponent<LineRenderer>();
            telegraph.sharedMaterial = beamMat;
            telegraph.enabled = false;
            var telegraphLine = bot.AddComponent<BotTelegraphLine>();
            SetRefs(telegraphLine, ("bot", botInput), ("botHero", botHero), ("line", telegraph));
            return botInput;
        }

        // RUN island bot (R8): the Quick Match bot's hero and controllers under one
        // root. The arena and the enemy are set at spawn (RunBot.Setup); the input
        // starts off and RunDirector turns it on while the island is fought.
        // Rewritten on every build so the asset always matches this code.
        private static RunBot GetOrCreateRunBotPrefab(Material botMat, Material noseMat, Material barMat,
            Material markerMat, Material beamMat, GameObject impactPrefab, GameObject deathEffectPrefab,
            BotDifficulty difficulty)
        {
            EnsureFolder(BotDir);
            var root = new GameObject("RunBot");
            FlyingCharacter hero = Hero("RunBotHero", root.transform, Vector3.zero, botMat, noseMat, barMat, isBot: true);
            GroundMarker(hero, markerMat, beamMat, new Color(1f, 0.4f, 0.4f, 0.5f)).transform.SetParent(root.transform, false);
            BotInputSource input = BotControllers(root, hero, null, difficulty, beamMat, impactPrefab, deathEffectPrefab,
                root.transform);
            input.enabled = false;

            var runBot = root.AddComponent<RunBot>();
            SetRefs(runBot, ("hero", hero), ("health", hero.GetComponent<HeroHealth>()), ("input", input),
                ("pointing", root.GetComponent<PointingBeamController>()));
            ArchetypeShapes(hero, runBot, botMat);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, RunBotPrefabPath);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<RunBot>();
        }

        // Round 4 (S6): archetype silhouette parts under the bot's Visual (they turn
        // with it), inactive until RunBot.Activate shows the archetype's set. No
        // colliders: the root sphere stays the only hit volume (same hitbox for all).
        // The body and parts take the archetype color; the nose and the fins keep their own.
        private static void ArchetypeShapes(FlyingCharacter hero, RunBot runBot, Material mat)
        {
            Transform visual = hero.transform.Find("Visual");

            // Sniper: a tall needle and a thin barrel through the nose.
            GameObject needle = ShapeGroup("Shape_Needle", visual);
            Primitive(PrimitiveType.Cylinder, "Needle", needle.transform, new Vector3(0f, 0.95f, -0.1f),
                new Vector3(0.1f, 0.55f, 0.1f), mat);
            Primitive(PrimitiveType.Cylinder, "Barrel", needle.transform, new Vector3(0f, 0f, 1.25f),
                new Vector3(0.07f, 0.45f, 0.07f), mat).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            // Gunner: a chunky wide block across the body.
            GameObject block = ShapeGroup("Shape_Block", visual);
            Primitive(PrimitiveType.Cube, "Block", block.transform, new Vector3(0f, -0.05f, -0.1f),
                new Vector3(1.9f, 0.45f, 0.75f), mat);

            // Lancer: a long lance forward with a cross guard.
            GameObject lance = ShapeGroup("Shape_Lance", visual);
            Primitive(PrimitiveType.Cylinder, "Lance", lance.transform, new Vector3(0f, -0.05f, 1.5f),
                new Vector3(0.18f, 0.9f, 0.18f), mat).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Primitive(PrimitiveType.Cube, "Guard", lance.transform, new Vector3(0f, -0.05f, 0.65f),
                new Vector3(0.9f, 0.12f, 0.12f), mat);

            SetArray(runBot, "shapes", needle, block, lance);

            var tinted = new System.Collections.Generic.List<Object>();
            // Round 5 T1 (F4-1): only the body takes the archetype color. The fins
            // (Fin_L / Fin_R) keep the bot material's red, as decided in round 4 (S6-3).
            foreach (string part in new[] { "Body" })
            {
                Transform t = visual.Find(part);
                if (t != null) tinted.Add(t.GetComponent<Renderer>());
            }
            foreach (GameObject group in new[] { needle, block, lance })
                foreach (Renderer r in group.GetComponentsInChildren<Renderer>(true))
                    tinted.Add(r);
            SetArray(hero.GetComponent<HeroHealth>(), "baseColorRenderers", tinted.ToArray());

            needle.SetActive(false);
            block.SetActive(false);
            lance.SetActive(false);
        }

        private static GameObject ShapeGroup(string name, Transform parent)
        {
            var group = new GameObject(name);
            group.transform.SetParent(parent, false);
            return group;
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

        // Bot kill burst prefab (P12): eight cube shards at the corners fly outward.
        // Rewritten on every build like the impact prefab.
        private static GameObject GetOrCreateKillBurstPrefab(Material mat)
        {
            EnsureFolder(FxDir);
            var temp = new GameObject("KillBurst");
            const float offset = 0.15f;
            for (int i = 0; i < 8; i++)
            {
                var at = new Vector3((i & 1) == 0 ? -offset : offset, (i & 2) == 0 ? -offset : offset,
                    (i & 4) == 0 ? -offset : offset);
                GameObject shard = Primitive(PrimitiveType.Cube, "Shard" + i, temp.transform, at, Vector3.one * 0.3f, mat);
                shard.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            temp.AddComponent<KillBurst>();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temp, KillBurstPrefabPath);
            Object.DestroyImmediate(temp);
            return prefab;
        }

        // Round 5 T3: a pool of disabled terrain pieces (cubes with colliders, so they
        // stop beams like the pillars) for ArenaLayoutApplier. Parked on the far half
        // of the floor; the applier places a piece before turning it on.
        private static Transform[] PiecePool(string name, Transform arena, Vector3 size, Material mat)
        {
            var pool = new Transform[Core.ArenaPieceTable.DefaultPoolSize];
            for (int i = 0; i < pool.Length; i++)
            {
                var parked = new Vector3(0f, -ArenaSize.y * 0.5f + size.y * 0.5f, ArenaSize.z * 0.25f);
                GameObject piece = Cube($"{name}_{i + 1}", arena, parked, size, mat);
                piece.SetActive(false);
                pool[i] = piece.transform;
            }
            return pool;
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

        // A renamed or removed serialized field must fail BuildAll; logging and carrying
        // on produced scenes with silently missing wiring.
        private static SerializedProperty Prop(SerializedObject so, string field)
        {
            SerializedProperty prop = so.FindProperty(field);
            if (prop == null)
                throw new System.InvalidOperationException(
                    $"[HandHeroSceneBuilder] {so.targetObject.GetType().Name} has no field '{field}'");
            return prop;
        }

        private static void SetArray(Object target, string field, params Object[] values)
        {
            var so = new SerializedObject(target);
            SerializedProperty prop = Prop(so, field);
            if (!prop.isArray)
                throw new System.InvalidOperationException(
                    $"[HandHeroSceneBuilder] {target.GetType().Name} has no array field '{field}'");
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetEnum(Object target, string field, int value)
        {
            var so = new SerializedObject(target);
            Prop(so, field).enumValueIndex = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetRefs(Object target, params (string field, Object value)[] refs)
        {
            var so = new SerializedObject(target);
            foreach (var (field, value) in refs)
                Prop(so, field).objectReferenceValue = value;
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

        // URP Unlit switched to alpha-blended transparent, drawn after the world
        // (the damage vignette; its texture and color come from a property block).
        private static Material TransparentUnlitMaterial(string name)
        {
            Material mat = GetOrCreateMaterial(name, "Universal Render Pipeline/Unlit", Color.white);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_SrcBlendAlpha")) mat.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            if (mat.HasProperty("_DstBlendAlpha"))
                mat.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            if (mat.HasProperty("_ZTest")) mat.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = 3500;
            EditorUtility.SetDirty(mat);
            return mat;
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
