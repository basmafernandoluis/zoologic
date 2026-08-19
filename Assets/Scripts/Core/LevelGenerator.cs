using System;
using System.Collections.Generic;

namespace Zoodoku.Core
{
    /// <summary>
    /// Génère des niveaux Zoodoku / Queens à solution unique.
    ///
    /// Pipeline en 3 étapes :
    ///   A) placement d'une solution valide aléatoire (backtracking randomisé) ;
    ///   B) découpe de la grille en zones colorées par croissance aléatoire
    ///      (flood-fill) autour des pions de la solution, avec des tailles
    ///      raisonnablement équilibrées (taille max ≈ 2 x la taille moyenne) ;
    ///   C) vérification de l'unicité via PuzzleSolver.HasUniqueSolution() ;
    ///      si la solution n'est pas unique, on recommence depuis l'étape A.
    ///
    /// Classe 100 % logique : aucun lien avec Unity, testable en isolation.
    /// </summary>
    public sealed class LevelGenerator
    {
        /// <summary>Tentatives maximales pour obtenir une grille à solution unique.</summary>
        public const int MaxTentativesUnicite = 50;

        /// <summary>Tentatives maximales pour obtenir la difficulté cible.</summary>
        public const int MaxTentativesDifficulte = 20;

        private readonly Random _rng;
        private readonly PuzzleSolver _solver = new PuzzleSolver();
        private readonly double _tailleMaxFacteur;

        /// <summary>
        /// Crée un générateur. Un <paramref name="seed"/> fixe permet de rendre la
        /// génération reproductible (utile pour les tests et le débogage).
        /// <paramref name="tailleMaxFacteur"/> contrôle la taille maximale d'une zone
        /// (taille max ≈ facteur x taille moyenne) ; la valeur par défaut 2.0 respecte
        /// le cahier des charges ("environ 2x la moyenne"). Un facteur plus petit
        /// produit des zones plus petites, donc des grilles plus contraintes et plus
        /// souvent à solution unique.
        /// </summary>
        public LevelGenerator(int? seed = null, double tailleMaxFacteur = 2.0)
        {
            _rng = seed.HasValue ? new Random(seed.Value) : new Random();
            _tailleMaxFacteur = tailleMaxFacteur;
        }

        /// <summary>
        /// Méthode publique principale : génère une grille dont la difficulté estimée
        /// est proche de <paramref name="targetDifficulty"/>.
        /// </summary>
        /// <param name="size">Taille de la grille (N ≥ 3).</param>
        /// <param name="targetDifficulty">Difficulté visée : 1 (facile), 2 (moyen) ou 3 (difficile).</param>
        /// <returns>
        /// Une grille unique dont le score correspond à la cible si elle a été trouvée
        /// dans les tentatives prévues ; sinon la grille la plus proche rencontrée.
        /// </returns>
        public PuzzleGrid GenerateLevel(int size, int targetDifficulty)
        {
            VerifierParametres(size, targetDifficulty);

            PuzzleGrid meilleureGrille = null;
            int ecartMinimal = int.MaxValue;

            for (int tentative = 0; tentative < MaxTentativesDifficulte; tentative++)
            {
                PuzzleGrid grille;

                try
                {
                    grille = GenerateUniqueGrid(size);
                }
                catch (InvalidOperationException)
                {
                    // Pas de grille à solution unique sur cette tentative : on
                    // continue avec la meilleure trouvée jusque-là.
                    continue;
                }

                int score = DifficultyScorer.ScoreDifficulty(grille);
                int ecart = Math.Abs(score - targetDifficulty);

                if (ecart < ecartMinimal)
                {
                    ecartMinimal = ecart;
                    meilleureGrille = grille;
                }

                // Difficulté exacte trouvée : on s'arrête tout de suite.
                if (ecart == 0)
                    return grille;
            }

            return meilleureGrille;
        }

