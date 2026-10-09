using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Zoologic.Core;

namespace Zoologic.EditorTools
{
    /// <summary>
    /// QA Gate pré-prod : Core puzzle + méta-progression (coffres, niveaux,
    /// skins, loc), testable en batch Éditeur Windows.
    /// Éditeur Windows, rapport seul : ne bloque jamais le build prod.
    /// Menu : Tools/Zoo Logic/QA/Run Core Gate (log + .md). Batchmode : QAGate.RunBatch.
    /// </summary>
    public static class QAGate
    {
        private enum QAStatus { Pass, Warn, Fail }

        private sealed class QACheck
        {
            public string Id;
            public string Module;
            public string Name;
            public QAStatus Status;
            public long Ms;
            public string Detail;
        }

        private sealed class QAFailException : Exception
        {
            public QAFailException(string message) : base(message) { }
        }

        private static readonly List<QACheck> _checks = new List<QACheck>();

        private static readonly int[,] Ref4x4 =
        {
            { 0, 0, 0, 1 },
            { 0, 2, 1, 1 },
            { 2, 2, 1, 3 },
            { 2, 3, 3, 3 },
        };

        private static readonly List<(int row, int col)> SolutionRef4x4 =
            new List<(int row, int col)> { (0, 1), (1, 3), (2, 0), (3, 2) };

        // ------------------------------------------------------------------
        // Entrées.
        // ------------------------------------------------------------------

        [MenuItem("Tools/Zoo Logic/QA/Run Core Gate (log + .md)")]
        public static void RunFromMenu()
        {
            RunAll(saveReport: true);
        }

        /// <summary>Entrée batchmode : ne bloque jamais (exit 0 même si FAIL).</summary>
        public static void RunBatch()
        {
            RunAll(saveReport: true);
        }

        public static void RunAll(bool saveReport)
        {
            _checks.Clear();
            var totalSw = System.Diagnostics.Stopwatch.StartNew();

            SectionLegacy();
            SectionRuleValidator();
            SectionSolver();
            SectionGenerator();
            SectionDifficultyAndConfig();
            SectionPuzzleGrid();
            SectionIdentite();
            SectionAnalytics();
            SectionStarChest();
            SectionProgression();
            SectionSkins();
            SectionLoc();

            totalSw.Stop();
            PrintConsole(totalSw.ElapsedMilliseconds);
            if (saveReport)
                SaveReport(totalSw.ElapsedMilliseconds);
        }

        // ------------------------------------------------------------------
        // Infra de check.
        // ------------------------------------------------------------------

