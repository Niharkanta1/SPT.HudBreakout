using TMPro;
using UnityEngine;

namespace HudBreakout
{
    internal static class FontHelper
    {
        public static TMP_FontAsset BenderNormal { get; private set; }

        public static void Load()
        {
            BenderNormal = Resources.Load<TMP_FontAsset>(
                "ui/fonts/jovanny lemonad - bender normal sdf");
            if (!BenderNormal)
            {
                HudBreakoutPlugin.Log.LogError(
                    "Could not load Tarkov's Bender Normal font asset; HUD text will use TextMeshPro's default.");
            }
        }
    }
}
