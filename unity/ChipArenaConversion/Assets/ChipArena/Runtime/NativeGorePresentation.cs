using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace ChipArena.UnityConversion
{
    // C17 (client/pve/gore.ts): a limb severed from a Ward Zero add is thrown as a tumbling chunk with a spray of black
    // ink, and a directed hit on an add splashes ink (NativeGoreHits: field and burn ticks do not). Chunks and drops are
    // pure in their event time (NativeGoreRules). This side only notices new losses in the replicated add state
    // (NativePveAdd.Lost, and a corpse's `lost` when the blow killed), so the host, guests and killcam replays throw the
    // same chunk. The body keeps hiding the lost limb as before (NativeSourcePresentation). Presentation only: no
    // simulation, hit or network state is read back or changed.
    public sealed partial class ArenaPresentation
    {
        // Settings · Reduced effects scales the ink like the source's effects quality (Full 1, Reduced .3).
        public float EffectsQuality=1;
        // The rig bone at each LIMB bit's joint: head, shoulders, elbows, hips, knees (the bones NativeSourcePresentation hides).
        private static readonly int[] GoreLimbBones={5,7,11,8,12,14,18,15,19};
        private readonly NativeGoreRules.ChunkList goreChunks=new NativeGoreRules.ChunkList();
        private readonly NativeGoreRules.DropList goreDrops=new NativeGoreRules.DropList();
        private readonly Dictionary<int,int> goreLost=new Dictionary<int,int>(),goreSeen=new Dictionary<int,int>();
        // Each add's last ink splash (event time).
        private readonly Dictionary<int,double> goreSplash=new Dictionary<int,double>();
        private readonly List<int> goreStale=new List<int>();
        private readonly Matrix4x4[] goreOlive=new Matrix4x4[NativeGoreRules.ChunkMax],goreShell=new Matrix4x4[NativeGoreRules.ChunkMax],goreInk=new Matrix4x4[NativeGoreRules.DropMax];
        private Material[] goreMaterials;private NativeArena goreArena;private int goreFrame,goreEvent;private float goreTime=float.NegativeInfinity;

        private void PveGore(NativeArena arena)
        {
            if(arena.Pve==null){if(goreArena!=null)ClearWardZero();return;}
            float now=arena.Time;
            if(goreArena!=arena){ClearWardZero();goreArena=arena;}
            // A rewind re-learns the adds without throwing; the chunks themselves are rewind-safe (gore.ts:71).
            else if(now<goreTime-.35f){goreLost.Clear();goreSeen.Clear();goreSplash.Clear();}
            goreTime=now;goreFrame++;float quality=Mathf.Clamp01(EffectsQuality);
            // gore.ts:46-47 / horde-view.ts:211: a directed hit on an add, once per hit time, drops 6-10 ink drops at chest
            // height (a hit up to .5 s ahead of the presented clock waits in the pool, as chunks do). HitPveAdd replicates the
            // hit direction in ArenaEvent.Normal; field, zone and burn/poison ticks carry none and do not splash.
            int newest=goreEvent;
            foreach(var e in arena.Events)
            {
                if(e.Id<=goreEvent)continue;newest=Math.Max(newest,e.Id);
                if(!NativeGoreHits.IsHit(e)||!NativeGoreHits.Recent(e.Time,now))continue;bool aimed=NativeGoreHits.Directional(e.Normal);
                if(!aimed||!AddBody(arena,e.Target,e.Time,out Vector3 body,out bool crawl))continue;
                if(!goreSplash.TryGetValue(e.Target,out double last))last=double.NegativeInfinity;
                if(!NativeGoreHits.Splash(true,e.Normal,e.Time,last))continue;goreSplash[e.Target]=e.Time;
                float ground=body.y-(crawl?.44f:.85f);
                GoreInk(e.Time,NativeGoreRules.GibSeed(e.Time,NativeGoreRules.FnvDigits(e.Target),0x68697421u),new Vector3(body.x,ground+1.05f,body.z),NativeGoreRules.HitInk(quality),2.2f,quality);
            }
            goreEvent=newest;
            // New LIMB bits on a living add, then on an add that died this frame (its corpse carries the final bits).
            foreach(var m in arena.Minions)
            {
                var a=m.Pve;if(a==null)continue;goreSeen[m.Id]=goreFrame;
                if(goreLost.TryGetValue(m.Id,out int old)){int fresh=a.Lost&~old;if(fresh!=0)Sever(arena,m.Id,fresh,m.Body.position,m.Yaw,a.Crawl,quality);}
                goreLost[m.Id]=a.Lost;
            }
            foreach(var d in arena.MinionDeaths)
            {
                if(!goreLost.TryGetValue(d.id,out int old)||goreSeen.TryGetValue(d.id,out int seen)&&seen==goreFrame)continue;
                int fresh=d.lost&~old;if(fresh!=0&&now-d.at<.5f)Sever(arena,d.id,fresh,d.position,d.yaw,false,quality);
                goreLost[d.id]=d.lost;   // the add is gone: the sweep below forgets it (unseen this frame), so a death throws once
            }
            goreStale.Clear();foreach(var pair in goreSeen)if(pair.Value!=goreFrame)goreStale.Add(pair.Key);
            foreach(int id in goreStale){goreSeen.Remove(id);goreLost.Remove(id);}
            goreStale.Clear();foreach(var pair in goreSplash)if(!goreSeen.ContainsKey(pair.Key))goreStale.Add(pair.Key);
            foreach(int id in goreStale)goreSplash.Remove(id);
            // gore.ts:69-76 pose the live chunks (slot colours by draw order) and the ink.
            goreChunks.Update(now);goreDrops.Update(now);int olive=0,shell=0,ink=0,slot=0;
            for(int i=0;i<goreChunks.Count;i++)
            {
                if(!NativeGoreRules.Pose(goreChunks[i],now,out double x,out double y,out double z,out double qx,out double qy,out double qz,out double qw,out double sx,out double sy,out double sz))continue;
                var matrix=Matrix4x4.TRS(new Vector3(-(float)x,(float)y,(float)z),new Quaternion((float)qx,-(float)qy,-(float)qz,(float)qw),new Vector3((float)sx,(float)sy,(float)sz));
                if(slot++%3==0)goreOlive[olive++]=matrix;else goreShell[shell++]=matrix;
            }
            for(int i=0;i<goreDrops.Count;i++)
            {if(NativeGoreRules.DropAt(goreDrops[i],now,out double x,out double y,out double z,out double s))goreInk[ink++]=Matrix4x4.TRS(new Vector3(-(float)x,(float)y,(float)z),Quaternion.identity,Vector3.one*(float)s);}
            if(olive+shell+ink==0||!EnsureGoreMaterials())return;
            DrawGore(goreOlive,olive,0);DrawGore(goreShell,shell,1);DrawGore(goreInk,ink,2);
        }
        // The add's body (live, or its fresh corpse when the hit killed it).
        private static bool AddBody(NativeArena arena,int id,float at,out Vector3 body,out bool crawl)
        {
            foreach(var m in arena.Minions)if(m.Id==id){body=m.Body.position;crawl=m.Pve?.Crawl==true;return m.Pve!=null;}
            foreach(var d in arena.MinionDeaths)if(d.id==id&&Math.Abs(d.at-at)<.05f&&d.model!=null&&d.model.StartsWith("horde-",StringComparison.Ordinal)){body=d.position;crawl=false;return true;}
            body=default;crawl=false;return false;
        }
        // gore.ts:52-61: one chunk and an ink spray per severed part, timed and aimed by the severing hit on the add (the
        // replicated events, NativeGoreHits.SeverHit), seeded from that time, the add and the limb. A directed hit aims along
        // its direction; a field or burn tick has none and the chunk rises, as the source's zero dir.
        private void Sever(NativeArena arena,int id,int fresh,Vector3 body,float yaw,bool crawl,float quality)
        {
            float at=arena.Time;bool hasFrom=false;Vector3 from=Vector3.zero;int hit=NativeGoreHits.SeverHit(arena.Events,id,arena.Time,true,out bool aimed);
            if(hit>=0)
            {
                var e=arena.Events[hit];at=e.Time;
                hasFrom=true;if(aimed)from=NativeArena.Vec(e.Normal);
            }
            minionRigs.TryGetValue(id,out var rig);uint target=NativeGoreRules.FnvDigits(id);
            for(int limb=0;limb<GoreLimbBones.Length;limb++)
            {
                if(!NativeGoreRules.RootLimb(fresh,limb))continue;var bone=rig?.Bones[GoreLimbBones[limb]];Vector3 joint;
                // The posed rig's joint; the source's body-local joint table when the rig is gone or was not posed near the body.
                if(bone!=null&&(bone.position-body).sqrMagnitude<9)joint=bone.position;
                else{NativeGoreRules.Joint(limb,-body.x,body.y,body.z,-yaw*Mathf.Deg2Rad,crawl,out double x,out double y,out double z);joint=new Vector3(-(float)x,(float)y,(float)z);}
                uint seed=NativeGoreRules.GibSeed(at,target,NativeGoreRules.LimbHash(limb));
                goreChunks.Add(NativeGoreRules.Gib(at,seed,limb,-joint.x,joint.y,joint.z,hasFrom,-from.x,from.y,from.z));
                GoreInk(at,seed^0x9e3779b9u,joint,NativeGoreRules.GibInk(quality),3.2f,quality);
            }
        }
        private void GoreInk(double at,uint seed,Vector3 position,int count,float power,float quality)
        {int n=NativeGoreRules.Spawned(count,quality);for(int i=0;i<n;i++)goreDrops.Add(NativeGoreRules.MakeDrop(at,seed,i,-position.x,position.y,position.z,power));}
        // gore.ts:36-38 lit chunks (rot olive, shell) without shadows; burst-pool.ts unlit ink (black albedo, ink emission).
        private bool EnsureGoreMaterials()
        {
            if(goreMaterials!=null)return true;var shader=Resources.Load<Shader>("NativeLit");if(shader==null)return false;goreMaterials=new Material[6];
            for(int i=0;i<6;i++)
            {
                int kind=i%3;var m=new Material(shader){name="Ward Zero gore "+i,enableInstancing=i<3,color=kind==2?Color.black:NativeArena.ColorOf(kind==0?NativeGoreRules.ChunkOlive:NativeGoreRules.ChunkShell)};
                if(kind==2)m.SetColor("_Emission",NativeArena.ColorOf(NativeGoreRules.Ink));goreMaterials[i]=m;
            }
            return true;
        }
        private void DrawGore(Matrix4x4[] buffer,int count,int material)
        {
            if(count==0)return;
            if(UsesInstancing)
            {
                try{Graphics.DrawMeshInstanced(cube,0,goreMaterials[material],buffer,count,null,ShadowCastingMode.Off,false,0,null);return;}
                catch(InvalidOperationException e){UseCompatibility(e.Message);}catch(ArgumentException e){UseCompatibility(e.Message);}
            }
            for(int i=0;i<count;i++)Graphics.DrawMesh(cube,buffer[i],goreMaterials[material+3],0,null,0,null,ShadowCastingMode.Off,false);
        }
        // Arena reset/dispose (ClearSourceActors) and leaving PvE: forget every add, chunk and drop; free the gore and
        // beacon resources (recreated on the next Ward Zero frame).
        private void ClearWardZero()
        {
            goreChunks.Clear();goreDrops.Clear();goreLost.Clear();goreSeen.Clear();goreSplash.Clear();goreArena=null;goreEvent=0;goreTime=float.NegativeInfinity;
            if(goreMaterials!=null){foreach(var m in goreMaterials)if(m!=null)CapturedAnimation.Release(m);goreMaterials=null;}
            ReleaseBeacons();
        }
    }
}
