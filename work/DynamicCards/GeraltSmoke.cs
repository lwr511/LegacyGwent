#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Reflection;
using Assets.Script.DynamicCards;
using UnityEngine;
using UnityEngine.UI;
public class GeraltSmoke:MonoBehaviour
{
    IEnumerator Start()
    {
        gameObject.AddComponent<AudioListener>();
        DynamicCardSettings.Enabled=true;
        var canvas=new GameObject("Canvas",typeof(Canvas)).GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
        var art=new GameObject("Art",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image)).GetComponent<Image>();
        art.transform.SetParent(canvas.transform,false);art.rectTransform.sizeDelta=new Vector2(500,700);
        var args=System.Environment.GetCommandLineArgs();int index=System.Array.IndexOf(args,"-artId");
        string artId=index<0?"11210300":args[index+1];
        DynamicCardView.Bind(art,artId,false,true);
        RawImage raw=null;while(raw==null || !raw.enabled){yield return null;raw=art.GetComponentInChildren<RawImage>();}
        var view=art.GetComponent<DynamicCardView>();
        var model=(GameObject)typeof(DynamicCardView).GetField("model",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(view);
        string output=Path.GetFullPath("../GeraltDiagnosis/"+artId);Directory.CreateDirectory(output);
        var nodes=model.GetComponentsInChildren<Transform>(true);var starts=System.Array.ConvertAll(nodes,n=>n.localPosition);var turns=System.Array.ConvertAll(nodes,n=>n.localRotation);
        Capture(raw,output+"/intro.png");
        yield return new WaitForSecondsRealtime(1.6f);
        int moved=0;for(int i=0;i<nodes.Length;i++)if(Vector3.Distance(starts[i],nodes[i].localPosition)>.001f || Quaternion.Angle(turns[i],nodes[i].localRotation)>.1f)moved++;
        Debug.Log("CARD_ANIMATION_MOVED "+artId+" transforms="+moved);
        Capture(raw,output+"/all.png");
        foreach(var ps in model.GetComponentsInChildren<ParticleSystem>(true))ps.GetComponent<Renderer>().enabled=false;
        yield return null;yield return null;Capture(raw,output+"/no_particles.png");
        var renderers=model.GetComponentsInChildren<Renderer>(true);
        foreach(var r in renderers)
        {
            Debug.Log("DIAG_BOUNDS "+r.name+" active="+r.gameObject.activeInHierarchy+" enabled="+r.enabled+" bounds="+r.bounds);
            if(r is ParticleSystemRenderer)continue;
            var materials=r.materials;
            foreach(var material in materials) material.shader=Shader.Find("DynamicCards/Native/Custom_Cards_CardCore_ImageLayerShader");
        }
        yield return null;yield return null;Capture(raw,output+"/simple_shader.png");
        foreach(var r in renderers)Debug.Log("GERALT_RENDERER "+r.name+" "+r.GetType().Name+" "+string.Join("|",System.Array.ConvertAll(r.sharedMaterials,m=>m==null?"null":m.name+":"+m.shader.name+":"+(m.mainTexture==null?"null":m.mainTexture.name))));
        Debug.Log("GERALT_DIAG_DONE");
        UnityEditor.EditorApplication.Exit(0);
    }
    void Capture(RawImage raw,string path){var rt=(RenderTexture)raw.texture;RenderTexture.active=rt;var t=new Texture2D(rt.width,rt.height,TextureFormat.RGBA32,false);t.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);t.Apply();File.WriteAllBytes(path,t.EncodeToPNG());RenderTexture.active=null;Destroy(t);}
}
#endif
