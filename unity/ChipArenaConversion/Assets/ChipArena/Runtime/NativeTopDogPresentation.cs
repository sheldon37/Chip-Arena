using System;
using System.Collections.Generic;
using UnityEngine;
using NVector=System.Numerics.Vector3;
using NMatrix=System.Numerics.Matrix4x4;

namespace ChipArena.UnityConversion
{
    // The Top Dog in the arena (docs/TOP-DOG.md). Everything here reads the replicated kit (NativeDogView.Source: the snapshot, or the sandbox
    // preview): the props as toon models (Core NativeTopDogModels, in the pilots' own toon shader with its outline), the effects (Core
    // NativeTopDogFx), the victim's body through the feast (the belly swelling on the spine, the head thrown back, the mouth wide), the
    // flattened and the pied, the hot dogs flying out of the pop, and the feast's private camera. Presentation only: nothing here reaches the
    // authority, the network frame, hit timing, hitboxes, damage or movement.
    public sealed partial class ArenaPresentation
    {
        /// <summary>The Dev Sandbox's Preview Top Dog: the whole kit on a loop. Off everywhere else.</summary>
        public bool DogPreviewOn;
        private readonly NativeTopDogSnapshot dogSnapshot=new NativeTopDogSnapshot();
        private readonly NativeTopDogPreview dogPreview=new NativeTopDogPreview();
        // The feasts by victim, and the marks by victim, gathered once a frame from every Top Dog's kit (the body and face read them).
        private static readonly Dictionary<int,(NativeDogFeast feast,int caster)> dogVictims=new Dictionary<int,(NativeDogFeast,int)>();
        private static readonly List<(int id,NativeDogMark mark)> dogMarks=new List<(int,NativeDogMark)>();
        private static float dogNow;
        private void DogBegin(Snapshot s,float time)
        {
            dogSnapshot.Snapshot=s;NativeDogView.Source=dogSnapshot;Actor me=null;if(s?.Actors!=null)foreach(var a in s.Actors)if(a!=null&&a.Id==LocalId){me=a;break;}
            if(DogPreviewOn&&me!=null&&me.Alive)
            {
                // The feast's victim: the nearest other living pilot within the lasso's reach.
                Actor near=null;float best=NativeTopDog.LassoReach;foreach(var o in s.Actors)if(o!=null&&o!=me&&o.Alive){float d=Vector3.Distance(Vec(o.Position),Vec(me.Position));if(d<best){best=d;near=o;}}
                dogPreview.Victim=near?.Id??0;if(near!=null){var np=visualPositions.TryGetValue(near.Id,out var nv)?nv:Vec(near.Position);dogPreview.VictimAt=new NVector(np.x,np.y,np.z);}
                NativeDogView.Source=dogPreview;var feet=visualPositions.TryGetValue(me.Id,out var v)?v:Vec(me.Position);dogPreview.Step(me,new NVector(feet.x,feet.y,feet.z),me.Yaw*Mathf.Deg2Rad,time);}
            else if(dogPreview.Any)dogPreview.Clear();
            dogVictims.Clear();dogMarks.Clear();dogNow=time;DogBodiesSweep();if(s?.Actors==null)return;
            foreach(var a in s.Actors)
            {
                var k=a==null?null:NativeDogView.Of(a);if(k==null)continue;
                if(k.Feast!=null&&k.Feast.Catch>-50&&k.Feast.Victim!=0)dogVictims[k.Feast.Victim]=(k.Feast,a.Id);
                if(k.Marks!=null)foreach(var m in k.Marks)if(m!=null)dogMarks.Add((m.Id,m));
            }
        }
        private void ClearDog(){dogPreview.Clear();if(ReferenceEquals(NativeDogView.Source,dogPreview)||ReferenceEquals(NativeDogView.Source,dogSnapshot))NativeDogView.Source=null;dogVictims.Clear();dogMarks.Clear();foreach(var kv in dogBodies)if(kv.Value.Warped)DogBelly(kv.Key,Vector4.zero,Vector4.zero);dogBodies.Clear();dogWarps.Clear();}
        private static NativeDogMark DogMarkOn(int id,string kind,float time){foreach(var (who,m) in dogMarks)if(who==id&&m.Kind==kind&&time>=m.At&&time<m.Until)return m;return null;}
        /// <summary>The face's share of the feast for pilot `id` at `time` (VisualFacePilot): the mouth wide on each hot dog, stuffed after.</summary>
        internal static void TopDogFace(int id,float time,out float gobble,out float bloat)
        {
            gobble=bloat=0;if(!dogVictims.TryGetValue(id,out var v))return;var f=v.feast;if(f.Snap>-50&&time>=f.Snap+.3f)return;
            if(time<f.Release){gobble=time<f.FeedAt-.2f?.45f:1;return;}if(time<f.Pop)bloat=1;
        }

