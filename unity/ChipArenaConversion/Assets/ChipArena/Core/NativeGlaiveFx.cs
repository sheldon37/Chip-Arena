using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // ---- the Glaive's effects (docs/GLAIVE.md "The look"; owner 2026-11-09: "with great quality effects") ----
    // In the pilot's weapon colour: a crescent of light along the ring's outer edge through every fast part of a cut (from the keyed moves
    // on the drawn feet and facing), the thrown ring's spinning glow, its wake and the motes it sheds, a sharp ping and sparks at each cut
    // and ricochet, the halo's circling trail, the burst (a white flash, a shockwave over the floor, shards and sparks flung out), the ring
    // re-forming out of converging light, and a flash as it is caught. Pure: the same record and clock draw the same.
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
                GlaiveTrail(s,scene,a,k,time,tint,seed,q,low);GlaiveDisc(s,scene,a,k,time,tint,seed+7,q,low);
                if(k.Hits!=null)foreach(var h in k.Hits)if(h!=null)GlaiveHitFx(s,h,time,tint,seed+h.Id*37L,q,low);
            }
        }
        /// <summary>The cut's crescent: the ring's outer edge traced back along its keyed path, widest and brightest where it moves fastest.</summary>
        private static void GlaiveTrail(ITechniqueFxSink s,IKitFxScene scene,Actor a,NativeGlaiveKit k,float time,Vector3 tint,long seed,float q,bool low)
        {
            if(NativeGlaive.InHand(k,time)<.5f||!NativeGlaiveMotion.Sample(k,time,out var now,out var move,out float from)||now.Smear<.05f)return;
            bool view=scene.Viewer==a.Id;if(view)return;
            var feet=scene.Feet(a);float yaw=a.Yaw;int n=low?6:12;const float step=.009f;Vector3 i0=default,o0=default;float k0=0;bool has=false;
            for(int j=0;j<n;j++)
            {
                float t=time-j*step;if(!NativeGlaiveMotion.Sample(k,t,out var p,out var m,out float f)||m!=move||Math.Abs(f-from)>1e-3f)break;
                var ring=feet+NativeDogMallet.Turn(NativeGlaiveMotion.RingAt(p),yaw,0,0);var d=NativeDogMallet.Turn(NativeDogMallet.HandleAt(p),yaw,0,0);
                float fade=1-j/(float)n,kk=p.Smear*p.Weight*fade*fade;var inner=ring+d*(NativeWeaponModels.GlaiveOuter*.25f);var outer=ring+d*(NativeWeaponModels.GlaiveOuter*1.05f);
                if(has&&(k0>.01f||kk>.01f))
                {
                    s.Band(false,i0,inner,outer,o0,.35f*k0,.35f*kk,tint*1.1f);
                    s.Band(false,o0-(o0-i0)*.18f,outer-(outer-inner)*.18f,outer,o0,.9f*k0,.9f*kk,Vector3.Lerp(tint,GlaiveWhite,.55f)*1.6f);
                }
                i0=inner;o0=outer;k0=kk;has=true;
            }
            // The edge's spark where it is now.
            if(now.Smear>.4f){var ring=feet+NativeDogMallet.Turn(NativeGlaiveMotion.RingAt(now),yaw,0,0);var d=NativeDogMallet.Turn(NativeDogMallet.HandleAt(now),yaw,0,0);s.Sprite(false,ring+d*NativeWeaponModels.GlaiveOuter,Vector3.Lerp(tint,GlaiveWhite,.6f)*(2.2f*now.Smear),.09f,Sparkle);}
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
        private static void GlaiveHitFx(ITechniqueFxSink s,NativeGlaiveHit h,float time,Vector3 tint,long seed,float q,bool low)
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
                    Flash(s,at,age,.2f,GlaiveWhite,2.2f,seed);s.Glow(at,tint*(2.4f*(1-Clamp01(age/.4f))),0,1+2.4f*OutCubic(age/.4f),RingGlow,age,seed+1);
                    float floor=at.Y-.8f;var ground=new Vector3(at.X,floor+.04f,at.Z);
                    if(age<.55f)Ring(s,ground,.4f+NativeGlaive.BurstRadius*OutCubic(age/.4f),.12f*(1-age/.55f),hot*(1.6f*(1-age/.55f)),Count(40,q,low));
                    if(age>.05f&&age<.6f)Ring(s,at,.2f+NativeGlaive.BurstRadius*.8f*OutCubic((age-.05f)/.4f),.08f*(1-(age-.05f)/.55f),tint*(2*(1-(age-.05f)/.55f)),Count(32,q,low));
                    int shards=Count(14,q,low);for(int i=0;i<shards;i++){var d=Vector3.Normalize(RandomDir(seed,i*5)+Up*.25f);float r=NativeGlaive.BurstRadius*OutCubic(age/.35f)*(.5f+.5f*R(seed,i+9));float f=1-Clamp01(age/.6f);s.Line(false,at+d*(r*.6f),at+d*r,hot*(2.2f*f),.035f*f+.008f,true);}
                    Sparks(s,at,age,.8f,Count(24,q,low),11,hot,seed+11,9,.025f,Up*.5f);Dust(s,ground,age,1.1f,Count(10,q,low),NativeGlaive.BurstRadius*.8f,new Vector3(.4f,.42f,.48f),.5f,seed+13);
                    break;
                }
                case NativeGlaive.HitCatch:
                    if(age<.25f){s.Glow(at,hot*(1.6f*(1-age/.25f)),0,.3f+.3f*age/.25f,RingGlow,age,seed);Sparks(s,at,age,.25f,Count(6,q,low),3,hot,seed+2,4,.015f);}
                    break;
            }
        }
    }
}
