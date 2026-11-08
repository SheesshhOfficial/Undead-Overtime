using UnityEngine;

namespace UndeadOvertime.Prototype
{
    /// <summary>Stationary test dummy, deliberately not zombie AI or a networked enemy.</summary>
    public sealed class PrototypeTarget : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");
        private readonly TargetHealth health = new TargetHealth(100);
        private Renderer targetRenderer;
        private Collider targetCollider;
        private MaterialPropertyBlock propertyBlock;
        private Color idleColor;
        private float flashUntil;

        public int CurrentHealth => health.Current;
        public bool IsAlive => health.IsAlive;

        public void Initialize(Renderer renderer, Collider collider, Color color)
        {
            targetRenderer = renderer;
            targetCollider = collider;
            propertyBlock = new MaterialPropertyBlock();
            idleColor = color;
            ResetTarget();
        }

        public bool TakeDamage(int damage)
        {
            bool killed = health.ApplyDamage(damage);
            if (damage > 0)
            {
                flashUntil = Time.time + 0.12f;
                SetColor(Color.white);
            }
            if (killed)
            {
                targetRenderer.enabled = false;
                targetCollider.enabled = false;
            }
            return killed;
        }

        public void ResetTarget()
        {
            health.Reset();
            flashUntil = 0f;
            targetRenderer.enabled = true;
            targetCollider.enabled = true;
            SetColor(idleColor);
        }

        private void Update()
        {
            if (flashUntil <= 0f || Time.time < flashUntil) return;
            flashUntil = 0f;
            SetColor(idleColor);
        }

        private void SetColor(Color color)
        {
            propertyBlock.SetColor(BaseColor, color);
            propertyBlock.SetColor(ColorProperty, color);
            targetRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
