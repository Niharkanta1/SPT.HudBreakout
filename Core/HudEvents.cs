using System;
using EFT;
using UnityEngine;

namespace HudBreakout
{
    internal struct HitEvent
    {
        public float Damage;
        public EBodyPart Part;
        public Vector3 Point;
        public bool Killed;
    }

    internal struct DamageTakenEvent
    {
        public float Damage;
        public EBodyPart Part;
        public Vector3 SourceDirection;
    }

    internal struct KillEvent
    {
        public string Name, Weapon, Tag;
        public EBodyPart Part;
        public float Distance;
        public int Level;
        public bool Headshot;
    }

    internal static class HudEvents
    {
        public static event Action<HitEvent> HitDealt;
        public static event Action<DamageTakenEvent> DamageTaken;
        public static event Action<KillEvent> Kill;

        public static void Raise(HitEvent hitEvent) => HitDealt?.Invoke(hitEvent);
        public static void Raise(DamageTakenEvent damageEvent) => DamageTaken?.Invoke(damageEvent);
        public static void Raise(KillEvent killEvent) => Kill?.Invoke(killEvent);
    }
}
