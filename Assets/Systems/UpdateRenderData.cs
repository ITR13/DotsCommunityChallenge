using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[UpdateAfter(typeof(RunCgl))]
[UpdateAfter(typeof(RunCgl2))]
public partial class UpdateRenderData : SystemBase
{
    private const int ShownSize = 1024 / Constants.GroupTotalEdgeLength;
    private const int ShownArea = ShownSize * ShownSize;

    private NativeArray<CglGroupData> _visualizedGroups;
    private ComputeBuffer _computeBuffer;

    private int _positionProperty;
    private int _bufferProperty;
    private int _lengthProperty;
    private int _sizeProperty;
    private int _areaProperty;

    private int2 _previousIntMovement;

    protected override void OnCreate()
    {
        RequireForUpdate<Visualizer>();
        RequireForUpdate<CalcMode>();

        _positionProperty = Shader.PropertyToID("_offset");
        _bufferProperty = Shader.PropertyToID("_buffer");
        _lengthProperty = Shader.PropertyToID("_length");
        _sizeProperty = Shader.PropertyToID("_size");
        _areaProperty = Shader.PropertyToID("_area");

        _visualizedGroups = new NativeArray<CglGroupData>(ShownArea, Allocator.Persistent);
        _computeBuffer = new ComputeBuffer(ShownArea * Constants.GroupTotalArea / (8 * 4), 4);

        // Application.targetFrameRate = 24;
    }

    protected override void OnDestroy()
    {
        _visualizedGroups.Dispose();
    }

    protected override void OnUpdate()
    {
        var calcMode = SystemAPI.GetSingleton<CalcMode>();
        if (!calcMode.Render) return;

        var dir = math.clamp(
            math.float2(
                -Input.GetAxis("Horizontal"),
                Input.GetAxis("Vertical")
            ),
            new float2(-1, -1),
            new float2(1, 1)
        );
        var delta = float2.zero;

        var visualizer = SystemAPI.QueryBuilder().WithAllRW<Visualizer>().Build().GetSingletonRW<Visualizer>();

        if (Input.GetKey(KeyCode.LeftControl))
        {
            var intMovement = (int2)math.round(dir);
            if (intMovement.x != _previousIntMovement.x)
            {
                delta.x += intMovement.x * Constants.GroupTotalEdgeLength;
            }

            if (intMovement.y != _previousIntMovement.y)
            {
                delta.y += intMovement.y * Constants.GroupTotalEdgeLength;
            }

            _previousIntMovement = intMovement;

            visualizer.Position = math.round(visualizer.Position / Constants.GroupTotalEdgeLength) * Constants.GroupTotalEdgeLength;
        }
        else
        {
            _previousIntMovement = int2.zero;
            delta += dir * SystemAPI.Time.DeltaTime * Constants.GroupTotalEdgeLength * 2;
        }

        visualizer.Position += delta;
        var position = visualizer.Position + ShownSize * Constants.GroupTotalEdgeLength / 2f;

        for (var i = 0; i < 9; i++)
        {
            _visualizedGroups[i] = default;
        }

        var simplePosition = (int2)math.floor(-position / Constants.GroupTotalEdgeLength - 0.5f);

        Dependency = new FindRenderData
        {
            Groups = _visualizedGroups,
            ViewSimplePosition = simplePosition,
        }.ScheduleParallel(Dependency);

        Dependency.Complete();


        NativeArray<uint> reinterpreted = _visualizedGroups.Reinterpret<uint>(Constants.GroupTotalArea / 8);
        _computeBuffer.SetData(reinterpreted);


        var fractionalPos = ((position + (simplePosition + new int2(1, 1)) * Constants.GroupTotalEdgeLength) / Constants.GroupTotalEdgeLength);

        visualizer.Material.SetVector(_positionProperty, new Vector4(fractionalPos.x, fractionalPos.y, 0, 0));
        visualizer.Material.SetInt(_lengthProperty, reinterpreted.Length);
        visualizer.Material.SetInt(_sizeProperty, ShownSize);
        visualizer.Material.SetInt(_areaProperty, ShownArea);
        visualizer.Material.SetBuffer(_bufferProperty, _computeBuffer);
    }

    private partial struct FindRenderData : IJobEntity
    {
        [NativeDisableParallelForRestriction] public NativeArray<CglGroupData> Groups;
        [ReadOnly] public int2 ViewSimplePosition;

        private void Execute(in CurrentCglGroup currentCglGroup, in GroupPosition position)
        {
            var groupSimplePosition = position.Position / Constants.GroupTotalEdgeLength;

            var delta = groupSimplePosition - ViewSimplePosition;
            if (delta.x < 0 || delta.y < 0 || delta.x >= ShownSize || delta.y >= ShownSize)
            {
                return;
            }

            Groups[delta.y * ShownSize + delta.x] = currentCglGroup.Data;
        }
    }
}