using System;
using System.Reflection;
using UnityEngine;

namespace Zoologic
{
    /// <summary>
    /// Wrapper Google Analytics for Firebase, SANS dépendance de compilation :
    /// la liaison au SDK se fait par réflexion, uniquement s'il est présent
    /// (FirebaseAnalytics.unitypackage importé par ailleurs). Sans SDK, tous
    /// les appels sont des no-op silencieux (un log en éditeur).
    ///
    /// Confidentialité enfants (Familles) :
    /// - collecte coupée par défaut, activée uniquement si consentement UMP
    ///   résolu ET tranche 6+ (jamais en mode -5 ans, jamais sans consentement) ;
    /// - aucun SetUserId, aucune propriété perso, events sobres (nombres de jeu) ;
    /// - collecte de l'ID pub Android coupée via le manifest
    ///   (google_analytics_adid_collection_enabled=false).
    /// </summary>
    public static class AnalyticsManager
    {
        private static bool _bindingTried;
        private static bool _available;
        private static bool _announced;
        private static bool _consentResolved;

        private static Type _analyticsType;
        private static Type _parameterType;
        private static MethodInfo _mSetCollectionEnabled;
        private static MethodInfo _mResetAnalyticsData;
        private static MethodInfo _mLogEvent;

        /// <summary>True si le SDK Firebase Analytics est présent et lié.</summary>
        public static bool IsAvailable
        {
            get
            {
                EnsureBinding();
                return _available;
            }
        }

        /// <summary>
        /// Tente la liaison au SDK (idempotent). Coupe la collecte par défaut
        /// dès que possible : aucune donnée ne part avant RefreshConsent().
        /// Ne lève jamais.
        /// </summary>
        public static void Initialize()
        {
            try
            {
                EnsureBinding();
                SetCollectionEnabled(false);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Analytics] Initialize: " + e.GetType().Name);
            }
        }

        /// <summary>À appeler quand le flux UMP est résolu (6+ uniquement).</summary>
        public static void OnConsentResolved()
        {
            _consentResolved = true;
            RefreshConsent();
        }

