using System;
using System.Collections.Generic;
using UnityEngine;

public class BoardView : MonoBehaviour
{
    [SerializeField] private Transform _dropPoint;

    private PegView[]          _pegs;
    private ScoreBucketView[]  _buckets;

    public IReadOnlyList<ScoreBucketView> Buckets => _buckets;

    // World-space Y where balls are dropped from
    public float DropY => _dropPoint != null ? _dropPoint.position.y : ComputeDropY();

    // Horizontal span from leftmost to rightmost bucket center
    public float BoardWidth { get; private set; }

    public void Setup(BoardConfig cfg)
    {
        _pegs    = GetComponentsInChildren<PegView>(true);
        _buckets = GetComponentsInChildren<ScoreBucketView>(true);

        // Sort buckets left-to-right so index matches position order
        Array.Sort(_buckets, (a, b) =>
            a.transform.position.x.CompareTo(b.transform.position.x));

        foreach (var peg in _pegs)
            peg.Setup(cfg.pegRadius, cfg.pegRestitution);

        if (_buckets.Length != cfg.bucketValues.Length)
        {
            Debug.LogError($"[BoardView] Prefab has {_buckets.Length} buckets but config has " +
                           $"{cfg.bucketValues.Length} bucketValues. Check BoardDefault prefab.");
        }

        float bucketWidth = _buckets.Length > 1
            ? Mathf.Abs(_buckets[1].transform.position.x - _buckets[0].transform.position.x)
            : cfg.pegSpacing;

        BoardWidth = _buckets.Length > 1
            ? _buckets[_buckets.Length - 1].transform.position.x -
              _buckets[0].transform.position.x
            : cfg.pegSpacing * (cfg.cols - 1);

        int count = Mathf.Min(_buckets.Length, cfg.bucketValues.Length);
        for (int i = 0; i < count; i++)
            _buckets[i].Setup(i, cfg.bucketValues[i], bucketWidth);
    }

    private float ComputeDropY()
    {
        if (_pegs == null || _pegs.Length == 0) return 5f;
        float maxY = float.MinValue;
        foreach (var p in _pegs)
            if (p.transform.position.y > maxY) maxY = p.transform.position.y;
        return maxY + 1f;
    }
}
