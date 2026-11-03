using System;
using System.IO;

namespace Game.Core
{
    public static class WireCodec
    {
        public const byte CommandMessage = 1;
        public const byte SnapshotMessage = 2;

        const int CommandSize = 1 + 4 + 1 + 8;
        const int SnapshotHeaderSize = 1 + 4 + 4 + 1 + 1 + 1 + 4 + 2;
        public const int EntityBytes = EntitySize;

        const int EntitySize = 4 + 4 + 1 + 4 + 8 + 8 + 4 + 4 + 20 + 4 + 4 + 4 + 4 + 4 + 4 + 1 + 1 + 4;
        const int EventSize = 4 + 4 + 4 + 1 + 4 + 4 + 1;
        const int UseSize = 4 + 1 + 4 + 4 + 8;
        const int WindupSize = 4 + 4 + 4 + 8 + 8 + 4;
        const int MaxEntities = 1024;
        const int MaxEvents = 1024;

        public static byte[] Encode(in Command command)
        {
            using var stream = new MemoryStream(CommandSize);
            using var writer = new BinaryWriter(stream);
            writer.Write(CommandMessage);
            writer.Write(command.Sequence);
            writer.Write((byte)command.Type);
            Write(writer, command.Aim);
            return stream.ToArray();
        }

        public static bool TryDecode(byte[] data, out Command command)
        {
            command = default;
            if (data == null || data.Length != CommandSize || data[0] != CommandMessage)
            {
                return false;
            }

            using var stream = new MemoryStream(data);
            using var reader = new BinaryReader(stream);
            reader.ReadByte();
            var sequence = reader.ReadInt32();
            var type = reader.ReadByte();
            var aim = ReadVec2(reader);

            if (type < (byte)CommandType.Move || type > (byte)CommandType.Restart || !aim.IsFinite)
            {
                return false;
            }

            command = new Command(PlayerId.None, sequence, (CommandType)type, aim);
            return true;
        }

        public static int EncodedSize(Snapshot snapshot)
        {
            return SnapshotHeaderSize + snapshot.Entities.Count * EntitySize + 2 + snapshot.Events.Count * EventSize + 2 + snapshot.Uses.Count * UseSize + 2 + snapshot.Windups.Count * WindupSize;
        }

        public static byte[] Encode(Snapshot snapshot)
        {
            var size = EncodedSize(snapshot);
            using var stream = new MemoryStream(size);
            using var writer = new BinaryWriter(stream);
            writer.Write(SnapshotMessage);
            writer.Write(snapshot.Tick);
            writer.Write(snapshot.AckedSequence);
            writer.Write(snapshot.Room);
            writer.Write(snapshot.RoomCount);
            writer.Write((byte)snapshot.Dungeon);
            writer.Write(snapshot.DungeonTimer);
            writer.Write((ushort)snapshot.Entities.Count);
            for (var i = 0; i < snapshot.Entities.Count; i++)
            {
                var e = snapshot.Entities[i];
                writer.Write(e.Id.Value);
                writer.Write(e.Owner.Value);
                writer.Write((byte)e.Team);
                writer.Write(e.Appearance);
                Write(writer, e.Position);
                Write(writer, e.Facing);
                writer.Write(e.Health);
                writer.Write(e.MaxHealth);
                writer.Write(e.Cooldown0);
                writer.Write(e.Cooldown1);
                writer.Write(e.Cooldown2);
                writer.Write(e.Cooldown3);
                writer.Write(e.Cooldown4);
                writer.Write(e.Resource);
                writer.Write(e.MaxResource);
                writer.Write(e.ResourceId);
                writer.Write(e.Charge);
                writer.Write(e.ChargeMax);
                writer.Write(e.Target.Value);
                writer.Write(e.Effects);
                writer.Write(e.AggroQuarters);
                writer.Write(e.ClassHash);
            }

            writer.Write((ushort)snapshot.Events.Count);
            for (var i = 0; i < snapshot.Events.Count; i++)
            {
                var ev = snapshot.Events[i];
                writer.Write(ev.Source.Value);
                writer.Write(ev.Target.Value);
                writer.Write(ev.Amount);
                writer.Write(ev.Slot);
                writer.Write(ev.Ability);
                writer.Write(ev.Variant);
                writer.Write((byte)ev.Outcome);
            }

            writer.Write((ushort)snapshot.Uses.Count);
            for (var i = 0; i < snapshot.Uses.Count; i++)
            {
                var use = snapshot.Uses[i];
                writer.Write(use.User.Value);
                writer.Write(use.Slot);
                writer.Write(use.Ability);
                writer.Write(use.Variant);
                Write(writer, use.Direction);
            }

            writer.Write((ushort)snapshot.Windups.Count);
            for (var i = 0; i < snapshot.Windups.Count; i++)
            {
                var windup = snapshot.Windups[i];
                writer.Write(windup.User.Value);
                writer.Write(windup.Ability);
                writer.Write(windup.Variant);
                Write(writer, windup.Origin);
                Write(writer, windup.Direction);
                writer.Write(windup.Remaining);
            }

            return stream.ToArray();
        }

