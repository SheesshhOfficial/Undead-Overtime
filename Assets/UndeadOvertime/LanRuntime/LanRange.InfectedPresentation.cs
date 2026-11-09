using UndeadOvertime.Core;
using UnityEngine;

namespace UndeadOvertime.LanPrototype
{
    public sealed partial class LanRange
    {
        private readonly InfectedLocomotionPresentation infectedPresentation = new InfectedLocomotionPresentation();
        private readonly InfectedDamageFeedback infectedDamage = new InfectedDamageFeedback();
        private readonly InfectedDamageTint[] infectedTints = new InfectedDamageTint[HostInfectedEncounter.MaximumEnemies];
        public bool InfectedDamagePulseEnabled { get; private set; } = true;

        public void SetInfectedDamagePulseEnabled(bool enabled)
        {
            if (InfectedDamagePulseEnabled == enabled) return;
            InfectedDamagePulseEnabled = enabled; infectedDamage.Reset();
            foreach (InfectedDamageTint tint in infectedTints) tint?.Reset();
        }
        public bool TryGetInfectedDamagePulse(byte enemyId, out double pulse)
        {
            pulse = 0;
            return PresentationEnabled && InfectedDamagePulseEnabled &&
                infectedDamage.TryRead(enemyId, Time.realtimeSinceStartupAsDouble, out pulse);
        }

        // A read-only seam for a future validated private idle/walk visual. It does not load content,
        // create Animators, rotate/move host objects, infer melee, or extend a dead enemy's lifetime.
        public bool TryGetInfectedLocomotion(byte enemyId, out InfectedLocomotionState state)
        {
            state = default;
            return PresentationEnabled && infectedPresentation.TryRead(enemyId, Time.realtimeSinceStartupAsDouble, out state);
        }

        private void ObserveInfectedPresentation(RangeSnapshot state)
        {
            if (!PresentationEnabled) { ResetInfectedPresentation(); return; }
            double now = Time.realtimeSinceStartupAsDouble;
            infectedPresentation.Observe(state, now);
            if (InfectedDamagePulseEnabled) infectedDamage.Observe(state, now);
            else infectedDamage.Reset();
        }
        private void ResetInfectedPresentation()
        {
            infectedPresentation.Reset(); infectedDamage.Reset();
            foreach (InfectedDamageTint tint in infectedTints) tint?.Reset();
        }
        private void UpdateInfectedDamageVisuals()
        {
            if (!PresentationEnabled) return;
            double now = Time.realtimeSinceStartupAsDouble;
            for (int i = 0; i < infectedObjects.Length; i++)
            {
                if (infectedObjects[i] == null) continue;
                if (infectedTints[i] == null)
                {
                    Renderer renderer = infectedObjects[i].GetComponent<Renderer>();
                    if (renderer != null) infectedTints[i] = new InfectedDamageTint(renderer);
                }
                double pulse = 0;
                if (InfectedDamagePulseEnabled && infectedObjects[i].activeSelf && snapshot != null && i < snapshot.EnemyCount)
                    infectedDamage.TryRead(snapshot.Enemy(i).Id, now, out pulse);
                infectedTints[i]?.Apply(pulse);
            }
        }
    }
}
