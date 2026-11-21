using System.Collections.Generic;
using UnityEngine;
using NVector=System.Numerics.Vector3;
using NMatrix=System.Numerics.Matrix4x4;

namespace ChipArena.UnityConversion
{
    // The Tiler drawn as what it is, a crab of the pool's own tiles (owner 2026-11-03: the Backrooms' entities need not be the
    // player's model; Core NativeTilerLook for its body and moves). Its rig is still posed on the gait (hits, arrows and the effects
    // find its body there), but neither the player's model nor the voxel model is drawn: its carapace (one mesh per variant) and
    // its limbs (one mesh per Tiler, posed every frame) with the zone gear's toon material, so it needs no local assets. Dormant it
    // lies flat in its wall (the Poolrooms layout's nearest wall to it) and its peel leaves a hole there; hits chip shards off it
    // (no blood, no wounds, no severed limbs); its death bursts its plates over the floor and crumbles its grout (no corpse, no
    // gore); no zombie voice and no footfalls. Presentation only.
    public sealed partial class ArenaPresentation
    {
        private static bool TilerIs(int kind)=>kind>=0&&kind==NativeTilerLook.Kind;
        private sealed class TilerView
        {
            public NativeTilerLook.Gait Gait;public readonly NativeTilerLook.Pose Pose=new NativeTilerLook.Pose();
            public Mesh Limbs;public Vector3[] Pos,Nor;public NVector[] NPos,NNor;
            public float HitAt=float.NaN,Yaw=float.NaN;public Vector2 HitPush;public int Frame;public bool Scarred;
        }
        private readonly Dictionary<int,TilerView> tilerViews=new Dictionary<int,TilerView>();private readonly List<int> tilerStale=new List<int>();
        private Mesh[] tilerShells,tilerDomes;private Mesh[][] tilerPlates;private Mesh tilerChip,tilerScar;private int[] tilerLimbTris;private Color[] tilerLimbCols;
        private MaterialPropertyBlock tilerBlock;private bool tilerFailed;private readonly NMatrix[] tilerPlateAt=new NMatrix[NativeTilerLook.Plates];
        private readonly NativeTilerLook.Pose tilerDying=new NativeTilerLook.Pose();
        // Shards chipped off by hits, the holes peels leave, the dust of a death.
        private struct TilerChip{public Vector3 At,Velocity,Axis;public float Born,Floor,Rate;}
        private readonly TilerChip[] tilerChips=new TilerChip[48];private int tilerChipNext;
        private struct TilerScar{public Matrix4x4 At;public float Born;}
        private readonly List<TilerScar> tilerScars=new List<TilerScar>();private readonly HashSet<int> tilerBursts=new HashSet<int>();
        private static readonly Color TilerDust=new Color(.42f,.52f,.5f,0);
        /// <summary>How long a peel's hole stays in the wall (s).</summary>
        public const float TilerScarLife=90;

