using UnityEngine;

namespace DeliveryDash
{
    /// <summary>Player settings and personal bests, kept in PlayerPrefs (works in browsers too).</summary>
    public static class Settings
    {
        public static float Music { get => PlayerPrefs.GetFloat("dd.music", 0.55f); set => PlayerPrefs.SetFloat("dd.music", value); }
        public static float Sound { get => PlayerPrefs.GetFloat("dd.sound", 0.85f); set => PlayerPrefs.SetFloat("dd.sound", value); }
        /// <summary>0 Low, 1 Medium, 2 High.</summary>
        public static int Quality { get => PlayerPrefs.GetInt("dd.quality", DefaultQuality); set => PlayerPrefs.SetInt("dd.quality", value); }
        public static bool CameraShake { get => PlayerPrefs.GetInt("dd.shake", 1) == 1; set => PlayerPrefs.SetInt("dd.shake", value ? 1 : 0); }
        public static bool TutorialDone { get => PlayerPrefs.GetInt("dd.tutorial", 0) == 1; set => PlayerPrefs.SetInt("dd.tutorial", value ? 1 : 0); }
        public static int BestScore { get => PlayerPrefs.GetInt("dd.best", 0); set => PlayerPrefs.SetInt("dd.best", value); }
        public static float BestTime { get => PlayerPrefs.GetFloat("dd.bestTime", 0f); set => PlayerPrefs.SetFloat("dd.bestTime", value); }
        public static string PlayerName { get => PlayerPrefs.GetString("dd.name", ""); set => PlayerPrefs.SetString("dd.name", value); }

        static int DefaultQuality => Application.platform == RuntimePlatform.WebGLPlayer ? 1 : 2;
        public static readonly string[] QualityNames = { "Low", "Medium", "High" };

        public static void Save() => PlayerPrefs.Save();
    }
}
