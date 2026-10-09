using System.Collections.Generic;
using UnityEngine;

namespace Zoologic
{
    /// <summary>
    /// Coffres de progression par monde (4 mondes x 25 niveaux), paliers 10/25/50⭐.
    /// P1 : 30 pièces + 1 indice. P2 : 60 + 1 indice + mascotte (70% C / 30% R).
    /// P3 : 100 + 2 indices + mascotte (70% R / 25% E / 5% L).
    /// Indices excédentaires (plafond stock 3) convertis en +20 pièces. Vies jamais
    /// en coffre. 100% local PlayerPrefs (claim hors-ligne OK, IsClaimed anti double).
    ///
    /// Mascottes = animaux réels mx__0-7 (neutres). Rareté par index : 0-3 Commun,
    /// 4-5 Rare, 6 Épique, 7 Légendaire. Tirage sans remise par rareté, pity Épique
    /// à 8 tirages P2/P3 sans E/L, doublon converti en pièces (C40/R100/E200/L300).
    /// </summary>
    public static class StarChestManager
    {
        public const int Worlds = 4;
        public const int WorldSize = 25;
        public static readonly int[] Thresholds = { 10, 25, 50 };
        /// <summary>Nombre de mascottes : mx__0-7 + licorne (8) + axolotl (9).</summary>
        public const int MascotCount = 10;

        private const string ClaimKey = "StarChest_W{0}_P{1}";
        private const string OwnKey = "Mascot_Owned_mx{0}";
        private const string PityKey = "Mascot_PityEpic";
        private const int PityEpicAt = 8;
        private const int HintOverflowCoins = 20;

        public static int WorldOf(int level) => Mathf.Clamp((level - 1) / WorldSize, 0, Worlds - 1);

        public static int StarsOfWorld(int world)
        {
            int total = 0;
            int from = world * WorldSize + 1;
            for (int lvl = from; lvl < from + WorldSize; lvl++)
                total += LevelProgressManager.GetStars(lvl);
            return total;
        }

        public static bool IsClaimed(int world, int palier) =>
            PlayerPrefs.GetInt(string.Format(ClaimKey, world, palier), 0) == 1;

        public static bool IsReady(int world, int palier) =>
            !IsClaimed(world, palier) && StarsOfWorld(world) >= Thresholds[palier];

        public static void SetClaimed(int world, int palier)
        {
            PlayerPrefs.SetInt(string.Format(ClaimKey, world, palier), 1);
            PlayerPrefs.Save();
        }

        // ------------------------------------------------------------------
        // Mascottes (IDs = index mx__).
        // ------------------------------------------------------------------

        public static string RarityOf(int mxIndex)
        {
            if (mxIndex <= 3) return "C";
            if (mxIndex <= 5) return "R";
            if (mxIndex == 6) return "E";
            return "L"; // mx7 + licorne (8) + axolotl (9)
        }

        public static bool OwnsMascot(int mxIndex) =>
            PlayerPrefs.GetInt(string.Format(OwnKey, mxIndex), 0) == 1;

        /// <summary>Premier non-possédé d'une rareté (-1 si épuisée).</summary>
        public static int FirstUnowned(string rarity)
        {
            for (int i = 0; i < MascotCount; i++)
                if (RarityOf(i) == rarity && !OwnsMascot(i))
                    return i;
            return -1;
        }

        public static void SetOwned(int mxIndex)
        {
            PlayerPrefs.SetInt(string.Format(OwnKey, mxIndex), 1);
            PlayerPrefs.Save();
        }

        public static Sprite MascotFace(int mxIndex)
        {
            try
            {
                Sprite[] neutrals = AnimalIconSet.LoadMoodNeutrals();
                if (neutrals != null && mxIndex >= 0 && mxIndex < neutrals.Length)
                    return neutrals[mxIndex];
            }
            catch { }
            return null;
        }

        private static int DuplicateValue(string rarity) => rarity switch
        {
            "C" => 40,
            "R" => 100,
            "E" => 200,
            _ => 300,
        };

        /// <summary>
        /// Tirage mascotte. Retourne l'index mx__ ou -1 si doublon converti
        /// (duplicateCoins renseigné). Pity Épique à 8 tirages sans E/L.
        /// </summary>
        public static int RollMascot(bool isP3, out string rarity, out int duplicateCoins)
        {
            duplicateCoins = 0;
            int pity = PlayerPrefs.GetInt(PityKey, 0);
            float r = Random.value;
            rarity = isP3
                ? (r < 0.70f ? "R" : r < 0.95f ? "E" : "L")
                : (r < 0.70f ? "C" : "R");
            if (pity >= PityEpicAt && rarity != "L")
                rarity = "E";
            var pool = new List<int>();
            for (int i = 0; i < MascotCount; i++)
                if (RarityOf(i) == rarity && !OwnsMascot(i))
                    pool.Add(i);
            bool epicOrBetter = rarity == "E" || rarity == "L";
            if (pool.Count == 0)
            {
                duplicateCoins = DuplicateValue(rarity);
                return -1;
            }
            int pick = pool[Random.Range(0, pool.Count)];
            SetOwned(pick);
            PlayerPrefs.SetInt(PityKey, epicOrBetter ? 0 : pity + 1);
            PlayerPrefs.Save();
            return pick;
        }

