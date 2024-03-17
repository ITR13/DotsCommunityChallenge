using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;

public partial struct EdgeHashMapSystem : ISystem
{
    private const int RowPadding = 1, ColumnPadding = 1;
    private const int ArrayWidth = ColumnPadding * 2 + Constants.GroupTotalEdgeLength;
    private EntityArchetype _groupArchetype;

    private struct Edges
    {
        public uint Top, Bot, Left, Right;
    }

    public void OnCreate(ref SystemState state)
    {
        Application.targetFrameRate = -1;
        state.RequireForUpdate<NextCglGroup>();
        state.RequireForUpdate<CalcMode>();

        _groupArchetype = state.EntityManager.CreateArchetype(typeof(GroupPosition), typeof(CurrentCglGroup), typeof(NextCglGroup));
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var calcMode = SystemAPI.GetSingleton<CalcMode>();
        if (calcMode.Algorithm != Algorithm.EdgeHashMap || calcMode.Paused) return;

        var groupQuery = SystemAPI.QueryBuilder().WithAll<GroupPosition, CurrentCglGroup>().WithAllRW<NextCglGroup>().Build();

        var groupCount = groupQuery.CalculateEntityCount();
        var groups = new NativeParallelHashMap<int2, Edges>(groupCount, state.WorldUpdateAllocator);
        var activeGroups = new NativeParallelHashMap<int2, bool>(groupCount, state.WorldUpdateAllocator);

        var positionTypeHandle = SystemAPI.GetComponentTypeHandle<GroupPosition>(true);
        var currentTypeHandle = SystemAPI.GetComponentTypeHandle<CurrentCglGroup>(true);
        var nextTypeHandle = SystemAPI.GetComponentTypeHandle<NextCglGroup>();

        var hashmapJob = new PopulateGroupHashmapJob
        {
            PositionTypeHandle = positionTypeHandle,
            CurrentTypeHandle = currentTypeHandle,
            Groups = groups.AsParallelWriter(),
        };

#if DEBUGGER_FIX
        hashmapJob.Run(groupQuery);
#else
        state.Dependency = hashmapJob.ScheduleParallel(groupQuery, state.Dependency);
#endif

        var updateGroup = new UpdateGroup
        {
            PositionTypeHandle = positionTypeHandle,
            CurrentTypeHandle = currentTypeHandle,
            NextTypeHandle = nextTypeHandle,
            Groups = groups.AsReadOnly(),

            TopLeftOffset = new int2(-Constants.GroupTotalEdgeLength, -Constants.GroupTotalEdgeLength),
            TopCentOffset = new int2(0, -Constants.GroupTotalEdgeLength),
            TopRighOffset = new int2(+Constants.GroupTotalEdgeLength, -Constants.GroupTotalEdgeLength),
            MidLeftOffset = new int2(-Constants.GroupTotalEdgeLength, 0),
            // MidCentOffset = new int2(0, 0),
            MidRighOffset = new int2(+Constants.GroupTotalEdgeLength, 0),
            BotLeftOffset = new int2(-Constants.GroupTotalEdgeLength, +Constants.GroupTotalEdgeLength),
            BotCentOffset = new int2(0, +Constants.GroupTotalEdgeLength),
            BotRighOffset = new int2(+Constants.GroupTotalEdgeLength, +Constants.GroupTotalEdgeLength),

            ActiveGroups = activeGroups.AsParallelWriter(),
        };
#if DEBUGGER_FIX
        updateGroup.Run(groupQuery);
#else
        state.Dependency = updateGroup.ScheduleParallel(groupQuery, state.Dependency);
#endif

        if (calcMode.SimulateStill) return;

        var toCreate = new NativeParallelHashSet<int2>(groupCount * 8, state.WorldUpdateAllocator);
        var toDestroy = new NativeParallelHashSet<Entity>(groupCount, state.WorldUpdateAllocator);
        state.Dependency = JobHandle.CombineDependencies(
            new QuadTreeSystem.CollectStructuralChanges
            {
                ActiveGroups = activeGroups.AsReadOnly(),
                ToCreate = toCreate.AsParallelWriter(),
                ToDestroy = toDestroy.AsParallelWriter(),
            }.ScheduleParallel(state.Dependency),
            new QuadTreeSystem.SwapBuffersJob().ScheduleParallel(state.Dependency),
            new QuadTreeSystem.UpdateStatsJob
            {
                ActiveGroups = activeGroups.AsReadOnly(),
            }.Schedule(state.Dependency)
        );

        state.Dependency.Complete();
        state.EntityManager.DestroyEntity(toDestroy.ToNativeArray(Allocator.Temp));

        if (toCreate.IsEmpty) return;

        var toCreateArray = toCreate.ToNativeArray(Allocator.Temp);
        var entities = state.EntityManager.CreateEntity(_groupArchetype, toCreateArray.Length, Allocator.Temp);
        for (var i = 0; i < toCreateArray.Length; i++)
        {
            state.EntityManager.SetComponentData(entities[i], new GroupPosition {Position = toCreateArray[i]});
        }
    }

