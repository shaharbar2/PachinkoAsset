using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D), typeof(SpriteRenderer))]
public class BallView : MonoBehaviour
{
    private Rigidbody2D _rb;

    public event Action OnSeededPathComplete;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        tag = "Ball";
    }

    private void OnEnable()
    {
        _rb.linearVelocity  = Vector2.zero;
        _rb.angularVelocity = 0f;
    }

    public void SetupPhysics(BoardConfig cfg)
    {
        _rb.mass                   = cfg.ballMass;
        _rb.gravityScale           = cfg.gravityScale;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        _rb.interpolation          = RigidbodyInterpolation2D.Interpolate;
        _rb.sleepMode              = RigidbodySleepMode2D.NeverSleep;
        _rb.linearDamping          = 0f;
        _rb.angularDamping         = 0f;
        _rb.isKinematic            = false;
        GetComponent<CircleCollider2D>().radius = cfg.pegRadius * 0.8f;
    }

    public void LaunchPhysics(float impulseRange)
    {
        float x = UnityEngine.Random.Range(-impulseRange, impulseRange);
        _rb.AddForce(new Vector2(x, 0f), ForceMode2D.Impulse);
    }

    // Seeded drop: kinematic per-arc animation with InQuad ease (gravity-authentic feel)
    public void LaunchSeeded(Vector3[] waypoints, int rows)
    {
        _rb.isKinematic    = true;
        _rb.linearVelocity = Vector2.zero;
        StartCoroutine(AnimateArcs(waypoints, rows));
    }

    private IEnumerator AnimateArcs(Vector3[] waypoints, int rows)
    {
        for (int i = 0; i < waypoints.Length - 1; i++)
        {
            float duration = Mathf.Max(SeededPathAnimator.MinHopSeconds,
                SeededPathAnimator.BaseDuration / (1f + i * SeededPathAnimator.SpeedFactor));

            Vector3 start   = waypoints[i];
            Vector3 end     = waypoints[i + 1];
            float   elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t     = Mathf.Clamp01(elapsed / duration);
                float eased = t * t; // InQuad — accelerates like gravity
                transform.position = Vector3.LerpUnclamped(start, end, eased);
                yield return null;
            }
            transform.position = end;
        }

        OnSeededPathComplete?.Invoke();
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        OnSeededPathComplete = null;
    }
}
