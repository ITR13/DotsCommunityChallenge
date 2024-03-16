using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class CglGroupAuthoring : MonoBehaviour
{
    public enum Spawnable
    {
        None,
        Blink,
        Box,
        Glider,
        TinyBox,
        BigBlock,
        LeftBlock,
        RightBlock,
        TopBlock,
        BotBlock,
        TopLeftBlock,
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
            const ulong leftblock = 0b1111000011110000111100001111000011110000111100001111000011110000;
            const ulong rightblock = ~leftblock;
            const ulong topblock = 0b1111111111111111111111111111111100000000000000000000000000000000;
            const ulong botblock = ~topblock;
            const ulong topleftblock = 0b1111000011110000111100001111000000000000000000000000000000000000;

            var current = authoring.WhatToSpawn switch
            {
                Spawnable.Blink => blink,
                Spawnable.Box => box,
                Spawnable.Glider => glider,
                Spawnable.TinyBox => tinybox,
                Spawnable.BigBlock => bigblock,
                Spawnable.LeftBlock => leftblock,
                Spawnable.RightBlock => rightblock,
                Spawnable.TopBlock => topblock,
                Spawnable.BotBlock => botblock,
                Spawnable.TopLeftBlock => topleftblock,
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

            x -= width / 2;
            y -= width / 2;
            
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