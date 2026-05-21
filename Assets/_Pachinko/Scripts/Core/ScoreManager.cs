using System;
using System.Collections.Generic;

public interface IScoreModifier
{
    int Modify(int baseScore);
}

public class ScoreManager
{
    public int RoundTotal { get; private set; }

    public event Action<int, int> OnScoreChanged; // (bucketValue, roundTotal)

    private readonly List<IScoreModifier> _modifiers = new();

    public void Reset() => RoundTotal = 0;

    public void AddModifier(IScoreModifier mod)    => _modifiers.Add(mod);
    public void RemoveModifier(IScoreModifier mod) => _modifiers.Remove(mod);

    public void AddScore(int rawValue)
    {
        int value = rawValue;
        foreach (var mod in _modifiers)
            value = mod.Modify(value);

        RoundTotal += value;
        OnScoreChanged?.Invoke(value, RoundTotal);
    }
}
