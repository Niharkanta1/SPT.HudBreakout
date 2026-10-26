using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using EFT;
using EFT.Ballistics;
using EFT.UI;
using HarmonyLib;
using UnityEngine;

namespace HudBreakout
{
    // Patches only raise HudEvents, all the UI work happens in HudRoot
    [HarmonyPatch(typeof(Player), nameof(Player.ApplyDamageInfo))]
    internal static class ApplyDamageInfoPatch
    {
        [HarmonyPrefix]
        private static void Prefix(Player __instance, out bool __state)
        {
            __state = __instance.HealthController != null && __instance.HealthController.IsAlive;
        }

        [HarmonyPostfix]
        private static void Postfix(Player __instance, DamageInfo damageInfo, EBodyPart bodyPartType, bool __state)
        {
            if (!__state) return;

            try
            {
                var player = HudRoot.MainPlayer;
                if (player == null) return;

                var attacker = damageInfo.Player?.iPlayer;

                if (__instance == player)
                {
                    // fall back to the bullet direction for self damage / unknown attackers
                    var fromOther = attacker != null && attacker.ProfileId != player.ProfileId;
                    HudEvents.Raise(new DamageTakenEvent
                    {
                        Damage = damageInfo.Damage,
                        Part = bodyPartType,
                        SourceDirection = fromOther ? attacker.Position - player.Position : -damageInfo.Direction
                    });
                }
                else if (attacker != null && attacker.ProfileId == player.ProfileId)
                {
                    HudEvents.Raise(new HitEvent
                    {
                        Damage = damageInfo.Damage,
                        Part = bodyPartType,
                        Point = damageInfo.HitPoint,
                        Killed = !__instance.HealthController.IsAlive
                    });
                }
            }
            catch (Exception ex)
            {
                HudBreakoutPlugin.Log.LogError("ApplyDamageInfo: " + ex);
            }
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.OnBeenKilledByAggressor))]
    internal static class KilledPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Player __instance, IPlayer aggressor, DamageInfo damageInfo, EBodyPart bodyPart)
        {
            try
            {
                var player = HudRoot.MainPlayer;
                if (player == null || aggressor == null || aggressor.ProfileId != player.ProfileId || __instance == player)
                    return;

                var info = __instance.Profile.Info;
                HudEvents.Raise(new KillEvent
                {
                    Name = HudLocalization.Localized(__instance.Profile.Nickname),
                    Weapon = HudLocalization.Localized(damageInfo.Weapon?.ShortName ?? ""),
                    Part = bodyPart,
                    Distance = Vector3.Distance(aggressor.Position, __instance.Position),
                    Level = info.Level,
                    Headshot = bodyPart == EBodyPart.Head,
                    Tag = GetTag(__instance)
                });
            }
            catch (Exception ex)
            {
                HudBreakoutPlugin.Log.LogError("Kill: " + ex);
            }
        }

        private static string GetTag(Player victim)
        {
            var info = victim.Profile.Info;
            if (info.Settings.Role.ToString().StartsWith("boss", StringComparison.OrdinalIgnoreCase)) return "BOSS";
            if (!victim.IsAI) return "PMC";
            return info.Side == EPlayerSide.Savage ? "SCAV" : "PMC BOT";
        }
    }

    [HarmonyPatch]
    internal static class InventoryScreenPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            return AccessTools.GetDeclaredMethods(typeof(InventoryScreen))
                .Where(m => m.Name == "Show" || m.Name == "Close");
        }

        [HarmonyPostfix]
        private static void Postfix(MethodBase __originalMethod)
        {
            HudRoot.SetInventoryOpen(__originalMethod.Name == "Show");
        }
    }

    // Optional: only patched when Fika is installed. Fika calls this once when the local player extracts
    [HarmonyPatch]
    internal static class FikaExtractPatch
    {
        internal const string FikaGuid = "com.fika.core";
        private static MethodBase _target;

        private static bool Prepare()
        {
            // TypeByName logs a warning when the type is missing, so only look it up with Fika loaded
            if (_target == null && Chainloader.PluginInfos.ContainsKey(FikaGuid))
                _target = AccessTools.Method(AccessTools.TypeByName("Fika.Core.Main.GameMode.CoopGame"), "Extract");
            return _target != null;
        }

        private static MethodBase TargetMethod() => _target;

        [HarmonyPostfix]
        private static void Postfix(Player player)
        {
            if (player != null && player == HudRoot.MainPlayer)
                HudRoot.SetExtracted();
        }
    }
}