        private bool TilerReady()
        {
            if(tilerShells!=null)return true;if(tilerFailed)return false;
            var mats=ZombieGearMaterials();if(mats==null){tilerFailed=true;Debug.LogWarning("The toon shader is missing or unsupported: the Tiler is not drawn");return false;}
            int v=NativeTilerLook.Variants;tilerShells=new Mesh[v];tilerDomes=new Mesh[v];tilerPlates=new Mesh[v][];
            for(int k=0;k<v;k++)
            {
                var c=NativeTilerLook.Shell(k);tilerShells[k]=TilerMesh(c.Living,"Tiler shell "+k);tilerDomes[k]=TilerMesh(c.Dome,"Tiler grout "+k);
                tilerPlates[k]=new Mesh[c.Plate.Length];for(int i=0;i<c.Plate.Length;i++)tilerPlates[k][i]=TilerMesh(c.Plate[i],"Tiler plate "+k+"."+i);
            }
            tilerChip=TilerMesh(NativeTilerLook.Chip(),"Tiler chip");tilerScar=TilerMesh(NativeTilerLook.Scar(),"Tiler scar");
            // The limbs' colours and triangles once (every Tiler's limb mesh shares them; only the places move).
            var tris=new List<int>();var cols=new List<Color>();int o=0;
            foreach(var s in NativeTilerLook.Limbs()){foreach(int t in s.Tri)tris.Add(o+t);foreach(var c in s.Col)cols.Add(TilerColor(c));o+=s.Pos.Length;}
            tilerLimbTris=tris.ToArray();tilerLimbCols=cols.ToArray();tilerBlock=new MaterialPropertyBlock();return true;
        }
        private static Color TilerColor(System.Numerics.Vector4 c)
        {
            var col=new Color(c.X,c.Y,c.Z,c.W);
            return QualitySettings.activeColorSpace==ColorSpace.Linear?new Color(Mathf.GammaToLinearSpace(col.r),Mathf.GammaToLinearSpace(col.g),Mathf.GammaToLinearSpace(col.b),col.a):col;
        }
        private static Mesh TilerMesh(NativeTilerLook.Shape s,string name)
        {
            int n=s.Pos.Length;var p=new Vector3[n];var nr=new Vector3[n];var c=new Color[n];
            for(int i=0;i<n;i++){p[i]=new Vector3(s.Pos[i].X,s.Pos[i].Y,s.Pos[i].Z);nr[i]=new Vector3(s.Nor[i].X,s.Nor[i].Y,s.Nor[i].Z);c[i]=TilerColor(s.Col[i]);}
            var m=new Mesh{name=name,hideFlags=HideFlags.HideAndDontSave};m.vertices=p;m.normals=nr;m.colors=c;m.triangles=s.Tri;m.RecalculateBounds();m.UploadMeshData(true);return m;
        }
        private TilerView TilerViewOf(int id)
        {
            if(tilerViews.TryGetValue(id,out var v))return v;v=new TilerView();int n=NativeTilerLook.LimbVertices;
            v.Pos=new Vector3[n];v.Nor=new Vector3[n];v.NPos=new NVector[n];v.NNor=new NVector[n];
            v.Limbs=new Mesh{name="Tiler limbs",hideFlags=HideFlags.HideAndDontSave};v.Limbs.MarkDynamic();
            v.Limbs.vertices=v.Pos;v.Limbs.colors=tilerLimbCols;v.Limbs.triangles=tilerLimbTris;
            v.Limbs.bounds=new Bounds(new Vector3(0,.2f,0),new Vector3(1.8f,1.2f,1.8f));tilerViews[id]=v;return v;
        }
        private static Matrix4x4 TilerUnity(NMatrix m)=>new Matrix4x4(new Vector4(m.M11,m.M12,m.M13,m.M14),new Vector4(m.M21,m.M22,m.M23,m.M24),new Vector4(m.M31,m.M32,m.M33,m.M34),new Vector4(m.M41,m.M42,m.M43,m.M44));

