using System.Collections.Generic;
using UndeadOvertime.Core;
using UnityEngine;

namespace UndeadOvertime.Prototype
{
    /// <summary>Explicitly solo, transient survival loop. No production assets or network state.</summary>
    public sealed class PrototypeSurvival : MonoBehaviour
    {
        public const int MaximumAlive = 6;
        public const float PreparationSeconds = 3f;
        public const float ExtractionSeconds = 2f;
        private static readonly Vector3 ExtractionPosition = new Vector3(0f, 0f, -12f);
        private readonly List<PrototypeZombie> zombies = new List<PrototypeZombie>();
        private readonly WaveProgression waves = new WaveProgression(5, 8, 12);
        private PrototypeRange range;
        private Transform generatedRoot;
        private Material zombieMaterial;
        private Renderer extractionMarker;
        private bool initialized, simulationEnabled = true, interactHeld;
        private float nextWaveAt, nextSpawnAt, extractionHeldFor;
        private int spawnIndex;
        public WaveProgression Waves => waves;
        public PrototypePlayer Player => range.Player;
        public IReadOnlyList<PrototypeZombie> Zombies => zombies;
        public float SimulationTime { get; private set; }
        public float ExtractionProgress => Mathf.Clamp01(extractionHeldFor / ExtractionSeconds);
        public bool IsTerminal => waves.Phase == SurvivalPhase.Won || waves.Phase == SurvivalPhase.Lost;
        public bool CanFight => initialized && waves.Phase == SurvivalPhase.Fighting;
        public float PreparationRemaining => Mathf.Max(0f, nextWaveAt - SimulationTime);

        public void Initialize(PrototypeRange arena, Transform root, Material enemyMaterial, Renderer marker)
        {
            if (initialized) return;
            range = arena;
            generatedRoot = root;
            zombieMaterial = enemyMaterial;
            extractionMarker = marker;
            initialized = true;
            ResetEncounter();
        }

        public void SetSimulationEnabled(bool enabled) => simulationEnabled = enabled;
        public void SetInteractHeld(bool held) => interactHeld = held;

        private void Update()
        {
            if (!initialized || !simulationEnabled || IsTerminal) return;
            if (!Player.IsInputCaptured)
            {
                interactHeld = false;
                extractionHeldFor = 0f;
                return;
            }
            Step(Mathf.Min(Time.deltaTime, 0.1f));
        }

        /// <summary>Bounded simulation entry point; also used by automated PlayMode checks.</summary>
        public void Step(float deltaTime)
        {
            if (!initialized || IsTerminal || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime <= 0f) return;
            deltaTime = Mathf.Min(deltaTime, 0.1f);
            SimulationTime += deltaTime;
            if (!Player.IsAlive) { Lose(); return; }
            for (int i = zombies.Count - 1; i >= 0; i--) zombies[i].Step(deltaTime);
            if (IsTerminal) return;
            if (waves.Phase == SurvivalPhase.Preparation && SimulationTime >= nextWaveAt)
            {
                waves.BeginNextWave();
                if (waves.WaveNumber > 1) Player.RestoreHealth(20);
                nextSpawnAt = SimulationTime;
            }
            if (waves.Phase == SurvivalPhase.Fighting && SimulationTime >= nextSpawnAt && waves.TryReserveSpawn(MaximumAlive))
            {
                SpawnZombie();
                nextSpawnAt = SimulationTime + 0.8f;
            }
            if (waves.Phase == SurvivalPhase.Extraction)
            {
                extractionMarker.enabled = true;
                Vector3 toExit = Player.transform.position - ExtractionPosition;
                toExit.y = 0f;
                bool inside = Mathf.Abs(toExit.x) <= 2f && Mathf.Abs(toExit.z) <= 2f &&
                    Mathf.Abs(Player.transform.position.y) < 1f;
                extractionHeldFor = inside && interactHeld ? extractionHeldFor + deltaTime : 0f;
                if (extractionHeldFor >= ExtractionSeconds && waves.TryExtract()) Player.ReleaseControls();
            }
        }

        private void SpawnZombie()
        {
            // Choose the farthest perimeter point, deterministic tie break, never spawn on the player.
            Vector3[] points = { new Vector3(-12f, 0.1f, 12f), new Vector3(0f, 0.1f, 12f),
                new Vector3(12f, 0.1f, 12f), new Vector3(-12f, 0.1f, -12f), new Vector3(12f, 0.1f, -12f) };
            int best = spawnIndex % points.Length;
            float bestScore = float.NegativeInfinity;
            for (int n = 0; n < points.Length; n++)
            {
                int index = (spawnIndex + n) % points.Length;
                float nearestEnemy = 1000f;
                foreach (PrototypeZombie existing in zombies)
                    nearestEnemy = Mathf.Min(nearestEnemy, (points[index] - existing.transform.position).sqrMagnitude);
                float score = (points[index] - Player.transform.position).sqrMagnitude + Mathf.Min(nearestEnemy, 16f) * 4f;
                if (score > bestScore) { bestScore = score; best = index; }
            }
            spawnIndex++;
            GameObject item = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            item.name = $"Placeholder infected W{waves.WaveNumber}-{waves.Spawned}";
            item.transform.SetParent(generatedRoot, false);
            item.transform.position = points[best];
            item.transform.localScale = Vector3.one;
            Collider primitiveCollider = item.GetComponent<Collider>();
            primitiveCollider.enabled = false;
            Destroy(primitiveCollider);
            CharacterController controller = item.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.stepOffset = 0.3f;
            controller.skinWidth = 0.035f;
            controller.minMoveDistance = 0f;
            // Offset the capsule visual separately so the gameplay pivot remains at foot level.
            MeshFilter filter = item.GetComponent<MeshFilter>();
            MeshRenderer renderer = item.GetComponent<MeshRenderer>();
            renderer.enabled = false;
            GameObject visual = new GameObject("Temporary infected visual");
            visual.transform.SetParent(item.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            visual.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
            visual.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            Renderer body = visual.AddComponent<MeshRenderer>();
            body.sharedMaterial = zombieMaterial;
            PrototypeTarget target = item.AddComponent<PrototypeTarget>();
            target.Initialize(body, controller, new Color(0.32f, 0.48f, 0.18f));
            PrototypeZombie zombie = item.AddComponent<PrototypeZombie>();
            zombie.Initialize(this, 1.25f + 0.15f * waves.WaveNumber);
            zombies.Add(zombie);
        }

        public void RegisterDefeated(PrototypeZombie zombie)
        {
            if (zombie == null || zombie.Target == null || zombie.Target.IsAlive || !zombies.Remove(zombie)) return;
            zombie.gameObject.SetActive(false);
            Destroy(zombie.gameObject);
            if (!waves.RegisterDefeat()) return;
            if (waves.Phase == SurvivalPhase.Preparation) nextWaveAt = SimulationTime + PreparationSeconds;
        }

        public void Lose()
        {
            if (waves.Lose()) Player.ReleaseControls();
        }

        public void ResetEncounter()
        {
            foreach (PrototypeZombie zombie in zombies)
            {
                if (zombie == null) continue;
                zombie.gameObject.SetActive(false);
                Destroy(zombie.gameObject);
            }
            zombies.Clear();
            waves.Reset();
            SimulationTime = nextSpawnAt = extractionHeldFor = 0f;
            nextWaveAt = PreparationSeconds;
            spawnIndex = 0;
            interactHeld = false;
            extractionMarker.enabled = false;
        }
    }
}
