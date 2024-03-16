using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

public partial struct PlacePixelSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<CalcMode>();
        state.RequireForUpdate<PlacePixel>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var placePixelQuery = SystemAPI.QueryBuilder().WithAll<PlacePixel>().Build();
        var pixelsToPlace = placePixelQuery.ToComponentDataArray<PlacePixel>(Allocator.Temp);
        state.EntityManager.DestroyEntity(placePixelQuery);

        var calcMode = SystemAPI.GetSingletonRW<CalcMode>();
        calcMode.ValueRW.SimulateStill = true;

        foreach (var placePixel in pixelsToPlace)
        {
            var tilePos = placePixel.Position;
            
            var groupPos = Constants.GroupTotalEdgeLength * (int2)math.floor(tilePos / Constants.GroupTotalEdgeLength);

            var targetEntity = CollectionHelper.CreateNativeArray<Entity>(1, state.WorldUpdateAllocator);
            var groupData = CollectionHelper.CreateNativeArray<CurrentCglGroup>(1, state.WorldUpdateAllocator);
            new DrawSystem.DrawSystemJob
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
    }
}