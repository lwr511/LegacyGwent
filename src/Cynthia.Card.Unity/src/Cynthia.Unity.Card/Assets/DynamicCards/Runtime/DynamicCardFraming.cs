using UnityEngine;

namespace Assets.Script.DynamicCards
{
    internal static class DynamicCardFraming
    {
        // Shared by every source scene, including cards added to the catalog in future.
        // Measured in visible portrait height, so cropped cards and thumbnail resolutions agree.
        public const float VerticalArtOffset = .13f;
        private const float SourceSize = 1024f;

        public static Rect ArtRegion(DynamicCardEntry entry)
        {
            float sideMargin = Mathf.Ceil(entry.topMargin * 648f / 947f * .5f);
            return new Rect((189f + sideMargin) / SourceSize, 34f / SourceSize,
                (648f - 2 * sideMargin) / SourceSize, (947f - entry.topMargin) / SourceSize);
        }

        public static void Apply(Camera camera, DynamicCardEntry entry)
        {
            camera.aspect = 1f;
            // Shift the portrait composition without tilting the scene or changing perspective scale.
            // Apply before the first render; intro, loop and drag return share the same origin.
            var projection = camera.projectionMatrix;
            // The existing top trim has already lifted the portrait origin; do not apply that lift twice.
            float offset = Mathf.Max(0f, VerticalArtOffset * ArtRegion(entry).height - entry.topMargin / SourceSize);
            projection.m12 -= 2f * offset;
            camera.projectionMatrix = projection;
        }
    }
}
