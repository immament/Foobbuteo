// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (C) 2024–2026 Dariusz Juńczyk

using UnityEngine;

namespace Imma.Foobbuteo.Shared.Game
{
    public readonly struct Bounds2d
    {
        public Vector2 Min { get; }
        public Vector2 Max { get; }
        public Vector2 Center { get; }

        public Bounds2d(Vector2 min, Vector2 max)
        {
            Min = min;
            Max = max;
            Center = (Min + Max) / 2f;
        }

        public Bounds2d(float minX, float minY, float maxX, float maxY)
            : this(new(minX, minY), new(maxX, maxY)) { }

        public bool Contains(Vector2 point) =>
            Min.x <= point.x && Min.y <= point.y && point.x <= Max.x && point.y <= Max.y;

        // [HideInCallstack]
        public bool ContainsExclusive(Vector2 point)
        {
            // this.LDebug($"{point}, {this}");
            return Min.x < point.x && Min.y < point.y && point.x < Max.x && point.y < Max.y;
        }

        public Vector2 Clamp(Vector2 point)
        {
            return Vector2.Max(Min, Vector2.Min(point, Max));
        }

        public Vector2 GetFarthestCorner(Vector2 origin)
        {
            Vector2 farthest = Min;
            float farthestDistance = (farthest - origin).sqrMagnitude;
            CheckCorner(new Vector2(Min.x, Max.y));
            CheckCorner(new Vector2(Max.x, Min.y));
            CheckCorner(Max);
            return farthest;

            void CheckCorner(Vector2 corner)
            {
                float distance = (corner - origin).sqrMagnitude;
                if (distance > farthestDistance)
                {
                    farthest = corner;
                    farthestDistance = distance;
                }
            }
        }

        public bool TryGetPointInDistanceRange(
            Vector2 origin,
            float minimumDistance,
            float maximumDistance,
            out Vector2 point
        )
        {
            point = default;
            if (maximumDistance < minimumDistance)
                return false;

            Vector2 nearest = Clamp(origin);
            Vector2 farthest = GetFarthestCorner(origin);
            float overlapMin = Mathf.Max(minimumDistance, Vector2.Distance(origin, nearest));
            float overlapMax = Mathf.Min(maximumDistance, Vector2.Distance(origin, farthest));
            if (overlapMin > overlapMax)
                return false;

            float centerDistance = Vector2.Distance(origin, Center);
            if (centerDistance >= minimumDistance && centerDistance <= maximumDistance)
            {
                point = Center;
                return true;
            }

            float targetDistance = (overlapMin + overlapMax) * 0.5f;
            float low = 0f;
            float high = 1f;
            for (int i = 0; i < 24; i++)
            {
                float middle = (low + high) * 0.5f;
                Vector2 candidate = Vector2.Lerp(nearest, farthest, middle);
                if (Vector2.Distance(origin, candidate) < targetDistance)
                    low = middle;
                else
                    high = middle;
            }

            point = Vector2.Lerp(nearest, farthest, (low + high) * 0.5f);
            return true;
        }

        public override string ToString()
        {
            return $"(min: {Min}, max: {Max})";
        }

        // public readonly Vector3 Center3 => new(Center.x, 0f, Center.y);
        public readonly Vector2 Size => new(Mathf.Abs(Max.x - Min.x), Mathf.Abs(Max.y - Min.y));
        // public readonly Vector3 Size3 => new(Mathf.Abs(Max.x - Min.x), 0f, Mathf.Abs(Max.y - Min.y));
    }
}