        // ---- the Hot Dog, always (owner 2026-11-08: "Always the Hot Dog") ----
        private bool dogLookBusy;private readonly Dictionary<string,string> dogLooks=new Dictionary<string,string>(StringComparer.Ordinal);
        /// <summary>Whether body `id` is a Top Dog pilot's (the view body and the local id are the local seat's).</summary>
        private bool TopDogSeat(int id)
        {
            if(id==dogIntroActor&&id>=0)return true;
            if(id<RooftopIdTop&&id>RooftopIdTop-8||id==RooftopIdTop)return false;var seat=WeaponLoadout?.Match?.Seat(id==ViewBodyId?LocalId:id);return seat?.spec==NativeTopDog.Spec;
        }
        private string TopDogLook(string look)
        {
            look??="";if(!dogLooks.TryGetValue(look,out var dog)){if(dogLooks.Count>32)dogLooks.Clear();dogLooks[look]=dog=NativeLook.With(look,NativeLook.Upper,NativeHotDog.Id);}return dog;
        }

        // ---- the intro (Core NativeTopDogIntro, staged by NativeIntros.DirectedIntro) ----
        private int dogIntroActor=-1;private float dogMalletScale;private Func<int,Matrix4x4> dogIntroHand;
        private readonly List<NativeIntroToon> dogIntroToons=new List<NativeIntroToon>();private readonly Dictionary<int,Mesh> dogCarts=new Dictionary<int,Mesh>();
        /// <summary>The entrance's toon models in the Top Dog's shader, out of the source frame (x mirrored).</summary>
        private void DogIntroToons(NativeTopDogIntro intro,float t)
        {
            dogPoolUsed=0;if(DogMaterials()==null)return;intro.Toons(t,dogIntroToons);int tier=LowGraphics?1:0;
            foreach(var toon in dogIntroToons)
            {
                var at=new Vector3(-toon.At.X,toon.At.Y,toon.At.Z);var q=new Quaternion(toon.Turn.X,-toon.Turn.Y,-toon.Turn.Z,toon.Turn.W);var m=Matrix4x4.TRS(at,q,Vector3.one*toon.Scale);
                switch(toon.Id)
                {
                    case "cart":{int key=Mathf.RoundToInt(Mathf.Clamp(toon.Param,0,1.2f)*30)+tier*100;if(!dogCarts.TryGetValue(key,out var mesh)){var k=NativeTopDogModels.Cart(key%100/30f,tier);mesh=FbKitMesh(k,Matrix4x4.identity,"Top Dog cart",out _);dogCarts[key]=mesh;}DogDraw(mesh,m);break;}
                    case "spring":{float h=toon.Param/Mathf.Max(.1f,toon.Scale);DogDraw(DogLive(kit=>NativeTopDogModels.Spring(kit,NMatrix.Identity,h,tier)),m);break;}
                    case "hotdog":{float w=toon.Wiggle;float phase=toon.Param;DogDraw(DogLive(kit=>NativeTopDogModels.HotDog(kit,NMatrix.Identity,phase,w,0,1,tier)),m);break;}
                }
            }
        }

