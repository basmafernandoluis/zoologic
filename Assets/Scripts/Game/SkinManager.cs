using UnityEngine;

namespace Zoologic
{
    /// <summary>
    /// Collection de skins de pions : le but des pièces. Sélection persistée,
    /// appliquée au prochain niveau (pions du plateau + jetons de la barre).
    ///
    /// - Bois (0) : médailles sp1, gratuit, possédé par défaut ;
    /// - Flat (1) : têtes plates Art/Animals, 200 pièces ;
    /// - Doré (2) : médailles sp1 teintées or, 350 pièces ;
    /// - Nuit (3) : médailles sp1 teintées bleu nuit, 500 pièces.
    /// </summary>
    public static class SkinManager
    {
        public const int Wood = 0;
        public const int Flat = 1;
        public const int Gold = 2;
        public const int Night = 3;
        public const int Count = 4;

        private const string SelectedKey = "skin_selected";
        private const string OwnedKey = "skin_owned";

        private static readonly int[] Costs = { 0, 200, 350, 500 };

        private static readonly Color[] Tints =
        {
            Color.white,
            Color.white,
            new Color(1f, 0.85f, 0.45f, 1f),
            new Color(0.62f, 0.72f, 1f, 1f),
        };

        public static int CostOf(int skinId)
        {
            if (skinId < 0 || skinId >= Count) return int.MaxValue;
            return Costs[skinId];
        }

        public static Color TintOf(int skinId)
        {
            if (skinId < 0 || skinId >= Count) return Color.white;
            return Tints[skinId];
        }

        public static bool UsesFlatFaces(int skinId)
        {
            return skinId == Flat;
        }

        public static int Selected
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(SelectedKey, Wood), 0, Count - 1);
        }

        public static Color SelectedTint => TintOf(Selected);

        public static bool UsesFlatFacesSelected => UsesFlatFaces(Selected);

        public static bool IsOwned(int skinId)
        {
            if (skinId == Wood) return true;
            string owned = PlayerPrefs.GetString(OwnedKey, "0");
            string[] parts = owned.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Trim() == skinId.ToString())
                    return true;
            }
            return false;
        }

        public static void Own(int skinId)
        {
            if (IsOwned(skinId)) return;
            string owned = PlayerPrefs.GetString(OwnedKey, "0");
            PlayerPrefs.SetString(OwnedKey, owned + "," + skinId);
            PlayerPrefs.Save();
        }

        public static void Select(int skinId)
        {
            if (skinId < 0 || skinId >= Count) return;
            if (!IsOwned(skinId)) return;
            PlayerPrefs.SetInt(SelectedKey, skinId);
            PlayerPrefs.Save();
        }

        /// <summary>Sprites de zones selon le skin sélectionné (mélangés).</summary>
        public static Sprite[] GetZoneSprites()
        {
            return UsesFlatFacesSelected
                ? AnimalIconSet.GetShuffledFlat()
                : AnimalIconSet.GetShuffled();
        }

        /// <summary>3 premiers sprites d'un skin pour l'aperçu boutique.</summary>
        public static Sprite[] GetPreviewSprites(int skinId)
        {
            Sprite[] all = UsesFlatFaces(skinId)
                ? AnimalIconSet.LoadFlatFaces()
                : AnimalIconSet.LoadAll();
            int n = Mathf.Min(3, all.Length);
            var preview = new Sprite[n];
            for (int i = 0; i < n; i++)
                preview[i] = all[i];
            return preview;
        }
    }
}
