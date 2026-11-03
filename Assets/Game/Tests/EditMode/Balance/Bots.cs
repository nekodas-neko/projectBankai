using System;
using System.Collections.Generic;

namespace Game.Core.Tests
{
    // What a bot is allowed to know: the same things a player sees on screen (the latest snapshot, including the
    // warnings for attacks that are winding up) plus the definitions, so it can work out where a warning lands.
    public sealed class BotView
    {
        readonly Dictionary<int, AbilityDef> byId = new Dictionary<int, AbilityDef>();

        public BotView(GameClient client, Registry<AbilityDef> abilities, ClassDef kit)
        {
            Client = client;
            Abilities = abilities;
            Kit = kit;
            foreach (var ability in abilities.All)
            {
                byId[StableId.From(ability.Id)] = ability;
            }
        }

        public GameClient Client { get; }

        public Registry<AbilityDef> Abilities { get; }

        public ClassDef Kit { get; }

        public float ArenaRadius => SimulationSettings.Default.ArenaRadius;

        // How big the thing being fought is, so a melee bot knows how close it has to stand.
        public float EnemyRadius { get; set; } = 1f;

        public bool TryMe(out EntityState me)
        {
            return Client.TryGetLocalState(out me) && me.Health > 0f;
        }

        public AbilityDef Ability(string id)
        {
            return id != null && Abilities.TryGet(id, out var ability) ? ability : null;
        }

        public AbilityDef Ability(int stableId)
        {
            return byId.TryGetValue(stableId, out var ability) ? ability : null;
        }

        public bool TryNearestEnemy(Vec2 from, out EntityState enemy)
        {
            enemy = default;
            var found = false;
            var best = float.MaxValue;
            var entities = Client.Latest?.Entities;
            for (var i = 0; entities != null && i < entities.Count; i++)
            {
                var candidate = entities[i];
                if (candidate.Team != Team.Enemies || candidate.Health <= 0f)
                {
                    continue;
                }

                var distance = (candidate.Position - from).SqrLength;
                if (distance < best)
                {
                    best = distance;
                    enemy = candidate;
                    found = true;
                }
            }

            return found;
        }
    }

    public interface IBotPolicy
    {
        string Name { get; }

        void Step(BotView view, float dt);
    }

    // The shared parts: pressing the buttons a player would press.
    public abstract class BotBase : IBotPolicy
    {
        // How long after a warning appears a human can start to react.
        protected const float ReactionSeconds = 0.4f;

        const int Directions = 16;
        const float EscapeMargin = 0.8f;

        public abstract string Name { get; }

        public abstract void Step(BotView view, float dt);

        protected static void Fire(BotView view, in EntityState me, bool useAbility)
        {
            var kit = view.Kit;
            if (me.GetCooldown(0) <= 0f)
            {
                view.Client.Attack(Vec2.Zero);
            }

            if (kit.Skills.Length > Entity.UltimateSlot - 1 && me.ChargeMax > 0f && me.Charge >= me.ChargeMax && me.GetCooldown(Entity.UltimateSlot) <= 0f)
            {
                view.Client.Skill(Entity.UltimateSlot, Vec2.Zero);
            }

            if (useAbility && kit.Skills.Length > 0 && me.GetCooldown(1) <= 0f)
            {
                var ability = view.Ability(kit.Skills[0]);
                if (ability != null && (ability.ResourceCost <= 0f || me.Resource >= ability.ResourceCost))
                {
                    view.Client.Skill(1, Vec2.Zero);
                }
            }
        }

        protected static float AttackReach(BotView view)
        {
            var attack = view.Ability(view.Kit.AutoAttack);
            return attack != null ? HitAreas.Reach(attack) : 3f;
        }

        // Warnings that will land on a spot, with how long until they do.
        protected static bool Threatens(BotView view, in WindupState windup, Vec2 point, float margin)
        {
            var ability = view.Ability(windup.Ability);
            return ability != null && HitAreas.Contains(ability, windup.Origin, windup.Direction, point, margin);
        }

        // Looks for a direction to run in that leaves every warning that threatens the bot before it lands.
        // With onlyUnavoidable set, orange warnings (that block or dodge can answer) are ignored.
        protected static bool TryEscape(BotView view, in EntityState me, float speed, Vec2 enemyPosition, float desired, IReadOnlyList<WindupState> windups, bool onlyUnavoidable, out Vec2 best)
        {
            best = Vec2.Zero;
            var bestScore = float.MaxValue;
            var found = false;
            for (var d = 0; d < Directions; d++)
            {
                var angle = d * 2f * MathF.PI / Directions;
                var direction = new Vec2(MathF.Cos(angle), MathF.Sin(angle));
                var safe = true;
                var latest = 0f;
                for (var i = 0; i < windups.Count && safe; i++)
                {
                    if (!Counts(view, windups[i], onlyUnavoidable) || !Noticed(view, windups[i]) || !Threatens(view, windups[i], me.Position, EscapeMargin))
                    {
                        continue;
                    }

                    var travel = windups[i].Remaining * speed;
                    var end = (me.Position + direction * travel).ClampLength(view.ArenaRadius);
                    safe = !Threatens(view, windups[i], end, EscapeMargin);
                    latest = Math.Max(latest, windups[i].Remaining);
                }

                if (!safe)
                {
                    continue;
                }

                var endPoint = (me.Position + direction * (speed * latest)).ClampLength(view.ArenaRadius);
                var score = Math.Abs((endPoint - enemyPosition).Length - desired);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = direction;
                    found = true;
                }
            }

