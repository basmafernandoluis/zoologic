using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Zoologic
{
    /// <summary>
    /// Garantit un EventSystem unique et vivant (cause n°1 des "boutons morts"
    /// après rebuild UI : doublon, module old-input résiduel, ou EventSystem
    /// détruit avec un panel). Appeler après chaque (re)construction d'UI.
    /// Ne lève jamais. Pas de GetComponent dans Update (appels ponctuels).
    /// </summary>
    public static class UiInputGuard
    {
        public static void EnsureSingleEventSystem()
        {
            try
            {
                var all = Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
                EventSystem keep = EventSystem.current;
                if (keep == null || !keep.gameObject.activeInHierarchy || !keep.enabled)
                {
                    keep = null;
                    foreach (var e in all)
                    {
                        if (e != null && e.gameObject.activeInHierarchy && e.enabled)
                        {
                            keep = e;
                            break;
                        }
                    }
                    if (keep == null && all.Length > 0) keep = all[0];
                }
                if (keep == null)
                {
                    var go = new GameObject("EventSystem", typeof(EventSystem));
                    keep = go.GetComponent<EventSystem>();
                }
                // Détruit les doublons (2 EventSystems = clics perdus / doubles).
                foreach (var e in all)
                {
                    if (e != null && e != keep)
                        Object.Destroy(e.gameObject);
                }
                if (keep.GetComponent<InputSystemUIInputModule>() == null)
                    keep.gameObject.AddComponent<InputSystemUIInputModule>();
                // Chien de garde old-input : le StandaloneInputModule résiduel
                // casse l'UI sous "Input System (New)".
                foreach (var m in Object.FindObjectsByType<StandaloneInputModule>(FindObjectsSortMode.None))
                    Object.Destroy(m);
            }
            catch { }
        }
    }
}
