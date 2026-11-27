namespace Messenger.Domain.Entities;

public sealed class Attachment
{
    private Attachment() { }

    public Guid Id { get; private set; }
    public Guid UploaderId { get; private set; }
    public string StoredFileName { get; private set; } = null!;
    public string OriginalFileName { get; private set; } = null!;
    public string ContentType { get; private set; } = null!;
    public long SizeBytes { get; private set; }
    public int? Width { get; private set; }
    public int? Height { get; private set; }
    public DateTime UploadedAt { get; private set; }

    public static Attachment Create(Guid uploaderId, string storedFileName, string originalFileName,
        string contentType, long sizeBytes, int? width, int? height, DateTime now) => new()
    {
        Id = Guid.NewGuid(), UploaderId = uploaderId, StoredFileName = storedFileName,
        OriginalFileName = originalFileName, ContentType = contentType, SizeBytes = sizeBytes,
        Width = width, Height = height, UploadedAt = now,
    };
}

public sealed class StickerPack
{
    // Mapped by EF through the backing field (convention: _stickers).
    private readonly List<Sticker> _stickers = new();
    private StickerPack() { }

    public int Id { get; private set; }
    public string Name { get; private set; } = null!;
    /// <summary>A ready-to-use URL (e.g. "/stickers/pack1/thumb.png"), returned verbatim as thumbnailUrl.</summary>
    public string? ThumbnailPath { get; private set; }
    public IReadOnlyCollection<Sticker> Stickers => _stickers;
}

public sealed class Sticker
{
    private Sticker() { }

    public int Id { get; private set; }
    public int PackId { get; private set; }
    public string Code { get; private set; } = null!;
    /// <summary>A ready-to-use URL (e.g. "/stickers/pack1/smile.png"), returned verbatim as imageUrl.</summary>
    public string ImagePath { get; private set; } = null!;
}
