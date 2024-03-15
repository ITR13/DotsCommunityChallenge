using Unity.Entities;

public struct CalcMode : IComponentData
{
    public Algorithm Algorithm;
    public int RenderSize;
    public bool SimulateStill;
}

public enum Algorithm
{
    QuadTree,
    HashMap,
}