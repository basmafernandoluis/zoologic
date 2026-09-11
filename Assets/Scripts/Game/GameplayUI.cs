using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Zoologic
{
    public static class GameplayUI
    {
        private const string UiFolder = "Sprites";
        private const string AnimalPrefix = "sp1_";
        private const string UiPrefix = "bak_";

        public static Sprite[] LoadAnimalSprites()
        {
            var all = Resources.LoadAll<Sprite>(UiFolder);
            var filtered = new System.Collections.Generic.List<Sprite>();
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && all[i].name.StartsWith(AnimalPrefix))
                    filtered.Add(all[i]);
            filtered.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return filtered.ToArray();
        }

        public static Sprite[] LoadUiSprites()
        {
            var all = Resources.LoadAll<Sprite>(UiFolder);
            var filtered = new System.Collections.Generic.List<Sprite>();
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && all[i].name.StartsWith(UiPrefix))
                    filtered.Add(all[i]);
            return filtered.ToArray();
        }

        public static Sprite LoadBanner(int index)
        {
            string path = index == 0 ? "Sprites/bak_0" : index == 1 ? "Sprites/bak_2" : "Sprites/bak_3";
            var s = Resources.Load<Sprite>(path);
            return s;
        }

        public static Sprite LoadDockSprite() => Resources.LoadAll<Sprite>("Sprites").FirstOrDefault(s => s.name == "bak_4") ?? Resources.Load<Sprite>("Sprites/bak_4");
        public static Sprite LoadResetSprite() => Resources.LoadAll<Sprite>("Sprites").FirstOrDefault(s => s.name == "b_11") ?? Resources.Load<Sprite>("Sprites/b_11");

        public static void ApplyBannerBkg(Image img, int index)
        {
            var s = LoadBanner(index);
            if (s != null) img.sprite = s;
            img.type = Image.Type.Sliced;
            img.color = Color.white;
        }

        private static float BottomInset
        {
            get
            {
                Rect safe = Screen.safeArea;
                float insetPx = safe.yMin;
                if (insetPx <= 1f) return 18f;
                return insetPx * (1920f / Mathf.Max(Screen.height, 1));
            }
        }

        public static void ApplyDockBkg(Image img)
        {
            if (img != null) Object.DestroyImmediate(img);
            var rt = img != null ? img.rectTransform : null;
            if (rt == null) return;
            rt.gameObject.name = "DockBois";
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(0f, 70f);
            rt.anchoredPosition = new Vector2(0f, Mathf.Max(BottomInset, 30f));
            rt.SetAsLastSibling();
        }

        public static void ApplyResetButton(Image img, Shadow shadow = null)
        {
            var s = LoadResetSprite();
            if (s != null)
            {
                img.sprite = s;
                img.type = Image.Type.Simple;
                img.color = Color.white;
            }
            else
            {
                img.sprite = null;
                img.color = new Color(0.92f, 0.36f, 0.42f, 1f);
            }
            img.preserveAspect = true;
            var rt = img.rectTransform;
            rt.gameObject.name = "ResetButton";
            rt.sizeDelta = new Vector2(72f, 72f);
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-56f, Mathf.Max(BottomInset, 30f) + 22f);
            rt.SetAsLastSibling();
            Shadow sh = shadow ?? img.GetComponent<Shadow>();
            if (sh == null) sh = img.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.25f);
            sh.effectDistance = new Vector2(0f, -4f);
        }
    }
}
