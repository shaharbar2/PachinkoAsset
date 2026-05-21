using UnityEngine;

[RequireComponent(typeof(CircleCollider2D), typeof(SpriteRenderer))]
public class PegView : MonoBehaviour
{
    private static PhysicsMaterial2D _sharedMaterial;

    public void Setup(float radius, float restitution)
    {
        if (_sharedMaterial == null)
            _sharedMaterial = new PhysicsMaterial2D("PegShared")
                { bounciness = restitution, friction = 0f };

        var col        = GetComponent<CircleCollider2D>();
        col.radius         = radius;
        col.sharedMaterial = _sharedMaterial;

        transform.localScale = Vector3.one * radius * 2f;
    }
}
