using System;
using UnityEngine;

namespace Zoologic
{
    public static class DailyPuzzleManager
    {
        private const string CompletedKeyPrefix = "daily_puzzle_done_";
        private const string PuzzleStreakKey = "daily_puzzle_streak";
        private const string PuzzleLastKey = "daily_puzzle_last";
        public const int RewardCoins = 20;

        /// <summary>Bonus de série : +5 par jour consécutif (max +25).</summary>
        public const int StreakBonusPerDay = 5;
        public const int StreakBonusMax = 25;

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

        /// <summary>Série de défis réussis (jours consécutifs).</summary>
        public static int GetPuzzleStreak()
        {
            return PlayerPrefs.GetInt(PuzzleStreakKey, 0);
        }

        /// <summary>Bonus courant en pièces selon la série (0 si défi du jour non fait).</summary>
        public static int GetStreakBonus()
        {
            if (!IsCompletedToday())
                return 0;
            return Mathf.Min(Mathf.Max(0, GetPuzzleStreak() - 1) * StreakBonusPerDay, StreakBonusMax);
        }

        /// <summary>Bonus affiché AVANT de jouer (série en cours +1 si défi non fait).</summary>
        public static int GetUpcomingBonus()
        {
            int projected = IsCompletedToday() ? GetPuzzleStreak() : GetPuzzleStreak() + 1;
            if (!IsCompletedToday() && !IsStreakAlive())
                projected = 1;
            return Mathf.Min(Mathf.Max(0, projected - 1) * StreakBonusPerDay, StreakBonusMax);
        }

        /// <summary>La série survit-elle jusqu'à aujourd'hui (hier fait, aujourd'hui à faire) ?</summary>
        public static bool IsStreakAlive()
        {
            string last = PlayerPrefs.GetString(PuzzleLastKey, "");
            if (string.IsNullOrEmpty(last))
                return GetPuzzleStreak() == 0;
            if (DateTime.TryParse(last, out DateTime lastDate))
            {
                int diff = (DateTime.Today - lastDate.Date).Days;
                return diff <= 1;
            }
            return false;
        }

        public static void MarkCompletedToday()
        {
            PlayerPrefs.SetInt(TodayKey(), 1);
            string last = PlayerPrefs.GetString(PuzzleLastKey, "");
            int streak = GetPuzzleStreak();
            if (string.IsNullOrEmpty(last))
            {
                streak = 1;
            }
            else if (DateTime.TryParse(last, out DateTime lastDate))
            {
                int diff = (DateTime.Today - lastDate.Date).Days;
                if (diff == 0)
                {
                    // Déjà fait aujourd'hui : série inchangée.
                }
                else if (diff == 1)
                {
                    streak = streak + 1;
                }
                else
                {
                    streak = 1;
                }
            }
            else
            {
                streak = 1;
            }
            PlayerPrefs.SetString(PuzzleLastKey, DateTime.Today.ToString("yyyy-MM-dd"));
            PlayerPrefs.SetInt(PuzzleStreakKey, streak);
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
