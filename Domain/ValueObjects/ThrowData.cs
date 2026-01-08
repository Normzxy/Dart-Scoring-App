namespace Domain.ValueObjects;
using Exceptions;

public sealed class ThrowData
{
    public int Value { get; }
    public int Multiplier { get; }
    
    public int Score => Value * Multiplier;

    public ThrowData(int value, int multiplier)
    {
        if (multiplier is < 1 or > 3)
            throw new InvalidHitException("Multiplier must be 1, 2 or 3.");
        
        switch (value)
        {
            case < 1 or > 20 when value != 25:
                throw new InvalidHitException("Value must be 1..20 or 25 (bull).");
            case 25 when multiplier is not (1 or 2):
                throw new InvalidHitException("Invalid bull multiplier.");
        }
        
        Value = value;
        Multiplier = multiplier;
    }
}