namespace Alexandria.Formats.Scripts;

/// <summary>The pools every script object shares (mirror: EventState.gEventGlobal
/// 2048 B, engine-wide and part of save data; EventContext.mapvar 80 B, zeroed at
/// each scene load and shared between entries).</summary>
public sealed class EbState
{
    public Byte[] Global { get; } = new Byte[2048];
    public Byte[] Map { get; } = new Byte[80];
}

