using System;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D), typeof(SpriteRenderer))]
public class ScoreBucketView : MonoBehaviour
{
    [SerializeField] private TextMeshPro _valueLabel;

    public int BucketIndex { get; private set; }
    public int Value       { get; private set; }

    public event Action<int, int> OnBallLanded; // (bucketIndex, value)

    public void Setup(int index, int value, float width)
    {
        BucketIndex = index;
        Value       = value;

        var col       = GetComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size      = new Vector2(width * 0.88f, 1f);
        col.offset    = new Vector2(0f, 0.5f);

        if (_valueLabel != null)
            _valueLabel.text = value.ToString("N0");
    }

    public void PlayLandingEffect()
    {
        // Extension point: add DOTween scale pulse + glow when FX are added
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Ball"))
            OnBallLanded?.Invoke(BucketIndex, Value);
    }
}
