using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public partial struct ModeSwapperSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.EntityManager.CreateSingleton(
            new CalcMode
            {
                Algorithm = Algorithm.HashMap,
                Paused = true,
                RenderSize = 1024,
                ShowUi = true,
            }
        );
    }

    public void OnUpdate(ref SystemState state)
    {
        var singleton = SystemAPI.GetSingletonRW<CalcMode>();
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.P))
        {
            singleton.ValueRW.Paused ^= true;
        }

        if (Input.GetKeyDown(KeyCode.Q))
        {
            singleton.ValueRW.Algorithm = (Algorithm)(((int)singleton.ValueRW.Algorithm - 1 + (int)Algorithm.MAX_VALUE) % (int)Algorithm.MAX_VALUE);
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            singleton.ValueRW.Algorithm = (Algorithm)(((int)singleton.ValueRW.Algorithm + 1) % 2);
        }

        if (Input.GetKeyDown(KeyCode.Delete))
        {
            var cglQuery = SystemAPI.QueryBuilder().WithAll<CurrentCglGroup>().Build();
            state.EntityManager.DestroyEntity(cglQuery);
        }

        if (Input.GetKeyDown(KeyCode.Alpha0))
        {
            singleton.ValueRW.RenderSize = 0;
        }

        for (var i = KeyCode.Alpha1; i <= KeyCode.Alpha9; i++)
        {
            if (!Input.GetKeyDown(i)) continue;
            var size = math.pow(2, (int)(i - KeyCode.Alpha0) + 4);
            singleton.ValueRW.RenderSize = Mathf.RoundToInt(size);
        }
    }
}