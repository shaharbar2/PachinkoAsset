using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Pool;

public class GameBootstrapper : MonoBehaviour
{
    [SerializeField] private BoardView  _boardView;
    [SerializeField] private UIController _ui;
    [SerializeField] private BallView   _ballPrefab;
    [SerializeField] private string     _boardId = "default";

    private GameController             _game;
    private ScoreManager               _score;
    private SeededPathAnimator         _seededAnimator;
    private BoardConfig                _cfg;
    private ObjectPool<BallView>       _ballPool;
    private BallView                   _activeBall;
    private int                        _dropIndex;
    private Coroutine                  _resolveCoroutine;

    private IEnumerator Start()
    {
        IConfigProvider provider = new LocalJsonConfigProvider();

        try
        {
            _cfg = provider.GetBoard(_boardId);
        }
        catch (Exception e)
        {
            Debug.LogError($"[GameBootstrapper] Failed to load board '{_boardId}': {e.Message}");
            enabled = false;
            yield break;
        }

        _game           = new GameController();
        _score          = new ScoreManager();
        _seededAnimator = new SeededPathAnimator();

        _ballPool = new ObjectPool<BallView>(
            createFunc:      () => Instantiate(_ballPrefab),
            actionOnGet:     b  => b.gameObject.SetActive(true),
            actionOnRelease: b  => b.gameObject.SetActive(false),
            actionOnDestroy: b  => Destroy(b.gameObject),
            defaultCapacity: 3
        );

        // Wire events before building board
        _ui.OnDropPressed     += HandleDropPressed;
        _game.OnStateChanged  += HandleStateChanged;
        _game.OnBallLanded    += HandleBallLandedEvent;
        _game.OnRoundEnded    += total => _ui.ShowRoundResult(total);
        _score.OnScoreChanged += (_, total) =>
            _ui.UpdateHUD(total, _game.ChipsLeft, _game.CanDrop());

        // Build board, then subscribe bucket events (subscribe ONCE, after Build)
        _boardView.Setup(_cfg);
        foreach (var bucket in _boardView.Buckets)
            bucket.OnBallLanded += HandlePhysicsBucketLanded;

        _dropIndex = 0;
        _game.ConfigLoaded(_cfg.chipsPerRound);
        _score.Reset();
        _ui.UpdateHUD(0, _cfg.chipsPerRound, true);
        FitCamera();

        yield return null;
    }

    private void HandleDropPressed()
    {
        if (!_game.CanDrop()) return;

        _activeBall = _ballPool.Get();
        var dropPos = new Vector3(0f, _boardView.DropY, 0f);
        _activeBall.transform.position = dropPos;
        _activeBall.SetupPhysics(_cfg);

        var seeded = GetSeededDrop(_dropIndex);
        _game.BeginDrop(_dropIndex);
        _dropIndex++;

        if (seeded != null)
        {
            int idx = seeded.targetBucketIndex;
            if (idx < 0 || idx >= _boardView.Buckets.Count)
            {
                Debug.LogError($"[GameBootstrapper] seededDrop targetBucketIndex {idx} " +
                               $"out of Buckets range ({_boardView.Buckets.Count}). Falling back to physics.");
                _activeBall.LaunchPhysics(_cfg.randomImpulseRange);
                return;
            }

            var targetPos = new Vector3(
                _boardView.Buckets[idx].transform.position.x,
                _boardView.Buckets[idx].transform.position.y,
                0f);

            var path = _seededAnimator.GeneratePath(
                dropPos, targetPos,
                _boardView.BoardWidth, _cfg.pegSpacing, _cfg.rows);

            _activeBall.OnSeededPathComplete += () =>
                HandlePhysicsBucketLanded(idx, _cfg.bucketValues[idx]);

            _activeBall.LaunchSeeded(path, _cfg.rows);
        }
        else
        {
            _activeBall.LaunchPhysics(_cfg.randomImpulseRange);
        }
    }

    // Fired by ScoreBucketView.OnTriggerEnter2D (physics) or seeded path complete
    private void HandlePhysicsBucketLanded(int bucketIndex, int value)
    {
        if (_game.State != GameState.Dropping) return; // guard against double-fire

        _boardView.Buckets[bucketIndex].PlayLandingEffect();
        _game.BallLanded(value);
    }

    private void HandleBallLandedEvent(int value)
    {
        _score.AddScore(value);

        if (_activeBall != null)
        {
            var ball = _activeBall;
            _activeBall = null;
            StartCoroutine(ReturnBallAfterDelay(ball, 0.4f));
        }

        if (_resolveCoroutine != null) StopCoroutine(_resolveCoroutine);
        _resolveCoroutine = StartCoroutine(ResolveAfterDelay(_cfg.resolveDelay));
    }

    private IEnumerator ReturnBallAfterDelay(BallView ball, float delay)
    {
        yield return new WaitForSeconds(delay);
        _ballPool.Release(ball);
    }

    private IEnumerator ResolveAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        _game.ResolveComplete(_score.RoundTotal);
    }

    private void HandleStateChanged(GameState state)
    {
        _ui.UpdateHUD(_score.RoundTotal, _game.ChipsLeft, _game.CanDrop());
    }

    private SeededDrop GetSeededDrop(int dropIndex)
    {
        if (_cfg.seededDrops == null) return null;
        foreach (var s in _cfg.seededDrops)
            if (s.chipIndex == dropIndex) return s;
        return null;
    }

    private void FitCamera()
    {
        float boardHeight = _boardView.DropY - _boardView.Buckets[0].transform.position.y;
        Camera.main.orthographicSize = (boardHeight + 4f) / 2f;
        Camera.main.transform.position = new Vector3(0f, _boardView.DropY - boardHeight / 2f, -10f);
    }

    private void OnDestroy()
    {
        _ballPool?.Dispose();
    }
}
