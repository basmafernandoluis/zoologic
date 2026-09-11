using TMPro;
using UnityEngine;

namespace Zoologic.Localization
{
    [RequireComponent(typeof(TMP_Text))]
    public sealed class LocalizedText : MonoBehaviour
    {
        [SerializeField] private string _key;
        [SerializeField] private bool _mirrorAlignment = true;
        private TMP_Text _tmp;
        private TextAlignmentOptions _baseAlignment;
        private bool _baseCaptured;

        public string Key { get => _key; set { _key = value; Refresh(); } }

        private void Awake()
        {
            _tmp = GetComponent<TMP_Text>();
            _baseAlignment = _tmp.alignment;
            _baseCaptured = true;
        }

        private void OnEnable()
        {
            LocalizationManager.OnLanguageChanged += Refresh;
            Refresh();
        }

        private void OnDisable() => LocalizationManager.OnLanguageChanged -= Refresh;

        public void Refresh()
        {
            if (_tmp == null) _tmp = GetComponent<TMP_Text>();
            if (_tmp == null || string.IsNullOrEmpty(_key)) return;
            if (!_baseCaptured) { _baseAlignment = _tmp.alignment; _baseCaptured = true; }
            _tmp.text = LocalizationManager.Get(_key);
            if (_mirrorAlignment) _tmp.alignment = LocalizationManager.Mirror(_baseAlignment);
            LocalizationManager.ApplyTo(_tmp);
        }

        public void Refresh(string key, params object[] args)
        {
            if (_tmp == null) _tmp = GetComponent<TMP_Text>();
            if (_tmp == null) return;
            if (!_baseCaptured) { _baseAlignment = _tmp.alignment; _baseCaptured = true; }
            _tmp.text = LocalizationManager.Get(key, args);
            if (_mirrorAlignment) _tmp.alignment = LocalizationManager.Mirror(_baseAlignment);
            LocalizationManager.ApplyTo(_tmp);
        }
    }
}
