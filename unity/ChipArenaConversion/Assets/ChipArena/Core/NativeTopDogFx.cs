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
        /// <summary>Hand-fed sausage `i` at `time` (NativeTopDog's feeding beats): 0 none; 1 in the hand at the hip, out of hammerspace; 2
        /// carried to the mouth (`u` 0..1); 3 pushed in (`u` 0..1: to half its length); 4 stuck half out of the mouth, squirming (`u` 0..1 of
        /// its stay); 5 slurped down (`u` 0..1); 6 spat out by a snap (`u` 0..1 of its flight). Gone at the pop.</summary>
        public static int FeedState(NativeDogFeast f,int i,float time,out float u)
        {
            u=0;if(f==null||f.Catch<-50||i<0||i>=NativeTopDog.Feeds)return 0;float offer=NativeTopDog.OfferAt(f.FeedAt,i),down=NativeTopDog.DownAt(f.FeedAt,i);
            float appear=offer-NativeTopDog.Appear,carry=offer-NativeTopDog.CarryFrom,push=offer+NativeTopDog.PushFrom,inAt=offer+NativeTopDog.PushEnd,stuck=inAt+NativeTopDog.StickFor,gone=stuck+NativeTopDog.Slurp;
            if(f.Snap>-50&&time>=f.Snap){if(f.Snap<appear||f.Snap>=gone)return 0;u=(time-f.Snap)/NativeTopDog.Spit;return u<1?6:0;}
            if(time>=f.Pop||time<appear)return 0;
            if(time<carry)return 1;
            if(time<push){u=Clamp01((time-carry)/(push-carry));return 2;}
            if(time<inAt){u=Clamp01((time-push)/(inAt-push));return 3;}
            if(time<stuck){u=Clamp01((time-inAt)/NativeTopDog.StickFor);return 4;}
            if(time<gone){u=Clamp01((time-stuck)/NativeTopDog.Slurp);return 5;}
            return 0;
        }
        /// <summary>Hand `hand` of the feeding Top Dog at `time` and its reach weight `w`: the right (0) feeds them all, `i` the sausage it is
        /// on (the one it carries or pushes, else the next); the left (1) holds the jaw open (`i` -1). False while it does neither.</summary>
        public static bool FeedHand(NativeDogFeast f,int hand,float time,out int i,out float w)
        {
            i=-1;w=0;if(f==null||f.Catch<-50||f.Snap>-50&&time>=f.Snap||time>=f.Release)return false;
            if(hand==1){float from=f.FeedAt-NativeTopDog.JawFrom;if(time<from)return false;w=Smooth((time-from)/.2f)*(1-Smooth((time-(f.Release-.12f))/.12f));return w>0;}
            float first=NativeTopDog.OfferAt(f.FeedAt,0)-NativeTopDog.GrabLead,last=NativeTopDog.OfferAt(f.FeedAt,NativeTopDog.Feeds-1)+NativeTopDog.PushEnd;
            if(time<first||time>=last+NativeTopDog.LetGo)return false;
            for(int k=0;k<NativeTopDog.Feeds;k++){if(time<NativeTopDog.OfferAt(f.FeedAt,k)+NativeTopDog.PushEnd){i=k;break;}}if(i<0)i=NativeTopDog.Feeds-1;
            w=Smooth((time-first)/(NativeTopDog.GrabLead*.6f))*(1-Smooth((time-last)/NativeTopDog.LetGo));return w>0;
        }
        // ---- the hand feeding made real (owner 2026-11-09: "actually look like they are realistically putting hot dogs in the players mouth") ----
        /// <summary>The push of the sausage going in at `time` (0..1: a bump over each one's push and the moment after it): the victim's lips
        /// close round it, their head gives with it and the hand on the chin tugs.</summary>
        public static float Push(NativeDogFeast f,float time)
        {
            if(f==null||f.Catch<-50||f.Snap>-50&&time>=f.Snap)return 0;float bump=0;
            for(int i=0;i<NativeTopDog.Feeds;i++)
            {
                float a=NativeTopDog.OfferAt(f.FeedAt,i)+NativeTopDog.PushFrom,b=NativeTopDog.OfferAt(f.FeedAt,i)+NativeTopDog.PushEnd+PushAfter;
                if(time>a&&time<b)bump=Math.Max(bump,(float)Math.Sin((time-a)/(b-a)*Math.PI));
            }
            return bump;
        }
        /// <summary>How long a push's bump runs on past its push (s).</summary>
        public const float PushAfter=.14f;
        /// <summary>How wide the victim's mouth is at `time` (0..1, the Gobble face): a gasp as the links catch, pulled wide as the left hand
        /// takes the chin, the lips closing a little round each sausage as it is pushed in, then wide again for the next; shut after the
        /// let-go (the stuffed face) or a snap.</summary>
        public static float Jaw(NativeDogFeast f,float time)
        {
            if(f==null||f.Catch<-50||time<f.Catch||time>=f.Release||f.Snap>-50&&time>=f.Snap+.3f)return 0;
            float from=f.FeedAt-NativeTopDog.JawFrom,open=time<from?.45f:.45f+.55f*Smooth((time-from)/.25f);
            return open*(1-.3f*Push(f,time));
        }
        /// <summary>The feeding Top Dog's fingers at `time` (the model's grip, 0..1): the right's pinched on a sausage's end from when it pops
        /// into the hand to its let-go, relaxed between; the left's cupped under the victim's chin. False while neither hand feeds.</summary>
        public static bool FeedGrip(NativeDogFeast f,float time,out float pinch,out float cup)
        {
            pinch=cup=0;if(f==null)return false;bool any=false;
            if(FeedHand(f,1,time,out _,out float wl)){cup=CupGrip*wl;any=true;}
            if(FeedHand(f,0,time,out int i,out float wr))
            {
                int st=FeedState(f,i,time,out _);float offer=NativeTopDog.OfferAt(f.FeedAt,i),hold;
                if(st>=1&&st<=3)hold=Smooth((time-(offer-NativeTopDog.Appear+.03f))/.06f);
                else if(st>=4)hold=1-Smooth((time-(offer+NativeTopDog.PushEnd))/.06f);
                else hold=i>0?1-Smooth((time-(NativeTopDog.OfferAt(f.FeedAt,i-1)+NativeTopDog.PushEnd))/.06f):0;
                pinch=(OpenGrip+(PinchGrip-OpenGrip)*hold)*wr;any=true;
            }
            return any;
        }
        /// <summary>The grips: an empty feeding hand, one pinched on a sausage, the left cupped round the chin.</summary>
        public const float OpenGrip=.12f,PinchGrip=.62f,CupGrip=.5f;
        /// <summary>The links' look at `time`: 0 none, 1 twirled overhead, 2 flying (the loop at f.Lasso), 3 round the caught victim, 4 lying
        /// where they fell (a miss or a snap).</summary>
        public static int LassoPhase(NativeDogFeast f,float time)
        {
            if(f==null||time<f.At)return 0;if(time<f.Throw)return 1;
            if(f.Catch<-50)return f.Miss?(time<f.Until?4:0):2;
            if(f.Snap>-50&&time>=f.Snap)return time<f.Snap+.8f?4:0;
            return time<f.Release?3:0;
        }
        /// <summary>When the mallet goes into hammerspace and when it comes back (owner 2026-11-09: "since hammer disappears, make a "poof"
        /// effect when it appears and disappears"), as MalletShown hides and shows it: -100 for none.</summary>
        public static void MalletPoofs(NativeTopDogKit k,out float hide,out float show)
        {
            hide=show=-100;if(k==null)return;
            if(k.Feast!=null){hide=k.Feast.At;show=(k.Feast.Catch>-50?(k.Feast.Snap>-50?k.Feast.Snap:k.Feast.Release):k.Feast.Until)+.3f;}
            if(k.CastKind!=null&&!NativeTopDog.MalletMove(k.CastKind)&&k.CastKind!=NativeTopDog.Boing&&k.CastKind!=NativeTopDog.Zip&&k.CastKind!=NativeTopDog.Feast){hide=k.CastAt;show=k.CastUntil;}
        }
        /// <summary>Where each Top Dog's mustard bottle's nozzle is drawn this frame and which way it points (the presentation writes it; the
        /// stream leaves the bottle there). Keyed by the actor.</summary>
        public static readonly System.Collections.Generic.Dictionary<int,(float time,Vector3 at,Vector3 dir)> Nozzles=new System.Collections.Generic.Dictionary<int,(float,Vector3,Vector3)>();
        /// <summary>The Top Dog's mallet hidden away (hammerspace) while both hands are busy: the feast, the bottle, the glove, the pie, the peel.</summary>
        public static float MalletShown(NativeTopDogKit k,float time)
        {
            if(k==null)return 1;float shown=1;
            if(k.Feast!=null&&time>=k.Feast.At-.05f){float end=k.Feast.Catch>-50?(k.Feast.Snap>-50?k.Feast.Snap:k.Feast.Release):k.Feast.Until;shown=Math.Min(shown,1-Clamp01((time-k.Feast.At)/.12f)+Clamp01((time-end-.3f)/.2f));}
            if(k.CastKind!=null&&!NativeTopDog.MalletMove(k.CastKind)&&k.CastKind!=NativeTopDog.Boing&&k.CastKind!=NativeTopDog.Zip&&k.CastKind!=NativeTopDog.Feast&&time>=k.CastAt-.02f&&time<k.CastUntil+.3f)
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
        /// <summary>The shot at `time` for the caster at `feet` and the victim at `body` (their body's middle), on side `flip` of the feast's
        /// line (1 its right, -1 its left; 0 the caster's fallback, high over its shoulder, for when both sides are walled in: the
        /// Runtime picks per phase, DogShotSide, so a wall never pulls the camera into the Top Dog).</summary>
        public static KernelPanicCamera.Shot At(NativeDogFeast f,Vector3 feet,Vector3 body,float time,bool victim,bool reduced,bool shake,float flip=1)
        {
            var s=new KernelPanicCamera.Shot();var d=new Vector3(f.Dir.x,0,f.Dir.z);d=d.LengthSquared()>1e-5f?Vector3.Normalize(d):Vector3.UnitZ;var r=new Vector3(d.Z,0,-d.X);if(flip<0)r=-r;var up=Vector3.UnitY;
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
            if(flip==0&&!victim){s.Position=feet-d*2.5f+r*.5f+up*2.7f;s.Look=f.Catch<-50?feet+up*1.5f+d*1.4f:(feet+up*1.2f+body)*.5f;s.Fov=Math.Max(s.Fov,52);}
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
        /// <summary>Whether gore is on (the presentation's setting): the pop then bursts as a gib (its blood mist, flecks and bits here),
        /// else as the toon pop.</summary>
        public bool DogGore=true;
        public void DrawDogs(ITechniqueFxSink s,IKitFxScene scene,float time,float q,bool low)
        {
            if(s==null||scene?.Actors==null||NativeDogView.Source==null)return;var actors=scene.Actors;
            for(int i=0;i<actors.Count;i++)
            {
                var a=actors[i];if(a==null)continue;var k=NativeDogView.Of(a);if(k==null)continue;long seed=a.Id*7307L;
                if(k.Hits!=null)foreach(var h in k.Hits)if(h!=null)DogHitFx(s,scene,h,time,seed+h.Id*53L,q,low,DogGore);
                if(k.Props!=null)foreach(var p in k.Props)if(p!=null)DogPropFx(s,scene,a,p,time,seed+p.Id*41L,q,low);
                if(k.Marks!=null)foreach(var m in k.Marks)if(m!=null)DogMarkFx(s,scene,m,time,seed+m.Id*29L+m.Kind.Length,q,low);
                if(k.Feast!=null)DogFeastFx(s,scene,a,k.Feast,time,seed+17,q,low);
                DogMoveFx(s,scene,a,k,time,seed+23,q,low);
            }
        }
        // The mallet's dash and slam while they go: white speed lines streaming back off the charging Top Dog and dust kicked up behind its
        // feet; on the drop, lines rushing up past it.
        private static void DogMoveFx(ITechniqueFxSink s,IKitFxScene scene,Actor a,NativeTopDogKit k,float time,long seed,float q,bool low)
        {
            DogSmear(s,scene,a,k,time,seed+5,q,low);DogPoofs(s,scene,a,k,time,seed+9,q,low);
            if(k.CastKind==NativeTopDog.Dash&&time>=k.CastRelease&&time<k.CastRelease+NativeTopDog.DashTime+.1f)
            {
                var feet=scene.Feet(a);float yaw=a.Yaw*(float)Math.PI/180;var f=new Vector3((float)Math.Sin(yaw),0,(float)Math.Cos(yaw));var side=new Vector3(f.Z,0,-f.X);
                float fade=1-Clamp01((time-k.CastRelease-NativeTopDog.DashTime)/.1f);
                for(int i=0;i<Count(9,q,low);i++)
                {
                    float c=(float)((time*7+R(seed,i))%1.0),len=.7f+.8f*R(seed,i+20);var head=feet+Up*(.25f+1.5f*R(seed,i+40))+side*((R(seed,i+60)-.5f)*1.3f)-f*(.4f+1.6f*c);
                    s.Line(false,head-f*len,head,DogWhite*(1.4f*fade*(float)Math.Sin(Math.PI*c)),.03f,false);
                }
                DogPuff(s,feet-f*.4f+Up*.05f,(float)((time-k.CastRelease)%.12f),.12f,Count(2,q,low),.35f,DogDust,.5f*fade,seed+(long)((time-k.CastRelease)/.12f)*13,.25f);
            }
            if((k.CastKind==NativeTopDog.Slam||k.CastKind==NativeTopDog.Smash)&&k.CastStep==0&&time>=k.CastRelease&&time<k.CastUntil)
            {
                var feet=scene.Feet(a);
                for(int i=0;i<Count(8,q,low);i++)
                {
                    float c=(float)((time*6+R(seed,i))%1.0),len=.8f+.9f*R(seed,i+20),ang=R(seed,i+40)*6.283f,r=.35f+.4f*R(seed,i+60);
                    var head=feet+new Vector3((float)Math.Cos(ang)*r,.6f+2.4f*c,(float)Math.Sin(ang)*r);s.Line(false,head,head+Up*len,DogWhite*(1.3f*(float)Math.Sin(Math.PI*c)),.03f,false);
                }
            }
        }
        /// <summary>The mallet's toon smear (owner 2026-11-09: "Make this animation a lot better"): through each swing's fast part a pale arc
        /// sweeps behind the head the width of its barrel, fading back along its path, with speed lines off its outer edge; from the keyed
        /// mallet (Core NativeDogMallet) on the drawn feet and facing.</summary>
        private static void DogSmear(ITechniqueFxSink s,IKitFxScene scene,Actor a,NativeTopDogKit k,float time,long seed,float q,bool low)
        {
            if(k?.CastKind==null||!NativeTopDog.MalletMove(k.CastKind)||!NativeDogMallet.Sample(k,time,out var now,out var move,out float from)||now.Smear<.02f)return;
            var feet=scene.Feet(a);float yaw=a.Yaw,size=1+NativeDogChips.MalletGrow*k.Mallet;int n=low?5:9;const float step=.011f;
            Vector3 head0=default,in0=default,out0=default;float k0=0;bool has=false;
            for(int i=0;i<n;i++)
            {
                float t=time-i*step;if(!NativeDogMallet.Sample(k,t,out var p,out var m,out float f)||m!=move||Math.Abs(f-from)>1e-3f)break;
                var head=feet+NativeDogMallet.Turn(NativeDogMallet.HeadAt(in p,size),yaw,0,0);var d=NativeDogMallet.Turn(NativeDogMallet.HandleAt(p),yaw,0,0);
                float r=NativeDogMallet.Head*size,fade=1-i/(float)n,kk=p.Smear*p.Weight*fade*fade;var inner=head-d*r*1.05f;var outer=head+d*r*1.05f;
                if(has&&(k0>.01f||kk>.01f))
                {
                    s.Band(false,in0,inner,outer,out0,.55f*k0,.55f*kk,DogCream);
                    s.Band(false,head0-d*r*.35f,head-d*r*.35f,head+d*r*.35f,head0+d*r*.35f,.35f*k0,.35f*kk,DogWhite);
                    if(i%2==1&&!low)s.Line(false,out0+d*.06f,outer+d*.06f,DogWhite*(.9f*kk),.018f,false);
                }
                head0=head;in0=inner;out0=outer;k0=kk;has=true;
            }
        }
        /// <summary>The mallet in and out of hammerspace: a cartoon poof where it goes and where it comes back, a white pop, a ring of puffs
        /// bursting out and twinkles.</summary>
        private static void DogPoofs(ITechniqueFxSink s,IKitFxScene scene,Actor a,NativeTopDogKit k,float time,long seed,float q,bool low)
        {
            NativeDogView.MalletPoofs(k,out float hide,out float show);
            if(time-hide<0&&time-show<0)return;Vector3 at;
            if(scene.Blade(a,false,out var root,out var tip))at=Vector3.Lerp(root,tip,.55f);
            else{float y=a.Yaw*(float)Math.PI/180;at=scene.Feet(a)+Up*1.2f+new Vector3((float)Math.Sin(y),0,(float)Math.Cos(y))*.45f;}
            if(hide>-50)DogPoof(s,at,time-hide,seed,q,low);if(show>-50)DogPoof(s,at,time-show,seed+31,q,low);
        }
        private static void DogPoof(ITechniqueFxSink s,Vector3 at,float age,long seed,float q,bool low)
        {
            const float life=.5f;if(age<0||age>life)return;float u=age/life,pop=OutCubic(age/.12f);
            if(age<.12f)s.Glow(at,DogWhite*(2.2f*(1-age/.12f)),0,.25f+.35f*pop,Core,u,seed);
            s.Glow(at,DogCream*(1.1f*(1-u)),0,.3f+.5f*OutCubic(u),RingGlow,u,seed+1);
            int n=Count(10,q,low);
            for(int i=0;i<n;i++)
            {
                float a=i*6.2831853f/n+R(seed,i)*.5f;var d=new Vector3((float)Math.Cos(a),.35f*(R(seed,i+11)-.4f),(float)Math.Sin(a));
                var p=at+d*(.12f+.38f*OutCubic(u))*(.8f+.4f*R(seed,i+21))+Up*(.12f*u);
                s.Smoke(p,new Vector3(.97f,.95f,.92f),.85f*(1-u)*(1-u),(.14f+.2f*OutCubic(u))*(.8f+.5f*R(seed,i+31)),.1f+.5f*u,seed+i,at.Y-1.2f);
            }
            for(int i=0;i<Count(5,q,low);i++){var d=Vector3.Normalize(RandomDir(seed,i*7+3)+Up*.5f);s.Sprite(false,at+d*(.5f*OutCubic(age/.35f)),DogStar*(1.8f*(1-u)),.07f,Sparkle);}
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
        private static void DogHitFx(ITechniqueFxSink s,IKitFxScene scene,NativeDogHit h,float time,long seed,float q,bool low,bool gore=true)
        {
            float age=time-h.At;if(age<0||age>2.2f)return;var at=N(h.Pos);var dir=N(h.Dir);if(dir.LengthSquared()<1e-6f)dir=Up;dir=Vector3.Normalize(dir);
            switch(h.Kind)
            {
                case NativeTopDog.HitBonk:DogBurst(s,at,age,.55f,DogStar,seed,q,low);break;
                case NativeTopDog.HitSlam:DogBurst(s,at,age,.75f,DogStar,seed,q,low,12);break;
                case NativeTopDog.HitClang:
                    if(age<.6f){Ring(s,at+Up*.03f,.4f+NativeTopDog.SlamRadius*OutCubic(age/.35f),.35f*(1-age/.6f),DogWhite*(1.2f*(1-age/.6f)),Count(28,q,low));}
                    Cracks(s,at,age,1.1f,Count(9,q,low),NativeTopDog.SlamRadius*.8f,new Vector3(.25f,.2f,.15f),seed);Dust(s,at,age,1.2f,Count(12,q,low),NativeTopDog.SlamRadius,DogDust,.7f,seed+5);DogBurst(s,at+Up*.2f,age,.6f,DogStar,seed+9,q,low,7);break;
                case NativeTopDog.HitQuake:
                    // Drawn as the mallet's head lands, its strike's length after the feet (Core NativeDogMallet: it swings down on landing).
                    age-=NativeDogMallet.HitKey(NativeDogMallet.Move.Slam);if(age<0)break;
                    Flash(s,at+Up*.3f,age,.22f,DogWhite,1.5f,seed);
                    if(age<.6f)Ring(s,at+Up*.03f,.5f+NativeTopDog.SlamRing*OutCubic(age/.32f),.45f*(1-age/.6f),DogWhite*(1.35f*(1-age/.6f)),Count(36,q,low));
                    if(age>.06f&&age<.7f)Ring(s,at+Up*.03f,.3f+NativeTopDog.SlamRing*.7f*OutCubic((age-.06f)/.4f),.3f*(1-(age-.06f)/.64f),DogStar*(1-(age-.06f)/.64f),Count(28,q,low));
                    Cracks(s,at,age,1.4f,Count(11,q,low),NativeTopDog.SlamRing*.85f,new Vector3(.22f,.18f,.13f),seed+3);Dust(s,at,age,1.6f,Count(16,q,low),NativeTopDog.SlamRing*1.1f,DogDust,.8f,seed+5);
                    DogBurst(s,at+Up*.25f,age,.8f,DogStar,seed+9,q,low,12);Sparks(s,at+Up*.2f,age,.55f,Count(10,q,low),4.5f,new Vector3(1,.8f,.4f),seed+11,8,.025f,Up*.9f);break;
                case NativeTopDog.HitDash:DogPuff(s,at+Up*.08f,age,.8f,Count(9,q,low),.7f,DogDust,.65f,seed);DogSpeed(s,at+Up*.8f,dir,age,.3f,Count(6,q,low),.5f,seed+2);break;
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
                case NativeTopDog.HitPop:DogPop(s,at,age,seed,q,low,gore);break;
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
        // The burst. With gore on (owner 2026-11-09: "make the gib effects more realistic") the body bursts as a gib (Runtime: the corpse's
        // NativeSmashGib.Burst, its severed limbs, torn chunks and blood); here only a short wet flash, a mist of blood bursting out and
        // sinking, and flecks of blood, bits of sausage and a little mustard flung out every way. With gore off, the toon pop: a flash, a
        // ring, a shower of confetti and condiment, and a puff.
        private static readonly Vector3 DogBlood=new Vector3(.3f,.015f,.025f),DogMist=new Vector3(.46f,.05f,.05f),DogMeat=new Vector3(.74f,.36f,.28f);
        private static void DogPop(ITechniqueFxSink s,Vector3 at,float age,long seed,float q,bool low,bool gore=false)
        {
            if(gore)
            {
                Flash(s,at+Up*.45f,age,.1f,new Vector3(1,.62f,.52f),.8f,seed);
                DogPuff(s,at+Up*.45f,age,1.3f,Count(18,q,low),1.2f,DogBlood,.62f,seed+9,-.15f);DogPuff(s,at+Up*.5f,age,.8f,Count(10,q,low),.75f,DogMist,.5f,seed+13,.1f);
                int flecks=Count(36,q,low);for(int i=0;i<flecks;i++)
                {
                    float life=.7f+.5f*R(seed,i+40);if(age>life)continue;var d=Vector3.Normalize(RandomDir(seed,i*7+1)+Up*.35f);float t=age,sp=4+5*R(seed,i+77);
                    var p=at+Up*.55f+d*(sp*t)-Up*(9*t*t);var c=i%6==0?DogMeat:i%9==0?DogMustard*.8f:DogBlood*(.8f+.5f*R(seed,i+5));
                    s.Drop(p,d*(.06f+.05f*R(seed,i+9))-Up*(.1f*t),c,.95f*(1-age/life),(i%6==0?.035f:.018f)+.012f*R(seed,i+3),age/life,seed+i);
                }
                return;
            }
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
                    // Out of the drawn bottle's nozzle (owner 2026-11-09: "Fix the mustard blaster to actually come out of the bottle"), bent on
                    // to the rules' line: the stream ends where the squirt reaches.
                    if(owner!=null&&NativeDogView.Nozzles.TryGetValue(owner.Id,out var nz)){var end=at+dir*reach;at=nz.at;var to=end-at;float far=to.Length();if(far>.5f){dir=to/far;reach=far;}
                        for(int i=0;i<Count(4,q,low);i++){float u=(float)((time*6+R(seed,i+70))%1.0);s.Drop(at+nz.dir*(.04f*u)+RandomDir(seed,i+80)*(.03f*u),nz.dir*.05f,DogMustard*1.2f,.9f*(1-u),.05f*(1-u)+.02f,u,seed+80+i);}}
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
            // Wrapped (owner 2026-11-09: "struggling to get out"): cartoon strain lines flicking off its sides with each heave, sweat flying off
            // its head, its feet scuffing up dust as it throws its weight about (NativeTopDog.Strain, as its body's struggle).
            if(phase==3)
            {
                var w=DogActor(scene,f.Victim);
                if(w!=null&&w.Alive&&scene.Viewer!=f.Victim&&scene.Chest(w,out var wc))
                {
                    float strain=NativeTopDog.Strain(f,time,out float side);var face=new Vector3(-f.Dir.x,0,-f.Dir.z);face=face.LengthSquared()>.01f?Vector3.Normalize(face):Vector3.UnitZ;var right=Vector3.Cross(Up,face);
                    if(strain>.25f)
                    {
                        float k=(strain-.25f)/.75f;int n=Count(3,q,low);
                        for(int sd=-1;sd<=1;sd+=2)for(int i=0;i<n;i++)
                        {
                            float push=.55f+.45f*sd*side,len=(.1f+.12f*k)*push,y=-.28f+.24f*i;var at=wc+Up*y+right*(sd*(.38f+.04f*k));
                            s.Line(false,at,at+right*(sd*len)+Up*(.04f*(i-1)),DogWhite*(1.5f*k*push),.022f,false);
                        }
                    }
                    var top=wc+Up*.45f;int drops=Count(4,q,low);
                    for(int i=0;i<drops;i++){float tt=(time*2.2f+R(seed,i+70))%1;var d=Vector3.Normalize(RandomDir(seed,i*7+3)+Up*1.2f);s.Drop(top+d*(.2f+.45f*tt)-Up*(.7f*tt*tt),d*.06f,DogSweat,.85f*(1-tt)*(.4f+.6f*strain),.03f,tt,seed+40+i);}
                    if(!low){var feet=scene.Feet(w);float pu=(time*3)%1;s.Smoke(feet+right*(side>0?.12f:-.12f)+Up*(.04f+.15f*pu),DogDust,.4f*strain*(1-pu),.1f+.15f*pu,pu,seed+91,feet.Y);}
                }
            }
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
