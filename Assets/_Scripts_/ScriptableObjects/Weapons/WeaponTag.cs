using System;

[Flags]
public enum WeaponTag {
    None         = 0,
    Ballistic    = 1 << 0,
    Pierce       = 1 << 1,
    Impact       = 1 << 2,
    Homing       = 1 << 3,
    Heavy        = 1 << 4,
    Rapid        = 1 << 5,
    SingleTarget = 1 << 6,
    Explosive    = 1 << 7,
    Melee        = 1 << 8,
}
