using System;
using System.Collections.Generic;

namespace Zoodoku.Core
{
    /// <summary>
    /// Représente la grille NxN du puzzle Zoodoku / Queens.
    ///
    /// Chaque case possède des coordonnées (row, col) et appartient à une zone
    /// identifiée par un entier (regionId). La grille conserve également la liste
    /// des positions où un pion est actuellement placé.
    ///
    /// Classe 100 % logique : aucun lien avec Unity, testable en isolation.
    /// </summary>
    public sealed class PuzzleGrid
    {
        private readonly int[,] _regionIds;
        private readonly List<(int row, int col)> _pions = new List<(int row, int col)>();

        /// <summary>Nombre de lignes (et de colonnes) de la grille.</summary>
        public int Size { get; }

        /// <summary>
        /// Positions des pions actuellement placés, dans l'ordre d'insertion.
        /// Lecture seule pour l'extérieur : la modification passe par les méthodes dédiées.
        /// </summary>
        public IReadOnlyList<(int row, int col)> Pions => _pions;

        /// <summary>
        /// Construit une grille carrée à partir d'un tableau de zones.
        /// </summary>
        /// <param name="regionIds">
        /// Tableau carré ; <c>regionIds[row, col]</c> donne l'identifiant de zone de la case
        /// (row, col). Deux cases qui partagent le même identifiant appartiennent à la même zone.
        /// </param>
        /// <exception cref="ArgumentNullException">Si <paramref name="regionIds"/> est null.</exception>
        /// <exception cref="ArgumentException">Si le tableau n'est pas carré.</exception>
        public PuzzleGrid(int[,] regionIds)
        {
            if (regionIds == null)
                throw new ArgumentNullException(nameof(regionIds));

            int size = regionIds.GetLength(0);
            if (size <= 0 || regionIds.GetLength(1) != size)
                throw new ArgumentException("Le tableau regionIds doit être carré (N x N).", nameof(regionIds));

            Size = size;
            _regionIds = (int[,])regionIds.Clone();
        }

        /// <summary>Retourne l'identifiant de zone de la case (row, col).</summary>
        public int GetRegionId(int row, int col) => _regionIds[row, col];

        /// <summary>Retourne true si un pion est déjà placé sur la case (row, col).</summary>
        public bool HasPion(int row, int col) => _pions.Contains((row, col));

        /// <summary>
        /// Place un pion sur la case (row, col).
        /// Cette méthode ne valide AUCUNE règle de jeu : c'est le rôle de
        /// <see cref="RuleValidator.IsValidPlacement(PuzzleGrid, int, int)"/>.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Si les coordonnées sortent de la grille.</exception>
        /// <exception cref="InvalidOperationException">Si un pion est déjà présent sur cette case.</exception>
        public void PlacePion(int row, int col)
        {
            CheckBounds(row, col);

            if (HasPion(row, col))
                throw new InvalidOperationException($"Un pion est déjà placé sur la case ({row}, {col}).");

            _pions.Add((row, col));
        }

        /// <summary>
        /// Retire le pion placé sur la case (row, col). Ne fait rien si aucune case n'en contient.
        /// </summary>
        public void RemovePion(int row, int col)
        {
            CheckBounds(row, col);
            _pions.Remove((row, col));
        }

        /// <summary>Retire tous les pions de la grille.</summary>
        public void Clear() => _pions.Clear();

        /// <summary>
        /// Retourne la liste triée des identifiants de zones présents dans la grille.
        /// Utile pour parcourir les zones dans un ordre stable lors de la résolution.
        /// </summary>
        public IReadOnlyList<int> GetRegionIds()
        {
            var zones = new HashSet<int>();

            for (int row = 0; row < Size; row++)
            {
                for (int col = 0; col < Size; col++)
                    zones.Add(_regionIds[row, col]);
            }

            var triees = new List<int>(zones);
            triees.Sort();
            return triees;
        }

        /// <summary>Vérifie que les coordonnées sont dans les limites de la grille.</summary>
        private void CheckBounds(int row, int col)
        {
            if (row < 0 || row >= Size || col < 0 || col >= Size)
                throw new ArgumentOutOfRangeException(
                    $"Les coordonnées ({row}, {col}) sortent de la grille {Size}x{Size}.");
        }
    }
}
