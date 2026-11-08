using System.Collections;
using NUnit.Framework;
using UndeadOvertime.Core;
using UnityEngine;
using UnityEngine.TestTools;

namespace UndeadOvertime.Prototype.Tests
{
    public sealed class SurvivalYardTests
    {
        private GameObject root, cameraObject;
        private PrototypeRange range;
        private PrototypeSurvival survival;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            root = new GameObject("Survival test root");
            cameraObject = new GameObject("Survival test camera");
            range = root.AddComponent<PrototypeRange>();
            range.BuildSurvival(cameraObject.AddComponent<Camera>());
            range.Player.SetInputEnabled(false);
            survival = range.Survival;
            survival.SetSimulationEnabled(false);
            Physics.SyncTransforms();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Teardown()
        {
            Object.Destroy(root);
            Object.Destroy(cameraObject);
            yield return null;
        }

        private void Advance(float seconds)
        {
            int steps = Mathf.CeilToInt(seconds / 0.1f);
            for (int i = 0; i < steps; i++) survival.Step(0.1f);
            Physics.SyncTransforms();
        }

        [Test]
        public void SurvivalIsOptInAndCreatesOneDirectorWithoutStaticTargets()
        {
            Assert.That(range.IsSurvivalMode, Is.True);
            Assert.That(range.Targets.Count, Is.Zero);
            Assert.That(root.GetComponentsInChildren<PrototypeSurvival>(true).Length, Is.EqualTo(1));
            Assert.That(survival.Waves.Phase, Is.EqualTo(SurvivalPhase.Preparation));
            range.Build(range.WorldCamera);
            Assert.That(root.GetComponentsInChildren<PrototypePlayer>(true).Length, Is.EqualTo(1));
        }

        [Test]
        public void SpawnBudgetIsBoundedAndHealthyEnemyCannotBeCountedAsDead()
        {
            Advance(3.2f);
            Assert.That(survival.Zombies.Count, Is.EqualTo(1));
            survival.RegisterDefeated(survival.Zombies[0]);
            Assert.That(survival.Zombies.Count, Is.EqualTo(1));
            Advance(4f);
            Assert.That(survival.Zombies.Count, Is.EqualTo(5));
            Assert.That(survival.Zombies.Count, Is.LessThanOrEqualTo(PrototypeSurvival.MaximumAlive));
            Assert.That(survival.Waves.RemainingToSpawn, Is.Zero);
        }

        [Test]
        public void InfectedPursuesAndAttacksButCannotAttackThroughWorldWall()
        {
            Advance(3.2f);
            var zombie = survival.Zombies[0];
            float before = Vector3.Distance(zombie.transform.position, range.Player.transform.position);
            survival.Step(0.1f);
            Assert.That(Vector3.Distance(zombie.transform.position, range.Player.transform.position), Is.LessThan(before));
            PlaceZombie(zombie, range.Player.transform.position + Vector3.forward * 1.4f);
            GameObject wall = new GameObject("Attack blocker");
            wall.transform.SetParent(root.transform);
            wall.transform.position = range.Player.transform.position + new Vector3(0f, 1f, 0.7f);
            wall.AddComponent<BoxCollider>().size = new Vector3(2f, 2f, 0.2f);
            Physics.SyncTransforms();
            survival.Step(0.1f);
            Assert.That(range.Player.CurrentHealth, Is.EqualTo(100));
            wall.SetActive(false);
            Physics.SyncTransforms();
            survival.Step(0.1f);
            Assert.That(range.Player.CurrentHealth, Is.EqualTo(85));
            survival.Step(0.1f);
            Assert.That(range.Player.CurrentHealth, Is.EqualTo(85), "Attack cooldown prevents damage every frame.");
        }

        [UnityTest]
        public IEnumerator RealHitscanDamagesMovingEnemyAndConsumesAmmo()
        {
            Advance(3.2f);
            var zombie = survival.Zombies[0];
            PlaceZombie(zombie, new Vector3(0f, 0.1f, -4f));
            for (int i = 0; i < 4; i++)
            {
                Physics.SyncTransforms();
                Assert.That(range.Player.TryFire(), Is.True);
                Assert.That(range.Player.TryFire(), Is.False);
                if (i < 3) Advance(0.2f);
            }
            Assert.That(range.Player.Hits, Is.EqualTo(4));
            Assert.That(range.Player.Kills, Is.EqualTo(1));
            Assert.That(range.Player.Ammo, Is.EqualTo(8));
            Assert.That(zombie.Target.IsAlive, Is.False);
            survival.Step(0.1f);
            Assert.That(survival.Waves.Defeated, Is.EqualTo(1));
            Assert.That(range.Player.TryReload(), Is.True);
            Advance(1.6f);
            yield return null; // Player updates the magazine from the simulation clock.
            Assert.That(range.Player.Ammo, Is.EqualTo(12));
        }

