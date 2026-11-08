using System;

namespace UndeadOvertime.Core
{
    public enum SurvivalPhase { Preparation, Fighting, Extraction, Won, Lost }

    /// <summary>Count-based solo encounter progression. Timing, spawning and presentation are adapters.</summary>
    public sealed class WaveProgression
    {
        private readonly int[] waveSizes;
        public SurvivalPhase Phase { get; private set; }
        public int WaveNumber { get; private set; }
        public int TotalWaves => waveSizes.Length;
        public int Spawned { get; private set; }
        public int Defeated { get; private set; }
        public int ActiveEnemies => Spawned - Defeated;
        public int WaveSize => WaveNumber > 0 ? waveSizes[WaveNumber - 1] : 0;
        public int RemainingToSpawn => Phase == SurvivalPhase.Fighting ? WaveSize - Spawned : 0;

        public WaveProgression(params int[] sizes)
        {
            if (sizes == null || sizes.Length == 0) throw new ArgumentException("At least one wave is required.", nameof(sizes));
            waveSizes = (int[])sizes.Clone();
            foreach (int size in waveSizes)
                if (size < 1 || size > 1000) throw new ArgumentOutOfRangeException(nameof(sizes));
            Reset();
        }

        public void Reset()
        {
            Phase = SurvivalPhase.Preparation;
            WaveNumber = Spawned = Defeated = 0;
        }

        public bool BeginNextWave()
        {
            if (Phase != SurvivalPhase.Preparation || WaveNumber >= TotalWaves) return false;
            WaveNumber++;
            Spawned = Defeated = 0;
            Phase = SurvivalPhase.Fighting;
            return true;
        }

        public bool TryReserveSpawn(int maximumAlive)
        {
            if (maximumAlive < 1) throw new ArgumentOutOfRangeException(nameof(maximumAlive));
            if (Phase != SurvivalPhase.Fighting || Spawned >= WaveSize || ActiveEnemies >= maximumAlive) return false;
            Spawned++;
            return true;
        }

        /// <summary>Caller must deduplicate enemy deaths by identity before reporting here.</summary>
        public bool RegisterDefeat()
        {
            if (Phase != SurvivalPhase.Fighting || ActiveEnemies == 0) return false;
            Defeated++;
            if (Defeated == WaveSize && Spawned == WaveSize)
                Phase = WaveNumber == TotalWaves ? SurvivalPhase.Extraction : SurvivalPhase.Preparation;
            return true;
        }

        public bool TryExtract()
        {
            if (Phase != SurvivalPhase.Extraction) return false;
            Phase = SurvivalPhase.Won;
            return true;
        }

        public bool Lose()
        {
            if (Phase == SurvivalPhase.Won || Phase == SurvivalPhase.Lost) return false;
            Phase = SurvivalPhase.Lost;
            return true;
        }
    }
}
