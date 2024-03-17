using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[UpdateAfter(typeof(QuadTreeSystem))]
[UpdateAfter(typeof(EntityHashMapSystem))]
[UpdateAfter(typeof(EdgeHashMapSystem))]
public partial class UpdateRenderSystem : SystemBase
{
    private int _shownSize = 0;
    private int _shownArea = 0;

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

        _visualizedGroups = new NativeArray<CglGroupData>(_shownArea, Allocator.Persistent);
        _computeBuffer = new ComputeBuffer(1, 4);

        // Application.targetFrameRate = 24;
    }

    protected override void OnDestroy()
    {
        _visualizedGroups.Dispose();
        _computeBuffer.Dispose();
    }

    protected override void OnUpdate()
    {
        var calcMode = SystemAPI.GetSingleton<CalcMode>();
        var visualizer = SystemAPI.QueryBuilder().WithAllRW<Visualizer>().Build().GetSingletonRW<Visualizer>();

        var shownSize = calcMode.RenderSize / Constants.GroupTotalEdgeLength;
        if (shownSize != _shownSize)
        {
            _shownSize = shownSize;
            _shownArea = _shownSize * _shownSize;


            _computeBuffer.Dispose();
            _visualizedGroups.Dispose();

            _visualizedGroups = new NativeArray<CglGroupData>(_shownArea, Allocator.Persistent);
            var bufferSize = _shownArea * Constants.GroupTotalArea / (8 * 4);

            if (bufferSize == 0) bufferSize = 1;
            _computeBuffer = new ComputeBuffer(bufferSize, 4);

            visualizer.Material.SetInt(_lengthProperty, 0);
            visualizer.Material.SetInt(_sizeProperty, _shownSize);
            visualizer.Material.SetInt(_areaProperty, _shownArea);
            visualizer.Material.SetBuffer(_bufferProperty, _computeBuffer);
        }

        if (calcMode.RenderSize == 0)
        {
            return;
        }

        var dir = math.clamp(
            math.float2(
                -Input.GetAxis("Horizontal"),
                Input.GetAxis("Vertical")
            ),
            new float2(-1, -1),
            new float2(1, 1)
        );
        var delta = float2.zero;

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

        if (_shownSize > 64)
        {
            delta *= _shownSize / 64f;
        }

        if (Input.GetKey(KeyCode.LeftShift))
        {
            delta *= 3;
        }

        if (Input.GetKey(KeyCode.RightShift))
        {
            delta *= 3;
        }

        visualizer.Position += delta;
        var position = visualizer.Position + _shownSize * Constants.GroupTotalEdgeLength / 2f;

        for (var i = 0; i < _shownArea; i++)
        {
            _visualizedGroups[i] = default;
        }

        var simplePosition = (int2)math.floor(-position / Constants.GroupTotalEdgeLength - 0.5f);

        var updateRenderDataJob = new FindRenderData
        {
            Groups = _visualizedGroups,
            ViewSimplePosition = simplePosition,
            ShownSize = _shownSize,
            ShownArea = _shownArea,
        };

#if DEBUGGER_FIX
        updateRenderDataJob.Run();
#else
        Dependency = updateRenderDataJob.ScheduleParallel(Dependency);
        Dependency.Complete();
#endif


        NativeArray<uint> reinterpreted = _visualizedGroups.Reinterpret<uint>(Constants.GroupTotalArea / 8);
        _computeBuffer.SetData(reinterpreted);


        var fractionalPos = ((position + (simplePosition + new int2(1, 1)) * Constants.GroupTotalEdgeLength) / Constants.GroupTotalEdgeLength);

        visualizer.Material.SetVector(_positionProperty, new Vector4(fractionalPos.x, fractionalPos.y, 0, 0));
        visualizer.Material.SetInt(_lengthProperty, reinterpreted.Length);
    }

    private partial struct FindRenderData : IJobEntity
    {
        [NativeDisableParallelForRestriction] public NativeArray<CglGroupData> Groups;
        [ReadOnly] public int2 ViewSimplePosition;
        [ReadOnly] public int ShownSize, ShownArea;

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