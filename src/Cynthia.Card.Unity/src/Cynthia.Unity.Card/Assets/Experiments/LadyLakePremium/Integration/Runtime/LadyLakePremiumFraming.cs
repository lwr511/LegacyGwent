using UnityEngine;
using Assets.Script.DynamicCards;

namespace LegacyGwent.LadyLakePremium
{
    /// <summary>
    /// Exact framing math shared by the assembler, the validator and the runtime stage.
    ///
    /// The production <see cref="DynamicCardView"/> always renders the source prefab inside a
    /// SQUARE RenderTexture and then samples the original
    /// <c>DynamicCardFraming.ArtRegion</c> rectangle into the 497x713 card window.
    /// Nothing here overrides that; this type only computes where the contract-space content has
    /// to sit inside the prefab so the 497x710 painting (4.97 x 7.10 world units) lands inside
    /// that unchanged region.
    ///
    /// Numbers (FOV 35, cameraDistance -11.5, square aspect forced to 1):
    ///   halfHeight      = |cameraDistance| * tan(FOV/2)
    ///                   = 11.5 * tan(17.5 deg)
    ///                   = 11.5 * 0.31529879
    ///                   = 3.62593617
    ///   fullHeight      = 7.25187235  (== fullWidth, square RenderTexture)
    ///   ArtRegion       = (x .16602942, y .0035620682, w .68260696, h .98114324)
    ///   regionHeight    = .98114324 * 7.25187235 = 7.11513360  -> a 7.10-high painting fits
    ///   regionWidth     = .68260696 * 7.25187235 = 4.95017834  -> a 4.97-wide painting is 0.4%
    ///                                                             wider than the region; fitting
    ///                                                             by height (the required "world
    ///                                                             7.1 high") is preserved.
    /// The region centre sits slightly off the camera axis:
    ///   u = .16602942 + .68260696/2 = .50733290  -> dx = (u-.5)*fullHeight =  0.05317502
    ///   v = .0035620682 + .98114324/2 = .49413369 -> dy = (v-.5)*fullHeight = -0.04254091
    /// The production appearance anchor parents the prefab at (0, +2, 0) while the card camera
    /// stays on the staging axis, so the pivot must also remove that +2. Final pivot:
    ///   Pivot.localPosition = (0.053175, -2.042541, 0)
    /// </summary>
    public static class LadyLakePremiumFraming
    {
        public const float FieldOfView = 35f;
        public const float CameraDistance = -11.5f;
        public const float NearClip = 1f;
        public const float FarClip = 300f;

        // The original DynamicCardFraming.ArtRegion values, reproduced as documentation. The
        // production method is called at runtime; these constants only let the editor validator
        // cross-check the framing without touching production code.
        public const float ArtRegionX = .16602942f;
        public const float ArtRegionY = .0035620682f;
        public const float ArtRegionWidth = .68260696f;
        public const float ArtRegionHeight = .98114324f;

        // Contract coordinate space: P(px, py, z) = ((px-248.5)/100, (355-py)/100, z).
        // The 497x710 painting therefore spans 4.97 x 7.10 world units around the origin.
        public const float PaintingWidthUnits = 4.97f;
        public const float PaintingHeightUnits = 7.10f;

        public static Vector3 PivotLocalPosition()
        {
            float half = Mathf.Tan(FieldOfView * .5f * Mathf.Deg2Rad) * Mathf.Abs(CameraDistance);
            float full = half * 2f;
            float u = ArtRegionX + ArtRegionWidth * .5f;
            float v = ArtRegionY + ArtRegionHeight * .5f;
            // Remove the production appearance anchor offset (0, 2, 0) as well.
            return new Vector3((u - .5f) * full, (v - .5f) * full - DynamicCardFraming.AppearanceOffset.y, 0f);
        }

        public static float ArtRegionWorldHeight()
        {
            return ArtRegionHeight * (Mathf.Tan(FieldOfView * .5f * Mathf.Deg2Rad) * Mathf.Abs(CameraDistance) * 2f);
        }

        public static float ArtRegionWorldWidth()
        {
            return ArtRegionWidth * (Mathf.Tan(FieldOfView * .5f * Mathf.Deg2Rad) * Mathf.Abs(CameraDistance) * 2f);
        }
    }
}
