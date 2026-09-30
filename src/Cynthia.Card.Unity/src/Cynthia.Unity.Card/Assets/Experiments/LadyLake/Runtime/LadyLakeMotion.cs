using UnityEngine;

namespace LegacyGwent.LadyLakeLab
{
    /// <summary>
    /// 循环动作曲线。约 11 秒（+ pause）C1 平滑循环：
    ///
    ///   u: 0.00 ─ 0.16 ─ 0.44 ─ 0.62 ─ 0.80 ─(pause)─ 0.92 ─ 1.00
    ///      Idle   Reach   Summon  Lift    Hold        Lower   Release
    ///
    /// 首尾都落在「原画姿态 + 剑漂在手左下方」这同一个姿态上，且每个相位端点都使用
    /// 五阶缓动（一阶/二阶导数在端点均为 0），因此 u=0 与 u=1 处位置与速度完全连续。
    /// 握住窗口（summon 结束 ~ lower 结束）内剑由手部前臂骨矩阵刚性驱动，握点距离恒为 0。
    /// </summary>
    public static class LadyLakeMotion
    {
        // 相位分割点（行动时长的归一化值）
        public const float IdleEnd = LadyLakeLabConfig.PhaseIdleEnd;
        public const float ReachEnd = LadyLakeLabConfig.PhaseReachEnd;
        public const float SummonEnd = LadyLakeLabConfig.PhaseSummonEnd;
        public const float LiftEnd = LadyLakeLabConfig.PhaseLiftEnd;
        public const float LowerEnd = LadyLakeLabConfig.PhaseLowerEnd;

        private const float WakeWindow = 0.6f; // 有限差分半宽（u）

        private struct CoreState
        {
            public float upperAngleDeg;
            public float foreAngleDeg;
            public float reach;
            public float hold;
            public float glow;
            public float swordAttach;
            public LadyLakePhase phase;
            public float phaseProgress;
        }

        /// <summary>姿态核心（不依赖 wake，便于有限差分求运动强度）。</summary>
        private static CoreState EvaluateCore(float u)
        {
            u = u < 0f ? 0f : (u > 1f ? 1f : u);
            CoreState s;
            s.upperAngleDeg = 0f;
            s.foreAngleDeg = 0f;
            s.reach = 0f;
            s.hold = 0f;
            s.glow = LadyLakeLabConfig.GlowIdle;
            s.swordAttach = 0f;
            s.phaseProgress = 0f;

            if (u <= IdleEnd)
            {
                s.phase = LadyLakePhase.Idle;
                s.phaseProgress = LadyLakeMath.Segment(u, 0f, IdleEnd);
            }
            else if (u <= ReachEnd)
            {
                s.phase = LadyLakePhase.Reach;
                s.phaseProgress = LadyLakeMath.Segment(u, IdleEnd, ReachEnd);
                float k = LadyLakeMath.Quintic(s.phaseProgress);
                s.upperAngleDeg = LadyLakeMath.Lerp(0f, LadyLakeLabConfig.ReachUpperAngleDeg, k);
                s.reach = k;
                s.glow = LadyLakeLabConfig.GlowIdle;
            }
            else if (u <= SummonEnd)
            {
                s.phase = LadyLakePhase.Summon;
                s.phaseProgress = LadyLakeMath.Segment(u, ReachEnd, SummonEnd);
                float k = LadyLakeMath.Quintic(s.phaseProgress);
                s.upperAngleDeg = LadyLakeLabConfig.ReachUpperAngleDeg;
                s.reach = 1f;
                s.swordAttach = k;
                s.hold = k;
                s.glow = LadyLakeMath.Lerp(LadyLakeLabConfig.GlowIdle, LadyLakeLabConfig.GlowSummon, k);
            }
            else if (u <= LiftEnd)
            {
                s.phase = LadyLakePhase.Lift;
                s.phaseProgress = LadyLakeMath.Segment(u, SummonEnd, LiftEnd);
                float k = LadyLakeMath.Quintic(s.phaseProgress);
                s.upperAngleDeg = LadyLakeMath.Lerp(LadyLakeLabConfig.ReachUpperAngleDeg, LadyLakeLabConfig.LiftUpperAngleDeg, k);
                s.foreAngleDeg = LadyLakeMath.Lerp(0f, LadyLakeLabConfig.LiftForeAngleDeg, k);
                s.reach = 1f - k;
                s.swordAttach = 1f;
                s.hold = 1f;
                s.glow = LadyLakeMath.Lerp(LadyLakeLabConfig.GlowSummon, LadyLakeLabConfig.GlowPeak, k);
            }
            else if (u <= LowerEnd)
            {
                s.phase = LadyLakePhase.Lower;
                s.phaseProgress = LadyLakeMath.Segment(u, LiftEnd, LowerEnd);
                float k = LadyLakeMath.Quintic(s.phaseProgress);
                s.upperAngleDeg = LadyLakeMath.Lerp(LadyLakeLabConfig.LiftUpperAngleDeg, LadyLakeLabConfig.ReachUpperAngleDeg, k);
                s.foreAngleDeg = LadyLakeMath.Lerp(LadyLakeLabConfig.LiftForeAngleDeg, 0f, k);
                s.reach = 0f;
                s.swordAttach = 1f;
                s.hold = 1f;
                s.glow = LadyLakeMath.Lerp(LadyLakeLabConfig.GlowPeak, LadyLakeLabConfig.GlowLower, k);
            }
            else
            {
                s.phase = LadyLakePhase.Release;
                s.phaseProgress = LadyLakeMath.Segment(u, LowerEnd, 1f);
                float k = LadyLakeMath.Quintic(s.phaseProgress);
                s.upperAngleDeg = LadyLakeMath.Lerp(LadyLakeLabConfig.ReachUpperAngleDeg, 0f, k);
                s.foreAngleDeg = 0f;
                s.reach = 0f;
                s.swordAttach = 1f - k;
                s.hold = 1f - k;
                s.glow = LadyLakeMath.Lerp(LadyLakeLabConfig.GlowLower, LadyLakeLabConfig.GlowIdle, k);
            }

            return s;
        }

