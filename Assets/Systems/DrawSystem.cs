using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public partial struct DrawSystem : ISystem
{
    private int2 _prevPos;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<CalcMode>();
        state.RequireForUpdate<Visualizer>();
    }

    public void OnUpdate(ref SystemState state)
    {
        if (!Input.GetMouseButton(0))
        {
            return;
        }

        var inputPos = Input.mousePosition;
        var screenSize = Screen.height;

        var bottomLeft = new float2(
            Screen.width - screenSize,
            Screen.height - screenSize
        ) / 2;

        var bottomLeftPos = (new float2(inputPos.x, inputPos.y) - bottomLeft) / screenSize;
        bottomLeftPos.y = 1 - bottomLeftPos.y;

        var calcMode = SystemAPI.GetSingletonRW<CalcMode>();
        
        if (calcMode.ValueRO.RenderSize == 0) return;

        var visualizer = SystemAPI.QueryBuilder().WithAll<Visualizer>().Build().GetSingleton<Visualizer>();

        var tilePos = bottomLeftPos * calcMode.ValueRO.RenderSize - visualizer.Position - calcMode.ValueRO.RenderSize / 2f - Constants.GroupTotalEdgeLength;

        var intPos = (int2)math.floor(tilePos);
        if (!Input.GetMouseButtonDown(0) && math.all(_prevPos == intPos))
        {
            return;
        }

        calcMode.ValueRW.SimulateStill = true;

        _prevPos = intPos;


        var groupPos = Constants.GroupTotalEdgeLength * (int2)math.floor(tilePos / Constants.GroupTotalEdgeLength);

        var targetEntity = CollectionHelper.CreateNativeArray<Entity>(1, state.WorldUpdateAllocator);
        var groupData = CollectionHelper.CreateNativeArray<CurrentCglGroup>(1, state.WorldUpdateAllocator);
        new DrawSystemJob
        {
            Position = groupPos,
            Entity = targetEntity,
            GroupData = groupData,
        }.Run();

        var relativePos = (int2)math.floor(tilePos - groupPos);
        // Divide by 8 since it needs the amount of bytes in CurrentCglGroup
        var reinterpretedGroup = groupData.Reinterpret<ulong>(Constants.GroupTotalArea / 8);

        var subGroupPos = relativePos / Constants.BitFieldSize;
        var bitIndex = relativePos % Constants.BitFieldSize;

        var value = reinterpretedGroup[subGroupPos.y * Constants.GroupSize + subGroupPos.x];
        var bitShifted = (ulong)1 << (bitIndex.y * Constants.BitFieldSize + bitIndex.x);
        value ^= bitShifted;
        reinterpretedGroup[subGroupPos.y * Constants.GroupSize + subGroupPos.x] = value;

        if (targetEntity[0] == Entity.Null)
        {
            targetEntity[0] = state.EntityManager.CreateEntity(typeof(GroupPosition), typeof(CurrentCglGroup), typeof(NextCglGroup));
            state.EntityManager.SetComponentData(
                targetEntity[0],
                new GroupPosition
                {
                    Position = groupPos,
                }
            );
        }

        state.EntityManager.SetComponentData(targetEntity[0], groupData[0]);
    }

    [BurstCompile]
    public partial struct DrawSystemJob : IJobEntity
    {
        [ReadOnly] public int2 Position;
        [NativeDisableParallelForRestriction] public NativeArray<Entity> Entity;
        [NativeDisableParallelForRestriction] public NativeArray<CurrentCglGroup> GroupData;

        public void Execute(Entity entity, in GroupPosition groupPosition, in CurrentCglGroup groupData)
        {
            if (!math.all(groupPosition.Position == Position)) return;
            Entity[0] = entity;
            GroupData[0] = groupData;
        }
    }
}