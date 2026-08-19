using System;
using UnityEngine;

namespace Zoodoku
{
    public static class Easing
    {
        public static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;

            float x = t - 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }

        public static float EaseOutCubic(float t)
            => 1f - Mathf.Pow(1f - t, 3f);
    }

    public static class Haptics
    {
        private const string HapticsEnabledKey = "haptics_enabled";

        private static bool _isEnabled = PlayerPrefs.GetInt(HapticsEnabledKey, 1) == 1;

        public static bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                _isEnabled = value;
                PlayerPrefs.SetInt(HapticsEnabledKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static void VibrateLight() => Vibrate(50);

        public static void VibrateStrong() => Vibrate(350);

        private static void Vibrate(int milliseconds)
        {
            if (!_isEnabled || !Application.isMobilePlatform)
                return;

            try
            {
                if (Application.platform == RuntimePlatform.Android && milliseconds > 0)
                    VibrateAndroid(milliseconds);
                else
                    Handheld.Vibrate();
            }
            catch (Exception)
            {
            }
        }

        private static void VibrateAndroid(int milliseconds)
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
            {
                vibrator?.Call("vibrate", (long)milliseconds);
            }
        }
    }
}
