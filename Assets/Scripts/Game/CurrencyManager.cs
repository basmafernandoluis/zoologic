using UnityEngine;

namespace Zoologic
{
    /// <summary>
    /// Cagnotte de pièces persistante (PlayerPrefs). Les pièces sont uniquement
    /// gagnées en jeu (bonus de victoire) ; il n'y a ni achat in-app, ni régénération
    /// temporelle, ni serveur. Sert à acheter des power-ups (ex: indice supplémentaire).
    /// </summary>
    public static class CurrencyManager
    {
        private const string CoinsKey = "player_coins";

        /// <summary>Nombre de pièces actuellement détenues.</summary>
        public static int GetCoins()
        {
            return PlayerPrefs.GetInt(CoinsKey, 0);
        }

        /// <summary>Ajoute (ou retire) des pièces à la cagnotte. Le solde ne descend jamais sous 0.</summary>
        public static void AddCoins(int amount)
        {
            PlayerPrefs.SetInt(CoinsKey, Mathf.Max(0, GetCoins() + amount));
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Tente de dépenser <paramref name="amount"/> pièces. Retourne true si
        /// le solde était suffisant (et le solde a été réduit), false sinon.
        /// </summary>
        public static bool SpendCoins(int amount)
        {
            int coins = GetCoins();
            if (coins < amount)
                return false;

            PlayerPrefs.SetInt(CoinsKey, coins - amount);
            PlayerPrefs.Save();
            return true;
        }

        /// <summary>Vrai si le solde est suffisant pour payer <paramref name="amount"/>.</summary>
        public static bool HasCoins(int amount)
        {
            return GetCoins() >= amount;
        }

        /// <summary>Réinitialise la cagnotte (debug / développement).</summary>
        public static void ResetCoins()
        {
            PlayerPrefs.DeleteKey(CoinsKey);
            PlayerPrefs.Save();
        }
    }
}
