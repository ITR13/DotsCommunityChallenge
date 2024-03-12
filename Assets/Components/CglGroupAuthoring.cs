using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class CglGroupAuthoring : MonoBehaviour
{
    public enum Spawnable
    {
        None = 0,
        Blink = 1,
        Box = 2,
        Glider = 3,
        TinyBox = 4,
        BigBlock = 5,
    }

    public Spawnable WhatToSpawn;
    public bool SpawnInAll;

    public class CurrentCglGroupBaker : Baker<CglGroupAuthoring>
    {
        public override void Bake(CglGroupAuthoring authoring)
        {
            // if (!authoring.gameObject.activeInHierarchy) return;

            const ulong blink = 0b0000000001110111000000000111011100000000011101110000000001110111;
            const ulong box = 0b110011001100110000000000;
            const ulong glider = 0b000000010000010100000011000000000;
            const ulong tinybox = 771;
            const ulong bigblock = ~(ulong)0;

            var current = authoring.WhatToSpawn switch
            {
                Spawnable.None => (ulong)0,
                Spawnable.Blink => blink,
                Spawnable.Box => box,
                Spawnable.Glider => glider,
                Spawnable.TinyBox => tinybox,
                Spawnable.BigBlock => bigblock,
                _ => (ulong)0,
            };

            var entity = GetEntity(TransformUsageFlags.None);
            if (authoring.SpawnInAll)
            {
                AddComponent(
                    entity,
                    new CurrentCglGroup
                    {
                        Data = new CglGroupData
                        {
                            Alive0 = current,
                            Alive1 = current,
                            Alive2 = current,
                            Alive3 = current,
                            Alive4 = current,
                            Alive5 = current,
                            Alive6 = current,
                            Alive7 = current,
                            Alive8 = current,
                            Alive9 = current,
                            Alive10 = current,
                            Alive11 = current,
                            Alive12 = current,
                            Alive13 = current,
                            Alive14 = current,
                            Alive15 = current,
                        },
                    }
                );
            }
            else
            {
                AddComponent(
                    entity,
                    new CurrentCglGroup
                    {
                        Data = new CglGroupData
                        {
                            Alive5 = current,
                        },
                    }
                );
                
            }

            AddComponent(
                entity,
                new NextCglGroup()
                {
                }
            );

            if (authoring.transform.parent == null)
            {
                AddComponent<GroupPosition>(entity);
                return;
            }

            var siblings = authoring.transform.parent.childCount;
            var index = authoring.transform.GetSiblingIndex();

            var width = Mathf.CeilToInt(math.sqrt(siblings));
            var x = index % width;
            var y = index / width;

            AddComponent(
                entity,
                new GroupPosition
                {
                    Position = new int2(x, y) * Constants.GroupTotalEdgeLength,
                }
            );
        }
    }
}