        /// <summary>
        /// Génère une grille de taille <paramref name="size"/> à solution unique
        /// (étapes A, B et C). La grille est vide : les pions de la solution interne
        /// ne sont pas conservés.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Si aucune grille à solution unique n'est trouvée après
        /// <see cref="MaxTentativesUnicite"/> tentatives.
        /// </exception>
        public PuzzleGrid GenerateUniqueGrid(int size)
        {
            if (size < 3)
                throw new ArgumentOutOfRangeException(nameof(size), "La taille minimale d'une grille générée est 3.");

            for (int tentative = 0; tentative < MaxTentativesUnicite; tentative++)
            {
                List<(int row, int col)> solution = GenererSolutionAleatoire(size);   // étape A
                int[,] zones = GenererZonesAutourDeLaSolution(size, solution);         // étape B
                var grille = new PuzzleGrid(zones);

                if (_solver.HasUniqueSolution(grille))                                  // étape C
                    return grille;
            }

            throw new InvalidOperationException(
                $"Impossible de générer une grille {size}x{size} à solution unique après {MaxTentativesUnicite} tentatives.");
        }

        /// <summary>
        /// Génère une grille avec les étapes A et B uniquement (solution aléatoire +
        /// zones), SANS vérification de l'unicité. Méthode de diagnostic : utile pour
        /// étudier les grilles non uniques.
        /// </summary>
        public PuzzleGrid GenerateRawGrid(int size)
        {
            if (size < 3)
                throw new ArgumentOutOfRangeException(nameof(size), "La taille minimale d'une grille générée est 3.");

            List<(int row, int col)> solution = GenererSolutionAleatoire(size);
            int[,] zones = GenererZonesAutourDeLaSolution(size, solution);
            return new PuzzleGrid(zones);
        }

        // ------------------------------------------------------------------
        // Étape A : solution valide aléatoire.
        // ------------------------------------------------------------------

        /// <summary>
        /// Place un pion par ligne (et par colonne) en évitant les diagonales
        /// adjacentes. Backtracking randomisé : si un emplacement mène à une
        /// impasse, on revient en arrière et on essaie un autre emplacement.
        /// </summary>
        private List<(int row, int col)> GenererSolutionAleatoire(int size)
        {
            var colonnesUtilisees = new bool[size];
            var solution = new List<(int row, int col)>();

            if (!PlacerRangee(size, 0, colonnesUtilisees, solution))
                throw new InvalidOperationException(
                    $"Échec inattendu du placement aléatoire pour une grille {size}x{size}.");

            return solution;
        }

        /// <summary>
        /// Place le pion de la ligne <paramref name="ligne"/>, puis les suivantes.
        /// Retourne true si une solution complète a été trouvée.
        /// </summary>
        private bool PlacerRangee(int size, int ligne, bool[] colonnesUtilisees, List<(int row, int col)> solution)
        {
            if (ligne == size)
                return true;

            var colonnes = new List<int>();
            for (int col = 0; col < size; col++)
            {
                if (!colonnesUtilisees[col])
                    colonnes.Add(col);
            }

            Melanger(colonnes);

            foreach (int col in colonnes)
            {
                if (EnConflitDiagonalAvecLignePrecedente(solution, ligne, col))
                    continue;

                colonnesUtilisees[col] = true;
                solution.Add((ligne, col));

                if (PlacerRangee(size, ligne + 1, colonnesUtilisees, solution))
                    return true;

                // Backtracking : on annule le choix.
                solution.RemoveAt(solution.Count - 1);
                colonnesUtilisees[col] = false;
            }

            return false;
        }

        /// <summary>
        /// Vérifie si la case (ligne, col) est en diagonale adjacente avec le pion
        /// de la ligne précédente. Comme seules les diagonales ADJACENTES comptent,
        /// seul le pion placé juste au-dessus peut être en conflit.
        /// </summary>
        private bool EnConflitDiagonalAvecLignePrecedente(List<(int row, int col)> solution, int ligne, int col)
        {
            if (ligne == 0)
                return false;

            int colonnePrecedente = solution[solution.Count - 1].col;
            return Math.Abs(colonnePrecedente - col) == 1;
        }

        // ------------------------------------------------------------------
        // Étape B : croissance des zones autour de la solution.
        // ------------------------------------------------------------------