        private static void RunOne(string id, string module, string name, Func<(QAStatus, string)> test)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var (status, detail) = test();
                sw.Stop();
                _checks.Add(new QACheck { Id = id, Module = module, Name = name, Status = status, Ms = sw.ElapsedMilliseconds, Detail = detail ?? "OK" });
            }
            catch (QAFailException ex)
            {
                sw.Stop();
                _checks.Add(new QACheck { Id = id, Module = module, Name = name, Status = QAStatus.Fail, Ms = sw.ElapsedMilliseconds, Detail = "FAIL: " + Truncate(ex.Message, 300) });
            }
            catch (Exception ex)
            {
                sw.Stop();
                _checks.Add(new QACheck { Id = id, Module = module, Name = name, Status = QAStatus.Fail, Ms = sw.ElapsedMilliseconds, Detail = "EXCEPTION: " + ex.GetType().Name + " - " + Truncate(ex.Message, 300) });
            }
        }

        private static void Fail(string message) => throw new QAFailException(message);

        private static void Expect(bool condition, string message)
        {
            if (!condition)
                Fail(message);
        }

        private static void ExpectThrows<T>(Action action, string what) where T : Exception
        {
            try
            {
                action();
            }
            catch (T)
            {
                return;
            }
            catch (Exception ex)
            {
                Fail(what + " : attendu " + typeof(T).Name + ", obtenu " + ex.GetType().Name + " (" + Truncate(ex.Message, 120) + ").");
                return;
            }
            Fail(what + " : attendu " + typeof(T).Name + ", aucune exception levée.");
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max)
                return s ?? "";
            return s.Substring(0, max) + "...";
        }

        private static bool SamePositions(IReadOnlyList<(int row, int col)> a, IReadOnlyList<(int row, int col)> b)
        {
            if (a.Count != b.Count)
                return false;
            foreach (var p in a)
                if (!b.Contains(p))
                    return false;
            return true;
        }

        private static string SnapshotPions(PuzzleGrid g) => string.Join(";", g.Pions);

        // ------------------------------------------------------------------
        // Section 0 : non-régression legacy (informatif, WARN si rouge).
        // ------------------------------------------------------------------

        private static void SectionLegacy()
        {
            RunOne("CORE-00", "Legacy", "PuzzleTests.RunAllTests() historique", () =>
            {
                bool ok;
                try
                {
                    ok = PuzzleTests.RunAllTests();
                }
                catch (Exception ex)
                {
                    return (QAStatus.Warn, "WARN: RunAllTests a levé " + ex.GetType().Name + " - " + Truncate(ex.Message, 200));
                }
                if (!ok)
                    return (QAStatus.Warn, "WARN: au moins un test historique échoue (inclut un test de difficulté aléatoire potentiellement flaky). Voir console stdout.");
                return (QAStatus.Pass, "9 tests historiques OK.");
            });
        }

        // ------------------------------------------------------------------
        // Section A : RuleValidator.
        // ------------------------------------------------------------------

        private static void SectionRuleValidator()
        {
            RunOne("CORE-A01", "RuleValidator", "Grille réf 4x4 résolue + unique", () =>
            {
                var g = new PuzzleGrid(Ref4x4);
                foreach (var (r, c) in SolutionRef4x4)
                    g.PlacePion(r, c);
                Expect(RuleValidator.IsSolved(g), "La solution connue devrait être résolue.");
                Expect(new PuzzleSolver().HasUniqueSolution(new PuzzleGrid(Ref4x4)), "La grille réf devrait avoir une solution unique.");
                return (QAStatus.Pass, "Solution (0,1),(1,3),(2,0),(3,2) résolue + unique.");
            });

            RunOne("CORE-A02", "RuleValidator", "Même ligne refusée", () =>
            {
                var g = new PuzzleGrid(Ref4x4);
                g.PlacePion(0, 1);
                Expect(!RuleValidator.IsValidPlacement(g, 0, 3), "(0,3) même ligne que (0,1) devrait être refusé.");
                g.PlacePion(0, 3);
                Expect(!RuleValidator.IsSolved(g), "2 pions même ligne ne doivent pas être résolus.");
                return (QAStatus.Pass, "OK.");
            });

            RunOne("CORE-A03", "RuleValidator", "Même colonne refusée", () =>
            {
                var g = new PuzzleGrid(Ref4x4);
                g.PlacePion(0, 1);
                Expect(!RuleValidator.IsValidPlacement(g, 3, 1), "(3,1) même colonne que (0,1) devrait être refusé.");
                return (QAStatus.Pass, "OK.");
            });

            RunOne("CORE-A04", "RuleValidator", "Même zone refusée", () =>
            {
                var g = new PuzzleGrid(Ref4x4);
                g.PlacePion(0, 1); // zone 0
                Expect(!RuleValidator.IsValidPlacement(g, 1, 0), "(1,0) même zone 0 devrait être refusé.");
                return (QAStatus.Pass, "OK.");
            });

            RunOne("CORE-A05", "RuleValidator", "Diagonale adjacente refusée, lointaine OK", () =>
            {
                var g = new PuzzleGrid(Ref4x4);
                g.PlacePion(0, 1);
                Expect(!RuleValidator.IsValidPlacement(g, 1, 2), "(1,2) diagonale adjacente devrait être refusée.");
                // (2,3) : dr=2, pas de diagonale adjacente ; zone 3 vs zone 0, ligne/col différentes.
                Expect(RuleValidator.IsValidPlacement(g, 2, 3), "(2,3) à distance 2 devrait être acceptée.");
                return (QAStatus.Pass, "OK.");
            });

            RunOne("CORE-A06", "RuleValidator", "GetConflicts cumulés Row+Zone", () =>
            {
                var g = new PuzzleGrid(Ref4x4);
                g.PlacePion(0, 1); // zone 0
                var conflits = RuleValidator.GetConflicts(g, 0, 0); // même ligne + même zone 0
                Expect(conflits.Contains(ConflictType.Row), "Devrait contenir Row, obtenu [" + string.Join(",", conflits) + "].");
                Expect(conflits.Contains(ConflictType.Zone), "Devrait contenir Zone, obtenu [" + string.Join(",", conflits) + "].");
                Expect(conflits.Count == new HashSet<ConflictType>(conflits).Count, "Liste dédupliquée attendue.");
                return (QAStatus.Pass, "Conflits [" + string.Join(",", conflits) + "].");
            });

            RunOne("CORE-A07", "RuleValidator", "GetConflictingCells positions exactes", () =>
            {
                var g = new PuzzleGrid(Ref4x4);
                g.PlacePion(0, 1);
                var cells = RuleValidator.GetConflictingCells(g, 0, 3);
                Expect(cells.Count == 1 && cells[0].row == 0 && cells[0].col == 1,
                    "Attendu [(0,1)], obtenu [" + string.Join(",", cells) + "].");
                var aucune = RuleValidator.GetConflictingCells(g, 2, 3);
                Expect(aucune.Count == 0, "Aucune cellule en conflit attendue pour (2,3), obtenu " + aucune.Count + ".");
                return (QAStatus.Pass, "OK.");
            });

            RunOne("CORE-A08", "RuleValidator", "Hors-grille sans crash", () =>
            {
                var g = new PuzzleGrid(Ref4x4);
                Expect(!RuleValidator.IsValidPlacement(g, -1, 0), "Hors-grille devrait être invalide.");
                Expect(!RuleValidator.IsValidPlacement(g, 99, 99), "Hors-grille devrait être invalide.");
                Expect(RuleValidator.GetConflicts(g, -1, 0).Count == 0, "GetConflicts hors-grille devrait être vide.");
                Expect(RuleValidator.GetConflictingCells(g, -1, 0).Count == 0, "GetConflictingCells hors-grille devrait être vide.");
                return (QAStatus.Pass, "OK.");
            });

            RunOne("CORE-A09", "RuleValidator", "Case occupée refusée", () =>
            {
                var g = new PuzzleGrid(Ref4x4);
                g.PlacePion(0, 1);
                Expect(!RuleValidator.IsValidPlacement(g, 0, 1), "Case déjà occupée devrait être refusée.");
                return (QAStatus.Pass, "OK.");
            });

            RunOne("CORE-A10", "RuleValidator", "Grille vide non résolue", () =>
            {
                var g = new PuzzleGrid(Ref4x4);
                Expect(!RuleValidator.IsSolved(g), "Grille vide ne doit pas être résolue.");
                g.PlacePion(0, 1);
                Expect(!RuleValidator.IsSolved(g), "Grille partielle (1 pion) ne doit pas être résolue.");
                return (QAStatus.Pass, "OK.");
            });
        }

        // ------------------------------------------------------------------
        // Section B : PuzzleSolver.
        // ------------------------------------------------------------------

        private static void SectionSolver()
        {
            RunOne("CORE-B01", "PuzzleSolver", "FindAll réf = solution exacte", () =>
            {
                var solver = new PuzzleSolver();
                var sols = solver.FindAllSolutions(new PuzzleGrid(Ref4x4));
                Expect(sols.Count == 1, "Attendu 1 solution, obtenu " + sols.Count + ".");
                Expect(SamePositions(sols[0], SolutionRef4x4), "Solution inattendue [" + string.Join(",", sols[0]) + "].");
                return (QAStatus.Pass, "1 solution exacte.");
            });

            RunOne("CORE-B02", "PuzzleSolver", "Multi-solutions zones=colonnes", () =>
            {
                int[,] zones = { { 0, 1, 2, 3 }, { 0, 1, 2, 3 }, { 0, 1, 2, 3 }, { 0, 1, 2, 3 } };
                var solver = new PuzzleSolver();
                var sols = solver.FindAllSolutions(new PuzzleGrid(zones), maxSolutions: 5);
                Expect(sols.Count >= 2, "Attendu >= 2 solutions, obtenu " + sols.Count + ".");
                foreach (var s in sols)
                {
                    var gs = new PuzzleGrid(zones);
                    foreach (var (r, c) in s)
                        gs.PlacePion(r, c);
                    Expect(RuleValidator.IsSolved(gs), "Chaque solution doit être résolue.");
                }
                Expect(!solver.HasUniqueSolution(new PuzzleGrid(zones)), "HasUniqueSolution devrait être false.");
                return (QAStatus.Pass, sols.Count + " solutions, toutes résolues.");
            });

            RunOne("CORE-B03", "PuzzleSolver", "Grille insoluble = 0 solution", () =>
            {
                // Zone 0 : (0,0),(1,1) en diagonale ; zone 1 : (0,1),(1,0). Aucune combinaison valide.
                int[,] zones = { { 0, 1 }, { 1, 0 } };
                var solver = new PuzzleSolver();
                var sols = solver.FindAllSolutions(new PuzzleGrid(zones));
                Expect(sols.Count == 0, "Attendu 0 solution, obtenu " + sols.Count + ".");
                Expect(!solver.HasUniqueSolution(new PuzzleGrid(zones)), "HasUniqueSolution devrait être false.");
                return (QAStatus.Pass, "0 solution comme attendu.");
            });

            RunOne("CORE-B04", "PuzzleSolver", "Grille restaurée après appels", () =>
            {
                var g = new PuzzleGrid(Ref4x4);
                g.PlacePion(0, 1);
                string avant = SnapshotPions(g);
                var solver = new PuzzleSolver();
                solver.FindAllSolutions(g);
                Expect(SnapshotPions(g) == avant, "FindAll a muté la grille.");
                solver.SolveWithFixedPlacements(g, new List<(int, int)>());
                Expect(SnapshotPions(g) == avant, "SolveWithFixedPlacements a muté la grille.");
                return (QAStatus.Pass, "Pions restaurés [" + avant + "].");
            });

            RunOne("CORE-B05", "PuzzleSolver", "maxSolutions > 2 respecté", () =>
            {
                int[,] zones = { { 0, 1, 2, 3 }, { 0, 1, 2, 3 }, { 0, 1, 2, 3 }, { 0, 1, 2, 3 } };
                var solver = new PuzzleSolver();
                var sols = solver.FindAllSolutions(new PuzzleGrid(zones), maxSolutions: 5);
                Expect(sols.Count <= 5, "Devrait couper à 5, obtenu " + sols.Count + ".");
                Expect(sols.Count >= 2, "Attendu >= 2, obtenu " + sols.Count + ".");
                return (QAStatus.Pass, sols.Count + " solutions (cap 5).");
            });

            RunOne("CORE-B06", "PuzzleSolver", "Contrats null / maxSolutions", () =>
            {
                var solver = new PuzzleSolver();
                ExpectThrows<ArgumentNullException>(() => solver.FindAllSolutions(null), "FindAllSolutions(null)");
                ExpectThrows<ArgumentOutOfRangeException>(() => solver.FindAllSolutions(new PuzzleGrid(Ref4x4), 0), "FindAllSolutions(maxSolutions:0)");
                ExpectThrows<ArgumentNullException>(() => solver.SolveWithFixedPlacements(null, new List<(int, int)>()), "SolveWithFixed(null,...)");
                ExpectThrows<ArgumentNullException>(() => solver.SolveWithFixedPlacements(new PuzzleGrid(Ref4x4), null), "SolveWithFixed(...,null)");
                return (QAStatus.Pass, "4 contrats OK.");
            });

            RunOne("CORE-B07", "PuzzleSolver", "Fixes : complète, vide, conflit, hors-grille", () =>
            {
                var solver = new PuzzleSolver();
                var sol = solver.SolveWithFixedPlacements(new PuzzleGrid(Ref4x4), new List<(int, int)> { (0, 1), (1, 3) });
                Expect(sol != null && SamePositions(sol, SolutionRef4x4), "2 fixes corrects devraient compléter la solution réf.");
                var solVide = solver.SolveWithFixedPlacements(new PuzzleGrid(Ref4x4), new List<(int, int)>());
                Expect(solVide != null, "Liste vide devrait quand même trouver une solution.");
                var conflit = solver.SolveWithFixedPlacements(new PuzzleGrid(Ref4x4), new List<(int, int)> { (0, 1), (0, 3) });
                Expect(conflit == null, "Fixes en conflit devraient retourner null.");
                try { solver.SolveWithFixedPlacements(new PuzzleGrid(Ref4x4), new List<(int, int)> { (99, 99) }); }
                catch { return (QAStatus.Pass, "Complète + vide + conflit(null) + hors-grille lève (contrat)."); }
                Fail("Fixe hors-grille : attendu une exception, aucune levée.");
                return (QAStatus.Pass, "Inatteignable.");
            });
        }

        // ------------------------------------------------------------------
        // Section C : LevelGenerator.
        // ------------------------------------------------------------------

        /// <summary>
        /// Pipeline HISTORIQUE exact (pré-filet) : seed = numéro de niveau,
        /// GenerateLevel puis fallback GenerateUniqueGrid SANS try/catch.
        /// NE PAS MODIFIER : punaise le comportement legacy pour le check
        /// d'identité CORE-F01 (grilles figées).
        /// </summary>
        private static PuzzleGrid PipelineLegacy(int seedBase, int size, int difficulty)
        {
            var generator = new LevelGenerator(seed: seedBase);
            try
            {
                PuzzleGrid grid = generator.GenerateLevel(size, difficulty);
                if (grid != null)
                    return grid;
            }
            catch (Exception) { }
            return generator.GenerateUniqueGrid(size);
        }

        /// <summary>Chemin réel du jeu (filet résilient du contrôleur).</summary>
        private static PuzzleGrid PipelineResilient(int seedBase, int size, int difficulty)
            => Zoologic.PuzzleGameController.GenererNiveauResilient(seedBase, size, difficulty);

        private static string HashGrille(PuzzleGrid g)
        {
            unchecked
            {
                ulong h = 1469598103934665603UL;
                h = (h ^ (ulong)g.Size) * 1099511628211UL;
                for (int r = 0; r < g.Size; r++)
                    for (int c = 0; c < g.Size; c++)
                        h = (h ^ (ulong)(g.GetRegionId(r, c) + 1)) * 1099511628211UL;
                return h.ToString("X16");
            }
        }

        private static void VerifierGrilleUniqueSolvable(PuzzleGrid g, string contexte)
        {
            var solver = new PuzzleSolver();
            var sols = solver.FindAllSolutions(g, 2);
            if (sols.Count != 1)
                Fail(contexte + " : attendu 1 solution unique, obtenu " + sols.Count + ".");
            var place = new PuzzleGrid(RegionCopy(g));
            foreach (var (r, c) in sols[0])
                place.PlacePion(r, c);
            if (!RuleValidator.IsSolved(place))
                Fail(contexte + " : la solution du solver ne résout pas la grille.");
        }

        private static int[,] RegionCopy(PuzzleGrid g)
        {
            var arr = new int[g.Size, g.Size];
            for (int r = 0; r < g.Size; r++)
                for (int c = 0; c < g.Size; c++)
                    arr[r, c] = g.GetRegionId(r, c);
            return arr;
        }

        private static void SectionGenerator()
        {
            RunOne("CORE-C01", "LevelGenerator", "Résilient niveaux 1..40 (4x4→6x6)", () =>
            {
                var fails = new List<string>();
                long pire = 0;
                int pireNiveau = 0;
                for (int level = 1; level <= 40; level++)
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    try
                    {
                        var g = PipelineResilient(level, LevelConfig.GetGridSize(level), LevelConfig.GetTargetDifficulty(level));
                        if (g == null)
                            fails.Add("niv." + level + " null");
                        else
                            VerifierGrilleUniqueSolvable(g, "niv." + level);
                    }
                    catch (QAFailException ex) { fails.Add(ex.Message); }
                    catch (Exception ex) { fails.Add("niv." + level + " : " + ex.GetType().Name + " (crash déterministe en jeu)"); }
                    sw.Stop();
                    if (sw.ElapsedMilliseconds > pire) { pire = sw.ElapsedMilliseconds; pireNiveau = level; }
                    if (fails.Count >= 5)
                        break;
                }
                if (fails.Count > 0)
                    Fail(fails.Count + " niveau(x) en échec : " + string.Join(" | ", fails.Take(5).ToArray()));
                string d = "40/40 niveaux OK (uniques + solvables), pire " + pire + "ms (niv." + pireNiveau + ").";
                return pire > 5000 ? (QAStatus.Warn, "WARN perf : " + d) : (QAStatus.Pass, d);
            });

            RunOne("CORE-C02", "LevelGenerator", "Résilient 7x7 + 8x8 échantillonnés", () =>
            {
                int[] niveaux = { 41, 45, 50, 55, 60, 65, 70, 75, 80, 81, 85, 90, 95, 100, 110, 120, 150, 200 };
                var fails = new List<string>();
                foreach (int level in niveaux)
                {
                    try
                    {
                        var g = PipelineResilient(level, LevelConfig.GetGridSize(level), LevelConfig.GetTargetDifficulty(level));
                        if (g == null)
                            fails.Add("niv." + level + " null");
                        else
                            VerifierGrilleUniqueSolvable(g, "niv." + level);
                    }
                    catch (QAFailException ex) { fails.Add(ex.Message); }
                    catch (Exception ex) { fails.Add("niv." + level + " : " + ex.GetType().Name + " (crash déterministe en jeu)"); }
                    if (fails.Count >= 5)
                        break;
                }
                if (fails.Count > 0)
                    Fail(fails.Count + " niveau(x) en échec : " + string.Join(" | ", fails.Take(5).ToArray()));
                return (QAStatus.Pass, niveaux.Length + "/" + niveaux.Length + " niveaux 7x7/8x8 OK (uniques + solvables).");
            });

            RunOne("CORE-C03", "LevelGenerator", "Daily puzzle 2026-2027 (730 jours, 5x5)", () =>
            {
                var solver = new PuzzleSolver();
                var fails = new List<string>();
                int testes = 0;
                DateTime jour = new DateTime(2026, 1, 1);
                DateTime fin = new DateTime(2027, 12, 31);
                while (jour <= fin)
                {
                    string cle = jour.ToString("yyyy-MM-dd");
                    int seed = jour.Year * 10000 + jour.Month * 100 + jour.Day;
                    try
                    {
                        var g = PipelineResilient(seed, 5, 3);
                        if (g == null)
                            fails.Add(cle + " null");
                        else if (!solver.HasUniqueSolution(g))
                            fails.Add(cle + " non-unique");
                    }
                    catch (Exception ex) { fails.Add(cle + " : " + ex.GetType().Name + " (daily crash ce jour-là)"); }
                    testes++;
                    jour = jour.AddDays(1);
                    if (fails.Count >= 5)
                        break;
                }
                if (fails.Count > 0)
                    Fail(fails.Count + " jour(s) en échec : " + string.Join(" | ", fails.Take(5).ToArray()));
                return (QAStatus.Pass, testes + " jours OK (uniques).");
            });

            RunOne("CORE-C04", "LevelGenerator", "Rendement brut unicité (informatif, jamais FAIL)", () =>
            {
                var solver = new PuzzleSolver();
                var sb = new StringBuilder();
                foreach (int size in new[] { 4, 5, 6, 7, 8 })
                {
                    var gen = new LevelGenerator();
                    int u = 0, n = 24;
                    for (int i = 0; i < n; i++)
                        if (solver.HasUniqueSolution(gen.GenerateRawGrid(size)))
                            u++;
                    sb.Append(size + "x" + size + ":" + u + "/" + n + " ");
                }
                return (QAStatus.Pass, "Rendement brut [" + sb.ToString().Trim() + "] — faible à grande taille, compensé par 20x50 tentatives (cf. C01-C03 déterministes).");
            });

            RunOne("CORE-C05", "LevelGenerator", "Zones contiguës (8-dir FAIL, 4-dir WARN)", () =>
            {
                var coinsWarn = new List<string>();
                foreach (int level in new[] { 2, 10, 25, 60, 100 })
                {
                    PuzzleGrid g;
                    try { g = PipelineResilient(level, LevelConfig.GetGridSize(level), LevelConfig.GetTargetDifficulty(level)); }
                    catch (Exception ex) { return (QAStatus.Warn, "WARN: niv." + level + " non testé, pipeline en échec (" + ex.GetType().Name + ", cf. C01/C02)."); }
                    if (g == null)
                        return (QAStatus.Warn, "WARN: niv." + level + " non testé, pipeline null (cf. C01/C02).");
                    Expect(AllZonesContiguous(g, 8), "Zone 8-disconnectée détectée (niv." + level + ") : défaut structurel du générateur.");
                    if (!AllZonesContiguous(g, 4))
                        coinsWarn.Add("niv." + level);
                }
                if (coinsWarn.Count > 0)
                    return (QAStatus.Warn, "WARN qualité visuelle : zones reliées en coin uniquement (4-disconnectées) : " + string.Join(",", coinsWarn.ToArray()) + " — jouable, rendu à vérifier.");
                return (QAStatus.Pass, "Niveaux 2/10/25/60/100 contigus (4- et 8-voisins).");
            });

            RunOne("CORE-C06", "LevelGenerator", "Tailles de zones équilibrées", () =>
            {
                var tailles = new List<int>();
                foreach (int level in new[] { 10, 25, 60 })
                {
                    PuzzleGrid g;
                    try { g = PipelineResilient(level, LevelConfig.GetGridSize(level), LevelConfig.GetTargetDifficulty(level)); }
                    catch { return (QAStatus.Warn, "WARN: niv." + level + " non testé, pipeline en échec (cf. C01/C02)."); }
                    if (g == null)
                        return (QAStatus.Warn, "WARN: niv." + level + " non testé, pipeline null (cf. C01/C02).");
                    tailles.AddRange(ZoneSizes(g));
                }
                double moy = 0;
                foreach (int t in tailles)
                    moy += t;
                moy /= tailles.Count;
                int max = 0;
                foreach (int t in tailles)
                    max = Math.Max(max, t);
                if (max > 2.0 * moy + 2)
                    return (QAStatus.Warn, "WARN: zone max " + max + " pour moyenne " + moy.ToString("F1") + " (relaxation au-delà de 2x admise).");
                return (QAStatus.Pass, "max " + max + " / moy " + moy.ToString("F1") + " (niv.10/25/60).");
            });

            RunOne("CORE-C07", "LevelGenerator", "Reproductibilité même seed", () =>
            {
                var a = new LevelGenerator(seed: 999).GenerateUniqueGrid(5);
                var b = new LevelGenerator(seed: 999).GenerateUniqueGrid(5);
                for (int r = 0; r < 5; r++)
                    for (int c = 0; c < 5; c++)
                        Expect(a.GetRegionId(r, c) == b.GetRegionId(r, c), "Divergence en (" + r + "," + c + ") pour même seed.");
                return (QAStatus.Pass, "2 grilles 5x5 identiques avec seed 999.");
            });

            RunOne("CORE-C08", "LevelGenerator", "Contrats size / targetDifficulty", () =>
            {
                var gen = new LevelGenerator(seed: 1);
                ExpectThrows<ArgumentOutOfRangeException>(() => gen.GenerateUniqueGrid(2), "GenerateUniqueGrid(2)");
                ExpectThrows<ArgumentOutOfRangeException>(() => gen.GenerateRawGrid(2), "GenerateRawGrid(2)");
                ExpectThrows<ArgumentOutOfRangeException>(() => gen.GenerateLevel(4, 0), "GenerateLevel(target:0)");
                ExpectThrows<ArgumentOutOfRangeException>(() => gen.GenerateLevel(4, 4), "GenerateLevel(target:4)");
                ExpectThrows<ArgumentOutOfRangeException>(() => gen.GenerateLevel(2, 2), "GenerateLevel(size:2)");
                var g = gen.GenerateLevel(4, 2);
                if (g == null)
                    return (QAStatus.Warn, "WARN: GenerateLevel(4,2) a retourné null (0 succès en 20 tentatives, cas rare admis).");
                int score = DifficultyScorer.ScoreDifficulty(g);
                Expect(score >= 1 && score <= 3, "Score hors bornes : " + score + ".");
                return (QAStatus.Pass, "5 contrats OK, GenerateLevel(4,2) score " + score + ".");
            });

            RunOne("CORE-C09", "Banque", "Bank8x8 intègre (uniques + 4-connexes)", () =>
            {
                var asset = UnityEngine.Resources.Load<UnityEngine.TextAsset>("Bank8x8");
                Expect(asset != null && !string.IsNullOrEmpty(asset.text), "Bank8x8 introuvable ou vide dans Resources.");
                var solver = new PuzzleSolver();
                int n = 0;
                var scores = new HashSet<int>();
                foreach (string brut in asset.text.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string ligne = brut.Trim();
                    if (ligne.Length == 0 || ligne[0] == '#')
                        continue;
                    Expect(ligne.Length == 66 && ligne[1] == ':' && ligne[0] >= '1' && ligne[0] <= '3',
                        "Ligne banque malformée : \"" + Truncate(ligne, 20) + "\".");
                    var zones = new int[8, 8];
                    for (int i = 0; i < 64; i++)
                    {
                        char c = ligne[2 + i];
                        Expect(c >= '0' && c <= '7', "Zone invalide dans la banque (ligne " + (n + 1) + ").");
                        zones[i / 8, i % 8] = c - '0';
                    }
                    var g = new PuzzleGrid(zones);
                    Expect(solver.HasUniqueSolution(g), "Entrée banque non-unique (ligne " + (n + 1) + ").");
                    Expect(AllZonesContiguous(g, 4), "Entrée banque 4-disconnectée (ligne " + (n + 1) + ").");
                    scores.Add(ligne[0] - '0');
                    n++;
                }
                Expect(n >= 128, "Banque trop petite : " + n + " entrées.");
                return (QAStatus.Pass, n + " entrées uniques 4-connexes, scores [" + string.Join(",", scores) + "].");
            });
        }

        private static List<int> ZoneSizes(PuzzleGrid g)
        {
            var counts = new Dictionary<int, int>();
            for (int r = 0; r < g.Size; r++)
                for (int c = 0; c < g.Size; c++)
                {
                    int z = g.GetRegionId(r, c);
                    counts.TryGetValue(z, out int n);
                    counts[z] = n + 1;
                }
            return new List<int>(counts.Values);
        }

        private static bool AllZonesContiguous(PuzzleGrid g, int voisins)
        {
            int size = g.Size;
            int[] drs = voisins == 4
                ? new[] { -1, 1, 0, 0 }
                : new[] { -1, -1, -1, 0, 0, 1, 1, 1 };
            int[] dcs = voisins == 4
                ? new[] { 0, 0, -1, 1 }
                : new[] { -1, 0, 1, -1, 1, -1, 0, 1 };
            var ids = g.GetRegionIds();
            foreach (int z in ids)
            {
                int total = 0, sr = -1, sc = -1;
                for (int r = 0; r < size; r++)
                    for (int c = 0; c < size; c++)
                        if (g.GetRegionId(r, c) == z)
                        {
                            total++;
                            if (sr == -1) { sr = r; sc = c; }
                        }
                if (total == 0)
                    return false;
                var seen = new bool[size, size];
                var q = new Queue<(int, int)>();
                q.Enqueue((sr, sc));
                seen[sr, sc] = true;
                int reached = 1;
                while (q.Count > 0)
                {
                    var (r, c) = q.Dequeue();
                    for (int d = 0; d < drs.Length; d++)
                    {
                        int nr = r + drs[d], nc = c + dcs[d];
                        if (nr < 0 || nr >= size || nc < 0 || nc >= size || seen[nr, nc])
                            continue;
                        if (g.GetRegionId(nr, nc) != z)
                            continue;
                        seen[nr, nc] = true;
                        q.Enqueue((nr, nc));
                        reached++;
                    }
                }
                if (reached != total)
                    return false;
            }
            return true;
        }

        // ------------------------------------------------------------------
        // Section D : DifficultyScorer + LevelConfig.
        // ------------------------------------------------------------------

        private static void SectionDifficultyAndConfig()
        {
            RunOne("CORE-D01", "DifficultyScorer", "Score réf dans [1,3] + Analyse", () =>
            {
                var g = new PuzzleGrid(Ref4x4);
                int score = DifficultyScorer.ScoreDifficulty(g);
                Expect(score >= 1 && score <= 3, "Score hors bornes : " + score + ".");
                var (cout, resolu) = DifficultyScorer.AnalyseResolution(g);
                Expect(resolu, "La grille réf devrait être résolue par le simulateur.");
                Expect(cout != int.MaxValue && cout >= 0, "Coût inattendu : " + cout + ".");
                return (QAStatus.Pass, "score " + score + ", coût " + cout + ".");
            });

            RunOne("CORE-D02", "DifficultyScorer", "Grille non-unique documentée (MaxValue→3)", () =>
            {
                int[,] zones = { { 0, 1, 2, 3 }, { 0, 1, 2, 3 }, { 0, 1, 2, 3 }, { 0, 1, 2, 3 } };
                var g = new PuzzleGrid(zones);
                var (cout, resolu) = DifficultyScorer.AnalyseResolution(g);
                Expect(!resolu && cout == int.MaxValue, "Attendu (MaxValue,false) pour grille non-unique, obtenu (" + cout + "," + resolu + ").");
                Expect(DifficultyScorer.ScoreDifficulty(g) == 3, "Grille non-unique devrait scorer 3 (comportement actuel documenté).");
                return (QAStatus.Pass, "Non-unique → (MaxValue,false) → score 3 (documenté).");
            });

            RunOne("CORE-D03", "DifficultyScorer", "Contrat null", () =>
            {
                ExpectThrows<ArgumentNullException>(() => DifficultyScorer.ScoreDifficulty(null), "ScoreDifficulty(null)");
                ExpectThrows<ArgumentNullException>(() => DifficultyScorer.AnalyseResolution(null), "AnalyseResolution(null)");
                return (QAStatus.Pass, "2 contrats OK.");
            });

            RunOne("CORE-D04", "LevelConfig", "Seuils tailles 3/15/40/80", () =>
            {
                Expect(LevelConfig.GetGridSize(1) == 4, "niv.1 → 4.");
                Expect(LevelConfig.GetGridSize(3) == 4, "niv.3 → 4.");
                Expect(LevelConfig.GetGridSize(4) == 5, "niv.4 → 5.");
                Expect(LevelConfig.GetGridSize(15) == 5, "niv.15 → 5.");
                Expect(LevelConfig.GetGridSize(16) == 6, "niv.16 → 6.");
                Expect(LevelConfig.GetGridSize(40) == 6, "niv.40 → 6.");
                Expect(LevelConfig.GetGridSize(41) == 7, "niv.41 → 7.");
                Expect(LevelConfig.GetGridSize(80) == 7, "niv.80 → 7.");
                Expect(LevelConfig.GetGridSize(81) == 8, "niv.81 → 8.");
                Expect(LevelConfig.GetGridSize(200) == 8, "niv.200 → 8.");
                return (QAStatus.Pass, "10 seuils OK.");
            });

            RunOne("CORE-D05", "LevelConfig", "Difficulté modulo 3/5/15", () =>
            {
                Expect(LevelConfig.GetTargetDifficulty(5) == 1, "niv.5 → 1.");
                Expect(LevelConfig.GetTargetDifficulty(10) == 1, "niv.10 → 1.");
                Expect(LevelConfig.GetTargetDifficulty(15) == 1, "niv.15 → 1 (priorité %5).");
                Expect(LevelConfig.GetTargetDifficulty(3) == 3, "niv.3 → 3.");
                Expect(LevelConfig.GetTargetDifficulty(6) == 3, "niv.6 → 3.");
                Expect(LevelConfig.GetTargetDifficulty(9) == 3, "niv.9 → 3.");
                Expect(LevelConfig.GetTargetDifficulty(7) == 2, "niv.7 → 2.");
                Expect(LevelConfig.GetTargetDifficulty(1) == 2, "niv.1 → 2.");
                return (QAStatus.Pass, "8 cas OK.");
            });

            RunOne("CORE-D06", "LevelGenerator", "GenerateLevel(4) scores bornés", () =>
            {
                var gen = new LevelGenerator(seed: 2026);
                foreach (int target in new[] { 1, 2, 3 })
                {
                    var g = gen.GenerateLevel(4, target);
                    if (g == null)
                        return (QAStatus.Warn, "WARN: GenerateLevel(4," + target + ") null (rare, admis).");
                    int s = DifficultyScorer.ScoreDifficulty(g);
                    Expect(s >= 1 && s <= 3, "Score " + s + " hors bornes pour target " + target + ".");
                }
                return (QAStatus.Pass, "Targets 1/2/3 bornés [1,3].");
            });
        }

        // ------------------------------------------------------------------
        // Section E : PuzzleGrid contrats.
        // ------------------------------------------------------------------

        private static void SectionPuzzleGrid()
        {
            RunOne("CORE-E01", "PuzzleGrid", "Constructeur null", () =>
            {
                ExpectThrows<ArgumentNullException>(() => new PuzzleGrid(null), "new PuzzleGrid(null)");
                return (QAStatus.Pass, "OK.");
            });

            RunOne("CORE-E02", "PuzzleGrid", "Tableau non-carré rejeté", () =>
            {
                ExpectThrows<ArgumentException>(() => new PuzzleGrid(new int[2, 3]), "new PuzzleGrid(2x3)");
                return (QAStatus.Pass, "OK.");
            });

            RunOne("CORE-E03", "PuzzleGrid", "Double PlacePion rejeté", () =>
            {
                var g = new PuzzleGrid(Ref4x4);
                g.PlacePion(0, 1);
                ExpectThrows<InvalidOperationException>(() => g.PlacePion(0, 1), "PlacePion double");
                return (QAStatus.Pass, "OK.");
            });

            RunOne("CORE-E04", "PuzzleGrid", "Hors-bornes lèvent", () =>
            {
                var g = new PuzzleGrid(Ref4x4);
                ExpectThrows<ArgumentOutOfRangeException>(() => g.PlacePion(-1, 0), "PlacePion(-1,0)");
                ExpectThrows<ArgumentOutOfRangeException>(() => g.RemovePion(0, 99), "RemovePion(0,99)");
                try { g.GetRegionId(-1, 0); }
                catch (Exception ex)
                {
                    return (QAStatus.Pass, "Place/Remove → ArgumentOutOfRange ; GetRegionId → " + ex.GetType().Name + " (indexeur brut, documenté).");
                }
                Fail("GetRegionId(-1,0) : attendu une exception, aucune levée.");
                return (QAStatus.Pass, "Inatteignable.");
            });

            RunOne("CORE-E05", "PuzzleGrid", "Clear + Remove idempotent", () =>
            {
                var g = new PuzzleGrid(Ref4x4);
                g.PlacePion(0, 1);
                g.PlacePion(1, 3);
                g.RemovePion(0, 1);
                Expect(!g.HasPion(0, 1) && g.HasPion(1, 3), "RemovePion sélectif.");
                g.RemovePion(0, 1); // absent : no-op attendu
                g.Clear();
                Expect(g.Pions.Count == 0, "Clear devrait vider.");
                Expect(g.GetRegionIds().Count == 4, "4 zones attendues sur la réf.");
                return (QAStatus.Pass, "OK.");
            });
        }

        // ------------------------------------------------------------------
        // Section F : non-régression grilles figées (étage 1 legacy).
        // ------------------------------------------------------------------

        private static void SectionIdentite()
        {
            RunOne("CORE-F01", "NonRegression", "Grilles figées identiques (90 niv + 30 daily)", () =>
            {
                string path = System.IO.Path.Combine(UnityEngine.Application.dataPath, "Editor/QAReferenceGrids.txt");
                if (!System.IO.File.Exists(path))
                    Fail("Référence introuvable : " + path + " (générer via QARefDump sur code legacy).");

                int ok = 0, total = 0;
                var diffs = new List<string>();
                foreach (string raw in System.IO.File.ReadAllLines(path))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#"))
                        continue;
                    int eq = line.IndexOf('=');
                    if (eq < 0)
                        continue;
                    string key = line.Substring(0, eq);
                    string attendu = line.Substring(eq + 1);
                    total++;

                    try
                    {
                        PuzzleGrid g;
                        if (key[0] == 'L')
                        {
                            int level = int.Parse(key.Substring(1));
                            g = PipelineLegacy(level, LevelConfig.GetGridSize(level), LevelConfig.GetTargetDifficulty(level));
                        }
                        else
                        {
                            g = PipelineLegacy(int.Parse(key.Substring(1)), 5, 3);
                        }
                        if (g != null && HashGrille(g) == attendu)
                            ok++;
                        else
                            diffs.Add(key);
                    }
                    catch
                    {
                        diffs.Add(key + "!");
                    }
                }

                if (diffs.Count > 0)
                    Fail(diffs.Count + "/" + total + " grilles ont changé : " + string.Join(",", diffs.Take(8).ToArray()) + " — chemin legacy altéré !");
                return (QAStatus.Pass, total + "/" + total + " grilles figées identiques.");
            });
        }

        private static void SectionAnalytics()
        {
            RunOne("ANALYTICS-01", "Analytics", "Manager no-op sans SDK (pas de throw)", () =>
            {
                // Sans le SDK Firebase importé, tout doit être no-op silencieux.
                bool available = Zoologic.AnalyticsManager.IsAvailable;
                Zoologic.AnalyticsManager.Initialize();
                Zoologic.AnalyticsManager.OnConsentResolved();
                Zoologic.AnalyticsManager.RefreshConsent();
                Zoologic.AnalyticsManager.SetCollectionEnabled(true);
                Zoologic.AnalyticsManager.SetCollectionEnabled(false);
                Zoologic.AnalyticsManager.ResetData();
                Zoologic.AnalyticsManager.LogLevelStart(1, 4, false);
                Zoologic.AnalyticsManager.LogLevelEnd(1, true);
                Zoologic.AnalyticsManager.LogTutorialBegin();
                Zoologic.AnalyticsManager.LogTutorialComplete();
                Zoologic.AnalyticsManager.LogHintUsed(1);
                Zoologic.AnalyticsManager.LogDailyClaim(3);
                Zoologic.AnalyticsManager.LogAdReward("hints");
                if (available)
                    return (QAStatus.Warn, "WARN: SDK Firebase présent — checks no-op non applicables, vérif DebugView requise.");
                return (QAStatus.Pass, "No-op sans SDK vérifié (IsAvailable=false).");
            });
        }

        // ------------------------------------------------------------------
        // Isolation PlayerPrefs : snapshot/restaure les clés touchées par les
        // tests méta-progression (le batch Éditeur persiste sur disque).
        // ------------------------------------------------------------------

        private static System.Action SnapshotPrefs(
            IEnumerable<string> intKeys, IEnumerable<string> stringKeys)
        {
            var ints = new Dictionary<string, KeyValuePair<bool, int>>();
            foreach (string k in intKeys)
                ints[k] = new KeyValuePair<bool, int>(PlayerPrefs.HasKey(k), PlayerPrefs.GetInt(k, 0));
            var strs = new Dictionary<string, KeyValuePair<bool, string>>();
            foreach (string k in stringKeys)
                strs[k] = new KeyValuePair<bool, string>(PlayerPrefs.HasKey(k), PlayerPrefs.GetString(k, ""));
            return () =>
            {
                foreach (var kv in ints)
                {
                    if (kv.Value.Key) PlayerPrefs.SetInt(kv.Key, kv.Value.Value);
                    else PlayerPrefs.DeleteKey(kv.Key);
                }
                foreach (var kv in strs)
                {
                    if (kv.Value.Key) PlayerPrefs.SetString(kv.Key, kv.Value.Value);
                    else PlayerPrefs.DeleteKey(kv.Key);
                }
                PlayerPrefs.Save();
            };
        }

        private static List<string> StarKeysAll()
        {
            var keys = new List<string> { "highest_unlocked" };
            for (int lvl = 1; lvl <= 100; lvl++)
                keys.Add("stars_level_" + lvl);
            return keys;
        }

        private static List<string> ChestKeysAll()
        {
            var keys = new List<string> { "Mascot_PityEpic" };
            for (int w = 0; w < 4; w++)
                for (int p = 0; p < 3; p++)
                    keys.Add("StarChest_W" + w + "_P" + p);
            for (int i = 0; i < 10; i++)
                keys.Add("Mascot_Owned_mx" + i);
            return keys;
        }

        private static readonly List<string> EconKeys =
            new List<string> { "player_coins", "hint_stock" };

        private static readonly List<string> SkinIntKeys =
            new List<string> { "skin_selected" };

        private static readonly List<string> SkinStringKeys =
            new List<string> { "skin_owned" };

        private static void ClearMascots()
        {
            for (int i = 0; i < 10; i++)
                PlayerPrefs.DeleteKey("Mascot_Owned_mx" + i);
            PlayerPrefs.DeleteKey("Mascot_PityEpic");
        }

        // ------------------------------------------------------------------
        // Section StarChest (P1) : paliers, pity, prix, exécution, monde 2.
        // ------------------------------------------------------------------

        private static void SectionStarChest()
        {
            RunOne("CHEST-WORLD", "StarChest", "WorldOf bornes + StarsOfWorld", () =>
            {
                var restore = SnapshotPrefs(StarKeysAll(), new List<string>());
                try
                {
                    Expect(Zoologic.StarChestManager.WorldOf(1) == 0, "niv.1 → monde 0.");
                    Expect(Zoologic.StarChestManager.WorldOf(25) == 0, "niv.25 → monde 0.");
                    Expect(Zoologic.StarChestManager.WorldOf(26) == 1, "niv.26 → monde 1.");
                    Expect(Zoologic.StarChestManager.WorldOf(100) == 3, "niv.100 → monde 3.");
                    for (int lvl = 1; lvl <= 5; lvl++)
                        Zoologic.LevelProgressManager.SetStars(lvl, 3);
                    Expect(Zoologic.StarChestManager.StarsOfWorld(0) == 15, "5×3⭐ monde 0 = 15.");
                    Expect(Zoologic.StarChestManager.TotalStars() == 15, "TotalStars = 15.");
                    return (QAStatus.Pass, "Bornes mondes + sommes OK.");
                }
                finally { restore(); }
            });

            RunOne("CHEST-READY", "StarChest", "IsReady seuils + claim", () =>
            {
                var restore = SnapshotPrefs(StarKeysAll().Concat(ChestKeysAll()).ToList(), new List<string>());
                try
                {
                    for (int lvl = 26; lvl <= 30; lvl++)
                        Zoologic.LevelProgressManager.SetStars(lvl, 1);
                    Expect(!Zoologic.StarChestManager.IsReady(1, 0), "5⭐ < 10 : pas prêt.");
                    for (int lvl = 31; lvl <= 35; lvl++)
                        Zoologic.LevelProgressManager.SetStars(lvl, 1);
                    Expect(Zoologic.StarChestManager.IsReady(1, 0), "10⭐ : prêt P0.");
                    Expect(!Zoologic.StarChestManager.IsReady(1, 2), "10⭐ < 50 : P2 pas prêt.");
                    Zoologic.StarChestManager.SetClaimed(1, 0);
                    Expect(Zoologic.StarChestManager.IsClaimed(1, 0), "Claim enregistré.");
                    Expect(!Zoologic.StarChestManager.IsReady(1, 0), "Réclamé : plus prêt.");
                    return (QAStatus.Pass, "Seuils 10/25/50 + claim OK.");
                }
                finally { restore(); }
            });

            RunOne("CHEST-PREVIEW", "StarChest", "Preview exact + pur (sans effet)", () =>
            {
                var restore = SnapshotPrefs(
                    StarKeysAll().Concat(ChestKeysAll()).Concat(EconKeys).ToList(), new List<string>());
                try
                {
                    int c0 = Zoologic.CurrencyManager.GetCoins();
                    int h0 = Zoologic.HintStockManager.Get();
                    var p0 = Zoologic.StarChestManager.Preview(0, 0);
                    var p1 = Zoologic.StarChestManager.Preview(0, 1);
                    var p2 = Zoologic.StarChestManager.Preview(0, 2);
                    Expect(p0.Coins == 30 && p0.Hints == 1 && p0.MascotIndex == -1, "P0 = 30c+1.");
                    Expect(p1.Coins == 60 && p1.Hints == 1, "P1 = 60c+1.");
                    Expect(p2.Coins == 100 && p2.Hints == 2, "P2 = 100c+2.");
                    Expect(Zoologic.CurrencyManager.GetCoins() == c0
                        && Zoologic.HintStockManager.Get() == h0, "Preview sans effet.");
                    return (QAStatus.Pass, "P0/P1/P2 exacts, zéro effet de bord.");
                }
                finally { restore(); }
            });

            RunOne("CHEST-EXEC0", "StarChest", "Execute P0 + overflow + idempotence", () =>
            {
                var restore = SnapshotPrefs(
                    StarKeysAll().Concat(ChestKeysAll()).Concat(EconKeys).ToList(), new List<string>());
                try
                {
                    for (int lvl = 76; lvl <= 80; lvl++)
                        Zoologic.LevelProgressManager.SetStars(lvl, 2);
                    Zoologic.HintStockManager.Set(Zoologic.HintStockManager.MaxStock);
                    int c0 = Zoologic.CurrencyManager.GetCoins();
                    var g = Zoologic.StarChestManager.Execute(3, 0);
                    Expect(g.Coins == 50 && g.Hints == 0,
                        "Stock plein : 30 + 20 overflow = 50c, 0 indice (obtenu " + g.Coins + "c/" + g.Hints + ").");
                    Expect(Zoologic.StarChestManager.IsClaimed(3, 0), "Claim enregistré.");
                    Expect(Zoologic.CurrencyManager.GetCoins() == c0 + 50, "Cagnotte +50.");
                    var g2 = Zoologic.StarChestManager.Execute(3, 0);
                    Expect(g2.Coins == 0 && g2.Hints == 0, "2e claim : zéro (anti double).");
                    Expect(Zoologic.CurrencyManager.GetCoins() == c0 + 50, "Pas de double crédit.");
                    return (QAStatus.Pass, "P0 overflow + idempotence OK.");
                }
                finally { restore(); }
            });

            RunOne("CHEST-EXEC1", "StarChest", "Execute P1 + mascotte cohérente", () =>
            {
                var restore = SnapshotPrefs(
                    StarKeysAll().Concat(ChestKeysAll()).Concat(EconKeys).ToList(), new List<string>());
                try
                {
                    for (int lvl = 1; lvl <= 9; lvl++)
                        Zoologic.LevelProgressManager.SetStars(lvl, 3);
                    ClearMascots();
                    Zoologic.HintStockManager.Set(1);
                    UnityEngine.Random.InitState(1234);
                    var g = Zoologic.StarChestManager.Execute(0, 1);
                    Expect(g.Coins == 60 && g.Hints == 1, "60c+1 sans overflow (obtenu " + g.Coins + "c/" + g.Hints + ").");
                    Expect(Zoologic.HintStockManager.Get() == 2, "Stock 1 → 2.");
                    if (g.MascotIndex >= 0)
                    {
                        Expect(Zoologic.StarChestManager.OwnsMascot(g.MascotIndex), "Mascotte possédée.");
                        Expect(Zoologic.StarChestManager.RarityOf(g.MascotIndex) == g.MascotRarity, "Rareté cohérente.");
                    }
                    else
                    {
                        Expect(g.DuplicateCoins == 40 || g.DuplicateCoins == 100,
                            "Doublon converti (obtenu " + g.DuplicateCoins + ").");
                    }
                    return (QAStatus.Pass, "P1 mascotte cohérente, stock OK.");
                }
                finally { restore(); }
            });

            RunOne("CHEST-PITY", "StarChest", "Pity Épique à 8 + doublon", () =>
            {
                var restore = SnapshotPrefs(ChestKeysAll().Concat(EconKeys).ToList(), new List<string>());
                try
                {
                    ClearMascots();
                    PlayerPrefs.SetInt("Mascot_PityEpic", 8);
                    UnityEngine.Random.InitState(777);
                    string rarity;
                    int dup;
                    int pick = Zoologic.StarChestManager.RollMascot(false, out rarity, out dup);
                    Expect(rarity == "E", "Pity 8 force E (obtenu " + rarity + ").");
                    Expect(pick == 6 && Zoologic.StarChestManager.OwnsMascot(6), "mx6 attribué.");
                    Expect(PlayerPrefs.GetInt("Mascot_PityEpic", -1) == 0, "Pity reset à 0.");
                    PlayerPrefs.SetInt("Mascot_PityEpic", 8);
                    int pick2 = Zoologic.StarChestManager.RollMascot(false, out string rarity2, out int dup2);
                    Expect(pick2 == -1 && dup2 == 200, "E épuisé → doublon 200 (obtenu " + dup2 + ").");
                    return (QAStatus.Pass, "Pity 8 → E, doublon 200 OK.");
                }
                finally { restore(); }
            });

            RunOne("CHEST-RARITY", "StarChest", "Raretés + prix + FirstUnowned", () =>
            {
                var restore = SnapshotPrefs(ChestKeysAll(), new List<string>());
                try
                {
                    for (int i = 0; i <= 3; i++)
                        Expect(Zoologic.StarChestManager.RarityOf(i) == "C", "mx" + i + " = C.");
                    for (int i = 4; i <= 5; i++)
                        Expect(Zoologic.StarChestManager.RarityOf(i) == "R", "mx" + i + " = R.");
                    Expect(Zoologic.StarChestManager.RarityOf(6) == "E", "mx6 = E.");
                    for (int i = 7; i <= 9; i++)
                        Expect(Zoologic.StarChestManager.RarityOf(i) == "L", "mx" + i + " = L.");
                    Expect(Zoologic.StarChestManager.ShopPrice("C") == 80, "Shop C 80.");
                    Expect(Zoologic.StarChestManager.ShopPrice("R") == 200, "Shop R 200.");
                    Expect(Zoologic.StarChestManager.ShopPrice("E") == 450, "Shop E 450.");
                    Expect(Zoologic.StarChestManager.ShopPrice("L") == -1, "Shop L inachetable.");
                    ClearMascots();
                    Expect(Zoologic.StarChestManager.FirstUnowned("C") == 0, "1er C libre = 0.");
                    Zoologic.StarChestManager.SetOwned(0);
                    Expect(Zoologic.StarChestManager.FirstUnowned("C") == 1, "Après mx0 → 1.");
                    return (QAStatus.Pass, "Raretés/prix/FirstUnowned OK.");
                }
                finally { restore(); }
            });

            RunOne("CHEST-FACES", "StarChest", "10 visages mascottes non-null", () =>
            {
                Sprite[] neutrals = Zoologic.AnimalIconSet.LoadMoodNeutrals();
                int count = neutrals != null ? neutrals.Length : 0;
                Expect(count >= 10, "LoadMoodNeutrals >= 10 (obtenu " + count + ").");
                for (int i = 0; i < 10; i++)
                    Expect(Zoologic.StarChestManager.MascotFace(i) != null, "Face mx" + i + " non-null.");
                return (QAStatus.Pass, count + " visages, 10/10 non-null.");
            });

            RunOne("CHEST-THRESHOLD", "StarChest", "Seuils A 30/80/150/250 + monde 2 verrouillé", () =>
            {
                var restore = SnapshotPrefs(
                    StarKeysAll().Concat(ChestKeysAll()).Concat(EconKeys).ToList(), new List<string>());
                try
                {
                    ClearMascots();
                    for (int lvl = 1; lvl <= 10; lvl++)
                        Zoologic.LevelProgressManager.SetStars(lvl, 3);
                    Zoologic.StarChestManager.CheckAndGrant();
                    Expect(Zoologic.StarChestManager.OwnsMascot(0), "Seuil 30 → mx0 (1er C).");
                    Expect(!Zoologic.StarChestManager.OwnsMascot(1), "Un seul octroi par seuil.");
                    // Monde 2 seul complet, total < 30 : aucun octroi (asymétrie voulue).
                    ClearMascots();
                    for (int lvl = 1; lvl <= 100; lvl++)
                        PlayerPrefs.DeleteKey("stars_level_" + lvl);
                    for (int lvl = 51; lvl <= 75; lvl++)
                        Zoologic.LevelProgressManager.SetStars(lvl, 1);
                    Zoologic.StarChestManager.CheckAndGrant();
                    bool anyOwned = false;
                    for (int i = 0; i < 10; i++)
                        anyOwned |= Zoologic.StarChestManager.OwnsMascot(i);
                    Expect(!anyOwned, "Monde 2 seul : zéro octroi (voulu, verrouillé).");
                    return (QAStatus.Pass, "Seuils A + monde-2 verrouillé OK.");
                }
                finally { restore(); }
            });

            RunOne("CHEST-DIST", "StarChest", "Distribution P2 ~70% C (seed fixe)", () =>
            {
                var restore = SnapshotPrefs(ChestKeysAll(), new List<string>());
                try
                {
                    UnityEngine.Random.InitState(42);
                    var savedState = UnityEngine.Random.state;
                    int c = 0, n = 200;
                    for (int i = 0; i < n; i++)
                    {
                        ClearMascots();
                        PlayerPrefs.SetInt("Mascot_PityEpic", 0);
                        UnityEngine.Random.InitState(42 + i);
                        string rarity;
                        int dup;
                        Zoologic.StarChestManager.RollMascot(false, out rarity, out dup);
                        if (rarity == "C") c++;
                    }
                    UnityEngine.Random.state = savedState;
                    int pct = c * 100 / n;
                    Expect(pct >= 40 && pct <= 90, "Taux C hors bornes [40,90] : " + pct + "%.");
                    return (QAStatus.Pass, "Distribution C = " + pct + "% (cible 70).");
                }
                finally { restore(); }
            });
        }

        // ------------------------------------------------------------------
        // Section Progression : monotonie, pas de downgrade, bornes.
        // ------------------------------------------------------------------

        private static void SectionProgression()
        {
            RunOne("PROG-UNLOCK", "Progression", "UnlockNextLevel monotone", () =>
            {
                var restore = SnapshotPrefs(new List<string> { "highest_unlocked" }, new List<string>());
                try
                {
                    Zoologic.LevelProgressManager.UnlockNextLevel(900);
                    Expect(Zoologic.LevelProgressManager.GetHighestUnlockedLevel() == 901, "Unlock 900 → 901.");
                    Zoologic.LevelProgressManager.UnlockNextLevel(5);
                    Expect(Zoologic.LevelProgressManager.GetHighestUnlockedLevel() == 901, "Pas de downgrade.");
                    return (QAStatus.Pass, "Monotonie OK.");
                }
                finally { restore(); }
            });

            RunOne("PROG-STARS", "Progression", "SetStars pas de downgrade", () =>
            {
                var restore = SnapshotPrefs(new List<string> { "stars_level_999" }, new List<string>());
                try
                {
                    Expect(Zoologic.LevelProgressManager.GetStars(999) == 0, "Défaut 0.");
                    Zoologic.LevelProgressManager.SetStars(999, 2);
                    Expect(Zoologic.LevelProgressManager.GetStars(999) == 2, "Set 2.");
                    Zoologic.LevelProgressManager.SetStars(999, 1);
                    Expect(Zoologic.LevelProgressManager.GetStars(999) == 2, "Pas de downgrade.");
                    return (QAStatus.Pass, "No-downgrade OK.");
                }
                finally { restore(); }
            });

            RunOne("PROG-SCOPE", "Progression", "TotalStars 1..100 + ResetAll non couvert", () =>
            {
                var restore = SnapshotPrefs(new List<string> { "stars_level_500" }, new List<string>());
                try
                {
                    int before = Zoologic.StarChestManager.TotalStars();
                    Zoologic.LevelProgressManager.SetStars(500, 3);
                    Expect(Zoologic.StarChestManager.TotalStars() == before, "Niv.500 hors TotalStars (portée 1..100).");
                    return (QAStatus.Warn, "WARN: TotalStars 1..100 mais ResetAll 1..1000 (divergence doc) ; ResetAll destructif non couvert en batch.");
                }
                finally { restore(); }
            });
        }

        // ------------------------------------------------------------------
        // Section Skins (P2) : coûts, ownership, sélection.
        // ------------------------------------------------------------------

        private static void SectionSkins()
        {
            RunOne("SKIN-COSTS", "Skins", "Coûts + hors-bornes", () =>
            {
                Expect(Zoologic.SkinManager.CostOf(0) == 0, "Bois 0.");
                Expect(Zoologic.SkinManager.CostOf(1) == 200, "Flat 200.");
                Expect(Zoologic.SkinManager.CostOf(2) == 350, "Doré 350.");
                Expect(Zoologic.SkinManager.CostOf(3) == 500, "Nuit 500.");
                Expect(Zoologic.SkinManager.CostOf(-1) == int.MaxValue, "Hors-bornes MaxValue.");
                Expect(Zoologic.SkinManager.CostOf(99) == int.MaxValue, "Hors-bornes MaxValue.");
                return (QAStatus.Pass, "Coûts 0/200/350/500 OK.");
            });

            RunOne("SKIN-TINT", "Skins", "Teintes opaques + FlatFaces", () =>
            {
                for (int i = 0; i < 4; i++)
                    Expect(System.Math.Abs(Zoologic.SkinManager.TintOf(i).a - 1f) < 0.001f, "Teinte " + i + " opaque.");
                Expect(Zoologic.SkinManager.TintOf(99) == UnityEngine.Color.white, "Hors-bornes blanc.");
                Expect(Zoologic.SkinManager.UsesFlatFaces(1), "Seul Flat utilise flat faces.");
                Expect(!Zoologic.SkinManager.UsesFlatFaces(0)
                    && !Zoologic.SkinManager.UsesFlatFaces(2)
                    && !Zoologic.SkinManager.UsesFlatFaces(3), "Autres : non.");
                return (QAStatus.Pass, "Teintes + FlatFaces OK.");
            });

            RunOne("SKIN-OWN", "Skins", "Own/Select + persistance", () =>
            {
                var restore = SnapshotPrefs(SkinIntKeys, SkinStringKeys);
                try
                {
                    int sel0 = Zoologic.SkinManager.Selected;
                    Zoologic.SkinManager.Own(2);
                    Expect(Zoologic.SkinManager.IsOwned(2), "Own(2).");
                    Expect(Zoologic.SkinManager.IsOwned(0), "Bois toujours possédé.");
                    Zoologic.SkinManager.Select(2);
                    Expect(Zoologic.SkinManager.Selected == 2, "Select(2).");
                    Zoologic.SkinManager.Select(1);
                    Expect(Zoologic.SkinManager.Selected == 2, "Select non-possédé ignoré.");
                    Zoologic.SkinManager.Select(99);
                    Expect(Zoologic.SkinManager.Selected == 2, "Select hors-bornes ignoré.");
                    Expect(sel0 >= 0, "Sélection initiale lisible.");
                    return (QAStatus.Pass, "Own/Select OK.");
                }
                finally { restore(); }
            });
        }

        // ------------------------------------------------------------------
        // Section Loc : complétude des clés (code vs 8 langues).
        // ------------------------------------------------------------------

        private static void SectionLoc()
        {
            RunOne("LOC-KEYS-FR", "Loc", "Toutes les clés code existent en FR", () =>
            {
                var codeKeys = CollectLocKeys();
                var frKeys = LoadLocKeys("fr-FR");
                Expect(frKeys.Count > 0, "fr-FR.json lisible et non vide.");
                var missing = new List<string>();
                foreach (string k in codeKeys)
                    if (!frKeys.Contains(k))
                        missing.Add(k);
                if (missing.Count > 0)
                    Fail("Clés code absentes de fr-FR : " + string.Join(",", missing.Take(8).ToArray()));
                return (QAStatus.Pass, codeKeys.Count + " clés code couvertes en FR (" + frKeys.Count + " clés).");
            });

            RunOne("LOC-COVERAGE", "Loc", "Couverture 7 autres langues + valeurs FR", () =>
            {
                var codeKeys = CollectLocKeys();
                var frValues = LoadLocValues("fr-FR");
                var warns = new List<string>();
                foreach (string k in codeKeys)
                {
                    string v;
                    if (!frValues.TryGetValue(k, out v) || string.IsNullOrEmpty(v))
                        warns.Add("FR vide:" + k);
                }
                string[] langs = { "en-US", "ar-SA", "hi-IN", "ja-JP", "pt-BR", "ru-RU", "zh-CN" };
                foreach (string lang in langs)
                {
                    var keys = LoadLocKeys(lang);
                    int miss = 0;
                    string first = "";
                    foreach (string k in codeKeys)
                    {
                        if (!keys.Contains(k))
                        {
                            if (miss == 0) first = k;
                            miss++;
                        }
                    }
                    if (miss > 0)
                        warns.Add(lang + " -" + miss + " (ex:" + first + ")");
                }
                if (warns.Count > 0)
                    return (QAStatus.Warn, "WARN loc : " + string.Join(" | ", warns.Take(6).ToArray()));
                return (QAStatus.Pass, "7 langues complètes, valeurs FR non vides.");
            });
        }

        private static HashSet<string> CollectLocKeys()
        {
            var keys = new HashSet<string>();
            string dir = System.IO.Path.Combine(UnityEngine.Application.dataPath, "Scripts");
            if (!System.IO.Directory.Exists(dir))
                return keys;
            var rx = new System.Text.RegularExpressions.Regex(
                "LocalizationManager\\.Get\\(\\s*\"([^\"]+)\"");
            foreach (string file in System.IO.Directory.GetFiles(dir, "*.cs",
                System.IO.SearchOption.AllDirectories))
            {
                string text;
                try { text = System.IO.File.ReadAllText(file); }
                catch { continue; }
                foreach (string line in text.Split('\n'))
                {
                    string t = line.TrimStart();
                    if (t.StartsWith("//") || t.StartsWith("*"))
                        continue;
                    var m = rx.Match(line);
                    if (m.Success && !m.Groups[1].Value.EndsWith("."))
                        keys.Add(m.Groups[1].Value);
                }
            }
            return keys;
        }

        private static HashSet<string> LoadLocKeys(string lang)
        {
            var keys = new HashSet<string>();
            var values = LoadLocValues(lang);
            foreach (var kv in values)
                keys.Add(kv.Key);
            return keys;
        }

        private static Dictionary<string, string> LoadLocValues(string lang)
        {
            var dict = new Dictionary<string, string>();
            string path = System.IO.Path.Combine(UnityEngine.Application.dataPath,
                "Resources/Localization/" + lang + ".json");
            string text;
            try { text = System.IO.File.ReadAllText(path); }
            catch { return dict; }
            var rx = new System.Text.RegularExpressions.Regex(
                "\"k\"\\s*:\\s*\"([^\"]+)\"\\s*,\\s*\"v\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
            foreach (System.Text.RegularExpressions.Match m in rx.Matches(text))
                dict[m.Groups[1].Value] = m.Groups[2].Value;
            return dict;
        }

        // ------------------------------------------------------------------
        // Sorties.
        // ------------------------------------------------------------------

        private static void PrintConsole(long totalMs)
        {
            int pass = 0, warn = 0, fail = 0;
            foreach (var c in _checks)
            {
                if (c.Status == QAStatus.Pass) pass++;
                else if (c.Status == QAStatus.Warn) warn++;
                else fail++;
            }
            Debug.Log("=== QA GATE CORE : " + pass + " PASS, " + warn + " WARN, " + fail + " FAIL (" + totalMs + "ms) — rapport seul, build non bloqué ===");
            foreach (var c in _checks)
                Debug.Log("[QA][" + c.Status.ToString().ToUpper() + "] " + c.Id + " " + c.Name + " (" + c.Ms + "ms) — " + c.Detail);
            Debug.Log(fail > 0 ? "=== QA GATE : NO-GO conseillé (au moins 1 FAIL) ==="
                : warn > 0 ? "=== QA GATE : GO avec réserves (WARN uniquement) ==="
                : "=== QA GATE : GO (tout PASS) ===");
        }

        private static void SaveReport(long totalMs)
        {
            try
            {
                int pass = 0, warn = 0, fail = 0;
                foreach (var c in _checks)
                {
                    if (c.Status == QAStatus.Pass) pass++;
                    else if (c.Status == QAStatus.Warn) warn++;
                    else fail++;
                }
                string verdict = fail > 0 ? "NO-GO conseillé (au moins 1 FAIL) — rapport seul, build non bloqué"
                    : warn > 0 ? "GO avec réserves (WARN uniquement)"
                    : "GO (tout PASS)";

                var sb = new StringBuilder();
                sb.AppendLine("# QA Gate Core — " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                sb.AppendLine();
                sb.AppendLine("- Unity : " + Application.unityVersion + " (" + SystemInfo.operatingSystem + ")");
                sb.AppendLine("- Périmètre : Core puzzle (RuleValidator, PuzzleSolver, LevelGenerator, DifficultyScorer, LevelConfig, PuzzleGrid)");
                sb.AppendLine("- Env : Éditeur Windows, seeds fixes, rapport seul (non-bloquant prod)");
                sb.AppendLine("- Résumé : " + pass + " PASS, " + warn + " WARN, " + fail + " FAIL, " + _checks.Count + " checks, " + totalMs + "ms");
                sb.AppendLine("- Verdict : " + verdict);
                sb.AppendLine();
                sb.AppendLine("| ID | Module | Check | Status | ms | Détail |");
                sb.AppendLine("|---|---|---|---|---|---|");
                foreach (var c in _checks)
                    sb.AppendLine("| " + c.Id + " | " + c.Module + " | " + c.Name + " | " + c.Status.ToString().ToUpper() + " | " + c.Ms + " | " + c.Detail.Replace("|", "/").Replace("\n", " ") + " | ");

                Directory.CreateDirectory("Builds");
                string path = "Builds/QA-Report_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".md";
                File.WriteAllText(path, sb.ToString());
                Debug.Log("[QA] Rapport écrit : " + path);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[QA] Écriture rapport impossible : " + ex.GetType().Name + " - " + ex.Message);
            }
        }
    }
}
