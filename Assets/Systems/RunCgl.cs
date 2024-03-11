using NativeQuadTree;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;

public partial struct RunCgl : ISystem
{
    private ComponentTypeHandle<GroupPosition> _positionTypeHandle;
    private ComponentTypeHandle<CurrentCglGroup> _currentTypeHandle;
    private ComponentTypeHandle<NextCglGroup> _nextTypeHandle;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<CurrentCglGroup>();

        _positionTypeHandle = state.GetComponentTypeHandle<GroupPosition>(true);
        _currentTypeHandle = state.GetComponentTypeHandle<CurrentCglGroup>(true);
        _nextTypeHandle = state.GetComponentTypeHandle<NextCglGroup>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var groupsQuery = SystemAPI.QueryBuilder().WithAll<GroupPosition, CurrentCglGroup>().WithAllRW<NextCglGroup>().Build();

        var chunks = groupsQuery.CalculateChunkCountWithoutFiltering();
        var bounds = CollectionHelper.CreateNativeArray<AABB2D>(chunks, state.WorldUpdateAllocator);

        var groups = groupsQuery.ToEntityArray(state.WorldUpdateAllocator);
        var positions = groupsQuery.ToComponentDataArray<GroupPosition>(state.WorldUpdateAllocator);
        var groupQuadTree = new NativeQuadTree<Entity>(allocator: state.WorldUpdateAllocator, initialElementsCapacity: groups.Length);

        _positionTypeHandle.Update(ref state);
        _currentTypeHandle.Update(ref state);
        _nextTypeHandle.Update(ref state);

        state.Dependency = new CalculateBoundsJob
        {
            PositionTypeHandle = _positionTypeHandle,
            Bounds = bounds,
        }.ScheduleParallel(groupsQuery, state.Dependency);

        state.Dependency = new CreateQuadTreeJob()
        {
            Bounds = bounds,
            GroupQuadTree = groupQuadTree,
            Groups = groups,
            Positions = positions,
        }.Schedule(state.Dependency);

        state.Dependency = new RunCglJob
        {
            PositionTypeHandle = _positionTypeHandle,
            CurrentTypeHandle = _currentTypeHandle,
            NextTypeHandle = _nextTypeHandle,
            CurrentLookup = SystemAPI.GetComponentLookup<CurrentCglGroup>(true),
            GroupQuadTree = groupQuadTree,
        }.ScheduleParallel(groupsQuery, state.Dependency);