        /// <summary>
        /// Construit les zones colorées autour des pions de la solution.
        ///
        /// Phase 1 — chaque pion devient la graine d'une zone, puis la zone grandit
        /// en "serpent" : un chemin aléatoire de la taille moyenne, ce qui donne des
        /// zones fines et très contraintes (bonnes pour l'unicité).
        /// Phase 2 — les cases restantes sont réparties par croissance équilibrée
        /// (flood-fill pondéré : les plus petites zones grandissent en priorité).
        ///
        /// La taille d'une zone est plafonnée à environ 2 x la taille moyenne.
        /// </summary>
        /// <returns>Tableau size x size où zones[row, col] est l'identifiant de zone.</returns>
        private int[,] GenererZonesAutourDeLaSolution(int size, List<(int row, int col)> solution)
        {
            int nbZones = solution.Count;
            int tailleMoyenne = size * size / nbZones;       // vaut "size" quand nbZones == size
            int tailleMax = Math.Max(1, (int)Math.Ceiling(_tailleMaxFacteur * tailleMoyenne));

            var zones = new int[size, size];
            for (int row = 0; row < size; row++)
            {
                for (int col = 0; col < size; col++)
                    zones[row, col] = -1;
            }

            int[] tailles = new int[nbZones];
            var frontieres = new List<(int row, int col)>[nbZones];
            for (int i = 0; i < nbZones; i++)
                frontieres[i] = new List<(int row, int col)>();

            // Phase 1 : croissance en serpent depuis chaque graine, de façon
            // séquentielle : chaque zone déroule son chemin dans un espace encore
            // vierge, ce qui produit des zones fines, allongées et peu imbriquées
            // (très favorable à l'unicité de la solution).
            var pointes = new (int row, int col)[nbZones];
            var direction = new (int dr, int dc)[] { (-1, 0), (1, 0), (0, -1), (0, 1) };

            for (int i = 0; i < nbZones; i++)
            {
                var (row, col) = solution[i];
                zones[row, col] = i;
                tailles[i] = 1;
                pointes[i] = (row, col);
                AjouterVoisinsLibres(zones, frontieres[i], row, col);
            }

            int casesRestantes = size * size - nbZones;

            for (int i = 0; i < nbZones; i++)
            {
                int directionPrecedente = _rng.Next(direction.Length);

                while (tailles[i] < tailleMoyenne)
                {
                    // Avec 50 % de chances, on prolonge le serpent tout droit pour
                    // obtenir des formes fines et allongées.
                    int premierChoix = _rng.Next(2) == 0 ? directionPrecedente : -1;

                    var options = new List<int>();
                    for (int d = 0; d < direction.Length; d++)
                    {
                        int tr = pointes[i].row + direction[d].dr;
                        int tc = pointes[i].col + direction[d].dc;
                        if (tr >= 0 && tr < size && tc >= 0 && tc < size && zones[tr, tc] == -1)
                            options.Add(d);
                    }

                    if (options.Count == 0)
                        break; // le serpent est coincé, la phase 2 comblera

                    int choisi;
                    if (premierChoix != -1 && options.Contains(premierChoix))
                        choisi = premierChoix;
                    else
                        choisi = options[_rng.Next(options.Count)];

                    directionPrecedente = choisi;
                    var (dr, dc) = direction[choisi];
                    int nr = pointes[i].row + dr;
                    int nc = pointes[i].col + dc;

                    zones[nr, nc] = i;
                    tailles[i]++;
                    casesRestantes--;
                    pointes[i] = (nr, nc);
                    AjouterVoisinsLibres(zones, frontieres[i], nr, nc);
                }
            }

            // Phase 2 : croissance équilibrée pour couvrir les cases restantes.
            while (casesRestantes > 0)
            {
                // Zones qui peuvent encore grandir normalement (sous la taille max).
                var actives = new List<int>();
                for (int i = 0; i < nbZones; i++)
                {
                    if (tailles[i] < tailleMax && frontieres[i].Count > 0)
                        actives.Add(i);
                }

                // Relaxation : si plus rien ne peut grandir, on autorise les zones
                // au-delà de la taille max pour garantir la couverture de la grille.
                if (actives.Count == 0)
                {
                    for (int i = 0; i < nbZones; i++)
                    {
                        if (frontieres[i].Count > 0)
                            actives.Add(i);
                    }

                    if (actives.Count == 0)
                        break; // grille entièrement couverte
                }

                // Tirage pondéré : les zones les plus petites grandissent en priorité.
                int zone = ChoisirZonePonderee(actives, tailles, tailleMax);

                // On prend une case voisine au hasard ; les entrées devenues obsolètes
                // (case déjà prise par une autre zone) sont simplement sautées.
                var frontiereZone = frontieres[zone];
                while (frontiereZone.Count > 0)
                {
                    int index = _rng.Next(frontiereZone.Count);
                    var (row, col) = frontiereZone[index];

                    // Retrait en O(1) en échangeant avec le dernier élément.
                    frontiereZone[index] = frontiereZone[frontiereZone.Count - 1];
                    frontiereZone.RemoveAt(frontiereZone.Count - 1);

                    if (zones[row, col] != -1)
                        continue;

                    zones[row, col] = zone;
                    tailles[zone]++;
                    casesRestantes--;
                    AjouterVoisinsLibres(zones, frontiereZone, row, col);
                    break;
                }
            }

            // Phase 3 : rééquilibrage. Une zone tombée sous la taille minimale
            // (parce que son serpent s'est retrouvé coincé) récupère des cases
            // voisines d'une zone plus grande, sans jamais casser la contiguïté
            // de la zone donneuse.
            int tailleMin = Math.Max(2, tailleMoyenne / 2);

            for (int i = 0; i < nbZones; i++)
            {
                while (tailles[i] < tailleMin)
                {
                    (int row, int col)? aPrendre = null;
                    int donneuse = -1;
                    int tailleDonneuse = -1;

                    // Cherche une case voisine de la zone i dont le retrait de la
                    // zone donneuse ne casse pas la contiguïté de cette dernière.
                    for (int r = 0; r < size; r++)
                    {
                        for (int c = 0; c < size; c++)
                        {
                            if (zones[r, c] != i)
                                continue;

                            for (int dr = -1; dr <= 1; dr++)
                            {
                                for (int dc = -1; dc <= 1; dc++)
                                {
                                    if (dr == 0 && dc == 0)
                                        continue;

                                    int nr = r + dr;
                                    int nc = c + dc;
                                    if (nr < 0 || nr >= size || nc < 0 || nc >= size)
                                        continue;

                                    int j = zones[nr, nc];
                                    if (j == i || j == -1)
                                        continue;

                                    // Préfère voler à la plus grande zone possible,
                                    // et seulement si elle reste contiguë.
                                    if (tailles[j] <= tailleDonneuse || tailles[j] < 2)
                                        continue;

                                    if (!ZoneResteContigue(zones, j, nr, nc))
                                        continue;

                                    aPrendre = (nr, nc);
                                    donneuse = j;
                                    tailleDonneuse = tailles[j];
                                }
                            }
                        }
                    }

                    if (aPrendre == null)
                        break; // plus rien à récupérer

                    var (cellRow, cellCol) = aPrendre.Value;
                    zones[cellRow, cellCol] = i;
                    tailles[i]++;
                    tailles[donneuse]--;
                }
            }

            // Sûreté : au cas où une case resterait non attribuée, on lui donne la
            // zone d'un voisin (ne devrait jamais arriver avec la croissance ci-dessus).
            for (int row = 0; row < size; row++)
            {
                for (int col = 0; col < size; col++)
                {
                    if (zones[row, col] == -1)
                        zones[row, col] = ZoneDUnVoisin(zones, row, col);
                }
            }

            return zones;
        }

