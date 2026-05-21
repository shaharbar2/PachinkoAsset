using System;

[Serializable]
public class BoardConfig
{
    public string boardId            = "default";
    public int    rows               = 8;
    public int    cols               = 9;
    public float  pegSpacing         = 1.2f;
    public float  pegRadius          = 0.15f;
    public int[]  bucketValues       = { 100, 500, 1000, 10000, 1000, 500, 100 };
    public int    chipsPerRound      = 5;
    public float  ballMass           = 1.0f;
    public float  gravityScale       = 2.0f;
    public float  pegRestitution     = 0.55f;
    public float  randomImpulseRange = 2.5f;
    public float  resolveDelay       = 0.6f;
    public SeededDrop[] seededDrops  = Array.Empty<SeededDrop>();

    // Returns null on success, error string on failure.
    public string Validate()
    {
        if (pegSpacing < 0.05f || pegSpacing > 10f)
            return $"pegSpacing {pegSpacing} out of range [0.05, 10]";
        if (pegRadius < 0.01f || pegRadius > 2f)
            return $"pegRadius {pegRadius} out of range [0.01, 2]";
        if (pegRadius >= pegSpacing * 0.5f)
            return $"pegRadius {pegRadius} must be < pegSpacing/2 ({pegSpacing * 0.5f})";
        if (rows < 1 || rows > 50)
            return $"rows {rows} out of range [1, 50]";
        if (cols < 1 || cols > 50)
            return $"cols {cols} out of range [1, 50]";
        if (ballMass < 0.01f || ballMass > 10f)
            return $"ballMass {ballMass} out of range [0.01, 10]";
        if (gravityScale < 0.1f || gravityScale > 5f)
            return $"gravityScale {gravityScale} out of range [0.1, 5]";
        if (randomImpulseRange < 0 || randomImpulseRange > 20f)
            return $"randomImpulseRange {randomImpulseRange} out of range [0, 20]";
        if (bucketValues == null || bucketValues.Length == 0)
            return "bucketValues is null or empty";
        if (seededDrops != null)
        {
            for (int i = 0; i < seededDrops.Length; i++)
            {
                if (seededDrops[i].targetBucketIndex < 0 ||
                    seededDrops[i].targetBucketIndex >= bucketValues.Length)
                    return $"seededDrops[{i}].targetBucketIndex {seededDrops[i].targetBucketIndex} " +
                           $"out of bucketValues bounds ({bucketValues.Length})";
            }
        }
        return null;
    }
}
