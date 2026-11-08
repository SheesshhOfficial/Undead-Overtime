using System;
using NUnit.Framework;

namespace UndeadOvertime.Core.Tests
{
    public sealed class SurvivalRulesTests
    {
        [Test]
        public void WaveCapAndStaggeredSpawnsCannotCompleteBeforeAllAreDefeated()
        {
            var waves = new WaveProgression(3, 1);
            Assert.That(waves.TryReserveSpawn(2), Is.False);
            Assert.That(waves.BeginNextWave(), Is.True);
            Assert.That(waves.BeginNextWave(), Is.False);
            Assert.That(waves.TryReserveSpawn(2), Is.True);
            Assert.That(waves.TryReserveSpawn(2), Is.True);
            Assert.That(waves.TryReserveSpawn(2), Is.False);
            Assert.That(waves.RegisterDefeat(), Is.True);
            Assert.That(waves.RegisterDefeat(), Is.True);
            Assert.That(waves.Phase, Is.EqualTo(SurvivalPhase.Fighting));
            Assert.That(waves.RegisterDefeat(), Is.False);
            Assert.That(waves.TryReserveSpawn(2), Is.True);
            Assert.That(waves.RegisterDefeat(), Is.True);
            Assert.That(waves.Phase, Is.EqualTo(SurvivalPhase.Preparation));
            Assert.That(waves.BeginNextWave(), Is.True);
            Assert.That(waves.WaveNumber, Is.EqualTo(2));
            Assert.That(waves.TryReserveSpawn(2), Is.True);
            Assert.That(waves.RegisterDefeat(), Is.True);
            Assert.That(waves.Phase, Is.EqualTo(SurvivalPhase.Extraction));
            Assert.That(waves.TryReserveSpawn(2), Is.False);
            Assert.That(waves.TryExtract(), Is.True);
            Assert.That(waves.TryExtract(), Is.False);
            Assert.That(waves.Lose(), Is.False);
        }

        [Test]
        public void DeathIsTerminalAndResetRestoresPristineState()
        {
            var waves = new WaveProgression(2);
            Assert.That(waves.TryExtract(), Is.False);
            waves.BeginNextWave();
            waves.TryReserveSpawn(2);
            Assert.That(waves.Lose(), Is.True);
            Assert.That(waves.Lose(), Is.False);
            Assert.That(waves.RegisterDefeat(), Is.False);
            Assert.That(waves.BeginNextWave(), Is.False);
            waves.Reset();
            Assert.That(waves.Phase, Is.EqualTo(SurvivalPhase.Preparation));
            Assert.That(waves.WaveNumber, Is.Zero);
            Assert.That(waves.ActiveEnemies, Is.Zero);
            Assert.That(waves.Spawned, Is.Zero);
        }

        [Test]
        public void WaveConfigIsCopiedAndInvalidSizesRejected()
        {
            int[] sizes = { 2 };
            var waves = new WaveProgression(sizes);
            sizes[0] = 100;
            waves.BeginNextWave();
            Assert.That(waves.WaveSize, Is.EqualTo(2));
            Assert.Throws<ArgumentException>(() => new WaveProgression());
            Assert.Throws<ArgumentException>(() => new WaveProgression(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WaveProgression(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WaveProgression(1001));
            Assert.Throws<ArgumentOutOfRangeException>(() => waves.TryReserveSpawn(0));
        }

        [Test]
        public void MagazineLimitsRateAndAmmoAndReloadCompletesOnce()
        {
            var magazine = new PrototypeMagazine();
            Assert.That(magazine.TryReload(0), Is.False);
            for (int shot = 0; shot < 12; shot++)
            {
                Assert.That(magazine.TryFire(shot), Is.True);
                Assert.That(magazine.TryFire(shot), Is.False);
            }
            Assert.That(magazine.Ammo, Is.Zero);
            Assert.That(magazine.TryFire(12), Is.False);
            Assert.That(magazine.TryReload(12), Is.True);
            Assert.That(magazine.TryReload(12.5), Is.False);
            Assert.That(magazine.TryFire(13), Is.False);
            Assert.That(magazine.Advance(13.5), Is.True);
            Assert.That(magazine.Ammo, Is.EqualTo(12));
            Assert.That(magazine.IsReloading, Is.False);
            Assert.That(magazine.TryFire(13.5), Is.True);
            Assert.That(magazine.Ammo, Is.EqualTo(11));
        }

        [Test]
        public void ReloadAndTimeGuardsSurvivePauseAndReset()
        {
            var magazine = new PrototypeMagazine();
            magazine.TryFire(1);
            magazine.TryReload(1);
            Assert.That(magazine.Advance(1), Is.True);
            Assert.That(magazine.IsReloading, Is.True);
            Assert.That(magazine.Advance(0), Is.False);
            Assert.That(magazine.Advance(double.NaN), Is.False);
            Assert.That(magazine.Advance(double.PositiveInfinity), Is.False);
            magazine.Reset();
            Assert.That(magazine.Ammo, Is.EqualTo(12));
            Assert.That(magazine.IsReloading, Is.False);
            Assert.That(magazine.TryFire(0), Is.True);
        }
    }
}
