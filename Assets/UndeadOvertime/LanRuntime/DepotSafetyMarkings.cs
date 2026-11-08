using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace UndeadOvertime.LanPrototype
{
    /// <summary>Static, renderer-only depot paint. Owns one combined mesh; never owns or alters the supplied material.
    /// Coordinates are world meters above the accepted floor top at y=0. No arrows imply an unlocked gate.</summary>
    public sealed class DepotSafetyMarkings : MonoBehaviour
    {
        public const float PaintHeight = .012f, LineWidth = .10f;
        // Four 8 x 4 m bay outlines, all outside the x=[-2,2] central route.
        public const float WestBayMinX = -13, WestBayMaxX = -5, EastBayMinX = 5, EastBayMaxX = 13;
        public const float SouthBayMinZ = 1, SouthBayMaxZ = 5, NorthBayMinZ = 7, NorthBayMaxZ = 11;
        // Clearance/approach paint, not an authoritative interaction area. Open north toward the cabinet at x=-9.
        // South/front strip spans z=[-6.8,-6.7], inside the unchanged 2 m power interaction reach at its center.
        public const float ApproachMinX = -10.15f, ApproachLeftInnerX = -10.05f;
        public const float ApproachRightInnerX = -7.95f, ApproachMaxX = -7.85f;
        public const float ApproachMinZ = -6.8f, ApproachJoinZ = -6.7f, ApproachMaxZ = -5.25f;
        // Dashes occupy x=[-1.7,-1.6] and [1.6,1.7]; the inner 3.2 m remains unpainted.
        // They end before extraction x=[-2,2], z=[10,14], and never cross the closed gate clearance.
        public const float LaneInnerX = 1.6f, LaneOuterX = 1.7f;
        public const float LaneMinZ = -13, LaneMaxZ = 8, DashLength = 1, DashPeriod = 2;
        public const float GateClearMinZ = -2.35f, GateClearMaxZ = -1.65f;
        public const int QuadCount = 41, TriangleCount = QuadCount * 2;
        private Mesh ownedMesh;

        public static DepotSafetyMarkings Create(Transform activeIdentityParent, Material paint)
        {
            // Validate everything before creating an object or mesh. Active attachment guarantees OnDestroy ownership.
            if (activeIdentityParent == null) throw new ArgumentNullException(nameof(activeIdentityParent));
            if (paint == null) throw new ArgumentNullException(nameof(paint));
            if (!activeIdentityParent.gameObject.activeInHierarchy)
                throw new InvalidOperationException("Depot paint ownership requires an active parent.");
            if (!IdentityWorld(activeIdentityParent))
                throw new ArgumentException("Depot paint requires a finite identity world-space parent.", nameof(activeIdentityParent));
            if (!Application.isPlaying || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("Depot paint requires rendered Play Mode.");

            var root = new GameObject("Depot safety floor markings");
            try
            {
                root.transform.SetParent(activeIdentityParent, false);
                var owner = root.AddComponent<DepotSafetyMarkings>();
                owner.ownedMesh = new Mesh { name = "Owned depot safety paint" };
                owner.BuildMesh();
                root.AddComponent<MeshFilter>().sharedMesh = owner.ownedMesh;
                var renderer = root.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = paint;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = true;
                return owner;
            }
            catch { root.SetActive(false); Destroy(root); throw; }
        }

        private void BuildMesh()
        {
            var vertices = new List<Vector3>(QuadCount * 4);
            var normals = new List<Vector3>(QuadCount * 4);
            var uv = new List<Vector2>(QuadCount * 4);
            var triangles = new List<int>(TriangleCount * 3);
            AddBay(WestBayMinX, WestBayMaxX, SouthBayMinZ, SouthBayMaxZ);
            AddBay(EastBayMinX, EastBayMaxX, SouthBayMinZ, SouthBayMaxZ);
            AddBay(WestBayMinX, WestBayMaxX, NorthBayMinZ, NorthBayMaxZ);
            AddBay(EastBayMinX, EastBayMaxX, NorthBayMinZ, NorthBayMaxZ);
            AddQuad(ApproachMinX, ApproachMaxX, ApproachMinZ, ApproachJoinZ);
            AddQuad(ApproachMinX, ApproachLeftInnerX, ApproachJoinZ, ApproachMaxZ);
            AddQuad(ApproachRightInnerX, ApproachMaxX, ApproachJoinZ, ApproachMaxZ);
            for (float z = LaneMinZ; z < LaneMaxZ; z += DashPeriod)
            {
                float end = Mathf.Min(z + DashLength, LaneMaxZ);
                // Clip rather than paint under the gate. No connecting strip or directional arrow crosses this gap.
                if (z < GateClearMinZ) AddLanePair(z, Mathf.Min(end, GateClearMinZ));
                if (end > GateClearMaxZ) AddLanePair(Mathf.Max(z, GateClearMaxZ), end);
            }
            if (vertices.Count != QuadCount * 4 || triangles.Count != TriangleCount * 3)
                throw new InvalidOperationException("Depot paint exceeded its fixed geometry contract.");
            ownedMesh.SetVertices(vertices); ownedMesh.SetNormals(normals);
            ownedMesh.SetUVs(0, uv); ownedMesh.SetTriangles(triangles, 0);
            ownedMesh.RecalculateBounds(); ownedMesh.RecalculateTangents();

            void AddBay(float minX, float maxX, float minZ, float maxZ)
            {
                // End strips own the corners. Side strips stop at their inside edges, preventing coplanar overlap.
                AddQuad(minX, maxX, minZ, minZ + LineWidth);
                AddQuad(minX, maxX, maxZ - LineWidth, maxZ);
                AddQuad(minX, minX + LineWidth, minZ + LineWidth, maxZ - LineWidth);
                AddQuad(maxX - LineWidth, maxX, minZ + LineWidth, maxZ - LineWidth);
            }
            void AddLanePair(float minZ, float maxZ)
            {
                if (maxZ <= minZ) return;
                AddQuad(-LaneOuterX, -LaneInnerX, minZ, maxZ);
                AddQuad(LaneInnerX, LaneOuterX, minZ, maxZ);
            }
            void AddQuad(float minX, float maxX, float minZ, float maxZ)
            {
                int first = vertices.Count;
                vertices.Add(new Vector3(minX, PaintHeight, minZ));
                vertices.Add(new Vector3(minX, PaintHeight, maxZ));
                vertices.Add(new Vector3(maxX, PaintHeight, maxZ));
                vertices.Add(new Vector3(maxX, PaintHeight, minZ));
                // Meter-based UV0 and +Y winding on both triangles. No duplicate back face or collider.
                for (int i = first; i < first + 4; i++)
                { normals.Add(Vector3.up); uv.Add(new Vector2(vertices[i].x, vertices[i].z)); }
                triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
                triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 3);
            }
        }

        private static bool IdentityWorld(Transform parent)
        {
            Vector3 position = parent.position, scale = parent.lossyScale;
            Quaternion rotation = parent.rotation;
            if (!Finite(position.x) || !Finite(position.y) || !Finite(position.z) ||
                !Finite(rotation.x) || !Finite(rotation.y) || !Finite(rotation.z) || !Finite(rotation.w) ||
                !Finite(scale.x) || !Finite(scale.y) || !Finite(scale.z)) return false;
            // Also reject inherited shear/reflection. Distance and angle comparisons alone can accept NaN.
            Matrix4x4 world = parent.localToWorldMatrix;
            for (int row = 0; row < 4; row++) for (int column = 0; column < 4; column++)
            {
                float value = world[row, column], expected = row == column ? 1 : 0;
                if (!Finite(value) || Mathf.Abs(value - expected) > .000001f) return false;
            }
            return true;
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private void OnDestroy() { if (ownedMesh != null) Destroy(ownedMesh); ownedMesh = null; }
    }
}
