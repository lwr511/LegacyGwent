using UnityEngine;

namespace Assets.Script.DynamicCards
{
    internal static class DynamicCardFraming
    {
        // Shared by every source scene, including cards added to the catalog in future.
        // Measured in visible portrait height, so cropped cards and thumbnail resolutions agree.
        public const float VerticalArtOffset = .13f;
        private const float SourceSize = 1024f;

        public static Rect ThumbnailRegion(Vector2 displaySize)
        {
            // Keep the existing thumbnail focal point, but crop the square render texture
            // to the actual slot aspect instead of squeezing a wide slice into a thin row.
            var region = new Rect(.185f, .52f, .633f, .19f);
            if (displaySize.x <= 0 || displaySize.y <= 0) return region;
            float aspect = displaySize.x / displaySize.y;
            float width = Mathf.Min(region.width, region.height * aspect);
            float height = width / aspect;
            return new Rect(region.center.x - width * .5f, region.center.y - height * .5f, width, height);
        }

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
