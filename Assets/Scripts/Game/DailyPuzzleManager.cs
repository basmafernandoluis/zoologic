using System;
using UnityEngine;

namespace Zoologic
{
    public static class DailyPuzzleManager
    {
        private const string CompletedKeyPrefix = "daily_puzzle_done_";
        public const int RewardCoins = 50;

        public static int GetTodaySeed()
        {
            DateTime d = DateTime.Today;
            return d.Year * 10000 + d.Month * 100 + d.Day;
        }

        public static string TodayKey()
        {
            return CompletedKeyPrefix + DateTime.Today.ToString("yyyy_MM_dd");
        }

        public static bool IsCompletedToday()
        {
            return PlayerPrefs.GetInt(TodayKey(), 0) == 1;
        }

        public static void MarkCompletedToday()
        {
            PlayerPrefs.SetInt(TodayKey(), 1);
            PlayerPrefs.Save();
        }

        public static int GetTodaySize()
        {
            return 5;
        }

        public static void DebugReset()
        {
            PlayerPrefs.DeleteKey(TodayKey());
            PlayerPrefs.Save();
        }
    }
}
