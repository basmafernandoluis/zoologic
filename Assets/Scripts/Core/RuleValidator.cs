using System;
using System.Collections.Generic;

namespace Zoodoku.Core
{
    /// <summary>
    /// Type précis d'un conflit entre un pion et un autre pion (ou plusieurs).
    /// Peut être combiné : un pion peut violer plusieurs règles à la fois
    /// (ex. même zone ET même ligne).
    /// </summary>
    public enum ConflictType
    {
        /// <summary>Deux pions dans la même zone colorée.</summary>
        Zone,
        /// <summary>Deux pions sur la même ligne.</summary>
        Row,
        /// <summary>Deux pions sur la même colonne.</summary>
        Column,
        /// <summary>Deux pions adjacents en diagonale (coin à coin).</summary>
        Diagonal,
    }

    /// <summary>
    /// Règles du puzzle Zoodoku / Queens.
    ///
    /// Deux pions ne peuvent jamais :
    ///   - être sur la même ligne ;
    ///   - être sur la même colonne ;
    ///   - appartenir à la même zone (regionId) ;
    ///   - être adjacents en diagonale (cases se touchant par un coin).
    ///
    /// Les règles n'ont aucun état : cette classe est purement statique, donc
    /// utilisable dans les tests et dans le solveur sans aucune configuration.
    /// </summary>
    public static class RuleValidator
    {
        /// <summary>
        /// Vérifie qu'ajouter un pion sur la case (row, col) ne crée AUCUN conflit
        /// avec les pions déjà placés sur la grille.
        /// </summary>
        /// <returns>
        /// false si la case est hors grille, déjà occupée, ou en conflit avec un pion
        /// existant (même ligne, même colonne, même zone, ou diagonale adjacente) ;
        /// true sinon.
        /// </returns>
        public static bool IsValidPlacement(PuzzleGrid grid, int row, int col)
        {
            if (grid == null)
                throw new ArgumentNullException(nameof(grid));

            if (row < 0 || row >= grid.Size || col < 0 || col >= grid.Size)
                return false;

            if (grid.HasPion(row, col))
                return false;

            return GetConflicts(grid, row, col).Count == 0;
        }

        /// <summary>
        /// Retourne la liste des types de conflits entre la case (row, col) et les
        /// autres pions de la grille : <see cref="ConflictType.Zone"/>, Row, Column et/ou
        /// Diagonal. La liste est vide si la case ne viole aucune règle.
        ///
        /// Fonctionne aussi bien pour une case vide que pour une case déjà occupée
        /// (le pion de la case elle-même est ignoré dans la comparaison).
        /// </summary>
        public static List<ConflictType> GetConflicts(PuzzleGrid grid, int row, int col)
        {
            if (grid == null)
                throw new ArgumentNullException(nameof(grid));

            var conflits = new List<ConflictType>(4);

            if (row < 0 || row >= grid.Size || col < 0 || col >= grid.Size)
                return conflits;

            int zoneNouvelle = grid.GetRegionId(row, col);

            foreach (var (pionRow, pionCol) in grid.Pions)
            {
                if (pionRow == row && pionCol == col)
                    continue; // on ignore le pion déjà placé sur cette case

                if (pionRow == row)
                    AjouterUnique(conflits, ConflictType.Row);
                if (pionCol == col)
                    AjouterUnique(conflits, ConflictType.Column);
                if (grid.GetRegionId(pionRow, pionCol) == zoneNouvelle)
                    AjouterUnique(conflits, ConflictType.Zone);
                if (EstAdjacentEnDiagonale(pionRow, pionCol, row, col))
                    AjouterUnique(conflits, ConflictType.Diagonal);
            }

            return conflits;
        }

        /// <summary>Ajoute un type de conflit s'il n'est pas déjà présent dans la liste.</summary>
        private static void AjouterUnique(List<ConflictType> conflits, ConflictType type)
        {
            if (!conflits.Contains(type))
                conflits.Add(type);
        }

        /// <summary>
        /// Vérifie que la grille est résolue : chaque zone contient exactement un pion,
        /// et aucune paire de pions ne viole les règles de placement.
        /// </summary>
        public static bool IsSolved(PuzzleGrid grid)
        {
            if (grid == null)
                throw new ArgumentNullException(nameof(grid));

            // 1) Compte les pions présents dans chaque zone.
            var pionsParZone = new Dictionary<int, int>();

            foreach (var (pionRow, pionCol) in grid.Pions)
            {
                int zoneId = grid.GetRegionId(pionRow, pionCol);
                pionsParZone.TryGetValue(zoneId, out int compteur);
                pionsParZone[zoneId] = compteur + 1;
            }

            // 2) Chaque zone existante doit contenir exactement un pion.
            foreach (int zoneId in grid.GetRegionIds())
            {
                if (!pionsParZone.TryGetValue(zoneId, out int compteur) || compteur != 1)
                    return false;
            }

            // 3) Aucune paire de pions ne doit violer les règles.
            var pions = grid.Pions;

            for (int i = 0; i < pions.Count; i++)
            {
                for (int j = i + 1; j < pions.Count; j++)
                {
                    var a = pions[i];
                    var b = pions[j];

                    if (a.row == b.row || a.col == b.col)   // même ligne ou même colonne
                        return false;

                    if (grid.GetRegionId(a.row, a.col) == grid.GetRegionId(b.row, b.col)) // même zone
                        return false;

                    if (EstAdjacentEnDiagonale(a.row, a.col, b.row, b.col)) // diagonale adjacente
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Retourne true si deux cases sont adjacentes en diagonale, c'est-à-dire
        /// qu'elles se touchent par un coin (différence de 1 sur la ligne ET sur la colonne).
        /// </summary>
        private static bool EstAdjacentEnDiagonale(int rowA, int colA, int rowB, int colB)
            => Math.Abs(rowA - rowB) == 1 && Math.Abs(colA - colB) == 1;
    }
}
