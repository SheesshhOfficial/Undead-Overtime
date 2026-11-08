using System;
using UndeadOvertime.Core;
using UnityEngine;

namespace UndeadOvertime.LanPrototype
{
    /// <summary>Original flat-depot objective geometry, not a scene activation or network authority.
    /// Host applies its current mission snapshot; clients apply only validated current-host replica state.
    /// Supplied materials remain caller-owned. Rendered architecture may load selected cabinet resources
    /// and own a cloned material; headless physics performs no asset loads. No file saves or shader lookup.</summary>
    public sealed class DepotMissionPresentation : IDisposable
    {
        public static AimVector PowerControl => new AimVector(-9, HostMoveGate.EyeHeight, -5);
        public static AimVector ExtractionCenter => new AimVector(0, HostMoveGate.EyeHeight, 12);
        public const double ExtractionHalfWidth = 2, ExtractionHalfDepth = 2;
        public const float PartitionZ = -2, GateWidth = 4;
        private readonly long sessionId, roundId;
        private GameObject gate;
        private bool disposed, hasSnapshot;
        public Transform Root { get; private set; }
        public bool RenderingEnabled { get; }
        public bool GateUnlocked { get; private set; }
        public bool ArchitecturalDetailsEnabled { get; }
        public CoopMissionPhase Phase { get; private set; }
        private bool Available => !disposed && Root != null;

