using Fusion;
using UnityEngine;

namespace DogSimulator.Networking
{
    public sealed class FusionRunnerRoot : MonoBehaviour
    {
        [SerializeField] private NetworkRunner runner;
        [SerializeField] private NetworkEvents events;
        [SerializeField] private NetworkSceneManagerDefault sceneManager;
        [SerializeField] private NetworkObjectProviderDefault objectProvider;
        [SerializeField] private CustomDogFaceNetworkService faceService;

        internal NetworkRunner Runner => runner;
        internal NetworkEvents Events => events;
        internal NetworkSceneManagerDefault SceneManager => sceneManager;
        internal NetworkObjectProviderDefault ObjectProvider => objectProvider;
        internal CustomDogFaceNetworkService FaceService => faceService;
    }
}
