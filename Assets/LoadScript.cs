using System.Linq;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class LoadScript : MonoBehaviour
{
    [SerializeField] private GameObject[] _patterns;
    [SerializeField] private GameObject[] _sizes;
    [SerializeField] private Button _complete;

    private int _selectedPattern, _selectedSize;

    private void Start()
    {
        for (var i = 0; i < _patterns.Length; i++)
        {
            var index = i;
            _patterns[i].GetComponent<Button>().onClick.AddListener(() => SetPattern(index));
        }

        for (var i = 0; i < _sizes.Length; i++)
        {
            var index = i;
            _sizes[i].GetComponent<Button>().onClick.AddListener(() => SetSize(index));
        }

        _complete.onClick.AddListener(Play);

        SetPattern(0);
        SetSize(1);
    }

    private void SetPattern(int pattern)
    {
        _selectedPattern = pattern;
        var images = _patterns.Select(pattern => pattern.GetComponent<Image>()).ToArray();
        for (var i = 0; i < images.Length; i++)
        {
            images[i].color = i == pattern ? Color.yellow : Color.white;
        }
    }

    private void SetSize(int size)
    {
        _selectedSize = size;
        var images = _sizes.Select(pattern => pattern.GetComponent<Image>()).ToArray();
        for (var i = 0; i < images.Length; i++)
        {
            images[i].color = i == size ? Color.yellow : Color.white;
        }
    }

    private void Play()
    {
        gameObject.SetActive(false);
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        var calc = em.CreateEntityQuery(typeof(CalcMode)).GetSingletonRW<CalcMode>();
        calc.ValueRW.Loading = false;
        calc.ValueRW.Paused = true;


        var visualizer = em.CreateEntityQuery(typeof(Visualizer)).GetSingletonRW<Visualizer>();
        visualizer.Position = float2.zero;

        var cglQuery = em.CreateEntityQuery(typeof(CurrentCglGroup));
        em.DestroyEntity(cglQuery);

        var sizes = new[] {1, 1024, 2048, 4096, 8192, 16384, 32768, 65536, 131072, 262144};
        var widths = new[] {1, 32, 32, 64, 64, 128, 128, 256, 256, 512};
        var size = sizes[_selectedSize];

        if (_selectedPattern == 2)
        {
            SpawnGospelGliderGun(em, size);
            return;
        }

        var width = widths[_selectedSize];

        CurrentCglGroup toSpawn = _selectedPattern switch
        {
            0 => Glider(),
            1 => Lightweight(),
            3 => SpaceFiller(),
            _ => default
        };
        var random = new Unity.Mathematics.Random((uint)Random.Range(int.MinValue, int.MaxValue));

        var height = size / width;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (_selectedPattern == 4)
                {
                    toSpawn.Data.Alive0 = random.NextUInt() | (ulong)random.NextUInt() << 32;
                    toSpawn.Data.Alive1 = random.NextUInt() | (ulong)random.NextUInt() << 32;
                    toSpawn.Data.Alive2 = random.NextUInt() | (ulong)random.NextUInt() << 32;
                    toSpawn.Data.Alive3 = random.NextUInt() | (ulong)random.NextUInt() << 32;
                    toSpawn.Data.Alive4 = random.NextUInt() | (ulong)random.NextUInt() << 32;
                    toSpawn.Data.Alive5 = random.NextUInt() | (ulong)random.NextUInt() << 32;
                    toSpawn.Data.Alive6 = random.NextUInt() | (ulong)random.NextUInt() << 32;
                    toSpawn.Data.Alive7 = random.NextUInt() | (ulong)random.NextUInt() << 32;
                    toSpawn.Data.Alive8 = random.NextUInt() | (ulong)random.NextUInt() << 32;
                    toSpawn.Data.Alive9 = random.NextUInt() | (ulong)random.NextUInt() << 32;
                    toSpawn.Data.Alive10 = random.NextUInt() | (ulong)random.NextUInt() << 32;
                    toSpawn.Data.Alive11 = random.NextUInt() | (ulong)random.NextUInt() << 32;
                    toSpawn.Data.Alive12 = random.NextUInt() | (ulong)random.NextUInt() << 32;
                    toSpawn.Data.Alive13 = random.NextUInt() | (ulong)random.NextUInt() << 32;
                    toSpawn.Data.Alive14 = random.NextUInt() | (ulong)random.NextUInt() << 32;
                    toSpawn.Data.Alive15 = random.NextUInt() | (ulong)random.NextUInt() << 32;
                }

                var group = em.CreateEntity(typeof(GroupPosition), typeof(CurrentCglGroup), typeof(NextCglGroup));
                em.SetComponentData(
                    group,
                    new GroupPosition
                    {
                        Position = new int2(32 * (x - width / 2 - 1), 32 * (y - height / 2 - 1)),
                    }
                );
                em.SetComponentData(group, toSpawn);
            }
        }
    }

    private void SpawnGospelGliderGun(EntityManager em, int size)
    {
        var (lC, rC) = GospelGliderGun();

        for (var i = 0; i < size; i += 2)
        {
            var left = em.CreateEntity(typeof(GroupPosition), typeof(CurrentCglGroup), typeof(NextCglGroup));
            var right = em.CreateEntity(typeof(GroupPosition), typeof(CurrentCglGroup), typeof(NextCglGroup));

            em.SetComponentData(
                left,
                new GroupPosition
                {
                    Position = new int2(32 * i - 32, -32),
                }
            );
            em.SetComponentData(
                right,
                new GroupPosition
                {
                    Position = new int2(32 * i, -32),
                }
            );

            em.SetComponentData(left, lC);
            em.SetComponentData(right, rC);
        }
    }

    private CurrentCglGroup Glider()
    {
        return new CurrentCglGroup
        {
            Data = new CglGroupData
            {
                Alive0 = 0b000000010000010100000011000000000,
                Alive1 = 0b000000010000010100000011000000000,
                Alive2 = 0b000000010000010100000011000000000,
                Alive3 = 0b000000010000010100000011000000000,
                Alive4 = 0b000000010000010100000011000000000,
                Alive5 = 0b000000010000010100000011000000000,
                Alive6 = 0b000000010000010100000011000000000,
                Alive7 = 0b000000010000010100000011000000000,
                Alive8 = 0b000000010000010100000011000000000,
                Alive9 = 0b000000010000010100000011000000000,
                Alive10 = 0b000000010000010100000011000000000,
                Alive11 = 0b000000010000010100000011000000000,
                Alive12 = 0b000000010000010100000011000000000,
                Alive13 = 0b000000010000010100000011000000000,
                Alive14 = 0b000000010000010100000011000000000,
                Alive15 = 0b000000010000010100000011000000000,
            },
        };
    }

    private CurrentCglGroup Lightweight()
    {
        return new CurrentCglGroup
        {
            Data = new CglGroupData
            {
                Alive0 = 0b0011110000100010001000000001001000000000,
                Alive1 = 0b0011110000100010001000000001001000000000,
                Alive2 = 0b0011110000100010001000000001001000000000,
                Alive3 = 0b0011110000100010001000000001001000000000,
                Alive4 = 0b0011110000100010001000000001001000000000,
                Alive5 = 0b0011110000100010001000000001001000000000,
                Alive6 = 0b0011110000100010001000000001001000000000,
                Alive7 = 0b0011110000100010001000000001001000000000,
                Alive8 = 0b0011110000100010001000000001001000000000,
                Alive9 = 0b0011110000100010001000000001001000000000,
                Alive10 = 0b0011110000100010001000000001001000000000,
                Alive11 = 0b0011110000100010001000000001001000000000,
                Alive12 = 0b0011110000100010001000000001001000000000,
                Alive13 = 0b0011110000100010001000000001001000000000,
                Alive14 = 0b0011110000100010001000000001001000000000,
                Alive15 = 0b0011110000100010001000000001001000000000,
            },
        };
    }

    private CurrentCglGroup SpaceFiller()
    {
        return new CurrentCglGroup
        {
            Data = new CglGroupData
            {
                Alive0 = 0b1100000010100000100000001000000011000000000000000000000000000000,
                Alive1 = 0b1010011110101010111010000100010110011001001110010000000000000000,
                Alive2 = 0b0010001110000000000100000011011001011110100000100000000000000000,
                Alive3 = 0b0001001000000000000000000000000000000000000000000000000000000000,
                Alive4 = 0b0000000011100000001000000010000001000000000000000110000000000000,
                Alive5 = 0b1111100100000110101001010010000001001000000000000100010000100000,
                Alive6 = 0b0010101000000010111110101010100111110100000000010111100111001001,
                Alive7 = 0b0010010100111011000001000000000000000100001110110010010100100001,
                Alive8 = 0b0000000000000000010000000010000000100000111000000000000000000000,
                Alive9 = 0b0100000000001000001000101001110011110101000001100111100110101000,
                Alive10 = 0b1011100010101000001011100010010000010100000001001001000100100100,
                Alive11 = 0b0000100000101010000111110000000000110001000000000001000000100000,
                Alive12 = 0b0000000000000000000000000000000000000000000000000000000000000000,
                Alive13 = 0b0000000000000000000000000000000000000000000010001101000001100000,
                Alive14 = 0b0000000000000000000000000000000000000000111000101100101100010011,
                Alive15 = 0b0000000000000000000000000000000000000000000001000001110000001101,
            },
        };
    }

    private (CurrentCglGroup, CurrentCglGroup) GospelGliderGun()
    {
        return (
            new CurrentCglGroup
            {
                Data = new CglGroupData
                {
                    Alive3 = 0b0000000000000000000000000000000000110000001100000000000000000000,
                },
            },
            new CurrentCglGroup
            {
                Data = new CglGroupData
                {
                    Alive3 = 0b0000000000000000110000001100000000000000000000000000000000000000,
                    Alive2 = 0b0001000100100000001000100010000000010001000011000000000000000000,
                    Alive1 = 0b0000000010000000110000101000110000001100000011000000001000000000,
                    Alive0 = 0b0000000010000000100000000000000000000000000000001000000010000000,
                    Alive6 = 0b0000000000000000000000000000000000000000000000000000000000001100,
                },
            }
        );
    }
}