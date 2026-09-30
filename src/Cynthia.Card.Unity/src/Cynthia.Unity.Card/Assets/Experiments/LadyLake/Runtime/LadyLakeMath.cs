using UnityEngine;

namespace LegacyGwent.LadyLakeLab
{
    /// <summary>实验用的纯数学工具（五阶缓动、确定性哈希、绕点旋转）。全部无状态、可复现。</summary>
    public static class LadyLakeMath
    {
        public const float TwoPi = 6.28318530718f;

        /// <summary>五阶平滑插值：首尾一阶、二阶导数都为 0 -> 相位拼接处速度连续。</summary>
        public static float Quintic(float t)
        {
            t = t < 0f ? 0f : (t > 1f ? 1f : t);
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }

        /// <summary>三次平滑插值（首尾一阶导数为 0）。</summary>
        public static float Smooth(float t)
        {
            t = t < 0f ? 0f : (t > 1f ? 1f : t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>把 t 映射到 [a,b] 区间内的 0..1（区间外自动夹紧）。</summary>
        public static float Segment(float t, float a, float b)
        {
            float span = b - a;
            if (span <= 1e-6f) return t < a ? 0f : 1f;
            float k = (t - a) / span;
            return k < 0f ? 0f : (k > 1f ? 1f : k);
        }

        /// <summary>区间内的五阶缓动。</summary>
        public static float Ease(float t, float a, float b)
        {
            return Quintic(Segment(t, a, b));
        }

        public static float Lerp(float a, float b, float k)
        {
            return a + (b - a) * k;
        }

        public static Vector2 Lerp(Vector2 a, Vector2 b, float k)
        {
            return new Vector2(a.x + (b.x - a.x) * k, a.y + (b.y - a.y) * k);
        }

        /// <summary>确定性 0..1 哈希（不依赖 UnityEngine.Random，Editor 与运行时结果一致）。</summary>
        public static float Hash01(int i)
        {
            unchecked
            {
                uint x = (uint)i * 0x9E3779B1u + 0x165667B1u;
                x ^= x >> 15;
                x *= 0x85EBCA6Bu;
                x ^= x >> 13;
                x *= 0xC2B2AE35u;
                x ^= x >> 16;
                return (x & 0x00FFFFFFu) / 16777216f;
            }
        }

        public static float HashRange(int i, float min, float max)
        {
            return min + (max - min) * Hash01(i);
        }

        public static Vector2 HashVector01(int i)
        {
            return new Vector2(Hash01(i * 2 + 1), Hash01(i * 2 + 2));
        }

        /// <summary>绕枢轴旋转（角度制，逆时针为正）。</summary>
        public static Vector2 RotateAround(Vector2 point, Vector2 pivot, float degrees)
        {
            if (Mathf.Abs(degrees) < 1e-6f) return point;
            float r = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(r);
            float s = Mathf.Sin(r);
            float dx = point.x - pivot.x;
            float dy = point.y - pivot.y;
            return new Vector2(pivot.x + dx * c - dy * s, pivot.y + dx * s + dy * c);
        }

        /// <summary>旋转方向向量。</summary>
        public static Vector2 Rotate(Vector2 v, float degrees)
        {
            return RotateAround(v, Vector2.zero, degrees);
        }

        /// <summary>正数取模（用于循环时间）。</summary>
        public static float Wrap(float v, float length)
        {
            if (length <= 1e-6f) return 0f;
            float m = v - Mathf.Floor(v / length) * length;
            return m;
        }

        /// <summary>把归一化 y 映射到 [-0.5, 0.5] 的画面相对高度。</summary>
        public static float Centered(float normalized)
        {
            return normalized - 0.5f;
        }
    }
}
