using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // The third set's sword, daggers, shield and greatsword: effects, previews, sounds and poses. The sword is the worked example of the set.
    public sealed partial class NativeTechniqueFx
    {
        private static readonly string[] MeleeBuilt={NativeTechniques.Disarm,NativeTechniques.Iaido,NativeTechniques.Challenge};
        private static readonly Vector3 FlickLight=new Vector3(.82f,.93f,1),DrawGold=new Vector3(1,.8f,.42f),DuelRed=new Vector3(1,.2f,.14f);
        private static bool ThirdMelee(ITechniqueFxSink s,IKitFxScene scene,Actor a,NativeTechnique k,float t,long seed,float q,bool low)
        {
            switch(k.Kind)
            {
                case NativeTechniques.Disarm:Disarm(s,scene,a,k,t,seed,q,low);return true;
                case NativeTechniques.Iaido:Iaido(s,scene,a,k,t,seed,q,low);return true;
                case NativeTechniques.Challenge:Challenge(s,scene,a,k,t,seed,q,low);return true;
                default:return false;
            }
        }
        // A broken ring of `n` arcs turning about `c` in the flat, for what a mark holds over someone's head.
        private static void BrokenRing(ITechniqueFxSink s,Vector3 c,float radius,float turn,int n,Vector3 color,float width)
        {
            for(int i=0;i<n;i++)
            {
                float a0=turn+6.2831853f*i/n,a1=a0+6.2831853f/n*.62f;
                var p0=c+new Vector3((float)Math.Cos(a0),0,(float)Math.Sin(a0))*radius;var p1=c+new Vector3((float)Math.Cos(a1),0,(float)Math.Sin(a1))*radius;s.Line(false,p0,p1,color,width,false);
            }
        }

        // ---- Sword: Disarm ----------------------------------------------------------------------------------------------
        // A point of light runs up the blade; the flick is a hair-thin line that outruns the lunge. If it catches someone their weapon sparks,
        // and a cracked ring turns over their head for as long as they cannot use Q or E, so everyone can see the window.
        private static void Disarm(ITechniqueFxSink s,IKitFxScene scene,Actor a,NativeTechnique k,float t,long seed,float q,bool low)
        {
            var from=N(k.From);var dir=Flat(N(k.Dir),Vector3.UnitZ);
            if(t<k.Release){if(scene.Blade(a,false,out var b0,out var b1)){float g=Clamp01((t-k.At)/Math.Max(.05f,k.Release-k.At));s.Sprite(false,Vector3.Lerp(b0,b1,g),(FlickLight+Vector3.One)*(1.2f+1.6f*g),.12f+.1f*g,Sparkle);}return;}
            float age=t-k.Release;
            if(age<.3f)
            {
                float u=OutCubic(age/.2f),fade=1-Smooth(age/.3f);var tip=from+dir*(2.6f*u)+Up*1.18f;var tail=from+dir*(1.1f*u)+Up*1.18f;
                s.Line(false,tail,tip,FlickLight*(1.1f*fade),.06f,false);s.Line(false,tail,tip,(FlickLight+Vector3.One)*(2.4f*fade),.012f,true);s.Sprite(false,tip,(FlickLight+Vector3.One)*(2.2f*fade),.2f,Sparkle);
            }
            if(!k.Hit(t))return;
            float since=t-k.HitAt;var point=N(k.Point);
            Flash(s,point,since,.2f,FlickLight,1.1f,seed);if(since<.6f)Sparks(s,point,since,.5f,Count(20,q,low),6.5f,FlickLight+new Vector3(.4f,.3f,0),seed,8,.024f,dir*.8f);
            if(t>=k.Until)return;var who=ActorOf(scene,k.Target);
            float env=Smooth(since/.14f)*(1-Smooth((t-(k.Until-.3f))/.3f));var head=who!=null?scene.Feet(who)+Up*2.08f:point+Up*.88f;var tint=new Vector3(1,.34f,.28f);
            BrokenRing(s,head,.26f,t*2.6f,low?3:4,tint*(1.8f*env),.022f);s.Sprite(false,head,tint*(.5f*env*(.75f+.25f*(float)Math.Sin(t*9))),.3f,Hot);
            if(who!=null&&!low&&scene.Blade(who,false,out var w0,out var w1)){float run=(t*1.7f)%1;s.Sprite(false,Vector3.Lerp(w0,w1,run),tint*(1.7f*env),.1f,Sparkle);}
        }

        // ---- Sword: Iaido -----------------------------------------------------------------------------------------------
        // The blade goes away and a line of gold creeps along the floor ahead, brighter the longer you hold, with a glint gathering at the hilt: it tells everyone exactly where
        // not to stand. The draw is one bright hair along the whole line, then a beat, then the air opens along it: the wider and hotter the longer the charge.
        private static void Iaido(ITechniqueFxSink s,IKitFxScene scene,Actor a,NativeTechnique k,float t,long seed,float q,bool low)
        {
            var from=N(k.From);var to=N(k.To);var dir=Flat(to-from,Flat(N(k.Dir),Vector3.UnitZ));var r=RightOf(dir);bool drawn=k.Recast(t);
            if(!drawn)
            {
                float hold=Clamp01((t-k.At)/1.5f),env=Smooth((t-k.At)/.15f),pulse=.8f+.2f*(float)Math.Sin(t*14);
                var feet=scene.Feet(a);var line0=feet+Up*.04f;var line1=line0+dir*(5*Smooth(hold*2.2f));
                Strip(s,line0,line1,.09f,DrawGold,.1f*env,.26f*env*pulse*(.5f+.5f*hold));s.Line(false,line0,line1,DrawGold*((.5f+1.1f*hold)*env),.018f,true);
                var hilt=feet+Up*.95f+r*.22f;s.Sprite(false,hilt,(DrawGold+Vector3.One)*((.5f+2.6f*hold*hold)*env*pulse),.1f+.24f*hold,Sparkle);
                if(!low&&hold>.25f){int n=Count(8,q,low);for(int i=0;i<n;i++){float ph=(t*.9f+R(seed,i+3))%1,ang=6.2831853f*R(seed,i+4);var p=hilt+new Vector3((float)Math.Cos(ang),0,(float)Math.Sin(ang))*(.5f*(1-ph))+Up*(.3f*(R(seed,i+5)-.5f)*(1-ph));s.Glow(p,DrawGold*(1.6f*ph*hold),0,.05f,Core,0,seed+i);}}
                return;
            }
            float since=t-k.RecastAt,charge=Clamp01(k.Arm);if(since>.9f)return;var mid=Up*1.1f;
            // The hair, the instant of the draw.
            if(since<.14f){float f=1-Clamp01(since/.14f);s.Line(false,from+mid,to+mid,(DrawGold+Vector3.One)*(4.5f*f),.016f+.012f*charge,true);s.Line(false,from+mid,to+mid,DrawGold*(1.4f*f),.12f,false);}
            // The afterimages left behind along the path.
            if(since<.3f){int n=low?3:5;for(int i=0;i<n;i++){float u=(i+.5f)/n;var p=Vector3.Lerp(from,to,u)+mid;float f=1-Clamp01((since-.03f*i)/.24f);if(f>0)s.Sprite(false,p,DrawGold*(1.2f*f),.42f,Hot);}}
            // The beat, then the cut opens.
            float open=since-.09f;if(open<=0)return;float u2=OutCubic(open/.26f),fade=1-Smooth(open/.62f),half=(.1f+.22f*charge)*u2;
            for(int side=-1;side<=1;side+=2){var o=Up*(half*side);s.Band(false,from+mid+o-r*.02f,to+mid+o-r*.02f,to+mid+o+r*.02f,from+mid+o+r*.02f,1.8f*fade,1.2f*fade,DrawGold);}
            s.Band(false,from+mid-Up*half,to+mid-Up*half,to+mid+Up*half,from+mid+Up*half,.4f*fade,.25f*fade,DrawGold);
            if(k.Hit(t)&&open<.7f){int n=Count(14,q,low);for(int i=0;i<n;i++){var at=Vector3.Lerp(from,to,R(seed,i+11))+mid;Sparks(s,at,open,.55f,low?1:2,5.5f+3*charge,DrawGold+new Vector3(.3f,.2f,.1f),seed+i*17,9,.02f,r*(R(seed,i+12)-.5f)*2);}}
            Flash(s,to+mid,open,.22f,DrawGold,.9f+.7f*charge,seed);
        }

        // ---- Sword: Challenge -------------------------------------------------------------------------------------------
        // The tip points, a red spark jumps the gap, and from then on a taut line runs chest to chest and a ring burns under each of you: the rest of the fight can see the duel.
        private static void Challenge(ITechniqueFxSink s,IKitFxScene scene,Actor a,NativeTechnique k,float t,long seed,float q,bool low)
        {
            if(t<k.Release){if(scene.Blade(a,false,out var b0,out var b1)){float g=Clamp01((t-k.At)/Math.Max(.05f,k.Release-k.At));s.Sprite(false,b1,DuelRed*(1+3*g),.14f+.14f*g,Hot);}return;}
            if(t>=k.Until)return;var who=ActorOf(scene,k.Target);
            float age=t-k.Release,env=Smooth(age/.18f)*(1-Smooth((t-(k.Until-.3f))/.3f));var mine=scene.Feet(a);var theirs=who!=null?scene.Feet(who):N(k.Point);
            var c0=scene.Chest(a,out var cc0)?cc0:mine+Up*1.25f;var c1=who!=null&&scene.Chest(who,out var cc1)?cc1:theirs+Up*1.25f;
            if(age<.26f){var run=Vector3.Lerp(c0,c1,OutCubic(age/.2f));s.Sprite(false,run,(DuelRed+Vector3.One*.3f)*4f,.3f,Hot);if(age>.2f)Flash(s,c1,age-.2f,.2f,DuelRed,1.2f,seed);}
            float flicker=.75f+.25f*(float)Math.Sin(t*23+1.4f*Math.Sin(t*7));
            s.Line(false,c0,c1,DuelRed*(.55f*env*flicker),.05f,false);s.Line(false,c0,c1,(DuelRed+new Vector3(0,.4f,.4f))*(1.4f*env*flicker),.012f,true);
            float rr=.72f+.04f*(float)Math.Sin(t*5);int seg=low?14:24;Ring(s,mine+Up*.04f,rr,.07f,DuelRed,seg,.8f*env);Ring(s,theirs+Up*.04f,rr,.07f,DuelRed,seg,.8f*env);
            if(!low){int n=Count(4,q,low);for(int i=0;i<n;i++){float ph=(t*.7f+i/(float)n)%1;s.Sprite(false,Vector3.Lerp(c0,c1,ph),DuelRed*(1.6f*env),.08f,Sparkle);}}
        }
    }
    public sealed partial class NativeTechniquePreview
    {
        private static V3 V3Of(Vector3 v)=>new V3(v.X,v.Y,v.Z);
        private static bool WriteMelee(NativeTechnique k,string id,Vector3 feet,Vector3 f,Vector3 r,Vector3 up,Vector3 hand,float t0,bool first)
        {
            switch(id)
            {
                case NativeTechniques.Disarm:k.Release=t0+.14f;k.From=V3Of(feet);k.Dir=V3Of(f);k.Point=V3Of(feet+f*2.4f+up*1.2f);if(first){k.HitAt=k.Release+.14f;k.Until=k.HitAt+2.5f;k.RecoverUntil=k.Release+.3f;}else{k.Until=k.Release+.5f;k.RecoverUntil=k.Until;}return true;
                case NativeTechniques.Iaido:k.Release=t0+.15f;k.From=V3Of(feet);k.To=V3Of(feet+f*5);k.Dir=V3Of(f);k.Arm=first?1:.35f;k.RecastAt=t0+(first?1.5f:.55f);k.Until=k.RecastAt+.3f;k.RecoverUntil=k.Until+.25f;k.HitAt=first?k.RecastAt+.05f:-100;return true;
                case NativeTechniques.Challenge:k.Release=t0+.22f;k.Point=V3Of(feet+f*5);k.Until=k.Release+(first?6:3);k.RecoverUntil=k.Release+.2f;return true;
                default:return false;
            }
        }
        private static float PeriodMelee(string id)
        {
            switch(id)
            {
                case NativeTechniques.Disarm:return 4.4f;
                case NativeTechniques.Iaido:return 3.4f;
                case NativeTechniques.Challenge:return 7.5f;
                default:return 0;
            }
        }
        private static bool AimMelee(NativeTechnique k,string id,int target,Vector3 at)
        {
            switch(id)
            {
                case NativeTechniques.Disarm:if(k.HitAt>-50){k.Target=target;k.Point=V3Of(at+Vector3.UnitY*1.2f);}return true;
                case NativeTechniques.Challenge:k.Target=target;k.Point=V3Of(at);return true;
                default:return false;
            }
        }
    }
    public static partial class NativeTechniqueSounds
    {
        private static int MomentMelee(string kind,int m,NativeKitSound[] o,ref bool atPoint)
        {
            int n=0;
            switch(kind)
            {
                case NativeTechniques.Disarm:if(m==Release)n=S(o,n,"melee.sword.swing","sword",.75f);else if(m==Hit){atPoint=true;n=S(o,n,"melee.shield.block","parry",.8f);n=S(o,n,"impact.metal","metal",.7f);}break;
                case NativeTechniques.Iaido:if(m==Release)n=S(o,n,"gun.reload.end","metal",.3f);else if(m==Recast){n=S(o,n,"melee.greatsword.swing","greatsword",.9f);n=S(o,n,"move.roll","dash",.6f);}else if(m==Hit)n=S(o,n,"melee.greatsword.hit","greatsword",.85f);break;
                case NativeTechniques.Challenge:if(m==Release){n=S(o,n,"melee.shield.bash","shield",.4f);n=S(o,n,"blast.kinetic","dash",.35f);}break;
            }
            return n;
        }
    }
    public static partial class NativeVisualMotion
    {
        private static bool PoseMelee(Set set,NativeTechnique k,float time,float back,IClips clips,out Pick pick)
        {
            pick=default;Pick got=default;
            bool Part(string clip,float from,float to){return time>=from&&time<to&&clip!=null&&clips.Has(clip)&&Fit(clip,time,from,to,clips,FadeIn,out got);}
            bool hit=false;
            switch(k.Kind)
            {
                case NativeTechniques.Disarm:hit=Part(TechCounter,k.At,Math.Max(k.Release+.3f,back));break;
                case NativeTechniques.Iaido:{float draw=k.RecastAt>-50?k.RecastAt:k.Until;hit=Part(TechSurf,k.At,draw)||Part("Armed-Dash-Forward",draw,draw+.14f)||Part(TechCounter,draw+.14f,Math.Max(draw+.4f,k.RecoverUntil));}break;
                case NativeTechniques.Challenge:hit=Part(TechCoin,k.At,back);break;
                default:return false;
            }
            pick=got;return hit;
        }
    }
}
