using System;
using System.Collections.Generic;

namespace Zoologic.Core
{
    /// <summary>
    /// Suite de tests unitaires indépendante de Unity.
    ///
    /// Aucun framework externe : il suffit d'appeler <see cref="RunAllTests"/>
    /// (par exemple depuis le Main d'un petit programme de test) pour vérifier
    /// que la logique du jeu fonctionne.
    /// </summary>
    public static class PuzzleTests
    {
        /// <summary>
        /// Exécute tous les tests et affiche le détail dans la console.
        /// </summary>
        /// <returns>true si tous les tests passent, false sinon.</returns>
        public static bool RunAllTests()
        {
            bool toutEstOK = true;

            toutEstOK &= Execute("Grille 4x4 avec une solution unique connue",
                                 Test_Grille4x4_SolutionUnique);

            toutEstOK &= Execute("Deux pions sur la même ligne",
                                 Test_DeuxPions_MemeLigne);

            toutEstOK &= Execute("Deux pions en diagonale adjacente",
                                 Test_DeuxPions_DiagonaleAdjacente);

            toutEstOK &= Execute("Grille avec plusieurs solutions (non unique)",
                                 Test_PlusieursSolutions_NonUnique);

            toutEstOK &= Execute("Deux pions sur la même colonne",
                                 Test_DeuxPions_MemeColonne);

            toutEstOK &= Execute("Deux pions dans la même zone",
                                 Test_DeuxPions_MemeZone);

            toutEstOK &= Execute("Générateur : 10 grilles 5x5 à solution unique",
                                 Test_Generateur_GrillesUniques);

            toutEstOK &= Execute("Générateur : difficulté ciblée 1 vs 3",
                                 Test_Generateur_DifficulteCiblee);

            toutEstOK &= Execute("SolveWithFixedPlacements avec 2 pions fixes",
                                 Test_SolveWithFixedPlacements);

            Console.WriteLine();
            Console.WriteLine(toutEstOK
                ? "=== TOUS LES TESTS SONT PASSÉS ==="
                : "=== CERTAINS TESTS ONT ÉCHOUÉ ===");

            return toutEstOK;
        }

        // ------------------------------------------------------------------
        // Grille de référence utilisée par les tests 1, 2 et 3.
        // Sa solution unique (vérifiée par le solveur) est :
        //   (0,1), (1,3), (2,0), (3,2)
        // ------------------------------------------------------------------
        private static readonly int[,] GrilleReference4x4 =
        {
            { 0, 0, 0, 1 },
            { 0, 2, 1, 1 },
            { 2, 2, 1, 3 },
            { 2, 3, 3, 3 },
        };

        /// <summary>
        /// Une grille 4x4 connue à l'avance doit avoir exactement une solution,
        /// et cette solution doit correspondre à celle attendue.
        /// </summary>
        private static bool Test_Grille4x4_SolutionUnique()
        {
            var grille = new PuzzleGrid(GrilleReference4x4);
            var solveur = new PuzzleSolver();

            // 1) Le solveur ne doit trouver qu'une seule solution.
            List<List<(int row, int col)>> solutions = solveur.FindAllSolutions(grille);
            AssertTrue(solutions.Count == 1, $"Attendu 1 solution, obtenu {solutions.Count}.");

            // 2) Cette solution doit être exactement la solution connue.
            var attendu = new List<(int row, int col)> { (0, 1), (1, 3), (2, 0), (3, 2) };
            AssertTrue(MemesPositions(solutions[0], attendu),
                $"La solution trouvée {Formater(solutions[0])} ne correspond pas à {Formater(attendu)}.");

            // 3) HasUniqueSolution doit confirmer l'unicité.
            AssertTrue(solveur.HasUniqueSolution(grille),
                "HasUniqueSolution doit retourner true pour une grille à solution unique.");

            // 4) Poser manuellement la solution connue doit donner une grille résolue.
            var grilleRemplie = new PuzzleGrid(GrilleReference4x4);
            foreach (var (row, col) in attendu)
                grilleRemplie.PlacePion(row, col);

            AssertTrue(RuleValidator.IsSolved(grilleRemplie),
                "La grille contenant la solution connue doit être considérée comme résolue.");

            return true;
        }

