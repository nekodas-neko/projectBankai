using System;

namespace Game.Core
{
    public readonly struct Vec2 : IEquatable<Vec2>
    {
        public static readonly Vec2 Zero = new Vec2(0f, 0f);

        public readonly float X;
        public readonly float Y;

        public Vec2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float SqrLength => X * X + Y * Y;

        public float Length => MathF.Sqrt(SqrLength);

        public Vec2 Normalized
        {
            get
            {
                var length = Length;
                return length > 1e-6f ? new Vec2(X / length, Y / length) : Zero;
            }
        }

        public Vec2 ClampLength(float max)
        {
            var sqr = SqrLength;
            if (sqr <= max * max)
            {
                return this;
            }

            var scale = max / MathF.Sqrt(sqr);
            return new Vec2(X * scale, Y * scale);
        }

        public bool IsFinite => float.IsFinite(X) && float.IsFinite(Y);

        public static float Dot(Vec2 a, Vec2 b) => a.X * b.X + a.Y * b.Y;

        public static float Distance(Vec2 a, Vec2 b) => (a - b).Length;

        public static Vec2 Lerp(Vec2 a, Vec2 b, float t) => new Vec2(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

        public bool Equals(Vec2 other) => X == other.X && Y == other.Y;

        public override bool Equals(object obj) => obj is Vec2 other && Equals(other);

        public override int GetHashCode() => unchecked(X.GetHashCode() * 397 ^ Y.GetHashCode());

        public override string ToString() => $"({X:0.###}, {Y:0.###})";

        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Y + b.Y);

        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Y - b.Y);

        public static Vec2 operator *(Vec2 a, float s) => new Vec2(a.X * s, a.Y * s);

        public static bool operator ==(Vec2 a, Vec2 b) => a.Equals(b);

        public static bool operator !=(Vec2 a, Vec2 b) => !a.Equals(b);
    }
}