        /// <summary>上臂 + 前臂两骨骼正向运动学（长度恒定，不会拉伸原画手臂）。</summary>
        public static Vector2 ForwardKinematics(
            Vector2 shoulder, Vector2 elbowRest, Vector2 wristRest,
            float upperAngleDeg, float foreAngleDeg,
            out Vector2 elbow)
        {
            elbow = LadyLakeMath.RotateAround(elbowRest, shoulder, upperAngleDeg);
            Vector2 wristStraight = LadyLakeMath.RotateAround(wristRest, shoulder, upperAngleDeg);
            return LadyLakeMath.RotateAround(wristStraight, elbow, foreAngleDeg);
        }

        /// <summary>剑的挂点与角度（不含待机浮动）。</summary>
        private static void SwordState(
            float u, float breathPhaseUnused,
            out Vector2 gripTarget, out float angleDeg, out float attach)
        {
            CoreState s = EvaluateCore(u);
            Vector2 shoulder = LadyLakeLabConfig.AnchorToWorld(LadyLakeLabConfig.ShoulderAnchor);
            Vector2 elbowRest = LadyLakeLabConfig.AnchorToWorld(LadyLakeLabConfig.ElbowAnchor);
            Vector2 wristRest = LadyLakeLabConfig.AnchorToWorld(LadyLakeLabConfig.SwordHandAnchor);
            Vector2 elbow;
            Vector2 wrist = ForwardKinematics(shoulder, elbowRest, wristRest, s.upperAngleDeg, s.foreAngleDeg, out elbow);

            Vector2 gripArt = wristRest;
            Vector2 freeGrip = gripArt + LadyLakeLabConfig.SwordFreeOffset;
            gripTarget = LadyLakeMath.Lerp(freeGrip, wrist, s.swordAttach);
            angleDeg = LadyLakeMath.Lerp(LadyLakeLabConfig.SwordFreeAngleDeg, s.upperAngleDeg + s.foreAngleDeg, s.swordAttach);
            attach = s.swordAttach;
        }

