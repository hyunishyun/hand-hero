using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
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
        public const string BotDifficultyPath = BotDir + "/BotDifficulty_Normal.asset";

        // Heroes stay on the Default layer: PointingBeamController skips its own
        // hero's colliders, so player and bot can hit each other without new
        // project layers (ProjectSettings stay untouched until T9).

        private static readonly Vector3 ArenaCenter = new Vector3(0f, 2f, 20f);
        private static readonly Vector3 ArenaSize = new Vector3(35f, 20f, 35f);

        [MenuItem("HandHero/Build All Scenes")]
        public static void BuildAll()
        {
            BuildSandbox();
        }

        // Headset-free test scene: plain camera, greybox arena, player hero,
        // targets, and debug keyboard/mouse input (XR hand input is present too,
        // swap the controllers' Input Source field to use it).
        [MenuItem("HandHero/Build Sandbox Scene")]
        public static void BuildSandbox()
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
            Material beamMat = BeamMaterial("Beam");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Seat: fixed camera, never moves (ADR 4/5).
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.position = new Vector3(0f, 1.2f, 0f);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 200f;
            camGo.AddComponent<AudioListener>();

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

            var flying = Hero("PlayerHero", arena.transform, Vector3.zero, heroMat, noseMat);

            // Aim feedback: reticle without collider (else the aim ray hits it and jitters).
            GameObject reticle = Primitive(PrimitiveType.Sphere, "Reticle", null, ArenaCenter, Vector3.one * 0.4f, reticleMat);

            var beamGo = new GameObject("BeamRenderer");
            var beam = beamGo.AddComponent<LineRenderer>();
            beam.sharedMaterial = beamMat;
            beam.enabled = false;

            // Input: debug keyboard/mouse drives the controllers in the editor.
            var inputGo = new GameObject("HandInput");
            var tracker = inputGo.AddComponent<HandGestureTracker>();
            SetRefs(tracker, ("headCamera", camGo.transform));
            var xrInput = inputGo.AddComponent<XRHandsInputSource>();
            SetRefs(xrInput, ("tracker", tracker));
            var debugInput = inputGo.AddComponent<DebugKeyboardMouseInputSource>();
            SetRefs(debugInput, ("viewCamera", cam));

            var controllers = new GameObject("Controllers");
            var puppeteer = controllers.AddComponent<HandPuppeteerController>();
            SetRefs(puppeteer, ("character", flying), ("inputSource", debugInput));
            var pointing = controllers.AddComponent<PointingBeamController>();
            SetRefs(pointing, ("character", flying), ("reticle", reticle.transform), ("beam", beam),
                ("inputSource", debugInput));

            // Bot opponent: same hero, same controllers, input from BotInputSource.
            BotDifficulty difficulty = GetOrCreateBotDifficulty();
            var botFlying = Hero("BotHero", arena.transform, new Vector3(6f, 3f, 12f), botMat, noseMat);

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

            EditorSceneManager.SaveScene(scene, SandboxScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[HandHeroSceneBuilder] Built {SandboxScenePath}");
        }

        // Greybox hero: collider + BeamHitReceiver on the root (beams hit it),
        // visual pivot rotated by FlyingCharacter for facing/banking only.
        private static FlyingCharacter Hero(string name, Transform arena, Vector3 localPos, Material bodyMat,
            Material noseMat)
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
            return flying;
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
