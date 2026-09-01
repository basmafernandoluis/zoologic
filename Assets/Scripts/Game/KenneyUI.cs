using UnityEngine;

namespace Zoologic
{
    /// <summary>Accès centralisé aux sprites Kenney utilisés par l'UI procédurale.</summary>
    public static class KenneyUI
    {
        public static Sprite Button(string color)
        {
            return Resources.Load<Sprite>($"UI/Kenney/{color}/button_round_depth_gradient");
        }

        public static Sprite FlatButton(string color)
        {
            return Resources.Load<Sprite>($"UI/Kenney/{color}/button_round_depth_flat");
        }

        public static Sprite Cross()
        {
            return Resources.Load<Sprite>("UI/Kenney/Blue/icon_cross");
        }

        public static Sprite Checkmark()
        {
            return Resources.Load<Sprite>("UI/Kenney/Green/icon_checkmark");
        }
    }
}
