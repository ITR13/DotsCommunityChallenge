using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[UpdateAfter(typeof(RunCgl))]
[UpdateAfter(typeof(RunCgl2))]
public partial class UpdateRenderData : SystemBase
{
    public NativeArray<CglGroupData> VisualizedGroups;
    public ComputeBuffer ComputeBuffer;

    public int PositionProperty;
    public int BufferProperty;
    public int LengthProperty;

    protected override void OnCreate()
    {
        RequireForUpdate<Visualizer>();
        RequireForUpdate<CalcMode>();

        PositionProperty = Shader.PropertyToID("_offset");
        BufferProperty = Shader.PropertyToID("_buffer");
        LengthProperty = Shader.PropertyToID("_length");

        VisualizedGroups = new NativeArray<CglGroupData>(4, Allocator.Persistent);
        ComputeBuffer = new ComputeBuffer(Constants.GroupTotalArea / 8, 4);
    }

    protected override void OnDestroy()
    {
        VisualizedGroups.Dispose();
    }

    protected override void OnUpdate()
    {
        var calcMode = SystemAPI.GetSingleton<CalcMode>();
        if (!calcMode.Render) return;

        var visualizer = SystemAPI.QueryBuilder().WithAllRW<Visualizer>().Build().GetSingletonRW<Visualizer>();

        VisualizedGroups[0] = default;
        VisualizedGroups[1] = default;
        VisualizedGroups[2] = default;
        VisualizedGroups[3] = default;

        Dependency = new FindRenderData
        {
            Groups = VisualizedGroups,
            Position = visualizer.Position,
        }.ScheduleParallel(Dependency);

        Dependency.Complete();


        NativeArray<uint> reinterpreted = VisualizedGroups.Reinterpret<uint>(Constants.GroupTotalArea / 8);
        ComputeBuffer.SetData(reinterpreted);

        var pos = math.frac(visualizer.Position / Constants.GroupTotalEdgeLength);

        visualizer.Material.SetVector(PositionProperty, new Vector4(pos.x, pos.y, 0, 0));
        visualizer.Material.SetInt(LengthProperty, reinterpreted.Length);

        visualizer.Material.SetBuffer(BufferProperty, ComputeBuffer);
    }

    private partial struct FindRenderData : IJobEntity
    {
        [NativeDisableParallelForRestriction] public NativeArray<CglGroupData> Groups;
        [ReadOnly] public float2 Position;

        private void Execute(in CurrentCglGroup currentCglGroup, in GroupPosition position)
        {
            var groupSimplePosition = position.Position / Constants.GroupTotalEdgeLength;
            var viewSimplePosition = (int2)math.floor(Position / Constants.GroupTotalEdgeLength - 0.5f);

            if (groupSimplePosition.x == viewSimplePosition.x)
            {
                if (groupSimplePosition.y == viewSimplePosition.y)
                {
                    Groups[0] = currentCglGroup.Data;
                }
                else if (groupSimplePosition.y == viewSimplePosition.y + 1)
                {
                    Groups[2] = currentCglGroup.Data;
                }
            }
            else if (groupSimplePosition.x == viewSimplePosition.x + 1)
            {
                if (groupSimplePosition.y == viewSimplePosition.y)
                {
                    Groups[1] = currentCglGroup.Data;
                }
                else if (groupSimplePosition.y == viewSimplePosition.y + 1)
                {
                    Groups[3] = currentCglGroup.Data;
                }
            }
        }
    }
}