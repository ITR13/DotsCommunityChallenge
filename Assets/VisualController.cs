using System;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

public class VisualController : MonoBehaviour
{
    [SerializeField] private GameObject _quad, _graphy;
    [SerializeField] private Text _text;

    private EntityQuery _calcQuery;
    private CalcMode _prevCalc;

    private void Start()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        var entityManager = world.EntityManager;
        _calcQuery = entityManager.CreateEntityQuery(typeof(CalcMode));
    }

    private void Update()
    {
        if (_calcQuery.IsEmpty) return;

        var calc = _calcQuery.GetSingleton<CalcMode>();

        if (calc.Equals(_prevCalc))
        {
            return;
        }

        _prevCalc = calc;

        _quad.SetActive(calc.RenderSize > 0);
        _graphy.SetActive(calc.ShowUi);

        var algorithmName = Enum.GetName(typeof(Algorithm), calc.Algorithm);

        _text.text = @$"
Algorithm: {algorithmName}
Vis Scale: {calc.RenderSize}
Paused: {calc.Paused}
".Trim();
    }
}