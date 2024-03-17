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
        
        var calcMode = SystemAPI.GetSingleton<CalcMode>();
        if (calcMode.RenderSize == 0 || calcMode.Loading) return;

        var inputPos = Input.mousePosition;
        var screenSize = Screen.height;

        var bottomLeft = new float2(
            Screen.width - screenSize,
            Screen.height - screenSize
        ) / 2;

        var bottomLeftPos = (new float2(inputPos.x, inputPos.y) - bottomLeft) / screenSize;
        bottomLeftPos.y = 1 - bottomLeftPos.y;

        var visualizer = SystemAPI.QueryBuilder().WithAll<Visualizer>().Build().GetSingleton<Visualizer>();

        var tilePos = bottomLeftPos * calcMode.RenderSize - visualizer.Position - calcMode.RenderSize / 2f - Constants.GroupTotalEdgeLength;

        var intPos = (int2)math.floor(tilePos);
        
        if (Input.GetMouseButtonDown(0))
        {
            _prevPos = intPos;
            
            var placePixel = state.EntityManager.CreateEntity(typeof(PlacePixel));
            state.EntityManager.SetComponentData(
                placePixel,
                new PlacePixel
                {
                    Position = tilePos,
                }
            );
            return;
        }

        if (math.all(_prevPos == intPos))
        {
            return;
        }

        DrawLine(_prevPos, tilePos, ref state);
        _prevPos = intPos;
    }

    private void DrawLine(float2 start, float2 end, ref SystemState state)
    {
        var p0 = (int2)math.floor(start);
        var p1 = (int2)math.floor(end);

        int dx = math.abs(p1.x - p0.x);
        int dy = math.abs(p1.y - p0.y);
        int sx = p0.x < p1.x ? 1 : -1;
        int sy = p0.y < p1.y ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            if (p0.x == p1.x && p0.y == p1.y)
                break;

            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                p0.x += sx;
            }

            if (e2 < dx)
            {
                err += dx;
                p0.y += sy;
            }

            var placePixel = state.EntityManager.CreateEntity(typeof(PlacePixel));
            state.EntityManager.SetComponentData(
                placePixel,
                new PlacePixel
                {
                    Position = new float2(p0.x, p0.y),
                }
            );
        }
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