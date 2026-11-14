using System;
using System.Collections.Generic;
using UnityEngine;
using NVector=System.Numerics.Vector3;

namespace ChipArena.UnityConversion
{
    // The Expedition mutators' presentation (Claude, 2026-11-02; docs/EXPEDITION-DEPLOYMENT.md "Mutator looks"), on the run's own
    // mutators (GeneratedRun.Mutators: the ticket every peer rebuilds) and Astra's replicated mutator events:
    //  - MutatorFx: STORM's warning and strike, BLOATED's burst and ROULETTE's glints (Core NativeMutatorFx) as one effects batch;
    //    the storm's, HIGH EXPLOSIVE's and MARTYR's blasts go through the shared blast pools (NativeBlastPresentation.Impacts).
    //  - DARKNESS and WHITEOUT (Core NativeMutatorLook) through NativeLighting.Mutators, the town's lamps and lit windows and the
    //    world markers' reach.
    //  - BIG HEADS: every head drawn larger (VisualBigHead, ZombieBigHead, HordeBigHead and the voxel pilots' Bone).
    // Cosmetic only; nothing here touches the match.
    public sealed partial class ArenaPresentation
    {
        private readonly NativeMutatorFx mutatorFx=new NativeMutatorFx();private NativeFxDraw mutatorDraw;private bool mutatorDrawFailed;
        private int mutatorEvent,mutatorGeneration=-1;private float mutatorClock=float.NegativeInfinity;private readonly RaycastHit[] mutatorFloor=new RaycastHit[16];
        /// <summary>This frame's run has DARKNESS, WHITEOUT or BIG HEADS (from GeneratedRun.Mutators; off outside a generated run).</summary>
        public bool MutatorDark{get;private set;}public bool MutatorWhite{get;private set;}public bool MutatorBigHeads{get;private set;}
        /// <summary>The mutator effects' quads drawn last frame (budget lines).</summary>
        public int MutatorQuads=>mutatorFx.Quads.Count;
        private float HeadScale=>MutatorBigHeads?NativeMutatorLook.HeadScale:1;

        // From Render's effects pass: the run's look mutators, then its mutator events' looks.
        private void MutatorFx(NativeArena arena,Camera camera,float now)
        {
            var ids=GeneratedRun?.Mutators;MutatorDark=NativeMutatorLook.Dark(ids);MutatorWhite=NativeMutatorLook.White(ids);MutatorBigHeads=NativeMutatorLook.BigHeads(ids);
            NativeLighting.Mutators(camera,MutatorDark,MutatorWhite);if(townFx!=null)townFx.LampsOut=MutatorDark;
            if(arena==null||camera==null)return;
            // A rewind (killcam, new round) clears.
            if(mutatorGeneration!=arena.Generation||now<mutatorClock-.25f){mutatorFx.Clear();mutatorGeneration=arena.Generation;mutatorEvent=0;mutatorClock=float.NegativeInfinity;}
            mutatorClock=Mathf.Max(mutatorClock,now);bool world=NativeBlastPresentation.World(arena,out var physics);
            foreach(var e in arena.Events)
            {
                if(e.Id<=mutatorEvent)continue;mutatorEvent=e.Id;
                if(e.Kind!=EventKind.Impact||!NativeMutatorFx.Event(e.Detail)||now-e.Time>NativeMutatorFx.Life(e.Detail,e.Amount))continue;
                // The floor under it (a storm's mark and scorch, a burst's spot); ROULETTE spins round the pilot's body where it stood.
                var at=NativeArena.Vec(e.Position);float floor=at.y-.95f;
                if(world){double f=NativeBlastPresentation.FloorUnder(physics,at,mutatorFloor);if(f>NativeBlastSim.NoFloor*.5)floor=(float)f;}
                var p=e.Detail==NativeMutatorFx.Roulette?new NVector(at.x,at.y,at.z):new NVector(at.x,floor,at.z);
                mutatorFx.Add(e.Detail,p,e.Time,e.Amount,e.Radius,e.Id);
            }
            var c=camera.transform;Vector3 eye=c.position,right=c.right,up=c.up;
            mutatorFx.Build(now,new NVector(eye.x,eye.y,eye.z),new NVector(right.x,right.y,right.z),new NVector(up.x,up.y,up.z),WeaponEffectsQuality<.5f,Mathf.Clamp01(EffectsQuality));
            if(mutatorFx.Quads.Count==0||mutatorDrawFailed)return;
            if(mutatorDraw==null){try{mutatorDraw=new NativeFxDraw();}catch(InvalidOperationException ex){mutatorDrawFailed=true;Debug.LogWarning("Mutator effects disabled: "+ex.Message);return;}}
            var q=mutatorFx.Quads;mutatorDraw.Clear();var layer=mutatorDraw.Effects;
            for(int i=0;i<q.Count;i++)
            {
                int o=i*12,k=i*4;float flags=NativeFxDraw.Authored+(q.Additive[i]!=0?NativeFxDraw.Additive:0),mode=q.Modes[i];
                var color=new Color(q.Colors[k],q.Colors[k+1],q.Colors[k+2],q.Colors[k+3]).gamma;int a=layer.Count;
                for(int v=0;v<4;v++)layer.Vertex(new Vector3(q.Corners[o+v*3],q.Corners[o+v*3+1],q.Corners[o+v*3+2]),Vector3.up,color,new Vector4(v==1||v==2?1:-1,v>=2?1:-1,mode,flags),Vector4.zero,Vector4.zero);
                layer.Triangle(a,a+1,a+2);layer.Triangle(a,a+2,a+3);
            }
            mutatorDraw.Draw(camera,null,now);
        }

