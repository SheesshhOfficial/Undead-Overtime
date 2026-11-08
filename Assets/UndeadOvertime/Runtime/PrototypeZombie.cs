using UnityEngine;

namespace UndeadOvertime.Prototype
{
    /// <summary>Temporary capsule pursuit in the open solo arena, not production AI/navigation.</summary>
    [RequireComponent(typeof(CharacterController), typeof(PrototypeTarget))]
    public sealed class PrototypeZombie : MonoBehaviour
    {
        private CharacterController motor;
        private PrototypeTarget target;
        private PrototypeSurvival owner;
        private float speed, verticalVelocity, nextAttackAt;
        public PrototypeTarget Target => target;

        public void Initialize(PrototypeSurvival survival, float moveSpeed)
        {
            owner = survival;
            speed = moveSpeed;
            motor = GetComponent<CharacterController>();
            target = GetComponent<PrototypeTarget>();
        }

        public void Step(float deltaTime)
        {
            if (owner == null || target == null) return;
            if (!target.IsAlive)
            {
                owner.RegisterDefeated(this);
                return;
            }
            if (!owner.CanFight || !owner.Player.IsAlive) return;
            Vector3 difference = owner.Player.transform.position - transform.position;
            difference.y = 0f;
            float distance = difference.magnitude;
            Vector3 direction = distance > 0.001f ? difference / distance : Vector3.zero;
            if (direction.sqrMagnitude > 0f) transform.rotation = Quaternion.LookRotation(direction);
            if (motor.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            verticalVelocity -= 22f * deltaTime;
            Vector3 displacement = distance > 1.25f ? direction * (speed * deltaTime) : Vector3.zero;
            displacement.y = verticalVelocity * deltaTime;
            motor.Move(displacement);
            if (transform.position.y < -5f)
            {
                // Treat an out-of-bounds prototype enemy as defeated so the test cannot soft-lock.
                target.TakeDamage(100);
                owner.RegisterDefeated(this);
                return;
            }
            if (distance > 1.45f || owner.SimulationTime < nextAttackAt) return;
            Vector3 attackOrigin = transform.position + Vector3.up * 1.1f;
            Vector3 attackTarget = owner.Player.transform.position + Vector3.up * 1.1f;
            // The player is on Ignore Raycast; the line tests only intervening world/enemy geometry.
            if (Physics.Linecast(attackOrigin, attackTarget, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return;
            nextAttackAt = owner.SimulationTime + 1.2f;
            owner.Player.TakeDamage(15);
        }
    }
}
