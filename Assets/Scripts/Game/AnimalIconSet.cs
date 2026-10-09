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

        private static Sprite[] _flatFaces;

        /// <summary>
        /// Têtes plates (skin Flat) depuis Resources/Art/Animals, triées par nom.
        /// </summary>
        public static Sprite[] LoadFlatFaces()
        {
            if (_flatFaces != null)
                return _flatFaces;

            var all = Resources.LoadAll<Sprite>("Art/Animals");
            var list = new System.Collections.Generic.List<Sprite>();
            if (all != null)
            {
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] != null)
                        list.Add(all[i]);
                }
                list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            }
            _flatFaces = list.ToArray();
            if (_flatFaces.Length == 0)
                Debug.LogWarning("[Zoologic] AnimalIconSet : aucune tête flat dans Art/Animals.");
            return _flatFaces;
        }

        /// <summary>Humeur d'un pion (mx__ : neutre / heureux / triste).</summary>
        public enum PawnMood
        {
            Neutral = 0,
            Happy = 1,
            Sad = 2,
        }

        /// <summary>Triplet d'humeurs d'un même animal (jamais null si complet).</summary>
        public readonly struct MoodSet
        {
            public readonly Sprite Neutral;
            public readonly Sprite Happy;
            public readonly Sprite Sad;

            public MoodSet(Sprite neutral, Sprite happy, Sprite sad)
            {
                Neutral = neutral;
                Happy = happy;
                Sad = sad;
            }

            public bool IsComplete => Neutral != null && Happy != null && Sad != null;

            public Sprite For(PawnMood mood)
            {
                switch (mood)
                {
                    case PawnMood.Happy: return Happy ?? Neutral;
                    case PawnMood.Sad: return Sad ?? Neutral;
                    default: return Neutral;
                }
            }
        }

        private static Sprite[] _moodSheet;

        /// <summary>
        /// Charge la planche mx__ (mx__0..7 neutres, mx__8..15 heureux, mx__16..23
        /// tristes). Tri NUMÉRIQUE (l'ordre ordinal mettrait mx__10 avant mx__2).
        /// </summary>
        public static Sprite[] LoadMoodSheet()
        {
            if (_moodSheet != null)
                return _moodSheet;

            var all = Resources.LoadAll<Sprite>("Sprites/mx_");
            var indexed = new System.Collections.Generic.SortedDictionary<int, Sprite>();
            if (all != null)
            {
                for (int i = 0; i < all.Length; i++)
                {
                    Sprite s = all[i];
                    if (s == null) continue;
                    string name = s.name;
                    if (!name.StartsWith("mx__")) continue;
                    if (int.TryParse(name.Substring(4), out int index))
                        indexed[index] = s;
                }
            }
            var list = new System.Collections.Generic.List<Sprite>(indexed.Values);
            _moodSheet = list.ToArray();
            if (_moodSheet.Length != 24)
                Debug.LogWarning("[Zoologic] AnimalIconSet : planche mx__ incomplète (" + _moodSheet.Length + "/24).");
            return _moodSheet;
        }

        /// <summary>
        /// 8 triplets appariés par index (i, i+8, i+16), mélangés. Triplets
        /// incomplets exclus d'office (jamais de pion invisible).
        /// </summary>
        public static System.Collections.Generic.List<MoodSet> GetShuffledMoodSets()
        {
            Sprite[] sheet = LoadMoodSheet();
            var sets = new System.Collections.Generic.List<MoodSet>();
            for (int i = 0; i < 8; i++)
            {
                Sprite n = i < sheet.Length ? sheet[i] : null;
                Sprite h = i + 8 < sheet.Length ? sheet[i + 8] : null;
                Sprite s = i + 16 < sheet.Length ? sheet[i + 16] : null;
                var set = new MoodSet(n, h, s);
                if (set.IsComplete)
                    sets.Add(set);
                else
                    Debug.LogWarning("[Zoologic] AnimalIconSet : triplet mx__" + i + " incomplet, exclu.");
            }
            // Les légendaires restent exclusifs collection (jamais en pions
            // jouables) : les posséder doit signifier quelque chose. Ils restent
            // visibles via LoadMoodNeutrals (contrôle via déblocages).
            for (int i = sets.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                MoodSet temp = sets[i];
                sets[i] = sets[j];
                sets[j] = temp;
            }
            return sets;
        }

        /// <summary>
        /// Sprites d'extension (licorne + axolotl doré, triplets N/H/S découpés).
        /// Ordre vérifié : _0 neutre, _1 heureux, _2 triste.
        /// </summary>
        private static Sprite[] LoadExtraSet(string file)
        {
            try
            {
                var all = Resources.LoadAll<Sprite>("Sprites/" + file);
                var list = new System.Collections.Generic.List<Sprite>();
                if (all != null)
                {
                    foreach (var s in all)
                        if (s != null) list.Add(s);
                    list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
                }
                if (list.Count >= 3)
                    return new[] { list[0], list[1], list[2] };
            }
            catch { }
            return null;
        }

        /// <summary>Neutres mx__ (8) + licorne (8) + axolotl (9) pour collection.</summary>
        public static Sprite[] LoadMoodNeutrals()
        {
            Sprite[] sheet = LoadMoodSheet();
            var neutrals = new System.Collections.Generic.List<Sprite>();
            int n = sheet != null ? Mathf.Min(8, sheet.Length) : 0;
            for (int i = 0; i < n; i++)
                neutrals.Add(sheet[i]);
            Sprite[] licorne = LoadExtraSet("Licorne_legendaire");
            Sprite[] axolotl = LoadExtraSet("Axolotl_dore");
            if (licorne != null) neutrals.Add(licorne[0]);
            if (axolotl != null) neutrals.Add(axolotl[0]);
            return neutrals.ToArray();
        }

        /// <summary>Version mélangée des têtes plates (même contrat que GetShuffled).</summary>
        public static Sprite[] GetShuffledFlat()
        {
            Sprite[] icons = LoadFlatFaces();
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
