using Unity.Entities;

public partial struct ModeSwapperSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.EntityManager.CreateSingleton(
            new CalcMode
            {
                Algorithm = Algorithm.HashMap,
            }
        );
    }
}