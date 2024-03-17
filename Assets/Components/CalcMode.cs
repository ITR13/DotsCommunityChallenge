using Unity.Entities;

public struct CalcMode : IComponentData
{
    public Algorithm Algorithm;
    public int RenderSize;
    public bool SimulateStill;
    public bool ShowUi;
    public bool Paused;
    public bool Loading;

    public readonly bool Equals(CalcMode other)
    {
        return other.Algorithm == Algorithm && RenderSize == other.RenderSize && Paused == other.Paused && ShowUi == other.ShowUi && SimulateStill == other.SimulateStill;
    }
}

public enum Algorithm
{
    QuadTree,
    EntityHashMap,
    EdgeHashMap,
    // Sixes, Nope, not typing that all out

    // ReSharper disable once InconsistentNaming
    MAX_VALUE,
}