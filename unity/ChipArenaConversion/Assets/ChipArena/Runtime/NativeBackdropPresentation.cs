using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class ArenaPresentation
    {
        // The main menu's backdrop when no match exists: the selected map, its scenery and hazards, as renderer.ts draws the
        // idle state behind the menu. Ward Zero's moving parts rest in their PvP pose (cells stubbed, vents and doors shut).
        public void RenderBackdrop(string commit,string mapId,float time)
        {
            if(sourceModels==null)return;sourceModels.Begin();sourceModels.Queue("map-"+mapId,Matrix4x4.identity);HazardPresentation(mapId,time);SceneryMotion(commit,mapId);
            if(mapId=="siren")
            {
                if(wardMotion==null){wardMotion=JsonUtility.FromJson<NativeWardMotion>(Resources.Load<TextAsset>("WardMotion").text);if(wardMotion.commit!=commit)throw new System.InvalidOperationException("Ward motion source differs");}
                foreach(var p in wardMotion.parts)
                {
                    if(p.kind=="beam"||p.matrix==null||p.matrix.Length!=16)continue;var m=new Matrix4x4();for(int i=0;i<16;i++)m[i]=p.matrix[i];
                    sourceModels.Queue(p.id,p.kind=="cell"?m*Matrix4x4.Scale(new Vector3(1,.45f/3.45f,1)):m);
                }
            }
            sourceModels.Flush(UsesInstancing);
        }
        // renderer.ts idle camera: one lap of the hall every 2pi/.022 s, looking at the arena centre.
        public static void OverviewCamera(Camera camera,float time)
        {float t=time*.022f;camera.transform.position=new Vector3(42*Mathf.Cos(t+.45f),32,46*Mathf.Sin(t+.65f));camera.transform.LookAt(new Vector3(0,.7f,0));}
    }
}