            return found;
        }

        // A person needs a moment to see a new warning; the bot starts to react only after that long.
        protected static bool Noticed(BotView view, in WindupState windup)
        {
            var ability = view.Ability(windup.Ability);
            return ability != null && ability.Windup - windup.Remaining >= ReactionSeconds - 0.001f;
        }

        protected static bool Counts(BotView view, in WindupState windup, bool onlyUnavoidable)
        {
            if (!onlyUnavoidable)
            {
                return true;
            }

            var ability = view.Ability(windup.Ability);
            return ability != null && !ability.Avoidable;
        }

        protected static bool AnyThreat(BotView view, IReadOnlyList<WindupState> windups, Vec2 point, bool onlyUnavoidable)
        {
            for (var i = 0; i < windups.Count; i++)
            {
                if (Counts(view, windups[i], onlyUnavoidable) && Noticed(view, windups[i]) && Threatens(view, windups[i], point, EscapeMargin))
                {
                    return true;
                }
            }

            return false;
        }

        protected static Vec2 Toward(Vec2 from, Vec2 to)
        {
            var offset = to - from;
            return offset.SqrLength > 1e-6f ? offset.Normalized : Vec2.Zero;
        }
    }

    // The control: it never moves and never defends. It is what a player who is not paying attention does.
    public sealed class StandStill : BotBase
    {
        public override string Name => "stand still";

        public override void Step(BotView view, float dt)
        {
            view.Client.SetMoveInput(Vec2.Zero);
            if (view.TryMe(out var me))
            {
                Fire(view, me, true);
            }
        }
    }

    // How the tank is meant to play: walk up, then stay put, attack, and use the defensive skill on the warnings that land on it.
    public sealed class TankBlocker : BotBase
    {
        public override string Name => "stand and block";

        public override void Step(BotView view, float dt)
        {
            if (!view.TryMe(out var me))
            {
                return;
            }

            var move = Vec2.Zero;
            var closeEnough = AttackReach(view) * 0.7f + view.EnemyRadius;
            var hasEnemy = view.TryNearestEnemy(me.Position, out var enemy);
            if (hasEnemy && (enemy.Position - me.Position).Length > closeEnough)
            {
                move = Toward(me.Position, enemy.Position);
            }

            // Red warnings cannot be blocked: leave the area, then come back.
            var windups = view.Client.Latest.Windups;
            if (hasEnemy && AnyThreat(view, windups, me.Position, true) && TryEscape(view, me, view.Kit.Stats.MoveSpeed, enemy.Position, closeEnough + 2f, windups, true, out var away))
            {
                move = away;
            }

            view.Client.SetMoveInput(move);
            Fire(view, me, true);

            var defense = view.Ability(view.Kit.Defense);
            if (defense == null || me.GetCooldown(Entity.DefenseSlot) > 0f)
            {
                return;
            }

            for (var i = 0; i < windups.Count; i++)
            {
                var lead = Math.Max(0.1f, defense.Duration * 0.8f);
                var ability = view.Ability(windups[i].Ability);
                if (ability != null && ability.Avoidable && windups[i].Remaining <= lead && Threatens(view, windups[i], me.Position, 0.5f))
                {
                    view.Client.Defend(Vec2.Zero);
                    return;
                }
            }
        }
    }

    // How a ranged weapon is meant to play: keep a distance, keep attacking, and step out of every warning in time.
    public sealed class Kiter : BotBase
    {
        public override string Name => "kite";

        public override void Step(BotView view, float dt)
        {
            if (!view.TryMe(out var me))
            {
                return;
            }

            var speed = view.Kit.Stats.MoveSpeed;
            var desired = Math.Min(AttackReach(view) * 0.8f, 9f);
            var enemyPosition = me.Position;
            var hasEnemy = view.TryNearestEnemy(me.Position, out var enemy);
            if (hasEnemy)
            {
                enemyPosition = enemy.Position;
            }

            var move = Vec2.Zero;
            if (hasEnemy)
            {
                var distance = (enemy.Position - me.Position).Length;
                if (distance < desired - 1f)
                {
                    move = Toward(enemy.Position, me.Position);
                }
                else if (distance > desired + 0.5f)
                {
                    move = Toward(me.Position, enemy.Position);
                }
            }

            var windups = view.Client.Latest.Windups;
            var threatened = AnyThreat(view, windups, me.Position, false);

            var defended = false;
            if (threatened && !TryEscape(view, me, speed, enemyPosition, desired, windups, false, out move))
            {
                var defense = view.Ability(view.Kit.Defense);
                if (defense != null && me.GetCooldown(Entity.DefenseSlot) <= 0f)
                {
                    view.Client.Defend(Toward(me.Position, enemyPosition) * -1f);
                    defended = true;
                }
            }

            view.Client.SetMoveInput(move);
            if (!defended)
            {
                Fire(view, me, true);
            }
        }

    }
}
