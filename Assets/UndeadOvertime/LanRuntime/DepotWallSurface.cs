using System;
using UnityEngine;

namespace UndeadOvertime.LanPrototype
{
    /// <summary>One owned cube-mesh UV copy. Existing collider, vertices and shared PBR material stay unchanged.</summary>
    public sealed class DepotWallSurface : MonoBehaviour
    {
        private Mesh ownedMesh;
        public static DepotWallSurface Apply(GameObject wall, Material sharedConcrete)
        {
            if (wall == null) throw new ArgumentNullException(nameof(wall));
            if (sharedConcrete == null) throw new ArgumentNullException(nameof(sharedConcrete));
            if (!wall.activeInHierarchy) throw new InvalidOperationException("Wall must be active while attaching mesh ownership.");
            if (!Application.isPlaying) throw new InvalidOperationException("Depot wall surfaces are runtime-only.");
            if (wall.GetComponent<DepotWallSurface>() != null) throw new InvalidOperationException("Wall already owns a surface copy.");
            var filter = wall.GetComponent<MeshFilter>(); var renderer = wall.GetComponent<MeshRenderer>();
            if (filter == null || filter.sharedMesh == null || renderer == null)
                throw new ArgumentException("Wall requires a cube mesh and renderer.", nameof(wall));
            Vector3 size = wall.transform.lossyScale;
            if (!PositiveFinite(size.x) || !PositiveFinite(size.y) || !PositiveFinite(size.z))
                throw new ArgumentException("Wall requires positive finite scale.", nameof(wall));
            Mesh source = filter.sharedMesh;
            Vector3[] vertices = source.vertices, normals = source.normals;
            if (vertices.Length != normals.Length || vertices.Length == 0)
                throw new ArgumentException("Wall cube must have face normals.", nameof(wall));
            var uv = new Vector2[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 v = vertices[i], n = normals[i];
                if (!Finite(v.x) || !Finite(v.y) || !Finite(v.z) ||
                    Mathf.Abs(v.x) > .5001f || Mathf.Abs(v.y) > .5001f || Mathf.Abs(v.z) > .5001f)
                    throw new ArgumentException("Wall mesh must be a centered unit cube.", nameof(wall));
                // One texture repeat per world meter. Signed axes keep outward tangent orientation on every face.
                if (Mathf.Abs(n.x) > .999f) uv[i] = new Vector2((-Mathf.Sign(n.x) * v.z + .5f) * size.z, (v.y + .5f) * size.y);
                else if (Mathf.Abs(n.y) > .999f) uv[i] = new Vector2((v.x + .5f) * size.x, (-Mathf.Sign(n.y) * v.z + .5f) * size.z);
                else if (Mathf.Abs(n.z) > .999f) uv[i] = new Vector2((Mathf.Sign(n.z) * v.x + .5f) * size.x, (v.y + .5f) * size.y);
                else throw new ArgumentException("Wall mesh must have axis-aligned cube face normals.", nameof(wall));
            }
            Mesh copy = Instantiate(source); copy.name = "Owned depot wall meter UVs";
            try
            {
                copy.uv = uv; copy.RecalculateTangents();
                var owner = wall.AddComponent<DepotWallSurface>(); owner.ownedMesh = copy;
                filter.sharedMesh = copy; renderer.sharedMaterial = sharedConcrete; return owner;
            }
            catch { Destroy(copy); throw; }
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool PositiveFinite(float value) => Finite(value) && value > 0;
        private void OnDestroy() { if (ownedMesh != null) Destroy(ownedMesh); ownedMesh = null; }
    }
}
