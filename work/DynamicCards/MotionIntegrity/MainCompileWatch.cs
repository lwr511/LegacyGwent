using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEditor;using UnityEditor.Compilation;
public static class MotionWork {
const string W="C:/UnityProjects/LegacyGwent/work/DynamicCards/MotionIntegrity/";static List<string> rows=new List<string>();static int errors;static bool started;
public static void Run(){CompilationPipeline.assemblyCompilationFinished+=Finished;CompilationPipeline.compilationFinished+=AllFinished;EditorApplication.update+=Tick;File.WriteAllText(W+"final-clean-compile.txt","WATCHING");}
static void Tick(){if(started||File.Exists("Assets/Editor/LocalMotionInspection.cs"))return;started=true;EditorApplication.update-=Tick;File.WriteAllText(W+"final-clean-compile.txt","REFRESH "+DateTime.UtcNow.ToString("O"));AssetDatabase.Refresh();}
static void Finished(string assembly,CompilerMessage[] messages){rows.Add(assembly+" errors="+messages.Count(m=>m.type==CompilerMessageType.Error));foreach(var message in messages.Where(m=>m.type==CompilerMessageType.Error)){errors++;rows.Add(message.message);}}
static void AllFinished(object context){File.WriteAllLines(W+"final-clean-compile.txt",new[]{errors==0?"OK":"FAIL","utc="+DateTime.UtcNow.ToString("O"),"editorPlaying="+EditorApplication.isPlaying,"ready="+File.Exists("Library/DynamicCardsBundles/StandaloneWindows64/cards.bundle.editor-ready")}.Concat(rows));}
}