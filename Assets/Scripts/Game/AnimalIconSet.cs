using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Zoologic
{
    /// <summary>
    /// Charge et distribue les icônes d'animaux du jeu (une par zone de la grille).
    ///
    /// Les PNG doivent être importés dans <c>Assets/Resources/Art/Animals/</c> avec le
    /// type "Sprite (2D and UI)" : ils sont alors chargés dynamiquement au runtime via
    /// <see cref="Resources.LoadAll{T}"/>, sans aucune référence à configurer dans
    /// l'Inspector.
    ///
    /// En mode éditeur (et batchmode), <see cref="Resources.LoadAll{T}"/> peut renvoyer
    /// un tableau vide avant la fin de l'import : on bascule alors sur
    /// <see cref="AssetDatabase"/>, qui interroge directement les assets du projet.
    /// </summary>
    public static class AnimalIconSet
    {
        private const string ResourceFolder = "Art/Animals";
        private const string AssetFolder = "Assets/Resources/Art/Animals";

        private static Sprite[] _icons;

        /// <summary>
        /// Charge (une seule fois) toutes les icônes du dossier, triées par nom pour un
        /// ordre stable. Renvoie un tableau vide si le dossier manque.
        /// </summary>
        public static Sprite[] LoadAll()
        {
            if (_icons != null)
                return _icons;

            _icons = Resources.LoadAll<Sprite>(ResourceFolder);

#if UNITY_EDITOR
            // Repli éditeur/batch : la base Resources peut ne pas encore être à jour.
            if (_icons == null || _icons.Length == 0)
                _icons = LoadFromAssetDatabase();
#endif

            if (_icons == null || _icons.Length == 0)
            {
                _icons = new Sprite[0];
                Debug.LogWarning(
                    "[Zoologic] AnimalIconSet : aucune icône trouvée dans " + AssetFolder +
                    ". Les pions retomberont sur le cercle de secours.");
            }
            else
            {
                Array.Sort(_icons, (a, b) => string.CompareOrdinal(a.name, b.name));
            }

            return _icons;
        }

#if UNITY_EDITOR
        private static Sprite[] LoadFromAssetDatabase()
        {
            string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { AssetFolder });
            var sprites = new Sprite[guids.Length];

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                sprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }

            return sprites;
        }
#endif

        /// <summary>
        /// Renvoie toutes les icônes dans une permutation aléatoire (mélange de Fisher-Yates).
        /// Appelée une fois par niveau : GridView pioche ensuite dans l'ordre, ce qui
        /// garantit des animaux différents entre les zones d'un même niveau.
        /// </summary>
        public static Sprite[] GetShuffled()
        {
            Sprite[] icons = LoadAll();
            var copy = new Sprite[icons.Length];
            Array.Copy(icons, copy, icons.Length);

            for (int i = copy.Length - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                Sprite temp = copy[i];
                copy[i] = copy[j];
                copy[j] = temp;
            }

            return copy;
        }
    }
}