        // ---- the toon models ----
        private static readonly int DogUseCustomId=Shader.PropertyToID("_UseCustomTexture"),DogAlbedoId=Shader.PropertyToID("_VertexAlbedo"),DogOutlineId=Shader.PropertyToID("_UseOutline"),DogCullId=Shader.PropertyToID("_Cull"),DogFlashId=Shader.PropertyToID("_HitFlash");
        private Material[] dogMaterials;private bool dogMaterialsRead;private MaterialPropertyBlock dogBlock;
        private Material[] DogMaterials()
        {
            if(dogMaterialsRead)return dogMaterials;dogMaterialsRead=true;var shader=Resources.Load<Shader>("NativeVisualBodyToon");if(shader==null||!shader.isSupported)return null;
            Material Make(string name,float glow){var m=new Material(shader){name=name,hideFlags=HideFlags.HideAndDontSave};m.SetFloat(DogUseCustomId,0);m.SetFloat(DogAlbedoId,glow);m.SetFloat(DogOutlineId,1);m.SetFloat(DogCullId,2);return m;}
            return dogMaterials=new[]{Make("Top Dog props",1e-4f),Make("Top Dog props glow",1.4f)};
        }
        // The fixed props, built once per detail tier.
        private readonly Dictionary<string,Mesh> dogMeshes=new Dictionary<string,Mesh>(StringComparer.Ordinal);
        private Mesh DogFixed(string id)
        {
            int tier=LowGraphics?1:0;string key=id+tier;if(dogMeshes.TryGetValue(key,out var m))return m;
            NativeFallenKit k=id=="anvil"?NativeTopDogModels.Anvil(tier):id=="peel"?NativeTopDogModels.BananaPeel(tier):id=="pie"?NativeTopDogModels.Pie(tier):id=="glove"?NativeTopDogModels.Glove(tier):id=="bottle"?NativeTopDogModels.Bottle(tier):id=="star"?NativeTopDogModels.StarShape(tier):null;
            if(k==null)return null;m=FbKitMesh(k,Matrix4x4.identity,"Top Dog "+id,out _);dogMeshes[key]=m;return m;
        }
        // The moving ones (a wiggling hot dog, the spring, the scissor arm, the links), rebuilt each frame into a pool.
        private readonly List<Mesh> dogPool=new List<Mesh>();private int dogPoolUsed;private readonly NativeFallenKit dogKit=new NativeFallenKit();
        private Mesh DogLive(Action<NativeFallenKit> build)
        {
            dogKit.Clear();build(dogKit);dogKit.Finish();if(dogKit.Count==0)return null;
            Mesh into=dogPoolUsed<dogPool.Count?dogPool[dogPoolUsed]:null;var m=FbKitMesh(dogKit,Matrix4x4.identity,"Top Dog live",out _,into);if(into==null)dogPool.Add(m);dogPoolUsed++;return m;
        }
        private void DogDraw(Mesh m,Matrix4x4 world,float flash=1,Vector4 belly=default,Vector4 axis=default)
        {
            var mats=DogMaterials();if(m==null||mats==null)return;dogBlock??=new MaterialPropertyBlock();dogBlock.Clear();dogBlock.SetFloat(DogFlashId,flash);
            if(belly.w>0){dogBlock.SetVector(DogBellyId,belly);dogBlock.SetVector(DogBellyAxisId,axis);}
            Graphics.DrawMesh(m,world,mats[0],0,null,0,dogBlock,true,true);if(m.subMeshCount>1)Graphics.DrawMesh(m,world,mats[1],0,null,1,dogBlock,false,true);
        }
        private static Matrix4x4 Look(Vector3 at,Vector3 forward,Vector3 up,float scale=1)
        {
            if(forward.sqrMagnitude<1e-8f)forward=Vector3.forward;if(Mathf.Abs(Vector3.Dot(forward.normalized,up.normalized))>.98f)up=Mathf.Abs(forward.y)<.9f?Vector3.up:Vector3.forward;
            return Matrix4x4.TRS(at,Quaternion.LookRotation(forward,up),Vector3.one*scale);
        }
        private static NMatrix N(Matrix4x4 m)=>new NMatrix(m.m00,m.m10,m.m20,m.m30,m.m01,m.m11,m.m21,m.m31,m.m02,m.m12,m.m22,m.m32,m.m03,m.m13,m.m23,m.m33);
        private Vector3 DogHand(Actor a,bool left)
        {
            var rig=FirstPerson&&a.Id==LocalId?viewRig:rigs.TryGetValue(a.Id,out var own)?own:null;
            if(rig?.Bones!=null)return rig.Bones[left?CapturedRig.LHand:CapturedRig.RHand].position;
            var feet=visualPositions.TryGetValue(a.Id,out var v)?v:Vec(a.Position);return feet+Vector3.up*1.2f+Quaternion.Euler(0,a.Yaw,0)*new Vector3(left?-.3f:.3f,0,.35f);
        }
        /// <summary>Pilot `id`'s mouth (world) and which way their face looks: the model's head (its face gear's frame), else the rig's.</summary>
        private bool DogMouth(Actor a,out Vector3 mouth,out Vector3 facing)
        {
            mouth=facing=default;if(!rigs.TryGetValue(a.Id,out var rig)||rig?.Bones==null)return false;
            if(visualBodies.TryGetValue(rig,out var body)&&body.Root!=null&&body.Root.activeInHierarchy)
            {
                var head=VisualHeadOf(body);
                if(head!=null){bool dog=NativeHotDog.Worn(VisualLookOf(a.Id));mouth=head.TransformPoint(dog?new Vector3(0,-.13f,.2f):new Vector3(0,-.012f,.135f));facing=head.TransformDirection(Vector3.forward);return true;}
            }
            var h=rig.Bones[CapturedRig.Head];mouth=h.position+Quaternion.Euler(0,a.Yaw,0)*new Vector3(0,-.04f,.13f);facing=Quaternion.Euler(0,a.Yaw,0)*Vector3.forward;return true;
        }
        /// <summary>End of Render: every Top Dog's props, the feast's links and hot dogs, the pies stuck to faces, the pop's flying hot dogs.</summary>
        private void RenderDog(Snapshot s,float time)
        {
            dogPoolUsed=0;if(s?.Actors==null||NativeDogView.Source==null||DogMaterials()==null)return;int tier=LowGraphics?1:0;
            foreach(var a in s.Actors)
            {
                if(a==null)continue;var k=NativeDogView.Of(a);if(k==null)continue;long seed=a.Id*7919L;
                if(k.Props!=null)foreach(var p in k.Props)if(p!=null)DogProp(a,k,p,time,tier);
                if(k.Feast!=null)DogFeast(s,a,k.Feast,time,tier);
                if(k.Hits!=null)foreach(var h in k.Hits)if(h!=null&&h.Kind==NativeTopDog.HitPop)DogShower(h,time,tier);
                // The bottle in the off hand while it squirts.
                if(k.CastKind==NativeTopDog.Mustard&&time>=k.CastAt&&time<k.CastUntil+.15f&&!(FirstPerson&&a.Id==LocalId))
                {
                    var hand=DogHand(a,true);var aim=Quaternion.Euler(-a.Pitch,a.Yaw,0)*Vector3.forward;
                    DogDraw(DogFixed("bottle"),Matrix4x4.TRS(hand,Quaternion.FromToRotation(Vector3.up,aim),Vector3.one*1.6f));
                }
                // A pie or a peel in the hand before it goes.
                if((k.CastKind==NativeTopDog.Pie||k.CastKind==NativeTopDog.Peel)&&time>=k.CastAt&&time<k.CastRelease&&!(FirstPerson&&a.Id==LocalId))
                {
                    var hand=DogHand(a,false);var fwd=Quaternion.Euler(0,a.Yaw,0)*Vector3.forward;
                    if(k.CastKind==NativeTopDog.Pie)DogDraw(DogFixed("pie"),Matrix4x4.TRS(hand+Vector3.up*.05f,Quaternion.LookRotation(Vector3.up,-fwd)*Quaternion.Euler(-90,0,0),Vector3.one));
                    else DogDraw(DogFixed("peel"),Matrix4x4.TRS(hand,Quaternion.LookRotation(fwd,Vector3.up)*Quaternion.Euler(70,0,0),Vector3.one));
                }
            }
            // The pied and the flattened, on whoever they are.
            foreach(var a in s.Actors)
            {
                if(a==null||!a.Alive)continue;var pied=DogMarkOn(a.Id,NativeTopDog.Pied,time);
                if(pied!=null&&!(FirstPerson&&a.Id==LocalId)&&time-pied.At<1.6f&&DogMouth(a,out var mouth,out var face))
                {
                    // Stuck to the face, then sliding off and falling.
                    float t=time-pied.At,slide=Mathf.Clamp01((t-.9f)/.7f);var at=mouth+face*.07f+Vector3.up*(.06f-.5f*slide*slide);
                    DogDraw(DogFixed("pie"),Matrix4x4.TRS(at,Quaternion.LookRotation(-Vector3.up,face)*Quaternion.Euler(90,0,0)*Quaternion.Euler(0,0,8*slide),Vector3.one*.95f));
                }
            }
        }
        private void DogProp(Actor owner,NativeTopDogKit k,NativeDogProp p,float time,int tier)
        {
            if(time<p.At)return;var at=Vec(p.Pos);var dir=Vec(p.Dir);
            switch(p.Kind)
            {
                case NativeTopDog.PropAnvil:
                {
                    if(time>=p.Until)return;float h=NativeDogView.AnvilDrop(p,time,out float squash);float shrink=1-Mathf.Clamp01((time-(p.Until-.3f))/.3f);if(shrink<=.01f)return;
                    var fwd=new Vector3(dir.x,0,dir.z);if(fwd.sqrMagnitude<1e-4f)fwd=Vector3.forward;float tilt=time<p.LandAt?Mathf.Sin(time*3+p.Id)*6:0;
                    DogDraw(DogFixed("anvil"),Matrix4x4.TRS(at+Vector3.up*h,Quaternion.LookRotation(fwd.normalized,Vector3.up)*Quaternion.Euler(tilt,p.Id*37%360,0),new Vector3(1.35f/Mathf.Sqrt(squash),1.35f*squash,1.35f/Mathf.Sqrt(squash))*shrink));
                    break;
                }
                case NativeTopDog.PropPeel:
                {
                    var hn=NativeDogView.PropAt(p,time);var here=new Vector3(hn.X,hn.Y,hn.Z);bool down=time>=p.LandAt;var spin=down?Quaternion.Euler(0,p.Id*53%360,0):Quaternion.Euler((time-p.At)*600,(time-p.At)*240+p.Id*40,0);
                    DogDraw(DogFixed("peel"),Matrix4x4.TRS(here+(down?Vector3.up*.002f:Vector3.zero),spin,Vector3.one*1.25f));break;
                }
                case NativeTopDog.PropPie:
                {
                    if(p.LandAt>-50)return;var hn=NativeDogView.PropAt(p,time);var here=new Vector3(hn.X,hn.Y,hn.Z);var vel=Vec(p.Vel)-Vector3.up*(NativeTopDog.PieGravity*Mathf.Max(0,time-p.SampleAt));
                    // Cream first, spinning a little like a thrown plate.
                    DogDraw(DogFixed("pie"),Matrix4x4.TRS(here,Quaternion.LookRotation(vel.sqrMagnitude>.01f?vel.normalized:Vector3.forward,Vector3.up)*Quaternion.Euler(90,0,0)*Quaternion.Euler(0,(time-p.At)*420,0),Vector3.one));break;
                }
                case NativeTopDog.PropGlove:
                {
                    if(time>=p.Until)return;float ext=NativeDogView.GloveExtent(p,time);if(dir.sqrMagnitude<.5f)return;dir.Normalize();var hand=DogHand(owner,false);var fist=hand+dir*Mathf.Max(.2f,ext);
                    DogDraw(DogFixed("glove"),Look(fist,dir,Vector3.up,1.4f));float reach=Vector3.Distance(hand,fist)/1.4f;
                    var arm=DogLive(kit=>NativeTopDogModels.ScissorArm(kit,NMatrix.Identity,reach,tier));DogDraw(arm,Look(fist,dir,Vector3.up,1.4f));break;
                }
                case NativeTopDog.PropSpring:
                {
                    if(time>=p.Until)return;float h=NativeDogView.SpringHeight(p,time);var m=DogLive(kit=>NativeTopDogModels.Spring(kit,NMatrix.Identity,h,tier));DogDraw(m,Matrix4x4.TRS(at,Quaternion.Euler(0,p.Id*31%360,0),Vector3.one*1.3f));break;
                }
                case NativeTopDog.PropPuddle:
                {
                    if(time>=p.Until)return;float grow=Mathf.Clamp01((time-p.At)/.25f),dry=1-Mathf.Clamp01((time-(p.Until-.6f))/.6f);float r=p.Size*grow*dry;if(r<.02f)return;
                    DogDraw(DogPuddleMesh(),Matrix4x4.TRS(at+Vector3.up*.012f,Quaternion.Euler(0,p.Id*71%360,0),new Vector3(r,1,r*.85f)));break;
                }
            }
        }
        // A flat mustard puddle of radius 1 (a squashed blob with lobes), built once.
        private Mesh dogPuddle;
        private Mesh DogPuddleMesh()
        {
            if(dogPuddle!=null)return dogPuddle;var k=new NativeFallenKit();var ring=new NVector[28];var lid=new NVector[28];
            for(int i=0;i<28;i++){float a=i*Mathf.PI*2/28,r=1+.14f*Mathf.Sin(a*3+1)+.08f*Mathf.Sin(a*5);ring[i]=new NVector(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r);lid[i]=new NVector(ring[i].X*.92f,.018f,ring[i].Z*.92f);}
            k.Loft(new List<NVector[]>{ring,lid},new[]{NVector.Zero,new NVector(0,.018f,0)},NativeTopDogModels.Mustard,line:false,capLast:true);k.Finish();
            return dogPuddle=FbKitMesh(k,Matrix4x4.identity,"Top Dog puddle",out _);
        }

