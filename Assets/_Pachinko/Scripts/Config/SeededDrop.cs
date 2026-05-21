using System;

[Serializable]
public class SeededDrop
{
    public int chipIndex;         // which drop number (0-based) is scripted
    public int targetBucketIndex; // which bucket (0 = leftmost) to land in
}
