using Alexandria.Rules;

namespace Alexandria.Battle;

public sealed class Inventory
{
    public const Int32 StackMax = 99;

    private readonly Dictionary<ItemID, Int32> _counts = [];

    public Int32 this[ItemID item] => _counts.GetValueOrDefault(item);

    public void Add(ItemID item, Int32 count = 1)
        => _counts[item] = Math.Min(StackMax, this[item] + count);

    public Boolean Remove(ItemID item, Int32 count = 1)
    {
        var held = this[item];
        if (held < count)
            return false;
        if (held == count)
            _counts.Remove(item);
        else
            _counts[item] = held - count;
        return true;
    }

    public IEnumerable<KeyValuePair<ItemID, Int32>> Contents => _counts;
}

