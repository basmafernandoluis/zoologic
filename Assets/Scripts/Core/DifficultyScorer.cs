using System;
using System.Collections.Generic;

namespace Zoodoku.Core
{
    /// <summary>
    /// Estime la difficulté d'une grille Zoodoku / Queens.
    ///
    /// Principe : on simule une résolution "à la main" et l'on compte un "coût de
    /// raisonnement" = nombre de déductions AVANCÉES (règles de ligne / colonne,
    /// au-delà du simple "une zone n'a plus qu'une seule case possible") auxquelles
    /// s'ajoute 1 pour chaque hypothèse à essayer quand les déductions s'arrêtent.
    /// Plus le coût est élevé, plus la grille est difficile.
    ///
    /// Score retourné : 1 = facile, 2 = moyen, 3 = difficile.
    /// </summary>
    public static class DifficultyScorer
    {
        /// <summary>
        /// Coût jusqu'à 2 → facile ; 3-5 → moyen ; au-delà → difficile.
        /// Ces seuils donnent un bon étalement pour les tailles courantes (4 à 6).
        /// </summary>
        private const int SeuilFacile = 2;
        private const int SeuilMoyen = 5;

        /// <summary>
        /// Retourne une estimation de difficulté de 1 (facile) à 3 (difficile).
        /// </summary>
        /// <param name="grid">Grille à évaluer (les pions éventuels sont ignorés : on résout depuis zéro).</param>
        public static int ScoreDifficulty(PuzzleGrid grid)
        {
            if (grid == null)
                throw new ArgumentNullException(nameof(grid));

            int cout = Resoudre(grid);

            if (cout <= SeuilFacile)
                return 1;

            if (cout <= SeuilMoyen)
                return 2;

            return 3;
        }

        /// <summary>
        /// Analyse interne exposée pour les tests et le diagnostic : retourne le coût
        /// de raisonnement et un indicateur "résolu" (faux si le simulateur n'a pas
        /// abouti, ce qui ne devrait pas arriver pour une grille à solution unique).
        /// </summary>
        public static (int cout, bool resolu) AnalyseResolution(PuzzleGrid grid)
        {
            if (grid == null)
                throw new ArgumentNullException(nameof(grid));

            int cout = Resoudre(grid);
            return (cout, cout != int.MaxValue);
        }

        /// <summary>
        /// Coût de raisonnement : déductions avancées + hypothèses. Retourne
        /// int.MaxValue si aucune issue n'est trouvée (grille sans solution ou
        /// plusieurs branches valides, cas qui ne doit pas se produire pour une
        /// grille à solution unique).
        /// </summary>
        private static int Resoudre(PuzzleGrid grid)
        {
            var structure = ConstruireStructure(grid);
            var etat = new EtatResolution(structure.Taille, structure.NbZones);
            return ResoudreRecursif(structure, etat);
        }

        /// <summary>
        /// Résolution récursive : applique les déductions jusqu'à bloquer, puis, si
        /// nécessaire, essaie chaque possibilité d'une zone peu contrainte (une
        /// hypothèse). Pour une grille à solution unique, exactement une branche
        /// aboutit.
        /// </summary>
        private static int ResoudreRecursif(StructureGrille structure, EtatResolution etat)
        {
            bool termine;
            bool ok = Deduire(structure, etat, out termine, out int deductionsAvancees);

            if (!ok)
                return int.MaxValue; // contradiction : impasse

            if (termine)
                return deductionsAvancees;

            // Bloqué par déduction : il faut essayer une hypothèse. On choisit la
            // zone qui a le moins de cases possibles (branchement minimal).
            int meilleureZone = -1;
            int meilleurCompte = int.MaxValue;

            for (int idx = 0; idx < structure.NbZones; idx++)
            {
                if (etat.ZoneResolue[idx])
                    continue;

                int compte = CompterCasesPossibles(structure, etat, idx);
                if (compte == 0)
                    return int.MaxValue; // impasse

                if (compte < meilleurCompte)
                {
                    meilleurCompte = compte;
                    meilleureZone = idx;
                }
            }

            int solutionsTrouvees = 0;
            int coutBrancheOK = 0;

            foreach (var (row, col) in CasesPossibles(structure, etat, meilleureZone))
            {
                var copie = EtatResolution.Cloner(etat);
                PlacerPion(structure, copie, meilleureZone, row, col);

                int coutBranche = ResoudreRecursif(structure, copie);
                if (coutBranche != int.MaxValue)
                {
                    solutionsTrouvees++;
                    coutBrancheOK = coutBranche;
                }
            }

            if (solutionsTrouvees == 1)
                return deductionsAvancees + 1 + coutBrancheOK;

            // 0 branche valable (grille sans solution) ou plusieurs (grille non
            // unique) : cas hors contrat pour une grille à solution unique.
            return int.MaxValue;
        }

