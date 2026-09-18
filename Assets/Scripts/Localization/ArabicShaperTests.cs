using System;

namespace Zoologic.Localization
{
    /// <summary>
    /// Tests du façonnage arabe, indépendants de Unity (même style que
    /// Zoologic.Core.PuzzleTests) : appelés via RunAllTests(), vérifiables
    /// hors éditeur puisque ArabicShaper est pur C#.
    /// Valeurs attendues = formes de présentation Unicode (tables
    /// python-arabic-reshaper, vérifiées à la main).
    /// </summary>
    public static class ArabicShaperTests
    {
        public static bool RunAllTests()
        {
            bool ok = true;
            ok &= Execute("Lam-alef isolé", Test_LamAlefIsole);
            ok &= Execute("Salam (ligature médiane)", Test_Salam);
            ok &= Execute("Conflit colonne", Test_ConflitColonne);
            ok &= Execute("Titre réglages (ligature لإ)", Test_TitreReglages);
            ok &= Execute("Shadda conservée", Test_Shadda);
            ok &= Execute("Mixte lettres + chiffres", Test_MixteChiffres);
            ok &= Execute("Flèche miroir", Test_FlecheMiroir);
            ok &= Execute("Tags riches appariés", Test_TagsApparies);
            ok &= Execute("Multi-ligne", Test_MultiLigne);
            ok &= Execute("Passe-through non-arabe", Test_PasseThrough);
            ok &= Execute("Idempotence", Test_Idempotence);

            Console.WriteLine();
            Console.WriteLine(ok ? "=== ARABE : TOUS LES TESTS SONT PASSÉS ==="
                                 : "=== ARABE : CERTAINS TESTS ONT ÉCHOUÉ ===");
            return ok;
        }

        private static bool Execute(string nom, Func<bool> test)
        {
            try
            {
                if (test()) { Console.WriteLine("[OK] " + nom); return true; }
                Console.WriteLine("[KO] " + nom + " (false)");
                return false;
            }
            catch (Exception e)
            {
                Console.WriteLine("[KO] " + nom + " : " + e.Message);
                return false;
            }
        }

        private static void AssertEqual(string attendu, string obtenu)
        {
            if (!string.Equals(attendu, obtenu, StringComparison.Ordinal))
                throw new Exception("attendu <" + Codes(attendu) + "> obtenu <" + Codes(obtenu) + ">");
        }

        private static string Codes(string s)
        {
            if (s == null) return "null";
            string r = "";
            foreach (char c in s) r += "U+" + ((int)c).ToString("X4") + " ";
            return r.TrimEnd();
        }

        private static string C(params int[] codes)
        {
            string s = "";
            foreach (int c in codes) s += (char)c;
            return s;
        }

        private static bool Test_LamAlefIsole()
        {
            AssertEqual(C(0xFEFB), ArabicShaper.Shape("لا"));
            return true;
        }

        private static bool Test_Salam()
        {
            AssertEqual(C(0x645, 0xFEFC, 0xFEB3), ArabicShaper.Shape("سلام"));
            return true;
        }

        private static bool Test_ConflitColonne()
        {
            AssertEqual(
                C(0x21, 0x62F, 0xFEEE, 0xFEE4, 0xFECC, 0xFEDF, 0x627, 0x20, 0xFEB2, 0xFED4, 0xFEE7),
                ArabicShaper.Shape("نفس العمود!"));
            return true;
        }

        private static bool Test_TitreReglages()
        {
            AssertEqual(
                C(0x62A, 0x627, 0x62F, 0x627, 0xFEAA, 0xFECB, 0xFEF9, 0x627),
                ArabicShaper.Shape("الإعدادات"));
            return true;
        }

        private static bool Test_Shadda()
        {
            AssertEqual(
                C(0xFEDE, 0xFECC, 0x0651, 0xFED4, 0xFEE3),
                ArabicShaper.Shape("مفعّل"));
            return true;
        }

        private static bool Test_MixteChiffres()
        {
            AssertEqual(
                C(0x31, 0x35, 0x20, 0x649, 0xFEEE, 0xFE98, 0xFEB4, 0xFEE4, 0xFEDF, 0x627),
                ArabicShaper.Shape("المستوى 15"));
            return true;
        }

        private static bool Test_FlecheMiroir()
        {
            AssertEqual(
                C(0x3C, 0x20, 0xFE90, 0xFECC, 0xFEDF, 0x627),
                ArabicShaper.Shape("العب >"));
            return true;
        }

        private static bool Test_TagsApparies()
        {
            string r = ArabicShaper.Shape("<u>رجوع</u>");
            if (!r.StartsWith("<u>") || !r.EndsWith("</u>"))
                throw new Exception("tags désappariés : " + Codes(r));
            return true;
        }

        private static bool Test_MultiLigne()
        {
            string r = ArabicShaper.Shape("هل أنت متأكد؟\nلا يمكن التراجع.");
            if (r.Split('\n').Length != 2)
                throw new Exception("lignes perdues : " + Codes(r));
            return true;
        }

        private static bool Test_PasseThrough()
        {
            AssertEqual("Français", ArabicShaper.Shape("Français"));
            AssertEqual("v0.1", ArabicShaper.Shape("v0.1"));
            AssertEqual("05:00", ArabicShaper.Shape("05:00"));
            AssertEqual("", ArabicShaper.Shape(""));
            return true;
        }

        private static bool Test_Idempotence()
        {
            string uneFois = ArabicShaper.Shape("نفس اللون!");
            AssertEqual(uneFois, ArabicShaper.Shape(uneFois));
            return true;
        }
    }
}
