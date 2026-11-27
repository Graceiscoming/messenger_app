using Messenger.Domain.Abstractions;

namespace Messenger.Api.Infrastructure;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
