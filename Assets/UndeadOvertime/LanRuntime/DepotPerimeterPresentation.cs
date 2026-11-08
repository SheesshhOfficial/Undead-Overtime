using System;
using UnityEngine;

namespace UndeadOvertime.LanPrototype
{
    /// <summary>Mission-only render duplicates for existing range walls. The originals keep all collision.</summary>
    public sealed class DepotPerimeterPresentation : IDisposable
    {
        private readonly Renderer[] originals = new Renderer[4];
        private readonly bool[] enabledBefore = new bool[4];
        private bool disposed;
        public Transform Root { get; private set; }
        private static readonly string[] WallNames = { "North wall", "South wall", "West wall", "East wall" };
        public DepotPerimeterPresentation(Transform parent, Material sharedConcrete)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (sharedConcrete == null) throw new ArgumentNullException(nameof(sharedConcrete));
            if (!Application.isPlaying || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                throw new InvalidOperationException("Depot perimeter is a rendered Play Mode presentation only.");
            Root = new GameObject("Depot perimeter original concrete surfaces").transform; Root.SetParent(parent, false);
            try
            {
                for (int i = 0; i < WallNames.Length; i++)
                {
                    Transform source = parent.Find(WallNames[i]);
                    if (source == null) throw new InvalidOperationException("Missing existing range wall " + WallNames[i] + ".");
                    var sourceFilter = source.GetComponent<MeshFilter>(); var sourceRenderer = source.GetComponent<MeshRenderer>();
                    if (sourceFilter == null || sourceFilter.sharedMesh == null || sourceRenderer == null || source.GetComponent<BoxCollider>() == null)
                        throw new InvalidOperationException("Existing range wall needs its mesh, renderer and BoxCollider.");
                    originals[i] = sourceRenderer; enabledBefore[i] = sourceRenderer.enabled;
                    var visual = new GameObject("Concrete " + WallNames[i]); visual.transform.SetParent(Root, false);
                    visual.transform.localPosition = source.localPosition; visual.transform.localRotation = source.localRotation;
                    visual.transform.localScale = source.localScale;
                    visual.layer = source.gameObject.layer;
                    visual.AddComponent<MeshFilter>().sharedMesh = sourceFilter.sharedMesh;
                    var renderer = visual.AddComponent<MeshRenderer>(); renderer.sharedMaterials = sourceRenderer.sharedMaterials;
                    renderer.shadowCastingMode = sourceRenderer.shadowCastingMode; renderer.receiveShadows = sourceRenderer.receiveShadows;
                    DepotWallSurface.Apply(visual, sharedConcrete); renderer.enabled = enabledBefore[i];
                    // Attach ownership while active, so Unity will run its OnDestroy even for a hidden source wall.
                    visual.SetActive(source.gameObject.activeSelf);
                    // Hide only after a complete replacement exists. The original mesh/material/collider is untouched.
                    sourceRenderer.enabled = false;
                }
            }
            catch { Dispose(); throw; }
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            if (Root != null) { Root.gameObject.SetActive(false); UnityEngine.Object.Destroy(Root.gameObject); }
            Root = null;
            for (int i = 0; i < originals.Length; i++)
                if (originals[i] != null) originals[i].enabled = enabledBefore[i];
        }
    }
}
