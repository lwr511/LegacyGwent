from pathlib import Path
r=Path(__file__).resolve().parent
phase='''        // A second partition must not unload the partition still used by visible cards.
        cards[10].rectTransform.anchoredPosition=Vector2.zero;
        DynamicCardView.Bind(cards[10],"20027500");
        start=Time.realtimeSinceStartup;
        while(!Ready(cards[10]) && Time.realtimeSinceStartup-start<60)yield return null;
        if(!Ready(cards[10]) || LoadedParts()<2){Fail("second card partition did not load");yield break;}
        cards[10].gameObject.SetActive(false);
        start=Time.realtimeSinceStartup;
        while(LoadedParts()>1 && Time.realtimeSinceStartup-start<15)yield return null;
        if(LoadedParts()!=1 || !Ready(cards[0])){Fail("partition release damaged a live card or retained unused partition");yield break;}
        Debug.Log("QUEUE_PART_ISOLATION_PASS");
        foreach(var card in cards)card.gameObject.SetActive(false);
        start=Time.realtimeSinceStartup;
        while(LoadedParts()>0 && Time.realtimeSinceStartup-start<15)yield return null;
        if(LoadedParts()!=0){Fail("unreferenced partitions were not released");yield break;}
        cards[0].gameObject.SetActive(true);
        start=Time.realtimeSinceStartup;
        while(!Ready(cards[0]) && Time.realtimeSinceStartup-start<60)yield return null;
        if(!Ready(cards[0]) || LoadedParts()!=1){Fail("released partition did not reload");yield break;}
        Debug.Log("QUEUE_PARTS_UNLOADED_AND_RELOADED");
'''
helper='''    private int LoadedParts()
    {
        var field=typeof(DynamicCardLibrary).GetField("parts",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
        var values=(System.Collections.IDictionary)field.GetValue(DynamicCardLibrary.Instance);
        int count=0;foreach(var value in values.Values)if((AssetBundle)value.GetType().GetField("Bundle").GetValue(value)!=null)count++;
        return count;
    }
'''
for name in ['Probe','FocusedProbe']:
 p=r/name/'Assets/QueueSmoke.cs';s=p.read_text();assert 'QUEUE_PART_ISOLATION_PASS' not in s
 marker='        DynamicCardSettings.Enabled=false;'
 assert marker in s;s=s.replace(marker,phase+marker,1)
 marker='    private bool Ready(Image art)';assert marker in s;s=s.replace(marker,helper+marker,1)
 p.write_text(s)
print('PARTITION_QUEUE_CHECKS_ADDED')
