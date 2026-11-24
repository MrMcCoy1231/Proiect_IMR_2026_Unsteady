using UnityEditor;
using UnityEngine;

namespace Unsteady.EditorTools
{
    /// Setările de build pentru Quest: Android minim 12L (API 32), țintă Android 14 (API 34).
    /// Cu minimul 32, APK-ul se instalează și pe căștile care nu au Horizon OS v76+.
    public static class SetariBuildQuest
    {
        [MenuItem("Unsteady/Utilitare/Setări build Quest (Android minim API 32, țintă 34)")]
        static void Aplica()
        {
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)34;
            AssetDatabase.SaveAssets();
            string msg = $"Android: minim API {(int)PlayerSettings.Android.minSdkVersion}, țintă API {(int)PlayerSettings.Android.targetSdkVersion}.";
            Debug.Log("[Unsteady] " + msg);
            EditorUtility.DisplayDialog("Unsteady", msg, "OK");
        }
    }
}
