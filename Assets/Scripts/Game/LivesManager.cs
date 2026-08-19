using System;

namespace Zoodoku
{
    /// <summary>
    /// Gestionnaire de vies du joueur : compteur de 3 cœurs décrémenté
    /// à chaque conflit, avec un événement quand les vies sont épuisées.
    /// Classe pure (pas de MonoBehaviour) : instanciée et pilotée par
    /// <see cref="PuzzleGameController"/>.
    /// </summary>
    public sealed class LivesManager
    {
        public const int ViesDepart = 3;

        private int _vies;

        /// <summary>
        /// Nombre de vies restantes (0 = partie perdue).
        /// </summary>
        public int Vies => _vies;

        /// <summary>
        /// Événement invoqué quand le joueur atteint 0 vie.
        /// Le contrôleur principal l'écoute pour bloquer les interactions
        /// et afficher le panneau de défaite.
        /// </summary>
        public Action OnPartiePerdue;

        /// <summary>
        /// Crée un gestionnaire avec le maximum de vies (<see cref="ViesDepart"/>).
        /// </summary>
        public LivesManager()
        {
            _vies = ViesDepart;
        }

        /// <summary>
        /// Retire une vie. Renvoie true si la vie a bien été retirée,
        /// false si le compteur est déjà à 0.
        /// Invoque <see cref="OnPartiePerdue"/> si les vies atteignent 0.
        /// </summary>
        public bool PerdreVie()
        {
            if (_vies <= 0)
                return false;

            _vies--;

            if (_vies <= 0)
                OnPartiePerdue?.Invoke();

            return true;
        }

        /// <summary>
        /// Remet les vies au maximum (<see cref="ViesDepart"/>).
        /// Utilisé lors de la réinitialisation du niveau (bouton Réessayer).
        /// </summary>
        public void Reinitialiser()
        {
            _vies = ViesDepart;
        }
    }
}
