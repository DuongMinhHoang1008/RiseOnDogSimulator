using System.Runtime.InteropServices;
using UnityEngine;

namespace DogSimulator.Networking
{
    public static class WebClipboard
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int DogSimulator_CopyText(string text);
#endif

        public static bool Copy(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
#if UNITY_WEBGL && !UNITY_EDITOR
            return DogSimulator_CopyText(text) != 0;
#else
            GUIUtility.systemCopyBuffer = text;
            return true;
#endif
        }
    }
}