        public DepotMissionPresentation(Transform parent, long sessionId, long roundId, bool enablePresentation,
            Material partitionMaterial = null, Material controlMaterial = null, Material extractionMaterial = null, Material concreteMaterial = null)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (!Application.isPlaying) throw new InvalidOperationException("Create mission geometry only during Play Mode.");
            if (sessionId <= 0) throw new ArgumentOutOfRangeException(nameof(sessionId));
            if (roundId <= 0) throw new ArgumentOutOfRangeException(nameof(roundId));
            // Coordinator positions are world meters; reject a transformed parent rather than silently offset physics.
            if (parent.position.sqrMagnitude > .000001f || Quaternion.Angle(parent.rotation, Quaternion.identity) > .001f ||
                (parent.lossyScale - Vector3.one).sqrMagnitude > .000001f)
                throw new ArgumentException("Mission layout requires an identity world-space parent.", nameof(parent));
            RenderingEnabled = enablePresentation && SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null;
            if (RenderingEnabled && (partitionMaterial == null || controlMaterial == null || extractionMaterial == null))
                throw new ArgumentException("Rendered mission geometry requires all three caller-owned materials.");
            ArchitecturalDetailsEnabled = RenderingEnabled && concreteMaterial != null;
            this.sessionId = sessionId; this.roundId = roundId; Phase = CoopMissionPhase.RestorePower;
            Root = new GameObject("Undead Overtime depot mission - prototype geometry").transform;
            Root.SetParent(parent, false);
            try
            {
                // Existing range walls close the outer ends at x=+/-16. A four-meter opening remains at center.
                GameObject west = Part("Mission west partition", new Vector3(-9, 1.5f, PartitionZ), new Vector3(14, 3, .4f), partitionMaterial, true);
                GameObject east = Part("Mission east partition", new Vector3(9, 1.5f, PartitionZ), new Vector3(14, 3, .4f), partitionMaterial, true);
                gate = Part("Mission route gate", new Vector3(0, 1.5f, PartitionZ), new Vector3(GateWidth, 3, .4f), partitionMaterial, true);
                if (ArchitecturalDetailsEnabled)
                {
                    DepotWallSurface.Apply(west, concreteMaterial); DepotWallSurface.Apply(east, concreteMaterial);
                    AddGateRelief(partitionMaterial, controlMaterial);
                }
                // Eye-level interaction point lies above and just in front of the physical control pedestal.
                GameObject console = Part("Mission power console", DepotConsolePresentation.CollisionCenter,
                    DepotConsolePresentation.CollisionSize, controlMaterial, true);
                if (ArchitecturalDetailsEnabled)
                {
                    DepotConsolePresentation.Create(Root, concreteMaterial);
                    console.GetComponent<Renderer>().enabled = false;
                }
                // This is a marker, never a second floor or blocking/trigger collision surface.
                Part("Mission extraction marker", new Vector3(0, .025f, 12), new Vector3(4, .025f, 4), extractionMaterial, false);
                Physics.SyncTransforms();
            }
            catch { Dispose(); throw; }
        }
        private GameObject Part(string name, Vector3 position, Vector3 scale, Material material, bool collision)
        {
            GameObject part;
            if (RenderingEnabled)
            {
                part = GameObject.CreatePrimitive(PrimitiveType.Cube);
                part.GetComponent<Renderer>().sharedMaterial = material;
                part.GetComponent<Collider>().enabled = collision;
            }
            else
            {
                part = new GameObject(name);
                if (collision) part.AddComponent<BoxCollider>().size = Vector3.one;
            }
            part.name = name; part.transform.SetParent(Root, false);
            part.transform.localPosition = position; part.transform.localScale = scale;
            return part;
        }
        private void AddGateRelief(Material steel, Material caution)
        {
            // A bounded painted-shutter treatment, not another collision object or a newly imported asset.
            // Children follow the existing gate's active state exactly. Maximum relief is 2 cm beyond its faces.
            Mesh cube = gate.GetComponent<MeshFilter>().sharedMesh;
            for (int side = -1; side <= 1; side += 2)
            {
                // Keep rib ends short of caution edges; overlapping coplanar color patches would z-fight.
                for (int row = 0; row < 8; row++)
                    GateDetail("Gate shutter rib " + side + ":" + row, new Vector3(0, .2f + row * .37f, PartitionZ + side * .205f),
                        new Vector3(3.64f, .065f, .03f), steel, cube);
                foreach (float x in new[] { -1.91f, 1.91f })
                    GateDetail("Gate caution edge " + side + ":" + x, new Vector3(x, 1.5f, PartitionZ + side * .205f),
                        new Vector3(.12f, 2.9f, .03f), caution, cube);
            }
        }
        private void GateDetail(string name, Vector3 position, Vector3 scale, Material material, Mesh sharedCube)
        {
            // Do not CreatePrimitive here: purely visual relief must never acquire even a transient collider.
            var part = new GameObject(name); part.transform.SetParent(Root, false);
            part.transform.localPosition = position; part.transform.localScale = scale;
            part.AddComponent<MeshFilter>().sharedMesh = sharedCube;
            part.AddComponent<MeshRenderer>().sharedMaterial = material;
            part.transform.SetParent(gate.transform, true);
        }
        /// <summary>Apply only same-round authoritative/view-validated state. A late frame cannot relock the gate
        /// or regress an objective. Network frame sequence/sender checks belong to CoopMissionReplica/transport.</summary>
        public bool TryApply(CoopMissionSnapshot state)
        {
            if (!Available || !state.IsCurrent || state.SessionId != sessionId || state.RoundId != roundId || !ValidState(state)) return false;
            if (hasSnapshot)
            {
                if (GateUnlocked && !state.DepotGateUnlocked) return false;
                if ((Phase == CoopMissionPhase.Completed || Phase == CoopMissionPhase.Failed) &&
                    (state.Phase != Phase || state.DepotGateUnlocked != GateUnlocked)) return false;
                if (state.Phase != CoopMissionPhase.Failed && (byte)state.Phase < (byte)Phase) return false;
            }
            hasSnapshot = true; Phase = state.Phase; GateUnlocked = state.DepotGateUnlocked;
            gate.SetActive(!GateUnlocked); Physics.SyncTransforms(); return true;
        }
        private static bool ValidState(CoopMissionSnapshot state)
        {
            if (!AimVector.Finite(state.HoldSeconds) || state.HoldSeconds < 0) return false;
            switch (state.Phase)
            {
                case CoopMissionPhase.RestorePower: return !state.DepotGateUnlocked && state.HoldSeconds <= CoopMissionProgression.PowerHoldDuration;
                case CoopMissionPhase.DefendDepot: return state.DepotGateUnlocked && state.HoldSeconds == 0;
                case CoopMissionPhase.ReachExtraction: return state.DepotGateUnlocked && state.HoldSeconds <= CoopMissionProgression.ExtractionHoldDuration;
                case CoopMissionPhase.Completed: return state.DepotGateUnlocked && state.HoldSeconds == 0;
                case CoopMissionPhase.Failed: return state.HoldSeconds == 0;
                default: return false;
            }
        }
        public bool HasPowerLine(AimVector eye)
        {
            if (!Available || !HostMoveGate.ValidPosition(eye) || (eye - PowerControl).LengthSquared > HostCoopMission.PowerRange * HostCoopMission.PowerRange) return false;
            Physics.SyncTransforms();
            return !Physics.Linecast(ToUnity(eye), ToUnity(PowerControl), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }
        public bool ContainsExtraction(AimVector eye) => Available && HostMoveGate.ValidPosition(eye) &&
            Math.Abs(eye.X - ExtractionCenter.X) <= ExtractionHalfWidth && Math.Abs(eye.Z - ExtractionCenter.Z) <= ExtractionHalfDepth;
        private static Vector3 ToUnity(AimVector value) => new Vector3((float)value.X, (float)value.Y, (float)value.Z);
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (Root != null)
            {
                // Disable collision immediately; Unity destroys owned objects at the end of the frame.
                Root.gameObject.SetActive(false); Physics.SyncTransforms();
                UnityEngine.Object.Destroy(Root.gameObject);
            }
            Root = null; gate = null;
        }
    }
}
