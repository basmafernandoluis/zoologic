using System;
using UnityEngine;

namespace Zoologic
{
    public sealed class LivesManager
    {
        public const int ViesDepart = 3;
        public const int MaxVies = 3;
        public const int RegenSeconds = 900;

        private const string LivesKey = "player_lives";
        private const string LastLostKey = "lives_last_lost_ticks";

        private int _vies;

        public int Vies
        {
            get
            {
                RefreshFromStorage();
                return _vies;
            }
        }

        public Action OnPartiePerdue;

        public LivesManager()
        {
            RefreshFromStorage();
        }

        private void RefreshFromStorage()
        {
            int stored = PlayerPrefs.GetInt(LivesKey, ViesDepart);
            if (stored >= MaxVies)
            {
                _vies = MaxVies;
                return;
            }

            string lastStr = PlayerPrefs.GetString(LastLostKey, "");
            if (string.IsNullOrEmpty(lastStr) || !long.TryParse(lastStr, out long ticks))
            {
                _vies = stored;
                return;
            }

            DateTime last = new DateTime(ticks, DateTimeKind.Local);
            TimeSpan elapsed = DateTime.Now - last;
            int regen = (int)(elapsed.TotalSeconds / RegenSeconds);
            if (regen <= 0)
            {
                _vies = stored;
                return;
            }

            int newVies = Mathf.Min(MaxVies, stored + regen);
            PlayerPrefs.SetInt(LivesKey, newVies);
            if (newVies >= MaxVies)
            {
                PlayerPrefs.DeleteKey(LastLostKey);
            }
            else
            {
                DateTime newLast = last.AddSeconds(regen * RegenSeconds);
                PlayerPrefs.SetString(LastLostKey, newLast.Ticks.ToString());
            }
            PlayerPrefs.Save();
            _vies = newVies;
        }

        public bool PerdreVie()
        {
            RefreshFromStorage();
            if (_vies <= 0) return false;

            _vies--;
            PlayerPrefs.SetInt(LivesKey, _vies);
            if (_vies < MaxVies && !PlayerPrefs.HasKey(LastLostKey))
                PlayerPrefs.SetString(LastLostKey, DateTime.Now.Ticks.ToString());
            if (_vies < MaxVies && PlayerPrefs.GetInt(LivesKey, MaxVies) == MaxVies)
                PlayerPrefs.SetString(LastLostKey, DateTime.Now.Ticks.ToString());
            PlayerPrefs.Save();

            if (_vies <= 0)
                OnPartiePerdue?.Invoke();

            return true;
        }

        public void Reinitialiser()
        {
            _vies = MaxVies;
            PlayerPrefs.SetInt(LivesKey, _vies);
            PlayerPrefs.DeleteKey(LastLostKey);
            PlayerPrefs.Save();
        }

        public void AjouterVies(int count)
        {
            RefreshFromStorage();
            _vies = Mathf.Min(MaxVies, _vies + count);
            PlayerPrefs.SetInt(LivesKey, _vies);
            if (_vies >= MaxVies)
                PlayerPrefs.DeleteKey(LastLostKey);
            PlayerPrefs.Save();
        }

        public static int GetStoredLives()
        {
            int stored = PlayerPrefs.GetInt(LivesKey, ViesDepart);
            string lastStr = PlayerPrefs.GetString(LastLostKey, "");
            if (stored >= MaxVies || string.IsNullOrEmpty(lastStr) || !long.TryParse(lastStr, out long ticks))
                return Mathf.Clamp(stored, 0, MaxVies);

            DateTime last = new DateTime(ticks, DateTimeKind.Local);
            int regen = (int)((DateTime.Now - last).TotalSeconds / RegenSeconds);
            return Mathf.Clamp(stored + regen, 0, MaxVies);
        }

        public static int GetSecondsUntilNextLife()
        {
            int stored = PlayerPrefs.GetInt(LivesKey, ViesDepart);
            if (stored >= MaxVies) return 0;
            string lastStr = PlayerPrefs.GetString(LastLostKey, "");
            if (string.IsNullOrEmpty(lastStr) || !long.TryParse(lastStr, out long ticks)) return RegenSeconds;
            DateTime last = new DateTime(ticks, DateTimeKind.Local);
            double elapsed = (DateTime.Now - last).TotalSeconds;
            int remaining = RegenSeconds - (int)(elapsed % RegenSeconds);
            return Mathf.Clamp(remaining, 0, RegenSeconds);
        }

        public static void DebugReset()
        {
            PlayerPrefs.SetInt(LivesKey, MaxVies);
            PlayerPrefs.DeleteKey(LastLostKey);
            PlayerPrefs.Save();
        }
    }
}
