using System;
using System.Collections.Generic;
using UnityEngine;
using NVector=System.Numerics.Vector3;

namespace ChipArena.UnityConversion
{
    // The wall breach entrance (owner 2026-10-13: "They can also break through the walls to enter (Stage will eventually
    // holographically recover broken pieces)"), Core/NativeBreach's contract drawn: generic and data driven, an add whose
    // replicated entry is a map entry of kind "breach" breaks the wall segment the map names "breach:<entry id>" (its renderers;
    // its collider is the authority's to remove and restore, never touched here). From the add's replicated SpawnAt / ActiveAt
    // (and the breach's RestoreAt once replicated):
    //  - The knocks: the segment's renderers are drawn by the breach (hidden, then drawn shuddering on each blow from behind,
    //    cracks spreading over it and dust sifting out of them).
    //  - The break: the segment is drawn as its broken panel (Core/NativeBreach.Hole: the wall less a ragged hole the add's size,
    //    in the segment's own material) while its pieces burst into the arena and tumble to rest, a burst of dust with them.
    //  - The holographic restore: a hardlight scan sweeps the hole bottom to top, the pieces dissolve into hologram wireframes,
    //    fly back to their places and solidify; then the segment's own renderers come back (a last hardlight flash hides the swap).
    // With no named segment in the map the breach still bursts and restores its pieces where the entry crosses, hiding nothing.
    // A Hulk's breach of an interior wall block it cannot fit past (owner 2026-10-13: "make the hulk punch through any wall in ward
    // zero if he cant fit"; a dynamic NativeBreachRecord from arena.WardBreaches, read alike by the host, guests and the killcam's
    // replay arena) runs the same timeline on that block, found once by its definition id: the Hulk (the record's actor) pounds it
    // on the three knocks from its side (cracks on both faces from the blow, dust, the block shuddering along the Hulk's facing:
    // on the revamp through its dressing, NudgeWardBlock, so its look stays); at the break the whole block goes (its renderers,
    // its block dressing and its kit parts hidden: the authority removes its whole collider) and its pieces, which tile the whole
    // block (Core NativeBreach.BuildBlock), burst out along the facing with dust; the hologram rebuilds the whole block, then its
    // own look comes back. Keyed by the block and the break, seeded from both (NativeBreach.Seed), so every peer draws the same.
    // The pools and meshes are made once per breach (lazily, never in a field initializer); nothing is allocated per frame.
    public sealed partial class ArenaPresentation
    {
        private sealed class HordeBreachState
        {
            public bool Live,Panel,Hidden;public PveEntry Entry;public float SpawnAt,BreakAt,RestoreAt;public uint Seed;public int Kind;
            public NativeBreach.Hole Hole;public Renderer[] Renderers;public MeshFilter[] Filters;public Material[][] Materials;public Material Material;public Mesh PanelMesh;public int Layer;
            // A Hulk's breach of a wall block (a dynamic record): the block (its key, with BreakAt), the Hulk pounding it, and
            // whether the knock's shudder is on the block's dressing this frame.
            public bool Dynamic,Nudged;public string Block;public int Actor;
        }
        private readonly HordeBreachState[] hordeBreaches=MakeBreaches();
        private static HordeBreachState[] MakeBreaches(){var b=new HordeBreachState[64];for(int i=0;i<b.Length;i++)b[i]=new HordeBreachState();return b;}
        private Mesh hordeBreachCube;
        /// <summary>The stage's hardlight (the restore's hologram).</summary>
        private static readonly Color HardlightColor=new Color(.38f,.92f,1f);

