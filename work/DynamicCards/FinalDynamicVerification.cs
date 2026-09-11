using System;
using System.IO;
using Assets.Script.DynamicCards.Editor;
using UnityEngine;
public static class FinalDynamicVerification
{
    [Serializable] class Audit { public int cards,particles; public string[] issues; }
    public static void Run(){
        DynamicCardContentAudit.Run();
        var audit=JsonUtility.FromJson<Audit>(File.ReadAllText(Path.GetFullPath("../RenderAuditNew/audit.json")));
        if(audit.cards!=462 || audit.particles!=6005 || audit.issues.Length!=0)throw new Exception("Full card audit failed");
        DynamicCardContentAudit.Bundle();
        NativePlayerBuild.Run();
        QueueSmokeEditor.Build();
        Debug.Log("DYNAMIC_FINAL_BUILD_READY");
    }
}
