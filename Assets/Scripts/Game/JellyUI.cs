using UnityEngine;

namespace Zoologic
{
    public static class JellyUI
    {
        public static Sprite ButtonGreen => Load("UI/Jelly/Button_Green");
        public static Sprite ButtonRed => Load("UI/Jelly/Button_Red");
        public static Sprite ButtonYellow => Load("UI/Jelly/Button_Yellow");
        public static Sprite ButtonGrey => Load("UI/Jelly/Button_Grey");
        public static Sprite SmallGreen => Load("UI/Jelly/SmallButton_Green");
        public static Sprite SmallRed => Load("UI/Jelly/SmallButton_Red");
        public static Sprite SmallYellow => Load("UI/Jelly/SmallButton_Yellow");
        public static Sprite SmallGrey => Load("UI/Jelly/SmallButton_Grey");

        private static Sprite Load(string path)
        {
            var s = Resources.Load<Sprite>(path);
            if (s != null) return s;
            return null;
        }

        public static void ApplyJellyButton(UnityEngine.UI.Button btn, UnityEngine.UI.Image img, Sprite normal, Sprite highlighted, Sprite pressed, Sprite disabled)
        {
            if (img == null || btn == null) return;
            img.sprite = normal;
            img.type = UnityEngine.UI.Image.Type.Sliced;
            img.preserveAspect = false;
            btn.transition = UnityEngine.UI.Selectable.Transition.SpriteSwap;
            var state = new UnityEngine.UI.SpriteState();
            state.highlightedSprite = highlighted;
            state.pressedSprite = pressed;
            state.selectedSprite = normal;
            state.disabledSprite = disabled;
            btn.spriteState = state;
            btn.targetGraphic = img;
        }
    }
}
