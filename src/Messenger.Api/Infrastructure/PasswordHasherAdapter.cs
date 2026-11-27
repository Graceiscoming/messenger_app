using Messenger.Domain.Abstractions;
using Messenger.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace Messenger.Api.Infrastructure;

/// <summary>Adapter over ASP.NET Core's PasswordHasher (PBKDF2, versioned format). The user argument is unused by the hasher.</summary>
public sealed class PasswordHasherAdapter : Messenger.Domain.Abstractions.IPasswordHasher
{
    private readonly PasswordHasher<User> _hasher = new();

    public string Hash(string password) => _hasher.HashPassword(null!, password);

    public bool Verify(string passwordHash, string password)
    {
        if (string.IsNullOrEmpty(passwordHash)) return false;
        return _hasher.VerifyHashedPassword(null!, passwordHash, password) != PasswordVerificationResult.Failed;
    }
}
