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

        var cglQuery = em.CreateEntityQuery(typeof(CurrentCglGroup));
        em.DestroyEntity(cglQuery);

        var sizes = new[] {1, 1024, 2048, 4096, 8192, 16384, 32768, 65536, 131072};
        var widths = new[] {1, 32, 32, 64, 64, 128, 128, 256, 256};
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
                    toSpawn.Data.Alive1 = random.NextUInt();
                    toSpawn.Data.Alive2 = random.NextUInt();
                    toSpawn.Data.Alive3 = random.NextUInt();
                    toSpawn.Data.Alive4 = random.NextUInt();
                    toSpawn.Data.Alive5 = random.NextUInt();
                    toSpawn.Data.Alive6 = random.NextUInt();
                    toSpawn.Data.Alive7 = random.NextUInt();
                    toSpawn.Data.Alive8 = random.NextUInt();
                    toSpawn.Data.Alive9 = random.NextUInt();
                    toSpawn.Data.Alive10 = random.NextUInt();
                    toSpawn.Data.Alive11 = random.NextUInt();
                    toSpawn.Data.Alive12 = random.NextUInt();
                    toSpawn.Data.Alive13 = random.NextUInt();
                    toSpawn.Data.Alive14 = random.NextUInt();
                    toSpawn.Data.Alive15 = random.NextUInt();
                }
                
                var group = em.CreateEntity(typeof(GroupPosition), typeof(CurrentCglGroup), typeof(NextCglGroup));
                em.SetComponentData(
                    group,
                    new GroupPosition
                    {
                        Position = new int2(32 * (x - 1), 32 * (y - 1)),
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
                Alive0 = 0b1000000000000000000000001000000000000000000000000000000000000000,
                Alive1 = 0b0001000110000000100111111010101010100010000101100110011111100100,
                Alive2 = 0b1110010100100100100011100000001001000011110110010111101000001000,
                Alive3 = 0b1001010110000111010010000000001000000000000000000000000100000010,

                Alive4 = 0b0000000000000000000000001000000010000000100000000000000000000000,
                Alive5 = 0b1110010010100000111001000001101110010100100000000010000100000000,
                Alive6 = 0b0100010110010010101010110000100011101010101001001101000100000100,
                Alive7 = 0b0100001010000000100101001110110000010011000000100001001111101100,

                Alive8 = 0b0000000000000000000000000000000000000000100000001000000010000000,
                Alive9 = 0b0100000010000000000000000010000010001001011100001101010000011011,
                Alive10 = 0b0001000001010011100100101011100010100000111000010100110100101111,
                Alive11 = 0b0111001100110100001000101010101001111110000000000110010000000000,

                Alive12 = 0b00000000,
                Alive13 = 0b00100000,
                Alive14 = 0b10001000,
                Alive15 = 0b00010011,
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