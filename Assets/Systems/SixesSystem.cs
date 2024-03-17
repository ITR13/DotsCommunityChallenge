// I don't feel like writing this all out, but
//  The idea was essentially to remap all the memory into overlapping 8x8 cells, then calculate the inner 6x6 cells 
//  using the algorithm further down.

// The EdgeHashMap algorithm uses ~64*2 array indexes per chunk, this would use ~6 + ~4
//   The left right edge would need a lot more bitshifting though.
//   Don't bother trying to run it, I only typed a tiny portion of what's needed

/*
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;

public partial struct SixesSystem : ISystem
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
        if (calcMode.Algorithm != Algorithm.Sixes || calcMode.Paused) return;

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
            new RunCgl.CollectStructuralChanges
            {
                ActiveGroups = activeGroups.AsReadOnly(),
                ToCreate = toCreate.AsParallelWriter(),
                ToDestroy = toDestroy.AsParallelWriter(),
            }.ScheduleParallel(state.Dependency),
            new RunCgl.SwapBuffersJob().ScheduleParallel(state.Dependency)
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

            var simplified = new NativeArray<ulong>(6 * 6, Allocator.Temp);
            for (var i = 0; i < chunk.Count; i++)
            {
                var position = positions[i].Position;
                var hasAny = false;

                #region Courners

                {
                    if (Groups.TryGetValue(position + TopLeftOffset, out var edges))
                    {
                        var isAlive = (ulong)(edges.Bot >> 31);
                        simplified[0] = isAlive;
                        hasAny |= isAlive != 0;
                    }
                }

                {
                    if (Groups.TryGetValue(position + TopRighOffset, out var edges))
                    {
                        var isAlive = (ulong)(edges.Bot & 1);
                        simplified[5] = isAlive << 3;
                        hasAny |= isAlive != 0;
                    }
                }

                {
                    if (Groups.TryGetValue(position + BotLeftOffset, out var edges))
                    {
                        var isAlive = (ulong)(edges.Top >> 31);
                        simplified[30] = isAlive << 20;
                        hasAny |= isAlive != 0;
                    }
                }

                {
                    if (Groups.TryGetValue(position + BotRighOffset, out var edges))
                    {
                        var isAlive = (ulong)(edges.Top & 1);
                        simplified[35] = isAlive << 23;
                        hasAny |= isAlive != 0;
                    }
                }

                #endregion


                #region UpperLowerEdges

                {
                    if (Groups.TryGetValue(position + TopCentOffset, out var edges) && edges.Bot != 0)
                    {
                        hasAny = true;
                        simplified[0] |= (edges.Bot & 0b00000000000000000000000000000000000001111111) << 1;
                        simplified[1] = (edges.Bot & 0b00000000000000000000000000000001111111100000) >> 5;
                        simplified[2] = (edges.Bot & 0b00000000000000000000000001111111100000000000) >> 11;
                        simplified[3] = (edges.Bot & 0b00000000000000000001111111100000000000000000) >> 17;
                        simplified[4] = (edges.Bot & 0b00000000000001111111100000000000000000000000) >> 23;
                        simplified[5] |= (edges.Bot & (ulong)0b1111111100000000000000000000000000000) >> 29;
                    }
                }

                {
                    if (Groups.TryGetValue(position + BotCentOffset, out var edges) && edges.Top != 0)
                    {
                        hasAny = true;
                        simplified[30] |= (edges.Bot & 0b1111111) << 1;
                        simplified[31] = (edges.Bot & 0b1111111100000) >> 5;
                        simplified[32] = (edges.Bot & 0b1111111100000000000) >> 11;
                        simplified[33] = (edges.Bot & 0b1111111100000000000000000) >> 17;
                        simplified[34] = (edges.Bot & 0b1111111100000000000000000000000) >> 23;
                        simplified[35] |= (edges.Bot & (ulong)0b1111111100000000000000000000000000000) >> 29;
                    }
                }

                #endregion

                #region LeftRightEdges

                {
                    if (Groups.TryGetValue(position + MidLeftOffset, out var edges) && edges.Right != 0)
                    {
                        hasAny = true;

                        var value = edges.Right;

                        simplified[0] |= ((ulong)edges.Right & 0b0000001) << (8 * 1 - 0);
                        simplified[0] |= ((ulong)edges.Right & 0b0000010) << (8 * 2 - 1);
                        simplified[0] |= ((ulong)edges.Right & 0b0000100) << (8 * 3 - 2);
                        simplified[0] |= ((ulong)edges.Right & 0b0001000) << (8 * 4 - 3);
                        simplified[0] |= ((ulong)edges.Right & 0b0010000) << (8 * 5 - 4);
                        simplified[0] |= ((ulong)edges.Right & 0b0100000) << (8 * 6 - 5);
                        simplified[0] |= ((ulong)edges.Right & 0b1000000) << (8 * 7 - 6);

                        simplified[6] |= ((ulong)edges.Right & 0b000000000100000) >> 5;
                        simplified[6] |= ((ulong)edges.Right & 0b000000001000000) << (8 * 1 - 6);
                        simplified[6] |= ((ulong)edges.Right & 0b000000010000000) << (8 * 2 - 7);
                        simplified[6] |= ((ulong)edges.Right & 0b000000100000000) << (8 * 3 - 8);
                        simplified[6] |= ((ulong)edges.Right & 0b000001000000000) << (8 * 4 - 9);
                        simplified[6] |= ((ulong)edges.Right & 0b000010000000000) << (8 * 5 - 10);
                        simplified[6] |= ((ulong)edges.Right & 0b000100000000000) << (8 * 6 - 11);
                        simplified[6] |= ((ulong)edges.Right & 0b001000000000000) << (8 * 7 - 12);

                        simplified[12] |= ((ulong)edges.Right & 0b000000000000000000100000000000) >> (11 - 8 * 0);
                        simplified[12] |= ((ulong)edges.Right & 0b000000000000000001000000000000) >> (12 - 8 * 1);
                        simplified[12] |= ((ulong)edges.Right & 0b000000000000000010000000000000) >> (8 * 3 - 13);
                        simplified[12] |= ((ulong)edges.Right & 0b000000000000000100000000000000) >> (8 * 4 - 14);
                        simplified[12] |= ((ulong)edges.Right & 0b000000000000001000000000000000) << (8 * 5 - 15);
                        simplified[12] |= ((ulong)edges.Right & 0b000000000000010000000000000000) << (8 * 6 - 16);
                        simplified[12] |= ((ulong)edges.Right & 0b000000000000100000000000000000) << (8 * 7 - 17);

                        simplified[18] |= ((ulong)edges.Right & 0b000000000000100000000000000000) >> (16 - 8 * 0);
                        simplified[18] |= ((ulong)edges.Right & 0b000000000001000000000000000000) >> (17 - 8 * 1);
                        simplified[18] |= ((ulong)edges.Right & 0b000000000010000000000000000000) >> (18 - 8 * 2);
                        simplified[18] |= ((ulong)edges.Right & 0b000000000100000000000000000000) << (8 * 3 - 19);
                        simplified[18] |= ((ulong)edges.Right & 0b000000001000000000000000000000) >> (8 * 4 - 20);
                        simplified[18] |= ((ulong)edges.Right & 0b000000010000000000000000000000) >> (8 * 5 - 21);
                        simplified[18] |= ((ulong)edges.Right & 0b000000100000000000000000000000) >> (8 * 6 - 22);
                        simplified[18] |= ((ulong)edges.Right & 0b000001000000000000000000000000) >> (8 * 7 - 23);

                        simplified[24] |= ((ulong)edges.Right & 0b000001000000000) >> (24 - 8 * 0);
                        simplified[24] |= ((ulong)edges.Right & 0b000010000000000) >> (25 - 8 * 1);
                        simplified[24] |= ((ulong)edges.Right & 0b000100000000000) >> (26 - 8 * 2);
                        simplified[24] |= ((ulong)edges.Right & 0b001000000000000) >> (27 - 8 * 3);
                        simplified[24] |= ((ulong)edges.Right & 0b010000000000000) >> (28 - 8 * 4);
                        simplified[24] |= ((ulong)edges.Right & 0b100000000000000) >> (29 - 8 * 5);

                    }
                }

                {
                    if (Groups.TryGetValue(position + MidRighOffset, out var edges) && edges.Left != 0)
                    {
                        hasAny = true;

                        var value = edges.Left;

                        simplified[5] |= (edges.Right & 0b0001) << 8;
                        simplified[5] |= (edges.Right & 0b0010) << 15;
                        simplified[5] |= (edges.Right & 0b0100) << 22;
                        simplified[5] |= (edges.Right & 0b1000) << 29;

                        simplified[11] |= (edges.Right & 0b00010000) << 4;
                        simplified[11] |= (edges.Right & 0b00100000) << 11;
                        simplified[11] |= (edges.Right & 0b01000000) << 18;
                        simplified[11] |= (edges.Right & 0b10000000) << 25;

                        simplified[17] |= (edges.Right & 0b000100000000);
                        simplified[17] |= (edges.Right & 0b001000000000) << 7;
                        simplified[17] |= (edges.Right & 0b010000000000) << 14;
                        simplified[17] |= (edges.Right & 0b100000000000) << 21;

                        simplified[23] |= (edges.Right & 0b0001000000000000) >> 4;
                        simplified[23] |= (edges.Right & 0b0010000000000000) << 3;
                        simplified[23] |= (edges.Right & 0b0100000000000000) << 10;
                        simplified[23] |= (edges.Right & 0b1000000000000000) << 17;
                    }
                }

                #endregion

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

                            hasAny |= currentBitmask != 0;

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

                ActiveGroups.TryAdd(position, hasAny);

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

        // Based on code from Tom Rokicki and Tony Finch
        // https://dotat.at/prog/life/liar2.c
        private ulong Liar2(ulong bmp)
        {
            // this wraps around the left and right edges
            // but incorrect results for the border are ok
            var left = bmp << 1;
            var right = bmp >> 1;
            // half adder count of cells to either side
            var side1 = left ^ right;
            var side2 = left & right;
            // full adder count of cells in middle row
            var mid1 = side1 ^ bmp;
            var mid2 = side1 & bmp;
            mid2 = side2 | mid2;
            // shift middle row count to get upper and lower row counts
            var upper1 = mid1 << 8;
            var lower1 = mid1 >> 8;
            var upper2 = mid2 << 8;
            var lower2 = mid2 >> 8;
            // compress vertically
            Reduce(out var sum12, out var sum13, upper1, side1, lower1);
            Reduce(out var sum24, out var sum26, upper2, side2, lower2);
            // calculate result
            var tmp = sum12 ^ sum13;
            bmp = (bmp | sum13) & (tmp ^ sum24) & (tmp ^ sum26);
            // mask out incorrect border cells
            return (bmp & 0x007E7E7E7E7E7E00);
            // total 19 + 7 = 26 operations
        }

        // Helper function for adder-like 3-to-2 reduction
        private void Reduce(out ulong g, out ulong f, ulong a, ulong b, ulong c)
        {
            ulong d = a ^ b;
            ulong e = b ^ c;
            f = c ^ d;
            g = d | e;
        }
    }
}*/