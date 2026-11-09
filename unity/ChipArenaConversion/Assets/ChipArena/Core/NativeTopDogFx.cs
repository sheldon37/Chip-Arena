using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>The kits as the rendered snapshot carries them (Actor.Dog).</summary>
    public sealed class NativeTopDogSnapshot:ITopDogSource
    {
        public Snapshot Snapshot;
        public NativeTopDogKit Of(int id){if(Snapshot?.Actors!=null)foreach(var a in Snapshot.Actors)if(a!=null&&a.Id==id)return a.Dog;return null;}
    }
    /// <summary>
    /// Where the Top Dog's things are at a moment, from the record alone (docs/TOP-DOG.md "Looks"): the same answer for the effects, the models,
    /// the sound, the camera and the checks. Pure.
    /// </summary>
    public static class NativeDogView
    {
        /// <summary>Who holds the records (null: nothing to draw): the snapshot's, or the sandbox preview's.</summary>
        public static ITopDogSource Source;
        public static NativeTopDogKit Of(Actor a)=>a==null||Source==null?null:Source.Of(a.Id);
        private static float Clamp01(float x)=>x<0?0:x>1?1:x;private static float Smooth(float x){x=Clamp01(x);return x*x*(3-2*x);}
        private static Vector3 N(V3 v)=>new Vector3(v.x,v.y,v.z);
        /// <summary>A thrown thing's place at `time`: its sample carried on under its gravity (a peel 23, a pie 12), or where it stopped.</summary>
        public static Vector3 PropAt(NativeDogProp p,float time)
        {
            if((p.Kind==NativeTopDog.PropPeel||p.Kind==NativeTopDog.PropSnack)&&time>=p.LandAt)return N(p.To);
            if(p.Kind==NativeTopDog.PropPie&&p.LandAt>-50)return N(p.Pos);
            float g=p.Kind==NativeTopDog.PropPeel||p.Kind==NativeTopDog.PropSnack?23:p.Kind==NativeTopDog.PropPie?NativeTopDog.PieGravity:0,t=Math.Max(0,time-p.SampleAt);
            if(p.Kind==NativeTopDog.PropPeel||p.Kind==NativeTopDog.PropSnack)t=Math.Min(t,Math.Max(0,p.LandAt-p.SampleAt));
            return N(p.Pos)+N(p.Vel)*t-Vector3.UnitY*(.5f*g*t*t);
        }
        /// <summary>The anvil's height over its spot (m) and its squash (1 none) at `time`: dropped from AnvilHeight, gathering speed to the
        /// impact, then a toon squash and a little bounce before it settles.</summary>
        public static float AnvilDrop(NativeDogProp p,float time,out float squash)
        {
            squash=1;float fall=Math.Max(.05f,p.LandAt-p.At);if(time<p.LandAt){float u=Clamp01((time-p.At)/fall);return NativeTopDog.AnvilHeight*(1-u*u);}
            float t=time-p.LandAt;
            if(t<.09f){squash=1-.3f*(float)Math.Sin(Math.PI*t/.09f);return 0;}
            if(t<.36f){float u=(t-.09f)/.27f;squash=1+.08f*(float)Math.Sin(Math.PI*u);return .32f*4*u*(1-u);}
            if(t<.46f){squash=1-.12f*(float)Math.Sin(Math.PI*(t-.36f)/.1f);}
            return 0;
        }
        /// <summary>How far out the glove is (m) at `time`: shot out to its reach with an overshoot, held a beat, wound back.</summary>
        public static float GloveExtent(NativeDogProp p,float time)
        {
            if(time<p.At)return 0;float reach=p.Size;
            if(time<p.LandAt){float u=Clamp01((time-p.At)/Math.Max(.02f,p.LandAt-p.At));return reach*(1-(1-u)*(1-u)*(1-u));}
            float back=Math.Max(.05f,p.Until-p.LandAt),t=(time-p.LandAt)/back;if(t<.2f)return reach*(1+.06f*(float)Math.Sin(Math.PI*t/.2f));
            return reach*(1-Smooth((t-.2f)/.8f));
        }
        /// <summary>The spring under the feet at `time`: its height (m) as it pops, wobbles and folds away.</summary>
        public static float SpringHeight(NativeDogProp p,float time)
        {
            float t=time-p.At;if(t<0)return .1f;if(t<.08f)return .1f+.6f*Smooth(t/.08f);
            float wob=(float)(Math.Exp(-6*(t-.08f))*Math.Cos((t-.08f)*30));return Math.Max(.08f,.55f+.18f*wob-.45f*Smooth((t-.55f)/.3f));
        }
        /// <summary>One hot dog of the feast at `time`: 0 not yet (behind the Top Dog's back), 1 in the hand going to the mouth (`move` 0..1),
        /// 2 going in (`into` 0..1: how much has gone), 3 down. A snapped feast stops the rest.</summary>
        public static int FeedPhase(NativeDogFeast f,int i,float time,out float move,out float into)
        {
            move=into=0;if(f==null||f.Catch<-50)return 0;float offer=NativeTopDog.OfferAt(f.FeedAt,i),down=NativeTopDog.DownAt(f.FeedAt,i);
            if(f.Snap>-50&&f.Snap<down)return 0;if(time<offer-.2f)return 0;
            if(time<offer){move=0;return 1;}
            const float Reach=.18f;if(time<offer+Reach){move=Smooth((time-offer)/Reach);return 1;}
            if(time<down){into=Smooth((time-offer-Reach)/Math.Max(.05f,down-offer-Reach));return 2;}
            return 3;
        }
        /// <summary>The links' look at `time`: 0 none, 1 twirled overhead, 2 flying (the loop at f.Lasso), 3 round the caught victim, 4 lying
        /// where they fell (a miss or a snap).</summary>
        public static int LassoPhase(NativeDogFeast f,float time)
        {
            if(f==null||time<f.At)return 0;if(time<f.Throw)return 1;
            if(f.Catch<-50)return f.Miss?(time<f.Until?4:0):2;
            if(f.Snap>-50&&time>=f.Snap)return time<f.Snap+.8f?4:0;
            return time<f.Release?3:0;
        }
        /// <summary>The Top Dog's mallet hidden away (hammerspace) while both hands are busy: the feast, the bottle, the glove, the pie, the peel.</summary>
        public static float MalletShown(NativeTopDogKit k,float time)
        {
            if(k==null)return 1;float shown=1;
            if(k.Feast!=null&&time>=k.Feast.At-.05f){float end=k.Feast.Catch>-50?(k.Feast.Snap>-50?k.Feast.Snap:k.Feast.Release):k.Feast.Until;shown=Math.Min(shown,1-Clamp01((time-k.Feast.At)/.12f)+Clamp01((time-end-.3f)/.2f));}
            if(k.CastKind!=null&&k.CastKind!=NativeTopDog.Mallet&&k.CastKind!=NativeTopDog.Boing&&k.CastKind!=NativeTopDog.Zip&&k.CastKind!=NativeTopDog.Feast&&time>=k.CastAt-.02f&&time<k.CastUntil+.3f)
                shown=Math.Min(shown,Math.Max(Clamp01(1-(time-k.CastAt)/.08f),Clamp01((time-k.CastUntil)/.2f)));
            return Clamp01(shown);
        }
        /// <summary>The mark of kind `kind` on `id` that is live at `time`, from any Top Dog's kit, or null.</summary>
        public static NativeDogMark Mark(NativeTopDogKit k,int id,string kind,float time)
        {
            if(k?.Marks==null)return null;foreach(var m in k.Marks)if(m!=null&&m.Id==id&&m.Kind==kind&&time>=m.At&&time<m.Until)return m;return null;
        }
    }

    /// <summary>All You Can Eat's private camera for the Top Dog and its victim (docs/TOP-DOG.md): the twirl over the shoulder, the throw, then
    /// a low two-shot of the feeding, a push in on each gulp, out wide for the let-go, and the victim's own swelling seen from in front. Everyone
    /// else keeps their camera. Blends in and out; `reduced` takes the gentle path.</summary>
    public static class NativeDogCamera
    {
        public const float BlendIn=.16f,BlendOut=.5f;
        private static float Clamp01(float x)=>x<0?0:x>1?1:x;private static float Smooth(float x){x=Clamp01(x);return x*x*(3-2*x);}
        public static bool Active(NativeDogFeast f,float time,bool victim)
        {
            if(f==null||f.At<-50||time<f.At)return false;if(f.Catch<-50)return !victim&&time<f.Throw+.45f;
            float end=f.Snap>-50?f.Snap+.3f:f.Pop+(victim?.15f:.7f);return time<end;
        }
        /// <summary>The shot at `time` for the caster at `feet` and the victim at `body` (their body's middle).</summary>
        public static KernelPanicCamera.Shot At(NativeDogFeast f,Vector3 feet,Vector3 body,float time,bool victim,bool reduced,bool shake)
        {
            var s=new KernelPanicCamera.Shot();var d=new Vector3(f.Dir.x,0,f.Dir.z);d=d.LengthSquared()>1e-5f?Vector3.Normalize(d):Vector3.UnitZ;var r=new Vector3(d.Z,0,-d.X);var up=Vector3.UnitY;
            float t=time-f.At,end;
            if(f.Catch<-50)
            {
                // The twirl from low and to the side, the hat of links whirling over it, then a whip along the throw.
                float u=Smooth(t/Math.Max(.1f,f.Throw-f.At));s.Position=feet+d*(2.2f-.6f*u)+r*(1.6f-.3f*u)+up*(.9f+.2f*u);s.Look=feet+up*(1.9f-.5f*u)+d*(.4f*u);s.Fov=50-6*u;
                if(time>=f.Throw){float w=Smooth((time-f.Throw)/.4f);s.Position=feet-d*(1.8f+.6f*w)+r*(.9f)+up*1.7f;s.Look=feet+d*(6+6*w)+up*1.1f;s.Fov=58;}
                end=f.Throw+.45f-f.At;
            }
            else if(!victim)
            {
                // Side on to both: the Top Dog feeding, the victim fed; a little push in on each gulp.
                var mid=(feet+up*1.25f+body)*.5f;float tc=time-f.Catch;
                if(time<f.FeedAt){float u=Smooth(tc/Math.Max(.1f,f.FeedAt-f.Catch));s.Position=mid+r*(4.1f-.6f*u)+up*(.45f-.1f*u)-d*.15f;s.Look=mid+up*.2f;s.Fov=50-4*u;}
                else if(time<f.Release)
                {
                    float since=(time-f.FeedAt)%NativeTopDog.FeedPeriod,gulp=(float)Math.Sin(Math.PI*Clamp01((since-NativeTopDog.Swallow*.5f)/.25f));
                    s.Position=mid+r*(3.4f-.25f*gulp)+up*(.32f)-d*.1f;s.Look=mid+up*(.28f)+d*.2f;s.Fov=45-3*gulp;s.Shake=shake?.004f*gulp:0;
                }
                else
                {
                    // Let go: wide on the victim staggering off and swelling, the Top Dog in the near corner, pushing in to the pop.
                    float u=Smooth((time-f.Release)/Math.Max(.1f,f.Pop-f.Release));var both=(feet+up*1.1f+body)*.5f+d*.5f;
                    s.Position=both+r*(4.6f-1.3f*u)+up*(.75f-.25f*u)-d*(.4f-.3f*u);s.Look=body+up*(.15f+.1f*u)-d*(.3f*(1-u));s.Fov=48-8*u;s.Shake=shake?.006f+.02f*u*u:0;
                }
                end=(f.Snap>-50?f.Snap+.3f:f.Pop+.7f)-f.At;
            }
            else
            {
                // The victim: their feeding from beside the Top Dog's shoulder, then their own swelling, face on.
                float tc=time-f.Catch;var face=-d;var side=r;
                if(time<f.Release){float u=Smooth(tc/Math.Max(.1f,f.FeedAt-f.Catch));s.Position=body+face*(1.9f-.3f*u)+side*(-1.1f)+up*(.55f+.1f*u);s.Look=body+up*.45f;s.Fov=46-4*u;}
                else{float u=Smooth((time-f.Release)/Math.Max(.1f,f.Pop-f.Release));s.Position=body+face*(2.6f-.9f*u)+side*(.6f-.4f*u)+up*(.25f+.15f*u);s.Look=body+up*(.15f+.1f*u);s.Fov=46-8*u;s.Shake=shake?.01f+.03f*u*u:0;}
                end=(f.Snap>-50?f.Snap+.3f:f.Pop+.15f)-f.At;
            }
            if(reduced){s.Fov=Math.Max(s.Fov,48);s.Shake=0;}
            s.Blend=Smooth(t/BlendIn)*(1-Smooth((t-(end-BlendOut))/BlendOut));return s;
        }
    }

    // ---- the effects -------------------------------------------------------------------------------------------------------------------
    public sealed partial class NativeTechniqueFx
    {
        private static readonly Vector3 DogMustard=new Vector3(1,.58f,.03f),DogKetchup=new Vector3(.8f,.04f,.02f),DogCream=new Vector3(1,.98f,.9f),DogStar=new Vector3(1,.82f,.2f),
            DogDust=new Vector3(.46f,.38f,.28f),DogSauce=new Vector3(1,.16f,.05f),DogFlame=new Vector3(1,.5f,.08f),DogWhite=new Vector3(1,1,1),DogSweat=new Vector3(.55f,.8f,1),DogPink=new Vector3(1,.35f,.6f),DogBlue=new Vector3(.3f,.6f,1),DogGreen=new Vector3(.4f,1,.35f);
        private static Actor DogActor(IKitFxScene scene,int id){if(id==0)return null;var l=scene.Actors;for(int i=0;i<l.Count;i++)if(l[i]!=null&&l[i].Id==id)return l[i];return null;}
        /// <summary>Draws every Top Dog's hits, props, marks and feast of the frame at `time`.</summary>
        public void DrawDogs(ITechniqueFxSink s,IKitFxScene scene,float time,float q,bool low)
        {
            if(s==null||scene?.Actors==null||NativeDogView.Source==null)return;var actors=scene.Actors;
            for(int i=0;i<actors.Count;i++)
            {
                var a=actors[i];if(a==null)continue;var k=NativeDogView.Of(a);if(k==null)continue;long seed=a.Id*7307L;
                if(k.Hits!=null)foreach(var h in k.Hits)if(h!=null)DogHitFx(s,scene,h,time,seed+h.Id*53L,q,low);
                if(k.Props!=null)foreach(var p in k.Props)if(p!=null)DogPropFx(s,scene,a,p,time,seed+p.Id*41L,q,low);
                if(k.Marks!=null)foreach(var m in k.Marks)if(m!=null)DogMarkFx(s,scene,m,time,seed+m.Id*29L+m.Kind.Length,q,low);
                if(k.Feast!=null)DogFeastFx(s,scene,a,k.Feast,time,seed+17,q,low);
            }
        }
        // A toon impact: a white starburst of short thick rays, a ring, little stars thrown off.
        private static void DogBurst(ITechniqueFxSink s,Vector3 at,float age,float size,Vector3 tint,long seed,float q,bool low,int rays=9)
        {
            if(age<0||age>.55f)return;float u=age/.55f,pop=OutCubic(age/.08f),fade=1-Smooth((age-.12f)/.4f);
            s.Glow(at,(DogWhite*1.6f)*fade,0,size*(.5f+.6f*pop),Core,u,seed);s.Glow(at,tint*(1.2f*fade),0,size*(.9f+1.1f*pop),RingGlow,u,seed+1);
            int n=Count(rays,q,low);for(int i=0;i<n;i++){var d=Vector3.Normalize(RandomDir(seed,i*3)+Up*.15f);float len=size*(.55f+.6f*R(seed,i+5))*pop;s.Line(false,at+d*(size*.25f),at+d*(size*.25f+len),Vector3.Lerp(DogWhite,tint,.35f)*(1.8f*fade),.05f*size,false);}
            int stars=Count(5,q,low);for(int i=0;i<stars;i++){var d=Vector3.Normalize(RandomDir(seed,i*7+3)+Up*.6f);float t=age;var p=at+d*(size*1.6f*OutCubic(age/.45f))-Up*(1.2f*t*t);s.Sprite(false,p,DogStar*(1.8f*fade),.09f*size,Sparkle);}
        }
        private static void DogPuff(ITechniqueFxSink s,Vector3 at,float age,float life,int n,float radius,Vector3 albedo,float opacity,long seed,float rise=.4f)
        {
            if(age<0||age>life)return;float u=age/life;
            for(int i=0;i<n;i++){var d=RandomDir(seed,i*5);d.Y=Math.Abs(d.Y)*.6f;var p=at+d*(radius*OutCubic(u)*(.5f+.5f*R(seed,i+3)))+Up*(rise*u);s.Smoke(p,albedo,opacity*(1-u)*(1-u),(.35f+.7f*u)*(.7f+.6f*R(seed,i+9)),.1f+.6f*u,seed+i,at.Y-.2f);}
        }
        // Speed lines: white streaks along `dir` behind `at`.
        private static void DogSpeed(ITechniqueFxSink s,Vector3 at,Vector3 dir,float age,float life,int n,float spread,long seed)
        {
            if(age<0||age>life)return;float u=age/life;var side=Vector3.Normalize(Vector3.Cross(dir,Up).LengthSquared()>1e-5f?Vector3.Cross(dir,Up):Vector3.UnitX);var up2=Vector3.Cross(side,dir);
            for(int i=0;i<n;i++){var o=side*((R(seed,i)-.5f)*2*spread)+up2*((R(seed,i+30)-.2f)*spread*1.4f);float len=.6f+.9f*R(seed,i+60);var head=at+o-dir*(.2f+1.5f*u*R(seed,i+90));s.Line(false,head-dir*len,head,DogWhite*(1.3f*(1-u)),.025f,false);}
        }
        private static void DogHitFx(ITechniqueFxSink s,IKitFxScene scene,NativeDogHit h,float time,long seed,float q,bool low)
        {
            float age=time-h.At;if(age<0||age>2.2f)return;var at=N(h.Pos);var dir=N(h.Dir);if(dir.LengthSquared()<1e-6f)dir=Up;dir=Vector3.Normalize(dir);
            switch(h.Kind)
            {
                case NativeTopDog.HitBonk:DogBurst(s,at,age,.55f,DogStar,seed,q,low);break;
                case NativeTopDog.HitSlam:DogBurst(s,at,age,.75f,DogStar,seed,q,low,12);break;
                case NativeTopDog.HitClang:
                    if(age<.6f){Ring(s,at+Up*.03f,.4f+NativeTopDog.SlamRadius*OutCubic(age/.35f),.35f*(1-age/.6f),DogWhite*(1.2f*(1-age/.6f)),Count(28,q,low));}
                    Cracks(s,at,age,1.1f,Count(9,q,low),NativeTopDog.SlamRadius*.8f,new Vector3(.25f,.2f,.15f),seed);Dust(s,at,age,1.2f,Count(12,q,low),NativeTopDog.SlamRadius,DogDust,.7f,seed+5);DogBurst(s,at+Up*.2f,age,.6f,DogStar,seed+9,q,low,7);break;
                case NativeTopDog.HitThud:
                    if(age<.5f)Ring(s,at+Up*.03f,.3f+NativeTopDog.ThudRadius*OutCubic(age/.3f),.3f*(1-age/.5f),DogWhite*(1-age/.5f),Count(24,q,low));Dust(s,at,age,1,Count(10,q,low),NativeTopDog.ThudRadius*.9f,DogDust,.65f,seed);break;
                case NativeTopDog.HitBoing:DogPuff(s,at+Up*.1f,age,.7f,Count(6,q,low),.6f,DogDust,.55f,seed);DogBurst(s,at+Up*.2f,age,.35f,DogWhite,seed+3,q,low,6);break;
                case NativeTopDog.HitSwat:DogBurst(s,at,age,.5f,DogWhite,seed,q,low);DogSpeed(s,at,dir,age,.35f,Count(5,q,low),.4f,seed+2);break;
                case NativeTopDog.Zip:DogPuff(s,at+Up*.05f,age,.9f,Count(10,q,low),.8f,DogDust,.7f,seed);break;
                case NativeTopDog.HitAnvil:
                    Flash(s,at+Up*.3f,age,.25f,DogWhite,1.4f,seed);if(age<.55f)Ring(s,at+Up*.03f,.4f+NativeTopDog.AnvilRadius*1.2f*OutCubic(age/.3f),.45f*(1-age/.55f),DogWhite*(1.3f*(1-age/.55f)),Count(32,q,low));
                    Dust(s,at,age,1.6f,Count(16,q,low),NativeTopDog.AnvilRadius*1.3f,DogDust,.8f,seed+3);Cracks(s,at,age,1.4f,Count(10,q,low),NativeTopDog.AnvilRadius,new Vector3(.2f,.16f,.12f),seed+5);
                    Sparks(s,at+Up*.4f,age,.6f,Count(12,q,low),5,new Vector3(1,.75f,.35f),seed+7,9,.025f,Up*.8f);break;
                case NativeTopDog.HitSlip:
                {
                    DogBurst(s,at+Up*.6f,age,.4f,DogStar,seed,q,low,6);
                    // The peel's flip: a curl of motion lines rising where they went over.
                    if(age<.6f){float u=age/.6f;for(int i=0;i<Count(8,q,low);i++){float a0=i*.8f+u*5,a1=a0+.5f,r=.35f+.15f*i/8f;var c=at+Up*(.4f+1.2f*u);s.Line(false,c+new Vector3((float)Math.Cos(a0)*r,0,(float)Math.Sin(a0)*r),c+new Vector3((float)Math.Cos(a1)*r,.05f,(float)Math.Sin(a1)*r),DogWhite*(1.1f*(1-u)),.02f,false);}}
                    break;
                }
                case NativeTopDog.HitPow:DogBurst(s,at,age,.8f,DogStar,seed,q,low,12);DogSpeed(s,at,dir,age,.3f,Count(6,q,low),.5f,seed+3);break;
                case NativeTopDog.HitPie:
                {
                    DogBurst(s,at,age,.45f,DogCream,seed,q,low,7);
                    for(int i=0;i<Count(14,q,low);i++){var d=Vector3.Normalize(RandomDir(seed,i*3)+dir*.8f+Up*.5f);float t=age;var p=at+d*(2.2f*t*(.5f+.5f*R(seed,i+4)))-Up*(4.5f*t*t);if(age<.9f)s.Drop(p,d*.12f-Up*(.08f*t),DogCream*1.1f,.9f*(1-age/.9f),.05f+.04f*R(seed,i+8),age/.9f,seed+i);}
                    break;
                }
                case NativeTopDog.HitGulp:DogPuff(s,at,age,.4f,Count(4,q,low),.18f,new Vector3(.9f,.85f,.75f),.4f,seed,.1f);if(age<.25f)for(int i=0;i<Count(4,q,low);i++)s.Sprite(false,at+RandomDir(seed,i)*(.15f+.4f*age),DogStar*(1.5f*(1-age/.25f)),.05f,Sparkle);break;
                case NativeTopDog.HitSnap:Sparks(s,at,age,.5f,Count(10,q,low),3.5f,new Vector3(.9f,.35f,.2f),seed,7,.03f);DogBurst(s,at,age,.4f,DogWhite,seed+1,q,low,6);break;
                case NativeTopDog.HitPop:DogPop(s,at,age,seed,q,low);break;
                case NativeTopDog.HitPiano:
                    Flash(s,at+Up*.4f,age,.3f,DogWhite,1.6f,seed);if(age<.6f)Ring(s,at+Up*.03f,.5f+NativeDogChips.PianoRadius*1.25f*OutCubic(age/.32f),.5f*(1-age/.6f),DogWhite*(1.3f*(1-age/.6f)),Count(34,q,low));
                    Dust(s,at,age,1.8f,Count(18,q,low),NativeDogChips.PianoRadius*1.4f,DogDust,.85f,seed+3);Cracks(s,at,age,1.5f,Count(10,q,low),NativeDogChips.PianoRadius,new Vector3(.2f,.16f,.12f),seed+5);
                    DogBurst(s,at+Up*.6f,age,.7f,DogCream,seed+9,q,low,12);
                    // Keys and splinters thrown out.
                    if(age<1.1f)for(int i=0;i<Count(14,q,low);i++){var d=Vector3.Normalize(RandomDir(seed,i*3+1)+Up*.9f);float t=age;var p2=at+Up*.5f+d*(4.5f*t*(.5f+.5f*R(seed,i+11)))-Up*(8*t*t);s.Drop(p2,d*.1f,i%3==0?new Vector3(.05f,.05f,.06f):DogCream,.95f*(1-age/1.1f),.05f,age/1.1f,seed+20+i);}
                    break;
                case NativeTopDog.HitWall:DogBurst(s,at,age,.8f,DogStar,seed,q,low,12);DogPuff(s,at,age,.7f,Count(6,q,low),.5f,DogDust,.6f,seed+4,.2f);break;
                case NativeTopDog.HitMunch:DogPuff(s,at,age,.5f,Count(5,q,low),.25f,new Vector3(.9f,.7f,.4f),.5f,seed,.15f);DogBurst(s,at,age,.3f,DogStar,seed+2,q,low,6);break;
                case NativeTopDog.HitToonFlat:
                    if(age<.5f)Ring(s,at+Up*.03f,.3f+1.6f*OutCubic(age/.3f),.3f*(1-age/.5f),DogWhite*(1-age/.5f),Count(22,q,low));Dust(s,at,age,1,Count(10,q,low),1.4f,DogDust,.6f,seed);break;
                case NativeTopDog.HitToonPop:
                    DogBurst(s,at,age,.8f,DogStar,seed,q,low,14);if(age<.6f)Ring(s,at+Up*.05f,.4f+NativeDogChips.ToonRadius*OutCubic(age/.35f),.35f*(1-age/.6f),DogStar*(1.4f*(1-age/.6f)),Count(28,q,low));
                    if(age<1.2f)for(int i=0;i<Count(8,q,low);i++){float a=age*6+i*.785f;var p2=at+Up*(.6f+.8f*age)+new Vector3((float)Math.Cos(a)*(.3f+.6f*age),0,(float)Math.Sin(a)*(.3f+.6f*age));s.Sprite(false,p2,DogStar*(1.8f*(1-age/1.2f)),.12f,Sparkle);}
                    break;
                case NativeTopDog.Feast:DogSpeed(s,at,dir,age,.35f,Count(6,q,low),.35f,seed);break;
            }
        }
        // The burst: a flash, a ring, a shower of confetti and condiment, and a puff.
        private static void DogPop(ITechniqueFxSink s,Vector3 at,float age,long seed,float q,bool low)
        {
            Flash(s,at+Up*.4f,age,.35f,new Vector3(1,.85f,.55f),2.4f,seed);DogBurst(s,at+Up*.4f,age,1.4f,DogStar,seed+1,q,low,16);
            if(age<.7f)Ring(s,at+Up*.05f,.5f+NativeTopDog.PopRadius*1.2f*OutCubic(age/.35f),.5f*(1-age/.7f),DogWhite*(1.4f*(1-age/.7f)),Count(36,q,low));
            Vector3[] confetti={DogStar,DogPink,DogBlue,DogGreen,DogWhite,DogMustard};
            int n=Count(48,q,low);for(int i=0;i<n;i++)
            {
                float life=1.6f+.8f*R(seed,i+300);if(age>life)continue;var d=Vector3.Normalize(RandomDir(seed,i*3+5)+Up*.9f);float sp=4+6*R(seed,i+200),t=age,drag=1-(float)Math.Exp(-2.2f*t);
                var p=at+Up*.6f+d*(sp/2.2f*drag)-Up*(1.1f*t*t);float u=age/life;var c=confetti[i%confetti.Length];
                // Paper turning: it flickers as it tumbles.
                float flick=.55f+.45f*(float)Math.Abs(Math.Sin(t*14+i));s.Sprite(false,p,c*(1.6f*flick*(1-u*u)),.05f+.03f*R(seed,i+11),Mote);
            }
            int m=Count(22,q,low);for(int i=0;i<m;i++)
            {
                if(age>1.1f)break;var d=Vector3.Normalize(RandomDir(seed,i*7+1)+Up*.4f);float t=age;var p=at+Up*.6f+d*(5.5f*t*(.5f+.5f*R(seed,i+77)))-Up*(7*t*t);
                s.Drop(p,d*.14f-Up*(.12f*t),(i%3==0?DogKetchup:DogMustard)*1.1f,.95f*(1-age/1.1f),.06f+.05f*R(seed,i+3),age/1.1f,seed+i);
            }
            DogPuff(s,at+Up*.5f,age,1.5f,Count(14,q,low),1.3f,new Vector3(.85f,.8f,.72f),.6f,seed+9,.8f);
        }
        private static void DogPropFx(ITechniqueFxSink s,IKitFxScene scene,Actor owner,NativeDogProp p,float time,long seed,float q,bool low)
        {
            float age=time-p.At;if(age<0)return;var at=N(p.Pos);
            switch(p.Kind)
            {
                case NativeTopDog.PropAnvil:
                    if(time<p.LandAt)
                    {
                        // Its shadow growing on the floor as it comes, and a warning ring that tightens.
                        float u=Clamp01(age/Math.Max(.05f,p.LandAt-p.At));int seg=Count(26,q,low);
                        Ring(s,at+Up*.03f,NativeTopDog.AnvilRadius*(1.3f-.3f*u),.08f+.05f*u,new Vector3(1,.12f,.05f)*(.7f+.6f*(float)Math.Abs(Math.Sin(time*14))),seg);
                        for(int i=0;i<Count(10,q,low);i++){float a0=6.2831853f*i/10f;s.Line(false,at+Up*.03f,at+Up*.03f+new Vector3((float)Math.Cos(a0),0,(float)Math.Sin(a0))*(NativeTopDog.AnvilRadius*.6f*u),new Vector3(.6f,.08f,.04f)*.6f,.04f,false);}
                        // Falling lines above it.
                        float h=NativeDogView.AnvilDrop(p,time,out _);if(h>.5f)for(int i=0;i<Count(4,q,low);i++){var o=new Vector3((R(seed,i)-.5f)*.7f,0,(R(seed,i+5)-.5f)*.7f);s.Line(false,at+o+Up*(h+.7f),at+o+Up*(h+1.6f+.5f*R(seed,i+9)),DogWhite*.9f,.02f,false);}
                    }
                    else if(time>p.Until-.4f)DogPuff(s,at+Up*.3f,time-(p.Until-.4f),.6f,Count(8,q,low),.5f,new Vector3(.9f,.9f,.9f),.6f,seed);
                    break;
                case NativeTopDog.PropPeel:
                    if(time>=p.LandAt&&((int)((time-p.LandAt)*1.3f)%3==0)){float u=((time-p.LandAt)*1.3f)%1;s.Sprite(false,N(p.To)+Up*.06f,DogWhite*(1.4f*(float)Math.Sin(Math.PI*u)),.06f,Sparkle);}
                    break;
                case NativeTopDog.PropPie:
                    if(p.LandAt>-50){float t=time-p.LandAt;if(t<3){var n=N(p.Dir);if(n.LengthSquared()<.5f)n=Up;for(int i=0;i<Count(6,q,low);i++){var o=Vector3.Normalize(RandomDir(seed,i)-n*Vector3.Dot(RandomDir(seed,i),n))*(.12f+.18f*R(seed,i+3));s.Smoke(at+n*.03f+o,DogCream,.8f*(1-Smooth((t-2.2f)/.8f)),.18f+.1f*R(seed,i+7),.2f,seed+i,at.Y-.5f);}}}
                    else{var here=NativeDogView.PropAt(p,time);s.Glow(here,DogCream*.25f,0,.4f,Core,0,seed);}
                    break;
                case NativeTopDog.PropGlove:
                {
                    float ext=NativeDogView.GloveExtent(p,time);var dir=N(p.Dir);if(dir.LengthSquared()<.5f)break;dir=Vector3.Normalize(dir);
                    if(time<p.LandAt+.06f)DogSpeed(s,at+dir*ext,dir,Clamp01((time-p.At)/.25f)*.2f,.25f,Count(5,q,low),.25f,seed);
                    break;
                }
                case NativeTopDog.PropSquirt:
                {
                    if(time>=p.Until)break;var dir=N(p.Dir);if(dir.LengthSquared()<.5f)break;dir=Vector3.Normalize(dir);int n=Count(26,q,low);float reach=Math.Max(1,p.Size);
                    // The stream: a rope of mustard blobs on a shallow arc, flowing out, with drips off it.
                    for(int i=0;i<n;i++)
                    {
                        float u=((i+time*9)%n)/n,dist=reach*u;var pos=at+dir*dist-Up*(1.6f*u*u*u*reach/9);var seg=dir*.18f-Up*(.25f*u*u);
                        float wob=.02f*(float)Math.Sin(time*40+i*1.7f);pos+=new Vector3(wob,wob*.5f,-wob);
                        s.Drop(pos,seg,(p.Hit==1?DogSauce:DogMustard)*(1.05f+.2f*(float)Math.Sin(i)),.95f,.07f+.05f*u,u,seed+i);
                    }
                    for(int i=0;i<Count(6,q,low);i++){float u=R(seed,i+(int)(time*8)),t=(time*2.3f+R(seed,i))%1;var pos=at+dir*(reach*u)-Up*(1.4f*t);s.Drop(pos,-Up*.08f,p.Hit==1?DogSauce:DogMustard,.8f*(1-t),.035f,t,seed+50+i);}
                    if(p.Hit==1)for(int i=0;i<Count(5,q,low);i++){float u=R(seed,i+(int)(time*6)),t=(time*1.8f+R(seed,i+40))%1;var pos=at+dir*(reach*u)+Up*(.5f*t);s.Smoke(pos,new Vector3(.95f,.92f,.9f),.35f*(1-t),.25f+.4f*t,.1f+.5f*t,seed+60+i,pos.Y-.3f);}
                    break;
                }
                case NativeTopDog.PropZip:
                {
                    // The dust silhouette left standing where it set off, a column of puffs that drifts apart; speed lines down the path.
                    float t=time-p.LandAt;if(t<0){DogPuff(s,at+Up*.1f,age,.3f,Count(3,q,low),.3f,DogDust,.4f,seed);break;}
                    float life=p.Until-p.LandAt;if(t<life){float u=t/life;for(int i=0;i<Count(14,q,low);i++){float h=.15f+1.65f*i/14f,w=(h>1.45f?.2f:h>1.1f?.32f:.26f)*(1+.6f*u);var o=new Vector3((R(seed,i)-.5f)*w,h+.3f*u*R(seed,i+3),(R(seed,i+6)-.5f)*w);s.Smoke(at+o,DogDust*1.15f,.85f*(1-u)*(1-u),.32f+.25f*u,.1f+.5f*u,seed+i,at.Y-.1f);}}
                    var to=N(p.To);var dir=to-at;if(dir.LengthSquared()>.01f&&t<.6f){var d=Vector3.Normalize(dir);for(int i=0;i<Count(8,q,low);i++){float w=R(seed,i+20);var c=Vector3.Lerp(at,to,w)+Up*(.4f+1.1f*R(seed,i+30));s.Line(false,c-d*(.8f+.6f*R(seed,i+40)),c,DogWhite*(1.1f*(1-t/.6f)),.022f,false);}}
                    break;
                }
                case NativeTopDog.PropPiano:
                    if(time<p.LandAt)
                    {
                        float u=Clamp01(age/Math.Max(.05f,p.LandAt-p.At));int seg=Count(26,q,low);
                        Ring(s,at+Up*.03f,NativeDogChips.PianoRadius*(1.3f-.3f*u),.08f+.05f*u,new Vector3(1,.12f,.05f)*(.7f+.6f*(float)Math.Abs(Math.Sin(time*16))),seg);
                        float h=NativeDogView.AnvilDrop(p,time,out _);if(h>.5f)for(int i=0;i<Count(5,q,low);i++){var o=new Vector3((R(seed,i)-.5f)*1.2f,0,(R(seed,i+5)-.5f)*1.2f);s.Line(false,at+o+Up*(h+1.1f),at+o+Up*(h+2.1f+.5f*R(seed,i+9)),DogWhite*.9f,.02f,false);}
                        if(age<.3f)DogPuff(s,at+Up*(NativeDogView.AnvilDrop(p,time,out _)+.6f),age,.3f,Count(6,q,low),.8f,new Vector3(.92f,.92f,.95f),.55f,seed+3,.2f);
                    }
                    else if(time>p.Until-.4f)DogPuff(s,at+Up*.5f,time-(p.Until-.4f),.6f,Count(10,q,low),.9f,new Vector3(.9f,.9f,.9f),.6f,seed);
                    break;
                case NativeTopDog.PropSnack:
                    if(time>=p.LandAt&&time<p.Until&&((int)((time-p.LandAt)*1.6f)%2==0)){float u=((time-p.LandAt)*1.6f)%1;s.Sprite(false,N(p.To)+Up*.18f,DogStar*(1.5f*(float)Math.Sin(Math.PI*u)),.08f,Sparkle);}
                    break;
                case NativeTopDog.PropPuddle:
                    if(p.Hit==1&&time<p.Until){float fade=Clamp01((p.Until-time)/.6f);for(int i=0;i<Count(4,q,low);i++){float t=(time*1.3f+R(seed,i))%1;var o=new Vector3((R(seed,i+3)-.5f)*p.Size*1.2f,0,(R(seed,i+6)-.5f)*p.Size*1.2f);s.Smoke(at+o+Up*(.1f+.6f*t),DogFlame*(1.6f*(1-t)),.6f*(1-t)*fade,.18f+.2f*t,.05f+.3f*t,seed+i,at.Y);}}
                    break;
                case NativeTopDog.PropSpring:if(age<.25f)DogSpeed(s,at+Up*.5f,-Up,age,.25f,Count(5,q,low),.35f,seed);break;
            }
        }
        private static void DogMarkFx(ITechniqueFxSink s,IKitFxScene scene,NativeDogMark m,float time,long seed,float q,bool low)
        {
            var v=DogActor(scene,m.Id);if(v==null||!v.Alive||scene.Viewer==m.Id)return;float age=time-m.At;if(age<0||time>=m.Until)return;
            if(!scene.Chest(v,out var chest))chest=scene.Feet(v)+Up*1.2f;var head=chest+Up*.45f;float left=m.Until-time;
            switch(m.Kind)
            {
                case NativeTopDog.Dazed:
                case NativeTopDog.Walled:
                case NativeTopDog.Pancake:
                {
                    // Stars round the head (a flattened pilot's sit low).
                    var c=m.Kind==NativeTopDog.Pancake?scene.Feet(v)+Up*.45f:head+Up*.12f;int n=Count(5,q,low);float fade=Clamp01(left/.3f)*Clamp01(age/.15f);
                    for(int i=0;i<n;i++){float a=time*5.5f+i*6.2831853f/n;var p=c+new Vector3((float)Math.Cos(a)*.32f,.04f*(float)Math.Sin(a*2),(float)Math.Sin(a)*.32f);s.Sprite(false,p,DogStar*(1.7f*fade),.11f,Sparkle);}
                    break;
                }
                case NativeTopDog.Mustarded:
                    for(int i=0;i<Count(3,q,low);i++){float t=(time*1.6f+R(seed,i))%1;var o=new Vector3((R(seed,i+3)-.5f)*.4f,0,(R(seed,i+6)-.5f)*.4f);s.Drop(chest+o-Up*(1.1f*t),-Up*.06f,DogMustard,.9f*(1-t),.04f,t,seed+i);}
                    break;
                case NativeTopDog.Sauced:
                    // Steam out of the ears, and a little fire breath.
                    for(int i=0;i<Count(4,q,low);i++){float t=(time*1.8f+R(seed,i))%1;var side=new Vector3((i%2==0?-1:1)*.16f,0,0);s.Smoke(head+side+Up*(.1f+.7f*t),new Vector3(.95f,.95f,.95f),.6f*(1-t),.14f+.3f*t,.05f+.3f*t,seed+i,head.Y);}
                    if(Count(2,q,low)>1){float t=(time*2.4f)%1;s.Smoke(head-Up*.12f+Up*(.2f*t),DogFlame*(1.5f*(1-t)),.5f*(1-t),.1f+.15f*t,.05f+.2f*t,seed+9,head.Y);}
                    break;
                case NativeTopDog.Pied:
                    for(int i=0;i<Count(3,q,low);i++){float t=(time*1.1f+R(seed,i))%1;var o=new Vector3((R(seed,i+3)-.5f)*.2f,0,(R(seed,i+6)-.5f)*.2f);s.Drop(head+o-Up*(.6f*t),-Up*.05f,DogCream,.9f*(1-t),.045f,t,seed+i);}
                    break;
                case NativeTopDog.Stuffed:
                {
                    // Sweat flying off, faster as the pop nears.
                    float u=Clamp01(age/Math.Max(.1f,m.Until-m.At));int n=Count(6,q,low);
                    for(int i=0;i<n;i++){float t=(time*(1.5f+3*u)+R(seed,i))%1;var d=Vector3.Normalize(RandomDir(seed,i*5)+Up*1.4f);var p=head+d*(.25f+.5f*t)-Up*(.8f*t*t);s.Drop(p,d*.06f,DogSweat,.85f*(1-t),.035f,t,seed+i);}
                    break;
                }
            }
        }
        private static void DogFeastFx(ITechniqueFxSink s,IKitFxScene scene,Actor caster,NativeDogFeast f,float time,long seed,float q,bool low)
        {
            int phase=NativeDogView.LassoPhase(f,time);
            if(phase==1&&scene.Chest(caster,out var chest)){float t=time-f.At;var top=chest+Up*1.05f;for(int i=0;i<Count(4,q,low);i++){float a=t*16+i*1.57f;var p=top+new Vector3((float)Math.Cos(a)*.75f,0,(float)Math.Sin(a)*.75f);var w=new Vector3(-(float)Math.Sin(a),0,(float)Math.Cos(a))*12;s.Streak(p,w,DogWhite*.8f,.03f,.12f,.03f,.8f,0,seed+i);}}
            // The victim swelling to the pop: a glint on the belly, then the last tremble.
            var v=DogActor(scene,f.Victim);if(v==null||!v.Alive||f.Catch<-50||f.Snap>-50||time>=f.Pop||time<f.Release)return;
            if(scene.Viewer!=f.Victim&&scene.Chest(v,out var c))
            {
                float u=Clamp01((time-f.Release)/Math.Max(.1f,f.Pop-f.Release));var face=new Vector3(-f.Dir.x,0,-f.Dir.z);if(face.LengthSquared()>.01f)face=Vector3.Normalize(face);
                s.Glow(c-Up*.25f+face*(.3f+.2f*u),DogWhite*(.5f+1.2f*u*u),0,.12f+.1f*u,Core,u,seed+5);
            }
        }
    }
}
