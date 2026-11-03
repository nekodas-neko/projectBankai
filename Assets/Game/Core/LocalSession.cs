using System.Collections.Generic;

namespace Game.Core
{
    public sealed class LocalSession
    {
        readonly Registry<ClassDef> classes;
        readonly IRandom random;
        readonly ClientConfig clientConfig;
        readonly List<LoopbackLink> links = new List<LoopbackLink>();
        readonly List<GameClient> clients = new List<GameClient>();

        public LocalSession(
            ServerConfig serverConfig,
            Registry<ClassDef> classes,
            Registry<AbilityDef> abilities,
            ulong seed,
            NetworkConditions conditions = default)
        {
            this.classes = classes;
            Conditions = conditions;
            random = new SeededRandom(seed);
            Server = new GameServer(serverConfig, classes, abilities, random);
            clientConfig = new ClientConfig { Simulation = serverConfig.Simulation };
        }

        public GameServer Server { get; }

        public NetworkConditions Conditions { get; private set; }

        public IReadOnlyList<GameClient> Clients => clients;

        public GameClient AddPlayer(PlayerId player, string classId, int appearance = 0, int[] variants = null, float weaponPower = 1f)
        {
            var link = new LoopbackLink(random, Conditions);
            Server.AddPlayer(player, classId, link.ServerEnd, appearance, variants, weaponPower);
            var client = new GameClient(player, link.ClientEnd, classes.Get(classId), clientConfig);
            links.Add(link);
            clients.Add(client);
            return client;
        }

        // Starts a dungeon for everyone in the session; its map (if it has one) is shared with the clients so their prediction matches the server's walls.
        public void StartDungeon(DungeonDef dungeon)
        {
            Server.StartDungeon(dungeon);
            clientConfig.Simulation = Server.Simulation;
        }

        public void SetConditions(NetworkConditions conditions)
        {
            Conditions = conditions;
            for (var i = 0; i < links.Count; i++)
            {
                links[i].Conditions = conditions;
            }
        }

        public void Advance(float dt)
        {
            for (var i = 0; i < links.Count; i++)
            {
                links[i].Advance(dt);
            }

            Server.Update(dt);

            for (var i = 0; i < clients.Count; i++)
            {
                clients[i].Update(dt);
            }
        }

        public void Advance(float seconds, float step)
        {
            var steps = (int)(seconds / step + 0.5f);
            for (var i = 0; i < steps; i++)
            {
                Advance(step);
            }
        }
    }
}
