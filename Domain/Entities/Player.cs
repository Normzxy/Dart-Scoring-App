namespace Domain.Entities;

/// <summary>
/// Basic player information.
/// </summary>
public class Player(string name)
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; } = name;
}