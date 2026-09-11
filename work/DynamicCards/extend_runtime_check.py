from pathlib import Path
root=Path(__file__).resolve().parent
path=root.parents[1]/'src/Cynthia.Card.Unity/src/Cynthia.Unity.Card/Assets/DynamicCards/Editor/DynamicCardVerification.cs'
text=path.read_text()
old='''                    Debug.Log("DYNAMIC_RUNTIME_PASS static-toggle intro-loop drag-half-limit return hidden unmatched cleanup");Finish(0);'''
new='''                    DynamicCardView.Bind(art, "11210300", false, true, frame);step = 8;
                }
                else if (step == 8 && art.GetComponentInChildren<RawImage>() != null)
                {
                    CheckGeralt(false);Capture("unity_geralt_before_cut.png");start = EditorApplication.timeSinceStartup;step = 9;
                }
                else if (step == 9 && elapsed > 1.6)
                {
                    CheckGeralt(true);Capture("unity_geralt_after_cut.png");
                    Debug.Log("DYNAMIC_RUNTIME_PASS static-toggle intro-loop drag-half-limit return hidden unmatched cleanup geralt-cut-timing");Finish(0);'''
if old not in text:raise RuntimeError('Verification source changed')
text=text.replace(old,new)
marker='        private static void Capture(string name)'
helper='''        private static void CheckGeralt(bool cut)
        {
            var view = art.GetComponent<DynamicCardView>();
            var model = (GameObject)typeof(DynamicCardView).GetField("model",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(view);
            var before = model.transform.Find("10090100/Pivot/GeraltSwordmasterMesh/Pose01");
            var after = model.transform.Find("10090100/Pivot/GeraltSwordmasterMesh/Pose02");
            if(before==null || after==null || before.gameObject.activeSelf==cut || after.gameObject.activeSelf!=cut)
                throw new Exception("Geralt cut groups are out of phase: cut="+cut);
        }
'''
text=text.replace(marker,helper+marker)
(root/'DynamicCardVerification.cs').write_text(text,encoding='utf8')