        // ---- All You Can Eat ----
        private void DogFeast(Snapshot s,Actor caster,NativeDogFeast f,float time,int tier)
        {
            Actor victim=null;if(f.Victim!=0&&s.Actors!=null)foreach(var a in s.Actors)if(a!=null&&a.Id==f.Victim){victim=a;break;}
            int phase=NativeDogView.LassoPhase(f,time);bool mine=FirstPerson&&caster.Id==LocalId;var right=DogHand(caster,false);
            // The links.
            if(phase==1&&!mine)
            {
                float t=time-f.At;var top=right+Vector3.up*.35f;var knots=new List<NVector>();
                for(int i=0;i<=12;i++){float a=t*16+i*Mathf.PI*2/12;knots.Add(V(top+new Vector3(Mathf.Cos(a)*.6f,.06f*Mathf.Sin(a*2),Mathf.Sin(a)*.6f)));}
                knots.Insert(0,V(right));DogDraw(DogLive(kit=>NativeTopDogModels.Links(kit,knots,tier)),Matrix4x4.identity);
            }
            else if(phase==2)
            {
                var loop=Vec(f.Lasso);var fly=Vec(f.LassoVel);if(fly.sqrMagnitude<.01f)fly=Vec(f.Dir);var fwd=fly.normalized;var side=Vector3.Cross(Vector3.up,fwd).normalized;var up=Vector3.Cross(fwd,side);
                float t=time-f.Throw;var knots=new List<NVector>();int tail=10;
                for(int i=0;i<=tail;i++){float u=i/(float)tail;var p=Vector3.Lerp(right,loop,u)-Vector3.up*(.6f*4*u*(1-u))+side*(.15f*Mathf.Sin(u*9-t*18)*(1-u));knots.Add(V(p));}
                for(int i=1;i<=12;i++){float a=i*Mathf.PI*2/12+t*10;knots.Add(V(loop+(side*Mathf.Cos(a)+up*Mathf.Sin(a))*.55f));}
                DogDraw(DogLive(kit=>NativeTopDogModels.Links(kit,knots,tier)),Matrix4x4.identity);
            }
            else if(phase==3&&victim!=null)
            {
                // Wrapped twice round the victim's chest and belly, the arms pinned in it.
                var feet=visualPositions.TryGetValue(victim.Id,out var vf)?vf:Vec(victim.Position);float belly=NativeTopDog.Belly(f,time);var knots=new List<NVector>();
                for(int i=0;i<=26;i++){float u=i/26f,a=u*Mathf.PI*4,h=1.32f-.42f*u,r=.33f+.03f*Mathf.Sin(u*Mathf.PI)*belly;knots.Add(V(feet+new Vector3(Mathf.Cos(a)*r,h,Mathf.Sin(a)*r)));}
                dogWarps.TryGetValue(victim.Id,out var warp);DogDraw(DogLive(kit=>NativeTopDogModels.Links(kit,knots,tier)),Matrix4x4.identity,1,warp.belly,warp.axis);
            }
            else if(phase==4)
            {
                var at=f.Catch>-50&&victim!=null?(visualPositions.TryGetValue(victim.Id,out var vf)?vf:Vec(victim.Position)):Vec(f.Lasso);var ground=new Vector3(at.x,f.Catch>-50?at.y:at.y-.75f,at.z);
                float fade=Mathf.Clamp01(1-(time-(f.Snap>-50?f.Snap:f.Until-.6f))/.8f);var knots=new List<NVector>();for(int i=0;i<=12;i++){float a=i*Mathf.PI*2/12;knots.Add(V(ground+new Vector3(Mathf.Cos(a)*.5f*fade,.04f,Mathf.Sin(a)*.5f*fade)));}
                if(fade>.05f)DogDraw(DogLive(kit=>NativeTopDogModels.Links(kit,knots,tier)),Matrix4x4.identity);
            }
            // The hot dogs: pulled from behind the back, offered, pushed in wiggling.
            if(victim!=null&&f.Catch>-50&&DogMouth(victim,out var mouth,out var face))
            {
                var inward=-face;
                for(int i=0;i<NativeTopDog.Feeds;i++)
                {
                    int st=NativeDogView.FeedPhase(f,i,time,out float move,out float into);if(st==0||st==3)continue;
                    var hand=DogHand(caster,i%2==1);float wig=time*16+i*1.7f;Matrix4x4 m;float from=0,to=1;
                    var front=mouth-inward*(NativeTopDogModels.DogLength*.5f);
                    if(st==1){var c=Vector3.Lerp(hand,front,move*move*(3-2*move));var d=Vector3.Slerp((front-hand).sqrMagnitude>1e-4f?(front-hand).normalized:inward,inward,move);m=Look(c,d,Vector3.up,1.15f);}
                    else{float l=NativeTopDogModels.DogLength*1.15f;m=Look(mouth+inward*(l*into-l*.5f),inward,Vector3.up,1.15f);to=1-into;}
                    float amp=st==1?1:.8f;DogDraw(DogLive(kit=>NativeTopDogModels.HotDog(kit,NMatrix.Identity,wig,amp,from,to,tier)),m);
                }
            }
        }
        // The pop's hot dogs: flung out of the burst on high arcs, a bounce, then lying there wiggling until they fade.
        private void DogShower(NativeDogHit h,float time,int tier)
        {
            float age=time-h.At;if(age<0||age>1.5f)return;var at=Vec(h.Pos);int n=LowGraphics?4:NativeTopDog.Feeds;
            for(int i=0;i<n;i++)
            {
                float a=i*Mathf.PI*2/n+h.Id*.37f,sp=3.2f+1.6f*Mathf.Repeat(i*.618f+h.Id*.1f,1),up=6+2.5f*Mathf.Repeat(i*.414f,1);var d=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                float t=age,land=2*up/23;Vector3 p;Quaternion q;
                if(t<land){p=at+d*(sp*t)+Vector3.up*(up*t-11.5f*t*t+.4f);q=Quaternion.Euler(t*720+i*60,a*Mathf.Rad2Deg,t*300);}
                else{float b=t-land;p=at+d*(sp*land+1.2f*b*(1-b))+Vector3.up*(Mathf.Max(0,1.2f*b-3*b*b)+.03f);q=Quaternion.Euler(0,a*Mathf.Rad2Deg+i*20,0);}
                float fade=1-Mathf.Clamp01((age-1.2f)/.3f);if(fade<=.02f)continue;
                DogDraw(DogLive(kit=>NativeTopDogModels.HotDog(kit,NMatrix.Identity,time*12+i,t<land?1:.5f,0,1,tier)),Matrix4x4.TRS(p,q,Vector3.one*1.15f*fade));
            }
        }
        private static NVector V(Vector3 v)=>new NVector(v.x,v.y,v.z);

