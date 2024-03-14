using Unity.Entities;
using UnityEngine;

public partial struct ModeSwapperSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.EntityManager.CreateSingleton(
            new CalcMode
            {
                Algorithm = Algorithm.HashMap,
                SimulateStill = false,
                Render = true,
            }
        );
    }

    public void OnUpdate(ref SystemState state)
    {
        var singleton = SystemAPI.GetSingletonRW<CalcMode>();
        if (Input.GetKeyDown(KeyCode.Space))
        {
            singleton.ValueRW.Algorithm = (Algorithm)(((int)singleton.ValueRW.Algorithm + 1) % 2);
        }
    }
}