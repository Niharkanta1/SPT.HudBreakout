using System;
using System.Linq;
using System.Reflection;
using EFT;

namespace HudBreakout
{
    internal static class HudLocalization
    {
        private static readonly MethodInfo Method = Find();

        public static string Localized(string key)
        {
            if (string.IsNullOrEmpty(key) || Method == null) return key ?? "";

            try
            {
                var text = Method.Invoke(null, new object[] { key, null }) as string;
                return string.IsNullOrEmpty(text) ? key : text;
            }
            catch (Exception ex)
            {
                HudBreakoutPlugin.Log.LogWarning("localize failed for '" + key + "': " + ex.Message);
                return key;
            }
        }

        // EFT's string extension is obfuscated into a random class, so look for the class
        // that also has ParseLocalization and take its Localized(string, string)
        private static MethodInfo Find()
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Static;

            foreach (var type in typeof(Player).Assembly.GetTypes())
            {
                var methods = type.GetMethods(flags);
                if (!methods.Any(m => m.Name == "ParseLocalization")) continue;

                var found = methods.FirstOrDefault(m =>
                {
                    var args = m.GetParameters();
                    return m.Name == "Localized" && args.Length == 2
                        && args[0].ParameterType == typeof(string)
                        && args[1].ParameterType == typeof(string);
                });
                if (found != null) return found;
            }

            HudBreakoutPlugin.Log.LogWarning("localization method not found, HUD will show raw keys");
            return null;
        }
    }
}