        // ---- the body through it all ----
        private sealed class DogBodyRig{public Transform S1,S2,S3,Neck,Head;public Vector3 RootScale;public bool Moved,Read,Warped;public int Frame;}
        private readonly Dictionary<VisualBody,DogBodyRig> dogBodies=new Dictionary<VisualBody,DogBodyRig>();
        // The belly's warp on each victim this frame (its centre and push, its forward and radius), for the links round them.
        private readonly Dictionary<int,(Vector4 belly,Vector4 axis)> dogWarps=new Dictionary<int,(Vector4,Vector4)>();
        private static readonly int DogBellyId=Shader.PropertyToID("_Belly"),DogBellyAxisId=Shader.PropertyToID("_BellyAxis");
        /// <summary>The belly's warp on every renderer of `body` (Resources NativeVisualBodyFx.cginc BellyVert; zero: off).</summary>
        private static void DogBelly(VisualBody body,Vector4 belly,Vector4 axis)
        {
            if(body?.All==null)return;
            foreach(var r in body.All){if(r==null)continue;r.GetPropertyBlock(body.Block);body.Block.SetVector(DogBellyId,belly);body.Block.SetVector(DogBellyAxisId,axis);r.SetPropertyBlock(body.Block);}
        }
        /// <summary>A body the feast left (no longer posed: dead, gone, a round over) put back: no warp, its own size.</summary>
        private void DogBodiesSweep()
        {
            foreach(var kv in dogBodies){var r=kv.Value;if(!r.Moved||r.Frame>=Time.frameCount-1)continue;if(r.Warped){DogBelly(kv.Key,Vector4.zero,Vector4.zero);r.Warped=false;}if(kv.Key.Root!=null)kv.Key.Root.transform.localScale=r.RootScale;r.Moved=false;}
        }
        /// <summary>VisualBodyPose, after the Hot Dog's jiggle: a feast's victim swells (a warp about the belly in the body's shader, so
        /// the skin and everything worn on it swell together), their head thrown back on the hot dogs; a pancaked pilot squashed flat
        /// and popping back.</summary>
        private void TopDogBody(VisualBody body,Actor a,float time)
        {
            float belly=0,tremble=0,back=0,flat=0;
            if(dogVictims.TryGetValue(a.Id,out var v)){var f=v.feast;belly=NativeTopDog.Belly(f,time);tremble=NativeTopDog.Tremble(f,time);if(time<f.Release&&(f.Snap<-50||time<f.Snap))back=Mathf.Clamp01((time-f.Catch-.3f)/.3f)*(1-Mathf.Clamp01((time-f.Release+.15f)/.15f));}
            var pan=DogMarkOn(a.Id,NativeTopDog.Pancake,time);
            if(pan!=null){float t=time-pan.At,left=pan.Until-time;flat=Mathf.Clamp01(t/.05f)*Mathf.Clamp01(left/.12f);}
            if(!dogBodies.TryGetValue(body,out var r))
            {
                if(belly<=0&&back<=0&&flat<=0)return;r=new DogBodyRig();dogBodies[body]=r;
                // The skin's own bones (the body mesh's, as the Hot Dog binds to): the root also holds other skeletons by the same names.
                Transform[] bones=null;foreach(var s in body.Root.GetComponentsInChildren<SkinnedMeshRenderer>(true))if(s.name.StartsWith("Body.",StringComparison.Ordinal)&&s.sharedMesh!=null){bones=s.bones;break;}
                if(bones!=null)foreach(var t in bones){if(t==null)continue;switch(t.name){case "spine_01":r.S1=t;break;case "spine_02":r.S2=t;break;case "spine_03":r.S3=t;break;case "neck_01":r.Neck=t;break;case "head":r.Head=t;break;}}
                r.RootScale=body.Root.transform.localScale;r.Read=true;
            }
            bool any=belly>0||back>0||flat>0;if(any&&!r.Moved)r.RootScale=body.Root.transform.localScale;r.Frame=Time.frameCount;
            if(!any)
            {
                if(r.Warped){DogBelly(body,Vector4.zero,Vector4.zero);r.Warped=false;}dogWarps.Remove(a.Id);
                if(r.Moved){body.Root.transform.localScale=r.RootScale;r.Moved=false;}return;
            }
            // The belly: pushed out round a centre between spine_01 and spine_02, a little forward of it, most at the front (the way the
            // face looks, level: the mouth's frame, DogMouth); its radius from the torso's length, its push a third of it once stuffed
            // and over half of it at the pop, trembling before it.
            float amount=Mathf.Min(1.1f,.75f*belly)*(1+tremble);
            if(amount>.001f&&r.S1!=null&&r.S2!=null&&r.S3!=null)
            {
                var head=VisualHeadOf(body);var fwd=head!=null?head.TransformDirection(Vector3.forward):Quaternion.Euler(0,a.Yaw,0)*Vector3.forward;
                fwd.y=0;fwd=fwd.sqrMagnitude>1e-6f?fwd.normalized:Quaternion.Euler(0,a.Yaw,0)*Vector3.forward;
                float radius=Mathf.Clamp(3f*Vector3.Distance(r.S1.position,r.S3.position),.32f,.45f);
                var c=Vector3.Lerp(r.S1.position,r.S2.position,.5f)+fwd*(.05f*radius);
                var bv=new Vector4(c.x,c.y,c.z,.5f*amount);var av=new Vector4(fwd.x,fwd.y,fwd.z,radius);
                DogBelly(body,bv,av);r.Warped=true;dogWarps[a.Id]=(bv,av);
            }
            else{if(r.Warped){DogBelly(body,Vector4.zero,Vector4.zero);r.Warped=false;}dogWarps.Remove(a.Id);}
            // The head thrown back for the hot dogs, about the body's own right.
            if(back>0&&r.Neck!=null&&r.Head!=null){var axis=body.Root.transform.right;r.Neck.rotation=Quaternion.AngleAxis(-14*back,axis)*r.Neck.rotation;r.Head.rotation=Quaternion.AngleAxis(-22*back,axis)*r.Head.rotation;}
            // Flattened: squashed down to a fifth, wide, with a wobble as it springs back.
            if(flat>0){float sy=Mathf.Lerp(1,.22f,flat),sx=1/Mathf.Sqrt(sy);body.Root.transform.localScale=Vector3.Scale(r.RootScale,new Vector3(sx,sy,sx));}
            else body.Root.transform.localScale=r.RootScale;
            r.Moved=true;
        }

