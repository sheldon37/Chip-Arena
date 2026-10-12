using System;
using System.Numerics;
namespace ChipArena.UnityConversion
{
    // The eleven weapon signatures, re-staged around their baked choreography (the hit, footwork and name card unchanged).
    // Each camera plan: an establishing move, a push-in or orbit on the weapon, a low hero frame on the anticipation, a cut or
    // whip on the hit into a slow-motion beat, a dolly out to the final pose and a still hero frame under the name card.

    /// <summary>Flash Draw (sword, hit 1.8 s; pack motion, Core/NativeEntrancePack): a calm stand, the blade coiled back across
    /// the body, a backhand draw-cut through a crescent of light, an overhead flourish and the blade raised in salute.</summary>
    internal sealed class FlashDraw:NativeWeaponEntrance
    {
        public FlashDraw():base("weapon-sword",1.8f,0xb6fff0)
        {
            Shots=new[]
            {
                // Establishing: a crane coming down and in on the walk-in, the rim light carving the silhouette as the pylons wake.
                Orbit(0,NativeEntranceCut.Cut,40,37,-40,-25,4.2f,3.3f,1.3f,.75f,V(0,.95f,0),V(0,.9f,0),0,1,face:.5f),
                // The coil: a medium shot on the sword side, following the chest as the blade is drawn back across the body.
                Follow(Shot(.95f,NativeEntranceCut.Cut,37,34,V(-2.7f,.95f,2.9f),V(-2.4f,.95f,2.6f),V(0,.1f,0),V(0,.05f,0),1,1,face:1.2f),NativeEntranceAnchor.Chest),
                // Anticipation: low on the sword side, looking up at the coiled blade as the pilot sinks into the draw.
                Shot(1.42f,NativeEntranceCut.Cut,42,39,V(-1.7f,.3f,2.7f),V(-1.5f,.3f,2.4f),V(0,.95f,0),V(0,.9f,0),1,1,face:1.6f),
                // The cut, in slow motion: an orbit round the front that lets the crescent hang in the air.
                Orbit(1.8f,NativeEntranceCut.Cut,40,36,-38,-8,3.3f,2.9f,1.0f,.85f,V(0,.95f,.15f),V(0,.95f,.1f),1,1,face:1.8f),
                // Dolly out and crane up to the flourish.
                Orbit(2.7f,NativeEntranceCut.Blend,36,38,-28,-18,2.9f,3.5f,1.0f,1.3f,V(0,1.0f,0),V(0,1.02f,0),0,1,.3f,face:1.8f),
                // Hero: low three-quarter, drifting in, still before the hand-off.
                Shot(3.2f,NativeEntranceCut.Cut,36,34,V(.95f,.38f,3.7f),V(.6f,.42f,3.35f),V(0,.98f,0),V(0,1.0f,0),1,0),
            };
        }
        public override void Effects(float t,INativeEntranceFx fx,float q)
        {
            Motes(fx,t,q,Accent);Beams(fx,t,q,Accent);var white=Vector3.One;var tip=Anchor(NativeEntranceAnchor.RightTip,t);var hand=Anchor(NativeEntranceAnchor.RightHand,t);
            // Motes drawn onto the edge before the cut.
            for(int i=0;i<18;i++)
            {
                float born=Hit-.55f+i*.022f,a=t-born;if(a<0||a>.25f||NativeEntranceMath.Rand(31,i)>q)continue;var dir=NativeEntranceMath.Direction(31,i);
                var at=Anchor(NativeEntranceAnchor.RightTip,born+.25f);var p=at+dir*(.45f*(1-a/.25f));fx.Glow(p,Accent*2.2f*(a/.25f),0,.05f,0,a/.25f,i);
            }
            // The draw flash: a glint running from the hilt to the tip, then a star on the tip.
            float run=(t-(Hit-.1f))/.1f;if(run>0&&run<1){var p=Vector3.Lerp(hand,tip,run);fx.Glow(p,white*4,0,.22f,1,0,7);fx.Glow(p,Accent*3,0,.5f,0,0,8);}
            float star=NativeEntranceMath.Pulse(t,Hit,.02f,.18f);if(star>.02f)NativeEntranceFx.Glint(fx,tip,white*(3*star),.35f*star+.1f,Vector3.UnitX,Vector3.UnitY,9);
            // The blade's ribbon through the cut and the flourish.
            Trail(fx,NativeEntranceAnchor.RightTip,t,Hit-.16f,Hit+.38f,.16f,white*3.5f,Accent*2,.05f,40);
            // The crescent: an arc of light swept across the front of the pilot, hanging through the slow motion.
            var pelvis=Anchor(NativeEntranceAnchor.Pelvis,Hit);var fwd=Vector3.Normalize(new Vector3(tip.X-pelvis.X,0,tip.Z-pelvis.Z)+new Vector3(0,0,.001f));
            float c=t-Hit;if(c>-.02f&&c<.65f)
            {
                float yaw=(float)Math.Atan2(Anchor(NativeEntranceAnchor.RightTip,Hit).X-pelvis.X,Anchor(NativeEntranceAnchor.RightTip,Hit).Z-pelvis.Z);
                int n=28;float grow=NativeEntranceMath.Clamp01((c+.02f)/.09f),fade=1-NativeEntranceMath.Ramp(c,.12f,.65f);
                for(int i=1;i<=n;i++)
                {
                    float u=i/(float)n;if(u>grow)break;float a=yaw+1.25f-2.5f*u,a0=yaw+1.25f-2.5f*(i-1)/n,R=1.05f+.1f*(float)Math.Sin(Math.PI*u)+c*.6f,y=.95f+.25f*(u-.5f);
                    var p=new Vector3(pelvis.X+(float)Math.Sin(a)*R,y,pelvis.Z+(float)Math.Cos(a)*R);var p0=new Vector3(pelvis.X+(float)Math.Sin(a0)*R,.95f+.25f*((i-1f)/n-.5f),pelvis.Z+(float)Math.Cos(a0)*R);
                    float thick=(float)Math.Sin(Math.PI*u);fx.Streak(p,p-p0,Vector3.Lerp(white*4,Accent*2.5f,u)*(fade*thick),.03f+.06f*thick,0,1,0,c,60+i);
                    if(i%3==0)fx.Glow(p,Accent*(1.2f*fade*thick),0,.28f,0,c,90+i);
                }
            }
            NativeEntranceFx.Sparks(fx,t,Hit,Anchor(NativeEntranceAnchor.RightTip,Hit),fwd,.9f,26,4.2f,.6f,9.8f,1.2f,white*3,Accent*2,.02f,120,q,NativeEntranceStage.FloorY);
            // The planted step kicks dust.
            NativeEntranceFx.GroundDust(fx,t,Hit+.06f,new Vector3(pelvis.X,0,pelvis.Z),14,.25f,1.1f,1.4f,NativeEntranceFx.Hex(0x7d8496),.5f,.32f,150,q,NativeEntranceStage.FloorY);
        }
        protected override void Plan()=>Blasts=new[]{new NativeEntranceBlast{At=Hit,Impact=true,Position=Anchor(NativeEntranceAnchor.RightTip,Hit),Radius=.5f,ImpactColor=WeaponColor}};
    }

