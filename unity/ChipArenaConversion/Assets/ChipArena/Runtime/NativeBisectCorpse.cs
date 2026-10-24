using System;
using UnityEngine;
using NVector3=System.Numerics.Vector3;

namespace ChipArena.UnityConversion
{
    // A body split in two (Core NativeBisect, Runtime NativeBisectPresentation): two cosmetic corpses in one physics scene,
    // each the ragdoll bodies its half keeps (a joint's body goes to both halves when the plane crosses it, and a piece cut off
    // its own chain lies loose), with colliders sized from that half's geometry. They hold on the cutter (kinematic) for the
    // request's hitch, then part with its velocities and tumble, rigid about the half's centre so the half tips as one, and
    // fall as ragdolls; they never collide with each other. The first half owns the scene, steps both, freezes and wakes both.
    // The dropped weapon stays whole, with the half its hand is on. Presentation only: no collider here reaches the authority.
    internal sealed partial class NativeCorpse
    {
        internal readonly bool Halved;internal BisectCut Bisection;internal BisectRequest Request;internal int Half;internal NativeCorpse Host,Twin;
        /// <summary>The presentation's state for the split (its models, blood and sparks), and what releases its models.</summary>
        internal object Bisect;internal Action OnRelease;
        /// <summary>Waits for the split's geometry (it may run on a worker during the hold): called before the halves part.</summary>
        internal Action AwaitSplit;
        /// <summary>Whether the split's geometry is in (or can be had without waiting). Until it is, the halves keep holding past
        /// the request's hitch, for at most <see cref="HoldLimit"/> s more, rather than block the frame on the worker: a melee
        /// cut's hitch is shorter than a split takes.</summary>
        internal Func<bool> SplitReady;
        internal const float HoldLimit=1.5f;
        private bool parted;
        /// <summary>This half's pelvis is a stand-in (Core BisectCut.Rooted false): still, collisionless, nothing hangs from it.</summary>
        private bool standIn;
        /// <summary>Both halves of a bisected death from a posed rig: `split` gets the bind matrices and the pelvis (Unity world)
        /// and returns the cut and its request (null: no split, the caller makes the whole corpse).</summary>
        internal static NativeCorpse Bisected(NativeDeath death,CapturedAnimation.Player rig,NativeArena arena,Func<Matrix4x4[],Vector3,(BisectCut cut,BisectRequest request)> split,bool stock=false)
        {
            var bind=RigBind(death,rig);var at=RigAt(death,rig);var turn=RigTurn(rig);var (cut,request)=split(bind,at[CapturedRig.Pelvis]);if(cut==null||request==null)return null;
            var first=new NativeCorpse(death,arena,bind,at,turn,null,null,-1,null,cut,request,0,null,stock:stock);
            var second=new NativeCorpse(death,arena,bind,at,turn,null,null,-1,null,cut,request,1,first,stock:stock);first.Twin=second;
            foreach(var a in first.Parts.Values)foreach(var b in second.Parts.Values)Physics.IgnoreCollision(a.GetComponent<Collider>(),b.GetComponent<Collider>());
            Physics.SyncTransforms();return first;
        }
        private static NVector3 N(Vector3 v)=>new NVector3(v.x,v.y,v.z);
        private static Vector3 U(NVector3 v)=>new Vector3(v.X,v.Y,v.Z);
        /// <summary>The hand the dropped weapon leaves (the magic weapons' off hand, else the right).</summary>
        private static int WeaponHand(NativeDeath death){var s=death.actor?.SourceSpec;return s=="fire"||s=="water"||s=="lightning"||s=="darkness"||s=="leet"?9:13;}
        /// <summary>Whether this corpse drops the weapon: every whole body does; of two halves, the one the hand is on.</summary>
        private bool HoldsWeapon(Vector3[] at)=>!Halved||Request.HalfOf(WeaponHand(Death),N(at[WeaponHand(Death)]))==Half;
        /// <summary>A half body's collider bounds in its own frame: the half's geometry on that joint (Core BisectCut).</summary>
        private Bounds HalfBounds(int b,Matrix4x4 toLocal)
        {
            var lo=Bisection.Min[Half][b];var hi=Bisection.Max[Half][b];var m=toLocal*Bind[b];var bounds=new Bounds(m.MultiplyPoint3x4(U(lo)),Vector3.zero);
            for(int k=0;k<8;k++)bounds.Encapsulate(m.MultiplyPoint3x4(new Vector3((k&1)==0?lo.X:hi.X,(k&2)==0?lo.Y:hi.Y,(k&4)==0?lo.Z:hi.Z)));return bounds;
        }
        /// <summary>A half's body whose own parent body went to the other half lies loose (no joint).</summary>
        private void LoosePieces()
        {
            foreach(var pair in Parts)
            {
                int b=pair.Key,p=CapturedRig.Bones[b].Parent;while(p>=0&&Array.IndexOf(Bones,p)<0)p=CapturedRig.Bones[p].Parent;
                if(p>=0&&(!Parts.ContainsKey(p)||standIn&&p==CapturedRig.Pelvis)&&!Severed.Contains(b))Severed.Add(b);
            }
        }
        /// <summary>At the hitch's end both halves part: each body the half's velocity plus its tumble about the half's centre.</summary>
        private void ReleaseHalves()
        {
            if(!Halved||Host!=null||parted||clock+1e-6f<Request.Hitch)return;if(SplitReady!=null&&!SplitReady()&&clock<Request.Hitch+HoldLimit)return;
            AwaitSplit?.Invoke();PartHalf(clock);Twin?.PartHalf(clock);
        }
        /// <summary>A half whose pelvis is a stand-in: the stand-in stays put with no collider (the other bodies of that half are
        /// loose of it, LoosePieces), so a torso cut off above the hips cannot stand on an invisible pelvis.</summary>
        private void StandIn()
        {
            standIn=Halved&&!Bisection.Rooted[Half]&&Parts.ContainsKey(CapturedRig.Pelvis);if(!standIn)return;
            var root=Parts[CapturedRig.Pelvis];root.GetComponent<Collider>().enabled=false;root.isKinematic=true;
        }
        private bool Still(int part)=>IsCrushed(part)||standIn&&part==CapturedRig.Pelvis||Pinned;
        private void PartHalf(float at)
        {
            if(parted||at+1e-6f<Request.Hitch)return;parted=true;
            NVector3 away=Half==1?Request.Away1:Request.Away0,spin=Half==1?Request.Spin1:Request.Spin0;var v=U(away)+Vector3.ClampMagnitude(Death.velocity,4)*.3f;var w=U(spin);
            Vector3 centre=Vector3.zero;float mass=0;foreach(var body in Parts.Values){centre+=body.position*body.mass;mass+=body.mass;}centre=mass>0?centre/mass:Parts[CapturedRig.Pelvis].position;
            foreach(var pair in Parts)
            {
                var body=pair.Value;if(Still(pair.Key))continue;body.isKinematic=false;
                body.linearVelocity=Vector3.ClampMagnitude(v+Vector3.Cross(w,body.position-centre),(float)NativeKillEffectRules.MaxBodySpeed);body.angularVelocity=w;
            }
            if(weapon!=null){weapon.isKinematic=false;weapon.linearVelocity=v+Vector3.Cross(w,weapon.position-centre);weapon.angularVelocity=w+new Vector3(1,2,1);}
        }
        /// <summary>The second half's forces, inside its first half's step.</summary>
        private void Forces()
        {
            int index=0;foreach(var body in Parts.Values){int i=index++;if(!body.isKinematic)Force(body,i);}
            if(weapon!=null&&!weapon.isKinematic)Force(weapon,weaponPart);
        }
        /// <summary>The second half wakes with its first (a retained body's support changed).</summary>
        private void Thaw(float age)
        {
            if(!frozen)return;foreach(var pair in Parts)if(!Still(pair.Key)&&parted)pair.Value.isKinematic=false;
            if(weapon!=null&&parted)weapon.isKinematic=false;frozen=false;clock=age;stepped=true;
        }
        /// <summary>
        /// The split's kill-effect share (instead of SetupKill): no stump caps (the cut is geometry), one fountain per half from
        /// its trunk's cut face (the gibber's sever spray, landing as splats), `budget` drops between them.
        /// </summary>
        internal void SetupBisect(int budget,double quality,bool counted,NativeKillFloor floor)
        {
            Counted=counted;Band=-1;FxCount=Parts.Count+(Twin?.Parts.Count??0);fountainBudget=Math.Max(0,(Math.Max(0,budget)-FxCount)/2);fountainQuality=quality;
            fountainGround=floor!=null?floor(-Death.position.x,Death.position.y,Death.position.z):double.NaN;if(Twin!=null){Twin.Counted=counted;Twin.Band=-1;}
            fountains=true;Fountains();
        }
        private bool fountains;private int fountainBudget;private double fountainQuality,fountainGround;
        /// <summary>The split's exact pieces (they replace its plan once the split is in) and the fountains at their cut faces.</summary>
        internal void Settle(BisectCut cut){if(cut==null)return;Bisection=cut;if(Twin!=null)Twin.Bisection=cut;Fountains();}
        private void Fountains()
        {
            if(!fountains||Spray!=null||Bisection.Pieces.Count==0)return;Fountain(fountainBudget,fountainQuality,fountainGround);FxCount+=Spray?.Count??0;
            if(Twin!=null&&Twin.Spray==null){Twin.Fountain(fountainBudget,fountainQuality,fountainGround);FxCount+=Twin.Spray?.Count??0;}
        }
        private void Fountain(int budget,double quality,double ground)
        {
            sprayPart=-1;if(budget<1)return;BisectPiece best=null;
            foreach(var p in Bisection.Pieces)if(p.IsCap&&p.Half==Half&&p.Group==0&&Parts.ContainsKey(p.Body)&&(best==null||Rank(p.Body)<Rank(best.Body)||Rank(p.Body)==Rank(best.Body)&&p.Mesh.Indices.Count>best.Mesh.Indices.Count))best=p;
            if(best==null)return;var c=NVector3.Zero;foreach(var q in best.Mesh.Positions)c+=q;c/=Math.Max(1,best.Mesh.Positions.Count);
            int b=best.Body;var local=inverse[b]*Bind[b];sprayPart=b;sprayPoint=local.MultiplyPoint3x4(U(c));sprayNormal=local.MultiplyVector(U(best.Mesh.Normals[0])).normalized;
            // A little up and along the cutter: the gush leaves the opening seam rather than straight into the other half.
            Spray=new NativeKillSpray(Death.seed*31+Half+7,budget,quality,KillSprayProfile.Sever,Death.position.y-.85,double.IsNaN(ground)?0:ground);
        }
        private static int Rank(int body)=>body==CapturedRig.Spine?0:body==CapturedRig.Pelvis?1:2;
    }
}
