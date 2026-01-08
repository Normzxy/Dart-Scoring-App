namespace Domain.Entities;
using Domain.ValueObjects;

public class Throw(Guid playerId, ThrowData data)
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid PlayerId { get; private set; } = playerId;
    public ThrowData Data { get; } = data ?? throw new ArgumentNullException(nameof(data));
    public DateTime Timestamp { get; private set; } = DateTime.UtcNow;
}