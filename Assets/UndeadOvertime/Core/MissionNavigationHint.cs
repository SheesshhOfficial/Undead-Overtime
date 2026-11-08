using System;

namespace UndeadOvertime.Core
{
    public enum MissionNavigationGoal { None, PowerConsole, Extraction }
    public enum NavigationBearing { Ahead, AheadRight, Right, BehindRight, Behind, BehindLeft, Left, AheadLeft, Nearby }

    /// <summary>Presentation-only planar waypoint relative to the viewer's horizontal facing.
    /// Distance/bearing never proves interaction range, line of sight, eligibility or a traversable route.</summary>
    public readonly struct MissionNavigationHint
    {
        public readonly MissionNavigationGoal Goal;
        public readonly NavigationBearing Bearing;
        public readonly double DistanceMeters;
        private MissionNavigationHint(MissionNavigationGoal goal, NavigationBearing bearing, double distance)
        { Goal = goal; Bearing = bearing; DistanceMeters = distance; }

        public string GoalText => Goal == MissionNavigationGoal.PowerConsole ? "Power console" : Goal == MissionNavigationGoal.Extraction ? "Extraction" : "";
        public string BearingText
        {
            get
            {
                switch (Bearing)
                {
                    case NavigationBearing.Ahead: return "ahead";
                    case NavigationBearing.AheadRight: return "ahead-right";
                    case NavigationBearing.Right: return "right";
                    case NavigationBearing.BehindRight: return "behind-right";
                    case NavigationBearing.Behind: return "behind";
                    case NavigationBearing.BehindLeft: return "behind-left";
                    case NavigationBearing.Left: return "left";
                    case NavigationBearing.AheadLeft: return "ahead-left";
                    default: return "nearby";
                }
            }
        }

        public static bool TryCreate(CoopMissionSnapshot mission, AimVector eye, AimVector viewForward,
            AimVector powerControl, AimVector extractionCenter, out MissionNavigationHint hint)
        {
            hint = default;
            if (!mission.IsCurrent || mission.SessionId <= 0 || mission.RoundId <= 0 ||
                !HostMoveGate.ValidPosition(eye) || !HostMoveGate.ValidPosition(powerControl) || !HostMoveGate.ValidPosition(extractionCenter) ||
                !viewForward.IsFinite || !AimVector.Finite(mission.HoldSeconds) || mission.HoldSeconds < 0) return false;
            MissionNavigationGoal goal;
            AimVector target;
            if (mission.Phase == CoopMissionPhase.RestorePower && !mission.DepotGateUnlocked && mission.HoldSeconds <= CoopMissionProgression.PowerHoldDuration)
            { goal = MissionNavigationGoal.PowerConsole; target = powerControl; }
            else if (mission.Phase == CoopMissionPhase.ReachExtraction && mission.DepotGateUnlocked && mission.HoldSeconds <= CoopMissionProgression.ExtractionHoldDuration)
            { goal = MissionNavigationGoal.Extraction; target = extractionCenter; }
            else return false;

            double lengthSquared = viewForward.X * viewForward.X + viewForward.Z * viewForward.Z;
            if (!AimVector.Finite(lengthSquared) || lengthSquared < 1e-8) return false;
            double inverse = 1 / Math.Sqrt(lengthSquared), fx = viewForward.X * inverse, fz = viewForward.Z * inverse;
            double dx = target.X - eye.X, dz = target.Z - eye.Z;
            double distance = Math.Sqrt(dx * dx + dz * dz);
            NavigationBearing bearing = NavigationBearing.Nearby;
            if (distance > .5)
            {
                double angle = Math.Atan2(dx * fz - dz * fx, dx * fx + dz * fz) * (180 / Math.PI);
                int sector = ((int)Math.Floor((angle + 22.5) / 45) + 8) % 8;
                bearing = (NavigationBearing)sector;
            }
            hint = new MissionNavigationHint(goal, bearing, distance); return true;
        }
    }
}
