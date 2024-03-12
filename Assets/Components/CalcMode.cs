using Unity.Entities;

public struct CalcMode : IComponentData
{
    public Algorithm Algorithm;
    public bool Render;
}

public enum Algorithm
{
    QuadTree,
    HashMap,
}