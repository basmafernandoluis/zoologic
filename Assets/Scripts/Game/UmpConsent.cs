using System;
using UnityEngine;

namespace Zoologic
{
    /// <summary>
    /// Consentement GDPR/TTCF via UMP (User Messaging Platform) :
    /// - 5 ans et moins : jamais appelé (zéro pub, pas de consentement requis) ;
    /// - 6 ans et plus : met à jour ConsentInformation, affiche le formulaire
    ///   UMP si requis (EEE/RU), puis libère l'init des pubs dans tous les cas
    ///   (fail-open : en cas d'erreur on garde NPA=1 + TFCD déjà configurés).
    /// </summary>
    public static class UmpConsent
    {
        public static void RequestConsent(Action onDone)
        {
            try
            {
                var request = new GoogleMobileAds.Ump.Api.ConsentRequestParameters
                {
                    TagForUnderAgeOfConsent = false
                };
                GoogleMobileAds.Ump.Api.ConsentInformation.Update(
                    request,
                    (GoogleMobileAds.Ump.Api.FormError updateError) =>
                    {
                        if (updateError != null)
                        {
                            Debug.LogWarning("[UMP] Consent update failed: " + updateError.Message + " (fail-open NPA)");
                            try { onDone?.Invoke(); } catch { }
                            return;
                        }
                        if (GoogleMobileAds.Ump.Api.ConsentInformation.ConsentStatus
                            == GoogleMobileAds.Ump.Api.ConsentStatus.Required)
                        {
                            AdMobManager.AdLog("[UMP] Consent required: showing UMP form");
                            GoogleMobileAds.Ump.Api.ConsentForm.LoadAndShowConsentFormIfRequired(
                                (GoogleMobileAds.Ump.Api.FormError formError) =>
                                {
                                    if (formError != null)
                                        Debug.LogWarning("[UMP] Consent form failed: " + formError.Message + " (fail-open NPA)");
                                    else
                                        AdMobManager.AdLog("[UMP] Consent form resolved: " + GoogleMobileAds.Ump.Api.ConsentInformation.ConsentStatus);
                                    try { onDone?.Invoke(); } catch { }
                                });
                        }
                        else
                        {
                            AdMobManager.AdLog("[UMP] Consent not required: " + GoogleMobileAds.Ump.Api.ConsentInformation.ConsentStatus);
                            try { onDone?.Invoke(); } catch { }
                        }
                    });
            }
            catch (Exception e)
            {
                Debug.LogWarning("[UMP] RequestConsent exception: " + e.Message + " (fail-open NPA)");
                try { onDone?.Invoke(); } catch { }
            }
        }
    }
}
