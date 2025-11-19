using System;
using UnityEngine;

public static class WeaponUiUtils
{
    // Konfiguration pro Waffenslot
    public struct WeaponSlot
    {
        public string key;
        public Func<PlayerWeapons, WeaponDefinition> GetDef;
        public Func<PlayerWeapons, int> GetLevel;
        public Func<PlayerWeapons, WeaponRuntime> GetRuntime;
        public Func<PlayerWeapons, WeaponRuntime, float> ComputeDps;
        public Func<PlayerWeapons, WeaponRuntime, string> BuildExtraText;
    }

    public static readonly WeaponSlot[] WeaponSlots =
    {
        new WeaponSlot
        {
            key = "Weapon_Cannon",
            GetDef    = pw => pw.cannonDef,
            GetLevel  = pw => pw.cannonLevel.Value,
            GetRuntime= pw => pw.CannonRuntime,
            ComputeDps= (pw, rt) =>
            {
                if (rt == null) return 0f;
                return rt.damagePerShot * rt.shotsPerSecond;
            },
            BuildExtraText = (pw, rt) =>
            {
                if (rt == null) return null;
                float dps = rt.damagePerShot * rt.shotsPerSecond;
                return $"DPS≈ {dps:0.#}  |  {rt.shotsPerSecond:0.##}/s";
            }
        },
        new WeaponSlot
        {
            key = "Weapon_Blaster",
            GetDef    = pw => pw.blasterDef,
            GetLevel  = pw => pw.blasterLevel.Value,
            GetRuntime= pw => pw.BlasterRuntime,
            ComputeDps= (pw, rt) =>
            {
                if (rt == null) return 0f;
                return rt.damagePerShot * rt.shotsPerSecond;
            },
            BuildExtraText = (pw, rt) =>
            {
                if (rt == null) return null;
                float dps = rt.damagePerShot * rt.shotsPerSecond;
                return $"DPS≈ {dps:0.#}  |  {rt.shotsPerSecond:0.##}/s";
            }
        },
        new WeaponSlot
        {
            key = "Weapon_Grenade",
            GetDef    = pw => pw.grenadeDef,
            GetLevel  = pw => pw.grenadeLevel.Value,
            GetRuntime= pw => pw.GrenadeRuntime,
            ComputeDps= (pw, rt) =>
            {
                if (rt == null) return 0f;
                var def = pw.grenadeDef;
                int salvo = rt.salvoCount > 0
                    ? rt.salvoCount
                    : (def != null ? Mathf.Max(1, def.baseSalvoCount) : 1);
                return rt.damagePerShot * rt.shotsPerSecond * salvo;
            },
            BuildExtraText = (pw, rt) =>
            {
                if (rt == null) return null;
                var def = pw.grenadeDef;
                int salvo = rt.salvoCount > 0
                    ? rt.salvoCount
                    : (def != null ? Mathf.Max(1, def.baseSalvoCount) : 1);
                float dps = rt.damagePerShot * rt.shotsPerSecond * salvo;
                return $"DPS≈ {dps:0.#}  |  {rt.shotsPerSecond:0.##} salvos/s × {salvo}";
            }
        },
        new WeaponSlot
        {
            key = "Weapon_Lightning",
            GetDef    = pw => pw.lightningDef,
            GetLevel  = pw => pw.lightningLevel.Value,
            GetRuntime= pw => pw.LightningRuntime,
            ComputeDps= (pw, rt) =>
            {
                if (rt == null) return 0f;
                return rt.damagePerShot * rt.shotsPerSecond;
            },
            BuildExtraText = (pw, rt) =>
            {
                if (rt == null) return null;
                float dps = rt.damagePerShot * rt.shotsPerSecond;
                return $"DPS≈ {dps:0.#}  |  {rt.shotsPerSecond:0.##}/s";
            }
        },
        new WeaponSlot
        {
            key = "Weapon_Orbital",
            GetDef    = pw => pw.orbitalDef,
            GetLevel  = pw => pw.orbitalLevel.Value,
            GetRuntime= pw => pw.OrbitalRuntime,
            ComputeDps= (pw, rt) =>
            {
                if (rt == null) return 0f;
                var def = pw.orbitalDef;
                int salvo = rt.salvoCount > 0
                    ? rt.salvoCount
                    : (def != null ? Mathf.Max(1, def.baseSalvoCount) : 1);
                return rt.damagePerShot * rt.shotsPerSecond * salvo;
            },
            BuildExtraText = (pw, rt) =>
            {
                if (rt == null) return null;
                var def = pw.orbitalDef;
                int salvo = rt.salvoCount > 0
                    ? rt.salvoCount
                    : (def != null ? Mathf.Max(1, def.baseSalvoCount) : 1);
                float dps = rt.damagePerShot * rt.shotsPerSecond * salvo;
                return $"DPS≈ {dps:0.#}  |  {rt.shotsPerSecond:0.##} ticks/s × {salvo}";
            }
        },
    };
}