        /// <summary>HordeKindAdd: a living Tiler at its presented root (`pos`, `yaw`, `velocity`; `ground` its floor), struck by `flash`.</summary>
        private void TilerAdd(NativeArena arena,NativeMinion m,HordeKindState s,Vector3 pos,float yaw,Vector3 velocity,float ground,float now,float flash)
        {
            if(!TilerReady()||m.Pve==null)return;var a=m.Pve;var z=a.Zone;var v=TilerViewOf(m.Id);v.Frame=Time.frameCount;uint seed=NativeHordeKinds.Seed(m.Id);
            var toLocal=Quaternion.Euler(0,-yaw,0);var lv=toLocal*velocity;
            float rate=float.IsNaN(v.Yaw)||hordeDt<=0?0:Mathf.DeltaAngle(v.Yaw,yaw)*Mathf.Deg2Rad/hordeDt;v.Yaw=yaw;
            NativeTilerLook.Step(ref v.Gait,new System.Numerics.Vector2(lv.x,lv.z),rate,hordeDt);
            var floor=new Vector3(pos.x,ground,pos.z);
            var mo=new NativeTilerLook.Moment{Dormant=z!=null&&z.dormant,WakeAge=float.NaN,BiteAge=float.NaN,HitAge=float.NaN};
            float wakeAt=z!=null?NativeZoneCombat.View(z.wakeAt):float.NaN;
            if(z!=null&&z.attack=="wake"&&!float.IsNaN(wakeAt))mo.WakeAge=now-wakeAt;
            // The bite (the horde's swipe at the authority's .4 s contact): not the wake's own start, nor a blow the authority cancelled.
            float bite=now-m.LastAttack;bool wakeStart=!float.IsNaN(wakeAt)&&Mathf.Abs(m.LastAttack-wakeAt)<.05f,cancelled=s.CancelFor==m.LastAttack&&now>=s.CancelAt;
            if(!wakeStart&&!cancelled&&a.Stats!=null&&bite>=0){mo.BiteAge=bite;mo.Strike=NativeHordeKinds.Strike(a.Stats,a.Crawl,a.Lost);mo.Recover=NativeHordeKinds.Recover(a.Stats,a.Crawl,a.Lost);}
            var target=HordeTarget(arena,pos);
            if(target!=null){var t=toLocal*(target.Body.position-floor);mo.HasTarget=true;mo.Target=new NVector(t.x,t.y,t.z);}
            if(mo.Dormant||mo.WakeAge>=0&&mo.WakeAge<NativeTilerLook.Peel+.1f){var w=TilerWall(floor,out float dist);var wl=toLocal*w;mo.Wall=new NVector(wl.x,0,wl.z);mo.WallDistance=dist;}
            if(!float.IsNaN(v.HitAt)){mo.HitAge=now-v.HitAt;mo.HitPush=new System.Numerics.Vector2(v.HitPush.x,v.HitPush.y);}
            NativeTilerLook.Living(v.Pose,v.Gait,seed,now,mo);
            var add=Matrix4x4.TRS(floor,Quaternion.Euler(0,yaw,0),Vector3.one);
            // Its peel leaves the hole its plates came out of.
            if(!v.Scarred&&mo.WakeAge>=0&&mo.WakeAge<NativeTilerLook.Peel)
            {
                v.Scarred=true;NativeTilerLook.Patch(seed,mo,out var at,out var turn);var outward=System.Numerics.Vector3.Transform(NVector.UnitY,turn);
                var place=NMatrix.CreateFromQuaternion(turn)*NMatrix.CreateTranslation(at-outward*(NativeTilerLook.Proud-.0015f));
                if(tilerScars.Count>=24)tilerScars.RemoveAt(0);tilerScars.Add(new TilerScar{At=add*TilerUnity(place),Born=hordeClock});
            }
            TilerDraw(v,add*TilerUnity(v.Pose.Body),NativeTilerLook.Variant(seed),flash);
        }
        private void TilerDraw(TilerView v,Matrix4x4 body,int variant,float flash)
        {
            var mats=ZombieGearMaterials();if(mats==null)return;
            NativeTilerLook.WriteLimbs(v.Pose,v.NPos,v.NNor);
            for(int i=0;i<v.Pos.Length;i++){var p=v.NPos[i];var n=v.NNor[i];v.Pos[i]=new Vector3(p.X,p.Y,p.Z);v.Nor[i]=new Vector3(n.X,n.Y,n.Z);}
            v.Limbs.vertices=v.Pos;v.Limbs.normals=v.Nor;
            tilerBlock.Clear();tilerBlock.SetFloat(HitFlashId,flash);
            Graphics.DrawMesh(tilerShells[variant],body,mats[0],0,null,0,tilerBlock,true,true);
            Graphics.DrawMesh(v.Limbs,body,mats[0],0,null,0,tilerBlock,true,true);
        }
        /// <summary>The wall a dormant Tiler lies in: the Poolrooms layout's wall of its cell it stands nearest (the direction to its
        /// face and how far); zero (the deck) off the Poolrooms or with none.</summary>
        private static readonly (int,int)[] TilerSides={(1,0),(-1,0),(0,1),(0,-1)};
        private Vector3 TilerWall(Vector3 at,out float distance)
        {
            distance=0;var l=poolroomsLayout;if(l==null)return Vector3.zero;float c=PoolroomsLayout.Cell;
            var local=at-poolroomsOrigin;int x=Mathf.FloorToInt(local.x/c),z=Mathf.FloorToInt(local.z/c);if(!l.Inside(x,z))return Vector3.zero;
            float fx=local.x/c-x-.5f,fz=local.z/c-z-.5f,best=-1;var dir=Vector3.zero;
            foreach(var (dx,dz) in TilerSides)
            {
                if(l.Between(x,z,dx,dz)!=PoolroomsLayout.Wall)continue;float toward=dx*fx+dz*fz;
                if(toward>best){best=toward;dir=new Vector3(dx,0,dz);distance=(.5f-toward)*c-PoolroomsLayout.Thickness*.5f;}
            }
            if(distance<0)distance=0;return dir;
        }

