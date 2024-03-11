using Unity.Entities;

public struct CurrentCglGroup : IComponentData
{
    public CglGroupData Data;
}


public struct NextCglGroup : IComponentData
{
    public CglGroupData Data;
}