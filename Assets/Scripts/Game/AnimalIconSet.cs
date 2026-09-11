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
        private const string ResourceFolder = "Sprites";
        private const string AssetFolder = "Assets/Resources/Sprites";

        private static Sprite[] _icons;

        /// <summary>
        /// Charge (une seule fois) toutes les icônes du dossier, triées par nom pour un
        /// ordre stable. Renvoie un tableau vide si le dossier manque.
        /// </summary>
        public static Sprite[] LoadAll()
        {
            if (_icons != null)
                return _icons;

            var all = Resources.LoadAll<Sprite>(ResourceFolder);
            _icons = FilterAnimalSprites(all);

#if UNITY_EDITOR
            if (_icons == null || _icons.Length == 0)
                _icons = FilterAnimalSprites(LoadFromAssetDatabase());
#endif

            if (_icons == null || _icons.Length == 0)
            {
                _icons = new Sprite[0];
                Debug.LogWarning(
                    "[Zoologic] AnimalIconSet : aucune icône trouvée dans " + AssetFolder +
                    " (filtre sp1_*). Les pions retomberont sur le cercle de secours.");
            }
            else
            {
                Array.Sort(_icons, (a, b) => string.CompareOrdinal(a.name, b.name));
            }

            return _icons;
        }

        private static Sprite[] FilterAnimalSprites(Sprite[] source)
        {
            if (source == null) return new Sprite[0];
            var list = new System.Collections.Generic.List<Sprite>();
            for (int i = 0; i < source.Length; i++)
            {
                var s = source[i];
                if (s == null) continue;
                if (s.name.StartsWith("sp1_")) list.Add(s);
            }
            return list.ToArray();
        }

#if UNITY_EDITOR
        private static Sprite[] LoadFromAssetDatabase()
        {
            string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { AssetFolder });
            var list = new System.Collections.Generic.List<Sprite>();
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sp != null && sp.name.StartsWith("sp1_")) list.Add(sp);
            }
            return list.ToArray();
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
