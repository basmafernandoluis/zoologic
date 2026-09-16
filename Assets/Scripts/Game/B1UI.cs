using System.Collections.Generic;
using UnityEngine;

namespace Zoologic
{
    public static class B1UI
    {
        private static Dictionary<string, Sprite> _cache;
        private static Sprite Load(string subName)
        {
            try
            {
                if (_cache == null)
                {
                    _cache = new Dictionary<string, Sprite>(System.StringComparer.Ordinal);
                    var all = Resources.LoadAll<Sprite>("Sprites/b");
                    if (all != null)
                        foreach (var sp in all)
                            if (sp != null && !_cache.ContainsKey(sp.name)) _cache[sp.name] = sp;
                }
                if (_cache.TryGetValue(subName, out var found) && found != null) return found;
            }
            catch { }
            return null;
        }
        public static Sprite Bubble => Load("b_21") ?? Load("b_4");
        public static Sprite WoodBar => Load("b_4");
        public static Sprite Skip => Load("b_44") ?? Load("b_43");
        public static Sprite Primary => Load("b_38") ?? Load("b_32") ?? Load("b_44");
        public static Sprite StarBar => Load("b_42") ?? Load("b_15");
        public static Sprite CoinBar => Load("b_32");
        public static Sprite HeartBar => Load("b_38");
        public static Sprite Square => Load("b_31");
        public static Sprite Retry => Load("b_0") ?? Load("b_16");
        public static Sprite Get(string name) => Load(name);
    }
}
