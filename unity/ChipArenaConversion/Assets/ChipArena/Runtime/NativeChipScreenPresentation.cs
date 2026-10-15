using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using NVector=System.Numerics.Vector3;
using NVector4=System.Numerics.Vector4;

namespace ChipArena.UnityConversion
{
    /// <summary>One chooser's wrist screen this frame (world space): the open screen, where it is drawn now, its unfold, the wrist
    /// emitter, and a pick's state (the plant's chip where the hand takes the card, its size, the card chosen).</summary>
    public struct ChipScreenState
    {
        public ChipScreenPlace Open,Shown;public float Unfold,PlantAge,CatchSize;public Vector3 Emitter,Catch;public bool Planting;public int Chosen,Count;
    }

    // The Expedition chip screen (Claude, 2026-10-15; owner: "we should also make it a holographic screen projected from the wrist. That
    // way its easier to notice were selecting a chip"; rebuilt for "Holographic display kind of buggy, and display is odd. Can we make it like
    // a better holographic display with no artifacting?"; Core/NativeChipScreen.cs, docs/EXPEDITION-PRESENTATION.md "Chip picks"). In an
    // Expedition every choosing pilot's display wrist projects the chip screen in place of the small wrist panel: mine in my offers' rarities
    // under the faces the HUD draws over it (NativeExpeditionChipScreen.cs), anyone else's as blank cyan backs. The screens are their own
    // mesh drawn with Resources/NativeChipScreen.shader (alpha-blended glass, light budgeted under the bloom: NativeChipScreenLight); the
    // wrist plant's chip and the cuff's pulse still come from the wrist hologram's mesh (its panel hidden; its chip once the hand has the card).
    public sealed partial class ArenaPresentation
    {
        /// <summary>Set by ConversionGame's HUD each frame for my screen: the card lit (the cursor's or the keyboard's; -1 none), the
        /// faces' height (m; 0 the standard) and whether the HUD draws the faces (then my chosen card flies on the HUD, not here).</summary>
        public int ChipScreenLit=-1;public float ChipScreenCardHeight;public bool ChipScreenFaces;
        /// <summary>The screen can be drawn (both shaders load); else the HUD keeps the 2D tray.</summary>
        public bool ChipScreenAvailable=>!chipHoloFailed&&ChipScreenShader();
        // The screens' stream (NativeChipScreen.Build: five UV channels), mesh and material.
        private sealed class ChipScreenSink:INativeChipScreenSink
        {
            public readonly List<Vector3> Points=new List<Vector3>(512);public readonly List<Vector4> A=new List<Vector4>(512),B=new List<Vector4>(512),C=new List<Vector4>(512),D=new List<Vector4>(512),E=new List<Vector4>(512);public readonly List<int> Indices=new List<int>(768);
            public int Vertex(NVector at,NVector4 a,NVector4 b,NVector4 c,NVector4 d,NVector4 e){Points.Add(new Vector3(at.X,at.Y,at.Z));A.Add(V(a));B.Add(V(b));C.Add(V(c));D.Add(V(d));E.Add(V(e));return Points.Count-1;}
            public void Triangle(int a,int b,int c){Indices.Add(a);Indices.Add(b);Indices.Add(c);}
            public void Clear(){Points.Clear();A.Clear();B.Clear();C.Clear();D.Clear();E.Clear();Indices.Clear();}
            private static Vector4 V(NVector4 v)=>new Vector4(v.X,v.Y,v.Z,v.W);
        }
        private readonly ChipScreenSink chipScreenSink=new ChipScreenSink();private Mesh chipScreenMesh;private Material chipScreenMaterial;private bool chipScreenFailed;
        private static readonly int ChipScreenId=Shader.PropertyToID("_ChipScreen");
        // The screen's shader, loaded once: false (and the 2D tray) when it is missing or unsupported.
        private bool ChipScreenShader()
        {
            if(chipScreenMaterial!=null)return true;if(chipScreenFailed)return false;
            var shader=Resources.Load<Shader>("NativeChipScreen");
            if(shader==null||!shader.isSupported){chipScreenFailed=true;Debug.LogWarning("The Expedition chip screen needs shader NativeChipScreen; the 2D chip tray shows instead.");return false;}
            chipScreenMaterial=new Material(shader){name="Native chip screen",hideFlags=HideFlags.HideAndDontSave};materials.Add(chipScreenMaterial);return true;
        }
        // RenderChipSelect, after every look is built: every screen this frame in one draw (back to front within each screen).
        private void DrawChipScreens()
        {
            if(chipScreenSink.Points.Count==0||!ChipScreenShader())return;
            if(chipScreenMesh==null){chipScreenMesh=new Mesh{name="Native chip screen",hideFlags=HideFlags.HideAndDontSave};chipScreenMesh.MarkDynamic();}
            var m=chipScreenMesh;m.Clear();m.SetVertices(chipScreenSink.Points);m.SetUVs(0,chipScreenSink.A);m.SetUVs(1,chipScreenSink.B);m.SetUVs(2,chipScreenSink.C);m.SetUVs(3,chipScreenSink.D);m.SetUVs(4,chipScreenSink.E);m.SetTriangles(chipScreenSink.Indices,0,true);
            chipScreenMaterial.SetVector(ChipScreenId,new Vector4(hazardClock,WeaponEffectsQuality<.5f?0:1,0,LowGraphics?1:0));
            Graphics.DrawMesh(m,Matrix4x4.identity,chipScreenMaterial,0,null,0,null,ShadowCastingMode.Off,false);
        }