        /// <summary>HordeKindDeath: the dying Tiler `age` s after its death (its plates bursting over the floor, its grout crumbling).</summary>
        private void TilerDeath(NativeDeath death,float age)
        {
            if(!TilerReady()||!NativeTilerLook.Dying(tilerDying,NativeHordeKinds.Seed(death.id),age,tilerPlateAt))return;
            var mats=ZombieGearMaterials();if(mats==null)return;uint seed=NativeHordeKinds.Seed(death.id);int variant=NativeTilerLook.Variant(seed);
            var floor=new Vector3(death.position.x,death.position.y-NativeHordeKinds.RootHeight(NativeTilerLook.Kind,false),death.position.z);
            var add=Matrix4x4.TRS(floor,Quaternion.Euler(0,death.yaw,0),Vector3.one);tilerBlock.Clear();tilerBlock.SetFloat(HitFlashId,0);
            if(tilerDying.Scale>.01f)
            {
                var v=TilerViewOf(death.id);v.Frame=Time.frameCount;
                NativeTilerLook.WriteLimbs(tilerDying,v.NPos,v.NNor);
                for(int i=0;i<v.Pos.Length;i++){var p=v.NPos[i];var n=v.NNor[i];v.Pos[i]=new Vector3(p.X,p.Y,p.Z);v.Nor[i]=new Vector3(n.X,n.Y,n.Z);}
                v.Limbs.vertices=v.Pos;v.Limbs.normals=v.Nor;var body=add*TilerUnity(tilerDying.Body);
                Graphics.DrawMesh(tilerDomes[variant],body,mats[0],0,null,0,tilerBlock,true,true);Graphics.DrawMesh(v.Limbs,body,mats[0],0,null,0,tilerBlock,true,true);
            }
            var plates=tilerPlates[variant];for(int i=0;i<plates.Length;i++)Graphics.DrawMesh(plates[i],add*TilerUnity(tilerPlateAt[i]),mats[0],0,null,0,tilerBlock,true,true);
            // The grout's dust as it bursts (once).
            if(age<.25f&&tilerBursts.Add(death.id)&&vfx!=null)
                for(int i=0;i<6;i++){var r=new Vector3(SmilerHash(seed,i,0)-.5f,0,SmilerHash(seed,i,1)-.5f);TilerDustAt(floor+Vector3.up*.18f+r*.4f,r*.9f+Vector3.up*.3f,.35f+.25f*SmilerHash(seed,i,2),seed+(uint)i);}
        }
        // (A death's dust rides the Smiler's puffs: soft, slowing, swelling and thinning, in the grout's grey.)
        private void TilerDustAt(Vector3 at,Vector3 velocity,float size,uint seed)=>tilerDust[tilerDustNext++%tilerDust.Length]=new SmilerPuff{At=at,Velocity=velocity,Born=hordeClock,Size=size,Life=1.1f,Seed=seed};
        private readonly SmilerPuff[] tilerDust=new SmilerPuff[24];private int tilerDustNext;

