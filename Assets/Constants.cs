public static class Constants
{
    public const byte GroupSize = 4;
    public const ushort GroupArea = GroupSize * GroupSize;
    public const byte BitFieldSize = 8;
    public const byte BitFieldArea = BitFieldSize * BitFieldSize;
    public const ushort GroupTotalArea = GroupArea * BitFieldArea;

    public const int GroupTotalEdgeLength = GroupSize * BitFieldSize;
}
