using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace UndeadOvertime.Prototype
{
    /// <summary>Builds an isolated greybox at runtime, only in its explicitly opened test scene.</summary>
    public sealed class PrototypeRange : MonoBehaviour
    {
        [SerializeField] private Camera worldCamera;
        [SerializeField] private bool survivalMode;
        private readonly List<Material> materials = new List<Material>();
        private readonly List<PrototypeTarget> targets = new List<PrototypeTarget>();
        private Transform generatedRoot;

        public bool IsBuilt { get; private set; }
        public bool IsSurvivalMode => survivalMode;
        public PrototypeSurvival Survival { get; private set; }
        public double GameplayTime => Survival != null ? Survival.SimulationTime : Time.time;
        public PrototypePlayer Player { get; private set; }
        public IReadOnlyList<PrototypeTarget> Targets => targets;
        public Camera WorldCamera => worldCamera;

        private void Start()
        {
            if (!IsBuilt) Build(worldCamera);
        }

        public void BuildSurvival(Camera camera)
        {
            if (IsBuilt) throw new InvalidOperationException("BuildSurvival must be selected before construction.");
            survivalMode = true;
            Build(camera);
        }

        /// <summary>Explicit entry point also used by PlayMode tests. Repeated calls are harmless.</summary>
        public void Build(Camera camera)
        {
            if (IsBuilt) return;
            if (camera == null) throw new ArgumentNullException(nameof(camera), "The range needs its assigned scene camera.");
            worldCamera = camera;
            worldCamera.nearClipPlane = 0.05f;
            worldCamera.farClipPlane = 150f;
            worldCamera.fieldOfView = 75f;
            generatedRoot = new GameObject("Generated prototype (Play Mode only)").transform;
            generatedRoot.SetParent(transform, false);

            Material floor = CreateMaterial("Prototype floor", new Color(0.19f, 0.23f, 0.25f));
            Material wall = CreateMaterial("Prototype walls", new Color(0.42f, 0.47f, 0.48f));
            Material cover = CreateMaterial("Prototype cover", new Color(0.30f, 0.40f, 0.46f));
            Material target = CreateMaterial("Prototype targets", new Color(0.80f, 0.24f, 0.10f));
            Material player = CreateMaterial("Prototype player", new Color(0.12f, 0.60f, 0.72f));
            Material marker = CreateMaterial("Prototype calibration", new Color(1f, 0.72f, 0.15f));

            Box("Ground 32m x 32m", new Vector3(0f, -0.25f, 0f), new Vector3(32f, 0.5f, 32f), floor);
            Box("West boundary", new Vector3(-16f, 1.5f, 0f), new Vector3(0.5f, 3f, 32f), wall);
            Box("East boundary", new Vector3(16f, 1.5f, 0f), new Vector3(0.5f, 3f, 32f), wall);
            Box("North boundary", new Vector3(0f, 1.5f, 16f), new Vector3(32f, 3f, 0.5f), wall);
            Box("South boundary", new Vector3(0f, 1.5f, -16f), new Vector3(32f, 3f, 0.5f), wall);
            if (!survivalMode)
            {
                Box("Tall cover", new Vector3(-6f, 0.75f, 0f), new Vector3(4f, 1.5f, 2f), cover);
                Box("Low cover", new Vector3(5f, 0.5f, 2f), new Vector3(3f, 1f, 2f), cover);
                Box("One metre calibration cube", new Vector3(-10f, 0.5f, -5f), Vector3.one, marker);
                Box("Firing line", new Vector3(0f, 0.012f, -6f), new Vector3(20f, 0.02f, 0.08f), marker, false);
                AddTarget("Target A", new Vector3(-5f, 1f, 8f), target);
                AddTarget("Target B", new Vector3(0f, 1f, 10f), target);
                AddTarget("Target C", new Vector3(5f, 1f, 8f), target);
            }
            GameObject playerObject = new GameObject("Prototype player (local only)");
            playerObject.transform.SetParent(generatedRoot, false);
            playerObject.transform.position = new Vector3(0f, 0.10f, -9f);
            // Built-in layer 2: normal collisions, but excluded from default camera and firing raycasts.
            playerObject.layer = 2;
            CharacterController controller = playerObject.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.stepOffset = 0.3f;
            controller.slopeLimit = 45f;
            controller.skinWidth = 0.035f;
            controller.minMoveDistance = 0f;
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Temporary body (third person only)";
            visual.layer = 2;
            visual.transform.SetParent(playerObject.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            visual.transform.localScale = new Vector3(0.65f, 0.9f, 0.65f);
            RemoveCollider(visual);
            Renderer body = visual.GetComponent<Renderer>();
            body.sharedMaterial = player;
            Player = playerObject.AddComponent<PrototypePlayer>();
            Player.Initialize(worldCamera, body, marker, this);
            if (survivalMode)
            {
                Material infected = CreateMaterial("Prototype infected", new Color(0.32f, 0.48f, 0.18f));
                Material exit = CreateMaterial("Prototype extraction", new Color(0.15f, 0.95f, 0.45f));
                GameObject zone = Box("Extraction zone (after waves)", new Vector3(0f, 0.015f, -12f), new Vector3(4f, 0.02f, 4f), exit, false);
                Survival = generatedRoot.gameObject.AddComponent<PrototypeSurvival>();
                Survival.Initialize(this, generatedRoot, infected, zone.GetComponent<Renderer>());
            }
            IsBuilt = true;
            Debug.Log(survivalMode ? "Undead Overtime: solo Survival Yard ready. R reloads, T restarts. Placeholder infected; no networking." :
                "Undead Overtime: local prototype range ready. V toggles FPS/TPS, R resets. No networking is active.", this);
        }

        private Material CreateMaterial(string label, Color color)
        {
            Material template = GraphicsSettings.currentRenderPipeline != null
                ? GraphicsSettings.currentRenderPipeline.defaultMaterial : null;
            Material material;
            if (template != null) material = new Material(template);
            else
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (shader == null) throw new InvalidOperationException("No supported prototype material shader was found.");
                material = new Material(shader);
            }
            material.name = label;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.18f);
            materials.Add(material);
            return material;
        }

        private GameObject Box(string label, Vector3 position, Vector3 scale, Material material, bool collision = true)
        {
            GameObject item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = label;
            item.transform.SetParent(generatedRoot, false);
            item.transform.position = position;
            item.transform.localScale = scale;
            item.GetComponent<Renderer>().sharedMaterial = material;
            if (!collision) RemoveCollider(item);
            return item;
        }

        private void AddTarget(string label, Vector3 position, Material material)
        {
            GameObject item = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            item.name = label;
            item.transform.SetParent(generatedRoot, false);
            item.transform.position = position;
            item.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
            Renderer renderer = item.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            PrototypeTarget target = item.AddComponent<PrototypeTarget>();
            target.Initialize(renderer, item.GetComponent<Collider>(), new Color(0.80f, 0.24f, 0.10f));
            targets.Add(target);
        }

        private static void RemoveCollider(GameObject item)
        {
            Collider collider = item.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
        }

        public void ResetRange()
        {
            if (!IsBuilt) return;
            foreach (PrototypeTarget target in targets) target.ResetTarget();
            if (Survival != null) Survival.ResetEncounter();
            Player.ResetPlayer();
        }

        private void OnDestroy()
        {
            foreach (Material material in materials)
            {
                if (material == null) continue;
                if (Application.isPlaying) Destroy(material);
                else DestroyImmediate(material);
            }
        }

        private void DrawSurvivalHud(float width)
        {
            GUI.Box(new Rect(12f, 12f, width, 166f), GUIContent.none);
            string objective;
            switch (Survival.Waves.Phase)
            {
                case UndeadOvertime.Core.SurvivalPhase.Preparation:
                    objective = $"Next wave in {Survival.PreparationRemaining:0.0}s";
                    break;
                case UndeadOvertime.Core.SurvivalPhase.Fighting:
                    objective = $"Wave {Survival.Waves.WaveNumber}/{Survival.Waves.TotalWaves} | Alive {Survival.Waves.ActiveEnemies} | Incoming {Survival.Waves.RemainingToSpawn}";
                    break;
                case UndeadOvertime.Core.SurvivalPhase.Extraction:
                    objective = $"Reach the GREEN zone and hold E / North to extract: {Survival.ExtractionProgress:P0}";
                    break;
                case UndeadOvertime.Core.SurvivalPhase.Won: objective = "EXTRACTED. T / Start: restart the encounter."; break;
                default: objective = "YOU DIED. T / Start: restart the encounter."; break;
            }
            GUI.Label(new Rect(24f, 20f, width - 24f, 152f),
                "UNDEAD OVERTIME | SURVIVAL YARD | SOLO PROTOTYPE\n" +
                "WASD / LS: move | Mouse / RS: look | Shift / L3: sprint | Space / South: jump\n" +
                "Fire: LMB / RT | Reload: R / West | FPS/TPS: V / Select | Restart: T / Start\n" +
                $"Health: {Player.CurrentHealth}/100 | Ammo: {Player.Ammo}/12 {(Player.IsReloading ? "RELOADING" : "")} | Kills: {Player.Kills}\n" +
                objective + "\n" +
                (Survival.IsTerminal ? "Encounter ended: T / Start restarts" :
                    Player.IsInputCaptured ? "Esc / East: release cursor and pause encounter" : "PAUSED: click / RT to capture; release before firing") + "\n" +
                "Placeholder capsules. Three waves, then extraction. No multiplayer or production art.");
        }

        private void OnGUI()
        {
            if (!IsBuilt) return;
            float panelWidth = Mathf.Min(590f, Screen.width - 24f);
            if (survivalMode) DrawSurvivalHud(panelWidth);
            else
            {
                GUI.Box(new Rect(12f, 12f, panelWidth, 142f), GUIContent.none);
                string captureHint = Player.IsInputCaptured ? "Esc: release cursor" : "Click / right trigger: capture controls";
                GUI.Label(new Rect(24f, 20f, panelWidth - 24f, 130f),
                    "UNDEAD OVERTIME | MECHANICS RANGE | SOLO ONLY\n" +
                    "WASD / left stick: move | Mouse / right stick: look | Shift / L3: sprint\n" +
                    "Space / South: jump | Left click / RT: fire | V / Select: FPS/TPS\n" +
                    "R / Start: reset | " + captureHint + "\n" +
                    $"View: {(Player.IsThirdPerson ? "TPS" : "FPS")} | Shots: {Player.ShotsFired} | Hits: {Player.Hits} | Targets: {Player.Kills}/3\n" +
                    "100 HP per target, 25 damage per hit. Orange targets are stationary test dummies.");
            }
            if (!Player.IsInputCaptured) return;
            Color old = GUI.color;
            GUI.color = Player.ShowHitMarker ? Color.yellow : Color.white;
            float x = Screen.width * 0.5f;
            float y = Screen.height * 0.5f;
            GUI.DrawTexture(new Rect(x - 6f, y - 1f, 12f, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x - 1f, y - 6f, 2f, 12f), Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}

