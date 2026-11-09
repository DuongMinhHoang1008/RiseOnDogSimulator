#if UNITY_STANDALONE_WIN && !UNITY_EDITOR && !UNITY_SERVER
using UnityEngine;

namespace DogSimulator.Player
{
    internal static class WindowsPlayerStartup
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void ApplyFullscreen()
        {
            // Restore the fullscreen default if Unity remembered a windowed session.
            if (Screen.fullScreenMode != FullScreenMode.FullScreenWindow)
                Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
        }
    }
}
#endif