        // ---- the effects and the camera ----
        /// <summary>RenderGlow, after the Bombadier's: the kit's particles.</summary>
        private void DogFx(float now)
        {
            if(kitScene==null||vfx==null||NativeDogView.Source==null)return;var draw=WeaponFxDraw;if(draw==null)return;techSink??=new TechSink();techSink.Bind(vfx,draw,FirstPerson&&!ScopeActive&&ViewCamera!=null);
            techniqueFx.DrawDogs(techSink,kitScene,kitClock,Mathf.Clamp01(WeaponEffectsQuality),LowGraphics);
        }
        /// <summary>All You Can Eat on the local pilot's own camera, the Top Dog's or its victim's (NativeCinematicCamera.Direct). False when it
        /// is not this pilot's.</summary>
        internal bool DogFeastDirect(NativeArena arena,Camera view,float time,bool shake,bool reduced)
        {
            if(view==null||arena?.Local==null||NativeDogView.Source==null)return false;NativeDogFeast f=null;int caster=0;bool victim=false;
            var mine=NativeDogView.Source.Of(arena.LocalId);if(mine?.Feast!=null&&NativeDogCamera.Active(mine.Feast,time,false)){f=mine.Feast;caster=arena.LocalId;}
            else if(dogVictims.TryGetValue(arena.LocalId,out var v)&&NativeDogCamera.Active(v.feast,time,true)){f=v.feast;caster=v.caster;victim=true;}
            if(f==null)return false;
            var feet=visualPositions.TryGetValue(caster,out var cf)?cf:view.transform.position;var body=f.Victim!=0&&visualPositions.TryGetValue(f.Victim,out var vf)?vf+Vector3.up*1.05f:feet+Vec(f.Dir)*NativeTopDog.HoldAhead+Vector3.up*1.05f;
            var shot=NativeDogCamera.At(f,V(feet),V(body),time,victim,reduced,shake);if(shot.Blend<=0)return false;
            Vector3 position=new Vector3(shot.Position.X,shot.Position.Y,shot.Position.Z),look=new Vector3(shot.Look.X,shot.Look.Y,shot.Look.Z),ray=position-look;float distance=ray.magnitude;
            if(distance>.05f&&arena.Root!=null){ray/=distance;if(arena.Root.scene.GetPhysicsScene().Raycast(look+ray*.12f,ray,out var hit,Mathf.Max(0,distance-.12f),NativeArena.WorldMask,QueryTriggerInteraction.Ignore))position=look+ray*Mathf.Max(.3f,hit.distance);}
            NativeSirenCutscene.Shake(time,shot.Shake,out float dx,out float dy,out float roll);var forward=look-position;if(forward.sqrMagnitude<1e-8f)forward=view.transform.forward;var rotation=Quaternion.LookRotation(forward,Vector3.up)*Quaternion.AngleAxis(-roll*Mathf.Rad2Deg,Vector3.forward);
            float b=shot.Blend;view.transform.SetPositionAndRotation(Vector3.Lerp(view.transform.position,position+new Vector3(-dx,dy,0),b),Quaternion.Slerp(view.transform.rotation,rotation,b));view.fieldOfView=Mathf.Lerp(view.fieldOfView,shot.Fov,b);FirstPerson=false;AimHeld=false;return true;
        }
        /// <summary>The mallet's share shown in the hands this moment (hidden away while the hands are busy with a prop or the feast).</summary>
        internal static float TopDogMallet(Actor a,float time)=>a?.SourceSpec!=NativeTopDog.Spec?1:NativeDogView.MalletShown(NativeDogView.Of(a),time);
    }
}
