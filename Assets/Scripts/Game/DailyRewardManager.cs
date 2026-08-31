using System;
using UnityEngine;

namespace Zoologic
{
    public static class DailyRewardManager
    {
        private const string LastClaimKey = "daily_last_claim";
        private const string StreakKey = "daily_streak";
        private const string LastStreakResetKey = "daily_last_streak_reset";

        private static readonly int[] CoinRewards = { 20, 30, 40, 50, 60, 80, 100 };

        public static int GetStreak()
        {
            return PlayerPrefs.GetInt(StreakKey, 0);
        }

        public static string GetLastClaimDate()
        {
            return PlayerPrefs.GetString(LastClaimKey, "");
        }

        public static int GetRewardForDay(int day)
        {
            int idx = Mathf.Clamp(day - 1, 0, CoinRewards.Length - 1);
            return CoinRewards[idx];
        }

        public static int GetTodayReward()
        {
            int streak = GetStreak();
            int day = streak >= 7 ? 7 : streak + 1;
            if (CanClaimToday()) return GetRewardForDay(day);
            int claimedDay = Mathf.Clamp(streak, 1, 7);
            return GetRewardForDay(claimedDay);
        }

        public static bool CanClaimToday()
        {
            string last = GetLastClaimDate();
            string today = TodayString();
            if (string.IsNullOrEmpty(last)) return true;
            return last != today;
        }

        public static bool IsStreakContinued()
        {
            string last = GetLastClaimDate();
            if (string.IsNullOrEmpty(last)) return true;
            if (DateTime.TryParse(last, out DateTime lastDate))
            {
                DateTime today = DateTime.Today;
                int diff = (today - lastDate.Date).Days;
                return diff == 1;
            }
            return false;
        }

        public static int Claim()
        {
            if (!CanClaimToday()) return 0;

            string last = GetLastClaimDate();
            string today = TodayString();
            int streak = GetStreak();

            if (string.IsNullOrEmpty(last))
            {
                streak = 1;
            }
            else if (IsStreakContinued())
            {
                streak = Mathf.Min(streak + 1, 7);
                if (streak > 7) streak = 1;
            }
            else
            {
                DateTime lastDate = DateTime.Parse(last);
                int diff = (DateTime.Today - lastDate.Date).Days;
                if (diff > 1)
                    streak = 1;
                else if (diff == 0)
                    return 0;
                else
                    streak = Mathf.Min(streak + 1, 7);
            }

            if (GetLastClaimDate() == today) return 0;

            int reward = GetRewardForDay(streak);
            CurrencyManager.AddCoins(reward);
            PlayerPrefs.SetString(LastClaimKey, today);
            PlayerPrefs.SetInt(StreakKey, streak);
            PlayerPrefs.Save();
            return reward;
        }

        public static int ClaimDoubled()
        {
            int baseReward = Claim();
            if (baseReward == 0) return 0;
            CurrencyManager.AddCoins(baseReward);
            return baseReward * 2;
        }

        public static void DebugReset()
        {
            PlayerPrefs.DeleteKey(LastClaimKey);
            PlayerPrefs.DeleteKey(StreakKey);
            PlayerPrefs.Save();
        }

        private static string TodayString()
        {
            return DateTime.Today.ToString("yyyy-MM-dd");
        }
    }
}
