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
                SimulateStill = true,
                RenderSize = 1024,
            }
        );
    }

    public void OnUpdate(ref SystemState state)
    {
        var singleton = SystemAPI.GetSingletonRW<CalcMode>();
        if (Input.GetKeyDown(KeyCode.Space))
        {
            singleton.ValueRW.SimulateStill ^= true;
            // singleton.ValueRW.Algorithm = (Algorithm)(((int)singleton.ValueRW.Algorithm + 1) % 2);
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