        /// <summary>
        /// Deux pions sur la même ligne : le placement doit être refusé et
        /// l'état correspondant ne doit pas être considéré comme résolu.
        /// </summary>
        private static bool Test_DeuxPions_MemeLigne()
        {
            var grille = new PuzzleGrid(GrilleReference4x4);
            grille.PlacePion(0, 1);

            // (0, 3) est sur la même ligne que le pion (0, 1) : placement invalide.
            AssertFalse(RuleValidator.IsValidPlacement(grille, 0, 3),
                "Le placement sur la même ligne qu'un pion existant doit être refusé.");

            // Un état contenant deux pions sur la même ligne n'est pas résolu.
            grille.PlacePion(0, 3);
            AssertFalse(RuleValidator.IsSolved(grille),
                "Une grille avec deux pions sur la même ligne ne doit pas être résolue.");

            return true;
        }

        /// <summary>
        /// Deux pions en diagonale adjacente : le placement doit être refusé et
        /// l'état correspondant ne doit pas être considéré comme résolu.
        /// </summary>
        private static bool Test_DeuxPions_DiagonaleAdjacente()
        {
            var grille = new PuzzleGrid(GrilleReference4x4);
            grille.PlacePion(0, 1);

            // (1, 2) est en diagonale adjacente de (0, 1) : placement invalide.
            AssertFalse(RuleValidator.IsValidPlacement(grille, 1, 2),
                "Le placement sur une case en diagonale adjacente doit être refusé.");

            // Un état contenant deux pions en diagonale adjacente n'est pas résolu.
            grille.PlacePion(1, 2);
            AssertFalse(RuleValidator.IsSolved(grille),
                "Une grille avec deux pions en diagonale adjacente ne doit pas être résolue.");

            return true;
        }

        /// <summary>
        /// Chaque zone est une colonne entière : plusieurs permutations de lignes
        /// sont valides, donc la solution n'est pas unique.
        /// </summary>
        private static bool Test_PlusieursSolutions_NonUnique()
        {
            int[,] zonesColonnes =
            {
                { 0, 1, 2, 3 },
                { 0, 1, 2, 3 },
                { 0, 1, 2, 3 },
                { 0, 1, 2, 3 },
            };

            var grille = new PuzzleGrid(zonesColonnes);
            var solveur = new PuzzleSolver();

            // Au moins deux solutions valides doivent exister.
            List<List<(int row, int col)>> solutions = solveur.FindAllSolutions(grille, maxSolutions: 5);
            AssertTrue(solutions.Count >= 2,
                $"Attendu au moins 2 solutions, obtenu {solutions.Count}.");

            // Et chaque solution trouvée doit vraiment être résolue.
            foreach (var solution in solutions)
            {
                var grilleSol = new PuzzleGrid(zonesColonnes);
                foreach (var (row, col) in solution)
                    grilleSol.PlacePion(row, col);

                AssertTrue(RuleValidator.IsSolved(grilleSol),
                    $"La solution {Formater(solution)} doit être une grille résolue.");
            }

            AssertFalse(solveur.HasUniqueSolution(grille),
                "HasUniqueSolution doit retourner false pour une grille à plusieurs solutions.");

            return true;
        }

        /// <summary>
        /// Bonus : deux pions sur la même colonne doivent violer les règles.
        /// </summary>
        private static bool Test_DeuxPions_MemeColonne()
        {
            var grille = new PuzzleGrid(GrilleReference4x4);
            grille.PlacePion(0, 1);

            // (3, 1) est sur la même colonne que le pion (0, 1) : placement invalide.
            AssertFalse(RuleValidator.IsValidPlacement(grille, 3, 1),
                "Le placement sur la même colonne qu'un pion existant doit être refusé.");

            grille.PlacePion(3, 1);
            AssertFalse(RuleValidator.IsSolved(grille),
                "Une grille avec deux pions sur la même colonne ne doit pas être résolue.");

            return true;
        }

        /// <summary>
        /// Bonus : deux pions dans la même zone doivent violer les règles.
        /// </summary>
        private static bool Test_DeuxPions_MemeZone()
        {
            var grille = new PuzzleGrid(GrilleReference4x4);
            grille.PlacePion(0, 1);

            // (1, 0) appartient à la même zone (zone 0) que le pion (0, 1) : invalide.
            AssertFalse(RuleValidator.IsValidPlacement(grille, 1, 0),
                "Le placement dans une zone déjà occupée doit être refusé.");

            grille.PlacePion(1, 0);
            AssertFalse(RuleValidator.IsSolved(grille),
                "Une grille avec deux pions dans la même zone ne doit pas être résolue.");

            return true;
        }

        /// <summary>
        /// Le générateur doit produire des grilles à solution unique : on en génère
        /// 10 de taille 5 et chacune doit passer le test d'unicité.
        /// </summary>
        private static bool Test_Generateur_GrillesUniques()
        {
            var generateur = new LevelGenerator();
            var solveur = new PuzzleSolver();

            for (int i = 0; i < 10; i++)
            {
                var grille = generateur.GenerateUniqueGrid(5);

                AssertTrue(grille.Size == 5, $"La grille générée #{i + 1} doit avoir la taille 5 (obtenu {grille.Size}).");

                AssertTrue(solveur.HasUniqueSolution(grille),
                    $"La grille générée #{i + 1} doit avoir une solution unique.");
            }

            return true;
        }

