#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using C=ChipArena.UnityConversion.NativeCyberTheme;
using H=ChipArena.UnityConversion.NativeHudChrome;
using U=ChipArena.UnityConversion.NativeUi;

namespace ChipArena.UnityConversion
{
    public sealed partial class ConversionGame
    {
        private NativeMeridianStreetLifeDraw walkStreetLife;
        private NativeMeridianStreetPropsPreview walkStreetProps;

        /// <summary>The Developer walk's street life and the DEV preview of the solid street props (the one line in OpenCityWalk, after
        /// the dress, the bleed and the landmarks are made): planned from the walk's own records, as the live run plans it.</summary>
        private void StreetLifeWalkOpen(NativeLayeredCityWalk walk,uint layout,uint run)
        {
            StreetLifeWalkClose();if(walk==null)return;
            try
            {
                var input=NativeMeridianStreetLife.InputOf(walk,layout,run,surveySlots,meridianDress!=null?meridianDress.Plan:null,bleedDraw!=null?walkBleed:null,bleedDraw!=null?walkBleedPlan:null);
                walkStreetLife=NativeMeridianStreetLifeDraw.OpenAsync(input,cameraView);
                walkStreetProps=NativeMeridianStreetPropsPreview.Open(input,walk.Map.id,cameraView);
            }
            catch(Exception e){Debug.LogException(e);}
        }
        /// <summary>The walk closed (the one line in CloseCityWalk).</summary>
        private void StreetLifeWalkClose()
        {
            if(walkStreetLife!=null)walkStreetLife.Close();walkStreetLife=null;
            if(walkStreetProps!=null)Destroy(walkStreetProps.gameObject);walkStreetProps=null;
        }
    }

    /// <summary>
    /// The DEV preview of Meridian's solid street props (Settings · Developer · Walk Meridian greybox; Editor and development builds
    /// only, never a live run): every prop the contract proposes for the walk's layout and deal (Core NativeMeridianStreetLife
    /// .ProposeProps; NativeMeridianStreetProps' table), its model on its proposed block, for the owner to judge the look before Astra
    /// builds the blocks. Nothing here is a collider: the pilot walks through them, and the banner says so.
    ///  - P: the preview on or off. O: the proposed blocks themselves (filled boxes: the filled candidates bright, the run's empty
    ///    ones faint) and the routes' clear lanes on the floor. L: the street life (part 1) on or off, to compare with bare streets.
    /// Without its bank (ChipArenaCityStreetProps) the models are left out and the blocks draw as boxes.
    /// </summary>
    public sealed class NativeMeridianStreetPropsPreview : MonoBehaviour
    {
        public static bool Shown=true,Blocks;
        private List<NativeMeridianStreetProps.Block> props;private Camera eye;private string mapId;
        private readonly Dictionary<string,(Mesh near,Mesh far)> models=new Dictionary<string,(Mesh,Mesh)>(StringComparer.Ordinal);
        private Material solid,glow,boxMat;private Mesh cube;private Mesh lanes;private MaterialPropertyBlock tintBlock;
        private int filled;private System.Threading.Tasks.Task<List<NativeMeridianStreetProps.Block>> pending;
        private KeyCode lastKey;private float lastKeyAt=-9f;

        public static NativeMeridianStreetPropsPreview Open(NativeMeridianStreetLife.Input input,string mapId,Camera eye)
        {
            var go=new GameObject("Meridian street props (DEV preview)");var p=go.AddComponent<NativeMeridianStreetPropsPreview>();
            p.eye=eye;p.mapId=mapId;
            // the proposal builds the horde's lattice to keep it whole: on a worker, drawn from the frame it is ready
            p.pending=System.Threading.Tasks.Task.Run(()=>NativeMeridianStreetLife.ProposeProps(input));
            var shader=Resources.Load<Shader>("NativeStageLit");
            if(shader!=null&&shader.isSupported){p.solid=new Material(shader){name="Street props preview"};p.solid.SetFloat("_Glow",0);p.glow=new Material(shader){name="Street props preview glow"};p.glow.SetFloat("_Glow",1);}
            var bank=NativePackBanks.Bank(NativeMeridianStreetProps.Bank);
            if(bank!=null)foreach(var st in NativeMeridianStreetProps.Stems)foreach(var m in st.Models)
            {
                Mesh Tier(int t){var prop=bank.Find("rig/props/"+m+"/"+t);return prop==null||prop.Parts.Length==0?null:NativePackBanks.Mesh(NativeMeridianStreetProps.Bank,prop.Parts[0].Mesh);}
                var near=Tier(NativeStageArt.Fine);var far=Tier(NativeStageArt.Low)??near;if(near!=null)p.models[m]=(near,far);
            }
            var fx=Resources.Load<Shader>("NativeFieldGlow");
            if(fx!=null){p.boxMat=new Material(fx){name="Street props blocks",renderQueue=3010};p.boxMat.SetFloat("_Radial",0);p.boxMat.SetFloat("_Cull",0);}
            p.cube=Cube();p.lanes=Lanes(input);
            return p;
        }

