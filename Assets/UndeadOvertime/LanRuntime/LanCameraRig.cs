using System;
using UndeadOvertime.Core;
using UnityEngine;

namespace UndeadOvertime.LanPrototype
{
    public enum PlayerViewMode { FirstPerson, ThirdPerson }

    /// <summary>Presentation-only camera placement and crosshair convergence. Never moves the authoritative player.
    /// Host raycasts still originate from the host's player position and can hit cover before the chosen aim point.</summary>
    public static class LanCameraRig
    {
        public const float CameraRadius = .18f, CollisionSkin = .03f;
        public static Vector3 ShoulderOffset => new Vector3(.55f, .4f, -3f);
        private static bool Finite(Vector3 value) => AimVector.Finite(value.x) && AimVector.Finite(value.y) && AimVector.Finite(value.z);
        private static bool ValidRotation(Quaternion value)
        {
            float length = value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w;
            return AimVector.Finite(length) && Mathf.Abs(length - 1) < .001f;
        }
        public static bool ShowLocalBody(PlayerViewMode mode, Vector3 eye, Vector3 cameraPosition) =>
            mode == PlayerViewMode.ThirdPerson && Finite(eye) && Finite(cameraPosition) && (cameraPosition - eye).sqrMagnitude >= 1.5f * 1.5f;
        public static Vector3 ResolvePosition(Vector3 eye, Quaternion look, PlayerViewMode mode)
        {
            if (!Finite(eye) || !ValidRotation(look) || (mode != PlayerViewMode.FirstPerson && mode != PlayerViewMode.ThirdPerson))
                throw new ArgumentException("Finite eye, unit rotation and a supported view mode are required.");
            if (mode == PlayerViewMode.FirstPerson) return eye;
            Vector3 offset = look * ShoulderOffset; float distance = offset.magnitude; Vector3 direction = offset / distance;
            if (Physics.SphereCast(eye, CameraRadius, direction, out RaycastHit hit, distance + CollisionSkin,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return eye + direction * Mathf.Clamp(hit.distance - CollisionSkin, 0, distance);
            return eye + offset;
        }
        public static Vector3 ResolveShotDirection(Vector3 authoritativeEye, Vector3 cameraPosition, Quaternion look)
        {
            if (!Finite(authoritativeEye) || !Finite(cameraPosition) || !ValidRotation(look)) throw new ArgumentException("Finite view data is required.");
            Vector3 forward = look * Vector3.forward;
            Vector3 point = cameraPosition + forward * (float)HostFireGate.Range;
            if (Physics.Raycast(cameraPosition, forward, out RaycastHit hit, (float)HostFireGate.Range,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) point = hit.point;
            // A camera obstruction behind the muzzle is not a valid backwards shooting target.
            if (Vector3.Dot(point - authoritativeEye, forward) <= .1f) point = authoritativeEye + forward * (float)HostFireGate.Range;
            return (point - authoritativeEye).normalized;
        }
    }
}
