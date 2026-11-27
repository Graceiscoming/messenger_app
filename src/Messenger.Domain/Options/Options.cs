namespace Messenger.Domain.Options;

public sealed class ChatOptions
{
    public const string SectionName = "Chat";
    public int UnsendWindowHours { get; set; } = 24;
    public int MaxMessageLength { get; set; } = 2000;
    public int PageSize { get; set; } = 30;
    public int MaxCaptionLength { get; set; } = 500;
}

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Issuer { get; set; } = "messenger";
    public string Audience { get; set; } = "messenger-web";
    public string Key { get; set; } = "";
    public int AccessMinutes { get; set; } = 15;
    public int RefreshDays { get; set; } = 7;
}

public sealed class StorageOptions
{
    public const string SectionName = "Storage";
    public string UploadPath { get; set; } = "/data/uploads";
    public long MaxImageBytes { get; set; } = 5242880;
}

public sealed class AuthOptions
{
    public const string SectionName = "Auth";
    public int MaxFailedLogins { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 5;
}
