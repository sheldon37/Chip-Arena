using System;

namespace ChipArena.UnityConversion
{
    [Serializable]
    public struct V3
    {
        public float x, y, z;
        public V3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public float Length { get { return (float)Math.Sqrt(x * x + y * y + z * z); } }
        public V3 Normalized { get { float l = Length; return l > .00001f ? this / l : new V3(); } }
        public static V3 operator +(V3 a, V3 b) { return new V3(a.x + b.x, a.y + b.y, a.z + b.z); }
        public static V3 operator -(V3 a, V3 b) { return new V3(a.x - b.x, a.y - b.y, a.z - b.z); }
        public static V3 operator *(V3 a, float b) { return new V3(a.x * b, a.y * b, a.z * b); }
        public static V3 operator /(V3 a, float b) { return a * (1f / b); }
        public static float Dot(V3 a, V3 b) { return a.x * b.x + a.y * b.y + a.z * b.z; }
        public static V3 Forward(float yaw) { double r = yaw * Math.PI / 180; return new V3((float)Math.Sin(r), 0, (float)Math.Cos(r)); }
    }

    public static class MathUtil
    {
        public static float Clamp(float value, float lo, float hi) { return Math.Max(lo, Math.Min(hi, value)); }
        public static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        public static float Smooth(float t) { t = Clamp(t, 0, 1); return t * t * (3 - 2 * t); }
        public static float Approach(float a, float b, float amount) { return a + Clamp(b - a, -amount, amount); }

        public static bool SegmentSphere(V3 start, V3 end, V3 center, float radius, out float t)
        {
            V3 d = end - start, m = start - center;
            float a = V3.Dot(d, d), c = V3.Dot(m, m) - radius * radius;
            t = 0;
            if (c <= 0) return true;
            if (a < .000001f) return false;
            float b = V3.Dot(m, d), disc = b * b - a * c;
            if (disc < 0) return false;
            t = (-b - (float)Math.Sqrt(disc)) / a;
            return t >= 0 && t <= 1;
        }

        public static bool SegmentBox(V3 start, V3 end, Cover box, float padding, out float t)
        {
            float lo = 0, hi = 1;
            V3 d = end - start;
            bool hit = Slab(start.x, d.x, box.Position.x - box.HalfX - padding, box.Position.x + box.HalfX + padding, ref lo, ref hi)
                && Slab(start.y, d.y, -padding, box.Height + padding, ref lo, ref hi)
                && Slab(start.z, d.z, box.Position.z - box.HalfZ - padding, box.Position.z + box.HalfZ + padding, ref lo, ref hi);
            t = lo; return hit;
        }

        private static bool Slab(float start, float delta, float min, float max, ref float lo, ref float hi)
        {
            if (Math.Abs(delta) < .000001f) return start >= min && start <= max;
            float a = (min - start) / delta, b = (max - start) / delta;
            if (a > b) { float swap = a; a = b; b = swap; }
            lo = Math.Max(lo, a); hi = Math.Min(hi, b); return lo <= hi;
        }
    }
}
