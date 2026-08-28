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

        private static SFXManager _instance;

        private AudioSource _source;

        private bool _isEnabled;

        // Clips chargés une seule fois (lazy).
        private AudioClip _confirm;
        private AudioClip _failure;
        private AudioClip _clickedOut;
        private AudioClip _dialogueBlip;
        private AudioClip _success;
        private AudioClip _menuOpen;
        private AudioClip _menuClose;
        private AudioClip _unlock;

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