    /// <summary>Twin Phantom (daggers, hit 2.12 s): a tucked spin trailing phantom afterimages, blades crossed overhead, an X-cut.</summary>
    internal sealed class TwinPhantom:NativeWeaponEntrance
    {
        public TwinPhantom():base("weapon-daggers",2.12f,0x87ffd5)
        {
            Shots=new[]
            {
                // Establishing: a high crane settling down on the crouched pilot as the stage wakes.
                Orbit(0,NativeEntranceCut.Cut,38,36,28,18,4.2f,3.4f,2.6f,1.7f,V(0,.6f,0),V(0,.8f,0),0,1,face:.2f),
                // The spin: whipped into an orbit against the spin, the phantoms smearing round the pilot.
                Orbit(.78f,NativeEntranceCut.Whip,42,42,-45,65,3.6f,3.2f,.9f,1.0f,V(0,.95f,0),V(0,1.0f,0),3,1,.18f,face:.9f),
                // Anticipation: low in front, the blades crossed high overhead.
                Shot(1.62f,NativeEntranceCut.Cut,44,40,V(.6f,.32f,3.0f),V(.5f,.3f,2.7f),V(0,1.3f,0),V(0,1.2f,0),1,1,face:1.77f),
                // The X-cut: whipped to the front, pushing in through the slow motion as the cuts hang in the air.
                Orbit(2.12f,NativeEntranceCut.Whip,38,34,-22,6,2.8f,2.4f,1.05f,1.0f,V(0,1.0f,.3f),V(0,1.0f,.25f),1,1,.14f,face:2.12f),
                Orbit(2.8f,NativeEntranceCut.Blend,36,38,6,-4,2.5f,3.1f,1.0f,1.15f,V(0,1.0f,.22f),V(0,1.0f,0),0,1,.3f,face:2.12f),
                Shot(3.2f,NativeEntranceCut.Cut,36,34,V(.8f,.4f,3.65f),V(.5f,.42f,3.3f),V(0,.98f,0),V(0,1.0f,0),1,0),
            };
        }
        public override int Ghosts(float t,Span<NativeEntranceGhost> into)
        {
            float live=NativeEntranceMath.Ramp(t,.3f,.6f)*(1-NativeEntranceMath.Ramp(t,2.4f,2.8f));if(live<=.01f)return 0;
            float fast=Math.Max(Speed(NativeEntranceAnchor.RightTip,t,6),Speed(NativeEntranceAnchor.Head,t,2.2f));live*=.25f+.75f*fast;
            for(int i=0;i<3;i++)into[i]=new NativeEntranceGhost{Lag=.055f*(i+1),Opacity=(.1f-.028f*i)*live,Color=Accent*1.6f};return 3;
        }
        public override void Effects(float t,INativeEntranceFx fx,float q)
        {
            Motes(fx,t,q,Accent);Beams(fx,t,q,Accent);var white=Vector3.One;
            Trail(fx,NativeEntranceAnchor.RightTip,t,.9f,Hit+.3f,.12f,white*2.5f,Accent*1.6f,.035f,10);Trail(fx,NativeEntranceAnchor.LeftTip,t,.9f,Hit+.3f,.12f,white*2.5f,Accent*1.6f,.035f,30);
            // A glint on each crossed blade at the top of the wind-up.
            float g=NativeEntranceMath.Pulse(t,Hit-.36f,.03f,.15f);if(g>.02f){NativeEntranceFx.Glint(fx,Anchor(NativeEntranceAnchor.RightTip,t),white*(2.5f*g),.3f*g+.06f,Vector3.UnitX,Vector3.UnitY,50);NativeEntranceFx.Glint(fx,Anchor(NativeEntranceAnchor.LeftTip,t),white*(2.5f*g),.3f*g+.06f,Vector3.UnitY,Vector3.UnitX,52);}
            // The X: two cuts drawn across the chest at the hit, hanging through the slow motion.
            float c=t-Hit;var pelvis=Anchor(NativeEntranceAnchor.Pelvis,Hit);var fwd=ForwardOf(Hit);var right=RightOf(Hit);var centre=pelvis+fwd*.55f+new Vector3(0,.42f,0);
            if(c>-.01f&&c<.7f)
            {
                float fade=1-NativeEntranceMath.Ramp(c,.15f,.7f);
                for(int k=0;k<2;k++)
                {
                    float grow=NativeEntranceMath.Clamp01((c-.04f*k)/.07f);if(grow<=0)continue;float sgn=k==0?1:-1;var a=centre+right*(.5f*sgn)+new Vector3(0,.5f,0);var b=centre-right*(.5f*sgn)-new Vector3(0,.5f,0);
                    for(int i=1;i<=12;i++){float u=i/12f;if(u>grow)break;var p=Vector3.Lerp(a,b,u);var p0=Vector3.Lerp(a,b,(i-1)/12f);float thick=(float)Math.Sin(Math.PI*u);fx.Streak(p,p-p0,Vector3.Lerp(white*2.4f,Accent*1.8f,u)*(fade*thick),.018f+.032f*thick,0,1,0,c,70+k*20+i);}
                }
            }
            NativeEntranceFx.Sparks(fx,t,Hit,centre,fwd,1.1f,30,3.6f,.55f,9.8f,1.4f,white*3,Accent*2,.018f,90,q,NativeEntranceStage.FloorY);
            // Phantom mist swirling low round the spin, and the hop's landing dust.
            for(int i=0;i<14;i++)
            {
                if(NativeEntranceMath.Rand(140,i)>q)continue;float born=.5f+i*.08f,a=t-born;if(a<0||a>1.2f)continue;float u=a/1.2f,ang=i*1.9f-a*3.5f;var root=Anchor(NativeEntranceAnchor.Root,born);
                fx.Smoke(new Vector3(root.X+(float)Math.Cos(ang)*(.55f+.5f*u),NativeEntranceStage.FloorY+.12f+.25f*u,root.Z+(float)Math.Sin(ang)*(.55f+.5f*u)),NativeEntranceFx.Hex(0x2f6e64),.45f*NativeEntranceMath.Smooth(u*5)*(1-u),.45f+.5f*u,u,140+i,NativeEntranceStage.FloorY);
            }
            NativeEntranceFx.GroundDust(fx,t,1.44f,new Vector3(pelvis.X,0,pelvis.Z),12,.2f,.9f,1.1f,NativeEntranceFx.Hex(0x7d8496),.45f,.28f,160,q,NativeEntranceStage.FloorY);
        }
        protected override NativeEntranceAnchor HitAnchor=>NativeEntranceAnchor.Chest;
        protected override void Plan(){var p=Anchor(NativeEntranceAnchor.Pelvis,Hit)+ForwardOf(Hit)*.55f+new Vector3(0,.42f,0);Blasts=new[]{new NativeEntranceBlast{At=Hit,Impact=true,Position=p,Radius=.6f,ImpactColor=WeaponColor}};}
    }

    /// <summary>Bastion Drop (shield, hit 1.45 s): a hop with the shield raised, the rim driven into the floor, a honeycomb barrier rippling out.</summary>
    internal sealed class BastionDrop:NativeWeaponEntrance
    {
        public BastionDrop():base("weapon-shield",1.45f,0x8cceff)
        {
            Shots=new[]
            {
                Orbit(0,NativeEntranceCut.Cut,40,38,-28,-14,4.3f,3.4f,.6f,.7f,V(0,.9f,0),V(0,.95f,0),0,1,face:.2f),
                // The coil and hop, from above and pushing in.
                Shot(.6f,NativeEntranceCut.Cut,38,36,V(1.2f,2.3f,2.4f),V(1.0f,2.0f,2.0f),V(0,.9f,.2f),V(0,1.05f,.2f),1,1,face:.9f),
                // Anticipation: on the floor in front of where the rim will land.
                Shot(1.15f,NativeEntranceCut.Cut,44,42,V(-1.1f,.26f,3.0f),V(-.95f,.24f,2.7f),V(0,.9f,.3f),V(0,.7f,.3f),1,1,face:1.3f),
                // The slam, in slow motion: wide and a little high, the barrier rippling toward the lens.
                Orbit(1.45f,NativeEntranceCut.Cut,42,40,6,-24,3.8f,3.3f,1.6f,1.35f,V(0,.65f,.6f),V(0,.75f,.5f),1,1,face:1.45f),
                Orbit(2.25f,NativeEntranceCut.Blend,40,38,-24,-10,3.3f,3.8f,1.2f,.75f,V(0,.95f,0),V(0,1.0f,0),0,1,.3f,face:1.45f),
                Shot(3.2f,NativeEntranceCut.Cut,36,34,V(-.85f,.38f,3.6f),V(-.55f,.42f,3.3f),V(0,.95f,0),V(0,.98f,0),1,0),
            };
        }
        protected override NativeEntranceAnchor HitAnchor=>NativeEntranceAnchor.RightTip;
        public override void Effects(float t,INativeEntranceFx fx,float q)
        {
            Motes(fx,t,q,Accent);Beams(fx,t,q,Accent);var white=Vector3.One;
            var pelvis=Anchor(NativeEntranceAnchor.Pelvis,Hit);var fwd=ForwardOf(Hit);var right=RightOf(Hit);
            // The rim catches the light at the top of the hop.
            float g=NativeEntranceMath.Pulse(t,1.18f,.04f,.16f);if(g>.02f)NativeEntranceFx.Glint(fx,Anchor(NativeEntranceAnchor.RightTip,t)+new Vector3(0,.3f,0),white*(2.4f*g),.3f*g+.05f,Vector3.UnitX,Vector3.UnitY,30);
            // The honeycomb: a hexagonal barrier of cells rippling out from the impact, the rim brighter, standing in front of the pilot.
            float c=t-Hit;
            if(c>-.01f&&c<1.3f)
            {
                var centre=pelvis+fwd*.6f;float cell=q<.5f?.3f:.2f,edge=1.15f,sector=(float)Math.PI/3;var tone=NativeEntranceFx.Hex(0xbfe4ff);
                for(int a=-8;a<=8;a++)for(int b=-8;b<=8;b++)
                {
                    float u=(a+b*.5f)*cell,v=b*cell*.866f,d=(float)Math.Sqrt(u*u+v*v),ang=(float)Math.Atan2(v,u),wrap=((ang%sector)+sector)%sector,limit=edge*(float)Math.Cos(Math.PI/6)/(float)Math.Cos(wrap-Math.PI/6);if(d>limit)continue;
                    float local=c-d*.22f;if(local<0||local>.9f)continue;bool rim=d>limit-cell;float k=NativeEntranceMath.Smooth(local/.05f)*(1-NativeEntranceMath.Ramp(local,.25f,.9f));
                    var p=new Vector3(centre.X+right.X*u,NativeEntranceStage.FloorY+.95f+v,centre.Z+right.Z*u)+fwd*(local*.35f);
                    if(!rim)fx.Glow(p,tone*(1.1f*k),0,.075f,0,local,200+a*20+b);
                }
                // The barrier's hexagonal rim, drawn as bright edges once the ripple reaches it.
                float rl=c-edge*.22f;if(rl>0&&rl<.9f)
                {
                    float k=NativeEntranceMath.Smooth(rl/.05f)*(1-NativeEntranceMath.Ramp(rl,.25f,.9f));
                    var drift=fwd*(rl*.35f);
                    for(int e=0;e<6;e++)
                    {
                        float a0=e*sector,a1=(e+1)*sector;
                        var c0=new Vector3(centre.X+right.X*(float)Math.Cos(a0)*edge,NativeEntranceStage.FloorY+.95f+(float)Math.Sin(a0)*edge,centre.Z+right.Z*(float)Math.Cos(a0)*edge)+drift;
                        var c1=new Vector3(centre.X+right.X*(float)Math.Cos(a1)*edge,NativeEntranceStage.FloorY+.95f+(float)Math.Sin(a1)*edge,centre.Z+right.Z*(float)Math.Cos(a1)*edge)+drift;
                        for(int j=0;j<3;j++){var from=Vector3.Lerp(c0,c1,j/3f);var to=Vector3.Lerp(c0,c1,(j+1)/3f);fx.Streak(to,to-from,Vector3.Lerp(white*2.2f,tone*1.6f,j/3f)*k,.03f,0,1,.1f,rl,280+e*3+j);}
                    }
                }
            }
            NativeEntranceFx.Sparks(fx,t,Hit,Anchor(NativeEntranceAnchor.RightTip,Hit),new Vector3(0,.6f,0)+fwd*.5f,1.0f,28,3.4f,.6f,9.8f,1.2f,white*3,Accent*2,.02f,300,q,NativeEntranceStage.FloorY);
            NativeEntranceFx.GroundDust(fx,t,Hit,new Vector3(pelvis.X,0,pelvis.Z)+fwd*.4f,22,.3f,1.9f,1.7f,NativeEntranceFx.Hex(0x80889a),.55f,.42f,330,q,NativeEntranceStage.FloorY);
            NativeEntranceFx.GroundDust(fx,t,1.0f,new Vector3(pelvis.X,0,pelvis.Z),10,.2f,.7f,1.0f,NativeEntranceFx.Hex(0x7d8496),.35f,.26f,360,q,NativeEntranceStage.FloorY);
        }
        protected override void Plan()
        {
            var p=Anchor(NativeEntranceAnchor.Pelvis,Hit)+ForwardOf(Hit)*.4f;p.Y=NativeEntranceStage.FloorY+.95f;
            Blasts=new[]{new NativeEntranceBlast{At=Hit,Style=NativeBlastStyle.Kinetic,Position=p,Radius=1.4f,Size=.45f,Tint=new NativeBlastTint(0xffffff,0xbfe4ff,0x5aa6ff)}};
        }
    }

