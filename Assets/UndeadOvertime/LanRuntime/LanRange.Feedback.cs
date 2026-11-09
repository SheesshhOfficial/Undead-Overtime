using System.Globalization;
using UndeadOvertime.Core;
using UnityEngine;

namespace UndeadOvertime.LanPrototype
{
    public sealed partial class LanRange
    {
        private readonly LocalDamageFeedback damageFeedback = new LocalDamageFeedback();
        public bool DamageBorderEnabled { get; private set; } = true;
        public LocalDamageFeedbackState DamageFeedback => damageFeedback.Read(Time.realtimeSinceStartupAsDouble);
        public string DamageFeedbackText => DamageText(DamageFeedback);

        // Session-only presentation preference: no PlayerPrefs, settings file or authority mutation.
        // Text remains visible when the edge pulse is disabled.
        public void SetDamageBorderEnabled(bool enabled) { DamageBorderEnabled = enabled; }
        private static string DamageText(LocalDamageFeedbackState state) =>
            state.DamagePulse > 0 && !state.IsDowned ? "HIT: -" + state.LastDamage.ToString(CultureInfo.InvariantCulture) + " HP" : "";

        private void DrawDamageBorder(LocalDamageFeedbackState state)
        {
            if (!DamageBorderEnabled || !captured || state.IsDowned || state.DamagePulse <= 0 ||
                Event.current.type != EventType.Repaint || Screen.width <= 0 || Screen.height <= 0) return;
            // Non-directional, narrow and translucent. Never cover the aim point, flash the full
            // screen, shake the camera, infer an attacker, or turn a health drop into a hit marker.
            float width = Screen.width, height = Screen.height;
            float edge = Mathf.Min(24, Mathf.Min(width, height) * .02f);
            Color previous = GUI.color;
            try
            {
                GUI.color = new Color(.85f, .08f, .04f, .28f * (float)state.DamagePulse);
                Texture texture = Texture2D.whiteTexture;
                GUI.DrawTexture(new Rect(0, 0, width, edge), texture);
                GUI.DrawTexture(new Rect(0, height - edge, width, edge), texture);
                GUI.DrawTexture(new Rect(0, edge, edge, height - edge * 2), texture);
                GUI.DrawTexture(new Rect(width - edge, edge, edge, height - edge * 2), texture);
            }
            finally { GUI.color = previous; }
        }
    }
}