        /// <summary>HordeKindAdd (and the gallery): an add entering through a breach this frame; the breach starts on its first sight.</summary>
        private void HordeBreachJob(NativeArena arena,PveEntry entry,float spawnAt,float breakAt,Vector3 from,Vector3 exit,int kind,uint seed,GameObject segment=null)
        {
            if(entry==null)return;HordeBreachState free=null;
            for(int i=0;i<hordeBreaches.Length;i++){var b=hordeBreaches[i];if(b.Live&&!b.Dynamic&&b.Entry.e==entry.e&&b.Entry.id==entry.id){if(Mathf.Abs(b.SpawnAt-spawnAt)<.05f)return;HordeBreachRelease(b);free=b;break;}if(!b.Live&&free==null)free=b;}
            if(free==null)return;
            var s=free;s.Live=true;s.Entry=entry;s.SpawnAt=spawnAt;s.BreakAt=breakAt;s.RestoreAt=NativeBreach.DefaultRestoreAt(breakAt);s.Seed=seed;s.Kind=kind;s.Hidden=false;s.Dynamic=false;s.Nudged=false;s.Block=null;s.Actor=0;
            // The map's segment (by name; looked up once per breach), else the wall the entry implies.
            var go=segment;
            if(go==null)foreach(var prop in arena.AllBlocks)if(prop!=null&&prop.gameObject.name==NativeBreach.ObjectName(entry.id)){go=prop.gameObject;break;}
            s.Renderers=null;s.Filters=null;s.Materials=null;s.Material=null;s.Panel=false;
            NativeBreach.Wall wall;
            if(go!=null)
            {
                int n=BreachMeshes(s,go);
                if(n>0)
                {
                    var f=s.Filters[0];var t=f.transform;var size=Vector3.Scale(f.sharedMesh.bounds.size,t.lossyScale);var c=t.TransformPoint(f.sharedMesh.bounds.center);
                    wall=NativeBreach.WallOf(ToN(c),new System.Numerics.Quaternion(t.rotation.x,t.rotation.y,t.rotation.z,t.rotation.w),ToN(size),ToN(from),ToN(exit));
                    s.Material=s.Materials[0].Length>0?s.Materials[0][0]:null;s.Panel=s.Material!=null;
                }
                else wall=NativeBreach.WallOf(ToN(from),ToN(exit),exit.y,4.2f,4.2f,.5f);
            }
            else wall=NativeBreach.WallOf(ToN(from),ToN(exit),exit.y,4.2f,4.2f,.5f);
            // Where the add crosses the wall: the entry's line through the wall's plane.
            var n0=new Vector3(wall.Normal.X,0,wall.Normal.Z);var wc=new Vector3(wall.Center.X,wall.Center.Y,wall.Center.Z);var line=exit-from;float den=Vector3.Dot(line,n0);
            var cross=Mathf.Abs(den)>1e-4f?from+line*Mathf.Clamp01(Vector3.Dot(wc-from,n0)/den):(from+exit)*.5f;
            s.Hole=NativeBreach.Build(wall,ToN(cross),NativeHordeKinds.WorldRadius(kind),NativeHordeKinds.WorldHeight(kind),seed);
            if(s.Panel)
            {
                if(s.PanelMesh==null)s.PanelMesh=new Mesh{name="Siren breach panel"};else s.PanelMesh.Clear();
                var h=s.Hole;var v=new Vector3[h.Positions.Length];var nn=new Vector3[v.Length];var uv=new Vector2[v.Length];
                for(int i=0;i<v.Length;i++){v[i]=new Vector3(h.Positions[i].X,h.Positions[i].Y,h.Positions[i].Z);nn[i]=new Vector3(h.Normals[i].X,h.Normals[i].Y,h.Normals[i].Z);uv[i]=new Vector2(h.Uvs[i].X,h.Uvs[i].Y);}
                s.PanelMesh.vertices=v;s.PanelMesh.normals=nn;s.PanelMesh.uv=uv;s.PanelMesh.triangles=h.Triangles;s.PanelMesh.RecalculateBounds();
            }
        }
        // The segment's meshes and materials (its renderers are the breach's to hide, and to draw while it shudders).
        private static int BreachMeshes(HordeBreachState s,GameObject go)
        {
            var filters=go.GetComponentsInChildren<MeshFilter>();var renderers=new Renderer[filters.Length];var mats=new Material[filters.Length][];int n=0;
            for(int i=0;i<filters.Length;i++){var r=filters[i].GetComponent<MeshRenderer>();if(r==null||filters[i].sharedMesh==null)continue;filters[n]=filters[i];renderers[n]=r;mats[n]=r.sharedMaterials;n++;}
            System.Array.Resize(ref filters,n);System.Array.Resize(ref renderers,n);System.Array.Resize(ref mats,n);
            s.Filters=filters;s.Renderers=renderers;s.Materials=mats;s.Layer=go.layer;return n;
        }
        private void HordeBreachFrames(NativeArena arena)
        {
            // A plain loop over the arena's list: a foreach through the IEnumerable would box its enumerator on every drawn frame.
            if(arena.WardBreaches is List<NativeBreachRecord> list){for(int i=0;i<list.Count;i++)HordeBreachFrame(arena,list[i]);return;}
            foreach(var frame in arena.WardBreaches)HordeBreachFrame(arena,frame);
        }
        private void HordeBreachFrame(NativeArena arena,NativeBreachRecord frame)
        {
            if(frame==null||arena.Time>NativeBreach.SolidAt(frame.restoreAt)+.4f)return;
            if(frame.Dynamic){HordeWallJob(arena,frame);return;}
            // A plain search: the frame's capture would allocate a closure on every drawn frame.
            PveEntry entry=null;foreach(var e in NativeArena.StageContent.entries)if(e!=null&&e.e==frame.entry){entry=e;break;}
            if(entry==null)return;
            float spawn=frame.breakAt-NativeBreach.Knock;
            HordeBreachJob(arena,entry,spawn,frame.breakAt,NativeArena.Vec(entry.from.Position()),NativeArena.Vec(entry.@exit.Position()),NativeHordeKinds.Hulk,(uint)(frame.entry+1));
            foreach(var b in hordeBreaches)if(b.Live&&!b.Dynamic&&b.Entry.e==frame.entry&&Mathf.Abs(b.BreakAt-frame.breakAt)<.01f)b.RestoreAt=frame.restoreAt;
        }
        /// <summary>
        /// HordeBreachFrames: a Hulk's breach of a wall block (a dynamic record), keyed by its block and its break. The first sight
        /// finds the block by its definition id in the arena's static blocks (else the map's definition alone: nothing to hide)
        /// and builds the whole block's wall and pieces (Core NativeBreach.BlockWall / BuildBlock, from the record's punch point,
        /// facing and full opening, the block's centre and the floor under it); later frames only take the replicated restore.
        /// </summary>
        private void HordeWallJob(NativeArena arena,NativeBreachRecord r)
        {
            HordeBreachState free=null;
            for(int i=0;i<hordeBreaches.Length;i++){var b=hordeBreaches[i];if(b.Live&&b.Dynamic&&string.Equals(b.Block,r.block,StringComparison.Ordinal)){if(Mathf.Abs(b.BreakAt-r.breakAt)<.01f){b.RestoreAt=r.restoreAt;b.Actor=r.actor;return;}HordeBreachRelease(b);free=b;break;}if(!b.Live&&free==null)free=b;}
            if(free==null)return;
            NativePropBody prop=null;var statics=arena.StaticBlocks;for(int i=0;i<statics.Count;i++){var p=statics[i];if(p!=null&&p.Definition!=null&&p.Definition.id==r.block){prop=p;break;}}
            var blocks=arena.Map?.blocks;var def=prop!=null?prop.Definition:null;if(def==null&&blocks!=null)for(int i=0;i<blocks.Length;i++)if(blocks[i]!=null&&blocks[i].id==r.block){def=blocks[i];break;}
            if(def==null)return;
            var s=free;s.Live=true;s.Dynamic=true;s.Entry=null;s.Block=r.block;s.Actor=r.actor;s.SpawnAt=NativeBreach.KnockAt(r.breakAt);s.BreakAt=r.breakAt;s.RestoreAt=r.restoreAt;s.Seed=NativeBreach.Seed(r.block,r.breakAt);s.Kind=NativeHordeKinds.Hulk;s.Hidden=false;s.Nudged=false;
            s.Renderers=null;s.Filters=null;s.Materials=null;s.Material=null;s.Panel=false;
            if(prop!=null&&BreachMeshes(s,prop.gameObject)>0){s.Material=s.Materials[0].Length>0?s.Materials[0][0]:null;s.Panel=s.Material!=null;}
            var q=def.rotation!=null?new System.Numerics.Quaternion(def.rotation.x,def.rotation.y,def.rotation.z,def.rotation.w):System.Numerics.Quaternion.Identity;
            var point=new NVector(r.point.x,r.point.y,r.point.z);var normal=new NVector(r.normal.x,r.normal.y,r.normal.z);
            var wall=NativeBreach.BlockWall(new NVector(def.pos.x,def.pos.y,def.pos.z),q,new NVector(r.holeSize.x,r.holeSize.y,r.holeSize.z),point,normal);
            s.Hole=NativeBreach.BuildBlock(wall,point,normal,NativeBreach.FloorUnder(def,blocks),s.Seed);
        }
        /// <summary>HordeKindAdd: whether Hulk `actor` is pounding a wall block at `now` (a dynamic breach still knocking) and when
        /// its knock began (the three blows land on NativeBreach.Blows after it).</summary>
        private bool HordeWallKnock(int actor,float now,out float knockAt)
        {
            for(int i=0;i<hordeBreaches.Length;i++){var b=hordeBreaches[i];if(b.Live&&b.Dynamic&&b.Actor==actor&&now>=b.SpawnAt&&now<b.BreakAt){knockAt=b.SpawnAt;return true;}}
            knockAt=0;return false;
        }
        // Whether the revamp's block dressing draws the block (its renderers forced off): the knock then shudders the dressing.
        private static bool BreachDressed(HordeBreachState b)=>b.Renderers!=null&&b.Renderers.Length>0&&b.Renderers[0]!=null&&b.Renderers[0].forceRenderingOff;
        // The wall's frame as a Unity matrix (local x along it, y up, z out into the arena).
        private static Matrix4x4 BreachFrame(in NativeBreach.Wall w)
        {
            var m=Matrix4x4.identity;m.SetColumn(0,new Vector4(w.Right.X,w.Right.Y,w.Right.Z,0));m.SetColumn(1,new Vector4(0,1,0,0));m.SetColumn(2,new Vector4(w.Normal.X,w.Normal.Y,w.Normal.Z,0));m.SetColumn(3,new Vector4(w.Center.X,w.Center.Y,w.Center.Z,1));return m;
        }
        private static Vector3 BreachPoint(in NativeBreach.Wall w,float x,float y,float z){var p=w.ToWorld(new NVector(x,y,z));return new Vector3(p.X,p.Y,p.Z);}
        // The revamped Ward Zero draws its panels through the block dressing (renderers forced off), so each hide and show is also
        // reported to it (NativeWardZeroPresentation.SetWardBreach; nothing happens off that map).
        // A wall block (a dynamic breach) is reported by its block id (SetWardBlockBreach: the dressing and every kit part on it).
        private void HordeBreachHide(HordeBreachState b){if(b.Hidden||b.Renderers==null)return;for(int i=0;i<b.Renderers.Length;i++)if(b.Renderers[i]!=null)b.Renderers[i].enabled=false;b.Hidden=true;if(b.Dynamic)SetWardBlockBreach(b.Block,true);else SetWardBreach(b.Entry?.id,true);}
        private void HordeBreachShow(HordeBreachState b){HordeBreachSettle(b);if(!b.Hidden)return;if(b.Renderers!=null)for(int i=0;i<b.Renderers.Length;i++)if(b.Renderers[i]!=null)b.Renderers[i].enabled=true;b.Hidden=false;if(b.Dynamic)SetWardBlockBreach(b.Block,false);else SetWardBreach(b.Entry?.id,false);}
        // The knock's shudder off the block's dressing again (it stands still: broken, whole, or released).
        private void HordeBreachSettle(HordeBreachState b){if(!b.Nudged)return;b.Nudged=false;NudgeWardBlock(b.Block,Vector3.zero);}
        private void HordeBreachRelease(HordeBreachState b){HordeBreachShow(b);b.Live=false;b.Entry=null;b.Block=null;b.Dynamic=false;}