    /// <summary>Colossus Oath (greatsword, hit 1.8 s; pack motion, Core/NativeEntrancePack): the blade held up in oath, a crouch,
    /// a leap with the blade raised overhead and a two-handed slam into the floor, the ground cracking open and the tip scraping
    /// sparks, then the blade shouldered.</summary>
    internal sealed class ColossusOath:NativeWeaponEntrance
    {
        public ColossusOath():base("weapon-greatsword",1.8f,0xffcb87)
        {
            SlowRate=.25f;
            Shots=new[]
            {
                // The oath: low and close in front, looking up the raised blade as the pylons wake.
                Orbit(0,NativeEntranceCut.Cut,42,39,32,18,3.7f,3.1f,.45f,.5f,V(0,1.35f,0),V(0,1.25f,0),0,1,face:.3f),
                // The crouch: a push-in from the front, a little low.
                Shot(.9f,NativeEntranceCut.Cut,40,38,V(.5f,.55f,3.6f),V(.4f,.58f,3.2f),V(0,1.15f,0),V(0,1.3f,0),1,.6f,face:1.2f),
                // The leap: on the floor at the off side, looking up at the blade raised against the rim light.
                Orbit(1.45f,NativeEntranceCut.Cut,48,46,70,56,3.5f,3.2f,.3f,.28f,V(0,1.75f,0),V(0,1.45f,0),1,.3f,face:1.6f),
                // The slam, in slow motion, from high above: the floor splitting open round the blade.
                Shot(1.8f,NativeEntranceCut.Cut,44,40,V(1.2f,3.6f,3.0f),V(1.0f,3.2f,2.6f),V(0,.3f,.5f),V(0,.4f,.4f),1,1,face:1.8f),
                Orbit(2.6f,NativeEntranceCut.Blend,40,38,21,8,2.9f,3.6f,3.0f,.85f,V(0,.45f,.4f),V(0,1.0f,0),0,1,.3f,face:1.8f),
                Shot(3.2f,NativeEntranceCut.Cut,38,35,V(.9f,.32f,3.9f),V(.6f,.36f,3.6f),V(0,1.12f,0),V(0,1.15f,0),1,0),
            };
        }
        public override void Effects(float t,INativeEntranceFx fx,float q)
        {
            Motes(fx,t,q,Accent);Beams(fx,t,q,Accent);var white=Vector3.One;var ember=NativeEntranceFx.Hex(0xffb35a,2.4f);
            // Sparks where the tip scrapes the floor after the bite (a fixed schedule, each spark sampled where the tip was when
            // it flew; only while the tip is on the floor).
            for(int i=0;i<48;i++)
            {
                if(NativeEntranceMath.Rand(400,i)>q)continue;float born=Hit-.04f+i*.013f,a=t-born;if(a<0||a>.45f)continue;var tip=Anchor(NativeEntranceAnchor.RightTip,born);if(tip.Y>NativeEntranceStage.FloorY+.24f)continue;
                var v=-ForwardOf(born)*1.4f+new Vector3(NativeEntranceMath.Signed(400,i*3),0,NativeEntranceMath.Signed(400,i*3+1))*.6f+new Vector3(0,.9f+1.4f*NativeEntranceMath.Rand(400,i*3+2),0);
                var p=new Vector3(tip.X,NativeEntranceStage.FloorY+.03f,tip.Z)+v*a+new Vector3(0,-4.9f*a*a,0);if(p.Y<NativeEntranceStage.FloorY)continue;float u=a/.45f;
                fx.Streak(p,v+new Vector3(0,-9.8f*a,0),Vector3.Lerp(white*3,ember,u)*(1-u),.016f,.03f,.035f,.8f,u,400+i);
            }
            // Embers gathering on the blade through the wind-up.
            NativeEntranceFx.Embers(fx,t,1.2f,Hit,Anchor(NativeEntranceAnchor.RightTip,t)-new Vector3(0,.3f,0),.25f,40,.4f,.35f,ember,.05f,430,q);
            // The cracks: ember fissures racing out across the floor from the bite, cooling through the slow motion.
            var tipHit=Anchor(NativeEntranceAnchor.RightTip,Hit);float c=t-Hit;
            if(c>0&&c<2.2f)
            {
                int lines=q<.5f?6:9;float cool=1-NativeEntranceMath.Ramp(c,.5f,2.2f);
                for(int l=0;l<lines;l++)
                {
                    float ang=l/(float)lines*6.2831853f+NativeEntranceMath.Signed(450,l)*.3f;Vector3 prev=new Vector3(tipHit.X,NativeEntranceStage.FloorY+.012f,tipHit.Z);
                    for(int k=1;k<=10;k++)
                    {
                        float reach=k*.17f;if(reach>c*6)break;ang+=NativeEntranceMath.Signed(451+l,k)*.35f;var p=prev+new Vector3((float)Math.Cos(ang)*.17f,0,(float)Math.Sin(ang)*.17f);
                        float heat=cool*(1-k*.06f)*(.8f+.2f*(float)Math.Sin(c*20+k+l));fx.Streak(p,p-prev,Vector3.Lerp(white*2.5f,ember,NativeEntranceMath.Clamp01(c*2+k*.08f))*heat,.035f-k*.002f,0,1,0,c,460+l*12+k);
                        if(k%3==0)fx.Glow(p+new Vector3(0,.02f,0),ember*(.35f*heat),0,.22f,0,c,600+l*12+k);prev=p;
                    }
                }
            }
            NativeEntranceFx.Embers(fx,t,Hit+.05f,Hit+1.8f,new Vector3(tipHit.X,NativeEntranceStage.FloorY,tipHit.Z),.9f,26,.55f,1.1f,ember,.045f,700,q);
            NativeEntranceFx.Sparks(fx,t,Hit,new Vector3(tipHit.X,NativeEntranceStage.FloorY+.05f,tipHit.Z),new Vector3(0,1,0),1.4f,34,4.8f,.8f,9.8f,.8f,white*3,ember,.02f,720,q,NativeEntranceStage.FloorY);
            NativeEntranceFx.GroundDust(fx,t,Hit,new Vector3(tipHit.X,0,tipHit.Z),24,.3f,2.2f,1.9f,NativeEntranceFx.Hex(0x8a8070),.6f,.48f,760,q,NativeEntranceStage.FloorY);
        }
        public override void Lights(float t,NativeEntranceLights l)
        {
            base.Lights(t,l);var tip=Anchor(NativeEntranceAnchor.RightTip,Hit);float glow=NativeEntranceMath.Ramp(t,Hit,Hit+.05f)*(1-NativeEntranceMath.Ramp(t,Hit+.4f,Hit+2.2f));
            if(glow>.01f)l.Add(new Vector3(tip.X,NativeEntranceStage.FloorY+.15f,tip.Z),NativeEntranceFx.Hex(0xff9a40,9*glow),4);
        }
        protected override void Plan()
        {
            var tip=Anchor(NativeEntranceAnchor.RightTip,Hit);var p=new Vector3(tip.X,NativeEntranceStage.FloorY+.95f,tip.Z);
            Blasts=new[]{new NativeEntranceBlast{At=Hit,Style=NativeBlastStyle.Quake,Position=p,Radius=1.6f,Size=.5f},new NativeEntranceBlast{At=Hit,Impact=true,Position=new Vector3(tip.X,NativeEntranceStage.FloorY+.1f,tip.Z),Radius=.9f,ImpactColor=0xffd08a}};
        }
    }

