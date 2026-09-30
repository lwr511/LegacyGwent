using UnityEngine;

namespace LegacyGwent.LadyLakeLab
{
    /// <summary>循环中的语义相位，便于验收时对着截图判断「握住 / 释放」。</summary>
    public enum LadyLakePhase
    {
        Idle = 0,      // 原画姿态待机，剑漂在手左下方
        Reach = 1,     // 仙女伸手
        Summon = 2,    // 剑飞入手中并转到原画角度
        Lift = 3,      // 手与剑共同轻抬
        Hold = 4,      // pause 段：停留发光
        Lower = 5,     // 带手缓缓下放
        Release = 6,   // 松开并回到初始位置
    }

    /// <summary>
    /// 某一时刻的完整姿态快照。SampleAt() 只产出这个结构，
    /// 骨架 / 剑 / 粒子 / 材质全部从它派生，因此 Editor 截图与 Play 完全同源。
    /// </summary>
    public struct LadyLakePose
    {
        public bool valid;

        public float time;            // 循环内时间（秒）
        public float normalizedTime;  // 0..1
        public float loopDuration;    // duration + pause
        public float intensity;

        public LadyLakePhase phase;
        public float phaseProgress;

        /// <summary>0 = 原画姿态（手臂伸直），1 = 完全伸手。</summary>
        public float reach;
        /// <summary>握持强度 0..1；>=1 表示刚性握住（无滑移）。</summary>
        public float hold;
        /// <summary>剑刃 / rune 发光强度 0..1。</summary>
        public float glow;
        /// <summary>剑的运动强度 0..1（用于局部水流与气泡）。</summary>
        public float wake;

        public float upperAngleDeg;   // 上臂绕肩旋转
        public float foreAngleDeg;    // 前臂绕肘旋转

        public Vector2 shoulder;      // 以下均为世界单位（画面中心为原点，y 向上）
        public Vector2 elbow;
        public Vector2 wrist;
        public Vector2 swordGrip;     // 剑的握点（= 原画握点经过当前位移/旋转后）
        public float swordAngleDeg;   // 相对原画角度的附加旋转

        public bool held;

        private static readonly string[] PhaseNames =
        {
            "Idle", "Reach", "Summon", "Lift", "Hold", "Lower", "Release",
        };

        public string PhaseName
        {
            get
            {
                int i = (int)phase;
                return (i >= 0 && i < PhaseNames.Length) ? PhaseNames[i] : "Unknown";
            }
        }

        public override string ToString()
        {
            return string.Format(
                "t={0:F2}s u={1:F3} {2} reach={3:F3} hold={4:F2} glow={5:F2} wake={6:F2} " +
                "wrist=({7:F3},{8:F3}) grip=({9:F3},{10:F3}) angle={11:F2}deg held={12}",
                time, normalizedTime, PhaseName, reach, hold, glow, wake,
                wrist.x, wrist.y, swordGrip.x, swordGrip.y, swordAngleDeg, held);
        }
    }
}
