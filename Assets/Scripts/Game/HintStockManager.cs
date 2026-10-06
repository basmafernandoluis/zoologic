using UnityEngine;

namespace Zoologic
{
    /// <summary>
    /// Stock global d'indices, persistant entre les niveaux (PlayerPrefs).
    /// Le jeu démarre à 3 ; une pub rewarded recharge +1 (plafond 3).
    /// Pas d'achat en pièces.
    /// </summary>
    public static class HintStockManager
    {
        private const string StockKey = "hint_stock";
        public const int DefaultStock = 3;
        public const int MaxStock = 3;

        public static int Get()
        {
            return Mathf.Clamp(PlayerPrefs.GetInt(StockKey, DefaultStock), 0, MaxStock);
        }

        public static void Set(int value)
        {
            PlayerPrefs.SetInt(StockKey, Mathf.Clamp(value, 0, MaxStock));
            PlayerPrefs.Save();
        }

        /// <summary>Consomme 1 indice si possible.</summary>
        public static bool Consume()
        {
            int stock = Get();
            if (stock <= 0) return false;
            Set(stock - 1);
            return true;
        }

        /// <summary>Recharge (pub), plafonnée.</summary>
        public static void Add(int amount)
        {
            if (amount <= 0) return;
            Set(Get() + amount);
        }
    }
}
