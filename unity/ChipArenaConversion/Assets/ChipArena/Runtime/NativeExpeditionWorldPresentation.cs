using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using U=ChipArena.UnityConversion.NativeUi;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Expedition world pieces in 3D (Claude, 2026-10-14; docs/EXPEDITION-PRESENTATION.md "World pieces"): Core NativeExpeditionWorld's
    /// pieces at the bound view's real anchors, drawn in the revive beacon's style (Resources/NativeBeacon.shader and its motes map:
    /// additive, double-sided, no depth write), in the NativeCyberTheme palette through NativeExpeditionHud.Tone. Every visible piece of a frame goes into one dynamic mesh refilled from reused lists: one
    /// draw, no allocation per frame. Pieces beyond NativeExpeditionWorld.CullDistance of the camera are skipped; reduced effects
    /// stop the spin, bob and mote scroll. The labels and glyphs are the HUD kit's, drawn at the projected points (ConversionGame's
    /// ExpeditionWorldLabels), as the beacon labels are. Nothing here claims a pickup: claiming goes through the Expedition commands.
    /// </summary>
    public sealed class NativeExpeditionWorldPresentation : IDisposable
    {
        private sealed class Sink : IExpeditionWorldSink
        {
            public readonly List<Vector3> Points=new List<Vector3>(1024),Uv=new List<Vector3>(1024);public readonly List<Color> Colors=new List<Color>(1024);public readonly List<int> Indices=new List<int>(2048);
            public bool Linear;
            public int Vertex(float x,float y,float z,float u,float v,float flat,ExpeditionTone tone,float level,float alpha)
            {
                var c=NativeExpeditionHud.Tone(tone);if(Linear)c=c.linear;
                Points.Add(new Vector3(x,y,z));Uv.Add(new Vector3(u,v,flat));Colors.Add(new Color(c.r*level,c.g*level,c.b*level,alpha));return Points.Count-1;
            }
            public void Triangle(int a,int b,int c){Indices.Add(a);Indices.Add(b);Indices.Add(c);}
            public void Clear(){Points.Clear();Uv.Clear();Colors.Clear();Indices.Clear();}
        }
        public readonly NativeExpeditionWorld World=new NativeExpeditionWorld();
        private readonly Sink sink=new Sink();private Mesh mesh;private Material material;private Texture2D motes;
        private static readonly Bounds Everywhere=new Bounds(Vector3.zero,Vector3.one*4000);
        private NativeExpeditionWorldPresentation(){}
        /// <summary>Null when the beacon shader is missing or unsupported (the pieces' labels still draw).</summary>
        public static NativeExpeditionWorldPresentation Create()
        {
            var shader=Resources.Load<Shader>("NativeBeacon");if(shader==null||!shader.isSupported)return null;
            var p=new NativeExpeditionWorldPresentation{material=new Material(shader){name="Expedition world pieces"}};
            if(p.material.HasProperty("_MainTex"))
            {
                p.motes=new Texture2D(NativeBeaconRules.MoteWidth,NativeBeaconRules.MoteHeight,TextureFormat.RGBA32,true,true){name="Expedition piece motes",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear,hideFlags=HideFlags.HideAndDontSave};
                var pixels=new byte[NativeBeaconRules.MoteWidth*NativeBeaconRules.MoteHeight*4];NativeBeaconRules.Motes(pixels,true);p.motes.SetPixelData(pixels,0);p.motes.Apply(true,true);p.material.mainTexture=p.motes;
            }
            p.mesh=new Mesh{name="Expedition world pieces"};p.mesh.MarkDynamic();return p;
        }
        /// <summary>Builds this frame's pieces for the view and draws the visible ones (call once per frame, before the GUI).</summary>
        public void Frame(ExpeditionView v,Vector3 eye,double t,bool reduced)
        {
            World.Build(v);sink.Clear();sink.Linear=QualitySettings.activeColorSpace==ColorSpace.Linear;
            for(int i=0;i<World.Pieces.Count;i++){var p=World.Pieces[i];if(NativeExpeditionWorld.Visible(p,eye.x,eye.y,eye.z))NativeExpeditionWorld.Emit(sink,p,t,reduced);}
            mesh.Clear();if(sink.Points.Count==0)return;
            mesh.SetVertices(sink.Points);mesh.SetColors(sink.Colors);mesh.SetUVs(0,sink.Uv);mesh.SetTriangles(sink.Indices,0,false);mesh.bounds=Everywhere;
            material.SetFloat("_Scroll",reduced?0:NativeBeaconRules.MoteScroll(t));
            Graphics.DrawMesh(mesh,Matrix4x4.identity,material,0,null,0,null,ShadowCastingMode.Off,false);
        }
        /// <summary>No view: nothing to draw, and no stale pieces for the labels.</summary>
        public void Clear(){World.Pieces.Clear();sink.Clear();mesh?.Clear();}
        public void Dispose(){if(mesh!=null)CapturedAnimation.Release(mesh);if(material!=null)CapturedAnimation.Release(material);if(motes!=null)CapturedAnimation.Release(motes);mesh=null;material=null;motes=null;}
    }

    public sealed partial class ConversionGame
    {
        private NativeExpeditionWorldPresentation expeditionWorld;private bool expeditionWorldFailed;
        private readonly NativeExpeditionWorld expeditionLabelWorld=new NativeExpeditionWorld();
        /// <summary>The pieces of the bound view this frame (the 3D renderer's, or the labels' own when the shader is missing).</summary>
        private NativeExpeditionWorld ExpeditionPieces=>expeditionWorld!=null?expeditionWorld.World:expeditionLabelWorld;
        /// <summary>The world pieces are drawn in the world: a bound view, a camera, and a live scene view (not a menu, intro or replay).</summary>
        // Not under the chip pick screen: its 3D stage renders with the same camera (NativeExpeditionChipScreen.cs, DraftFrame).
        private bool ExpeditionWorldShown=>LiveExpedition!=null&&cameraView!=null&&!menu&&arena!=null&&!IntroActive&&!ReplayActive&&!PvePickScreen;
        /// <summary>LateUpdate: the 3D pieces at the bound view's anchors.</summary>
        private void ExpeditionWorldFrame()
        {
            // The doors, final gate and sanctuary, and the transfer screen's fade (NativeExpeditionStatesPresentation.cs).
            ExpeditionStatesFrame();
            // Nothing at the area's anchors until the arena holds its map (the view names the next area before the load).
            if(!ExpeditionWorldShown||!expeditionMapReady){expeditionWorld?.Clear();expeditionLabelWorld.Pieces.Clear();return;}
            if(expeditionWorld==null&&!expeditionWorldFailed){expeditionWorld=NativeExpeditionWorldPresentation.Create();expeditionWorldFailed=expeditionWorld==null;}
            if(expeditionWorld!=null)expeditionWorld.Frame(LiveExpedition,cameraView.transform.position,Time.unscaledTime,prefs.reducedEffects);
            else expeditionLabelWorld.Build(LiveExpedition);
        }
        private const int MaxLabelled=256;
        private static readonly int[] labelOrder=new int[MaxLabelled];private static readonly float[] labelDepth=new float[MaxLabelled],labelK=new float[MaxLabelled];
        private static readonly Vector2[] labelGlyph=new Vector2[MaxLabelled],labelAt=new Vector2[MaxLabelled];private static readonly bool[] labelShown=new bool[MaxLabelled];
        /// <summary>
        /// The pieces' glyphs and labels at their projected points, under the HUD panels: sized by the view depth, labels within
        /// NativeExpeditionWorld.LabelDistance and decluttered nearest first, hidden behind walls (a raycast, as the beacon labels),
        /// drawn far to near. Reused buffers: no allocation per frame.
        /// </summary>
        private void ExpeditionWorldLabels()
        {
            if(!ExpeditionWorldShown||!expeditionMapReady||Event.current.type!=EventType.Repaint)return;var pieces=ExpeditionPieces.Pieces;if(pieces.Count==0)return;
            float tan=Mathf.Tan(cameraView.fieldOfView*.5f*Mathf.Deg2Rad),half=cameraView.pixelHeight*.5f,t=Time.unscaledTime;Vector3 eye=cameraView.transform.position;
            var physics=arena!=null?arena.Root.scene.GetPhysicsScene():Physics.defaultPhysicsScene;bool reduced=prefs.reducedEffects;int n=0;
            for(int i=0;i<pieces.Count&&n<MaxLabelled;i++)
            {
                var p=pieces[i];if(!NativeExpeditionWorld.Visible(p,eye.x,eye.y,eye.z))continue;
                var centre=new Vector3(p.x,p.y+NativeExpeditionWorld.GlyphHeight(p.kind)+NativeExpeditionWorld.Bob(p,t,reduced),p.z);var s=cameraView.WorldToScreenPoint(centre);if(s.z<=.05f)continue;
                var at=new Vector2(s.x/U.S,(Screen.height-s.y)/U.S);if(at.x<-200||at.x>U.W+200||at.y<-200||at.y>U.H+200)continue;
                Vector3 delta=centre-eye;if(physics.Raycast(eye,delta.normalized,out _,delta.magnitude-.1f,NativeArena.WorldMask,QueryTriggerInteraction.Ignore))continue;
                var label=at;
                if(p.labelAtAnchor){var l=cameraView.WorldToScreenPoint(new Vector3(p.ax,p.ay+NativeExpeditionWorld.GlyphHeight(p.kind),p.az));if(l.z>.05f)label=new Vector2(l.x/U.S,(Screen.height-l.y)/U.S);}
                labelOrder[n]=i;labelDepth[n]=s.z;labelK[n]=half/(s.z*tan)/U.S;labelGlyph[n]=at;labelAt[n]=label;n++;
            }
            // Nearest first claims the label areas, then draw far to near.
            for(int i=1;i<n;i++){int j=i;while(j>0&&labelDepth[j-1]>labelDepth[j]){Swap(j,j-1);j--;}}
            NativeExpeditionHud.LabelsReset();
            for(int i=0;i<n;i++){var p=pieces[labelOrder[i]];labelShown[i]=NativeExpeditionWorld.Labelled(p,eye.x,eye.y,eye.z)&&NativeExpeditionHud.LabelClaim(p,labelAt[i],labelK[i]);}
            // The 3D pieces draw their own diamond, chip tile, machine and crate; a door with 3D leaves this frame (its closure in
            // the view, the layout bound) needs no glyph, any other door keeps the HUD's Door hologram.
            for(int i=n-1;i>=0;i--){var p=pieces[labelOrder[i]];NativeExpeditionHud.Piece(p,labelGlyph[i],labelAt[i],labelK[i],labelShown[i],expeditionWorld==null||p.kind==ExpeditionPieceKind.Door&&!expeditionStates.HasLeaf(p.anchor),1);}
        }
        private static void Swap(int a,int b)
        {
            (labelOrder[a],labelOrder[b])=(labelOrder[b],labelOrder[a]);(labelDepth[a],labelDepth[b])=(labelDepth[b],labelDepth[a]);(labelK[a],labelK[b])=(labelK[b],labelK[a]);
            (labelGlyph[a],labelGlyph[b])=(labelGlyph[b],labelGlyph[a]);(labelAt[a],labelAt[b])=(labelAt[b],labelAt[a]);
        }
    }
}