        /// <summary>
        /// Applique les déductions jusqu'à saturation :
        ///   - simple  : une zone qui n'a plus qu'une seule case possible impose son pion ;
        ///   - avancée : si toutes les cases possibles d'une zone sont sur une seule
        ///               ligne (ou colonne), la zone occupe cette ligne → on élimine
        ///               le reste de la ligne (ou colonne) dans les autres zones ;
        ///   - avancée : si une ligne (ou colonne) n'a de cases possibles que dans une
        ///               seule zone, c'est cette zone qui placera son pion sur cette
        ///               ligne → on élimine les autres cases de cette zone.
        /// </summary>
        /// <returns>false si une contradiction est trouvée (plus aucune issue).</returns>
        private static bool Deduire(StructureGrille s, EtatResolution etat, out bool termine, out int deductionsAvancees)
        {
            int taille = s.Taille;
            deductionsAvancees = 0;

            bool Disponible(int row, int col)
                => !etat.Interdit[row, col] && !etat.Occupe[row, col];

            void Eliminer(int row, int col)
            {
                if (row >= 0 && row < taille && col >= 0 && col < taille && !etat.Occupe[row, col])
                    etat.Interdit[row, col] = true;
            }

            void Placer(int idxZone, int row, int col)
            {
                etat.Occupe[row, col] = true;
                etat.ZoneResolue[idxZone] = true;

                for (int k = 0; k < taille; k++)
                {
                    if (!etat.Occupe[row, k])
                        etat.Interdit[row, k] = true;
                    if (!etat.Occupe[k, col])
                        etat.Interdit[k, col] = true;
                }

                for (int dr = -1; dr <= 1; dr++)
                {
                    for (int dc = -1; dc <= 1; dc++)
                    {
                        if (dr == 0 && dc == 0)
                            continue;
                        Eliminer(row + dr, col + dc);
                    }
                }
            }

            List<(int row, int col)> CasesZone(int idxZone)
            {
                var resultat = new List<(int row, int col)>();
                foreach (var (row, col) in s.CasesParZone[idxZone])
                {
                    if (Disponible(row, col))
                        resultat.Add((row, col));
                }
                return resultat;
            }

            bool LigneOccupee(int row)
            {
                for (int col = 0; col < taille; col++)
                    if (etat.Occupe[row, col])
                        return true;
                return false;
            }

            bool ColonneOccupee(int col)
            {
                for (int row = 0; row < taille; row++)
                    if (etat.Occupe[row, col])
                        return true;
                return false;
            }

            while (true)
            {
                bool changement = false;

                // 1) Règle simple : une zone n'a plus qu'une seule case possible.
                for (int idx = 0; idx < s.NbZones; idx++)
                {
                    if (etat.ZoneResolue[idx])
                        continue;

                    var cases = CasesZone(idx);

                    if (cases.Count == 0)
                    {
                        termine = false;
                        return false; // contradiction
                    }

                    if (cases.Count == 1)
                    {
                        Placer(idx, cases[0].row, cases[0].col);
                        changement = true;
                    }
                }

                // 2) Règle avancée : les cases possibles d'une zone sont toutes sur
                //    une seule ligne (ou une seule colonne).
                for (int idx = 0; idx < s.NbZones; idx++)
                {
                    if (etat.ZoneResolue[idx])
                        continue;

                    var cases = CasesZone(idx);
                    if (cases.Count == 0)
                    {
                        termine = false;
                        return false;
                    }

                    bool ligneUnique = true;
                    int premiereLigne = cases[0].row;
                    foreach (var (row, col) in cases)
                    {
                        if (row != premiereLigne)
                        {
                            ligneUnique = false;
                            break;
                        }
                    }

                    if (ligneUnique)
                    {
                        bool aElimine = false;
                        for (int col = 0; col < taille; col++)
                        {
                            if (s.ZoneParCase[premiereLigne, col] == idx)
                                continue;
                            if (!Disponible(premiereLigne, col))
                                continue;
                            Eliminer(premiereLigne, col);
                            aElimine = true;
                        }

                        if (aElimine)
                        {
                            deductionsAvancees++;
                            changement = true;
                        }
                    }

                    bool colonneUnique = true;
                    int premiereColonne = cases[0].col;
                    foreach (var (row, col) in cases)
                    {
                        if (col != premiereColonne)
                        {
                            colonneUnique = false;
                            break;
                        }
                    }

                    if (colonneUnique)
                    {
                        bool aElimine = false;
                        for (int row = 0; row < taille; row++)
                        {
                            if (s.ZoneParCase[row, premiereColonne] == idx)
                                continue;
                            if (!Disponible(row, premiereColonne))
                                continue;
                            Eliminer(row, premiereColonne);
                            aElimine = true;
                        }

                        if (aElimine)
                        {
                            deductionsAvancees++;
                            changement = true;
                        }
                    }
                }

                // 3) Règle avancée : une ligne dont toutes les cases possibles
                //    appartiennent à une seule zone.
                for (int row = 0; row < taille; row++)
                {
                    if (LigneOccupee(row))
                        continue;

                    int zoneUnique = -1;
                    bool plusieursZones = false;
                    bool aucuneCase = true;

                    for (int col = 0; col < taille; col++)
                    {
                        if (!Disponible(row, col))
                            continue;

                        aucuneCase = false;
                        int idxZone = s.ZoneParCase[row, col];

                        if (zoneUnique == -1)
                            zoneUnique = idxZone;
                        else if (zoneUnique != idxZone)
                            plusieursZones = true;
                    }

                    if (aucuneCase)
                    {
                        termine = false;
                        return false; // une ligne sans aucune case possible
                    }

                    if (!plusieursZones && !etat.ZoneResolue[zoneUnique])
                    {
                        bool aElimine = false;
                        foreach (var (r, c) in s.CasesParZone[zoneUnique])
                        {
                            if (r == row)
                                continue;
                            if (!Disponible(r, c))
                                continue;
                            Eliminer(r, c);
                            aElimine = true;
                        }

                        if (aElimine)
                        {
                            deductionsAvancees++;
                            changement = true;
                        }
                    }
                }

                // 4) Règle avancée : une colonne dont toutes les cases possibles
                //    appartiennent à une seule zone.
                for (int col = 0; col < taille; col++)
                {
                    if (ColonneOccupee(col))
                        continue;

                    int zoneUnique = -1;
                    bool plusieursZones = false;
                    bool aucuneCase = true;

                    for (int row = 0; row < taille; row++)
                    {
                        if (!Disponible(row, col))
                            continue;

                        aucuneCase = false;
                        int idxZone = s.ZoneParCase[row, col];

                        if (zoneUnique == -1)
                            zoneUnique = idxZone;
                        else if (zoneUnique != idxZone)
                            plusieursZones = true;
                    }

                    if (aucuneCase)
                    {
                        termine = false;
                        return false; // une colonne sans aucune case possible
                    }

                    if (!plusieursZones && !etat.ZoneResolue[zoneUnique])
                    {
                        bool aElimine = false;
                        foreach (var (r, c) in s.CasesParZone[zoneUnique])
                        {
                            if (c == col)
                                continue;
                            if (!Disponible(r, c))
                                continue;
                            Eliminer(r, c);
                            aElimine = true;
                        }

                        if (aElimine)
                        {
                            deductionsAvancees++;
                            changement = true;
                        }
                    }
                }

                // Plus aucune déduction possible : saturation atteinte.
                if (!changement)
                    break;
            }

            termine = true;
            for (int i = 0; i < s.NbZones; i++)
            {
                if (!etat.ZoneResolue[i])
                {
                    termine = false;
                    break;
                }
            }

            return true;
        }

