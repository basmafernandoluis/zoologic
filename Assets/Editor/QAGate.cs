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
    /// QA Gate pré-prod, périmètre Core puzzle uniquement.
    /// Éditeur Windows, rapport seul : ne bloque jamais le build prod.
    /// Menu : Tools/Zoo Logic/QA/Run Core Gate. Batchmode : QAGate.RunBatch.
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
