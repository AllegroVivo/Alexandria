namespace Alexandria.Battle;

public sealed class Inventory
{
    public const Int32 StackMax = 99;

    private readonly Dictionary<Int32, Int32> _counts = [];

    public Int32 this[Int32 itemId] => _counts.GetValueOrDefault(itemId);

    public void Add(Int32 itemId, Int32 count = 1)
        => _counts[itemId] = Math.Min(StackMax, this[itemId] + count);

    public Boolean Remove(Int32 itemId, Int32 count = 1)
    {
        var held = this[itemId];
        if (held < count)
            return false;
        if (held == count)
            _counts.Remove(itemId);
        else
            _counts[itemId] = held - count;
        return true;
    }

    public IEnumerable<KeyValuePair<Int32, Int32>> Contents => _counts;
}