        /// <summary>
        /// Ajoute à la frontière d'une zone les voisins (8 directions) de la case
        /// (row, col) qui ne sont pas encore attribués.
        /// </summary>
        private void AjouterVoisinsLibres(int[,] zones, List<(int row, int col)> frontiere, int row, int col)
        {
            int size = zones.GetLength(0);

            for (int dr = -1; dr <= 1; dr++)
            {
                for (int dc = -1; dc <= 1; dc++)
                {
                    if (dr == 0 && dc == 0)
                        continue;

                    int nr = row + dr;
                    int nc = col + dc;
                    if (nr < 0 || nr >= size || nc < 0 || nc >= size)
                        continue;

                    if (zones[nr, nc] == -1)
                        frontiere.Add((nr, nc));
                }
            }
        }

        /// <summary>
        /// Choisit une zone parmi celles listées, pondérée par la taille restante :
        /// plus une zone est petite, plus elle a de chances d'être tirée.
        /// </summary>
        private int ChoisirZonePonderee(List<int> actives, int[] tailles, int tailleMax)
        {
            var poids = new List<int>(actives.Count);
            int total = 0;

            foreach (int i in actives)
            {
                // Minimum 1 : en cas de relaxation, une zone peut dépasser la taille
                // max ; son poids ne doit pas devenir négatif.
                int p = Math.Max(1, (tailleMax - tailles[i]) + 1);
                poids.Add(p);
                total += p;
            }

            int tirage = _rng.Next(total);
            for (int k = 0; k < actives.Count; k++)
            {
                tirage -= poids[k];
                if (tirage < 0)
                    return actives[k];
            }

            return actives[actives.Count - 1];
        }