    /// <summary>Guns: a muzzle (the right tip), a target downrange and the shot schedule.</summary>
    internal abstract class GunEntrance:NativeWeaponEntrance
    {
        protected Vector3 TargetAt;protected float TargetHit;protected readonly float Range;
        protected GunEntrance(string style,float hit,int color,float range):base(style,hit,color){Range=range;SlowRate=.22f;SlowHold=.16f;}
        protected virtual float FirstShot=>Hit;
        protected override void Plan()
        {
            var muzzle=Anchor(NativeEntranceAnchor.RightTip,FirstShot);var dir=Aim(false,FirstShot);dir.Y=Math.Max(0,Math.Min(.04f,dir.Y));dir=Vector3.Normalize(dir);
            TargetAt=muzzle+dir*Range;TargetHit=FirstShot+Range/TracerSpeed;
            // The over-the-shoulder shot (OverShoulder >= 0): placed in the world behind the gun shoulder, the target downrange.
            if(OverShoulder>=0&&OverShoulder<Shots.Length)
            {
                var flat=Vector3.Normalize(new Vector3(dir.X,0,dir.Z));var right=new Vector3(-flat.Z,0,flat.X);var shoulder=Anchor(NativeEntranceAnchor.RightShoulder,FirstShot);
                var e0=shoulder-flat*1.75f+right*(OverShoulderSide+.07f)+new Vector3(0,.5f,0);var e1=shoulder-flat*1.45f+right*OverShoulderSide+new Vector3(0,.42f,0);var look=new Vector3(TargetAt.X,TargetAt.Y-.05f,TargetAt.Z);
                ref var sh=ref Shots[OverShoulder];sh.Orbit=false;sh.Track=0;sh.TrackY=0;sh.Face=-1;sh.Follow=-1;sh.Eye0=e0;sh.Eye1=(e0+e1)*.5f;sh.Eye2=e1;sh.Look0=look;sh.Look1=look;sh.Look2=look;
            }
        }
        /// <summary>The shot that becomes the over-the-shoulder view down the range (-1: none).</summary>
        protected int OverShoulder=-1;protected float OverShoulderSide=.48f;
        protected const float TracerSpeed=24;
        public override void Props(float t,INativeEntranceProps props,float quality){base.Props(t,props,quality);Prepare();Target(props,t,TargetAt,-Aim(false,FirstShot),TargetHit,Accent);}
        protected void Shot(INativeEntranceFx fx,float t,float at,float q,float size,int seed,bool tracer=true,bool casing=true)
        {
            var muzzle=Anchor(NativeEntranceAnchor.RightTip,at);var dir=Aim(false,at);MuzzleFlash(fx,t,at,muzzle,dir,size,Accent,seed,q);
            if(tracer){var to=TargetAt-muzzle;float range=to.Length();Tracer(fx,t,at,muzzle,to/range,TracerSpeed,range,Vector3.Lerp(Accent,Vector3.One,.5f),.025f,seed+20);}
            if(casing)Casing(fx,t,at+.02f,Anchor(NativeEntranceAnchor.RightHand,at)+new Vector3(0,.08f,0),RightOf(at)+ForwardOf(at)*-.2f,seed+40);
        }
        protected void TargetBurst(INativeEntranceFx fx,float t,float q)
        {
            NativeEntranceFx.Sparks(fx,t,TargetHit,TargetAt,Vector3.Zero,1,32,3.2f,.7f,6,1.5f,Vector3.One*3,Accent*2,.02f,880,q,NativeEntranceStage.FloorY);
            float g=NativeEntranceMath.Pulse(t,TargetHit,.01f,.12f);if(g>.02f)fx.Glow(TargetAt,Accent*(4*g),0,.9f*g,1,0,890);
        }
    }

    /// <summary>Deadeye (pistol, hit 2.12 s): a twirl, a two-hand aim held on a target downrange, one slow-motion shot, barrel up.</summary>
    internal sealed class Deadeye:GunEntrance
    {
        public Deadeye():base("weapon-pistol",2.12f,0xffd99f,9)
        {
            OverShoulder=2;
            Shots=new[]
            {
                Orbit(0,NativeEntranceCut.Cut,38,36,30,18,4.0f,3.3f,.7f,.8f,V(0,1.0f,0),V(0,1.0f,0),0,1,face:.2f),
                // The twirl: a long-lens detail on the gun hand.
                Follow(Shot(.5f,NativeEntranceCut.Cut,32,28,V(-2.3f,1.0f,2.9f),V(-2.05f,1.0f,2.6f),V(0,.1f,0),V(0,.1f,0),1,1,face:.8f),NativeEntranceAnchor.RightHand),
                // Over the shoulder down the aim, the target hanging in the dark.
                Shot(1.15f,NativeEntranceCut.Cut,40,36,V(-1.25f,1.95f,-2.6f),V(-1.05f,1.88f,-2.3f),V(.2f,.95f,5),V(.15f,1.0f,5),1,1,face:1.6f),
                // The stare: in front and low, pushing in on the two-hand aim.
                Shot(1.75f,NativeEntranceCut.Cut,36,33,V(1.2f,.95f,3.1f),V(1.0f,1.0f,2.8f),V(0,1.12f,.2f),V(0,1.15f,.2f),1,1,face:2.0f),
                // The shot: cut wide to the side, panning with the tracer through the slow motion.
                Orbit(2.12f,NativeEntranceCut.Cut,42,40,92,75,3.4f,3.1f,1.15f,1.1f,V(0,1.1f,.35f),V(0,1.1f,.75f),1,1,face:2.12f),
                Orbit(2.75f,NativeEntranceCut.Blend,40,38,75,62,3.1f,3.4f,1.1f,1.0f,V(0,1.1f,.7f),V(0,1.05f,.3f),0,1,.3f,face:2.12f),
                Shot(3.2f,NativeEntranceCut.Cut,35,33,V(.55f,.48f,3.45f),V(.4f,.52f,3.1f),V(0,1.08f,0),V(0,1.1f,0),1,0),
            };
        }
        public override void Effects(float t,INativeEntranceFx fx,float q)
        {
            Motes(fx,t,q,Accent);Beams(fx,t,q,Accent);Prepare();
            // The twirl's glints.
            for(int i=0;i<6;i++){float at=.55f+i*.09f,g=NativeEntranceMath.Pulse(t,at,.02f,.06f);if(g>.03f)NativeEntranceFx.Glint(fx,Anchor(NativeEntranceAnchor.RightTip,t),Vector3.One*(2.2f*g),.18f*g+.03f,Vector3.UnitX,Vector3.UnitY,800+i);}
            // A breath-hold glint on the front sight.
            float hold=NativeEntranceMath.Pulse(t,Hit-.25f,.03f,.12f);if(hold>.03f)NativeEntranceFx.Glint(fx,Anchor(NativeEntranceAnchor.RightTip,t),Vector3.One*(2*hold),.16f*hold+.02f,Vector3.UnitX,Vector3.UnitY,820);
            Shot(fx,t,Hit,q,.2f,830);TargetBurst(fx,t,q);
            NativeEntranceFx.Jet(fx,t,Hit+.06f,Hit+1.2f,Anchor(NativeEntranceAnchor.RightTip,Math.Min(t,Hit+1.2f)),new Vector3(0,1,0),9,.2f,1.1f,.12f,NativeEntranceFx.Hex(0x9a9ea6),.35f,850,q,NativeEntranceStage.FloorY,.25f,.35f);
        }
        protected override NativeEntranceAnchor HitAnchor=>NativeEntranceAnchor.RightTip;
    }

