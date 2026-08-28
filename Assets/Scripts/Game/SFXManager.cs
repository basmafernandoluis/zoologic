using UnityEngine;

namespace Zoologic
{
    /// <summary>
    /// Gestionnaire centralisé des effets sonores. Crée un GameObject persistant
    /// avec un AudioSource, et expose des méthodes pour jouer chaque clip.
    /// Les clips sont chargés depuis Resources/Sounds/.
    /// </summary>
    public sealed class SFXManager : MonoBehaviour
    {
        private const string SfxEnabledKey = "sfx_enabled";
        private const string MusicEnabledKey = "music_enabled";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInstance()
        {
            if (_instance == null)
                CreateInstance();
        }

        private static SFXManager _instance;

        private AudioSource _source;
        private AudioSource _musicSource;

        private bool _isEnabled;
        private bool _musicEnabled;

        // Clips chargés une seule fois (lazy).
        private AudioClip _confirm;
        private AudioClip _failure;
        private AudioClip _clickedOut;
        private AudioClip _dialogueBlip;
        private AudioClip _success;
        private AudioClip _menuOpen;
        private AudioClip _menuClose;
        private AudioClip _unlock;
        private AudioClip _music;

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                _isEnabled = value;
                PlayerPrefs.SetInt(SfxEnabledKey, value ? 1 : 0);
                PlayerPrefs.Save();
                if (_source != null)
                    _source.mute = !value;
            }
        }

        /// <summary>
        /// Musique d'ambiance, indépendante des effets sonores : elle utilise
        /// sa propre AudioSource (boucle), donc aucun chevauchement avec les
        /// <c>PlayOneShot</c> des SFX.
        /// </summary>
        public bool MusicEnabled
        {
            get => _musicEnabled;
            set
            {
                _musicEnabled = value;
                PlayerPrefs.SetInt(MusicEnabledKey, value ? 1 : 0);
                PlayerPrefs.Save();
                if (_musicSource == null) return;
                _musicSource.mute = !value;
                if (value && _musicSource.clip != null && !_musicSource.isPlaying)
                    _musicSource.Play();
            }
        }

        public static SFXManager Instance
        {
            get
            {
                if (_instance == null)
                    CreateInstance();
                return _instance;
            }
        }

        private static void CreateInstance()
        {
            var go = new GameObject("SFXManager");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<SFXManager>();
            _instance._source = go.AddComponent<AudioSource>();
            _instance._source.playOnAwake = false;
            _instance._source.volume = 1f;
            _instance._isEnabled = PlayerPrefs.GetInt(SfxEnabledKey, 1) == 1;
            _instance._source.mute = !_instance._isEnabled;

            // Canal de musique dédié (boucle) : séparé des SFX.
            var musicGO = new GameObject("MusicSource");
            musicGO.transform.SetParent(go.transform, false);
            _instance._musicSource = musicGO.AddComponent<AudioSource>();
            _instance._musicSource.playOnAwake = false;
            _instance._musicSource.loop = true;
            _instance._musicSource.volume = 0.5f;
            _instance._musicEnabled = PlayerPrefs.GetInt(MusicEnabledKey, 1) == 1;
            _instance._musicSource.mute = !_instance._musicEnabled;
        }

        private AudioClip Load(string path)
        {
            return Resources.Load<AudioClip>("Sounds/" + path);
        }

        private void Play(AudioClip clip, float pitchMin = 0.95f, float pitchMax = 1.05f)
        {
            if (clip == null || !_isEnabled)
                return;
            _source.pitch = UnityEngine.Random.Range(pitchMin, pitchMax);
            _source.PlayOneShot(clip);
            _source.pitch = 1f;
        }

        private void Start()
        {
            StartMusic();
        }

        /// <summary>
        /// Charge et lance la musique d'ambiance en boucle (canal séparé).
        /// Appelée automatiquement au démarrage ; peut être rappelée après
        /// réinitialisation des préférences.
        /// </summary>
        public void StartMusic()
        {
            if (_musicSource == null) return;
            if (_music == null)
                _music = Load("Music");
            if (_music == null) return;

            if (_musicSource.clip != _music)
            {
                _musicSource.clip = _music;
                _musicSource.time = 0f;
            }
            if (_musicEnabled)
                _musicSource.Play();
        }

        /// <summary>Met la musique en pause (fin de partie, menus), sans l'arrêter.</summary>
        public void PauseMusic()
        {
            if (_musicSource != null && _musicSource.isPlaying)
                _musicSource.Pause();
        }

        /// <summary>Reprend la musique là où elle s'était arrêtée (retour au jeu).</summary>
        public void ResumeMusic()
        {
            if (_musicSource == null || !_musicEnabled || _musicSource.clip == null)
                return;
            if (!_musicSource.isPlaying)
                _musicSource.Play();
        }

        public void PlayConfirm() => Play(_confirm ?? (_confirm = Load("Confirm")));
        public void PlayFailure() => Play(_failure ?? (_failure = Load("Failure")), 0.9f, 1.1f);
        public void PlayClickedOut() => Play(_clickedOut ?? (_clickedOut = Load("Clicked_Out")), 0.9f, 1.05f);
        public void PlayDialogueBlip() => Play(_dialogueBlip ?? (_dialogueBlip = Load("Dialogue_Blip")), 0.9f, 1.1f);
        public void PlaySuccess() => Play(_success ?? (_success = Load("Success")), 1f, 1.02f);
        public void PlayMenuOpen() => Play(_menuOpen ?? (_menuOpen = Load("Menu_Open")));
        public void PlayMenuClose() => Play(_menuClose ?? (_menuClose = Load("Menu_Close")));
        public void PlayUnlock() => Play(_unlock ?? (_unlock = Load("Unlock")), 0.95f, 1.1f);
    }
}