        private int BuildChipScreen(ChipHoloSink sink,ChipSelectSlot slot,ChipHoloFrame frame,ChipHoloLook look,int id)
        {
            var open=ScreenPlace(slot);bool mine=id==LocalId;int quads=0;
            // The red edge only for a real damaging hit on this pilot just now (the live arena's events, as the flinch reads them).
            var screen=new ChipScreenLook{Unfold=NativeChipSelectHolo.Unfold(slot.Weight)*(slot.Planting?slot.Plant.Fold:1),Time=slot.Time,Count=mine?slot.Count:3,Lit=mine?ChipScreenLit:-1,Seed=slot.Seed,
                CardHeight=mine?ChipScreenCardHeight:0,Mine=mine,Alert=NativeChipScreen.Alert(ChipHitAge(id,slot.Time)),Reduced=look.Reduced,Low=look.Low,C0=slot.C0,C1=slot.C1,C2=slot.C2,
                Plant=slot.Planting,FlyCard=!mine||!ChipScreenFaces,Faced=mine&&ChipScreenFaces,Chosen=mine?slot.PlantChosen:-1,PlantAge=slot.PlantAge,Fade=slot.Plant.Fade,Catch=frame.Chip,CatchSize=look.ChipSize};
            if(ChipScreenShader())quads+=NativeChipScreen.Build(chipScreenSink,open,frame.Emitter,screen);
            // The plant's chip once the hand has the card (the screen's card flies to it until then) and the cuff's pulse; no wrist panel.
            look.Weight=0;if(slot.PlantAge<NativeDraftPlant.Contact)look.ChipShown=false;
            return quads+NativeChipSelectHolo.Build(sink,frame,look);
        }
        // The open screen from the rig root's frame (its body centre, facing and scale: the rig's own, as the chooser camera's) and the
        // class's side (NativeChipScreen.OnLeft).
        private static ChipScreenPlace ScreenPlace(ChipSelectSlot slot)
        {
            var m=slot.Root;var body=m.MultiplyPoint3x4(Vector3.zero);var f=m.MultiplyVector(Vector3.forward);float facing=Mathf.Atan2(f.x,f.z)*Mathf.Rad2Deg,scale=f.magnitude;
            return NativeChipScreen.Place(new NVector(body.x,body.y,body.z),facing,slot.ScreenLeft,scale>.01f?scale:1);
        }
        /// <summary>Pilot `id`'s wrist screen as last posed (this frame or the one before), in an Expedition; false while its look is down.</summary>
        public bool ChipScreen(int id,out ChipScreenState state)
        {
            state=default;if(ExpeditionChoosing==null||!chipSelect.TryGetValue(id,out var slot)||slot.Frame<Time.frameCount-1||slot.Frame<0)return false;
            var open=ScreenPlace(slot);var m=slot.Root;var e=slot.Pose.Emitter;var emitter=m.MultiplyPoint3x4(new Vector3(-e.X,e.Y,e.Z));var c=slot.Plant.Chip;
            float unfold=NativeChipSelectHolo.Unfold(slot.Weight)*(slot.Planting?slot.Plant.Fold:1);
            state=new ChipScreenState{Open=open,Shown=NativeChipScreen.Shown(open,new NVector(emitter.x,emitter.y,emitter.z),unfold),Unfold=unfold,Emitter=emitter,Planting=slot.Planting,PlantAge=slot.PlantAge,
                Catch=m.MultiplyPoint3x4(new Vector3(-c.X,c.Y,c.Z)),CatchSize=slot.Plant.ChipSize*slot.RootScale,Chosen=slot.PlantChosen,Count=slot.Count};
            return true;
        }
        /// <summary>Pilot `id`'s display wrist (the screen's emitter, world) and the screen's side as last posed, for the chooser camera;
        /// false while its look is down.</summary>
        internal bool ChipSelectWrist(int id,out Vector3 world,out bool screenLeft)
        {
            world=default;screenLeft=false;if(!chipSelect.TryGetValue(id,out var slot)||slot.Frame<Time.frameCount-2||slot.Weight<=.05f)return false;
            var v=slot.Pose.Emitter;world=slot.Root.MultiplyPoint3x4(new Vector3(-v.X,v.Y,v.Z));screenLeft=slot.ScreenLeft;return true;
        }
    }
}
