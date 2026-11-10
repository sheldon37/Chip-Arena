using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // ---- the Glaive's effects (docs/GLAIVE.md "The look"; owner 2026-11-09: "with great quality effects") ----
    // In the pilot's weapon colour: a crescent of light along the ring's outer edge through every fast part of a cut (from the keyed moves
    // on the drawn feet and facing), the thrown ring's spinning glow, its wake and the motes it sheds, a sharp ping and sparks at each cut
    // and ricochet, the halo's circling trail, the burst (a white flash, a shockwave over the floor, shards and sparks flung out), the ring
    // re-forming out of converging light, and a flash as it is caught. Its chips' moments (Core NativeGlaiveChips): Twin Orbit's second ring
    // of light, Shatterpoint's shards, Skimming Stone's slick, Quick Catch's rush, Crescent Guard's shimmer and the shots it cuts down, the
    // drags toward the wielder and Keen Edge's bleeding. Pure: the same record and clock draw the same.
    public sealed partial class NativeTechniqueFx
    {
        private static readonly Vector3 GlaiveWhite=new Vector3(1,1,1);
        private static Vector3 GlaiveTint(IKitFxScene scene,Actor a){var c=WeaponPaint.Linear(scene.WeaponColor(a));float m=Math.Max(c.X,Math.Max(c.Y,c.Z));return m>1e-3f?c/m:new Vector3(.5f,.85f,1);}
        /// <summary>Draws every Glaive's moves, flights and hits of the frame at `time`.</summary>
        public void DrawGlaives(ITechniqueFxSink s,IKitFxScene scene,float time,float q,bool low)
        {
            if(s==null||scene?.Actors==null)return;var actors=scene.Actors;
            for(int i=0;i<actors.Count;i++)
            {
                var a=actors[i];var k=NativeGlaiveView.Of(a);if(k==null)continue;long seed=a.Id*6151L;var tint=GlaiveTint(scene,a);
                GlaiveTrail(s,scene,a,k,time,tint,seed,q,low);GlaiveDisc(s,scene,a,k,time,tint,seed+7,q,low);GlaiveChipFx(s,scene,a,k,time,tint,seed+13,q,low);
                float burst=NativeGlaive.BurstRadius*(k.Shatter?NativeGlaiveChips.ShatterWide:1);
                if(k.Hits!=null)foreach(var h in k.Hits)if(h!=null)GlaiveHitFx(s,h,time,tint,seed+h.Id*37L,q,low,burst,k.Shatter);
            }
        }
        /// <summary>The cut's crescent: the fangs' sweep traced back along the keyed path, widest and brightest where it moves fastest.</summary>
        private static void GlaiveTrail(ITechniqueFxSink s,IKitFxScene scene,Actor a,NativeGlaiveKit k,float time,Vector3 tint,long seed,float q,bool low)
        {
            if(NativeGlaive.InHand(k,time)<.5f||!NativeGlaiveMotion.Sample(k,time,out var now,out var move,out float from)||now.Smear<.05f)return;
            bool view=scene.Viewer==a.Id;if(view)return;
            var feet=scene.Feet(a);float yaw=a.Yaw;int n=low?6:12;const float step=.009f;Vector3 i0=default,o0=default;float k0=0;bool has=false;
            for(int j=0;j<n;j++)
            {
                float t=time-j*step;if(!NativeGlaiveMotion.Sample(k,t,out var p,out var m,out float f)||m!=move||Math.Abs(f-from)>1e-3f)break;
                var ring=feet+NativeDogMallet.Turn(NativeGlaiveMotion.RingAt(p),yaw,0,0);var d=NativeDogMallet.Turn(NativeDogMallet.HandleAt(p),yaw,0,0);
                float fade=1-j/(float)n,kk=p.Smear*p.Weight*fade*fade;var inner=ring+d*(NativeWeaponModels.GlaiveOuter*.35f);var outer=ring+d*(NativeWeaponModels.GlaiveFang*1.02f);
                if(has&&(k0>.01f||kk>.01f))
                {
                    s.Band(false,i0,inner,outer,o0,.35f*k0,.35f*kk,tint*(1.1f+.25f*k.Keen));
                    s.Band(false,o0-(o0-i0)*.18f,outer-(outer-inner)*.18f,outer,o0,.9f*k0,.9f*kk,Vector3.Lerp(tint,GlaiveWhite,.55f)*1.6f);
                }
                i0=inner;o0=outer;k0=kk;has=true;
            }
            // The edge's spark where it is now.
            if(now.Smear>.4f){var ring=feet+NativeDogMallet.Turn(NativeGlaiveMotion.RingAt(now),yaw,0,0);var d=NativeDogMallet.Turn(NativeDogMallet.HandleAt(now),yaw,0,0);s.Sprite(false,ring+d*NativeWeaponModels.GlaiveFang,Vector3.Lerp(tint,GlaiveWhite,.6f)*(2.2f*now.Smear),.09f,Sparkle);}
        }
        /// <summary>The ring out of the hand: its spinning glow, its wake back along its flight, the motes it sheds; the halo's circle; the
        /// re-forming after a burst.</summary>
        private static void GlaiveDisc(ITechniqueFxSink s,IKitFxScene scene,Actor a,NativeGlaiveKit k,float time,Vector3 tint,long seed,float q,bool low)
        {
            var centre=scene.Feet(a)+Up*.865f;
            if(k.Disc==NativeGlaive.Gone)
            {
                // Light drawn in from round the hand into the ring as it re-forms.
                float u=(time-k.DiscAt)/NativeGlaive.Reform;if(u<0||u>1)return;var hand=centre+Up*NativeGlaive.HandHeight+Forward(a)*.35f;
                for(int i=0;i<Count(14,q,low);i++){var d=RandomDir(seed,i);float r=.9f*(1-u)*(.6f+.4f*R(seed,i+3));s.Sprite(false,hand+d*r,tint*(1.6f*u),.05f+.04f*u,Sparkle);}
                s.Glow(hand,tint*(1.4f*u*u),0,.25f+.2f*u,RingGlow,u,seed);return;
            }
            if(!NativeGlaive.DiscAt(k,time,centre,out var at))return;float age=time-k.DiscAt;float spin=time*24;
            s.Glow(at,tint*1.2f,0,.62f,RingGlow,(spin%6.283f)/6.283f,seed);s.Glow(at,Vector3.Lerp(tint,GlaiveWhite,.5f)*.9f,0,.3f,Core,.5f,seed+1);
            // Its wake: the path it just came along (the halo's circle, the flick's loop, a flight's line).
            int n=low?5:10;Vector3 prev=at;
            for(int i=1;i<=n;i++)
            {
                float t=time-i*.012f;if(t<k.DiscAt)break;Vector3 p;
                if(k.Disc==NativeGlaive.Out||k.Disc==NativeGlaive.Back){var v=NativeGlaive.N(k.DiscVel);p=at-v*(i*.012f);}else if(!NativeGlaive.DiscAt(k,t,centre,out p))break;
                float f=1-i/(float)n;var dir=p-prev;if(dir.LengthSquared()<1e-6f){prev=p;continue;}var side=Vector3.Normalize(Vector3.Cross(dir,Up).LengthSquared()>1e-6f?Vector3.Cross(dir,Up):Vector3.UnitX)*(.26f*f);
                s.Band(false,prev-side,p-side*.8f,p+side*.8f,prev+side,.45f*f,.3f*f,tint*1.2f);prev=p;
            }
            for(int i=0;i<Count(4,q,low);i++){float c=(float)((time*5+R(seed,i))%1.0);var o=RandomDir(seed,i+(int)(time*5))*(.15f+.25f*c);s.Sprite(false,at+o-Up*(.2f*c*c),tint*(1.5f*(1-c)),.04f,Sparkle);}
        }
        private static Vector3 Forward(Actor a){float y=a.Yaw*(float)Math.PI/180;return new Vector3((float)Math.Sin(y),0,(float)Math.Cos(y));}
        private static void GlaiveHitFx(ITechniqueFxSink s,NativeGlaiveHit h,float time,Vector3 tint,long seed,float q,bool low,float burst=NativeGlaive.BurstRadius,bool shatter=false)
        {
            float age=time-h.At;if(age<0||age>1.2f)return;var at=NativeGlaive.N(h.Pos);var dir=NativeGlaive.N(h.Dir);if(dir.LengthSquared()<1e-6f)dir=Up;dir=Vector3.Normalize(dir);
            var hot=Vector3.Lerp(tint,GlaiveWhite,.6f);
            switch(h.Kind)
            {
                case NativeGlaive.HitCut:case NativeGlaive.HitWhirl:case NativeGlaive.HitFlick:case NativeGlaive.HitSkim:case NativeGlaive.HitHalo:
                {
                    if(age>.35f)break;float u=age/.35f;var side=Vector3.Normalize(Vector3.Cross(dir,Up).LengthSquared()>1e-6f?Vector3.Cross(dir,Up):Vector3.UnitX);var slash=Vector3.Normalize(side+Up*.35f);
                    float len=(.55f+.25f*R(seed,1))*(1-.3f*u);s.Line(false,at-slash*len,at+slash*len,hot*(2.4f*(1-u)),.05f*(1-u)+.01f,true);
                    Flash(s,at,age,.12f,hot,.5f,seed);Sparks(s,at,age,.35f,Count(h.Kind==NativeGlaive.HitHalo?5:9,q,low),5.5f,hot,seed+3,7,.02f,dir*.6f);break;
                }
                case NativeGlaive.HitThrow:case NativeGlaive.HitRicochet:case NativeGlaive.HitReturn:
                {
                    if(age>.5f)break;Flash(s,at,age,.15f,GlaiveWhite,h.Kind==NativeGlaive.HitReturn?.45f:.75f,seed);
                    if(age<.25f)Ring(s,at,.15f+.7f*OutCubic(age/.25f),.06f*(1-age/.25f),hot*(1.8f*(1-age/.25f)),Count(18,q,low));
                    Sparks(s,at,age,.45f,Count(14,q,low),7,hot,seed+5,8,.022f,dir*.4f);break;
                }
                case NativeGlaive.HitBurst:
                {
                    // A white flash, a shockwave over the floor, a second ring in the tint, shards flung out, sparks and smoke.
                    Flash(s,at,age,.22f,GlaiveWhite,3.4f,seed);s.Glow(at,tint*(4.2f*(1-Clamp01(age/.45f))),0,1.2f+3f*OutCubic(age/.45f),RingGlow,age,seed+1);
                    if(age<.12f)s.Glow(at,(GlaiveWhite+tint)*(3*(1-age/.12f)),0,.6f+1.6f*age/.12f,Core,age,seed+2);
                    float floor=at.Y-.8f;var ground=new Vector3(at.X,floor+.04f,at.Z);
                    if(age<.6f)Ring(s,ground,.4f+burst*OutCubic(age/.42f),.22f*(1-age/.6f),hot*(2.8f*(1-age/.6f)),Count(48,q,low));
                    if(age>.04f&&age<.62f)Ring(s,at,.2f+burst*.85f*OutCubic((age-.04f)/.4f),.14f*(1-(age-.04f)/.58f),tint*(3.2f*(1-(age-.04f)/.58f)),Count(40,q,low));
                    if(age>.1f&&age<.7f)Ring(s,at+Up*.3f,.2f+burst*.55f*OutCubic((age-.1f)/.45f),.08f*(1-(age-.1f)/.6f),hot*(2*(1-(age-.1f)/.6f)),Count(28,q,low));
                    int shards=Count(22,q,low);for(int i=0;i<shards;i++){var d=Vector3.Normalize(RandomDir(seed,i*5)+Up*.25f);float r=burst*1.1f*OutCubic(age/.35f)*(.5f+.5f*R(seed,i+9));float f=1-Clamp01(age/.65f);s.Line(false,at+d*(r*.55f),at+d*r,hot*(3.2f*f),.055f*f+.01f,true);}
                    Sparks(s,at,age,.9f,Count(40,q,low),12,hot,seed+11,9,.03f,Up*.5f);Dust(s,ground,age,1.2f,Count(14,q,low),burst*.9f,new Vector3(.4f,.42f,.48f),.6f,seed+13);
                    if(shatter)GlaiveShards(s,h,age,tint,hot,seed+17,q,low);
                    break;
                }
                case NativeGlaive.HitCatch:
                    if(age<.25f){s.Glow(at,hot*(1.6f*(1-age/.25f)),0,.3f+.3f*age/.25f,RingGlow,age,seed);Sparks(s,at,age,.25f,Count(6,q,low),3,hot,seed+2,4,.015f);}
                    break;
                // ---- the chips' ----
                case NativeGlaive.HitShard:
                {
                    // A shard's nick: a short bright slash and a few sparks along its flight.
                    if(age>.3f)break;float u=age/.3f;var side=Vector3.Normalize(Vector3.Cross(dir,Up).LengthSquared()>1e-6f?Vector3.Cross(dir,Up):Vector3.UnitX);
                    s.Line(false,at-side*.3f-Up*.15f,at+side*.3f+Up*.15f,hot*(2.2f*(1-u)),.03f*(1-u)+.008f,true);Flash(s,at,age,.1f,hot,.3f,seed);Sparks(s,at,age,.3f,Count(6,q,low),5,hot,seed+3,6,.018f,dir*.8f);break;
                }
                case NativeGlaive.HitPull:
                {
                    // Dragged in: light streaming from them toward the wielder, a ring tightening round them.
                    if(age>.5f)break;float u=age/.5f;var flat=Flat(dir,Vector3.UnitZ);
                    for(int i=0;i<Count(7,q,low);i++){float o=R(seed,i)*.8f,len=.6f+1.1f*R(seed,i+5);var side=RightOf(flat)*((R(seed,i+9)-.5f)*.7f);var tail=at+side+Up*((R(seed,i+13)-.5f)*.9f)+flat*(o+2.2f*u);s.Line(false,tail,tail+flat*len,tint*(1.7f*(1-u)),.02f,false);}
                    if(age<.3f)Ring(s,at-Up*.5f,.9f*(1-OutCubic(age/.3f))+.2f,.06f,hot*(1.6f*(1-age/.3f)),Count(18,q,low));break;
                }
                case NativeGlaive.HitGuard:
                {
                    // A shot cut down by the halo: a white flash, a crescent of light across it, the pieces scattered.
                    if(age>.45f)break;Flash(s,at,age,.18f,GlaiveWhite,.7f,seed);var side=Vector3.Normalize(Vector3.Cross(dir,Up).LengthSquared()>1e-6f?Vector3.Cross(dir,Up):Vector3.UnitX);
                    if(age<.2f){float u=age/.2f;for(int i=0;i<6;i++){float a0=-1.1f+i*.37f,a1=a0+.37f;Vector3 P(float an,float r)=>at+(side*(float)Math.Cos(an)+Up*(float)Math.Sin(an))*r;s.Band(false,P(a0,.28f),P(a1,.28f),P(a1,.38f+.1f*u),P(a0,.38f+.1f*u),1-u,1-u,hot*2.4f);}}
                    Sparks(s,at,age,.4f,Count(12,q,low),6,hot,seed+5,7,.02f,dir*.5f);break;
                }
                case NativeGlaive.HitBleed:
                {
                    // Keen Edge's bleed: a few dark red drops falling off them, a thin glint of the edge's colour.
                    if(age>.55f)break;var blood=new Vector3(.62f,.03f,.06f);
                    for(int i=0;i<Count(4,q,low);i++){float t=age*(1.1f+.4f*R(seed,i));var o=new Vector3((R(seed,i+3)-.5f)*.35f,0,(R(seed,i+6)-.5f)*.35f);var p=at+o-Up*(4.5f*t*t);s.Drop(p,-Up*(.05f+.12f*t),blood,.9f*(1-age/.55f),.032f,age/.55f,seed+i);}
                    if(age<.12f)s.Sprite(false,at+Up*.2f,tint*(1.4f*(1-age/.12f)),.05f,Sparkle);break;
                }
            }
        }
        /// <summary>Shatterpoint's shards out of the burst (Core NativeGlaiveChips.ShardDir, the same the rules fly): bright splinters spinning
        /// out ShardRange m, their streaks behind them, a glint where each stops, then fading.</summary>
        private static void GlaiveShards(ITechniqueFxSink s,NativeGlaiveHit h,float age,Vector3 tint,Vector3 hot,long seed,float q,bool low)
        {
            float fly=NativeGlaiveChips.ShardRange/NativeGlaiveChips.ShardSpeed;if(age<0||age>fly+.45f)return;var at=NativeGlaive.N(h.Pos);
            for(int i=0;i<NativeGlaiveChips.Shards;i++)
            {
                var d=NativeGlaiveChips.ShardDir(h.Id,i);float r=NativeGlaiveChips.ShardTravel(age),fade=age<fly?1:1-(age-fly)/.45f;var head=at+d*r;var side=RightOf(d);
                float spin=age*40+i*1.3f;var tw=(d*(float)Math.Cos(spin)+Up*(float)Math.Sin(spin))*.16f;
                s.Line(false,head-tw,head+tw,hot*(3*fade),.045f*fade+.01f,true);s.Line(false,head-side*.06f,head+side*.06f,hot*(2*fade),.03f*fade,true);
                if(age<fly)s.Streak(head,d*NativeGlaiveChips.ShardSpeed,tint*1.6f,.035f,.5f,.04f,.8f,age/fly,seed+i);
                else s.Sprite(false,head,hot*(2.2f*fade),.08f*fade+.02f,Sparkle);
                if(!low&&age<fly)s.Sprite(false,head-d*(.3f+.2f*R(seed,i)),tint*1.2f,.04f,Sparkle);
            }
        }
        /// <summary>The chips' standing moments on a Glaive: Twin Orbit's second ring of light, Crescent Guard's shimmer while the ring is out,
        /// Quick Catch's rush, Skimming Stone's slick on the floor.</summary>
        private static void GlaiveChipFx(ITechniqueFxSink s,IKitFxScene scene,Actor a,NativeGlaiveKit k,float time,Vector3 tint,long seed,float q,bool low)
        {
            var feet=scene.Feet(a);var centre=feet+Up*.865f;var hot=Vector3.Lerp(tint,GlaiveWhite,.55f);
            if(k.Twin&&k.Disc==NativeGlaive.Orbit&&time>=k.DiscAt)
            {
                // A ring of light the other way round: its spin, its glow and its wake.
                var at=NativeGlaiveChips.TwinAt(centre,NativeGlaive.N(k.DiscDir),k.DiscAt,time);float spin=-time*20,turn=spin%6.283f;if(turn<0)turn+=6.283f;
                s.Glow(at,tint*1.1f,0,.55f,RingGlow,turn/6.283f,seed);s.Glow(at,hot*.8f,0,.26f,Core,.5f,seed+1);
                int seg=low?8:14;for(int i=0;i<seg;i++){float a0=spin+i*6.2831853f/seg,a1=a0+6.2831853f/seg*.6f;var p0=at+new Vector3((float)Math.Cos(a0),0,(float)Math.Sin(a0))*.3f;var p1=at+new Vector3((float)Math.Cos(a1),0,(float)Math.Sin(a1))*.3f;s.Line(false,p0,p1,hot*1.6f,.025f,false);}
                int n=low?5:10;var prev=at;
                for(int i=1;i<=n;i++)
                {
                    float t=time-i*.012f;if(t<k.DiscAt)break;var p=NativeGlaiveChips.TwinAt(centre,NativeGlaive.N(k.DiscDir),k.DiscAt,t);float f=1-i/(float)n;var dd=p-prev;if(dd.LengthSquared()<1e-6f){prev=p;continue;}
                    var side=Vector3.Normalize(Vector3.Cross(dd,Up).LengthSquared()>1e-6f?Vector3.Cross(dd,Up):Vector3.UnitX)*(.22f*f);s.Band(false,prev-side,p-side*.8f,p+side*.8f,prev+side,.4f*f,.25f*f,tint*1.1f);prev=p;
                }
            }
            bool outNow=k.Disc==NativeGlaive.Out||k.Disc==NativeGlaive.Back||k.Disc==NativeGlaive.Orbit||k.Disc==NativeGlaive.Flick;
            if(k.Guard&&outNow)
            {
                // Three faint crescents turning round the wielder's middle, a glint running along each.
                float on=Clamp01((time-k.DiscAt)/.2f);
                for(int c=0;c<3;c++)
                {
                    float baseA=time*1.6f+c*2.094f;int seg=low?5:9;int cc=c;
                    Vector3 P(float an,float r)=>centre+new Vector3((float)Math.Cos(an)*r,.05f*(float)Math.Sin(an*3+cc),(float)Math.Sin(an)*r);
                    for(int i=0;i<seg;i++){float u0=i/(float)seg,u1=(i+1f)/seg;float a0=baseA+u0*1.5f,a1=baseA+u1*1.5f;float w0=(float)Math.Sin(Math.PI*u0),w1=(float)Math.Sin(Math.PI*u1);s.Band(false,P(a0,.78f),P(a1,.78f),P(a1,.78f+.07f*w1),P(a0,.78f+.07f*w0),.35f*on*w0,.35f*on*w1,tint*1.2f);}
                    if(!low){float g=(time*2.2f+c*.33f)%1;s.Sprite(false,P(baseA+g*1.5f,.82f),hot*(1.2f*on),.05f,Sparkle);}
                }
            }
            if(time<k.QuickUntil&&time>=k.QuickUntil-NativeGlaiveChips.QuickFor)
            {
                // A rush of speed lines streaming off behind, in the edge's colour, and motes kicked up at the feet.
                float left=(k.QuickUntil-time)/NativeGlaiveChips.QuickFor;var back=-Forward(a);var side=RightOf(Forward(a));
                for(int i=0;i<Count(7,q,low);i++){float ph=(time*3.4f+R(seed,i))%1;var o=feet+Up*(.25f+1.4f*R(seed,i+3))+side*((R(seed,i+5)-.5f)*.9f)+back*(.2f+1.2f*ph);s.Line(false,o,o+back*(.5f+.4f*R(seed,i+7)),tint*(1.5f*left*(1-ph)),.018f,false);}
                for(int i=0;i<Count(4,q,low);i++){float ph=(time*2.6f+R(seed,i+11))%1;s.Sprite(false,feet+back*(.3f+.6f*ph)+side*((R(seed,i+13)-.5f)*.6f)+Up*(.05f+.3f*ph),hot*(1.3f*left*(1-ph)),.04f,Sparkle);}
            }
            if(NativeGlaiveChips.SlickOn(k.SlickAt,time))
            {
                // A slick of light along the skim's path on the floor: laid as it goes, shimmering, rippling, fading at the end.
                var from=NativeGlaive.N(k.SlickFrom)+Up*.03f;var to=NativeGlaive.N(k.SlickTo)+Up*.03f;float laid=Clamp01((time-k.SlickAt)/NativeGlaive.SkimTime);var end=Vector3.Lerp(from,to,laid);var dd=end-from;
                if(dd.LengthSquared()>1e-4f)
                {
                    float total=NativeGlaive.SkimTime+NativeGlaiveChips.SlickTime,fade=Clamp01((k.SlickAt+total-time)/.6f);var d=Vector3.Normalize(dd);var side=RightOf(Flat(d,Vector3.UnitZ))*NativeGlaiveChips.SlickWidth;int seg=low?5:10;
                    for(int i=0;i<seg;i++)
                    {
                        float u0=i/(float)seg,u1=(i+1f)/seg;var p0=Vector3.Lerp(from,end,u0);var p1=Vector3.Lerp(from,end,u1);float sh0=.55f+.25f*(float)Math.Sin(time*6-u0*9),sh1=.55f+.25f*(float)Math.Sin(time*6-u1*9);
                        s.Band(false,p0-side*.85f,p1-side*.85f,p1+side*.85f,p0+side*.85f,.3f*fade*sh0,.3f*fade*sh1,tint*.9f);s.Band(false,p0-side*.18f,p1-side*.18f,p1+side*.18f,p0+side*.18f,.5f*fade*sh0,.5f*fade*sh1,hot*1.3f);
                    }
                    for(int i=0;i<Count(5,q,low);i++){float ph=(time*.9f+R(seed,i+20))%1;var c=Vector3.Lerp(from,end,R(seed,i+21))+side*((R(seed,i+22)-.5f)*1.2f);Ring(s,c,.15f+.6f*ph,.03f,tint*(1.2f*fade*(1-ph)),low?10:16);}
                    for(int i=0;i<Count(6,q,low);i++){float ph=(time*1.7f+R(seed,i+30))%1;var c=Vector3.Lerp(from,end,R(seed,i+31))+side*((R(seed,i+32)-.5f)*1.4f)+Up*(.05f+.25f*ph);s.Sprite(false,c,hot*(1.4f*fade*(float)Math.Sin(Math.PI*ph)),.04f,Sparkle);}
                }
            }
        }
    }
}