    /// <summary>Longwatch (rifle, hit 1.8 s): kneeling, a red sight beam laid on a far target, one shot with brake jets and a dust kick.</summary>
    internal sealed class Longwatch:GunEntrance
    {
        public Longwatch():base("weapon-rifle",1.8f,0xace1ff,13)
        {
            OverShoulder=2;OverShoulderSide=.8f;
            Shots=new[]
            {
                Orbit(0,NativeEntranceCut.Cut,40,38,-36,-24,4.4f,3.6f,.5f,.6f,V(0,.8f,0),V(0,.85f,0),0,1,face:.3f),
                // Along the rifle, stock to muzzle.
                Shot(.75f,NativeEntranceCut.Cut,38,35,V(-2.2f,1.2f,-.4f),V(-2.05f,1.15f,.3f),V(0,1.0f,.4f),V(0,1.0f,1.1f),3,1,face:1.0f),
                // The scope view: behind the shoulder, a long lens down the beam to the target.
                Shot(1.3f,NativeEntranceCut.Cut,30,26,V(-1.0f,1.8f,-2.3f),V(-.88f,1.74f,-2.05f),V(.1f,1.0f,8),V(.1f,1.02f,8),1,1,face:1.5f),
                // The shot: low in front, the flash and the brake jets hanging in slow motion.
                Orbit(1.8f,NativeEntranceCut.Cut,44,40,-36,-22,2.7f,2.4f,.38f,.42f,V(0,.85f,.45f),V(0,.9f,.4f),1,1,face:1.8f),
                Orbit(2.5f,NativeEntranceCut.Blend,38,38,-22,-12,2.6f,3.5f,.8f,1.1f,V(0,1.0f,0),V(0,1.02f,0),0,1,.3f,face:1.8f),
                Shot(3.2f,NativeEntranceCut.Cut,36,34,V(.85f,.4f,3.6f),V(.55f,.44f,3.3f),V(0,1.0f,0),V(0,1.02f,0),1,0),
            };
        }
        public override void Effects(float t,INativeEntranceFx fx,float q)
        {
            Motes(fx,t,q,Accent);Beams(fx,t,q,Accent);Prepare();var red=NativeEntranceFx.Hex(0xff5047,2.5f);
            // The sight beam and its dot on the target, flickering faintly.
            float beam=NativeEntranceMath.Ramp(t,1.18f,1.3f)*(1-NativeEntranceMath.Ramp(t,Hit,Hit+.02f));
            if(beam>.01f){var m=Anchor(NativeEntranceAnchor.RightTip,t);var to=TargetAt-m;float flick=.85f+.15f*(float)Math.Sin(t*41);fx.Streak(TargetAt,to,red*(beam*flick*.6f),.008f,0,1,.3f,0,900);fx.Glow(TargetAt,red*(beam*1.6f),0,.12f,0,0,901);fx.Glow(m,red*(beam*.8f),0,.07f,0,0,902);}
            Shot(fx,t,Hit,q,.26f,910);TargetBurst(fx,t,q);
            // The muzzle brake's side jets and the recoil's dust at the knees.
            var muzzle=Anchor(NativeEntranceAnchor.RightTip,Hit);var right=RightOf(Hit);
            NativeEntranceFx.Sparks(fx,t,Hit,muzzle,right,.25f,10,4,.18f,0,6,Vector3.One*3,Accent*2,.03f,930,q,NativeEntranceStage.FloorY);NativeEntranceFx.Sparks(fx,t,Hit,muzzle,-right,.25f,10,4,.18f,0,6,Vector3.One*3,Accent*2,.03f,940,q,NativeEntranceStage.FloorY);
            NativeEntranceFx.Jet(fx,t,Hit,Hit+.1f,muzzle,right,60,1.2f,1.1f,.2f,NativeEntranceFx.Hex(0x9ea4ae),.35f,950,q,NativeEntranceStage.FloorY,.2f,.2f);NativeEntranceFx.Jet(fx,t,Hit,Hit+.1f,muzzle,-right,60,1.2f,1.1f,.2f,NativeEntranceFx.Hex(0x9ea4ae),.35f,960,q,NativeEntranceStage.FloorY,.2f,.2f);
            var pelvis=Anchor(NativeEntranceAnchor.Pelvis,Hit);NativeEntranceFx.GroundDust(fx,t,Hit,new Vector3(pelvis.X,0,pelvis.Z),14,.25f,1.2f,1.3f,NativeEntranceFx.Hex(0x80889a),.45f,.3f,970,q,NativeEntranceStage.FloorY);
        }
    }

    /// <summary>Belt Rattle (machine gun, burst 1.93-2.33 s): braced low, a ten-round burst raining brass, the barrel smoking at the hip.</summary>
    internal sealed class BeltRattle:GunEntrance
    {
        public BeltRattle():base("weapon-machinegun",2.12f,0xffb867,10){SlowRate=.3f;Init();}
        protected override float FirstShot=>1.93f;
        private static float Round(int i)=>1.93f+i/24f;
        protected override void Plan(){base.Plan();TargetHit=Round(6)+Range/TracerSpeed;}
        public override void Effects(float t,INativeEntranceFx fx,float q)
        {
            Motes(fx,t,q,Accent);Beams(fx,t,q,Accent);Prepare();
            for(int i=0;i<10;i++)Shot(fx,t,Round(i),q,.16f,1000+i*50,i%2==0,true);TargetBurst(fx,t,q);
            // The barrel heats and smokes after the belt runs out.
            var muzzle=Anchor(NativeEntranceAnchor.RightTip,t);float heat=NativeEntranceMath.Ramp(t,1.95f,2.33f)*(1-NativeEntranceMath.Ramp(t,2.4f,3.4f));if(heat>.01f)fx.Glow(muzzle-Aim(false,t)*.12f,NativeEntranceFx.Hex(0xff6a20,1.4f*heat),0,.16f,0,0,1600);
            NativeEntranceFx.Jet(fx,t,2.33f,3.6f,Anchor(NativeEntranceAnchor.RightTip,Math.Min(t,3.6f)),new Vector3(0,1,0),11,.25f,1.3f,.16f,NativeEntranceFx.Hex(0x9a9ea6),.4f,1610,q,NativeEntranceStage.FloorY,.3f,.4f);
        }
        public override void Lights(float t,NativeEntranceLights l)
        {
            base.Lights(t,l);for(int i=0;i<10;i++){float f=NativeEntranceMath.Pulse(t,Round(i),.005f,.03f);if(f>.02f){l.Add(Anchor(NativeEntranceAnchor.RightTip,t),Accent*(18*f),5);break;}}
        }
        protected override NativeEntranceAnchor HitAnchor=>NativeEntranceAnchor.RightTip;
        // Shots (set in the constructor body below the effects).
        internal void Init()
        {
            Shots=new[]
            {
                Orbit(0,NativeEntranceCut.Cut,40,38,26,14,4.3f,3.4f,.45f,.55f,V(0,.95f,0),V(0,.95f,0),0,1,face:.3f),
                // The feed and the hands: a slow long-lens orbit.
                Follow(Orbit(.7f,NativeEntranceCut.Cut,34,32,-62,-34,3.4f,3.1f,1.05f,1.0f,V(0,.05f,0),V(0,.05f,0),3,1,face:1.1f),NativeEntranceAnchor.RightHand),
                // Low in front, looking up the barrel.
                Shot(1.45f,NativeEntranceCut.Cut,44,42,V(.7f,.28f,3.2f),V(.6f,.26f,2.9f),V(0,1.0f,.3f),V(0,.98f,.3f),1,1,face:1.8f),
                // The burst from the side, the tracers and brass slowed.
                Orbit(1.93f,NativeEntranceCut.Cut,42,40,84,66,3.4f,3.1f,1.0f,1.0f,V(0,1.0f,.4f),V(0,1.0f,.6f),1,1,face:2.0f),
                Orbit(2.7f,NativeEntranceCut.Blend,38,38,66,50,3.1f,3.4f,1.0f,.9f,V(0,1.0f,.55f),V(0,1.0f,.1f),0,1,.3f,face:2.0f),
                Shot(3.2f,NativeEntranceCut.Cut,36,34,V(-.85f,.42f,3.6f),V(-.55f,.46f,3.3f),V(0,1.0f,0),V(0,1.02f,0),1,0),
            };
        }
    }

