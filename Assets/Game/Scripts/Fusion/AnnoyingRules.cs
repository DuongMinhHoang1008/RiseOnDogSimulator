using UnityEngine;

namespace DogSimulator.Networking
{
    internal enum AnnoyingAction : byte { Bark, Bite, ThrownProp, Ram, DrivenVehicle, AttackPolice }

    internal static class AnnoyingRules
    {
        internal const float HeatDelay = 10f;
        internal const float DecayPerSecond = 5f;
        internal const float PairCooldown = 2f;
        internal const float BarkWindow = 6f;
        internal const int BarksToAnnoy = 3;
        internal const int PoliceCount = 2;
        internal const float SearchSeconds = 10f;
        internal const float AssignmentSeconds = 18f;
        internal const float DispatchRotationSeconds = 7f;
        internal const float ArrestSeconds = .85f;
        internal const float GateSeconds = 7f;
        internal const float EscapeGrace = 18f;
        internal const int SightLayers = Physics.DefaultRaycastLayers | (1 << 2);

        internal static int Points(AnnoyingAction action) => action switch
        {
            AnnoyingAction.Bark => 3,
            AnnoyingAction.Bite => 12,
            AnnoyingAction.ThrownProp => 15,
            AnnoyingAction.Ram => 20,
            AnnoyingAction.DrivenVehicle => 25,
            AnnoyingAction.AttackPolice => 20,
            _ => 0
        };

        internal static byte Level(float heat, byte previous)
        {
            if (heat >= (previous >= 3 ? 70 : 75)) return 3;
            if (heat >= (previous >= 2 ? 40 : 45)) return 2;
            return (byte)(heat >= (previous >= 1 ? 15 : 20) ? 1 : 0);
        }
    }
}
