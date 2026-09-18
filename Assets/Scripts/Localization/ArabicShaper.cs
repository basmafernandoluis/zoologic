using System.Collections.Generic;
using System.Text;

namespace Zoologic.Localization
{
    /// <summary>
    /// Façonnage arabe minimal (ordre logique -> ordre visuel + formes contextuelles).
    ///
    /// Contexte : TextMeshPro (pipeline SDF) ne soude pas les lettres arabes :
    /// isRightToLeftText inverse l'ordre mais ne fait pas de shaping OpenType.
    /// Sans prétraitement, "الإعدادات" s'affiche en glyphes isolés (pire : inversés).
    /// Ce module convertit chaque ligne en ordre visuel avec formes de présentation
    /// (isolée/initiale/médiane/finale + ligatures lam-alef), à la manière d'ArabicFixer.
    ///
    /// Tables de correspondance : python-arabic-reshaper (MIT), LETTERS_ARABIC v1.
    /// Pur C# (aucune dépendance Unity) pour rester testable hors éditeur.
    /// Actif uniquement quand LocalizationManager.Current == "ar-SA".
    /// </summary>
    public static class ArabicShaper
    {
        private const char Lam = '\u0644';
        private const char Tatweel = '\u0640';
        private const char Zwj = '\u200D';

        // Index : 0 = isolée, 1 = initiale, 2 = médiane, 3 = finale. '\0' = absente.
        private static readonly Dictionary<char, char[]> Forms = new Dictionary<char, char[]>
        {
            { '\u0621', new char[] { '\uFE80', '\0', '\0', '\0' } },
            { '\u0622', new char[] { '\uFE81', '\0', '\0', '\uFE82' } },
            { '\u0623', new char[] { '\uFE83', '\0', '\0', '\uFE84' } },
            { '\u0624', new char[] { '\uFE85', '\0', '\0', '\uFE86' } },
            { '\u0625', new char[] { '\uFE87', '\0', '\0', '\uFE88' } },
            { '\u0626', new char[] { '\uFE89', '\uFE8B', '\uFE8C', '\uFE8A' } },
            { '\u0627', new char[] { '\uFE8D', '\0', '\0', '\uFE8E' } },
            { '\u0628', new char[] { '\uFE8F', '\uFE91', '\uFE92', '\uFE90' } },
            { '\u0629', new char[] { '\uFE93', '\0', '\0', '\uFE94' } },
            { '\u062A', new char[] { '\uFE95', '\uFE97', '\uFE98', '\uFE96' } },
            { '\u062B', new char[] { '\uFE99', '\uFE9B', '\uFE9C', '\uFE9A' } },
            { '\u062C', new char[] { '\uFE9D', '\uFE9F', '\uFEA0', '\uFE9E' } },
            { '\u062D', new char[] { '\uFEA1', '\uFEA3', '\uFEA4', '\uFEA2' } },
            { '\u062E', new char[] { '\uFEA5', '\uFEA7', '\uFEA8', '\uFEA6' } },
            { '\u062F', new char[] { '\uFEA9', '\0', '\0', '\uFEAA' } },
            { '\u0630', new char[] { '\uFEAB', '\0', '\0', '\uFEAC' } },
            { '\u0631', new char[] { '\uFEAD', '\0', '\0', '\uFEAE' } },
            { '\u0632', new char[] { '\uFEAF', '\0', '\0', '\uFEB0' } },
            { '\u0633', new char[] { '\uFEB1', '\uFEB3', '\uFEB4', '\uFEB2' } },
            { '\u0634', new char[] { '\uFEB5', '\uFEB7', '\uFEB8', '\uFEB6' } },
            { '\u0635', new char[] { '\uFEB9', '\uFEBB', '\uFEBC', '\uFEBA' } },
            { '\u0636', new char[] { '\uFEBD', '\uFEBF', '\uFEC0', '\uFEBE' } },
            { '\u0637', new char[] { '\uFEC1', '\uFEC3', '\uFEC4', '\uFEC2' } },
            { '\u0638', new char[] { '\uFEC5', '\uFEC7', '\uFEC8', '\uFEC6' } },
            { '\u0639', new char[] { '\uFEC9', '\uFECB', '\uFECC', '\uFECA' } },
            { '\u063A', new char[] { '\uFECD', '\uFECF', '\uFED0', '\uFECE' } },
            { Tatweel, new char[] { Tatweel, Tatweel, Tatweel, Tatweel } },
            { '\u0641', new char[] { '\uFED1', '\uFED3', '\uFED4', '\uFED2' } },
            { '\u0642', new char[] { '\uFED5', '\uFED7', '\uFED8', '\uFED6' } },
            { '\u0643', new char[] { '\uFED9', '\uFEDB', '\uFEDC', '\uFEDA' } },
            { Lam, new char[] { '\uFEDD', '\uFEDF', '\uFEE0', '\uFEDE' } },
            { '\u0645', new char[] { '\uFEE1', '\uFEE3', '\uFEE4', '\uFEE2' } },
            { '\u0646', new char[] { '\uFEE5', '\uFEE7', '\uFEE8', '\uFEE6' } },
            { '\u0647', new char[] { '\uFEE9', '\uFEEB', '\uFEEC', '\uFEEA' } },
            { '\u0648', new char[] { '\uFEED', '\0', '\0', '\uFEEE' } },
            { '\u0649', new char[] { '\uFEEF', '\uFBE8', '\uFBE9', '\uFEF0' } },
            { '\u064A', new char[] { '\uFEF1', '\uFEF3', '\uFEF4', '\uFEF2' } },
            { '\u0671', new char[] { '\uFB50', '\0', '\0', '\uFB51' } },
            { Zwj, new char[] { Zwj, Zwj, Zwj, Zwj } },
        };