        state.Dependency = new SwapBuffersJob().Schedule(state.Dependency);
    }
    
    private class TextureHolder : IComponentData
    {
        public RenderTexture Texture;
    }
    

    [BurstCompile]
    private struct CalculateBoundsJob : IJobChunk
    {
        [ReadOnly] public ComponentTypeHandle<GroupPosition> PositionTypeHandle;

        [NativeDisableParallelForRestriction] public NativeArray<AABB2D> Bounds;

        [BurstCompile]
        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            var currentPositions = chunk.GetNativeArray(ref PositionTypeHandle);
            Assert.IsFalse(useEnabledMask);

            var min = currentPositions[0].Position;
            var max = min;

            for (var i = 1; i < chunk.Count; i++)
            {
                var current = currentPositions[i].Position;
                min = math.min(min, current);
                max = math.max(max, current);
            }
            max += Constants.PositionMultiplier;

            var size = math.cmax(max - min) / 2;

            Bounds[unfilteredChunkIndex] = new AABB2D(
                (min + max) / 2,
                new float2(size + 0.5f, size + 0.5f)
            );
        }
    }

    [BurstCompile]
    private struct CreateQuadTreeJob : IJob
    {
        [ReadOnly] public NativeArray<AABB2D> Bounds;

        public NativeQuadTree<Entity> GroupQuadTree;

        [ReadOnly] public NativeArray<Entity> Groups;

        [ReadOnly] public NativeArray<GroupPosition> Positions;

        public void Execute()
        {
            var min = Bounds[0].Min;
            var max = Bounds[0].Max;
            for (var i = 1; i < Bounds.Length; i++)
            {
                min = math.min(min, Bounds[0].Min);
                max = math.max(max, Bounds[0].Max);
            }

            var size = math.cmax(max - min) / 2;
            var bounds = new AABB2D((max + min) / 2, new float2(size, size));

            var quadElements = new NativeArray<QuadElement<Entity>>(Groups.Length, Allocator.Temp);
            for (var i = 0; i < quadElements.Length; i++)
            {
                quadElements[i] = new QuadElement<Entity>
                {
                    element = Groups[i],
                    pos = Positions[i].Position,
                };
            }

            GroupQuadTree.ClearAndBulkInsert(quadElements, bounds);
        }
    }

    [BurstCompile]
    private struct RunCglJob : IJobChunk
    {
        [ReadOnly] public ComponentTypeHandle<GroupPosition> PositionTypeHandle;
        [ReadOnly] public ComponentTypeHandle<CurrentCglGroup> CurrentTypeHandle;

        public ComponentTypeHandle<NextCglGroup> NextTypeHandle;

        [ReadOnly] public ComponentLookup<CurrentCglGroup> CurrentLookup;

        [ReadOnly] public NativeQuadTree<Entity> GroupQuadTree;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            NativeArray<GroupPosition> positions = chunk.GetNativeArray(ref PositionTypeHandle);
            NativeArray<CurrentCglGroup> currentGroups = chunk.GetNativeArray(ref CurrentTypeHandle);
            NativeArray<NextCglGroup> nextGroups = chunk.GetNativeArray(ref NextTypeHandle);

            Assert.IsFalse(useEnabledMask);

            var foundQuadElements = new NativeList<QuadElement<Entity>>(9, Allocator.Temp);
            var subQuadTree = new NativeQuadTree<byte>(Allocator.Temp);
            var subQuadElements = new NativeList<QuadElement<byte>>(Constants.BitFieldSize * (Constants.BitFieldSize + 4) + 4, Allocator.Temp);

            for (var i = 0; i < chunk.Count; i++)
            {
                var position = positions[i].Position;
                var extents = new float2(1.5f * Constants.PositionMultiplier, 1.5f * Constants.PositionMultiplier);
                var innerExtents = new float2(Constants.PositionMultiplier * 0.5f + 1.5f, Constants.PositionMultiplier * 0.5f + 1.5f);

                var bounds = new AABB2D(position, extents);
                var innerBounds = new AABB2D(position + Constants.PositionMultiplier * 0.5f, innerExtents);

                foundQuadElements.Clear();
                GroupQuadTree.RangeQuery(bounds, foundQuadElements);

                subQuadElements.Clear();

                for (var j = 0; j < foundQuadElements.Length; j++)
                {
                    FillSubElementList(foundQuadElements, j, innerBounds, ref subQuadElements);
                }

                if (subQuadElements.Length == 0) continue;

                subQuadTree.ClearAndBulkInsert(subQuadElements.ToArray(Allocator.Temp), innerBounds);

                unsafe
                {
                    var current = (ulong*)&(((CglGroupData*)currentGroups.GetUnsafeReadOnlyPtr())[i]);
                    var next = (ulong*)&(((CglGroupData*)nextGroups.GetUnsafePtr())[i]);
                    NextGen(position, current, next, subQuadTree, ref subQuadElements);
                }
            }
        }

        public unsafe void NextGen(in float2 position, in ulong* currentGroup, in ulong* nextGroup, in NativeQuadTree<byte> subQuadTree, ref NativeList<QuadElement<byte>> subQuadElements)
        {
            const int groupSize = Constants.GroupSize;
            for (var y = 0; y < groupSize; y++)
            {
                for (var x = 0; x < groupSize; x++)
                {
                    var x0 = x * Constants.BitFieldSize;
                    var x1 = (x + 1) * Constants.BitFieldSize;
                    var y0 = y * Constants.BitFieldSize;
                    var y1 = (y + 1) * Constants.BitFieldSize;

                    var min = position + new float2(x0 - 1.5f, y0 - 1.5f);
                    var max = position + new float2(x1 + 0.5f, y1 + 0.5f);
                    var bounds = new AABB2D((max + min) / 2, (max - min) / 2);

                    subQuadElements.Clear();
                    subQuadTree.RangeQuery(bounds, subQuadElements);

                    if (subQuadElements.Length <= 0)
                    {
                        continue;
                    }

                    var index = y * groupSize + x;

                    var current = currentGroup[index];
                    ulong next = 0;

                    for (var by = 0; by < Constants.BitFieldSize; by++)
                    {
                        for (var bx = 0; bx < Constants.BitFieldSize; bx++)
                        {
                            next <<= 1;
                            var isAlive = (current & 1) != 0;
                            current >>= 1;

                            var bPosition = position + new float2(x * Constants.BitFieldSize + bx, y * Constants.BitFieldSize + by);
                            var bExtents = 1.5f;

                            subQuadElements.Clear();
                            subQuadTree.RangeQuery(new AABB2D(bPosition, new float2(bExtents, bExtents)), subQuadElements);

                            var nextAlive = subQuadElements.Length == 3 || (isAlive && subQuadElements.Length is 4);
                            if (nextAlive) next |= 1;
                        }
                    }

                    // Reversed because we generated 0,0 first
                    nextGroup[index] = math.reversebits(next);
                }
            }
        }

        private void FillSubElementList(in NativeList<QuadElement<Entity>> foundQuadElements, int foundIndex, in AABB2D extents, ref NativeList<QuadElement<byte>> subQuadElements)
        {
            var foundPosition = foundQuadElements[foundIndex].pos;
            var foundData = CurrentLookup[foundQuadElements[foundIndex].element].Data;

            for (var y = 0; y < Constants.GroupSize; y++)
            {
                for (var x = 0; x < Constants.GroupSize; x++)
                {
                    ulong currentBitmask;
                    unsafe
                    {
                        currentBitmask = ((ulong*)(&foundData))[y * Constants.GroupSize + x];
                    }

                    if (currentBitmask == 0)
                    {
                        continue;
                    }

                    for (var by = 0; by < Constants.BitFieldSize; by++)
                    {
                        for (var bx = 0; bx < Constants.BitFieldSize; bx++)
                        {
                            if ((currentBitmask & 1) != 0)
                            {
                                var pos = foundPosition + new float2(x * Constants.BitFieldSize + bx, y * Constants.BitFieldSize + by);
                                if (extents.Contains(pos))
                                {
                                    subQuadElements.Add(
                                        new QuadElement<byte>
                                        {
                                            pos = pos
                                        }
                                    );
                                }
                            }

                            currentBitmask >>= 1;
                        }
                    }
                }
            }
        }
    }

    [BurstCompile]
    public partial struct SwapBuffersJob : IJobEntity
    {
        public void Execute(ref CurrentCglGroup current, ref NextCglGroup next)
        {
            current.Data = next.Data;
            next.Data = new CglGroupData();
        }
    }
}