        private void LateUpdate()
        {
            if(pending!=null&&pending.IsCompleted)
            {
                var task=pending;pending=null;
                if(task.Status!=System.Threading.Tasks.TaskStatus.RanToCompletion){Debug.LogWarning("Meridian street props (DEV preview): "+task.Exception?.GetBaseException().Message);return;}
                props=task.Result;foreach(var b in props)if(b.Filled)filled++;
                Debug.Log("Meridian street props (DEV preview): "+props.Count+" candidates, "+filled+" filled this run, "+models.Count+" models");
            }
            if(props==null||eye==null)return;var e=eye.transform.position;
            if(Shown&&solid!=null)
                foreach(var b in props)
                {
                    if(!b.Filled||!models.TryGetValue(b.Model,out var m))continue;var foot=NativeMeridianStreetProps.FootOf(b);
                    float d=Vector3.Distance(new Vector3(foot.x,foot.y,foot.z),e);if(d>180f)continue;var mesh=d<38f?m.near:m.far;
                    var M=Matrix4x4.TRS(new Vector3(foot.x,foot.y,foot.z),Quaternion.Euler(0,b.Yaw*Mathf.Rad2Deg,0),Vector3.one);
                    for(int s=0;s<mesh.subMeshCount;s++)Graphics.DrawMesh(mesh,M,s==0?solid:glow,0,null,s,null,s==0?ShadowCastingMode.On:ShadowCastingMode.Off,true);
                }
            if((Blocks||Shown&&models.Count==0)&&boxMat!=null)
            {
                foreach(var b in props)
                {
                    var c=b.Filled?new Color(1f,.25f,.75f,.32f):new Color(1f,.25f,.75f,.1f);tintBlock??=new MaterialPropertyBlock();tintBlock.SetColor("_Color",c);
                    Graphics.DrawMesh(cube,Matrix4x4.TRS(new Vector3(b.Pos.x,b.Pos.y,b.Pos.z),Quaternion.identity,new Vector3(b.Size.x,b.Size.y,b.Size.z)),boxMat,0,null,0,tintBlock,ShadowCastingMode.Off,false);
                }
                if(lanes!=null){tintBlock??=new MaterialPropertyBlock();tintBlock.SetColor("_Color",new Color(.2f,.95f,.85f,.16f));Graphics.DrawMesh(lanes,Matrix4x4.identity,boxMat,0,null,0,tintBlock,ShadowCastingMode.Off,false);}
            }
        }

