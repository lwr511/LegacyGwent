using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Assets.Script.DynamicCards.Editor;
[InitializeOnLoad]
public static class ToolbarSmoke
{
    const string Before="Assets/ToolbarBefore.unity", Login="Assets/Resources/Scenes/LoginScene.unity";
    static ToolbarSmoke(){EditorApplication.update+=Tick;}
    public static void Run(){
        System.IO.Directory.CreateDirectory("Assets/Resources/Scenes");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),Login);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),Before);
        EditorSceneManager.playModeStartScene=null;
        SessionState.SetInt("ToolbarSmokeStage",1);
        SessionState.SetFloat("ToolbarSmokeStart",(float)EditorApplication.timeSinceStartup);
    }
    static void Tick(){
        int stage=SessionState.GetInt("ToolbarSmokeStage",0);if(stage==0)return;
        try{
            if(EditorApplication.timeSinceStartup-SessionState.GetFloat("ToolbarSmokeStart",0)>90)throw new Exception("timeout");
            if(stage==1){
                var type=typeof(Editor).Assembly.GetType("UnityEditor.Toolbar");
                var prop=typeof(Editor).Assembly.GetType("UnityEditor.GUIView").GetProperty("visualTree",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
                bool found=false;foreach(var bar in Resources.FindObjectsOfTypeAll(type)){
                    var root=prop.GetValue(bar,null) as VisualElement;
                    found|=root!=null && root.Q<VisualElement>("LegacyGwentLoginPlay")!=null;
                }
                if(!found)return;
                SessionState.SetInt("ToolbarSmokeStage",2);LoginPlayToolbar.PlayLogin();
            }else if(stage==2 && EditorApplication.isPlaying){
                if(EditorSceneManager.GetActiveScene().path!=Login)throw new Exception("wrong play scene");
                if(EditorSceneManager.playModeStartScene!=null)return;
                SessionState.SetInt("ToolbarSmokeStage",3);EditorApplication.isPlaying=false;
            }else if(stage==3 && !EditorApplication.isPlayingOrWillChangePlaymode){
                if(EditorSceneManager.GetActiveScene().path!=Before || EditorSceneManager.playModeStartScene!=null)throw new Exception("original scene/start override not restored");
                SessionState.SetInt("ToolbarSmokeStage",0);Debug.Log("LOGIN_TOOLBAR_PASS attached login-start original-scene-restored");EditorApplication.Exit(0);
            }
        }catch(Exception e){SessionState.SetInt("ToolbarSmokeStage",0);Debug.LogError("LOGIN_TOOLBAR_FAIL "+e);EditorApplication.Exit(1);}
    }
}
