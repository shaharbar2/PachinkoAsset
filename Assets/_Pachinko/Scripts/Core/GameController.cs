using System;
using System.Collections.Generic;
using UnityEngine;

public enum GameState { Loading, Idle, Dropping, Resolving, RoundEnd }

public class GameController
{
    public GameState State     { get; private set; } = GameState.Loading;
    public int       ChipsLeft { get; private set; }

    public event Action<GameState> OnStateChanged;
    public event Action<int>       OnDropStarted;  // (dropIndex)
    public event Action<int>       OnBallLanded;   // (bucketValue)
    public event Action<int>       OnRoundEnded;   // (roundTotal)

    private static readonly Dictionary<GameState, GameState[]> ValidNext = new()
    {
        { GameState.Loading,   new[] { GameState.Idle } },
        { GameState.Idle,      new[] { GameState.Dropping } },
        { GameState.Dropping,  new[] { GameState.Resolving } },
        { GameState.Resolving, new[] { GameState.Idle, GameState.RoundEnd } },
        { GameState.RoundEnd,  new[] { GameState.Loading } },
    };

    public void ConfigLoaded(int chips)
    {
        ChipsLeft = chips;
        Transition(GameState.Idle);
    }

    public bool CanDrop() => State == GameState.Idle && ChipsLeft > 0;

    public void BeginDrop(int dropIndex)
    {
        ChipsLeft--;
        Transition(GameState.Dropping);
        OnDropStarted?.Invoke(dropIndex);
    }

    public void BallLanded(int bucketValue)
    {
        Transition(GameState.Resolving);
        OnBallLanded?.Invoke(bucketValue);
    }

    public void ResolveComplete(int roundTotal)
    {
        if (ChipsLeft > 0)
            Transition(GameState.Idle);
        else
        {
            Transition(GameState.RoundEnd);
            OnRoundEnded?.Invoke(roundTotal);
        }
    }

    private void Transition(GameState next)
    {
        if (Array.IndexOf(ValidNext[State], next) < 0)
        {
            Debug.LogWarning($"[GameController] Invalid transition {State} → {next}. Ignored.");
            return;
        }
        State = next;
        OnStateChanged?.Invoke(next);
    }
}