    /// <summary>Cinder Crown (fire, hit 1.8 s): flame gathered in the casting hand, a flame lance into a fire burst, a crown of embers.</summary>
    internal sealed class CinderCrown:NativeWeaponEntrance
    {
        public CinderCrown():base("weapon-fire",1.8f,0xff6c32)
        {
            Shots=new[]
            {
                Orbit(0,NativeEntranceCut.Cut,40,38,-52,-26,4.4f,3.4f,1.3f,.9f,V(0,1.0f,0),V(0,1.0f,0),0,1,face:.3f),
                // The gathering flame in the casting hand, a long lens.
                Follow(Shot(.8f,NativeEntranceCut.Cut,33,30,V(-2.2f,1.25f,2.8f),V(-2.0f,1.22f,2.55f),V(0,.08f,0),V(0,.1f,0),1,1,face:1.1f),NativeEntranceAnchor.RightHand),
                // Low, the arm rising into the strike.
                Shot(1.4f,NativeEntranceCut.Cut,46,44,V(1.4f,.3f,3.0f),V(1.25f,.3f,2.7f),V(0,1.2f,0),V(0,1.1f,.1f),1,1,face:1.6f),
                // The lance and the burst from the side, slowed.
                Orbit(1.8f,NativeEntranceCut.Cut,42,40,78,56,3.6f,3.2f,1.0f,.95f,V(0,.95f,.5f),V(0,.95f,.45f),1,1,face:1.8f),
                Orbit(2.6f,NativeEntranceCut.Blend,38,38,56,24,3.2f,3.6f,1.0f,1.3f,V(0,1.1f,0),V(0,1.1f,0),0,1,.3f,face:1.8f),
                // Hero: the crown of embers round the head.
                Shot(3.2f,NativeEntranceCut.Cut,35,33,V(.7f,.7f,3.4f),V(.48f,.72f,3.1f),V(0,1.12f,0),V(0,1.14f,0),1,0),
            };
        }
        public override void Effects(float t,INativeEntranceFx fx,float q)
        {
            Motes(fx,t,q,NativeEntranceFx.Hex(0xffa060));Beams(fx,t,q,Accent,.8f);var white=Vector3.One;var flame=NativeEntranceFx.Hex(0xffb340,2.2f);var deep=NativeEntranceFx.Hex(0xd8401c,1.6f);
            var hand=Anchor(NativeEntranceAnchor.RightHand,t);float charge=NativeEntranceMath.Ramp(t,.4f,Hit)*(1-NativeEntranceMath.Ramp(t,Hit,Hit+.08f));
            // Flames spiral into the hand, the core swelling.
            for(int i=0;i<40;i++)
            {
                if(NativeEntranceMath.Rand(1100,i)>q)continue;float born=.4f+i*.035f,a=t-born;if(a<0||a>.4f||born>Hit)continue;float u=a/.4f;float ang=i*2.4f+u*5,r=.45f*(1-u);
                var p=hand+new Vector3((float)Math.Cos(ang)*r,(1-u)*.25f-.05f,(float)Math.Sin(ang)*r);fx.Glow(p,Vector3.Lerp(deep,flame,u)*(.6f+u),0,.06f+.05f*u,0,u,1100+i);
            }
            if(charge>.01f){fx.Glow(hand,white*(2.2f*charge),0,.14f+.1f*charge,1,0,1150);fx.Glow(hand,flame*(1.4f*charge),0,.4f*charge+.1f,0,0,1151);NativeEntranceFx.Embers(fx,t,.5f,Hit,hand,.08f,30,.9f,.4f,flame,.04f,1160,q);}
            // The lance: from the hand to the burst.
            var lanceFrom=Anchor(NativeEntranceAnchor.RightHand,Hit);var to=Anchor(NativeEntranceAnchor.Pelvis,Hit)+ForwardOf(Hit)*1.7f;to.Y=NativeEntranceStage.FloorY+.95f;float lance=1-NativeEntranceMath.Ramp(t,Hit+.04f,Hit+.3f);
            if(t>=Hit&&lance>.01f){fx.Streak(to,to-lanceFrom,Vector3.Lerp(white*4,flame,.3f)*lance,.07f,0,1,.4f,0,1170);fx.Streak(to,to-lanceFrom,flame*lance,.18f,0,1,.7f,0,1171);}
            NativeEntranceFx.Embers(fx,t,Hit,Hit+2.6f,new Vector3(0,NativeEntranceStage.FloorY,0),1.2f,22,.8f,1.4f,flame,.045f,1180,q);
            // The crown: embers orbiting above the head from the recovery on.
            float crown=NativeEntranceMath.Ramp(t,2.5f,3.1f);
            if(crown>.01f){var head=Anchor(NativeEntranceAnchor.Head,t);for(int i=0;i<12;i++){if(NativeEntranceMath.Rand(1200,i)>Math.Max(.4f,q))continue;float ang=i/12f*6.2831853f+t*1.6f,y=.34f+.04f*(float)Math.Sin(t*3+i);var p=head+new Vector3((float)Math.Cos(ang)*.32f,y,(float)Math.Sin(ang)*.32f);float f=.75f+.25f*(float)Math.Sin(t*17+i*2);fx.Glow(p,flame*(crown*f),0,.07f,0,0,1200+i);fx.Streak(p,new Vector3(-(float)Math.Sin(ang),0,(float)Math.Cos(ang))*.5f,deep*(crown*.8f*f),.025f,.05f,.12f,.9f,0,1220+i);}}
        }
        public override void Lights(float t,NativeEntranceLights l)
        {
            base.Lights(t,l);float charge=NativeEntranceMath.Ramp(t,.4f,Hit)*(1-NativeEntranceMath.Ramp(t,Hit,Hit+.1f));if(charge>.01f)l.Add(Anchor(NativeEntranceAnchor.RightHand,t)+new Vector3(0,.1f,.15f),NativeEntranceFx.Hex(0xff8a30,7*charge),4);
            float crown=NativeEntranceMath.Ramp(t,2.5f,3.1f);if(crown>.01f)l.Add(Anchor(NativeEntranceAnchor.Head,t)+new Vector3(0,.4f,.1f),NativeEntranceFx.Hex(0xff8a30,3.5f*crown),3);
        }
        protected override NativeEntranceAnchor HitAnchor=>NativeEntranceAnchor.RightHand;
        protected override void Plan()
        {
            var to=Anchor(NativeEntranceAnchor.Pelvis,Hit)+ForwardOf(Hit)*1.7f;to.Y=NativeEntranceStage.FloorY+.95f;
            Blasts=new[]{new NativeEntranceBlast{At=Hit,Style=NativeBlastStyle.Fire,Position=to,Radius=.85f,Size=.4f,Tint=new NativeBlastTint(0xffffff,0xfff0b0,0xffc23a)}};
        }
    }

