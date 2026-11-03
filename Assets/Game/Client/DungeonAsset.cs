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
    public class RoomData
    {
        public string name = "";
        public SpawnData[] spawns = new SpawnData[0];
    }

    [CreateAssetMenu(menuName = "Game/Dungeon", fileName = "new_dungeon")]
    public class DungeonAsset : DefinitionAsset
    {
        [Min(0f)] public float clearDelaySeconds = 3f;
        public RoomData[] rooms = new RoomData[0];

        public DungeonDef ToDefinition()
        {
            return new DungeonDef
            {
                Id = Id,
                ClearDelaySeconds = clearDelaySeconds,
                Rooms = rooms.Select(room => new RoomDef
                {
                    Name = room.name,
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
