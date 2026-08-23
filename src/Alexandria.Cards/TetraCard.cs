namespace Alexandria.Cards;

/// <summary>A Tetra Master card. Stats are full bytes and the card face shows
/// only the high nibble.</summary>
public sealed record TetraCard(
    Int32 ID,
    String Name,
    Byte Attack,
    CardType Type,
    Byte PhysicalDefense,
    Byte MagicalDefense,
    CardIcon Icon)
{
    /// <summary>The four characters printed on the card.</summary>
    public String Face
        => $"{Attack >> 4:X}{Type.Letter}{PhysicalDefense >> 4:X}{MagicalDefense >> 4:X}";
}