        public static bool TryDecode(byte[] data, out Snapshot snapshot)
        {
            snapshot = null;
            if (data == null || data.Length < SnapshotHeaderSize || data[0] != SnapshotMessage)
            {
                return false;
            }

            using var stream = new MemoryStream(data);
            using var reader = new BinaryReader(stream);
            reader.ReadByte();
            var result = new Snapshot
            {
                Tick = reader.ReadInt32(),
                AckedSequence = reader.ReadInt32(),
                Room = reader.ReadByte(),
                RoomCount = reader.ReadByte(),
                Dungeon = (DungeonPhase)reader.ReadByte(),
                DungeonTimer = reader.ReadSingle(),
            };

            int entityCount = reader.ReadUInt16();
            if (entityCount > MaxEntities || Remaining(stream) < entityCount * EntitySize + 4)
            {
                return false;
            }

            for (var i = 0; i < entityCount; i++)
            {
                result.Entities.Add(new EntityState
                {
                    Id = new EntityId(reader.ReadInt32()),
                    Owner = new PlayerId(reader.ReadInt32()),
                    Team = (Team)reader.ReadByte(),
                    Appearance = reader.ReadInt32(),
                    Position = ReadVec2(reader),
                    Facing = ReadVec2(reader),
                    Health = reader.ReadSingle(),
                    MaxHealth = reader.ReadSingle(),
                    Cooldown0 = reader.ReadSingle(),
                    Cooldown1 = reader.ReadSingle(),
                    Cooldown2 = reader.ReadSingle(),
                    Cooldown3 = reader.ReadSingle(),
                    Cooldown4 = reader.ReadSingle(),
                    Resource = reader.ReadSingle(),
                    MaxResource = reader.ReadSingle(),
                    ResourceId = reader.ReadInt32(),
                    Charge = reader.ReadSingle(),
                    ChargeMax = reader.ReadSingle(),
                    Target = new EntityId(reader.ReadInt32()),
                    Effects = reader.ReadByte(),
                    AggroQuarters = reader.ReadByte(),
                    ClassHash = reader.ReadInt32(),
                });
            }

            int eventCount = reader.ReadUInt16();
            if (eventCount > MaxEvents || Remaining(stream) < eventCount * EventSize + 2)
            {
                return false;
            }

            for (var i = 0; i < eventCount; i++)
            {
                result.Events.Add(new DamageEvent
                {
                    Source = new EntityId(reader.ReadInt32()),
                    Target = new EntityId(reader.ReadInt32()),
                    Amount = reader.ReadSingle(),
                    Slot = reader.ReadByte(),
                    Ability = reader.ReadInt32(),
                    Variant = reader.ReadInt32(),
                    Outcome = (HitOutcome)reader.ReadByte(),
                });
            }

            int useCount = reader.ReadUInt16();
            if (useCount > MaxEvents || Remaining(stream) < useCount * UseSize + 2)
            {
                return false;
            }

            for (var i = 0; i < useCount; i++)
            {
                result.Uses.Add(new UseEvent
                {
                    User = new EntityId(reader.ReadInt32()),
                    Slot = reader.ReadByte(),
                    Ability = reader.ReadInt32(),
                    Variant = reader.ReadInt32(),
                    Direction = ReadVec2(reader),
                });
            }

            int windupCount = reader.ReadUInt16();
            if (windupCount > MaxEvents || Remaining(stream) != windupCount * WindupSize)
            {
                return false;
            }

            for (var i = 0; i < windupCount; i++)
            {
                result.Windups.Add(new WindupState
                {
                    User = new EntityId(reader.ReadInt32()),
                    Ability = reader.ReadInt32(),
                    Variant = reader.ReadInt32(),
                    Origin = ReadVec2(reader),
                    Direction = ReadVec2(reader),
                    Remaining = reader.ReadSingle(),
                });
            }

            snapshot = result;
            return true;
        }

        static long Remaining(Stream stream) => stream.Length - stream.Position;

        static void Write(BinaryWriter writer, Vec2 value)
        {
            writer.Write(value.X);
            writer.Write(value.Y);
        }

        static Vec2 ReadVec2(BinaryReader reader)
        {
            var x = reader.ReadSingle();
            var y = reader.ReadSingle();
            return new Vec2(x, y);
        }
    }
}
