#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

public static class CityGameplayBuilder
{
    [MenuItem("Hooligan/City/Bake Gameplay Navigation")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop play mode before baking.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.name != "Gameplay") throw new InvalidOperationException("Open Gameplay first.");
        var environment = scene.GetRootGameObjects().First(g=>g.name=="Demonstration");
        var surface = environment.GetComponent<NavMeshSurface>() ?? environment.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.overrideVoxelSize = true;
        surface.voxelSize = .3f;
        surface.overrideTileSize = true;
        surface.tileSize = 256;
        int added = 0;
        foreach (var mf in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshFilter>()).Where(m=>m.transform.root.name=="Demonstration" || m.transform.root.name=="Traffic"))
        {
            if (!mf.sharedMesh) continue;
            if (!mf.GetComponent<Collider>())
            {
                mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                added++;
            }
            var r=mf.GetComponent<Renderer>();
            string n=mf.name.ToLowerInvariant();
            bool road = (n.StartsWith("road") && !n.Contains("column") && !n.Contains("sign") && !n.Contains("tunnel")) || n.StartsWith("asphalt");
            bool ground = road || n.StartsWith("park_road") ||
                n.StartsWith("grass_") || n.StartsWith("building_platform") || n.StartsWith("beach_") ||
                n.StartsWith("sidewalk") || n.StartsWith("parking_") || n.StartsWith("ground") || n.StartsWith("platform_") || n.StartsWith("football_field");
            var mod=mf.GetComponent<NavMeshModifier>() ?? mf.gameObject.AddComponent<NavMeshModifier>();
            mod.overrideArea=true;
            mod.area=road ? 3 : ground ? 0 : 1;
            if (r) { r.enabled=true; r.forceRenderingOff=false; }
        }
        surface.BuildNavMesh();
        if (!surface.navMeshData || NavMesh.CalculateTriangulation().vertices.Length==0)
            throw new InvalidOperationException("City navigation bake produced no walkable surface.");
        const string navPath="Assets/Scenes/GameplayNavigation.asset";
        var old=AssetDatabase.LoadAssetAtPath<NavMeshData>(navPath);
        if (old) { EditorUtility.CopySerialized(surface.navMeshData, old); surface.RemoveData(); surface.navMeshData=old; surface.AddData(); }
        else AssetDatabase.CreateAsset(surface.navMeshData,navPath);
        var anchors=GameObject.Find("GangPositionGameObject") ?? new GameObject("GangPositionGameObject");
        var player=Child(anchors.transform,"PlayerSpawnRoot");
        player.position=Walk(new Vector3(120,0,90));
        var enemies=Child(anchors.transform,"EnemySpawnRoot");
        Vector3[] targets={new Vector3(195,0,60),new Vector3(45,0,60),new Vector3(240,0,195),new Vector3(120,0,-120),new Vector3(-150,0,90)};
        for(int i=0;i<targets.Length;i++) Child(enemies,"EnemySpawn_"+i).position=Walk(targets[i]);
        var bm=UnityEngine.Object.FindFirstObjectByType<BattleManager>();
        bm.playerSpawnRoot=player; bm.enemySpawnRoot=enemies; bm.retreatPoint=player;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Artifacts/CityQA");
        File.WriteAllText("Artifacts/CityQA/bake.txt",$"Colliders added: {added}\nNav vertices: {NavMesh.CalculateTriangulation().vertices.Length}\nHome: {player.position}\n");
    }
    static Transform Child(Transform p,string n) { var t=p.Find(n); if(t)return t; t=new GameObject(n).transform; t.SetParent(p,false); return t; }
    public static void Report()
    {
        Directory.CreateDirectory("Artifacts/CityQA");
        var lines=new System.Collections.Generic.List<string>();
        lines.Add("Playing="+EditorApplication.isPlaying+" NavVertices="+NavMesh.CalculateTriangulation().vertices.Length);
        foreach(var a in UnityEngine.Object.FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None))
            lines.Add(a.name+" position="+a.transform.position+" onNav="+a.isOnNavMesh+" renderers="+a.GetComponentsInChildren<Renderer>().Length);
        if(Camera.main)lines.Add("Camera="+Camera.main.transform.position+" fov="+Camera.main.fieldOfView+" angle="+Camera.main.transform.eulerAngles);
        var city=CityGameplay.Instance;
        if(city && city.Locations!=null)
            for(int i=0;i<city.Locations.Length;i++)
            {
                var path=new NavMeshPath();NavMesh.CalculatePath(city.Home,city.Locations[i],NavMesh.AllAreas,path);
                lines.Add(city.LocationNames[i]+" "+city.Locations[i]+" "+path.status);
            }
        lines.Add("PoliceRoute="+(PoliceRoadSpline.Instance?.TotalLength??0));
        File.WriteAllLines("Artifacts/CityQA/runtime.txt",lines);
    }
    static Vector3 Walk(Vector3 p)
    {
        if(NavMesh.SamplePosition(p,out var hit,18,NavMesh.AllAreas)) return hit.position;
        throw new InvalidOperationException("No walkable point near "+p);
    }
}
#endif
