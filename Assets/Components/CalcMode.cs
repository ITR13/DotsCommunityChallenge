using Unity.Entities;

public struct CalcMode : IComponentData
{
    public Algorithm Algorithm;
    public bool Render;
    public bool SimulateStill;
}

public enum Algorithm
{
    QuadTree,
    HashMap,
}