        // ---- BIG HEADS ----
        private readonly Dictionary<Transform,Vector3> visualHeadRest=new Dictionary<Transform,Vector3>();
        // A modelled pilot's head at its rest scale times the run's head scale (VisualBodyPose, after the last bone write).
        private void VisualBigHead(VisualBody body)
        {
            var head=VisualHeadOf(body);if(head==null)return;
            if(!visualHeadRest.TryGetValue(head,out var rest)){rest=head.localScale;visualHeadRest[head]=rest;}
            var want=rest*HeadScale;if(head.localScale!=want)head.localScale=want;
        }
        // A modelled zombie's head (ZombieModelBind, after ZombieCollapse): unless the head is gone (collapsed by a burst or a sever).
        private void ZombieBigHead(ZombieBody z)
        {
            var head=z.Joint[CapturedRig.Head];if(head==null||(z.Hidden&(1<<CapturedRig.Head))!=0)return;
            var want=z.JointScale[CapturedRig.Head]*HeadScale;if(head.localScale!=want)head.localScale=want;
        }
        // A voxel horde rig's head (after its pose is written: the writer sets positions and turns only).
        private void HordeBigHead(CapturedAnimation.Player rig)
        {var head=rig.Bones[CapturedRig.Head];var want=Vector3.one*HeadScale;if(head.localScale!=want)head.localScale=want;}
        // A voxel pilot's bone matrix: its head part drawn about the head joint at the run's head scale.
        private Matrix4x4 BigHeadBone(CapturedAnimation.Player rig,int b)
        {var m=rig.Bones[b].localToWorldMatrix;return b==CapturedRig.Head&&MutatorBigHeads?m*Matrix4x4.Scale(Vector3.one*NativeMutatorLook.HeadScale):m;}
    }

    public sealed partial class ConversionGame
    {
        // The run's active mutators on the Expedition HUD: a chip each in its polarity's colour, right-aligned ending at `right`
        // (left of the J tag), top row.
        private void ExpeditionMutatorChips(string[] ids,float right)
        {
            if(ids==null||ids.Length==0)return;float x=right;
            for(int i=ids.Length-1;i>=0;i--)
            {
                var m=NativeDeployPresentation.MutatorOf(ids[i]);if(string.IsNullOrEmpty(m.Name))continue;var tone=NativeUi.Hex(m.Color);
                float w=NativeUi.Measure(m.Name,10,true,true,.1f).x+24;x-=w;var r=new Rect(x,24,w,28);
                NativeHudChrome.Tag(r,tone,.9f);NativeUi.Text(r,m.Name,10,tone,TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.1f);x-=6;
            }
        }
    }
}