        [UnityTest]
        public IEnumerator DeathStopsCombatAndRepeatedRestartClearsOldEnemies()
        {
            Advance(3.2f);
            range.Player.TakeDamage(1000);
            Assert.That(range.Player.CurrentHealth, Is.Zero);
            Assert.That(survival.Waves.Phase, Is.EqualTo(SurvivalPhase.Lost));
            Assert.That(range.Player.TryFire(), Is.False);
            float time = survival.SimulationTime;
            Advance(2f);
            Assert.That(survival.SimulationTime, Is.EqualTo(time));
            for (int i = 0; i < 3; i++)
            {
                range.ResetRange();
                Assert.That(survival.Zombies.Count, Is.Zero);
                Assert.That(range.Player.CurrentHealth, Is.EqualTo(100));
                Assert.That(range.Player.Ammo, Is.EqualTo(12));
                Assert.That(range.Player.Kills, Is.Zero);
                Assert.That(survival.SimulationTime, Is.Zero);
                Assert.That(survival.Waves.Phase, Is.EqualTo(SurvivalPhase.Preparation));
                Advance(3.2f);
            }
            range.ResetRange();
            yield return null;
            Assert.That(root.GetComponentsInChildren<PrototypeZombie>(true).Length, Is.Zero);
        }

        [Test]
        public void ThreeWavesRequireKillsThenContinuousExtractionHold()
        {
            // Exercise actual spawned objects, identity deduplication and wave transitions.
            for (int frame = 0; frame < 1000 && survival.Waves.Phase != SurvivalPhase.Extraction; frame++)
            {
                for (int i = survival.Zombies.Count - 1; i >= 0; i--) survival.Zombies[i].Target.TakeDamage(100);
                survival.Step(0.1f);
                Assert.That(survival.Zombies.Count, Is.LessThanOrEqualTo(PrototypeSurvival.MaximumAlive));
            }
            Assert.That(survival.Waves.Phase, Is.EqualTo(SurvivalPhase.Extraction));
            Assert.That(survival.Waves.WaveNumber, Is.EqualTo(3));
            var motor = range.Player.GetComponent<CharacterController>();
            motor.enabled = false;
            // A visible square corner must count too, not just an inscribed circular region.
            range.Player.transform.position = new Vector3(1.8f, 0.1f, -10.2f);
            motor.enabled = true;
            Physics.SyncTransforms();
            survival.SetInteractHeld(true);
            Advance(1f);
            Assert.That(survival.ExtractionProgress, Is.GreaterThan(0f));
            survival.SetInteractHeld(false);
            survival.Step(0.1f);
            Assert.That(survival.ExtractionProgress, Is.Zero);
            survival.SetInteractHeld(true);
            Advance(2.2f);
            Assert.That(survival.Waves.Phase, Is.EqualTo(SurvivalPhase.Won));
            range.Player.TakeDamage(100);
            Assert.That(range.Player.CurrentHealth, Is.EqualTo(100));
            Assert.That(range.Player.TryFire(), Is.False);
        }

        [UnityTest]
        public IEnumerator ReleasedCursorPausesEncounterClock()
        {
            survival.SetSimulationEnabled(true);
            range.Player.ReleaseControls();
            float before = survival.SimulationTime;
            yield return new WaitForSeconds(0.2f);
            Assert.That(survival.SimulationTime, Is.EqualTo(before));
            Assert.That(survival.Zombies.Count, Is.Zero);
        }

        private static void PlaceZombie(PrototypeZombie zombie, Vector3 position)
        {
            var controller = zombie.GetComponent<CharacterController>();
            controller.enabled = false;
            zombie.transform.position = position;
            controller.enabled = true;
            Physics.SyncTransforms();
        }
    }
}
