using UnityEngine;

namespace LegacyGwent.LadyLakePremium
{
    /// <summary>
    /// Twelve second environment effect cycle for the Lady Lake premium card.
    ///
    /// The Animator on the Environment prefab owns the slow transform sway; this
    /// component owns the two luminous beats that must follow the character:
    ///   * 3.2 s  sword catch glint (sharp attack, fast decay)
    ///   * 5.6-8.3 s lifted blade glow (sustained swell, then release)
    ///
    /// It writes a MaterialPropertyBlock only (no material instances, no
    /// allocations in Update) which is exactly how the production
    /// DynamicCardSourceControllers / DynamicCardEffects path writes animated
    /// material values. It uses the same 12 s clock as the model and audio so a
    /// single root cycle covers visuals and sound.
    ///
    /// If the model rig exposes the documented GripSocket (or Grip) marker the
    /// glow rides it; otherwise it sits on the contract grip anchor converted to
    /// world space. The environment never creates a sword or a character.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LadyLakeEnvironmentCycle : MonoBehaviour
    {
        public const float CycleDuration = 12f;

        [Header("Cycle")]
        public float duration = CycleDuration;
        public float catchTime = 3.2f;
        public float liftStart = 5.6f;
        public float liftEnd = 8.3f;

        [Header("Base levels")]
        public float glowIdle = 0.10f;
        public float glowCatch = 1.00f;
        public float glowLift = 0.62f;
        public float causticIdle = 0.62f;
        public float causticBoost = 0.22f;

        [Header("Targets")]
        public Renderer[] glowRenderers;
        public Renderer[] causticRenderers;
        public Transform glowRoot;

        [Header("Grip follow")]
        public string followName = "GripSocket";
        public string followFallbackName = "Grip";
        public bool followGrip = true;
        // Contract grip (pixel 380,543) plus the depth the model manifest reports
        // for the blade plane; used only when the rig exposes no marker.
        public Vector3 gripFallback = new Vector3(1.315f, -1.88f, 0.20f);

        private MaterialPropertyBlock block;
        private Transform follow;
        private bool followSearched;
        private float age;

        private static readonly int AlphaMultId = Shader.PropertyToID("_AlphaMult");
        private static readonly int TintColorId = Shader.PropertyToID("_TintColor");
        private static readonly Color GlowTint = new Color(0.72f, 1f, 0.96f, 1f);

        private void OnEnable()
        {
            block = new MaterialPropertyBlock();
            age = 0f;
            followSearched = false;
            follow = null;
            Apply(0f);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt < 0f) dt = 0f;
            if (dt > 0.1f) dt = 0.1f;
            age += dt;
            if (duration > 0.0001f)
            {
                // Repeat keeps the seam at exactly t=0/t=12, matching the
                // Intro and Loop clips and the 12 s audio file.
                while (age >= duration) age -= duration;
            }
            ResolveFollow();
            Apply(age);
            FollowGrip();
        }

        private void ResolveFollow()
        {
            if (!followGrip || followSearched) return;
            followSearched = true;
            Transform root = transform.root;
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == followName) { follow = all[i]; break; }
            }
            if (follow == null && !string.IsNullOrEmpty(followFallbackName))
            {
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] != null && all[i].name == followFallbackName) { follow = all[i]; break; }
                }
            }
        }

        private void FollowGrip()
        {
            if (glowRoot == null) return;
            if (follow != null)
            {
                // Ride the model rig's socket when it exists, so the glint sits on
                // the actual blade catch instead of a guessed position.
                glowRoot.position = follow.position;
                glowRoot.rotation = follow.rotation;
            }
            else if (glowRoot.parent != null)
            {
                // Contract grip anchor (pixel 380,543) converted with the shared
                // P(px,py,z) frame rule. The environment root sits on the frame
                // origin, so the anchor is a local offset.
                glowRoot.localPosition = gripFallback;
                glowRoot.localRotation = Quaternion.identity;
            }
        }

        private void Apply(float t)
        {
            if (block == null) block = new MaterialPropertyBlock();
            float glow = EvaluateGlow(t);
            float caustic = causticIdle + causticBoost * (0.5f + 0.5f * Mathf.Sin(t / Mathf.Max(0.001f, duration) * Mathf.PI * 4f));

            if (glowRenderers != null)
            {
                for (int i = 0; i < glowRenderers.Length; i++)
                {
                    Renderer r = glowRenderers[i];
                    if (r == null) continue;
                    r.GetPropertyBlock(block);
                    block.SetFloat(AlphaMultId, glow);
                    block.SetColor(TintColorId, new Color(GlowTint.r, GlowTint.g, GlowTint.b, glow));
                    r.SetPropertyBlock(block);
                }
            }
            if (causticRenderers != null)
            {
                for (int i = 0; i < causticRenderers.Length; i++)
                {
                    Renderer r = causticRenderers[i];
                    if (r == null) continue;
                    r.GetPropertyBlock(block);
                    block.SetFloat(AlphaMultId, caustic);
                    r.SetPropertyBlock(block);
                }
            }
            if (glowRoot != null)
            {
                float s = Mathf.Lerp(0.72f, 1.0f, Mathf.Clamp01(glow));
                glowRoot.localScale = new Vector3(s, s, s);
            }
        }

        /// <summary>
        /// 0..1 luminous envelope of the twelve second cycle. Pure function so the
        /// editor builder can bake matching Animator curves and so it can be
        /// verified without entering play mode.
        /// </summary>
        public float EvaluateGlow(float t)
        {
            float d = Mathf.Max(0.001f, duration);
            t = t - Mathf.Floor(t / d) * d;

            float value = glowIdle;

            // Catch glint: 40 ms attack from 3.06 s, exponential decay.
            float dtCatch = t - (catchTime - 0.14f);
            if (dtCatch > 0f && dtCatch < 0.85f)
            {
                float attack = 1f - Mathf.Exp(-dtCatch / 0.028f);
                float decay = Mathf.Exp(-dtCatch / 0.16f);
                value += (glowCatch - glowIdle) * attack * decay;
            }

            // Two bright reflections right after the catch, like a struck blade.
            float dtRing = t - catchTime;
            if (dtRing > 0.10f && dtRing < 1.4f)
                value += 0.22f * Mathf.Exp(-(dtRing - 0.10f) / 0.30f) * Mathf.Sin((dtRing - 0.10f) * 34f);

            // Lift swell: 5.6 s ramp, held to 8.0 s, released into 8.3 s.
            if (t > liftStart && t < liftEnd)
            {
                float rise = Mathf.Clamp01((t - liftStart) / 0.7f);
                float fall = Mathf.Clamp01((liftEnd - t) / 0.45f);
                value += (glowLift - glowIdle) * Mathf.SmoothStep(0f, 1f, Mathf.Min(rise, fall));
            }

            // Tail after the release, silently back to the idle level at t=12.
            if (t >= liftEnd && t < 10.9f)
                value += 0.16f * Mathf.Exp(-(t - liftEnd) / 0.55f);

            return Mathf.Clamp01(value);
        }
    }
}