        /// <summary>
        /// Recalcule l'autorisation : consentement UMP résolu ET pas en mode -5 ans.
        /// Appelé après UMP et à chaque changement de tranche d'âge.
        /// </summary>
        public static void RefreshConsent()
        {
            try
            {
                bool allow = _consentResolved && !AdMobManager.Under5Mode;
                SetCollectionEnabled(allow);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Analytics] RefreshConsent: " + e.GetType().Name);
            }
        }

        /// <summary>Active/coupe la collecte (no-op sans SDK). Ne lève jamais.</summary>
        public static void SetCollectionEnabled(bool enabled)
        {
            try
            {
                if (!EnsureBinding() || _mSetCollectionEnabled == null)
                    return;
                _mSetCollectionEnabled.Invoke(null, new object[] { enabled });
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Analytics] SetCollectionEnabled: " + e.GetType().Name);
            }
        }

        /// <summary>
        /// Efface les données analytics locales (changement vers -5 ans).
        /// No-op sans SDK. Ne lève jamais.
        /// </summary>
        public static void ResetData()
        {
            try
            {
                if (!EnsureBinding() || _mResetAnalyticsData == null)
                    return;
                _mResetAnalyticsData.Invoke(null, null);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Analytics] ResetData: " + e.GetType().Name);
            }
        }

        public static void LogLevelStart(int levelNumber, int gridSize, bool isDaily)
        {
            LogEvent("level_start",
                P("level_number", (long)levelNumber),
                P("grid_size", (long)gridSize),
                P("is_daily", isDaily ? "1" : "0"));
        }

        public static void LogLevelEnd(int levelNumber, bool success)
        {
            LogEvent("level_end",
                P("level_number", (long)levelNumber),
                P("success", success ? "1" : "0"));
        }

        public static void LogTutorialBegin() => LogEvent("tutorial_begin");
        public static void LogTutorialComplete() => LogEvent("tutorial_complete");

        public static void LogHintUsed(int levelNumber)
        {
            LogEvent("hint_used", P("level_number", (long)levelNumber));
        }

        public static void LogDailyClaim(int streak)
        {
            LogEvent("daily_claim", P("streak", (long)streak));
        }

        public static void LogAdReward(string rewardType)
        {
            LogEvent("ad_reward_granted", P("reward_type", rewardType ?? "unknown"));
        }

        public static void LogChestOpened(int world, int palier)
        {
            LogEvent("chest_opened", P("world", (long)world), P("palier", (long)palier));
        }

        public static void LogMascotUnlocked(string mascotId, string rarity)
        {
            LogEvent("mascot_unlocked", P("id", mascotId ?? "unknown"), P("rarity", rarity ?? "?"));
        }

        // ------------------------------------------------------------------

        private struct Param
        {
            public string Name;
            public object Value;
        }

        private static Param P(string name, long value) => new Param { Name = name, Value = value };
        private static Param P(string name, string value) => new Param { Name = name, Value = value ?? "" };

        private static void LogEvent(string name, params Param[] parameters)
        {
            try
            {
                if (!EnsureBinding() || _mLogEvent == null)
                    return;
                if (string.IsNullOrEmpty(name))
                    return;
                Array args = Array.CreateInstance(_parameterType, parameters.Length);
                for (int i = 0; i < parameters.Length; i++)
                {
                    object boxed = parameters[i].Value is long l
                        ? Activator.CreateInstance(_parameterType, parameters[i].Name, l)
                        : Activator.CreateInstance(_parameterType, parameters[i].Name, (string)parameters[i].Value);
                    args.SetValue(boxed, i);
                }
                _mLogEvent.Invoke(null, new object[] { name, args });
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Analytics] LogEvent: " + e.GetType().Name);
            }
        }

        private static bool EnsureBinding()
        {
            if (_bindingTried)
                return _available;
            _bindingTried = true;
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    string asmName = asm.GetName().Name;
                    if (_analyticsType == null && asmName == "Firebase.Analytics")
                        _analyticsType = asm.GetType("Firebase.Analytics.FirebaseAnalytics");
                    if (_parameterType == null && asmName == "Firebase.Analytics")
                        _parameterType = asm.GetType("Firebase.Analytics.Parameter");
                    if (_analyticsType != null && _parameterType != null)
                        break;
                }
                if (_analyticsType == null || _parameterType == null)
                {
                    AnnounceOnce("[Analytics] SDK Firebase absent : collecte désactivée (no-op).");
                    _available = false;
                    return false;
                }
                _mSetCollectionEnabled = _analyticsType.GetMethod(
                    "SetAnalyticsCollectionEnabled", new[] { typeof(bool) });
                _mResetAnalyticsData = _analyticsType.GetMethod(
                    "ResetAnalyticsData", Type.EmptyTypes);
                foreach (var m in _analyticsType.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    if (m.Name != "LogEvent")
                        continue;
                    var ps = m.GetParameters();
                    if (ps.Length == 2 && ps[0].ParameterType == typeof(string)
                        && ps[1].ParameterType.IsArray
                        && ps[1].ParameterType.GetElementType() == _parameterType)
                    {
                        _mLogEvent = m;
                        break;
                    }
                }
                _available = _mSetCollectionEnabled != null && _mLogEvent != null;
                AnnounceOnce(_available
                    ? "[Analytics] SDK Firebase lié."
                    : "[Analytics] SDK présent mais API inattendue : no-op.");
                return _available;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Analytics] Binding: " + e.GetType().Name);
                _available = false;
                return false;
            }
        }

        private static void AnnounceOnce(string message)
        {
            if (_announced)
                return;
            _announced = true;
            Debug.Log(message);
        }
    }
}
