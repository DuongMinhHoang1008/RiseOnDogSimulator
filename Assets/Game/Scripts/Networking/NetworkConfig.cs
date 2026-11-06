using UnityEngine;

namespace DogSimulator.Networking
{
    [CreateAssetMenu(menuName = "Dog Simulator/Network Config")]
    public sealed class NetworkConfig : ScriptableObject
    {
        [SerializeField] private string region = "asia";
        [SerializeField] private string gameVersion = "office-m1-v1";
        [SerializeField, Range(2, 20)] private int maxPlayers = 8;
        [SerializeField, Range(1, 5)] private int createAttempts = 3;
        [SerializeField, Range(5, 60)] private int connectionTimeoutSeconds = 20;

        public string Region => region;
        public string GameVersion => gameVersion;
        public int MaxPlayers => maxPlayers;
        public int CreateAttempts => createAttempts;
        public int ConnectionTimeoutSeconds => connectionTimeoutSeconds;

        public bool IsValid => !string.IsNullOrWhiteSpace(region)
            && !string.IsNullOrWhiteSpace(gameVersion) && maxPlayers >= 2 && maxPlayers <= 20
            && createAttempts >= 1 && createAttempts <= 5
            && connectionTimeoutSeconds >= 5 && connectionTimeoutSeconds <= 60;

        private void OnValidate()
        {
            region = (region ?? string.Empty).Trim().ToLowerInvariant();
            gameVersion = (gameVersion ?? string.Empty).Trim();
            maxPlayers = Mathf.Clamp(maxPlayers, 2, 20);
            createAttempts = Mathf.Clamp(createAttempts, 1, 5);
            connectionTimeoutSeconds = Mathf.Clamp(connectionTimeoutSeconds, 5, 60);
        }
    }
}