        /// <summary>Retourne la zone d'une case voisine déjà attribuée (fallback rare).</summary>
        private int ZoneDUnVoisin(int[,] zones, int row, int col)
        {
            int size = zones.GetLength(0);

            for (int dr = -1; dr <= 1; dr++)
            {
                for (int dc = -1; dc <= 1; dc++)
                {
                    if (dr == 0 && dc == 0)
                        continue;

                    int nr = row + dr;
                    int nc = col + dc;
                    if (nr < 0 || nr >= size || nc < 0 || nc >= size)
                        continue;

                    if (zones[nr, nc] != -1)
                        return zones[nr, nc];
                }
            }

            return 0;
        }

        /// <summary>
        /// Vérifie que la zone <paramref name="zoneId"/> reste un seul bloc connexe
        /// après retrait de la case (rowRetiree, colRetiree). Parcours en largeur
        /// à partir d'une case restante de la zone.
        /// </summary>
        private bool ZoneResteContigue(int[,] zones, int zoneId, int rowRetiree, int colRetiree)
        {
            int size = zones.GetLength(0);

            // Compte les cellules restantes de la zone et trouve un point de départ.
            int premierR = -1, premierC = -1;
            int attendu = 0;

            for (int r = 0; r < size; r++)
            {
                for (int c = 0; c < size; c++)
                {
                    if (zones[r, c] != zoneId || (r == rowRetiree && c == colRetiree))
                        continue;

                    attendu++;
                    if (premierR == -1)
                    {
                        premierR = r;
                        premierC = c;
                    }
                }
            }

            if (attendu == 0)
                return false; // la zone serait vide

            // Parcours en largeur sur les 4 voisins orthogonaux.
            var visites = new bool[size, size];
            var file = new Queue<(int row, int col)>();
            file.Enqueue((premierR, premierC));
            visites[premierR, premierC] = true;
            int atteints = 1;

            int[] drs = { -1, 1, 0, 0 };
            int[] dcs = { 0, 0, -1, 1 };

            while (file.Count > 0)
            {
                var (row, col) = file.Dequeue();

                for (int d = 0; d < drs.Length; d++)
                {
                    int nr = row + drs[d];
                    int nc = col + dcs[d];

                    if (nr < 0 || nr >= size || nc < 0 || nc >= size)
                        continue;
                    if (visites[nr, nc])
                        continue;
                    if (zones[nr, nc] != zoneId || (nr == rowRetiree && nc == colRetiree))
                        continue;

                    visites[nr, nc] = true;
                    file.Enqueue((nr, nc));
                    atteints++;
                }
            }

            return atteints == attendu;
        }

        // ------------------------------------------------------------------
        // Utilitaires.
        // ------------------------------------------------------------------

        private void VerifierParametres(int size, int targetDifficulty)
        {
            if (size < 3)
                throw new ArgumentOutOfRangeException(nameof(size), "La taille minimale d'une grille générée est 3.");

            if (targetDifficulty < 1 || targetDifficulty > 3)
                throw new ArgumentOutOfRangeException(nameof(targetDifficulty), "La difficulté cible doit être comprise entre 1 et 3.");
        }

        /// <summary>Mélange une liste en place (Fisher-Yates).</summary>
        private void Melanger<T>(List<T> liste)
        {
            for (int i = liste.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (liste[i], liste[j]) = (liste[j], liste[i]);
            }
        }
    }
}
