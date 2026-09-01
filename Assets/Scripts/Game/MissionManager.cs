using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zoologic
{
    public enum MissionType
    {
        PlaceAnimals = 0,
        CompleteLevels = 1,
        UseHints = 2,
        EarnStars = 3,
        UseEraser = 4
    }

    [Serializable]
    public class MissionData
    {
        public MissionType type;
        public int target;
        public int progress;
        public int reward;
        public bool claimed;

        public bool IsCompleted => progress >= target;
        public string Label
        {
            get
            {
                switch (type)
                {
                    case MissionType.PlaceAnimals: return $"Place {target} animaux";
                    case MissionType.CompleteLevels: return $"Termine {target} niveaux";
                    case MissionType.UseHints: return $"Utilise {target} indices";
                    case MissionType.EarnStars: return $"Gagne {target} étoiles";
                    case MissionType.UseEraser: return $"Utilise la gomme {target} fois";
                    default: return "Mission";
                }
            }
        }
    }

    public static class MissionManager
    {
        private const string CountKey = "mission_count";
        private const string Prefix = "mission_";
        private const int SlotCount = 3;

        private static readonly MissionType[] Pool = (MissionType[])Enum.GetValues(typeof(MissionType));

        public static List<MissionData> GetMissions()
        {
            int count = PlayerPrefs.GetInt(CountKey, 0);
            if (count == 0)
            {
                GenerateNewSet();
                count = SlotCount;
            }
            var list = new List<MissionData>();
            for (int i = 0; i < count; i++)
            {
                string json = PlayerPrefs.GetString(Prefix + i, "");
                if (string.IsNullOrEmpty(json)) continue;
                try { list.Add(JsonUtility.FromJson<MissionData>(json)); }
                catch { }
            }
            if (list.Count == 0)
            {
                GenerateNewSet();
                for (int i = 0; i < SlotCount; i++)
                {
                    string json = PlayerPrefs.GetString(Prefix + i, "");
                    if (!string.IsNullOrEmpty(json))
                        list.Add(JsonUtility.FromJson<MissionData>(json));
                }
            }
            return list;
        }

        public static void GenerateNewSet()
        {
            var rnd = new System.Random();
            var used = new HashSet<int>();
            for (int i = 0; i < SlotCount; i++)
            {
                MissionType t;
                int tries = 0;
                do { t = Pool[rnd.Next(Pool.Length)]; tries++; } while (used.Contains((int)t) && tries < 20);
                used.Add((int)t);
                var m = new MissionData
                {
                    type = t,
                    target = TargetFor(t),
                    progress = 0,
                    reward = RewardFor(t),
                    claimed = false
                };
                PlayerPrefs.SetString(Prefix + i, JsonUtility.ToJson(m));
            }
            PlayerPrefs.SetInt(CountKey, SlotCount);
            PlayerPrefs.Save();
        }

        private static int TargetFor(MissionType t)
        {
            switch (t)
            {
                case MissionType.PlaceAnimals: return 8;
                case MissionType.CompleteLevels: return 2;
                case MissionType.UseHints: return 2;
                case MissionType.EarnStars: return 4;
                case MissionType.UseEraser: return 2;
                default: return 3;
            }
        }

        private static int RewardFor(MissionType t)
        {
            switch (t)
            {
                case MissionType.CompleteLevels: return 40;
                case MissionType.EarnStars: return 40;
                default: return 30;
            }
        }

        public static void AddProgress(MissionType type, int amount = 1)
        {
            var list = GetMissions();
            bool changed = false;
            for (int i = 0; i < list.Count; i++)
            {
                var m = list[i];
                if (m.claimed || m.IsCompleted) continue;
                if (m.type != type) continue;
                m.progress = Mathf.Min(m.target, m.progress + amount);
                PlayerPrefs.SetString(Prefix + i, JsonUtility.ToJson(m));
                changed = true;
            }
            if (changed) PlayerPrefs.Save();
        }

        public static bool TryClaim(int index)
        {
            string json = PlayerPrefs.GetString(Prefix + index, "");
            if (string.IsNullOrEmpty(json)) return false;
            var m = JsonUtility.FromJson<MissionData>(json);
            if (m.claimed || !m.IsCompleted) return false;
            CurrencyManager.AddCoins(m.reward);
            m.claimed = true;
            PlayerPrefs.SetString(Prefix + index, JsonUtility.ToJson(m));
            PlayerPrefs.Save();
            CheckRegenerate();
            return true;
        }

        private static void CheckRegenerate()
        {
            var list = GetMissions();
            bool allClaimed = true;
            foreach (var m in list) if (!m.claimed) { allClaimed = false; break; }
            if (allClaimed) GenerateNewSet();
            else
            {
                for (int i = 0; i < list.Count; i++)
                {
                    var m = list[i];
                    if (m.claimed && m.IsCompleted)
                    {
                        var rnd = new System.Random();
                        MissionType t = Pool[rnd.Next(Pool.Length)];
                        var nm = new MissionData { type = t, target = TargetFor(t), progress = 0, reward = RewardFor(t), claimed = false };
                        PlayerPrefs.SetString(Prefix + i, JsonUtility.ToJson(nm));
                        PlayerPrefs.Save();
                        break;
                    }
                }
            }
        }

        public static int GetCompletedCount()
        {
            int c = 0;
            foreach (var m in GetMissions()) if (m.IsCompleted && !m.claimed) c++;
            return c;
        }

        public static void DebugReset()
        {
            PlayerPrefs.DeleteKey(CountKey);
            for (int i = 0; i < SlotCount; i++) PlayerPrefs.DeleteKey(Prefix + i);
            PlayerPrefs.Save();
        }
    }
}