        // Lam + alef -> ligature { isolée, finale }.
        private static char LamAlefLigature(char alef, bool joinPrev, out bool ok)
        {
            ok = true;
            if (alef == '\u0627') return joinPrev ? '\uFEFC' : '\uFEFB';
            if (alef == '\u0623') return joinPrev ? '\uFEF8' : '\uFEF7';
            if (alef == '\u0625') return joinPrev ? '\uFEFA' : '\uFEF9';
            if (alef == '\u0622') return joinPrev ? '\uFEF6' : '\uFEF5';
            ok = false;
            return '\0';
        }

        private static bool IsAlefVariant(char c)
        {
            return c == '\u0627' || c == '\u0623' || c == '\u0625' || c == '\u0622';
        }

        private static bool JoinsToPrev(char c)
        {
            char[] f;
            if (!Forms.TryGetValue(c, out f)) return false;
            return f[2] != '\0' || f[3] != '\0';
        }

        private static bool JoinsToNext(char c)
        {
            char[] f;
            if (!Forms.TryGetValue(c, out f)) return false;
            return f[1] != '\0' || f[2] != '\0';
        }

        private static bool IsMark(char c)
        {
            return (c >= '\u0610' && c <= '\u061A')
                || (c >= '\u064B' && c <= '\u065F')
                || c == '\u0670'
                || (c >= '\u06D6' && c <= '\u06ED');
        }

        private static bool IsArabicBase(char c)
        {
            return (c >= '\u0600' && c <= '\u06FF')
                || (c >= '\u0750' && c <= '\u077F')
                || (c >= '\u08A0' && c <= '\u08FF');
        }

        private static bool IsAsciiWordChar(char c)
        {
            return (c >= '0' && c <= '9')
                || (c >= 'A' && c <= 'Z')
                || (c >= 'a' && c <= 'z');
        }

        private static bool IsArabicDigit(char c)
        {
            return (c >= '\u0660' && c <= '\u0669')
                || (c >= '\u06F0' && c <= '\u06F9');
        }

        private static bool IsRunSeparator(char c)
        {
            return c == '.' || c == ',' || c == ':' || c == '/' || c == '%' || c == '+' || c == '-';
        }

        private static char Mirror(char c)
        {
            switch (c)
            {
                case '(': return ')';
                case ')': return '(';
                case '[': return ']';
                case ']': return '[';
                case '{': return '}';
                case '}': return '{';
                case '<': return '>';
                case '>': return '<';
                default: return c;
            }
        }

        /// <summary>Vrai si le texte contient de l'arabe à façonner.</summary>
        public static bool NeedsShaping(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            for (int i = 0; i < text.Length; i++)
                if (IsArabicBase(text[i])) return true;
            return false;
        }

        private static bool AlreadyShaped(string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if ((c >= '\uFB50' && c <= '\uFDFF') || (c >= '\uFE70' && c <= '\uFEFF'))
                    return true;
            }
            return false;
        }

        /// <summary>Ordre logique -> ordre visuel avec formes contextuelles (ligne par ligne).</summary>
        public static string Shape(string text)
        {
            if (!NeedsShaping(text) || AlreadyShaped(text)) return text;
            string[] lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
                lines[i] = ShapeLine(lines[i]);
            return string.Join("\n", lines);
        }

