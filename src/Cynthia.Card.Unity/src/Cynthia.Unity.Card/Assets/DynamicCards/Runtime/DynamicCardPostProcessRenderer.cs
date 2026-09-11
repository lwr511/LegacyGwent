using System.Collections.Generic;
using UnityEngine;

namespace Assets.Script.DynamicCards
{
    // The source card renderer applies each original pass twice. Keep this on the card's own camera.
    public sealed class DynamicCardPostProcessRenderer : MonoBehaviour
    {
        public DynamicCardPostEffect[] Effects;
        private readonly List<Material> active = new List<Material>();

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            active.Clear();
            if (Effects != null)
                foreach (var effect in Effects)
                {
                    if (effect == null) continue;
                    var material = effect.Prepare();
                    if (material != null) active.Add(material);
                }
            if (active.Count == 0) { Graphics.Blit(source, destination); return; }
            RenderTexture current = source, owned = null;
            try
            {
                for (int i = 0; i < active.Count; i++)
                {
                    var target = i == active.Count - 1 ? destination : RenderTexture.GetTemporary(source.width, source.height, 0, source.format);
                    var scratch = RenderTexture.GetTemporary(source.width, source.height, 0, source.format);
                    try
                    {
                        Graphics.Blit(current, scratch, active[i], 0);
                        Graphics.Blit(scratch, target, active[i], 0);
                    }
                    finally { RenderTexture.ReleaseTemporary(scratch); }
                    if (owned != null) RenderTexture.ReleaseTemporary(owned);
                    owned = i == active.Count - 1 ? null : target;
                    current = target;
                }
            }
            finally { if (owned != null) RenderTexture.ReleaseTemporary(owned); }
        }
    }
}
