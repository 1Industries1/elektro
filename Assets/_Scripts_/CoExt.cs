// CoExt.cs (kleine Utility aus deinem Original -> aus EnemyEliteMech)
using System;

static class CoExt
{
    public static void Let<T>(this T obj, Action<T> f) { if (obj != null) f(obj); }
}
