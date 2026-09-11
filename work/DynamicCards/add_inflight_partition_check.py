from pathlib import Path
r=Path(__file__).resolve().parent
text='''        cards[11].rectTransform.anchoredPosition=Vector2.zero;
        DynamicCardView.Bind(cards[11],"20027500");
        var creatingField=typeof(DynamicCardLibrary).GetField("creating",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
        start=Time.realtimeSinceStartup;
        while(!(bool)creatingField.GetValue(DynamicCardLibrary.Instance) && Time.realtimeSinceStartup-start<15)yield return null;
        if(!(bool)creatingField.GetValue(DynamicCardLibrary.Instance)){Fail("in-flight cancellation did not start a request");yield break;}
        DynamicCardView.Bind(cards[11],"no-such-card");
        start=Time.realtimeSinceStartup;
        while(((bool)creatingField.GetValue(DynamicCardLibrary.Instance) || LoadedParts()>1) && Time.realtimeSinceStartup-start<30)yield return null;
        if(Ready(cards[11]) || LoadedParts()!=1){Fail("cancelled in-flight request retained a partition or returned old art");yield break;}
        Debug.Log("QUEUE_INFLIGHT_PART_CANCELLATION_PASS");
'''
for project in ['Probe','FocusedProbe']:
 p=r/project/'Assets/QueueSmoke.cs';s=p.read_text();marker='        foreach(var card in cards)card.gameObject.SetActive(false);';assert marker in s;s=s.replace(marker,text+marker,1);p.write_text(s)
print('INFLIGHT_PART_CHECK_ADDED')
