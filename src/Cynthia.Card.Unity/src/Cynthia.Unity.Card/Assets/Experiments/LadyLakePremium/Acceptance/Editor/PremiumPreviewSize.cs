using System;
using System.Reflection;
using UnityEditor;
namespace LegacyGwent.LadyLakePremium.Acceptance {
public static class PremiumPreviewSize {
 public static void Set(){
 var asm=typeof(UnityEditor.Editor).Assembly;var sizesType=asm.GetType("UnityEditor.GameViewSizes");
 var singleton=typeof(ScriptableSingleton<>).MakeGenericType(sizesType);var sizes=singleton.GetProperty("instance").GetValue(null,null);
 var groupType=asm.GetType("UnityEditor.GameViewSizeGroupType");var group=sizesType.GetMethod("GetGroup").Invoke(sizes,new[]{Enum.Parse(groupType,"Standalone")});var gt=group.GetType();
 int count=(int)gt.GetMethod("GetBuiltinCount").Invoke(group,null)+(int)gt.GetMethod("GetCustomCount").Invoke(group,null);int chosen=-1;
 for(int i=0;i<count;i++){var size=gt.GetMethod("GetGameViewSize").Invoke(group,new object[]{i});var st=size.GetType();if((int)st.GetProperty("width").GetValue(size,null)==1600&&(int)st.GetProperty("height").GetValue(size,null)==900){chosen=i;break;}}
 if(chosen<0){var type=asm.GetType("UnityEditor.GameViewSize");var sizeType=asm.GetType("UnityEditor.GameViewSizeType");var item=Activator.CreateInstance(type,new[]{Enum.Parse(sizeType,"FixedResolution"),(object)1600,(object)900,(object)"Lady Lake preview"});gt.GetMethod("AddCustomSize").Invoke(group,new[]{item});chosen=count;}
 var viewType=asm.GetType("UnityEditor.GameView");var view=EditorWindow.GetWindow(viewType);viewType.GetProperty("selectedSizeIndex",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).SetValue(view,chosen,null);view.Show();
 }
}}
