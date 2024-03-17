using System;
using System.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

public class VisualController : MonoBehaviour
{
    [SerializeField] private GameObject _quad, _graphy, _loadMenu;
    [SerializeField] private Text _text;

    private EntityQuery _calcQuery, _statsQuery;
    private CalcMode _prevCalc;
    private Stats _prevStats;

    private IEnumerator Start()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        var entityManager = world.EntityManager;
        _calcQuery = entityManager.CreateEntityQuery(typeof(CalcMode));
        _statsQuery = entityManager.CreateEntityQuery(typeof(Stats));
        var visualizerQuery = entityManager.CreateEntityQuery(typeof(Visualizer));
        Debug.Log("Waiting for visualizer...");

        while (visualizerQuery.IsEmpty)
        {
            yield return null;
        }
        Application.targetFrameRate = -1;
        
        Debug.Log("Getting visualizer!");
        
        var visualizer = visualizerQuery.GetSingleton<Visualizer>();

        Debug.Log("Creating new material");
        var material = new Material(visualizer.Material);
        visualizer.Material = material;
        Debug.Log("Setting material to renderer");
        _quad.GetComponent<MeshRenderer>().sharedMaterial = visualizer.Material;
    }

    private void Update()
    {
        if (_calcQuery.IsEmpty) return;
        if (_calcQuery.IsEmpty || _statsQuery.IsEmpty) return;
        if (_calcQuery.IsEmpty) return;

        var calc = _calcQuery.GetSingletonRW<CalcMode>();
        var stats = _statsQuery.GetSingleton<Stats>();

        if (Input.GetKeyDown(KeyCode.L))
        {
            _loadMenu.SetActive(!_loadMenu.activeSelf);
            calc.ValueRW.Loading = _loadMenu.activeSelf;
            calc.ValueRW.Paused = true;
        }

        if (calc.ValueRO.Equals(_prevCalc) && _prevStats.ActiveGroups == stats.ActiveGroups && _prevStats.InactiveGroups == stats.InactiveGroups)
        {
            return;
        }

        _prevCalc = calc.ValueRO;
        _prevStats = stats;

        _quad.SetActive(calc.ValueRO.RenderSize > 0);
        _graphy.SetActive(calc.ValueRO.ShowUi);

        var algorithmName = Enum.GetName(typeof(Algorithm), calc.ValueRO.Algorithm);
        var pauseColor = calc.ValueRO.Paused ? "<color=red>" : "<color=green>";
        var renderColor = calc.ValueRO.RenderSize <= 0 ? "<color=red>" : "<color=yellow>";
        
        _text.text = @$"
<b>Algorithm:</b> {algorithmName}
<b>VisScale:</b> {renderColor}{calc.ValueRO.RenderSize}</color>
<b>Simulating:</b> {pauseColor}{!calc.ValueRO.Paused}</color>
<b>ActiveGroups:</b> {stats.ActiveGroups}
<b>InactiveGroups:</b> {stats.InactiveGroups}
".Trim();
    }
}