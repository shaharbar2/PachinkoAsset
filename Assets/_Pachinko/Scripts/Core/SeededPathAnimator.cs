using System.Collections.Generic;
using UnityEngine;

public class SeededPathAnimator
{
    public const float BaseDuration  = 0.35f;
    public const float SpeedFactor   = 0.08f;
    public const float MinHopSeconds = 0.10f;

    public float HopDuration(int rowIndex) =>
        Mathf.Max(MinHopSeconds, BaseDuration / (1f + rowIndex * SpeedFactor));

    // Generates waypoints from dropPos → targetPos with natural lateral drift.
    // boardWidth: used to clamp jitter so the ball stays on the board.
    public Vector3[] GeneratePath(Vector3 dropPos, Vector3 targetPos,
                                  float boardWidth, float pegSpacing, int rows)
    {
        var waypoints  = new List<Vector3> { dropPos };
        int steps      = rows + 1;
        float totalDx  = targetPos.x - dropPos.x;
        float totalDy  = dropPos.y - targetPos.y;
        float currentX = dropPos.x;
        float jitterDebt = 0f;
        float halfBoard  = boardWidth * 0.5f;

        for (int step = 1; step <= steps; step++)
        {
            float progress = (float)step / steps;
            float targetX  = dropPos.x + totalDx * progress;
            float y        = dropPos.y - totalDy * progress;

            float jitter     = Random.Range(-pegSpacing * 0.35f, pegSpacing * 0.35f);
            float cancelForce = -jitterDebt * 0.3f;
            float dx         = (targetX - currentX) + jitter + cancelForce;
            currentX = Mathf.Clamp(currentX + dx, -halfBoard, halfBoard);
            jitterDebt += jitter;

            waypoints.Add(new Vector3(currentX, y, 0f));
        }

        waypoints[waypoints.Count - 1] = targetPos;
        return waypoints.ToArray();
    }
}
