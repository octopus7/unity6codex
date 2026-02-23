namespace TopdownShooter.Server.Domain;

public readonly struct Vector2f
{
    public float X { get; }
    public float Y { get; }

    public Vector2f(float x, float y)
    {
        X = x;
        Y = y;
    }

    public float LengthSquared => (X * X) + (Y * Y);

    public Vector2f Normalized()
    {
        var lengthSq = LengthSquared;
        if (lengthSq <= 0.000001f)
        {
            return new Vector2f(0f, 0f);
        }

        var invLength = 1f / MathF.Sqrt(lengthSq);
        return new Vector2f(X * invLength, Y * invLength);
    }

    public static Vector2f operator +(Vector2f left, Vector2f right)
    {
        return new Vector2f(left.X + right.X, left.Y + right.Y);
    }

    public static Vector2f operator -(Vector2f left, Vector2f right)
    {
        return new Vector2f(left.X - right.X, left.Y - right.Y);
    }

    public static Vector2f operator *(Vector2f value, float scalar)
    {
        return new Vector2f(value.X * scalar, value.Y * scalar);
    }

    public static float DistanceSquared(Vector2f a, Vector2f b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return (dx * dx) + (dy * dy);
    }
}