        // ------------------------------------------------------------------
        // Structures auxiliaires.
        // ------------------------------------------------------------------

        /// <summary>Données statiques de la grille, partagées entre les états.</summary>
        private sealed class StructureGrille
        {
            public int Taille;
            public int NbZones;
            public int[,] ZoneParCase;
            public List<List<(int row, int col)>> CasesParZone;
        }

        /// <summary>État de résolution mutable (éliminations + pions posés).</summary>
        private sealed class EtatResolution
        {
            public bool[,] Interdit;
            public bool[,] Occupe;
            public bool[] ZoneResolue;

            public EtatResolution(int taille, int nbZones)
            {
                Interdit = new bool[taille, taille];
                Occupe = new bool[taille, taille];
                ZoneResolue = new bool[nbZones];
            }

            private EtatResolution(bool[,] interdit, bool[,] occupe, bool[] zoneResolue)
            {
                Interdit = interdit;
                Occupe = occupe;
                ZoneResolue = zoneResolue;
            }

            public static EtatResolution Cloner(EtatResolution source)
                => new EtatResolution(
                    (bool[,])source.Interdit.Clone(),
                    (bool[,])source.Occupe.Clone(),
                    (bool[])source.ZoneResolue.Clone());
        }

        private static StructureGrille ConstruireStructure(PuzzleGrid grid)
        {
            int taille = grid.Size;
            var zoneIds = grid.GetRegionIds();
            int nbZones = zoneIds.Count;

            var indexParZoneId = new Dictionary<int, int>();
            for (int i = 0; i < nbZones; i++)
                indexParZoneId[zoneIds[i]] = i;

            var structure = new StructureGrille
            {
                Taille = taille,
                NbZones = nbZones,
                ZoneParCase = new int[taille, taille],
                CasesParZone = new List<List<(int row, int col)>>(nbZones),
            };

            for (int i = 0; i < nbZones; i++)
                structure.CasesParZone.Add(new List<(int row, int col)>());

            for (int row = 0; row < taille; row++)
            {
                for (int col = 0; col < taille; col++)
                {
                    int idx = indexParZoneId[grid.GetRegionId(row, col)];
                    structure.ZoneParCase[row, col] = idx;
                    structure.CasesParZone[idx].Add((row, col));
                }
            }

            return structure;
        }