    /// <summary>Tidal Serpent (water, hit 2.12 s): two water serpents spiral up round the pilot and dive into a tidal splash.</summary>
    internal sealed class TidalSerpent:NativeWeaponEntrance
    {
        public TidalSerpent():base("weapon-water",2.12f,0x63e2ff)
        {
            Shots=new[]
            {
                Orbit(0,NativeEntranceCut.Cut,40,38,20,10,4.6f,3.9f,2.9f,2.2f,V(0,.5f,0),V(0,.8f,0),0,1,face:.2f),
                // Round the pilot, rising with the serpents.
                Orbit(.8f,NativeEntranceCut.Cut,42,42,-32,58,3.3f,2.9f,.9f,1.6f,V(0,1.1f,0),V(0,1.5f,0),3,1,face:1.0f),
                // Below the apex, looking up.
                Shot(1.6f,NativeEntranceCut.Cut,50,48,V(1.0f,.28f,3.2f),V(.9f,.26f,2.9f),V(0,1.45f,0),V(0,1.65f,0),1,1,face:1.9f),
                // The splash, whipped to the front and slowed.
                Orbit(2.12f,NativeEntranceCut.Whip,42,40,-8,-30,3.8f,3.4f,1.3f,1.05f,V(0,.85f,0),V(0,.9f,0),1,1,.16f,face:2.12f),
                Orbit(2.8f,NativeEntranceCut.Blend,38,38,-30,-22,3.4f,3.7f,1.05f,.85f,V(0,.92f,0),V(0,1.0f,0),0,1,.3f,face:2.12f),
                Shot(3.2f,NativeEntranceCut.Cut,36,34,V(-.8f,.42f,3.6f),V(-.52f,.46f,3.3f),V(0,1.0f,0),V(0,1.02f,0),1,0),
            };
        }
        private Vector3 Serpent(int i,float t)
        {
            var root=Anchor(NativeEntranceAnchor.Root,Math.Min(t,Hit));float rise=NativeEntranceMath.Clamp01((t-.1f)/(Hit-.1f)),dive=NativeEntranceMath.Clamp01((t-Hit)/.3f),u=rise*rise*(3-2*rise);
            float a=u*(float)Math.PI*3+i*(float)Math.PI+t*(i==1?-.8f:.8f),R=.75f+u*.2f+dive*.3f,y=.15f+u*1.75f-dive*1.8f;
            return new Vector3(root.X+(float)Math.Cos(a)*R,NativeEntranceStage.FloorY+y,root.Z+(float)Math.Sin(a)*R);
        }
        public override void Effects(float t,INativeEntranceFx fx,float q)
        {
            Motes(fx,t,q,Accent);Beams(fx,t,q,Accent);var white=Vector3.One;var foam=NativeEntranceFx.Hex(0xeaffff,2.2f);var deep=NativeEntranceFx.Hex(0x2b8bd6,1.6f);
            if(t<Hit+.32f)for(int s=0;s<2;s++)
            {
                // The body: segments trailing the head along its own path, a bright crest on a deep blue body.
                Vector3 prev=Serpent(s,t);float life=NativeEntranceMath.Ramp(t,.1f,.35f);
                for(int k=1;k<=16;k++)
                {
                    float back=t-k*.035f;if(back<.1f)break;var p=Serpent(s,back);float u=k/16f,thick=(1-u*.75f)*life;
                    fx.Streak(prev,prev-p,Vector3.Lerp(foam,deep,u)*thick,.07f*thick+.02f,0,1,.2f,u,1300+s*40+k);if(k%2==0)fx.Glow(p,deep*(.5f*thick),0,.28f*thick,0,u,1340+s*40+k);prev=p;
                }
                var head=Serpent(s,t);fx.Glow(head,foam*(1.4f*life),0,.18f,1,0,1390+s);
                // Spray shed from the body.
                for(int k=0;k<8;k++){if(NativeEntranceMath.Rand(1400+s,k)>q)continue;float born=t-((k*.09f+t*.7f)%.6f),a=t-born;var from=Serpent(s,born);var v=NativeEntranceMath.Direction(1400+s,k+(int)(born*10))*.6f+new Vector3(0,.4f,0);var p=from+v*a+new Vector3(0,-4.9f*a*a,0);fx.Streak(p,v+new Vector3(0,-9.8f*a,0),foam*(.6f*(1-a/.6f)*life),.012f,.03f,.04f,.7f,0,1420+s*10+k);}
            }
            var root=Anchor(NativeEntranceAnchor.Root,Hit);var centre=new Vector3(root.X,NativeEntranceStage.FloorY,root.Z);
            // The splash crown: droplets thrown up and out, slowed through the beat.
            for(int i=0;i<36;i++)
            {
                if(NativeEntranceMath.Rand(1500,i)>q)continue;float a=t-Hit-.02f*NativeEntranceMath.Rand(1500,i+80);if(a<0||a>1.3f)continue;float ang=i/36f*6.2831853f,R=.6f+.4f*NativeEntranceMath.Rand(1500,i+40);
                var v=new Vector3((float)Math.Cos(ang)*.9f,2.6f+2.2f*NativeEntranceMath.Rand(1500,i+120),(float)Math.Sin(ang)*.9f);var p=centre+new Vector3((float)Math.Cos(ang)*R,.1f,(float)Math.Sin(ang)*R)+v*a+new Vector3(0,-4.9f*a*a,0);if(p.Y<NativeEntranceStage.FloorY)continue;
                fx.Streak(p,v+new Vector3(0,-9.8f*a,0),foam*(1-a/1.3f),.022f,.05f,.035f,.6f,a,1500+i);
            }
            NativeEntranceFx.GroundDust(fx,t,Hit,centre,22,.5f,1.8f,1.8f,NativeEntranceFx.Hex(0xb9d6e6),.45f,.45f,1560,q,NativeEntranceStage.FloorY);
        }
        protected override NativeEntranceAnchor HitAnchor=>NativeEntranceAnchor.Pelvis;
        protected override void Plan()
        {
            var r=Anchor(NativeEntranceAnchor.Root,Hit);Blasts=new[]{new NativeEntranceBlast{At=Hit,Style=NativeBlastStyle.Kinetic,Position=new Vector3(r.X,NativeEntranceStage.FloorY+.95f,r.Z),Radius=1.4f,Size=.45f,Tint=new NativeBlastTint(0xeaffff,0x8de9ee,0x2b8bd6)}};
        }
    }

    /// <summary>Thunderstep (lightning, hit 1.45 s): hovering high, a gravity drop that lands on the hit under a violet bolt from the sky.</summary>
    internal sealed class Thunderstep:NativeWeaponEntrance
    {
        public Thunderstep():base("weapon-lightning",1.45f,0xc8aaff)
        {
            Shots=new[]
            {
                // Looking up from the floor at the pilot hanging in the dark.
                Shot(0,NativeEntranceCut.Cut,46,44,V(1.2f,.45f,3.1f),V(1.05f,.5f,2.8f),V(0,2.95f,0),V(0,3.0f,0),1,1,face:.3f),
                // The drop from the side, following it down.
                Orbit(.55f,NativeEntranceCut.Cut,44,44,82,70,3.8f,3.5f,1.0f,.7f,V(0,1.0f,0),V(0,.95f,0),1,1,face:.8f,trackY:1),
                // On the floor where it will land, sparks crawling in, the pilot falling into frame.
                Shot(1.1f,NativeEntranceCut.Cut,56,50,V(-1.3f,.26f,3.0f),V(-1.1f,.24f,2.7f),V(0,1.9f,0),V(0,1.0f,0),1,1,face:1.3f),
                // The strike: wide and high, the bolt and the shock slowed.
                Orbit(1.45f,NativeEntranceCut.Cut,44,42,-14,-36,4.2f,3.6f,2.0f,1.5f,V(0,1.05f,0),V(0,1.0f,0),1,1,face:1.45f),
                Orbit(2.25f,NativeEntranceCut.Blend,40,38,-36,-14,3.4f,3.8f,1.0f,.75f,V(0,1.0f,0),V(0,1.02f,0),0,1,.3f,face:1.45f),
                Shot(3.2f,NativeEntranceCut.Cut,36,34,V(.85f,.42f,3.6f),V(.55f,.46f,3.3f),V(0,1.0f,0),V(0,1.02f,0),1,0),
            };
        }
        private static readonly Vector3 Violet=NativeEntranceFx.Hex(0xc9a6ff,2.6f),Core=NativeEntranceFx.Hex(0xf3ecff,4.5f);
        public override void Effects(float t,INativeEntranceFx fx,float q)
        {
            Motes(fx,t,q,Accent);Beams(fx,t,q,Accent,.6f);
            var pelvis=Anchor(NativeEntranceAnchor.Pelvis,t);var land=Anchor(NativeEntranceAnchor.Root,Hit);var spot=new Vector3(land.X,NativeEntranceStage.FloorY+.02f,land.Z);
            // Crackle round the hovering body.
            if(t<Hit)for(int i=0;i<3;i++){if(NativeEntranceMath.Rand(1700,i+(int)(t*12))>q*.9f)continue;var a=pelvis+NativeEntranceMath.Direction(1700+(int)(t*12),i)*.5f+new Vector3(0,.3f,0);var b=a+NativeEntranceMath.Direction(1750+(int)(t*12),i)*.35f;NativeEntranceFx.Bolt(fx,t,a,b,5,.9f,.05f,Violet,Violet*.2f,.012f,1760+i,q);}
            // Sparks crawling toward the landing spot.
            for(int i=0;i<24;i++){if(NativeEntranceMath.Rand(1800,i)>q)continue;float born=.3f+i*.045f,a=t-born;if(a<0||a>.3f||born>Hit)continue;float ang=NativeEntranceMath.Rand(1800,i+50)*6.283f,R=.4f+.7f*NativeEntranceMath.Rand(1800,i+90);var from=spot+new Vector3((float)Math.Cos(ang)*R,0,(float)Math.Sin(ang)*R);var p=Vector3.Lerp(from,spot,a/.3f)+new Vector3(0,.04f+.08f*(float)Math.Sin(a*30),0);fx.Streak(p,spot-from,Violet*(1-a/.3f),.015f,.06f,.08f,.7f,0,1800+i);}
            // The bolt from the sky, flickering through the strike.
            float bolt=NativeEntranceMath.Ramp(t,Hit-.03f,Hit)*(1-NativeEntranceMath.Ramp(t,Hit+.18f,Hit+.4f));
            if(bolt>.01f){var top=spot+new Vector3(.6f,9,-.8f);NativeEntranceFx.Bolt(fx,t,top,spot+new Vector3(0,.3f,0),14,1.1f,.04f,Core*bolt,Violet*(.35f*bolt),.03f,1900,q);NativeEntranceFx.Bolt(fx,t+.02f,top,spot+new Vector3(0,.3f,0),12,1.4f,.05f,Violet*(.7f*bolt),Violet*(.1f*bolt),.016f,1950,q);}
            // Ground arcs racing out from the landing.
            float g=t-Hit;if(g>0&&g<.5f){int arcs=q<.5f?4:7;for(int i=0;i<arcs;i++){float ang=i/(float)arcs*6.283f+.4f,R=.2f+g*3.2f;var a=spot+new Vector3((float)Math.Cos(ang)*.15f,0,(float)Math.Sin(ang)*.15f);var b=spot+new Vector3((float)Math.Cos(ang)*R,0,(float)Math.Sin(ang)*R);NativeEntranceFx.Bolt(fx,t,a,b,6,.6f,.05f,Violet*(1-g/.5f),Violet*(.15f*(1-g/.5f)),.014f,2000+i,q);}}
            // The staff head keeps arcing for a beat.
            if(t>Hit+.05f&&t<Hit+1.3f){var head=Anchor(NativeEntranceAnchor.LeftTip,t);for(int i=0;i<2;i++){var b=head+NativeEntranceMath.Direction(2050+(int)(t*14),i)*.4f;NativeEntranceFx.Bolt(fx,t,head,b,5,.8f,.06f,Violet*(1-NativeEntranceMath.Ramp(t,Hit+.6f,Hit+1.3f)),Violet*.15f,.012f,2060+i,q);}}
            NativeEntranceFx.GroundDust(fx,t,Hit,spot,18,.25f,1.6f,1.5f,NativeEntranceFx.Hex(0x8a88a0),.5f,.38f,2100,q,NativeEntranceStage.FloorY);
        }
        public override void Lights(float t,NativeEntranceLights l)
        {
            float key=.25f+.75f*NativeEntranceMath.Ramp(t,Hit,Hit+.3f);NativeEntranceStage.Rig(l,key,Accent,1,.2f+.8f*NativeEntranceMath.Ramp(t,Hit,Hit+.5f));
            float bolt=NativeEntranceMath.Pulse(t,Hit,.02f,.25f);var land=Anchor(NativeEntranceAnchor.Root,Hit);if(bolt>.01f)l.Add(new Vector3(land.X,2.2f,land.Z+.4f),NativeEntranceFx.Hex(0xd8c4ff,70*bolt),10);
            l.Add(Anchor(NativeEntranceAnchor.Chest,t)+new Vector3(0,0,.35f),NativeEntranceFx.Hex(0x70e1d0,.9f),3.2f);
        }
        protected override NativeEntranceAnchor HitAnchor=>NativeEntranceAnchor.Root;
        protected override void Plan()
        {
            var r=Anchor(NativeEntranceAnchor.Root,Hit);Blasts=new[]{new NativeEntranceBlast{At=Hit,Style=NativeBlastStyle.Shock,Position=new Vector3(r.X,NativeEntranceStage.FloorY+.95f,r.Z),Radius=1.3f,Size=.45f,Tint=new NativeBlastTint(0xfbf0ff,0xd7a6ff,0x8a4cff)}};
        }
    }