        /// <summary>SourceWorld (HordeKindsEnd): the breaches' solid draws (the shuddering segment, the broken panel, the pieces).</summary>
        private void HordeBreachesDraw(float now)
        {
            for(int i=0;i<hordeBreaches.Length;i++)
            {
                var b=hordeBreaches[i];if(!b.Live)continue;var phase=NativeBreach.At(now,b.SpawnAt,b.BreakAt,b.RestoreAt,out float into);var w=b.Hole.Wall;
                if(phase==NativeBreach.Phase.Intact){HordeBreachShow(b);continue;}
                if(phase==NativeBreach.Phase.Solid){if(into>.4f)HordeBreachRelease(b);else HordeBreachShow(b);continue;}
                if(b.Renderers==null||b.Renderers.Length==0)continue;
                // A wall block the revamp's dressing draws shudders there (its look kept, its transform and collider never moved);
                // it leaves every draw at the break.
                if(b.Dynamic&&phase==NativeBreach.Phase.Knock&&BreachDressed(b)){b.Nudged=true;NudgeWardBlock(b.Block,new Vector3(w.Normal.X,0,w.Normal.Z)*NativeBreach.Shudder(into));continue;}
                HordeBreachSettle(b);HordeBreachHide(b);
                if(phase==NativeBreach.Phase.Knock)
                {
                    // The segment as it is, shuddering on each blow from behind.
                    float sh=NativeBreach.Shudder(into);var push=Matrix4x4.Translate(new Vector3(w.Normal.X,0,w.Normal.Z)*sh);
                    for(int r=0;r<b.Filters.Length;r++){var f=b.Filters[r];if(f==null)continue;var m=push*f.transform.localToWorldMatrix;var mats=b.Materials[r];for(int k=0;k<mats.Length&&k<f.sharedMesh.subMeshCount;k++)if(mats[k]!=null)Graphics.DrawMesh(f.sharedMesh,m,mats[k],b.Layer,null,k);}
                    continue;
                }
                if(!b.Panel)continue;
                // (A whole block leaves no panel: only its pieces.)
                var frame=BreachFrame(w);if(!b.Hole.Full)Graphics.DrawMesh(b.PanelMesh,frame,b.Material,b.Layer);
                hordeBreachCube??=BreachCube();float sinceBreak=now-b.BreakAt;
                for(int k=0;k<b.Hole.Chunks;k++)
                {
                    NativeBreach.PieceAt(b.Hole,k,phase,into,sinceBreak,out var at,out var spin,out float holo,out float solid);if(holo>0&&solid<.02f)continue;
                    var m=frame*Matrix4x4.Translate(new Vector3(at.X,at.Y,at.Z))*Matrix4x4.Rotate(Quaternion.Euler(spin.X*Mathf.Rad2Deg,spin.Y*Mathf.Rad2Deg,spin.Z*Mathf.Rad2Deg))*Matrix4x4.Scale(new Vector3(b.Hole.Size[k].X,b.Hole.Size[k].Y,b.Hole.Size[k].Z));
                    Graphics.DrawMesh(hordeBreachCube,m,b.Material,b.Layer);
                }
            }
        }
        /// <summary>DevEffects (from DrawHordeKindFx): the cracks, dust, the pieces without a material, and the hologram restore.</summary>
        private void DrawHordeBreachFx(NativeFxDraw draw,float now,Vector3 eye,Vector3 right,Vector3 up,bool low)
        {
            var dust=new Color(.45f,.41f,.47f);
            for(int i=0;i<hordeBreaches.Length;i++)
            {
                var b=hordeBreaches[i];if(!b.Live)continue;var h=b.Hole;var w=h.Wall;var phase=NativeBreach.At(now,b.SpawnAt,b.BreakAt,b.RestoreAt,out float into);float face=w.Thickness*.5f+.012f;
                if(h.Full){DrawBlockBreachFx(draw,b,phase,into,now,eye,right,up,low,dust);continue;}
                Vector3 hole=BreachPoint(w,h.Center.X,h.Center.Y,face);
                if(phase==NativeBreach.Phase.Knock)
                {
                    float cracked=NativeBreach.Cracked(into);
                    for(int k=0;k<NativeBreach.CrackLines;k++)if(NativeBreach.Crack(h,k,cracked,out var ca,out var cb,out float cw))
                    {Vector3 p0=BreachPoint(w,ca.X,ca.Y,face),p1=BreachPoint(w,cb.X,cb.Y,face);var side=Vector3.Cross(p1-p0,new Vector3(w.Normal.X,w.Normal.Y,w.Normal.Z)).normalized*cw;draw.Quad(draw.Effects,p0-side,p1-side*.2f,p1+side*.2f,p0+side,new Color(.02f,.015f,.02f,.9f),NativeFxDraw.Authored);}
                    // Dust shaken out on each blow.
                    for(int k=0;k<NativeBreach.Blows.Length-1;k++){float a=into-NativeBreach.Blows[k];if(a<0||a>.8f)continue;for(int d=0;d<6;d+=low?2:1){float hx=(NativeGoreRules.FnvDigits(d+k*7)%100/100f-.5f)*h.HalfWidth*1.6f;var p=BreachPoint(w,h.Center.X+hx,h.Center.Y+h.HalfHeight*(.2f+.12f*d)-a*a*2.5f,face+.05f+a*.4f);draw.Sprite(draw.Effects,p,right,up,.22f+.35f*a,Alpha(dust,.5f*(1-a/.8f)),NativeFxDraw.Authored);}}
                    continue;
                }
                float sinceBreak=now-b.BreakAt;
                if(phase==NativeBreach.Phase.Broken&&into<1.6f)
                {
                    // The burst: dust thrown out of the hole into the arena, a flash.
                    for(int d=0;d<14;d+=low?2:1){float ang=d*2.39996f,rr=(.3f+.7f*((d*37)%10)/10f);float out_=into*(2.2f+1.5f*rr);var p=BreachPoint(w,h.Center.X+Mathf.Cos(ang)*h.HalfWidth*rr,Mathf.Max(-w.Height*.5f+.2f,h.Center.Y+Mathf.Sin(ang)*h.HalfHeight*rr*.8f-into*.5f),face+out_);draw.Sprite(draw.Effects,p,right,up,(.5f+1.4f*into)*(.7f+.3f*rr),Alpha(dust,.55f*(1-into/1.6f)),NativeFxDraw.Authored);}
                    if(into<.15f)draw.Sprite(draw.Effects,hole,right,up,3f*(1-into/.15f),new Color(1,.9f,.8f,.6f*(1-into/.15f)),NativeFxDraw.Authored+NativeFxDraw.Additive);
                }
                // Without the segment's material the pieces are drawn here (a stone colour).
                if(!b.Panel&&phase!=NativeBreach.Phase.Solid)for(int k=0;k<h.Chunks;k++)
                {
                    NativeBreach.PieceAt(h,k,phase,into,sinceBreak,out var at,out var spin,out float holo,out float solid);if(holo>0&&solid<.02f)continue;
                    var wc=w.ToWorld(at);draw.Cube(draw.Solid,new Vector3(wc.X,wc.Y,wc.Z),Quaternion.LookRotation(new Vector3(w.Normal.X,0,w.Normal.Z))*Quaternion.Euler(spin.X*Mathf.Rad2Deg,spin.Y*Mathf.Rad2Deg,spin.Z*Mathf.Rad2Deg),new Vector3(h.Size[k].X,h.Size[k].Y,h.Size[k].Z),new Color(.3f,.29f,.31f),NativeFxDraw.Authored);
                }
                if(phase==NativeBreach.Phase.Rebuild)
                {
                    // The hardlight scan sweeping the hole, the grid it leaves, the pieces' wireframes flying home and lighting up as they set.
                    if(NativeBreach.Scan(h,into,out float sy,out float ss))
                    {
                        Vector3 l0=BreachPoint(w,h.Center.X-h.HalfWidth*1.15f,sy,face+.02f),l1=BreachPoint(w,h.Center.X+h.HalfWidth*1.15f,sy,face+.02f);var lift=Vector3.up*.025f;
                        draw.Quad(draw.Effects,l0-lift,l1-lift,l1+lift,l0+lift,Alpha(HardlightColor,.95f*ss),NativeFxDraw.Authored+NativeFxDraw.Additive);
                        Vector3 g0=BreachPoint(w,h.Center.X-h.HalfWidth*1.15f,h.Center.Y-h.HalfHeight*1.1f,face+.015f),g1=BreachPoint(w,h.Center.X+h.HalfWidth*1.15f,h.Center.Y-h.HalfHeight*1.1f,face+.015f);
                        draw.Quad(draw.Effects,g0,g1,l1,l0,Alpha(HardlightColor,.08f*ss),NativeFxDraw.Authored+NativeFxDraw.Additive);
                        for(int gx=0;gx<7;gx++){float x=h.Center.X-h.HalfWidth*1.15f+gx*h.HalfWidth*2.3f/6;HordeStreak(draw,BreachPoint(w,x,h.Center.Y-h.HalfHeight*1.1f,face+.02f),BreachPoint(w,x,sy,face+.02f),.008f,Alpha(HardlightColor,.35f*ss),eye);}
                    }
                    for(int k=0;k<h.Chunks;k++)
                    {
                        NativeBreach.PieceAt(h,k,phase,into,sinceBreak,out var at,out var spin,out float holo,out float solid);NativeBreach.Restore(h,k,into,out float back,out _);if(back<=0)continue;
                        float edge=holo>0?.9f*holo:.6f*(1-solid);if(edge<.02f)continue;
                        HordeWireBox(draw,w,at,spin,h.Size[k],Alpha(HardlightColor,edge),eye);
                    }
                }
                if(phase==NativeBreach.Phase.Solid&&into<.4f)
                {
                    // The hardlight flash as the segment's own renderers come back: a fan over the hole's ragged outline
                    // (clamped to the wall's foot where the rag opened onto the floor).
                    float k=1-into/.4f,bottom=-w.Height*.5f;var flash=Alpha(HardlightColor,.3f*k);var c0=BreachPoint(w,h.Center.X,h.Center.Y,face+.02f);
                    for(int e=0;e<NativeBreach.Outline;e++)
                    {
                        var r0=h.Rim[e];var r1=h.Rim[(e+1)%NativeBreach.Outline];
                        var p0=BreachPoint(w,h.Center.X+r0.X,Mathf.Max(bottom,h.Center.Y+r0.Y),face+.02f);var p1=BreachPoint(w,h.Center.X+r1.X,Mathf.Max(bottom,h.Center.Y+r1.Y),face+.02f);
                        draw.Quad(draw.Effects,c0,c0,p1,p0,flash,NativeFxDraw.Authored+NativeFxDraw.Additive);
                    }
                }
            }
        }
        // A whole wall block's effects (a dynamic breach): the cracks on both faces from the blow, the blow's puff off the Hulk's
        // face and grit out of the far one, a shake per blow; the burst out of the struck spot and the dust of the block's foot as it
        // slumps; the pieces without a material; the scan, its grid and the wireframes on both faces; the whole face's flash as the
        // block's own look comes back.
        private void DrawBlockBreachFx(NativeFxDraw draw,HordeBreachState b,NativeBreach.Phase phase,float into,float now,Vector3 eye,Vector3 right,Vector3 up,bool low,Color dust)
        {
            var h=b.Hole;var w=h.Wall;float face=w.Thickness*.5f+.012f;var normal=new Vector3(w.Normal.X,w.Normal.Y,w.Normal.Z);
            if(phase==NativeBreach.Phase.Knock)
            {
                float cracked=NativeBreach.Cracked(into);
                for(int k=0;k<NativeBreach.CrackLines;k++)if(NativeBreach.Crack(h,k,cracked,out var ca,out var cb,out float cw))for(int f=-1;f<=1;f+=2)
                {Vector3 p0=BreachPoint(w,ca.X,ca.Y,face*f),p1=BreachPoint(w,cb.X,cb.Y,face*f);var side=Vector3.Cross(p1-p0,normal).normalized*cw;draw.Quad(draw.Effects,p0-side,p1-side*.2f,p1+side*.2f,p0+side,new Color(.02f,.015f,.02f,.9f),NativeFxDraw.Authored);}
                for(int k=0;k<NativeBreach.Blows.Length-1;k++)
                {
                    float a=into-NativeBreach.Blows[k];if(a<0||a>.8f)continue;
                    for(int d=0;d<6;d+=low?2:1)
                    {
                        float hx=NativeGoreRules.FnvDigits(d+k*7)%100/100f-.5f,hy=NativeGoreRules.FnvDigits(d+k*7+40)%100/100f-.5f;
                        draw.Sprite(draw.Effects,BreachPoint(w,h.Strike.X+hx*(.9f+1.4f*a),h.Strike.Y+hy*.8f-a*a*1.5f,-(face+.06f+a*.6f)),right,up,.24f+.5f*a,Alpha(dust,.55f*(1-a/.8f)),NativeFxDraw.Authored);
                        draw.Sprite(draw.Effects,BreachPoint(w,h.Strike.X+hx*1.3f,h.Strike.Y+hy*.6f-a*a*2.5f,face+.05f+a*.35f),right,up,.18f+.3f*a,Alpha(dust,.4f*(1-a/.8f)),NativeFxDraw.Authored);
                    }
                    // The blow felt by the views near it (no kick: it is the wall that is struck).
                    if(a<.12f)NativeHordeFx.AddShake(hordeShakes,ToN(BreachPoint(w,h.Strike.X,h.Strike.Y,0)),now-a,.06f,12,0,b.Seed^(uint)(k+1)*2654435761u);
                }
                return;
            }
            float sinceBreak=now-b.BreakAt;
            if(phase==NativeBreach.Phase.Broken&&into<1.6f)
            {
                for(int d=0;d<14;d+=low?2:1){float ang=d*2.39996f,rr=.3f+.7f*((d*37)%10)/10f,out_=into*(2.2f+1.5f*rr);var p=BreachPoint(w,h.Strike.X+Mathf.Cos(ang)*1.3f*rr,Mathf.Max(h.Floor+.2f,h.Strike.Y+Mathf.Sin(ang)*1.1f*rr-into*.5f),face+out_);draw.Sprite(draw.Effects,p,right,up,(.5f+1.4f*into)*(.7f+.3f*rr),Alpha(dust,.55f*(1-into/1.6f)),NativeFxDraw.Authored);}
                int foot=Mathf.Clamp(Mathf.RoundToInt(h.HalfWidth*2/1.2f),2,12);float rise=Mathf.Clamp01((into-.25f)/1.1f);
                if(rise>0)for(int d=0;d<foot;d+=low?2:1){float u=(d+.5f)/foot*2-1,rr=(d*53)%10/10f;var p=BreachPoint(w,u*h.HalfWidth*.95f,h.Floor+.3f+rise*.9f,face+rise*(1f+1.6f*rr));draw.Sprite(draw.Effects,p,right,up,(.8f+1.8f*rise)*(.8f+.4f*rr),Alpha(dust,.5f*(1-rise)),NativeFxDraw.Authored);}
                if(into<.15f)draw.Sprite(draw.Effects,BreachPoint(w,h.Strike.X,h.Strike.Y,face),right,up,3f*(1-into/.15f),new Color(1,.9f,.8f,.6f*(1-into/.15f)),NativeFxDraw.Authored+NativeFxDraw.Additive);
            }
            if(!b.Panel&&phase!=NativeBreach.Phase.Solid)for(int k=0;k<h.Chunks;k++)
            {
                NativeBreach.PieceAt(h,k,phase,into,sinceBreak,out var at,out var spin,out float holo,out float solid);if(holo>0&&solid<.02f)continue;
                var wc=w.ToWorld(at);draw.Cube(draw.Solid,new Vector3(wc.X,wc.Y,wc.Z),Quaternion.LookRotation(new Vector3(w.Normal.X,0,w.Normal.Z))*Quaternion.Euler(spin.X*Mathf.Rad2Deg,spin.Y*Mathf.Rad2Deg,spin.Z*Mathf.Rad2Deg),new Vector3(h.Size[k].X,h.Size[k].Y,h.Size[k].Z),new Color(.3f,.29f,.31f),NativeFxDraw.Authored);
            }
            float xs=h.HalfWidth+.12f,top=h.HalfHeight,bottom=-h.HalfHeight;
            if(phase==NativeBreach.Phase.Rebuild)
            {
                if(NativeBreach.Scan(h,into,out float sy,out float ss))
                {
                    float y=Mathf.Clamp(sy,bottom,top);int lines=Mathf.Clamp(Mathf.RoundToInt(xs*2/.8f)+1,7,24);var lift=Vector3.up*.025f;
                    for(int f=-1;f<=1;f+=2)
                    {
                        float z=(face+.02f)*f;Vector3 l0=BreachPoint(w,-xs,y,z),l1=BreachPoint(w,xs,y,z),g0=BreachPoint(w,-xs,bottom,z),g1=BreachPoint(w,xs,bottom,z);
                        draw.Quad(draw.Effects,l0-lift,l1-lift,l1+lift,l0+lift,Alpha(HardlightColor,.95f*ss),NativeFxDraw.Authored+NativeFxDraw.Additive);
                        draw.Quad(draw.Effects,g0,g1,l1,l0,Alpha(HardlightColor,.08f*ss),NativeFxDraw.Authored+NativeFxDraw.Additive);
                        for(int gx=0;gx<lines;gx++){float x=-xs+gx*xs*2/(lines-1);HordeStreak(draw,BreachPoint(w,x,bottom,z),BreachPoint(w,x,y,z),.008f,Alpha(HardlightColor,.35f*ss),eye);}
                    }
                }
                for(int k=0;k<h.Chunks;k++)
                {
                    NativeBreach.PieceAt(h,k,phase,into,sinceBreak,out var at,out var spin,out float holo,out float solid);NativeBreach.Restore(h,k,into,out float back,out _);if(back<=0)continue;
                    float edge=holo>0?.9f*holo:.6f*(1-solid);if(edge<.02f)continue;
                    HordeWireBox(draw,w,at,spin,h.Size[k],Alpha(HardlightColor,edge),eye);
                }
            }
            if(phase==NativeBreach.Phase.Solid&&into<.4f)
            {
                // The hardlight flash over the whole block's faces as its own look comes back.
                var flash=Alpha(HardlightColor,.3f*(1-into/.4f));
                for(int f=-1;f<=1;f+=2){float z=(face+.02f)*f;draw.Quad(draw.Effects,BreachPoint(w,-h.HalfWidth,bottom,z),BreachPoint(w,h.HalfWidth,bottom,z),BreachPoint(w,h.HalfWidth,top,z),BreachPoint(w,-h.HalfWidth,top,z),flash,NativeFxDraw.Authored+NativeFxDraw.Additive);}
            }
        }
        // A piece's hologram: its box's twelve edges in hardlight.
        private void HordeWireBox(NativeFxDraw draw,in NativeBreach.Wall w,NVector at,NVector spin,NVector size,Color color,Vector3 eye)
        {
            var q=System.Numerics.Quaternion.CreateFromYawPitchRoll(spin.Y,spin.X,spin.Z);
            for(int e=0;e<12;e++)
            {
                int axis=e/4,a=e%4;
                // The edge's two ends: along `axis`, the other two coordinates fixed at the corners.
                NVector c0,c1;float sx=size.X*.5f,sy=size.Y*.5f,sz=size.Z*.5f,p1=(a&1)*2-1,p2=(a>>1)*2-1;
                if(axis==0){c0=new NVector(-sx,p1*sy,p2*sz);c1=new NVector(sx,p1*sy,p2*sz);}else if(axis==1){c0=new NVector(p1*sx,-sy,p2*sz);c1=new NVector(p1*sx,sy,p2*sz);}else{c0=new NVector(p1*sx,p2*sy,-sz);c1=new NVector(p1*sx,p2*sy,sz);}
                var w0=w.ToWorld(at+System.Numerics.Vector3.Transform(c0,q));var w1=w.ToWorld(at+System.Numerics.Vector3.Transform(c1,q));
                HordeStreak(draw,new Vector3(w0.X,w0.Y,w0.Z),new Vector3(w1.X,w1.Y,w1.Z),.012f,color,eye);
            }
        }
        // A unit cube (the breach's pieces, drawn in the segment's material), made the first time it is needed.
        private static Mesh BreachCube()
        {
            var v=new Vector3[24];var n=new Vector3[24];var uv=new Vector2[24];var t=new int[36];int o=0,ti=0;
            Vector3[] axes={Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back};
            foreach(var f in axes)
            {
                var u=Mathf.Abs(f.y)>.5f?Vector3.right:Vector3.up;var r=Vector3.Cross(u,f);
                for(int c=0;c<4;c++){float a=(c==1||c==2)?.5f:-.5f,b=c>=2?.5f:-.5f;v[o+c]=f*.5f+r*a+u*b;n[o+c]=f;uv[o+c]=new Vector2(a+.5f,b+.5f);}
                t[ti++]=o;t[ti++]=o+1;t[ti++]=o+2;t[ti++]=o;t[ti++]=o+2;t[ti++]=o+3;o+=4;
            }
            var m=new Mesh{name="Siren breach piece",vertices=v,normals=n,uv=uv,triangles=t};m.RecalculateBounds();return m;
        }
        /// <summary>A new arena or a rewind: every breach gives its segment back and is forgotten.</summary>
        private void ClearHordeBreaches(){for(int i=0;i<hordeBreaches.Length;i++)if(hordeBreaches[i].Live)HordeBreachRelease(hordeBreaches[i]);}
    }
}
