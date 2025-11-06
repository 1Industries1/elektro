public enum Rarity : int { Common = 0, Rare = 1, Epic = 2, Legendary = 3 }

public static class ChoiceCodec
{
    // 0..255 Basis-IDs, 256.. Kodierung offen
    private const int SHIFT = 8;
    private const int MASK  = (1 << SHIFT) - 1;

    public static int Encode(int baseId, Rarity r) => (baseId << SHIFT) | ((int)r & MASK);
    public static int BaseId(int choiceId)        => choiceId >> SHIFT;
    public static Rarity GetRarity(int choiceId)  => (Rarity)(choiceId & MASK);
}