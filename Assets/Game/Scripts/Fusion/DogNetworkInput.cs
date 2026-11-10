using DogSimulator.Player;
using Fusion;
using UnityEngine;

namespace DogSimulator.Networking
{
    [System.Flags]
    public enum DogInputButtons : byte
    {
        None = 0,
        Jump = 1 << 0,
        PrimaryCarry = 1 << 1,
        DropCarry = 1 << 2,
        ResetYard = 1 << 3,
        RespawnBone = 1 << 4,
        ResetMission = 1 << 5
    }

    public struct DogNetworkInput : INetworkInput
    {
        public Vector2 Move;
        public float Yaw;
        public byte Sprint;
        public DogAction Action;
        public DogInputButtons Buttons;

        public bool IsPressed(DogInputButtons button) => (Buttons & button) != 0;
    }
}
