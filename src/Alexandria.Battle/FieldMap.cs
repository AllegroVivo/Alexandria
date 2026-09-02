using Alexandria.Formats.Scripts;

namespace Alexandria.Battle;

public class FieldMap(EbScript script, WorldState world, FieldContext context, Random rng)
{
    private readonly FieldHost _host = new(world, context, rng);
    private readonly EbState _state = new();
    private readonly List<(Int32 Uid, EbVM Vm)> _objects = [];

    public IReadOnlyList<(Int32 Uid, EbVM Vm)> Objects => _objects;

    public void Load()
    {
        Instantiate(0, 0);

        for (var i = 0; i < _objects.Count; i++)
        {
            RunIfPresent(_objects[i], 0);
            foreach (var (entry, uid) in _host.DrawnSpawns())
                Instantiate(entry, uid);
        }
    }

    public void Run(Int32 uid, Int32 tag)
    {
        foreach (var obj in _objects.Where(o=>o.Uid==uid))
            RunIfPresent(obj, tag);
    }

    private void Instantiate(Int32 entry, Int32 uid)
    {
        if (entry < 0 || entry >= script.Entries.Count)
            return;
        _objects.Add((uid, new EbVM(script.Entries[entry], _host, _state, uid)));
    }

    private static void RunIfPresent((Int32 Uid, EbVM Vm) obj, Int32 tag)
    {
        if (obj.Vm.Entry.Functions.Any(f => f.Tag == tag))
            obj.Vm.RunFunction((UInt16)tag);
    }
}