        /// <summary>
        /// Une grille générée pour une difficulté cible élevée doit obtenir un score
        /// strictement supérieur à une grille générée pour une difficulté faible.
        /// </summary>
        private static bool Test_Generateur_DifficulteCiblee()
        {
            var generateur = new LevelGenerator();

            var grilleFacile = generateur.GenerateLevel(4, targetDifficulty: 1);
            var grilleDifficile = generateur.GenerateLevel(4, targetDifficulty: 3);

            int scoreFacile = DifficultyScorer.ScoreDifficulty(grilleFacile);
            int scoreDifficile = DifficultyScorer.ScoreDifficulty(grilleDifficile);

            AssertTrue(scoreFacile >= 1 && scoreFacile <= 3,
                $"Score de la grille ciblée 1 hors bornes : {scoreFacile}.");
            AssertTrue(scoreDifficile >= 1 && scoreDifficile <= 3,
                $"Score de la grille ciblée 3 hors bornes : {scoreDifficile}.");
            AssertTrue(scoreDifficile > scoreFacile,
                $"Le score de la grille ciblée 3 ({scoreDifficile}) doit être supérieur à celui de la grille ciblée 1 ({scoreFacile}).");

            return true;
        }

        /// <summary>
        /// SolveWithFixedPlacements : placer 2 pions corrects sur la grille 4x4,
        /// le solveur doit trouver les 2 pions manquants pour compléter la solution.
        /// </summary>
        private static bool Test_SolveWithFixedPlacements()
        {
            var grille = new PuzzleGrid(GrilleReference4x4);
            var solveur = new PuzzleSolver();

            // Solution connue : (0,1), (1,3), (2,0), (3,2)
            // On fixe les deux premiers pions (corrects, sans conflit).
            var fixes = new List<(int row, int col)> { (0, 1), (1, 3) };

            var solution = solveur.SolveWithFixedPlacements(grille, fixes);

            AssertTrue(solution != null, "SolveWithFixedPlacements doit trouver une solution.");

            // La solution doit contenir les positions fixes + les 2 manquantes.
            var attendu = new List<(int row, int col)> { (0, 1), (1, 3), (2, 0), (3, 2) };
            AssertTrue(MemesPositions(solution, attendu),
                $"La solution trouvée {Formater(solution)} ne correspond pas à {Formater(attendu)}.");

            // Aucun fixe : le solveur doit quand même trouver une solution.
            var solutionVide = solveur.SolveWithFixedPlacements(grille, new List<(int, int)>());
            AssertTrue(solutionVide != null,
                "SolveWithFixedPlacements avec liste vide doit trouver une solution.");

            // Fixe en conflit : pas de solution possible.
            var conflit = new List<(int row, int col)> { (0, 1), (0, 3) }; // même ligne
            grille.Clear();
            var solutionConflit = solveur.SolveWithFixedPlacements(grille, conflit);
            AssertTrue(solutionConflit == null,
                "SolveWithFixedPlacements avec pions en conflit doit retourner null.");

            return true;
        }

        // ------------------------------------------------------------------
        // Petits outils d'assertion et de rapport.
        // ------------------------------------------------------------------

        /// <summary>Exécute un test et rapporte son résultat dans la console.</summary>
        private static bool Execute(string nom, Func<bool> test)
        {
            try
            {
                bool ok = test();

                if (ok)
                    Console.WriteLine($"[OK]     {nom}");
                else
                    Console.WriteLine($"[ÉCHEC]  {nom} : le test a retourné false.");

                return ok;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ÉCHEC]  {nom} : exception levée : {ex.GetType().Name} - {ex.Message}");
                return false;
            }
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition)
                throw new Exception(message);
        }

        private static void AssertFalse(bool condition, string message)
            => AssertTrue(!condition, message);

        /// <summary>
        /// Compare deux ensembles de positions sans tenir compte de l'ordre :
        /// retourne true s'ils contiennent exactement les mêmes cases.
        /// </summary>
        private static bool MemesPositions(List<(int row, int col)> a, List<(int row, int col)> b)
        {
            if (a.Count != b.Count)
                return false;

            foreach (var position in a)
            {
                if (!b.Contains(position))
                    return false;
            }

            return true;
        }

        private static string Formater(List<(int row, int col)> positions)
            => string.Join(", ", positions);
    }
}