    /// <summary>Eventide Gate (darkness, hit 2.12 s): a void gate opens behind the pilot, a void blast is cast forward, the gate implodes.</summary>
    internal sealed class EventideGate:NativeWeaponEntrance
    {
        private static readonly Vector3 Gate=new Vector3(0,1.15f,-.6f);
        public EventideGate():base("weapon-darkness",2.12f,0xaf6eff)
        {
            Shots=new[]
            {
                // The gate tearing open behind the pilot.
                Shot(0,NativeEntranceCut.Cut,42,40,V(.5f,1.0f,5.0f),V(.35f,.9f,4.2f),V(0,1.2f,-.4f),V(0,1.2f,-.4f),0,0),
                // Low, the gate framing the pilot from behind.
                Shot(.9f,NativeEntranceCut.Cut,42,40,V(.6f,.32f,3.2f),V(.45f,.3f,2.7f),V(0,1.3f,-.4f),V(0,1.32f,-.4f),1,0),
                // The claw raised, a long lens.
                Follow(Shot(1.6f,NativeEntranceCut.Cut,34,32,V(-2.3f,1.4f,2.8f),V(-2.1f,1.4f,2.5f),V(0,.05f,0),V(0,.05f,0),1,1,face:1.9f),NativeEntranceAnchor.RightHand),
                // The void cast from the side, slowed.
                Orbit(2.12f,NativeEntranceCut.Cut,42,40,82,60,3.6f,3.2f,1.0f,1.0f,V(0,1.0f,.4f),V(0,1.0f,.35f),1,1,face:2.12f),
                // The gate collapsing behind.
                Orbit(2.75f,NativeEntranceCut.Blend,40,38,60,46,3.2f,3.8f,1.0f,1.2f,V(0,1.0f,.35f),V(0,1.12f,-.2f),0,1,.3f,face:2.12f),
                Shot(3.2f,NativeEntranceCut.Cut,36,34,V(-.8f,.42f,3.6f),V(-.52f,.46f,3.3f),V(0,1.0f,0),V(0,1.02f,0),1,0),
            };
        }
        private float Open(float t)=>NativeEntranceMath.Smooth(t/1.0f)*(1-NativeEntranceMath.Smooth((t-2.6f)/.55f));
        public override void Effects(float t,INativeEntranceFx fx,float q)
        {
            Motes(fx,t,q,Accent);Beams(fx,t,q,Accent,.5f);
            var rim=NativeEntranceFx.Hex(0xd59bff,2.4f);var deep=NativeEntranceFx.Hex(0x7a32d8,1.6f);float s=Open(t),RX=1.25f*s,RY=1.5f*NativeEntranceMath.Smooth(t/1.0f)*(t<2.6f?1:1-NativeEntranceMath.Smooth((t-2.6f)/.55f));
            if(s>.02f)
            {
                // The void inside: dark eroding puffs filling the ellipse; motes spiralling in; the rim a ring of light running round.
                for(int i=0;i<22;i++){if(NativeEntranceMath.Rand(2200,i)>Math.Max(.35f,q))continue;float ang=NativeEntranceMath.Rand(2200,i+30)*6.283f+t*.4f,d=(float)Math.Sqrt(NativeEntranceMath.Rand(2200,i+60))*.85f,u=(t*.35f+NativeEntranceMath.Rand(2200,i+90))%1f;fx.Smoke(Gate+new Vector3((float)Math.Cos(ang)*d*RX,(float)Math.Sin(ang)*d*RY,-.05f),NativeEntranceFx.Hex(0x0b0614),.85f*s,.55f*s+.15f,u,2200+i,-10);}
                for(int i=0;i<28;i++){if(NativeEntranceMath.Rand(2260,i)>q)continue;float u=(t*.6f+NativeEntranceMath.Rand(2260,i))%1f,ang=NativeEntranceMath.Rand(2260,i+40)*6.283f+u*3;float d=1.1f*(1-u);fx.Glow(Gate+new Vector3((float)Math.Cos(ang)*d*RX,(float)Math.Sin(ang)*d*RY,.05f),rim*(.7f*s*u),0,.05f,0,u,2260+i);}
                int n=40;Vector3 prev=Gate+new Vector3(RX,0,0);
                for(int i=1;i<=n;i++){float a=i/(float)n*6.2831853f;var p=Gate+new Vector3((float)Math.Cos(a)*RX,(float)Math.Sin(a)*RY,0);float run=.6f+.4f*(float)Math.Sin(a*3-t*6);fx.Streak(p,p-prev,Vector3.Lerp(rim,deep,.5f-.5f*(float)Math.Sin(a*2+t*4))*(s*run),.045f,0,1,0,0,2300+i);if(i%4==0)fx.Glow(p,deep*(.6f*s),0,.35f,0,0,2350+i);prev=p;}
            }
            // The cast: a void lance from the claw.
            var hand=Anchor(NativeEntranceAnchor.RightHand,Hit);var to=hand+ForwardOf(Hit)*.9f;float lance=1-NativeEntranceMath.Ramp(t,Hit+.04f,Hit+.28f);
            if(t>=Hit&&lance>.01f){fx.Streak(to,to-hand,NativeEntranceFx.Hex(0xf3e2ff,3)*lance,.05f,0,1,.4f,0,2400);fx.Streak(to,to-hand,rim*lance,.14f,0,1,.7f,0,2401);}
            NativeEntranceFx.Sparks(fx,t,2.95f,Gate,Vector3.Zero,1,30,2.6f,.7f,0,2.5f,rim,deep,.02f,2420,q,NativeEntranceStage.FloorY);
        }
        public override void Lights(float t,NativeEntranceLights l)
        {
            float key=.3f+.6f*NativeEntranceMath.Ramp(t,.15f,.6f);NativeEntranceStage.Rig(l,key,Accent,.8f,.5f);float s=Open(t);
            if(s>.01f)l.Add(Gate+new Vector3(0,0,.3f),NativeEntranceFx.Hex(0xa060ff,9*s),5);
            float f=NativeEntranceMath.Pulse(t,Hit,.015f,.22f);if(f>.01f)l.Add(Anchor(NativeEntranceAnchor.RightHand,Hit)+ForwardOf(Hit)*.6f,NativeEntranceFx.Hex(0xc890ff,30*f),6);
            l.Add(Anchor(NativeEntranceAnchor.Chest,t)+new Vector3(0,0,.35f),NativeEntranceFx.Hex(0x70e1d0,.9f),3.2f);
        }
        protected override NativeEntranceAnchor HitAnchor=>NativeEntranceAnchor.RightHand;
        protected override void Plan()
        {
            var to=Anchor(NativeEntranceAnchor.RightHand,Hit)+ForwardOf(Hit)*.9f;
            Blasts=new[]{new NativeEntranceBlast{At=Hit,Style=NativeBlastStyle.Void,Position=to,Radius=1.0f,Size=.45f},new NativeEntranceBlast{At=2.95f,Style=NativeBlastStyle.Implosion,Position=Gate,Radius=.9f,Size=.45f}};
        }
    }
}
