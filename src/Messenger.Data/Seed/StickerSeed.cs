namespace Messenger.Data;

/// <summary>Seed rows for HasData. Anonymous objects are used because entity constructors are private.</summary>
internal static class StickerSeed
{
    public static readonly string[] Codes =
        ["hello", "thanks", "love", "laugh", "cry", "ok", "sorry", "wow", "angry", "sleepy", "yes", "no"];

    public static object[] Packs =>
        [new { Id = 1, Name = "basic", ThumbnailPath = "/stickers/basic/hello.svg" }];

    public static object[] Stickers =>
        Codes.Select((code, i) => (object)new { Id = i + 1, PackId = 1, Code = code, ImagePath = $"/stickers/basic/{code}.svg" }).ToArray();
}