        /// <summary>HordeGoreHit: a hit on a Tiler jolts it and chips shards of tile off it (no wound, no blood); true when the add
        /// was a Tiler.</summary>
        private bool TilerHit(NativeMinion m,HordeKindState s,in ArenaEvent e,Vector3 at,Vector3 dir)
        {
            int kind=s!=null&&s.Kind>=0?s.Kind:NativeHordeKinds.Resolve(m?.Pve?.Stats?.id);if(!TilerIs(kind))return false;
            if(m!=null&&TilerReady()){var v=TilerViewOf(m.Id);v.HitAt=e.Time;var push=Quaternion.Euler(0,float.IsNaN(v.Yaw)?0:-v.Yaw,0)*dir;v.HitPush=new Vector2(push.x,push.z).normalized;}
            var point=e.Position.x==0&&e.Position.y==0&&e.Position.z==0?at+Vector3.up*.25f:NativeArena.Vec(e.Position);uint seed=unchecked((uint)e.Id*2654435761u);
            float floor=m!=null?m.Body.position.y-NativeHordeKinds.RootHeight(kind,false):point.y-.25f;
            for(int i=0;i<3;i++)
            {
                var r=new Vector3(SmilerHash(seed,i,0)-.5f,SmilerHash(seed,i,1)*.5f,SmilerHash(seed,i,2)-.5f);
                tilerChips[tilerChipNext++%tilerChips.Length]=new TilerChip{At=point+r*.05f,Velocity=-dir*(.6f+.8f*SmilerHash(seed,i,3))+r*1.4f+Vector3.up*(1.3f+.8f*SmilerHash(seed,i,4)),
                    Axis=new Vector3(SmilerHash(seed,i,5)-.5f,SmilerHash(seed,i,6)-.5f,SmilerHash(seed,i,7)-.5f).normalized,Rate=500+700*SmilerHash(seed,i,8),Born=hordeClock,Floor=floor};
            }
            return true;
        }
        /// <summary>DrawHordeKindFx: the chips falling and settling, the death's dust, the peels' holes (shrinking away at the end of
        /// their life); the views of Tilers no longer drawn are let go.</summary>
        private void TilerFx(float now)
        {
            if(tilerShells!=null)
            {
                var mats=ZombieGearMaterials();
                if(mats!=null)
                {
                    tilerBlock.Clear();tilerBlock.SetFloat(HitFlashId,0);
                    for(int i=0;i<tilerChips.Length;i++)
                    {
                        var c=tilerChips[i];if(c.Rate<=0)continue;float t=hordeClock-c.Born;if(t<0||t>1.4f){if(t>1.4f)tilerChips[i].Rate=0;continue;}
                        var p=c.At+c.Velocity*t+Vector3.down*(4.9f*t*t);float spin=t;
                        if(p.y<c.Floor+.004f){float tf=TilerLand(c,t);p=c.At+new Vector3(c.Velocity.x,0,c.Velocity.z)*(tf+.15f*Mathf.Min(t-tf,.2f));p.y=c.Floor+.004f;spin=tf;}
                        float shrink=1-Mathf.SmoothStep(0,1,(t-1.1f)/.3f);
                        Graphics.DrawMesh(tilerChip,Matrix4x4.TRS(p,Quaternion.AngleAxis(c.Rate*spin,c.Axis),Vector3.one*shrink),mats[0],0,null,0,tilerBlock,false,true);
                    }
                    for(int i=tilerScars.Count-1;i>=0;i--)
                    {
                        float age=hordeClock-tilerScars[i].Born;if(age<0||age>TilerScarLife){tilerScars.RemoveAt(i);continue;}
                        float k=1-Mathf.SmoothStep(0,1,(age-(TilerScarLife-4))/4);Graphics.DrawMesh(tilerScar,tilerScars[i].At*Matrix4x4.Scale(new Vector3(k,1,k)),mats[0],0,null,0,tilerBlock,false,true);
                    }
                }
            }
            if(vfx!=null)for(int i=0;i<tilerDust.Length;i++)
            {
                var q=tilerDust[i];if(q.Life<=0)continue;float age=now-q.Born;if(age<0||age>q.Life){if(age>q.Life)tilerDust[i].Life=0;continue;}
                float u=age/q.Life,go=(1-Mathf.Exp(-age*3))/3;
                vfx.Vapor(q.At+q.Velocity*go+Vector3.up*(.1f*age),TilerDust,.55f*Mathf.Pow(1-u,1.5f),q.Size*(1+1.4f*u),u,q.Seed%8191u,NativeVfxDraw.NoFloor,0);
            }
            if((Time.frameCount&63)!=0)return;tilerStale.Clear();
            foreach(var pair in tilerViews)if(pair.Value.Frame<Time.frameCount-120)tilerStale.Add(pair.Key);
            foreach(int id in tilerStale){var v=tilerViews[id];if(v.Limbs!=null)Object.Destroy(v.Limbs);tilerViews.Remove(id);}
            if(tilerBursts.Count>32)tilerBursts.Clear();
        }
        // When a chip reaches the floor (s after it was struck off).
        private static float TilerLand(in TilerChip c,float t){float h=c.At.y-c.Floor-.004f,vy=c.Velocity.y;return Mathf.Min(t,(vy+Mathf.Sqrt(Mathf.Max(0,vy*vy+19.6f*h)))/9.8f);}
        private void ClearTilers()
        {
            foreach(var v in tilerViews.Values)if(v.Limbs!=null)Object.Destroy(v.Limbs);tilerViews.Clear();tilerScars.Clear();tilerBursts.Clear();
            for(int i=0;i<tilerChips.Length;i++)tilerChips[i].Rate=0;for(int i=0;i<tilerDust.Length;i++)tilerDust[i].Life=0;
        }
    }
}