        private static int CompterCasesPossibles(StructureGrille s, EtatResolution etat, int idxZone)
        {
            int compte = 0;
            foreach (var (row, col) in s.CasesParZone[idxZone])
            {
                if (!etat.Interdit[row, col] && !etat.Occupe[row, col])
                    compte++;
            }
            return compte;
        }

        private static List<(int row, int col)> CasesPossibles(StructureGrille s, EtatResolution etat, int idxZone)
        {
            var resultat = new List<(int row, int col)>();
            foreach (var (row, col) in s.CasesParZone[idxZone])
            {
                if (!etat.Interdit[row, col] && !etat.Occupe[row, col])
                    resultat.Add((row, col));
            }
            return resultat;
        }

        private static void PlacerPion(StructureGrille s, EtatResolution etat, int idxZone, int row, int col)
        {
            etat.Occupe[row, col] = true;
            etat.ZoneResolue[idxZone] = true;

            for (int k = 0; k < s.Taille; k++)
            {
                if (!etat.Occupe[row, k])
                    etat.Interdit[row, k] = true;
                if (!etat.Occupe[k, col])
                    etat.Interdit[k, col] = true;
            }

            for (int dr = -1; dr <= 1; dr++)
            {
                for (int dc = -1; dc <= 1; dc++)
                {
                    if (dr == 0 && dc == 0)
                        continue;

                    int nr = row + dr;
                    int nc = col + dc;
                    if (nr >= 0 && nr < s.Taille && nc >= 0 && nc < s.Taille && !etat.Occupe[nr, nc])
                        etat.Interdit[nr, nc] = true;
                }
            }
        }
    }
}
