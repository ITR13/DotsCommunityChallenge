using System;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

public class VisualController : MonoBehaviour
{
    [SerializeField] private GameObject _quad, _graphy;
    [SerializeField] private Text _text;

    private EntityQuery _calcQuery, _statsQuery;
    private CalcMode _prevCalc;
    private Stats _prevStats;

    private void Start()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        var entityManager = world.EntityManager;
        _calcQuery = entityManager.CreateEntityQuery(typeof(CalcMode));
        _statsQuery = entityManager.CreateEntityQuery(typeof(Stats));
    }

    private void Update()
    {
        if (_calcQuery.IsEmpty || _statsQuery.IsEmpty) return;

        var calc = _calcQuery.GetSingleton<CalcMode>();
        var stats = _statsQuery.GetSingleton<Stats>();

        if (calc.Equals(_prevCalc) && _prevStats.ActiveGroups == stats.ActiveGroups && _prevStats.InactiveGroups == stats.InactiveGroups)
        {
            return;
        }

        _prevCalc = calc;
        _prevStats = stats;

        _quad.SetActive(calc.RenderSize > 0);
        _graphy.SetActive(calc.ShowUi);

        var algorithmName = Enum.GetName(typeof(Algorithm), calc.Algorithm);
        var pauseColor = calc.Paused ? "<color=red>" : "<color=green>";
        var renderColor = calc.RenderSize <= 0 ? "<color=red>" : "<color=yellow>";
        
        _text.text = @$"
<b>Algorithm:</b> {algorithmName}
<b>VisScale:</b> {renderColor}{calc.RenderSize}</color>
<b>Simulating:</b> {pauseColor}{!calc.Paused}</color>
<b>ActiveGroups:</b> {stats.ActiveGroups}
<b>InactiveGroups:</b> {stats.InactiveGroups}
".Trim();
    }
}