        // ------------------------------------------------------------------
        // Récompenses.
        // ------------------------------------------------------------------

        public struct Grant
        {
            public int Coins;
            public int Hints;
            public int MascotIndex; // -1 = aucune
            public string MascotRarity;
            public int DuplicateCoins;
        }

        /// <summary>Aperçu (popup) : ne touche à rien.</summary>
        public static Grant Preview(int world, int palier)
        {
            var g = new Grant { Coins = 0, Hints = 0, MascotIndex = -1, MascotRarity = null, DuplicateCoins = 0 };
            if (IsClaimed(world, palier))
                return g;
            if (palier == 0) { g.Coins = 30; g.Hints = 1; }
            else if (palier == 1) { g.Coins = 60; g.Hints = 1; }
            else { g.Coins = 100; g.Hints = 2; }
            return g;
        }

        /// <summary>Total d'étoiles tous niveaux (voies A/B).</summary>
        public static int TotalStars()
        {
            int total = 0;
            for (int lvl = 1; lvl <= 100; lvl++)
                total += LevelProgressManager.GetStars(lvl);
            return total;
        }

        public static bool IsWorldComplete(int world)
        {
            for (int lvl = world * StarChestManager.WorldSize + 1; lvl < world * StarChestManager.WorldSize + 26; lvl++)
                if (LevelProgressManager.GetStars(lvl) <= 0)
                    return false;
            return true;
        }

        /// <summary>Prix boutique par rareté (-1 = jamais achetable).</summary>
        public static int ShopPrice(string rarity) => rarity switch
        {
            "C" => 80,
            "R" => 200,
            "E" => 450,
            _ => -1,
        };

        public static void CheckAndGrant()
        {
            try
            {
                int total = TotalStars();
                GrantThreshold(total, 30, "C");
                GrantThreshold(total, 80, "R");
                GrantThreshold(total, 150, "E");
                GrantThreshold(total, 250, "L");
                if (IsWorldComplete(0)) GrantFixed(4);
                if (IsWorldComplete(1)) GrantFixed(6);
                if (IsWorldComplete(3)) GrantFixed(7);
            }
            catch { }
        }

        private static void GrantThreshold(int total, int need, string rarity)
        {
            if (total < need) return;
            for (int i = 0; i < MascotCount; i++)
            {
                if (StarChestManager.RarityOf(i) != rarity || StarChestManager.OwnsMascot(i))
                    continue;
                StarChestManager.SetOwned(i);
                try { AnalyticsManager.LogMascotUnlocked("mx" + i, rarity); } catch { }
                return;
            }
        }

        private static void GrantFixed(int index)
        {
            if (StarChestManager.OwnsMascot(index)) return;
            StarChestManager.SetOwned(index);
            try { AnalyticsManager.LogMascotUnlocked("mx" + index, StarChestManager.RarityOf(index)); } catch { }
        }

        /// <summary>Applique le claim : pièces, indices (overflow +20), mascotte.</summary>
        public static Grant Execute(int world, int palier)
        {
            var g = Preview(world, palier);
            if (IsClaimed(world, palier))
                return g;
            int free = Mathf.Max(0, HintStockManager.MaxStock - HintStockManager.Get());
            int hintsAdded = Mathf.Min(g.Hints, free);
            if (hintsAdded > 0)
                HintStockManager.Add(hintsAdded);
            g.Coins += HintOverflowCoins * (g.Hints - hintsAdded);
            g.Hints = hintsAdded;
            if (palier >= 1)
            {
                int pick = RollMascot(palier == 2, out string rarity, out int dup);
                g.MascotIndex = pick;
                g.MascotRarity = rarity;
                g.DuplicateCoins = dup;
                g.Coins += dup;
            }
            if (g.Coins > 0)
                CurrencyManager.AddCoins(g.Coins);
            SetClaimed(world, palier);
            try
            {
                AnalyticsManager.LogChestOpened(world, palier);
                if (g.MascotIndex >= 0)
                    AnalyticsManager.LogMascotUnlocked("mx" + g.MascotIndex, g.MascotRarity);
            }
            catch { }
            return g;
        }
    }
}
