using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace ChipArena.UnityConversion
{
    // C17 (client/pve/beacons.ts): who gets a revive beacon and what it shows, shared by the 3D column/ring below and the
    // label/HELP overlay (NativeBeaconHud). Reads only replicated state, so guests see the same beacons as the host.
    public static class NativeBeaconView
    {
        // beacons.ts:25-29 beaconTargets: dead, in the party (humans and party bots), connected, the run not over, and not
        // a vent-fall body still in the shaft.
        public static bool Target(NativeArena arena,NativePilot p,out NativePveRow row)
        {
            row=null;var v=arena.Pve;
            return v!=null&&v.Stage!="ended"&&p.Hp<=0&&p.Seat.connected&&v.Rows.TryGetValue(p.Seat.id,out row)&&!arena.InShaft(p);
        }
        // beacons.ts:87 at the body (death position), on the floor.
        public static Vector3 Position(NativePilot p){Vector3 at=p.Death!=null?p.Death.position:p.Body.position;return new Vector3(at.x,0,at.z);}
        public static bool Self(NativePveRow row)=>row.SelfAt>=0;
        // beacons.ts:21 the solo self-revive timer from the death time (NativeArena.PveDied sets SelfAt = death + 5 s).
        public static float Progress(NativeArena arena,NativePilot p,NativePveRow row)=>Self(row)?NativeBeaconRules.Progress(arena.Time,p.Death!=null?p.Death.at:arena.Time,row.SelfAt):0;
    }
    public sealed partial class ArenaPresentation
    {
        private sealed class BeaconGeometry : NativeBeaconRules.IMeshSink
        {
            public readonly List<Vector3> Points=new List<Vector3>(512),Uv=new List<Vector3>(512);public readonly List<Color> Colors=new List<Color>(512);public readonly List<int> Indices=new List<int>(1024);
            public int Vertex(float x,float y,float z,float u,float v,float flat,float r,float g,float b,float a){Points.Add(new Vector3(x,y,z));Uv.Add(new Vector3(u,v,flat));Colors.Add(new Color(r,g,b,a));return Points.Count-1;}
            public void Triangle(int a,int b,int c){Indices.Add(a);Indices.Add(b);Indices.Add(c);}
            public void Clear(){Points.Clear();Uv.Clear();Colors.Clear();Indices.Clear();}
        }
        private readonly BeaconGeometry beaconGeometry=new BeaconGeometry();private Mesh beaconMesh;private Material beaconMaterial;private Texture2D beaconMotes;
        // beacons.ts:64-97 the light column with rising motes and the floor ring with the self-revive arc, one additive
        // draw for every beacon (vertex colour = beacon colour and opacity, shaded for the project's colour space).
        private void PveBeacons(NativeArena arena)
        {
            if(arena.Pve==null)return;beaconGeometry.Clear();float t=arena.Time;bool linear=QualitySettings.activeColorSpace==ColorSpace.Linear;
            foreach(var p in arena.Pilots)
            {
                if(!NativeBeaconView.Target(arena,p,out var row))continue;bool self=NativeBeaconView.Self(row);
                int color=self?NativeBeaconRules.Self:NativeBeaconRules.Down;Vector3 at=NativeBeaconView.Position(p);
                NativeBeaconRules.Column(beaconGeometry,at.x,at.z,-1,color,linear,NativeBeaconRules.ColumnOpacity(t,self));
                NativeBeaconRules.Ring(beaconGeometry,at.x,at.z,-1,t,NativeBeaconView.Progress(arena,p,row),color,linear);
            }
            if(beaconGeometry.Points.Count==0||!EnsureBeacons())return;
            beaconMesh.Clear();beaconMesh.SetVertices(beaconGeometry.Points);beaconMesh.SetColors(beaconGeometry.Colors);beaconMesh.SetUVs(0,beaconGeometry.Uv);beaconMesh.SetTriangles(beaconGeometry.Indices,0);beaconMesh.RecalculateBounds();
            beaconMaterial.SetFloat("_Scroll",NativeBeaconRules.MoteScroll(t));
            Graphics.DrawMesh(beaconMesh,Matrix4x4.identity,beaconMaterial,0,null,0,null,ShadowCastingMode.Off,false);
        }
        private bool EnsureBeacons()
        {
            if(beaconMaterial!=null)return true;
            var shader=Resources.Load<Shader>("NativeBeacon");if(shader==null||!shader.isSupported)shader=Resources.Load<Shader>("NativeGlow");if(shader==null)return false;
            beaconMaterial=new Material(shader){name="Ward Zero beacon"};
            if(beaconMaterial.HasProperty("_MainTex"))
            {
                // beacons.ts:32-39 motes map (repeat-wrapped, mipmapped like three's CanvasTexture).
                beaconMotes=new Texture2D(NativeBeaconRules.MoteWidth,NativeBeaconRules.MoteHeight,TextureFormat.RGBA32,true,true){name="Ward Zero beacon motes",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear,hideFlags=HideFlags.HideAndDontSave};
                var pixels=new byte[NativeBeaconRules.MoteWidth*NativeBeaconRules.MoteHeight*4];NativeBeaconRules.Motes(pixels,true);beaconMotes.SetPixelData(pixels,0);beaconMotes.Apply(true,true);
                beaconMaterial.mainTexture=beaconMotes;
            }
            beaconMesh=new Mesh{name="Ward Zero beacons"};beaconMesh.MarkDynamic();return true;
        }
        private void ReleaseBeacons()
        {
            beaconGeometry.Clear();if(beaconMaterial!=null)CapturedAnimation.Release(beaconMaterial);if(beaconMotes!=null)CapturedAnimation.Release(beaconMotes);if(beaconMesh!=null)CapturedAnimation.Release(beaconMesh);
            beaconMaterial=null;beaconMotes=null;beaconMesh=null;
        }
    }
}
