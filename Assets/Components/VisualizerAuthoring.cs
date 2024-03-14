using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class Visualizer : IComponentData
{
    public Material Material;
    public float2 Position;
}

public class VisualizerAuthoring : MonoBehaviour
{
    public Material Material;

    public class VisualizerBaker : Baker<VisualizerAuthoring>
    {
        public override void Bake(VisualizerAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponentObject(entity, new Visualizer {Material = authoring.Material, Position = new float2(Constants.GroupTotalEdgeLength, Constants.GroupTotalEdgeLength)});
        }
    }
}