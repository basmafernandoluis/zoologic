using UnityEngine;

namespace Zoologic
{
    public static class LevelProgressManager
    {
        private const string HighestUnlockedKey = "highest_unlocked";
        private const string StarsPrefix = "stars_level_";

        public static int GetHighestUnlockedLevel()
        {
            return PlayerPrefs.GetInt(HighestUnlockedKey, 1);
        }

        public static void UnlockNextLevel(int completedLevel)
        {
            int current = GetHighestUnlockedLevel();
            if (completedLevel >= current)
            {
                PlayerPrefs.SetInt(HighestUnlockedKey, completedLevel + 1);
                PlayerPrefs.Save();
            }
        }

        public static int GetStars(int level)
        {
            return PlayerPrefs.GetInt(StarsPrefix + level, 0);
        }

        public static void SetStars(int level, int stars)
        {
            int current = GetStars(level);
            if (stars > current)
            {
                PlayerPrefs.SetInt(StarsPrefix + level, stars);
                PlayerPrefs.Save();
            }
        }

        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(HighestUnlockedKey);

            for (int i = 1; i <= 1000; i++)
                PlayerPrefs.DeleteKey(StarsPrefix + i);

            PlayerPrefs.Save();
        }
    }
}