        private static string ShapeLine(string line)
        {
            List<string> parts = new List<string>(line.Length + 4);
            List<bool> fixedTag = new List<bool>(line.Length + 4);
            StringBuilder run = new StringBuilder();
            char prevBase = '\0';
            bool prevLigature = false;

            int i = 0;
            while (i < line.Length)
            {
                char c = line[i];

                // Paire de substitution : unité insécable.
                if (char.IsHighSurrogate(c) && i + 1 < line.Length && char.IsLowSurrogate(line[i + 1]))
                {
                    FlushRun(run, parts, fixedTag);
                    parts.Add(line.Substring(i, 2));
                    fixedTag.Add(false);
                    prevBase = '\0';
                    prevLigature = false;
                    i += 2;
                    continue;
                }

                // Tag riche <...> : copié tel quel, position fixe, casse la jointure.
                if (c == '<')
                {
                    int end = line.IndexOf('>', i + 1);
                    if (end > i)
                    {
                        FlushRun(run, parts, fixedTag);
                        parts.Add(line.Substring(i, end - i + 1));
                        fixedTag.Add(true);
                        prevBase = '\0';
                        prevLigature = false;
                        i = end + 1;
                        continue;
                    }
                }

                // Diacritique : attaché à l'unité précédente, transparent pour la jointure.
                if (IsMark(c))
                {
                    if (parts.Count > 0 && !fixedTag[parts.Count - 1] && prevBase != '\0')
                        parts[parts.Count - 1] = parts[parts.Count - 1] + c;
                    else
                    {
                        FlushRun(run, parts, fixedTag);
                        parts.Add(c.ToString());
                        fixedTag.Add(false);
                    }
                    i++;
                    continue;
                }

                char[] f;
                if (Forms.TryGetValue(c, out f))
                {
                    FlushRun(run, parts, fixedTag);
                    // La ligature lam-alef ne se prolonge jamais vers la gauche :
                    // seule une lettre double (initiale/médiane) peut s'y attacher à droite.
                    bool canPrev = prevBase != '\0'
                        && (prevLigature ? false : JoinsToNext(prevBase))
                        && JoinsToPrev(c);

                    // Ligature lam-alef (regard devant, en sautant les diacritiques).
                    if (c == Lam)
                    {
                        int k = i + 1;
                        StringBuilder skippedMarks = null;
                        while (k < line.Length && IsMark(line[k]))
                        {
                            if (skippedMarks == null) skippedMarks = new StringBuilder();
                            skippedMarks.Append(line[k]);
                            k++;
                        }
                        if (k < line.Length && IsAlefVariant(line[k]))
                        {
                            bool ok;
                            char lig = LamAlefLigature(line[k], canPrev, out ok);
                            if (ok)
                            {
                                string unit = lig.ToString();
                                if (skippedMarks != null) unit += skippedMarks.ToString();
                                parts.Add(unit);
                                fixedTag.Add(false);
                                prevBase = Lam;
                                prevLigature = true;
                                i = k + 1;
                                continue;
                            }
                        }
                    }

                    bool canNext = false;
                    if (JoinsToNext(c))
                    {
                        int k = i + 1;
                        while (k < line.Length && IsMark(line[k])) k++;
                        if (k < line.Length && JoinsToPrev(line[k])) canNext = true;
                    }

                    char shaped;
                    if (canPrev && canNext && f[2] != '\0') shaped = f[2];
                    else if (canPrev && f[3] != '\0') shaped = f[3];
                    else if (canNext && f[1] != '\0') shaped = f[1];
                    // Position isolée : on émet la LETTRE DE BASE (ex. ا U+0627)
                    // plutôt que sa forme de présentation (ex. ﺍ U+FE8D).
                    // Les deux ont exactement le même glyphe, mais les fontes
                    // arabes (Almarai…) ne mappent souvent que la forme de base :
                    // TMP refuse les doublons de glyphes (TryAddCharacters les
                    // ignore) et la forme isolée devient un tofu □. La base est
                    // toujours présente (collectée depuis les JSON).
                    // (La ligature lam-alef, sans équivalent de base, est émise
                    // plus haut et ne passe jamais par cette branche.)
                    else shaped = c;
                    parts.Add(shaped.ToString());
                    fixedTag.Add(false);
                    prevBase = c;
                    prevLigature = false;
                    i++;
                    continue;
                }

                // Autres caractères : miroir éventuel, runs LTR, ponctuation.
                char m = Mirror(c);
                if (IsAsciiWordChar(c) || IsArabicDigit(c))
                {
                    run.Append(c);
                    i++;
                    continue;
                }
                if (IsRunSeparator(c) && run.Length > 0 && i + 1 < line.Length
                    && (IsAsciiWordChar(line[i + 1]) || IsArabicDigit(line[i + 1])))
                {
                    run.Append(c);
                    i++;
                    continue;
                }
                FlushRun(run, parts, fixedTag);
                parts.Add(m.ToString());
                fixedTag.Add(false);
                prevBase = '\0';
                prevLigature = false;
                i++;
            }
            FlushRun(run, parts, fixedTag);

            // Inversion : les tags restent à leur place, le reste est miroiré.
            List<int> movable = new List<int>(parts.Count);
            for (int p = 0; p < parts.Count; p++)
                if (!fixedTag[p]) movable.Add(p);
            int a = 0;
            int b = movable.Count - 1;
            while (a < b)
            {
                string tmp = parts[movable[a]];
                parts[movable[a]] = parts[movable[b]];
                parts[movable[b]] = tmp;
                a++;
                b--;
            }

            StringBuilder out_ = new StringBuilder(line.Length + 4);
            for (int p = 0; p < parts.Count; p++)
                out_.Append(parts[p]);
            return out_.ToString();
        }

        private static void FlushRun(StringBuilder run, List<string> parts, List<bool> fixedTag)
        {
            if (run.Length > 0)
            {
                parts.Add(run.ToString());
                fixedTag.Add(false);
                run.Length = 0;
            }
        }
    }
}