        private void OnGUI()
        {
            var ev=Event.current;
            // the keys as GUI events (a real key and a capture's keyboard event alike); a held key's repeats toggle nothing
            if(ev.type==EventType.KeyDown&&GUIUtility.keyboardControl==0&&(ev.keyCode==KeyCode.P||ev.keyCode==KeyCode.O||ev.keyCode==KeyCode.L))
            {
                bool repeat=ev.keyCode==lastKey&&Time.unscaledTime-lastKeyAt<.6f;lastKey=ev.keyCode;lastKeyAt=Time.unscaledTime;ev.Use();if(repeat)return;
                if(ev.keyCode==KeyCode.P)Shown=!Shown;else if(ev.keyCode==KeyCode.O)Blocks=!Blocks;else NativeMeridianStreetLifeDraw.Hidden=!NativeMeridianStreetLifeDraw.Hidden;
                return;
            }
            if(props==null||ev.type!=EventType.Repaint)return;
            const string label="DEV PREVIEW · STREET PROPS ON PROPOSED BLOCKS (ASTRA'S TO BUILD) · NOT GAMEPLAY";
            if(Shown||Blocks)
            {
                var size=U.Measure(label,11,true,true,.12f);var r=new Rect(U.W/2-size.x/2-14,92,size.x+28,24);
                U.Fill(r,C.Void(.75f));H.Outline(r,C.Magenta(.9f),0,H.Corners.None);U.Text(r,label,11,C.Magenta(),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.12f);
            }
            string keys="P  Street props "+(Shown?"on":"off")+" ("+filled+" of "+props.Count+")     O  Blocks "+(Blocks?"on":"off")+"     L  Street life "+(NativeMeridianStreetLifeDraw.Hidden?"off":"on");
            var k=new Rect(20,U.H-46,U.Measure(keys,10,false,true,.06f).x+24,22);U.Fill(k,C.Void(.7f));U.Text(k,keys,10,C.Magenta(.95f),TextAnchor.MiddleCenter,mono:true,tracking:.06f);
        }

        private static Mesh Cube()
        {
            var m=new Mesh{name="Street props block"};var v=new List<Vector3>();var uv=new List<Vector2>();var t=new List<int>();var col=new List<Color>();
            for(int f=0;f<6;f++)
            {
                int a=f>>1;float s=(f&1)==0?.5f:-.5f;var n=a==0?new Vector3(s,0,0):a==1?new Vector3(0,s,0):new Vector3(0,0,s);
                var u=a==0?Vector3.forward:Vector3.right;var w=a==1?Vector3.forward:Vector3.up;int b=v.Count;
                v.Add(n-u*.5f-w*.5f);v.Add(n+u*.5f-w*.5f);v.Add(n+u*.5f+w*.5f);v.Add(n-u*.5f+w*.5f);
                for(int i=0;i<4;i++){uv.Add(Vector2.zero);col.Add(Color.white);}t.AddRange(new[]{b,b+1,b+2,b,b+2,b+3});
            }
            m.SetVertices(v);m.SetUVs(0,uv);m.SetColors(col);m.SetTriangles(t,0);m.RecalculateBounds();return m;
        }
        // The routes' clear lanes as flat strips on their legs (the contract's lane: the larger of 3.5 m and the route's half width
        // less the kerb's band, either side of its line).
        private static Mesh Lanes(NativeMeridianStreetLife.Input input)
        {
            if(input.Routes==null)return null;var v=new List<Vector3>();var uv=new List<Vector2>();var t=new List<int>();var col=new List<Color>();
            foreach(var r in input.Routes)
            {
                if(r?.Points==null)continue;
                for(int i=0;i+1<r.Points.Length;i++)
                {
                    var a=r.Points[i];var b=r.Points[i+1];var ab=new Vector3(b.x-a.x,0,b.z-a.z);float len=ab.magnitude;if(len<.1f)continue;var al=ab/len;var ac=new Vector3(al.z,0,-al.x);
                    float width=r.Widths!=null&&i<r.Widths.Length?r.Widths[i]:10;float half=Mathf.Max(NativeMeridianStreetProps.LaneHalf,width*.5f-NativeMeridianStreetProps.KerbBand);
                    var A=new Vector3(a.x,a.y+.08f,a.z);var B=new Vector3(b.x,b.y+.08f,b.z);int k=v.Count;
                    v.Add(A-ac*half);v.Add(A+ac*half);v.Add(B+ac*half);v.Add(B-ac*half);for(int j=0;j<4;j++){uv.Add(Vector2.zero);col.Add(Color.white);}
                    t.AddRange(new[]{k,k+1,k+2,k,k+2,k+3});
                }
            }
            var m=new Mesh{name="Street props lanes",indexFormat=v.Count>65000?IndexFormat.UInt32:IndexFormat.UInt16};m.SetVertices(v);m.SetUVs(0,uv);m.SetColors(col);m.SetTriangles(t,0);m.RecalculateBounds();return m;
        }
        private void OnDestroy()
        {
            if(solid!=null)Destroy(solid);if(glow!=null)Destroy(glow);if(boxMat!=null)Destroy(boxMat);if(cube!=null)Destroy(cube);if(lanes!=null)Destroy(lanes);
        }
    }
}
#endif
