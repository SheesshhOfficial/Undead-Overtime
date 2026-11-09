using UndeadOvertime.Core;
using UnityEngine;

namespace UndeadOvertime.LanPrototype
{
    public sealed partial class LanRange
    {
        private readonly InfectedLocomotionPresentation infectedPresentation = new InfectedLocomotionPresentation();

        // A read-only seam for a future validated private idle/walk visual. It does not load content,
        // create Animators, rotate/move host objects, infer melee, or extend a dead enemy's lifetime.
        public bool TryGetInfectedLocomotion(byte enemyId, out InfectedLocomotionState state)
        {
            state = default;
            return PresentationEnabled && infectedPresentation.TryRead(enemyId, Time.realtimeSinceStartupAsDouble, out state);
        }

        private void ObserveInfectedPresentation(RangeSnapshot state)
        {
            if (PresentationEnabled) infectedPresentation.Observe(state, Time.realtimeSinceStartupAsDouble);
            else infectedPresentation.Reset();
        }
    }
}
