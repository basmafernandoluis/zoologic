using System;
using System.Collections.Generic;

namespace Zoodoku.Core
{
    /// <summary>
    /// Résout une grille Zoodoku / Queens par backtracking.
    ///
    /// Principe : on place un pion dans chaque zone, une zone à la fois. Pour une
    /// zone donnée, on essaie chaque case libre et valide, puis on passe à la zone
    /// suivante ; si plus aucune case n'est possible, on revient en arrière.
    ///
    /// Une solution est une liste de positions de pions (row, col).
    /// </summary>
    public sealed class PuzzleSolver
    {
        /// <summary>
        /// Cherche toutes les solutions possibles de la grille, en s'arrêtant dès que
        /// <paramref name="maxSolutions"/> solutions distinctes ont été trouvées.
        /// Par défaut on s'arrête à 2 : il suffit de savoir si la solution est unique
        /// ou non, inutile d'énumérer toutes les solutions.
        /// </summary>
        /// <param name="grid">La grille à résoudre (ses pions sont restaurés après l'appel).</param>
        /// <param name="maxSolutions">Nombre maximum de solutions à chercher (&gt; 0).</param>
        /// <returns>Les solutions trouvées, sous forme de liste de positions de pions.</returns>
        public List<List<(int row, int col)>> FindAllSolutions(PuzzleGrid grid, int maxSolutions = 2)
        {
            if (grid == null)
                throw new ArgumentNullException(nameof(grid));

            if (maxSolutions <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxSolutions), "maxSolutions doit être supérieur à 0.");

            var solutions = new List<List<(int row, int col)>>();
            var zones = grid.GetRegionIds();

            // Sauvegarde de l'état initial pour restaurer la grille après la recherche.
            var pionsInitiaux = new List<(int row, int col)>(grid.Pions);

            grid.Clear();
            Rechercher(grid, zones, 0, solutions, maxSolutions);

            // Restauration de la grille (on remet les pions qu'elle contenait au départ).
            grid.Clear();
            foreach (var (row, col) in pionsInitiaux)
                grid.PlacePion(row, col);

            return solutions;
        }

        /// <summary>
        /// Retourne true uniquement si la grille admet exactement une solution.
        /// </summary>
        public bool HasUniqueSolution(PuzzleGrid grid)
        {
            return FindAllSolutions(grid, maxSolutions: 2).Count == 1;
        }

        /// <summary>
        /// Cherche UNE solution de la grille en imposant que les positions
        /// <paramref name="fixedPlacements"/> soient occupées par des pions.
        /// Ces positions ne doivent PAS être en conflit entre elles.
        /// Retourne null si aucune solution n'existe avec ces contraintes.
        /// La grille est restaurée à son état initial après l'appel.
        /// </summary>
        public List<(int row, int col)> SolveWithFixedPlacements(
            PuzzleGrid grid, List<(int row, int col)> fixedPlacements)
        {
            if (grid == null)
                throw new ArgumentNullException(nameof(grid));
            if (fixedPlacements == null)
                throw new ArgumentNullException(nameof(fixedPlacements));

            var pionsInitiaux = new List<(int row, int col)>(grid.Pions);
            var zones = grid.GetRegionIds();

            grid.Clear();

            // Place les pions fixes et identifie les zones déjà occupées.
            var zonesOccupees = new HashSet<int>();
            foreach (var (row, col) in fixedPlacements)
            {
                grid.PlacePion(row, col);
                zonesOccupees.Add(grid.GetRegionId(row, col));
            }

            // Zones à résoudre : celles qui n'ont pas encore de pion.
            var zonesALiberer = new List<int>();
            foreach (int zoneId in zones)
            {
                if (!zonesOccupees.Contains(zoneId))
                    zonesALiberer.Add(zoneId);
            }

            var solution = new List<(int row, int col)>();
            RechercherAvecContraintes(grid, zonesALiberer, 0, solution);

            // Restauration de la grille.
            grid.Clear();
            foreach (var (row, col) in pionsInitiaux)
                grid.PlacePion(row, col);

            return solution.Count > 0 ? solution : null;
        }

        /// <summary>
        /// Recherche récursive avec contraintes : ne résout que les zones non encore occupées.
        /// </summary>
        private static void RechercherAvecContraintes(
            PuzzleGrid grid,
            IReadOnlyList<int> zones,
            int indexZone,
            List<(int row, int col)> solution)
        {
            if (indexZone == zones.Count)
            {
                // Vérifie que la grille est entièrement résolue.
                if (RuleValidator.IsSolved(grid))
                {
                    solution.Clear();
                    solution.AddRange(grid.Pions);
                }
                return;
            }

            int zoneId = zones[indexZone];

            for (int row = 0; row < grid.Size; row++)
            {
                for (int col = 0; col < grid.Size; col++)
                {
                    if (grid.GetRegionId(row, col) != zoneId)
                        continue;

                    if (grid.HasPion(row, col))
                        continue;

                    if (!RuleValidator.IsValidPlacement(grid, row, col))
                        continue;

                    grid.PlacePion(row, col);
                    RechercherAvecContraintes(grid, zones, indexZone + 1, solution);
                    grid.RemovePion(row, col);

                    if (solution.Count > 0)
                        return;
                }
            }
        }

        /// <summary>
        /// Recherche récursive : essaie de placer un pion dans la zone d'index
        /// <paramref name="indexZone"/>, puis explore la zone suivante.
        /// </summary>
        private static void Rechercher(
            PuzzleGrid grid,
            IReadOnlyList<int> zones,
            int indexZone,
            List<List<(int row, int col)>> solutions,
            int maxSolutions)
        {
            // On a déjà trouvé assez de solutions : on coupe la recherche.
            if (solutions.Count >= maxSolutions)
                return;

            // Toutes les zones ont reçu un pion : c'est une solution candidate.
            if (indexZone == zones.Count)
            {
                if (RuleValidator.IsSolved(grid))
                    solutions.Add(new List<(int row, int col)>(grid.Pions));
                return;
            }

            int zoneId = zones[indexZone];

            // Essaie chaque case de la zone courante.
            for (int row = 0; row < grid.Size; row++)
            {
                for (int col = 0; col < grid.Size; col++)
                {
                    if (grid.GetRegionId(row, col) != zoneId)
                        continue;

                    if (!RuleValidator.IsValidPlacement(grid, row, col))
                        continue;

                    grid.PlacePion(row, col);
                    Rechercher(grid, zones, indexZone + 1, solutions, maxSolutions);
                    grid.RemovePion(row, col); // backtracking : on retire le pion essayé
                }
            }
        }
    }
}