    [BurstCompile]
    private struct PopulateGroupHashmapJob : IJobChunk
    {
        public NativeParallelHashMap<int2, Edges>.ParallelWriter Groups;
        [ReadOnly] public ComponentTypeHandle<CurrentCglGroup> CurrentTypeHandle;
        [ReadOnly] public ComponentTypeHandle<GroupPosition> PositionTypeHandle;

        public unsafe void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            Assert.IsFalse(useEnabledMask);
            var positions = chunk.GetNativeArray(ref PositionTypeHandle);
            var currentGroups = chunk.GetNativeArray(ref CurrentTypeHandle);

            var currentStartPtr = (CurrentCglGroup*)currentGroups.GetUnsafeReadOnlyPtr();

            for (var i = 0; i < chunk.Count; i++)
            {
                var currentPtr = (ulong*)&(currentStartPtr[i]);

                uint left = 0;
                uint right = 0;

                for (byte subgroup = 0; subgroup < Constants.GroupSize; subgroup++)
                {
                    ulong rightValue = currentPtr[(subgroup + 1) * Constants.GroupSize - 1];
                    ulong leftValue = currentPtr[subgroup * Constants.GroupSize];

                    if (rightValue == 0 && leftValue == 0) continue;

                    for (byte by = 0; by < 8; by++)
                    {
                        var rightIsAlive = (uint)(rightValue >> (by * 8 + 7)) & 1;
                        var leftIsAlive = (uint)(leftValue >> (by * 8)) & 1;
                        right |= rightIsAlive << (subgroup * 8 + by);
                        left |= leftIsAlive << (subgroup * 8 + by);
                    }
                }

#if DEBUGGER_FIX
                var group = *(CglGroupData*)currentPtr;
#endif

                var bot = (uint)(
                    (currentPtr[12] >> 0x38) & 0b00000000000000000000000011111111 |
                    (currentPtr[13] >> 0x30) & 0b00000000000000001111111100000000 |
                    (currentPtr[14] >> 0x28) & 0b00000000111111110000000000000000 |
                    (currentPtr[15] >> 0x20) & 0b11111111000000000000000000000000
                );
                var top = (uint)(
                    (currentPtr[0] << 0x00) & 0b00000000000000000000000011111111 |
                    (currentPtr[1] << 0x08) & 0b00000000000000001111111100000000 |
                    (currentPtr[2] << 0x10) & 0b00000000111111110000000000000000 |
                    (currentPtr[3] << 0x18) & 0b11111111000000000000000000000000
                );

                if (left == 0 && right == 0 && top == 0 && bot == 0) continue;

                Groups.TryAdd(
                    positions[i].Position,
                    new Edges
                    {
                        Bot = bot,
                        Top = top,
                        Left = left,
                        Right = right,
                    }
                );
            }
        }
    }

    [BurstCompile]
    private struct UpdateGroup : IJobChunk
    {
        [ReadOnly] public int2 TopLeftOffset;
        [ReadOnly] public int2 TopCentOffset;
        [ReadOnly] public int2 TopRighOffset;

        [ReadOnly] public int2 MidLeftOffset;

        // [ReadOnly] public int2 MidCentOffset;
        [ReadOnly] public int2 MidRighOffset;
        [ReadOnly] public int2 BotLeftOffset;
        [ReadOnly] public int2 BotCentOffset;
        [ReadOnly] public int2 BotRighOffset;

        [ReadOnly] public ComponentTypeHandle<CurrentCglGroup> CurrentTypeHandle;
        [ReadOnly] public ComponentTypeHandle<GroupPosition> PositionTypeHandle;
        public ComponentTypeHandle<NextCglGroup> NextTypeHandle;

        [ReadOnly] public NativeParallelHashMap<int2, Edges>.ReadOnly Groups;

        public NativeParallelHashMap<int2, bool>.ParallelWriter ActiveGroups;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            Assert.IsFalse(useEnabledMask);

            var positions = chunk.GetNativeArray(ref PositionTypeHandle);
            var currentGroups = chunk.GetNativeArray(ref CurrentTypeHandle);
            var nextGroups = chunk.GetNativeArray(ref NextTypeHandle);

            // NB! One row uses 8 bits from each of the 4 sequential SubGroups
            var neighbors = new NativeArray<byte>(ArrayWidth * (Constants.GroupTotalEdgeLength + ColumnPadding * 2), Allocator.Temp);

            // bit pattern
            var precalculatedSum = new NativeArray<byte>(8, Allocator.Temp);
            for (var i = 0; i < 8; i++)
            {
                precalculatedSum[i] = (byte)math.countbits(i);
            }

            // bit pattern
            var precalculatedAlive = new NativeArray<ulong>(16, Allocator.Temp);
            for (var i = 0; i < 16; i++)
            {
                precalculatedAlive[i] = (i is 0b1010 or 0b1011 or 0b0011) ? 1 : (ulong)0;
            }

            for (var i = 0; i < chunk.Count; i++)
            {
                var position = positions[i].Position;
                var hasAny = false;

                #region Courners

                {
                    if (Groups.TryGetValue(position + TopLeftOffset, out var edges))
                    {
                        var isAlive = (byte)(edges.Bot >> 31);
                        neighbors[PosToBitIndex(0, 0, 0, 0)] = isAlive;
                        hasAny |= isAlive != 0;
                    }
                }

                {
                    if (Groups.TryGetValue(position + TopRighOffset, out var edges))
                    {
                        var isAlive = (byte)(edges.Bot & 1);
                        neighbors[PosToBitIndex(Constants.GroupSize - 1, 0, Constants.BitFieldSize - 1, 0)] = isAlive;
                        hasAny |= isAlive != 0;
                    }
                }

                {
                    if (Groups.TryGetValue(position + BotLeftOffset, out var edges))
                    {
                        var isAlive = (byte)(edges.Top >> 31);
                        neighbors[PosToBitIndex(0, Constants.GroupSize - 1, 0, Constants.BitFieldSize - 1)] = isAlive;
                        hasAny |= isAlive != 0;
                    }
                }

                {
                    if (Groups.TryGetValue(position + BotRighOffset, out var edges))
                    {
                        var isAlive = (byte)(edges.Top & 1);
                        neighbors[PosToBitIndex(Constants.GroupSize - 1, Constants.GroupSize - 1, Constants.BitFieldSize - 1, Constants.BitFieldSize - 1)] = isAlive;
                        hasAny |= isAlive != 0;
                    }
                }

                #endregion


                #region UpperLowerEdges

                {
                    if (Groups.TryGetValue(position + TopCentOffset, out var edges) && edges.Bot != 0)
                    {
                        hasAny = true;
                        unsafe
                        {
                            // We start this two before 0,0 for ease of use
                            var ptr = (byte*)neighbors.GetUnsafePtr() + PosToBitIndex(0, 0, 0, 0) - 2;
                            PopulateCenterEdgeNeigbors(edges.Bot, ptr);
                        }
                    }
                }

                {
                    if (Groups.TryGetValue(position + BotCentOffset, out var edges) && edges.Top != 0)
                    {
                        hasAny = true;
                        unsafe
                        {
                            // We start this two before [max-1],0 for ease of use
                            var ptr = ((byte*)neighbors.GetUnsafePtr()) + PosToBitIndex(0, 3, 0, 7) - 2;
                            PopulateCenterEdgeNeigbors(edges.Top, ptr);
                        }
                    }
                }

                #endregion

                #region LeftRightEdges

                {
                    if (Groups.TryGetValue(position + MidLeftOffset, out var edges) && edges.Right != 0)
                    {
                        hasAny = true;

                        var value = edges.Right;

                        var index = PosToBitIndex(0, 0, 0, 0);
                        neighbors[index] += precalculatedSum[(byte)value & 0b11];

                        for (var y = 1; y < Constants.GroupTotalEdgeLength - 1; y++)
                        {
                            index += ArrayWidth;
                            neighbors[index] = precalculatedSum[(byte)value & 0b111];
                            value >>= 1;
                        }

                        neighbors[index + ArrayWidth] += precalculatedSum[(byte)value & 0b11];
                    }
                }

                {
                    if (Groups.TryGetValue(position + MidRighOffset, out var edges) && edges.Left != 0)
                    {
                        hasAny = true;

                        var value = edges.Left;
                        var index = PosToBitIndex(Constants.GroupSize - 1, 0, Constants.BitFieldSize - 1, 0);
                        neighbors[index] += precalculatedSum[(byte)value & 0b11];

                        for (var y = 1; y < Constants.GroupTotalEdgeLength - 1; y++)
                        {
                            index += ArrayWidth;
                            // No need to += since we know the edges haven't been touched yet
                            neighbors[index] = precalculatedSum[(byte)value & 0b111];
                            value >>= 1;
                        }

                        neighbors[index + ArrayWidth] += precalculatedSum[(byte)value & 0b11];
                    }
                }

                #endregion

                var hasAnyInSelf = false;
                {
                    var current = currentGroups[i];
                    for (var y = 0; y < Constants.GroupSize; y++)
                    {
                        for (var x = 0; x < Constants.GroupSize; x++)
                        {
                            ulong currentBitmask;
                            unsafe
                            {
                                currentBitmask = ((ulong*)&current)[y * Constants.GroupSize + x];
                            }

                            hasAnyInSelf |= currentBitmask != 0;

                            for (var by = 0; by < Constants.BitFieldSize && currentBitmask > 0; by++)
                            {
                                for (var bx = 0; bx < Constants.BitFieldSize; bx++)
                                {
                                    var value = currentBitmask & 1;

                                    var index = PosToBitIndex(x, y, bx, by);
                                    var toAdd = 0b000000010000000100000001 * (uint)value;
                                    var toAddCenter = 0b000000010000100000000001 * (uint)value;
                                    unsafe
                                    {
                                        var ptr = (byte*)neighbors.GetUnsafePtr();
                                        *(uint*)(ptr + index - 1) += toAddCenter;
                                        *(uint*)(ptr + index - ArrayWidth - 1) += toAdd;
                                        *(uint*)(ptr + index + ArrayWidth - 1) += toAdd;
                                    }

                                    currentBitmask >>= 1;
                                }
                            }
                        }
                    }
                }

                ActiveGroups.TryAdd(position, hasAnyInSelf);
                if (!hasAny && !hasAnyInSelf) continue;

                {
                    var next = new NextCglGroup();
                    for (var y = 0; y < Constants.GroupSize; y++)
                    {
                        for (var x = 0; x < Constants.GroupSize; x++)
                        {
                            var nextBitmask = (ulong)0;
                            for (var by = Constants.BitFieldSize - 1; by >= 0; by--)
                            {
                                for (var bx = Constants.BitFieldSize - 1; bx >= 0; bx--)
                                {
                                    nextBitmask <<= 1;
                                    var index = PosToBitIndex(x, y, bx, by);
                                    nextBitmask |= precalculatedAlive[neighbors[index] & 0b1111];
                                    neighbors[index] = 0;
                                }
                            }

                            unsafe
                            {
                                ((ulong*)&next)[y * Constants.GroupSize + x] = nextBitmask;
                            }
                        }
                    }

                    nextGroups[i] = next;
                }
            }
        }

        private unsafe void PopulateCenterEdgeNeigbors(uint line, byte* neighbors)
        {
            // The row consists of 32 bytes padded with 2 bytes on each side we shouldn't touch:
            // [x][x][0][1][2][4][5][6][7]..[^3][^2][^1][x][x]
            // and neighbor points to the x two to the right of 0

            // First we want to add to the bytes [x][x][0][1], so if line.GetBits(0) == 1 then we want the bytes 0,0,1,1
            // The inner ones are easy since it's always [x][n][n+1][n+2], so we need the bytes 0,1,1,1
            // Final one is tricky again and becomes [^2][^1][x][x] 

            // NB: Assumes small endian!
            //                  33333333222222221111111100000000
            const uint addR = 0b00000001000000010000000100000000;
            const uint addF = 0b00000001000000010000000000000000;
            const uint addL = 0b00000000000000010000000100000000;

            var firstAdds = (line & 1) * addF;
            var lastAdds = (line >> 31) * addL;
            (*(uint*)neighbors) += firstAdds;
            (*(uint*)(neighbors + 31)) += lastAdds;

            for (var bit = 1; bit < 31; bit++)
            {
                (*(uint*)(neighbors + bit)) += ((line >> bit) & 1) * addR;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int PosToBitIndex(int x, int y, int bx, int by)
        {
            ThrowIfOutOfBounds(x, y, bx, by);
            return (y * Constants.BitFieldSize + by + ColumnPadding) * ArrayWidth +
                   x * Constants.BitFieldSize + bx + RowPadding;
        }

        [BurstDiscard]
        private void ThrowIfOutOfBounds(int x, int y, int bx, int by)
        {
            if (x is < 0 or >= Constants.GroupSize)
            {
                throw new ArgumentException($"x in {x},{y} - {bx},{by} is out of range 0->{Constants.GroupSize}", nameof(x));
            }

            if (y is < 0 or >= Constants.GroupSize)
            {
                throw new ArgumentException($"y in {x},{y} - {bx},{by} is out of range 0->{Constants.GroupSize}", nameof(y));
            }

            if (bx is < 0 or >= Constants.BitFieldSize)
            {
                throw new ArgumentException($"bx in {x},{y} - {bx},{by} is out of range 0->{Constants.BitFieldSize}", nameof(x));
            }

            if (by is < 0 or >= Constants.BitFieldSize)
            {
                throw new ArgumentException($"by in {x},{y} - {bx},{by} is out of range 0->{Constants.BitFieldSize}", nameof(y));
            }
        }
    }
}