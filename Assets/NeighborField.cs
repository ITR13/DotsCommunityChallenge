/*
using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Burst.Intrinsics;

using static Constants;
// ReSharper disable RedundantUsingDirective
using static Unity.Burst.Intrinsics.X86;
using static Unity.Burst.Intrinsics.X86.Sse;
using static Unity.Burst.Intrinsics.X86.Sse2;
using static Unity.Burst.Intrinsics.X86.Sse3;
using static Unity.Burst.Intrinsics.X86.Ssse3;
using static Unity.Burst.Intrinsics.X86.Sse4_1;
using static Unity.Burst.Intrinsics.X86.Sse4_2;
using static Unity.Burst.Intrinsics.X86.Popcnt;
using static Unity.Burst.Intrinsics.X86.Avx;
using static Unity.Burst.Intrinsics.X86.Avx2;
using static Unity.Burst.Intrinsics.X86.Fma;
using static Unity.Burst.Intrinsics.X86.F16C;
using static Unity.Burst.Intrinsics.X86.Bmi1;
using static Unity.Burst.Intrinsics.X86.Bmi2;
using static Unity.Burst.Intrinsics.Arm.Neon;
// ReSharper restore RedundantUsingDirective

public struct NeighorField
{
    public v256 Counts;
}

[BurstCompile]
public static class NeighborFieldTools
{
    [BurstCompile]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte GetCount(in this NeighorField neighbors, in int index)
    {
        var bit = index * 3;
        var byteShift = bit / 8;
        var bitShift = bit % 8;

        unsafe
        {
            var counts = neighbors.Counts;
            var bPtr = (byte*)&counts;
            var iPtr = (uint*)bPtr[byteShift];
            var value = (*iPtr) >> bitShift;
            return (byte)(value & 0b111);
        }
    }
    
    
    public static int SubVector(byte x, byte y)
    {
        ThrowIfUnbounded(x, y);
        return x + y * GroupSize;
    }

    [BurstDiscard]
    private static void ThrowIfUnbounded(ushort x, ushort y)
    {
        if (x >= GroupSize)
        {
            throw new ArgumentException(nameof(x), $"Value {x} is >= 32");
        }
        if (y >= GroupSize)
        {
            throw new ArgumentException(nameof(x), $"Value {x} is >= 32");
        }
    }
}*/