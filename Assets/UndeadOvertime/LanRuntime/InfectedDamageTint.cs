using UndeadOvertime.Core;
using UnityEngine;

namespace UndeadOvertime.LanPrototype
{
    /// <summary>Owns the property block on one generated fallback renderer. No material instance,
    /// mesh, collider, object lifetime or transform changes. Not a private-character adapter.</summary>
    public sealed class InfectedDamageTint
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor"), ColorProperty = Shader.PropertyToID("_Color");
        private static readonly Color Tint = new Color(.95f, .65f, .2f, 1);
        private readonly Renderer target;
        private readonly MaterialPropertyBlock original, working;
        private readonly bool hasBase, hasColor;
        private readonly Color baseValue, colorValue;
        private float lastAmount;

        public InfectedDamageTint(Renderer renderer)
        {
            target = renderer;
            if (target == null || target.sharedMaterial == null) return;
            Material material = target.sharedMaterial;
            hasBase = material.HasProperty(BaseColor); hasColor = material.HasProperty(ColorProperty);
            if (!hasBase && !hasColor) return;
            original = new MaterialPropertyBlock(); working = new MaterialPropertyBlock();
            target.GetPropertyBlock(original);
            if (hasBase) baseValue = original.HasProperty(BaseColor) ? original.GetColor(BaseColor) : material.GetColor(BaseColor);
            if (hasColor) colorValue = original.HasProperty(ColorProperty) ? original.GetColor(ColorProperty) : material.GetColor(ColorProperty);
        }

        public void Apply(double pulse)
        {
            if (target == null || original == null) return;
            float amount = AimVector.Finite(pulse) ? Mathf.Clamp01((float)pulse) : 0;
            if (amount == lastAmount) return;
            lastAmount = amount;
            if (amount <= 0) { target.SetPropertyBlock(original.isEmpty ? null : original); return; }
            target.GetPropertyBlock(working);
            if (hasBase) working.SetColor(BaseColor, Tinted(baseValue, amount));
            if (hasColor) working.SetColor(ColorProperty, Tinted(colorValue, amount));
            target.SetPropertyBlock(working);
        }
        private static Color Tinted(Color baseline, float pulse)
        {
            Color value = Color.Lerp(baseline, Tint, pulse * .65f);
            value.a = baseline.a; return value;
        }
        public void Reset() { Apply(0); }
    }
}
