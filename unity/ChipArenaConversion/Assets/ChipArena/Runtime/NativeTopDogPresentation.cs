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
            dogVictims.Clear();dogMarks.Clear();dogNow=time;HotDogFaceSource=hotDogFaceSource??=HotDogFaceMood;DogBodiesSweep();if(s?.Actors==null)return;
            foreach(var a in s.Actors)
            {
                var k=a==null?null:NativeDogView.Of(a);if(k==null)continue;
                if(k.Feast!=null&&k.Feast.Catch>-50&&k.Feast.Victim!=0)dogVictims[k.Feast.Victim]=(k.Feast,a.Id);
                if(k.Marks!=null)foreach(var m in k.Marks)if(m!=null)dogMarks.Add((m.Id,m));
            }
        }
        private void ClearDog(){dogPreview.Clear();NativeDogView.Nozzles.Clear();dogSwings.Clear();dogMalletToon.Clear();if(ReferenceEquals(NativeDogView.Source,dogPreview)||ReferenceEquals(NativeDogView.Source,dogSnapshot))NativeDogView.Source=null;dogVictims.Clear();dogMarks.Clear();foreach(var kv in dogBodies)if(kv.Value.Warped)DogBelly(kv.Key,Vector4.zero,Vector4.zero);dogBodies.Clear();dogWarps.Clear();dogArms.Clear();dogFeedHands.Clear();dogPins.Clear();dogWraps.Clear();}
        private static NativeDogMark DogMarkOn(int id,string kind,float time){foreach(var (who,m) in dogMarks)if(who==id&&m.Kind==kind&&time>=m.At&&time<m.Until)return m;return null;}
        /// <summary>The face's share of the feast for pilot `id` at `time` (VisualFacePilot): the mouth wide on each hot dog, stuffed after.</summary>
        internal static void TopDogFace(int id,float time,out float gobble,out float bloat)
        {
            gobble=bloat=0;if(!dogVictims.TryGetValue(id,out var v))return;var f=v.feast;if(f.Snap>-50&&time>=f.Snap+.3f)return;
            // The mouth as the hands work it (NativeDogView.Jaw): pulled wide by the chin, closing a little round each sausage as it goes in.
            if(time<f.Release){gobble=NativeDogView.Jaw(f,time);return;}if(time<f.Pop)bloat=1;
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
        private int dogIntroActor=-1;private float dogMalletScale,dogIntroClock;private Func<int,Matrix4x4> dogIntroHand;
        private readonly List<NativeIntroToon> dogIntroToons=new List<NativeIntroToon>();private readonly Dictionary<int,Mesh> dogCarts=new Dictionary<int,Mesh>();
        /// <summary>The entrance's toon models in the Top Dog's shader, out of the source frame (x mirrored).</summary>
        private void DogIntroToons(NativeTopDogIntro intro,float t)
        {
            hotDogIntroAge=t;hotDogIntroFrame=Time.frameCount;hotDogIntroId=dogIntroActor;HotDogFaceSource=hotDogFaceSource??=HotDogFaceMood;dogPoolUsed=0;if(DogMaterials()==null)return;intro.Toons(t,dogIntroToons);int tier=LowGraphics?1:0;
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
            NativeFallenKit k=id=="anvil"?NativeTopDogModels.Anvil(tier):id=="peel"?NativeTopDogModels.BananaPeel(tier):id=="pie"?NativeTopDogModels.Pie(tier):id=="glove"?NativeTopDogModels.Glove(tier):id=="bottle"?NativeTopDogModels.Bottle(tier):id=="star"?NativeTopDogModels.StarShape(tier):id=="piano"?NativeTopDogModels.Piano(tier):null;
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
                    // Held round its middle, its nozzle along the aim; the stream leaves the nozzle (NativeDogView.Nozzles).
                    var hand=DogHand(a,true);var aim=Quaternion.Euler(-a.Pitch,a.Yaw,0)*Vector3.forward;const float size=1.6f;var foot=hand-aim*(.1f*size);
                    DogDraw(DogFixed("bottle"),Matrix4x4.TRS(foot,Quaternion.FromToRotation(Vector3.up,aim),Vector3.one*size));
                    var nozzle=foot+aim*(NativeTopDogModels.BottleTip*size);NativeDogView.Nozzles[a.Id]=(time,new NVector(nozzle.x,nozzle.y,nozzle.z),new NVector(aim.x,aim.y,aim.z));
                }
                else NativeDogView.Nozzles.Remove(a.Id);
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
                    DogDraw(DogFixed("anvil"),Matrix4x4.TRS(at+Vector3.up*h,Quaternion.LookRotation(fwd.normalized,Vector3.up)*Quaternion.Euler(tilt,p.Id*37%360,0),new Vector3(1.35f/Mathf.Sqrt(squash),1.35f*squash,1.35f/Mathf.Sqrt(squash))*shrink*(p.Size>0?p.Size:1)));
                    break;
                }
                case NativeTopDog.PropPiano:
                {
                    // Hammerspace's grand piano: down out of nowhere, a squash on landing, then it sits there till it goes in a puff.
                    if(time>=p.Until)return;float h=NativeDogView.AnvilDrop(p,time,out float squash);float shrink=1-Mathf.Clamp01((time-(p.Until-.3f))/.3f);if(shrink<=.01f)return;
                    var fwd=new Vector3(dir.x,0,dir.z);if(fwd.sqrMagnitude<1e-4f)fwd=Vector3.forward;float tilt=time<p.LandAt?Mathf.Sin(time*4+p.Id)*9:0;float grow=Mathf.Clamp01((time-p.At)/.12f);
                    DogDraw(DogFixed("piano"),Matrix4x4.TRS(at+Vector3.up*h,Quaternion.LookRotation(-fwd.normalized,Vector3.up)*Quaternion.Euler(tilt,0,tilt*.6f),new Vector3(1/Mathf.Sqrt(squash),squash,1/Mathf.Sqrt(squash))*shrink*grow));
                    break;
                }
                case NativeTopDog.PropSnack:
                {
                    // Second Helpings' hot dogs: flying out of the pop, then wiggling on the floor, blinking out at the end.
                    if(time>=p.Until)return;var hn=NativeDogView.PropAt(p,time);var here=new Vector3(hn.X,hn.Y,hn.Z);bool down=time>=p.LandAt;float left=p.Until-time;if(left<1.5f&&Mathf.Repeat(left*6,1)<.35f)return;
                    var turn=down?Quaternion.Euler(0,p.Id*47%360+Mathf.Sin(time*3+p.Id)*12,0):Quaternion.Euler((time-p.At)*500,(time-p.At)*200+p.Id*40,0);float bob=down?.06f+.03f*Mathf.Abs(Mathf.Sin(time*5+p.Id)):0;
                    DogDraw(DogLive(kit=>NativeTopDogModels.HotDog(kit,NMatrix.Identity,time*9+p.Id,down?.6f:1,0,1,tier)),Matrix4x4.TRS(here+Vector3.up*bob,turn,Vector3.one*1.1f));break;
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
                    DogDraw(DogPuddleMesh(p.Hit==1),Matrix4x4.TRS(at+Vector3.up*.012f,Quaternion.Euler(0,p.Id*71%360,0),new Vector3(r,1,r*.85f)));break;
                }
            }
        }
        // A flat mustard puddle of radius 1 (a squashed blob with lobes), built once.
        private Mesh dogPuddle,dogSaucePuddle;
        /// <summary>The puddle mesh: mustard, or Hot Sauce's red.</summary>
        private Mesh DogPuddleMesh(bool sauce=false)
        {
            if(sauce?dogSaucePuddle!=null:dogPuddle!=null)return sauce?dogSaucePuddle:dogPuddle;var k=new NativeFallenKit();var ring=new NVector[28];var lid=new NVector[28];
            for(int i=0;i<28;i++){float a=i*Mathf.PI*2/28,r=1+.14f*Mathf.Sin(a*3+1)+.08f*Mathf.Sin(a*5);ring[i]=new NVector(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r);lid[i]=new NVector(ring[i].X*.92f,.018f,ring[i].Z*.92f);}
            k.Loft(new List<NVector[]>{ring,lid},new[]{NVector.Zero,new NVector(0,.018f,0)},sauce?NativeTopDogModels.Ketchup:NativeTopDogModels.Mustard,line:false,capLast:true);k.Finish();
            var made=FbKitMesh(k,Matrix4x4.identity,sauce?"Top Dog sauce":"Top Dog puddle",out _);if(sauce)dogSaucePuddle=made;else dogPuddle=made;return made;
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
                // Wrapped three times round the victim's chest, pinned arms and belly, on its spine as it twists (DogStruggle), squeezing in
                // and bulging out where the elbows shove on each heave.
                float belly=NativeTopDog.Belly(f,time);var knots=new List<NVector>();
                if(dogWraps.TryGetValue(victim.Id,out var w)&&w.frame==Time.frameCount)
                {
                    for(int i=0;i<=36;i++)
                    {
                        // An oval snug to the body: wide across the pinned arms, close front to back (the belly pushing the front out).
                        float u=i/36f,a=u*Mathf.PI*6+w.twist,side=Mathf.Cos(a),front=Mathf.Sin(a),squeeze=1-.03f*w.strain;
                        float across=.3f*squeeze+.06f*w.strain*side*side*side*side,deep=(front>0?.22f+.1f*belly*Mathf.Sin(u*Mathf.PI):.2f)*squeeze;
                        knots.Add(V(Vector3.Lerp(w.top,w.bottom,u)+w.right*(side*across)+w.fwd*(front*deep)));
                    }
                }
                else
                {
                    var feet=visualPositions.TryGetValue(victim.Id,out var vf)?vf:Vec(victim.Position);
                    for(int i=0;i<=26;i++){float u=i/26f,a=u*Mathf.PI*4,h=1.32f-.42f*u,r=.33f+.03f*Mathf.Sin(u*Mathf.PI)*belly;knots.Add(V(feet+new Vector3(Mathf.Cos(a)*r,h,Mathf.Sin(a)*r)));}
                }
                dogWarps.TryGetValue(victim.Id,out var warp);DogDraw(DogLive(kit=>NativeTopDogModels.Links(kit,knots,tier)),Matrix4x4.identity,1,warp.belly,warp.axis);
            }
            else if(phase==4)
            {
                var at=f.Catch>-50&&victim!=null?(visualPositions.TryGetValue(victim.Id,out var vf)?vf:Vec(victim.Position)):Vec(f.Lasso);var ground=new Vector3(at.x,f.Catch>-50?at.y:at.y-.75f,at.z);
                float fade=Mathf.Clamp01(1-(time-(f.Snap>-50?f.Snap:f.Until-.6f))/.8f);var knots=new List<NVector>();for(int i=0;i<=12;i++){float a=i*Mathf.PI*2/12;knots.Add(V(ground+new Vector3(Mathf.Cos(a)*.5f*fade,.04f,Mathf.Sin(a)*.5f*fade)));}
                if(fade>.05f)DogDraw(DogLive(kit=>NativeTopDogModels.Links(kit,knots,tier)),Matrix4x4.identity);
            }
            // The hot dogs: pulled from behind the back, offered, pushed in wiggling.
            // The sausages, bare (owner 2026-11-09): in the hand from the hip to the mouth (where the hand's IK put it, TopDogBody), pushed half
            // in, stuck there squirming for a few seconds, several at once on their fan round the mouth, then slurped down; spat out on a snap.
            if(victim!=null&&f.Catch>-50&&DogMouth(victim,out var mouth,out var face))
            {
                float l=DogHeld;bool posed=dogFeedHands.TryGetValue(caster.Id,out var hands)&&hands.frame==Time.frameCount&&hands.dir.sqrMagnitude>.5f;
                for(int i=0;i<NativeTopDog.Feeds;i++)
                {
                    int st=NativeDogView.FeedState(f,i,time,out float u);if(st==0)continue;
                    var into=DogSlotDir(face,i);var lip=DogLip(mouth,face,i);float wig=time*(st==4?21:14)+i*1.7f;
                    // In the hand (carried and pushed): held by its end between the thumb and fingers (DogFeedArms keeps the pinch and the
                    // fingers' way), the rest of it ahead of them.
                    bool held=posed&&st<=3&&NativeDogView.FeedHand(f,0,time,out int holding,out _)&&holding==i;
                    if(st<=2)
                    {
                        float pop=st==1?Mathf.Clamp01((time-(NativeTopDog.OfferAt(f.FeedAt,i)-NativeTopDog.Appear))/.08f):1;
                        if(held)DogDraw(DogLive(kit=>NativeTopDogModels.HotDog(kit,NMatrix.Identity,wig,1,0,1,tier,false)),Look(hands.grip+hands.dir*(l*(DogHold-.5f)),hands.dir,Vector3.up,DogFed*(.4f+.6f*pop)));
                        else
                        {
                            Vector3 hand=DogHand(caster,false);var aim=lip-hand;var dir=aim.sqrMagnitude>1e-4f?aim.normalized:into;if(st==2)dir=Vector3.Slerp(dir,into,u*u);
                            DogDraw(DogLive(kit=>NativeTopDogModels.HotDog(kit,NMatrix.Identity,wig,1,0,1,tier,false)),Look(hand+dir*(l*.42f),dir,Vector3.up,DogFed*(.4f+.6f*pop)));
                        }
                    }
                    else if(st==3)DogDraw(DogLive(kit=>NativeTopDogModels.HotDog(kit,NMatrix.Identity,wig,.7f,0,1-.5f*u,tier,false)),held?Look(hands.grip+hands.dir*(l*(DogHold-.5f)),hands.dir,Vector3.up,DogFed):Look(lip+into*(l*.5f*u-l*.5f),into,Vector3.up,DogFed));
                    else if(st<=5){float showing=st==4?.5f:.5f*(1-u*u),amp=st==4?1.25f+.25f*Mathf.Sin(time*3+i):.5f;DogDraw(DogLive(kit=>NativeTopDogModels.Stuck(kit,NMatrix.Identity,wig,amp,showing,tier)),Look(lip,into,Vector3.up,DogFed));}
                    else
                    {
                        // Spat out: flung off the lips, tumbling, to the floor.
                        float t=u*NativeTopDog.Spit;var at=lip+(face*(2.4f+.3f*i)+Vector3.up*(1.6f+.2f*i)+Vector3.Cross(Vector3.up,face).normalized*((i%3-1)*.8f))*t-Vector3.up*(5*t*t);
                        DogDraw(DogLive(kit=>NativeTopDogModels.HotDog(kit,NMatrix.Identity,wig,1,0,1,tier,false)),Matrix4x4.TRS(at,Quaternion.Euler(t*900+i*40,t*400,i*30),Vector3.one*DogFed*(1-Mathf.Clamp01((u-.8f)/.2f))));
                    }
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
                DogDraw(DogLive(kit=>NativeTopDogModels.HotDog(kit,NMatrix.Identity,time*12+i,t<land?1:.5f,0,1,tier,false)),Matrix4x4.TRS(p,q,Vector3.one*1.15f*fade));
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
            DogFeedArms(body,a,time);
            float belly=0,tremble=0,back=0,flat=0,bound=0;
            if(dogVictims.TryGetValue(a.Id,out var v)){var f=v.feast;belly=NativeTopDog.Belly(f,time);tremble=NativeTopDog.Tremble(f,time);bound=NativeTopDog.Bound(f,time);if(time<f.Release&&(f.Snap<-50||time<f.Snap))back=Mathf.Clamp01((time-f.Catch-.3f)/.3f)*(1-Mathf.Clamp01((time-f.Release+.15f)/.15f));}
            var pan=DogMarkOn(a.Id,NativeTopDog.Pancake,time);
            if(pan!=null){float t=time-pan.At,left=pan.Until-time;flat=Mathf.Clamp01(t/.05f)*Mathf.Clamp01(left/.12f);}
            if(!dogBodies.TryGetValue(body,out var r))
            {
                if(belly<=0&&back<=0&&flat<=0&&bound<=0)return;r=new DogBodyRig();dogBodies[body]=r;
                // The skin's own bones (the body mesh's, as the Hot Dog binds to): the root also holds other skeletons by the same names.
                Transform[] bones=null;foreach(var s in body.Root.GetComponentsInChildren<SkinnedMeshRenderer>(true))if(s.name.StartsWith("Body.",StringComparison.Ordinal)&&s.sharedMesh!=null){bones=s.bones;break;}
                if(bones!=null)foreach(var t in bones){if(t==null)continue;switch(t.name){case "spine_01":r.S1=t;break;case "spine_02":r.S2=t;break;case "spine_03":r.S3=t;break;case "neck_01":r.Neck=t;break;case "head":r.Head=t;break;}}
                r.RootScale=DogRootBase(body);r.Read=true;
            }
            bool any=belly>0||back>0||flat>0||bound>0;if(any&&!r.Moved)r.RootScale=DogRootBase(body);r.Frame=Time.frameCount;
            if(!any)
            {
                if(r.Warped){DogBelly(body,Vector4.zero,Vector4.zero);r.Warped=false;}dogWarps.Remove(a.Id);
                if(r.Moved){body.Root.transform.localScale=r.RootScale;r.Moved=false;}return;
            }
            if(bound>0)DogStruggle(body,a,r,v.feast,bound,time);else dogWraps.Remove(a.Id);
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
            // The face turned to the Top Dog's hands for the sausages, tipped a little back, mouth up to them (owner 2026-11-09: "literally
            // animate opening the players mouth and just putting hotdogs in there"): the neck takes a share, the head the rest.
            if(back>0&&r.Neck!=null&&r.Head!=null&&v.caster!=0)
            {
                var feet=visualPositions.TryGetValue(v.caster,out var cf)?cf:r.Head.position;var hands=feet+Vector3.up*1.35f;var to=hands-r.Head.position;to.y=0;
                if(to.sqrMagnitude>1e-4f)
                {
                    // Each push shoves the head back a little more (NativeDogView.Push).
                    var want=Quaternion.AngleAxis(-14-8*NativeDogView.Push(v.feast,time),Vector3.Cross(Vector3.up,to.normalized))*to.normalized;
                    var cur=r.Head.TransformDirection(Vector3.forward);r.Neck.rotation=Quaternion.Slerp(Quaternion.identity,Quaternion.FromToRotation(cur,want),.4f*back)*r.Neck.rotation;
                    cur=r.Head.TransformDirection(Vector3.forward);r.Head.rotation=Quaternion.Slerp(Quaternion.identity,Quaternion.FromToRotation(cur,want),back)*r.Head.rotation;
                }
            }
            // Flattened: squashed down to a fifth, wide, with a wobble as it springs back.
            if(flat>0){float sy=Mathf.Lerp(1,.22f,flat),sx=1/Mathf.Sqrt(sy);body.Root.transform.localScale=Vector3.Scale(r.RootScale,new Vector3(sx,sy,sx));}
            else body.Root.transform.localScale=r.RootScale;
            r.Moved=true;
        }

        // ---- the wrapped victim (owner 2026-11-09: "give more of a visual effect when the character is wrapped by sausaged, like their arms
        // are downwards, and they're struggling to get out") ----
        private sealed class DogPinRig{public readonly Transform[] Upper=new Transform[2],Lower=new Transform[2],Hand=new Transform[2];}
        private readonly Dictionary<VisualBody,DogPinRig> dogPins=new Dictionary<VisualBody,DogPinRig>();
        // The wrap round each victim this frame: its top and bottom on the spine, its right and front, the strain and twist (the links read them).
        private readonly Dictionary<int,(int frame,Vector3 top,Vector3 bottom,Vector3 right,Vector3 fwd,float strain,float twist)> dogWraps=new Dictionary<int,(int,Vector3,Vector3,Vector3,Vector3,float,float)>();
        /// <summary>TopDogBody: a wrapped victim struggling (NativeTopDog.Bound, Strain): the spine twisting and rocking against the links, both
        /// arms pinned straight down its sides by IK, the elbows shoving out at the links and the shoulders heaving in jerks; the wrap's frame
        /// kept for the links.</summary>
        private void DogStruggle(VisualBody body,Actor a,DogBodyRig r,NativeDogFeast f,float bound,float time)
        {
            float strain=NativeTopDog.Strain(f,time,out float side),t=time-f.Catch;
            var head=VisualHeadOf(body);var fwd=head!=null?head.TransformDirection(Vector3.forward):Quaternion.Euler(0,a.Yaw,0)*Vector3.forward;fwd.y=0;
            fwd=fwd.sqrMagnitude>1e-6f?fwd.normalized:Quaternion.Euler(0,a.Yaw,0)*Vector3.forward;var right=Vector3.Cross(Vector3.up,fwd);
            // The spine: twisting side to side with each heave and rocking over toward the arm that pushes, the chest most.
            float twist=(10*Mathf.Sin(t*6.1f)+6*Mathf.Sin(t*13.7f+.7f))*(.45f+.55f*strain)*bound,rock=(5*Mathf.Sin(t*4.3f)+5*side*strain)*bound;
            if(r.S1!=null)r.S1.rotation=Quaternion.AngleAxis(twist*.35f,Vector3.up)*Quaternion.AngleAxis(rock*.5f,fwd)*r.S1.rotation;
            if(r.S2!=null)r.S2.rotation=Quaternion.AngleAxis(twist*.4f,Vector3.up)*Quaternion.AngleAxis(rock*.3f,fwd)*r.S2.rotation;
            if(r.S3!=null)r.S3.rotation=Quaternion.AngleAxis(twist*.25f,Vector3.up)*Quaternion.AngleAxis(rock*.2f,fwd)*r.S3.rotation;
            // The arms: straight down the sides inside the links, the elbows bowed out against them, the shoulders up on each heave.
            if(!dogPins.TryGetValue(body,out var pin))
            {
                pin=new DogPinRig();dogPins[body]=pin;Transform[] bones=null;
                foreach(var sk in body.Root.GetComponentsInChildren<SkinnedMeshRenderer>(true))if(sk.name.StartsWith("Body.",StringComparison.Ordinal)&&sk.sharedMesh!=null){bones=sk.bones;break;}
                if(bones!=null)foreach(var bn in bones){if(bn==null)continue;switch(bn.name){case "upperarm_r":pin.Upper[0]=bn;break;case "lowerarm_r":pin.Lower[0]=bn;break;case "hand_r":pin.Hand[0]=bn;break;case "upperarm_l":pin.Upper[1]=bn;break;case "lowerarm_l":pin.Lower[1]=bn;break;case "hand_l":pin.Hand[1]=bn;break;}}
            }
            for(int h=0;h<2;h++)
            {
                if(pin.Upper[h]==null||pin.Lower[h]==null||pin.Hand[h]==null)continue;float sign=h==0?1:-1,push=strain*(.55f+.45f*sign*side);
                var shoulder=pin.Upper[h].position;float reach=(pin.Lower[h].position-shoulder).magnitude+(pin.Hand[h].position-pin.Lower[h].position).magnitude;
                var target=shoulder+Vector3.down*(reach*.93f)+Vector3.up*(.06f*push)+right*(sign*(.05f+.06f*push))+fwd*.03f;
                DogReach(pin.Upper[h],pin.Lower[h],pin.Hand[h],target,-fwd*.6f+right*(sign*(.5f+.7f*push)),bound,1,Vector3.one,Vector3.one);
            }
            var top=(r.S3!=null?r.S3.position:body.Root.transform.position+Vector3.up*1.3f)+Vector3.up*.08f;var bottom=(r.S1!=null?r.S1.position:body.Root.transform.position+Vector3.up*1.05f)-Vector3.up*.2f;
            dogWraps[a.Id]=(Time.frameCount,top,bottom,right,fwd,strain,twist*Mathf.Deg2Rad);
        }

        // ---- the hand feeding (owner 2026-11-09: "Change the animation to hand feed them"; then "actually look like they are realistically
        // putting hot dogs in the players mouth") ----
        // Each arm of a feeding Top Dog reaches by IK, over the stance, for the beat it is on (NativeDogView.FeedHand), and its hand is turned to
        // what it holds: the right takes each sausage by its end between the thumb and fingers (its fingers pinched on it, NativeDogView.FeedGrip
        // through VisualHold), the sausage running on ahead along them; it brings it in from the side, eases it onto the lips and pushes it half
        // in along its slot with a forcing judder, lets go and swings back out for the next. The left cups the chin from below, its fingers along
        // under the jaw, holding the mouth open, tugging with each push. The wrist goes where it puts the hold point (the pinch, the palm) on its
        // target. Its rubber-hose arm stretches (up to half again) when the mouth is beyond its reach. The pinch and the fingers' way are kept for
        // the sausage in them (RenderDog draws after the bodies are posed).
        private sealed class DogArmRig{public readonly Transform[] Upper=new Transform[2],Lower=new Transform[2],Hand=new Transform[2],Index1=new Transform[2],Index2=new Transform[2],Middle1=new Transform[2],Pinky1=new Transform[2],Thumb3=new Transform[2];public readonly Vector3[] UpperBase=new Vector3[2],HandBase=new Vector3[2];public readonly bool[] Scaled=new bool[2];}
        private readonly Dictionary<VisualBody,DogArmRig> dogArms=new Dictionary<VisualBody,DogArmRig>();
        private readonly Dictionary<int,(int frame,Vector3 grip,Vector3 dir)> dogFeedHands=new Dictionary<int,(int,Vector3,Vector3)>();
        private const float DogStretch=1.5f;
        private Actor DogActor(int id){var s=dogSnapshot.Snapshot;if(s?.Actors!=null)foreach(var a in s.Actors)if(a!=null&&a.Id==id)return a;return null;}
        /// <summary>Sausage `i`'s way into the mouth (unit, into the head) and its place on the lips: the fan round the mouth.</summary>
        private static Vector3 DogSlotDir(Vector3 face,int i){var into=-face;var right=Vector3.Cross(Vector3.up,face);if(right.sqrMagnitude<1e-4f)right=Vector3.right;right.Normalize();return Quaternion.AngleAxis(NativeTopDog.FanYaw[i],Vector3.up)*Quaternion.AngleAxis(NativeTopDog.FanPitch[i],right)*into;}
        private static Vector3 DogLip(Vector3 mouth,Vector3 face,int i){var right=Vector3.Cross(Vector3.up,face);if(right.sqrMagnitude<1e-4f)right=Vector3.right;right.Normalize();return mouth+face*.005f+right*(NativeTopDog.FanYaw[i]/12f*.011f)-Vector3.up*(NativeTopDog.FanPitch[i]/9f*.006f);}
        /// <summary>A feeding hand's hold this frame: where its hold point goes (the right's pinch on the sausage's end, the left's palm under
        /// the chin), the way its fingers lie, the way its palm faces, the reach's weight and the sausage it is on.</summary>
        private struct DogFeedPose{public Vector3 At,Along,Palm;public float W;public int I;}
        /// <summary>The share of a sausage ahead of the fingers holding it (the rest of it behind them).</summary>
        private const float DogHold=.85f;
        /// <summary>Where hand `hand` of the feeding `caster` holds at `time` (see DogFeedPose).</summary>
        private bool DogFeedTarget(Actor caster,NativeDogFeast f,int hand,float time,out DogFeedPose p)
        {
            p=default;if(!NativeDogView.FeedHand(f,hand,time,out int i,out float w))return false;p.W=w;p.I=i;
            var victim=DogActor(f.Victim);if(victim==null||!DogMouth(victim,out var mouth,out var face))return false;
            var fwd=Vec(f.Dir);fwd.y=0;fwd=fwd.sqrMagnitude>1e-4f?fwd.normalized:Vector3.forward;var right=Vector3.Cross(Vector3.up,fwd).normalized;
            if(hand==1)
            {
                // The left cups the chin from below, its fingers along under the jaw, holding the mouth open: a tug down with each push.
                float tug=NativeDogView.Push(f,time);
                p.At=mouth-Vector3.up*(.085f+.015f*tug)+face*.015f;p.Along=(fwd*.8f+right*.5f-Vector3.up*.15f).normalized;p.Palm=(Vector3.up+face*.25f).normalized;return true;
            }
            // The right: each by its end, palm down and in; in from the side to the lips, pushed half in along its slot, let go, back out.
            float l=DogHeld;var into=DogSlotDir(face,i);var lip=DogLip(mouth,face,i);var front=lip-into*(l*DogHold);var reload=front-into*.1f-Vector3.up*.04f+right*.06f;
            int st=NativeDogView.FeedState(f,i,time,out float u);p.Palm=(-right*.55f-Vector3.up*.8f).normalized;
            if(st<=1)
            {
                // Backing off the last one's lips to the side, where the next pops into the hand: the empty hand drops and swings out on the way.
                float offer=NativeTopDog.OfferAt(f.FeedAt,i),back=offer-NativeTopDog.FeedPeriod+NativeTopDog.PushEnd,k=Mathf.Clamp01((time-back)/Mathf.Max(.02f,offer-NativeTopDog.CarryFrom-back));k=k*k*(3-2*k);
                var from=i>0?DogLip(mouth,face,i-1)-DogSlotDir(face,i-1)*(l*(DogHold-.5f)):reload;
                p.At=Vector3.Lerp(from,reload,k)+(right*.035f-Vector3.up*.02f)*Mathf.Sin(k*Mathf.PI);
                var aim=(lip-p.At).normalized;p.Along=st==1||i==0?aim:Vector3.Slerp(DogSlotDir(face,i-1),aim,k);
            }
            else if(st==2)
            {
                // In fast, easing onto the lips over a little rise, turning to its slot as it gets there.
                float e=1-(1-u)*(1-u);p.At=Vector3.Lerp(reload,front,e)+Vector3.up*(.025f*Mathf.Sin(e*Mathf.PI));
                p.Along=Vector3.Slerp((lip-p.At).normalized,into,u*u);
            }
            else
            {
                // Pushed in with a judder (it is being forced), then held there to the let-go.
                float push=st==3?u:1,judder=st==3?.006f*Mathf.Sin(u*Mathf.PI*4)*(1-u):0;
                p.At=lip-into*(l*(DogHold-.5f*push))+into*judder;p.Along=into;
            }
            return true;
        }
        private const float DogFed=1.35f;private static float DogHeld=>NativeTopDogModels.DogLength*DogFed;
        /// <summary>The world turn that lays hand `h`'s fingers along `along` with its palm toward `palm` (squared to them); false without the
        /// hand's knuckles.</summary>
        private static bool DogHandTurn(DogArmRig r,int h,Vector3 along,Vector3 palm,out Quaternion turn)
        {
            turn=Quaternion.identity;var hand=r.Hand[h];if(hand==null||r.Middle1[h]==null||r.Index1[h]==null||r.Pinky1[h]==null||along.sqrMagnitude<1e-8f)return false;
            var aL=hand.InverseTransformDirection(r.Middle1[h].position-hand.position);var xL=hand.InverseTransformDirection(r.Index1[h].position-r.Pinky1[h].position);
            if(aL.sqrMagnitude<1e-10f||xL.sqrMagnitude<1e-10f)return false;aL.Normalize();var pL=Vector3.Cross(xL,aL);if(pL.sqrMagnitude<1e-10f)return false;pL.Normalize();
            // The palm is the side the thumb is on.
            if(r.Thumb3[h]!=null&&Vector3.Dot(hand.InverseTransformDirection(r.Thumb3[h].position-hand.position),pL)<0)pL=-pL;
            along.Normalize();palm=Vector3.ProjectOnPlane(palm,along);if(palm.sqrMagnitude<1e-6f)palm=Vector3.ProjectOnPlane(Vector3.down,along);palm.Normalize();
            turn=Quaternion.LookRotation(along,palm)*Quaternion.Inverse(Quaternion.LookRotation(aL,pL));return true;
        }
        /// <summary>Hand `h`'s hold point now: the right's pinch (between the thumb's tip and the index finger's middle), the left's palm
        /// (toward `palm` off it).</summary>
        private static Vector3 DogHoldPoint(DogArmRig r,int h,Vector3 palm)
        {
            var hand=r.Hand[h];
            if(h==0&&r.Thumb3[0]!=null&&r.Index2[0]!=null)return Vector3.Lerp(r.Thumb3[0].position,r.Index2[0].position,.5f);
            return r.Middle1[h]!=null?Vector3.Lerp(hand.position,r.Middle1[h].position,.55f)+palm.normalized*.02f:hand.position;
        }
        /// <summary>TopDogBody: a feeding Top Dog's arms by IK and its hands turned to their holds; each arm's stretch put back first.</summary>
        private void DogFeedArms(VisualBody body,Actor a,float time)
        {
            var f=a.SourceSpec==NativeTopDog.Spec?NativeDogView.Of(a)?.Feast:null;
            if(!dogArms.TryGetValue(body,out var r))
            {
                if(f==null||f.Catch<-50)return;r=new DogArmRig();dogArms[body]=r;Transform[] bones=null;
                foreach(var s in body.Root.GetComponentsInChildren<SkinnedMeshRenderer>(true))if(s.name.StartsWith("Body.",StringComparison.Ordinal)&&s.sharedMesh!=null){bones=s.bones;break;}
                if(bones!=null)foreach(var t in bones)
                {
                    if(t==null)continue;int hs=t.name.EndsWith("_r",StringComparison.Ordinal)?0:t.name.EndsWith("_l",StringComparison.Ordinal)?1:-1;if(hs<0)continue;
                    switch(t.name.Substring(0,t.name.Length-2))
                    {
                        case "upperarm":r.Upper[hs]=t;break;case "lowerarm":r.Lower[hs]=t;break;case "hand":r.Hand[hs]=t;break;case "index_01":r.Index1[hs]=t;break;case "index_02":r.Index2[hs]=t;break;
                        case "middle_01":r.Middle1[hs]=t;break;case "pinky_01":r.Pinky1[hs]=t;break;case "thumb_03":r.Thumb3[hs]=t;break;
                    }
                }
                for(int h=0;h<2;h++){if(r.Upper[h]!=null)r.UpperBase[h]=r.Upper[h].localScale;if(r.Hand[h]!=null)r.HandBase[h]=r.Hand[h].localScale;}
            }
            var fwd=f!=null?Vec(f.Dir):Vector3.forward;fwd.y=0;fwd=fwd.sqrMagnitude>1e-4f?fwd.normalized:Vector3.forward;var side=Vector3.Cross(Vector3.up,fwd).normalized;bool fed=false;Vector3 grip=default,dir=default;
            for(int h=0;h<2;h++)
            {
                if(r.Upper[h]==null||r.Lower[h]==null||r.Hand[h]==null)continue;
                if(r.Scaled[h]){r.Upper[h].localScale=r.UpperBase[h];r.Hand[h].localScale=r.HandBase[h];r.Scaled[h]=false;}
                if(f==null||!DogFeedTarget(a,f,h,time,out var p))continue;
                var hand=r.Hand[h];var pole=Vector3.down*.7f+side*(h==0?.8f:-.8f)-fwd*.25f;
                // The hand turned to its hold, and where its hold point then sits from the wrist: the wrist goes where that puts it on target.
                bool turned=DogHandTurn(r,h,p.Along,p.Palm,out var turn);var off=Vector3.zero;
                if(turned){var keep=hand.rotation;hand.rotation=turn;off=DogHoldPoint(r,h,p.Palm)-hand.position;hand.rotation=keep;}
                r.Scaled[h]=DogReach(r.Upper[h],r.Lower[h],hand,p.At-off*p.W,pole,p.W,DogStretch,r.UpperBase[h],r.HandBase[h]);
                if(turned)hand.rotation=Quaternion.Slerp(hand.rotation,turn,p.W);
                if(h==0&&turned){grip=DogHoldPoint(r,0,p.Palm);dir=r.Middle1[0]!=null?(r.Middle1[0].position-hand.position).normalized:p.Along;dir=Vector3.Slerp(dir,p.Along.normalized,p.W);fed=true;}
            }
            if(fed)dogFeedHands[a.Id]=(Time.frameCount,grip,dir);else dogFeedHands.Remove(a.Id);
        }
        /// <summary>Two-bone IK of an arm toward `target`, bending toward `pole`, by `w`; the arm stretched (uniformly, the hand counter-scaled)
        /// up to `stretch` when it falls short. True when it stretched.</summary>
        private static bool DogReach(Transform upper,Transform lower,Transform hand,Vector3 target,Vector3 pole,float w,float stretch,Vector3 upperBase,Vector3 handBase)
        {
            if(w<=0)return false;var s=upper.position;float a=(lower.position-s).magnitude,b=(hand.position-lower.position).magnitude;if(a<1e-4f||b<1e-4f)return false;
            float k=Mathf.Lerp(1,Mathf.Clamp((target-s).magnitude/((a+b)*.97f),1,stretch),w);bool scaled=k>1.001f;
            if(scaled){upper.localScale=upperBase*k;hand.localScale=new Vector3(handBase.x/k,handBase.y/k,handBase.z/k);a*=k;b*=k;}
            var e=lower.position;var d=target-s;float dist=Mathf.Clamp(d.magnitude,1e-3f,(a+b)*.999f);var dir=d.sqrMagnitude>1e-8f?d.normalized:Vector3.forward;
            float cos=Mathf.Clamp((a*a+dist*dist-b*b)/(2*a*dist),-1,1),sin=Mathf.Sqrt(Mathf.Max(0,1-cos*cos));
            var bend=Vector3.ProjectOnPlane(pole,dir);if(bend.sqrMagnitude<1e-6f)bend=Vector3.ProjectOnPlane(Vector3.down,dir);bend.Normalize();
            var elbow=s+dir*(a*cos)+bend*(a*sin);
            upper.rotation=Quaternion.Slerp(Quaternion.identity,Quaternion.FromToRotation(e-s,elbow-s),w)*upper.rotation;
            e=lower.position;var hp=hand.position;lower.rotation=Quaternion.Slerp(Quaternion.identity,Quaternion.FromToRotation(hp-e,target-e),w)*lower.rotation;
            return scaled;
        }

        // ---- the effects and the camera ----
        /// <summary>RenderGlow, after the Bombadier's: the kit's particles.</summary>
        private void DogFx(float now)
        {
            if(kitScene==null||vfx==null||NativeDogView.Source==null)return;var draw=WeaponFxDraw;if(draw==null)return;techSink??=new TechSink();techSink.Bind(vfx,draw,FirstPerson&&!ScopeActive&&ViewCamera!=null);
            techniqueFx.DogGore=Gore;techniqueFx.DrawDogs(techSink,kitScene,kitClock,Mathf.Clamp01(WeaponEffectsQuality),LowGraphics);
        }
        /// <summary>All You Can Eat on the local pilot's own camera, the Top Dog's or its victim's (NativeCinematicCamera.Direct). False when it
        /// is not this pilot's.</summary>
        // Which side of the feast's line its shots stand on, chosen once a phase (the twirl, the throw, the feeding, the victim's): the
        // side with the clearer line to its look point, else the caster's high fallback when both are walled in. A wall in the way
        // would pull the camera into the Top Dog's own head, which hid it (owner 2026-11-09: "We also disappear for a second when
        // using ult ability").
        private float dogSideAt=-100,dogSide=1;private int dogSidePhase=-1;
        private float DogShotSide(NativeArena arena,NativeDogFeast f,Vector3 feet,Vector3 body,float time,bool victim,bool reduced,bool shake)
        {
            int phase=victim?3:f.Catch<-50?(time<f.Throw?0:1):2;if(f.At==dogSideAt&&phase==dogSidePhase)return dogSide;
            float Clear(float side)
            {
                var s=NativeDogCamera.At(f,V(feet),V(body),time,victim,reduced,shake,side);var p=new Vector3(s.Position.X,s.Position.Y,s.Position.Z);var l=new Vector3(s.Look.X,s.Look.Y,s.Look.Z);
                var ray=p-l;float d=ray.magnitude;if(d<.05f||arena.Root==null)return 1;ray/=d;
                return arena.Root.scene.GetPhysicsScene().Raycast(l+ray*.12f,ray,out var hit,Mathf.Max(0,d-.12f),NativeArena.WorldMask,QueryTriggerInteraction.Ignore)?hit.distance/d:1;
            }
            float right=Clear(1),left=Clear(-1);dogSide=left>right+.05f?-1:1;
            if(!victim&&Mathf.Max(right,left)<.75f&&Clear(0)>Mathf.Max(right,left))dogSide=0;
            dogSideAt=f.At;dogSidePhase=phase;return dogSide;
        }
        internal bool DogFeastDirect(NativeArena arena,Camera view,float time,bool shake,bool reduced)
        {
            if(view==null||arena?.Local==null||NativeDogView.Source==null)return false;NativeDogFeast f=null;int caster=0;bool victim=false;
            var mine=NativeDogView.Source.Of(arena.LocalId);if(mine?.Feast!=null&&NativeDogCamera.Active(mine.Feast,time,false)){f=mine.Feast;caster=arena.LocalId;}
            else if(dogVictims.TryGetValue(arena.LocalId,out var v)&&NativeDogCamera.Active(v.feast,time,true)){f=v.feast;caster=v.caster;victim=true;}
            if(f==null)return false;
            var feet=visualPositions.TryGetValue(caster,out var cf)?cf:view.transform.position;var body=f.Victim!=0&&visualPositions.TryGetValue(f.Victim,out var vf)?vf+Vector3.up*1.05f:feet+Vec(f.Dir)*NativeTopDog.HoldAhead+Vector3.up*1.05f;
            var shot=NativeDogCamera.At(f,V(feet),V(body),time,victim,reduced,shake,DogShotSide(arena,f,feet,body,time,victim,reduced,shake));if(shot.Blend<=0)return false;
            Vector3 position=new Vector3(shot.Position.X,shot.Position.Y,shot.Position.Z),look=new Vector3(shot.Look.X,shot.Look.Y,shot.Look.Z),ray=position-look;float distance=ray.magnitude;
            if(distance>.05f&&arena.Root!=null){ray/=distance;if(arena.Root.scene.GetPhysicsScene().Raycast(look+ray*.12f,ray,out var hit,Mathf.Max(0,distance-.12f),NativeArena.WorldMask,QueryTriggerInteraction.Ignore))position=look+ray*Mathf.Max(.3f,hit.distance);}
            NativeSirenCutscene.Shake(time,shot.Shake,out float dx,out float dy,out float roll);var forward=look-position;if(forward.sqrMagnitude<1e-8f)forward=view.transform.forward;var rotation=Quaternion.LookRotation(forward,Vector3.up)*Quaternion.AngleAxis(-roll*Mathf.Rad2Deg,Vector3.forward);
            float b=shot.Blend;view.transform.SetPositionAndRotation(Vector3.Lerp(view.transform.position,position+new Vector3(-dx,dy,0),b),Quaternion.Slerp(view.transform.rotation,rotation,b));view.fieldOfView=Mathf.Lerp(view.fieldOfView,shot.Fov,b);FirstPerson=false;AimHeld=false;return true;
        }
        /// <summary>The held mallet's scale: in or out of hammerspace, and Bigger Mallet's copies (Core NativeDogChips).</summary>
        internal static float TopDogMallet(Actor a,float time){if(a?.SourceSpec!=NativeTopDog.Spec)return 1;var k=NativeDogView.Of(a);return NativeDogView.MalletShown(k,time)*(1+NativeDogChips.MalletGrow*(k?.Mallet??0));}
    }
}