        /// <summary>完整姿态求值。seconds 为循环时间，内部按 duration+pause 取模。</summary>
        public static LadyLakePose Evaluate(float seconds, float duration, float pause, float intensity)
        {
            duration = Mathf.Max(0.5f, duration);
            pause = Mathf.Max(0f, pause);
            intensity = Mathf.Clamp(intensity, 0f, 2f);

            float loopDuration = duration + pause;
            float t = LadyLakeMath.Wrap(seconds, loopDuration);
            float peakTime = duration * LiftEnd;

            bool inPause = false;
            float actionTime;
            if (t < peakTime)
            {
                actionTime = t;
            }
            else if (t < peakTime + pause)
            {
                actionTime = peakTime;
                inPause = true;
            }
            else
            {
                actionTime = t - pause;
            }

            float u = Mathf.Clamp01(actionTime / duration);
            float norm = loopDuration > 1e-6f ? t / loopDuration : 0f;

            CoreState core = EvaluateCore(u);
            float wake = ComputeWake(u, duration);

            LadyLakePose pose = new LadyLakePose();
            pose.valid = true;
            pose.time = t;
            pose.normalizedTime = norm;
            pose.loopDuration = loopDuration;
            pose.intensity = intensity;

            pose.phase = inPause ? LadyLakePhase.Hold : core.phase;
            pose.phaseProgress = inPause ? 1f : core.phaseProgress;

            pose.reach = core.reach;
            pose.hold = core.hold;
            pose.glow = core.glow * Mathf.Clamp(intensity, 0f, 2f);
            pose.wake = wake;
            pose.upperAngleDeg = core.upperAngleDeg;
            pose.foreAngleDeg = core.foreAngleDeg;

            pose.shoulder = LadyLakeLabConfig.AnchorToWorld(LadyLakeLabConfig.ShoulderAnchor);
            Vector2 elbowRest = LadyLakeLabConfig.AnchorToWorld(LadyLakeLabConfig.ElbowAnchor);
            Vector2 wristRest = LadyLakeLabConfig.AnchorToWorld(LadyLakeLabConfig.SwordHandAnchor);
            Vector2 elbow;
            pose.wrist = ForwardKinematics(
                pose.shoulder, elbowRest, wristRest, core.upperAngleDeg, core.foreAngleDeg, out elbow);
            pose.elbow = elbow;

            // 剑：自由漂浮时叠加整数周期的上下浮动与轻微摆角（首尾天然连续）
            Vector2 gripArt = wristRest;
            Vector2 freeGrip = gripArt + LadyLakeLabConfig.SwordFreeOffset;
            Vector2 gripTarget = LadyLakeMath.Lerp(freeGrip, pose.wrist, core.swordAttach);
            float angleDeg = LadyLakeMath.Lerp(LadyLakeLabConfig.SwordFreeAngleDeg, core.upperAngleDeg + core.foreAngleDeg, core.swordAttach);

            float free = 1f - core.swordAttach;
            float bobPhase = LadyLakeMath.TwoPi * norm * LadyLakeLabConfig.SwordFreeBobCycles;
            float bobLift = Mathf.Sin(bobPhase) * 0.035f * free;
            float bobAngle = Mathf.Sin(bobPhase + 0.6f) * 2.4f * free;
            gripTarget += new Vector2(0f, bobLift);
            angleDeg += bobAngle;

            pose.swordGrip = gripTarget;
            pose.swordAngleDeg = angleDeg;
            pose.held = core.swordAttach >= 0.9995f;

            return pose;
        }

        /// <summary>剑握点速度（世界单位/秒）-> 归一化 wake，用于局部水流与气泡。</summary>
        private static float ComputeWake(float u, float duration)
        {
            float h = 0.01f;
            float u0 = Mathf.Clamp01(u - h);
            float u1 = Mathf.Clamp01(u + h);
            Vector2 g0, g1;
            float a0, a1, s0, s1;
            SwordState(u0, 0f, out g0, out a0, out s0);
            SwordState(u1, 0f, out g1, out a1, out s1);

            float du = Mathf.Max(1e-5f, u1 - u0);
            float dt = du * Mathf.Max(0.5f, duration);
            float linear = Vector2.Distance(g0, g1) / dt;                 // 单位/秒
            float angular = Mathf.Abs(Mathf.DeltaAngle(a0, a1)) / dt;     // 度/秒

            float speed = linear + angular * 0.012f;
            return Mathf.Clamp01(speed / 2.6f);
        }

        /// <summary>握住窗口（含 pause）用于验收滑移检测。</summary>
        public static void HeldWindow(float duration, float pause, out float startSeconds, out float endSeconds)
        {
            duration = Mathf.Max(0.5f, duration);
            pause = Mathf.Max(0f, pause);
            startSeconds = duration * SummonEnd;
            endSeconds = duration * LowerEnd + pause;
        }
    }
}
