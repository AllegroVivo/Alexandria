namespace Alexandria.Formats.Scripts;

public sealed record EbFunction(UInt16 Tag, Int32 EntryPoint, Byte[] Code)
{
    public String TagName => Tag switch
    {
        0 => "init",
        5 => "atb",
        6 => "counter",
        9 => "death",
        _ => $"tag{Tag}"
    };
}

