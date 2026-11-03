using System;
using System.Linq;
using Game.Core;
using UnityEngine;

namespace Game.Client
{
    [Serializable]
    public class SpawnData
    {
        public ClassAsset spawnClass;
        public Vector2 position;
        public string skin = "";
        public bool boss;
        [Min(0)] public int pack;
    }

    [Serializable]
    public class ZoneData
    {
        public string name = "";
        public Vector2 min;
        public Vector2 max;
    }

    [Serializable]
    public class GateData
    {
        public string name = "";
        public Vector2 min;
        public Vector2 max;
        [Min(0)] public int openAfter;
    }

    [Serializable]
    public class RoomData
    {
        public string name = "";
        public Vector2 offset;
        public SpawnData[] spawns = new SpawnData[0];
    }

    [CreateAssetMenu(menuName = "Game/Dungeon", fileName = "new_dungeon")]
    public class DungeonAsset : DefinitionAsset
    {
        [Min(0f)] public float clearDelaySeconds = 3f;
        [Min(0f)] public float healthPerExtraPlayer;
        public RoomData[] rooms = new RoomData[0];
        public ZoneData[] zones = new ZoneData[0];
        public GateData[] gates = new GateData[0];

        public DungeonDef ToDefinition()
        {
            return new DungeonDef
            {
                Id = Id,
                ClearDelaySeconds = clearDelaySeconds,
                HealthPerExtraPlayer = healthPerExtraPlayer,
                Zones = zones.Select(zone => new ZoneDef { Name = zone.name, MinX = zone.min.x, MinY = zone.min.y, MaxX = zone.max.x, MaxY = zone.max.y }).ToArray(),
                Gates = gates.Select(gate => new GateDef { Name = gate.name, MinX = gate.min.x, MinY = gate.min.y, MaxX = gate.max.x, MaxY = gate.max.y, OpenAfter = gate.openAfter }).ToArray(),
                Rooms = rooms.Select(room => new RoomDef
                {
                    Name = room.name,
                    OffsetX = room.offset.x,
                    OffsetY = room.offset.y,
                    Spawns = room.spawns.Where(spawn => spawn.spawnClass != null).Select(spawn => new SpawnDef
                    {
                        Class = spawn.spawnClass.Id,
                        X = spawn.position.x,
                        Y = spawn.position.y,
                        Skin = spawn.skin,
                        Boss = spawn.boss,
                        Pack = spawn.pack,
                    }).ToArray(),
                }).ToArray(),
            };
        }
    }
}
