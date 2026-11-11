using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The blade classes' entrances (owner 2026-11-10: "give them both a very awesome intro with effects"; docs/KATANA.md, docs/FANGS.md "The
    /// intro"). Each plays the Frank Slash Pack's own clips on the model (Clip: which clip and where in it at intro time `t`; the runtime lays
    /// it on the intro body through the arena's motion, Runtime NativeVisualMotionPresentation), with a keyed stance under it for the Classic
    /// body, its camera, lights and effects. Presentation only: pure functions of the entrance clock, thinned by `quality`.
    /// </summary>
    public abstract class NativeBladeIntro:NativeProceduralEntrance
    {
        protected NativeBladeIntro(string style,float hit,int accent):base(style,hit,accent){}
        public abstract string Spec{get;}
        /// <summary>The pack clip the model plays at `t` and its time (false: the keyed stance).</summary>
        public abstract bool Clip(float t,out string clip,out float time);
        /// <summary>Whether the blade is put away at `t` (the katana in its saya).</summary>
        public virtual bool Sheathed(float t)=>false;
        /// <summary>A clip window `from`..`to` (clip s) laid across intro `t0`..`t1`, with `at` (clip s) landing on intro `hit`.</summary>
        protected static float Fit(float t,float t0,float hit,float t1,float from,float at,float to)
        {
            if(t<=hit)return from+(at-from)*NativeEntranceMath.Clamp01((t-t0)/Math.Max(1e-3f,hit-t0));
            return at+(to-at)*NativeEntranceMath.Clamp01((t-hit)/Math.Max(1e-3f,t1-hit));
        }
    }

    /// <summary>
    /// "Crimson Moon", the Katana's entrance. 7.2 s, the name at 5.4 s. Night; a crimson moon low behind the pilot, petals drifting. The pilot
    /// stands with the blade sheathed at the hip, settles, and draws: one great iai cut (slowed at its height) leaves a line of light hanging
    /// across the stage, the petals it crossed sliced and still. The blade is slid home; at the click the line splits, the moon flares and the
    /// petals burst. The hero: standing calm, the hand on the hilt.
    /// </summary>
    public sealed class NativeKatanaIntro:NativeBladeIntro
    {
        public const string Name="Crimson Moon",Music="audio/intros/arc-salute",Blurb="Under a crimson moon: one draw, a line of light, and the click that splits it.";
        public const float Total=7.2f,CardAt=5.4f,End=6.9f;
        public const float Poise=.9f,DrawAt=1.25f,CutAt=2.55f,HoldTo=3.4f,SheatheAt=3.5f,ClickAt=4.35f,Hero=4.7f;
        public override string Spec=>NativeKatana.Spec;
        public override float Length=>Total;public override float SettleAt=>End;protected override float HeroAt=>CardAt;
        private static readonly Vector3 Moon=new Vector3(1.8f,3.6f,-9f),White=new Vector3(1,1,1),Crimson=NativeEntranceFx.Hex(0xff3d6e),Blossom=NativeEntranceFx.Hex(0xffb3c6),Night=NativeEntranceFx.Hex(0x14101e);
        /// <summary>The line the cut leaves, across the stage at its height (stage frame).</summary>
        public static readonly Vector3 LineFrom=Local(-2.6f,1.25f,.9f),LineTo=Local(2.8f,1.05f,.35f);
        public NativeKatanaIntro():base(NativeKatana.Entrance,CutAt,0xff3d6e)
        {
            SlowRate=.25f;SlowHold=.18f;SlowRecover=.3f;SlowCatchUp=.5f;
            var stand=NativeEntranceKeys.Stand();var k=new NativeEntranceKeys();
            k.Key(0,stand).Root(0,.88f,0,Facing).Planted(1).J(LA,-.2f,0,.15f).J(RA,-.25f,0,-.2f);
            k.Key(CutAt,stand).Root(0,.84f,.1f,Facing).Planted(1).Drop(-.08f).J(B,.1f).J(RA,-1.4f,0,-.6f).J(LA,-.4f,0,.5f).ImpactKey();
            k.Key(Total,stand).Root(0,.88f,.1f,Facing).Planted(1).J(LA,-.2f,0,.15f).J(RA,-.3f,0,-.2f);Keys=k;
            Shots=new[]
            {
                // The moon over the pilot's shoulder, slow push in.
                Shot(0,NativeEntranceCut.Cut,46,40,V(-1.4f,1.3f,4.8f),V(-1.1f,1.35f,4.2f),V(.6f,1.9f,-3f),V(.2f,1.6f,-1f),1,0),
                // Close on the hand at the hilt as it settles.
                Shot(Poise,NativeEntranceCut.Cut,30,28,V(1.1f,1.05f,1.5f),V(1.0f,1.05f,1.35f),V(-.1f,.95f,0),V(-.1f,.95f,0),1,0),
                // Wide, low and side-on for the draw.
                Shot(DrawAt+.6f,NativeEntranceCut.Cut,52,48,V(-3.6f,.7f,2.4f),V(-3.3f,.75f,2.2f),V(0,1.1f,0),V(.4f,1.1f,0),1,0),
                // Behind the line as it hangs.
                Shot(CutAt+.35f,NativeEntranceCut.Whip,44,40,V(2.9f,1.6f,2.9f),V(2.6f,1.5f,2.6f),V(-.4f,1.15f,.2f),V(0,1.1f,0),1,0,.1f),
                // The click and the split, close and in front.
                Shot(SheatheAt,NativeEntranceCut.Cut,40,36,V(-.9f,1.05f,3.2f),V(-.75f,1.08f,2.9f),V(0,1.05f,0),V(0,1.1f,0),1,0),
                // The hero.
                Shot(Hero,NativeEntranceCut.Blend,38,34,V(-1.2f,1.1f,3.7f),V(-.9f,1.12f,3.3f),V(0,1.2f,0),V(0,1.22f,0),1,0,.4f),
                Shot(CardAt,NativeEntranceCut.Cut,36,33,V(-.85f,1.04f,3.5f),V(-.66f,1.06f,3.25f),V(-.08f,1.24f,0),V(-.1f,1.26f,0),1,0),
            };
        }
        public override bool Clip(float t,out string clip,out float time)
        {
            const string K="Frank_RPG_Katana_";
            // Sheathed and still, then the iai: the pack's slow stance-two draw (its cut on CutAt), the blade slid home (its click on ClickAt), calm.
            if(t<DrawAt){clip=K+"Unequip_Idle";time=t+.5f;return true;}
            if(t<SheatheAt){clip=K+"S2_Skill02";time=Fit(t,DrawAt,CutAt,SheatheAt,.85f,1.717f,2.5f);return true;}
            if(t<Hero){clip=K+"Unequip";time=Fit(t,SheatheAt,ClickAt,Hero,1.2f,1.85f,2.3f);return true;}
            clip=K+"Unequip_Idle";time=1.2f+(t-Hero);return true;
        }
        public override bool Sheathed(float t)=>t<CutAt-.18f||t>=ClickAt-.05f;
        public override Vector3 CameraJolt(float t)
        {
            float s=NativeEntranceMath.Pulse(t,CutAt,.01f,.35f)*.03f+NativeEntranceMath.Pulse(t,ClickAt,.01f,.3f)*.02f;
            return s<=0?Vector3.Zero:new Vector3((float)Math.Sin(t*71)*s,(float)Math.Sin(t*83+1)*s,(float)Math.Sin(t*59+2)*s);
        }
        public override void Lights(float t,NativeEntranceLights l)
        {
            // Moonlight: a crimson key from behind, a cold fill; the cut and the click flash white and crimson.
            float up=NativeEntranceMath.Ramp(t,Hero-.3f,CardAt);l.DriveProbe=true;NativeEntranceStage.Rig(l,.12f+.4f*up,Accent,.3f+.4f*up,.08f+.3f*up,.1f+.4f*up);
            l.Add(Moon*.5f+new Vector3(0,-.5f,0),Crimson*(2.4f+.8f*(float)Math.Sin(t*1.3f)),10);
            float cut=NativeEntranceMath.Pulse(t,CutAt,.01f,.5f),click=NativeEntranceMath.Pulse(t,ClickAt,.01f,.6f);
            if(cut>.01f)l.Add((LineFrom+LineTo)*.5f,(White*1.5f+Crimson)*(4*cut),6);if(click>.01f)l.Add(new Vector3(0,1.2f,0),(White+Crimson*2)*(4*click),7);
            if(t>=CardAt-.2f){float k=NativeEntranceMath.Ramp(t,CardAt-.2f,CardAt+.3f);l.Add(new Vector3(-2.2f,2.4f,2),Blossom*(1.6f*k),8);}
        }
        protected override float Pylon(int i,float t)=>t<ClickAt?.05f:PowerOn(i,t-ClickAt+.05f,.1f);
        public override void Props(float t,INativeEntranceProps props,float q)=>Stage(props,t,t<ClickAt?.03f:.1f+.25f*NativeEntranceMath.Ramp(t,ClickAt,CardAt),ClickAt,Accent);
        public override void Effects(float t,INativeEntranceFx fx,float q)
        {
            float floor=NativeEntranceStage.FloorY;bool low=q<.5f;
            // The moon: a great crimson disc low behind the stage, its halo breathing, flaring at the split.
            float flare=NativeEntranceMath.Pulse(t,ClickAt,.02f,.9f);fx.Glow(Moon,Crimson*(1.6f+2.4f*flare),0,3.2f+.8f*flare,2,0,21000);fx.Glow(Moon,(Crimson*.5f+White*.5f)*(1.2f+1.5f*flare),0,2.1f,1,0,21001);
            // Petals drifting across the night (fewer on Reduced), falling faster once the line splits.
            int n=low?18:46;for(int i=0;i<n;i++)
            {
                float life=5.5f,born=NativeEntranceMath.Rand(21100,i)*life,a=((t-born)%life+life)%life/life;
                var p=new Vector3(-4.5f+9*a+NativeEntranceMath.Signed(21100,i+50)*.8f,3.2f-2.8f*a+.3f*(float)Math.Sin(t*2+i),-2.5f+5*NativeEntranceMath.Rand(21100,i+90));
                // Sliced petals: the ones the line crosses freeze a moment after the cut.
                fx.Glow(p,Blossom*(1.1f*(1-a)),0,.05f,3,a,21110+i);
            }
            // The draw: the edge's streak across, then the line hanging, trembling brighter toward the click; the split breaks it into motes and petals.
            {
                float s=t-CutAt;
                if(s>=-.05f&&s<ClickAt-CutAt)
                {
                    float grow=NativeEntranceMath.Clamp01((s+.05f)/.12f),near=NativeEntranceMath.Ramp(t,SheatheAt,ClickAt);int m=low?16:40;
                    for(int i=0;i<m;i++){float u=i/(float)(m-1);if(u>grow)break;var p=Vector3.Lerp(LineFrom,LineTo,u);float edge=1-Math.Abs(u-.5f)*1.4f;fx.Streak(p,Vector3.Normalize(LineTo-LineFrom)*2,(White*.8f+Crimson)*((1.6f+1.6f*near)*edge),.02f+.02f*near,.15f,.02f,.4f,0,21300+i);}
                    if(s<.25f){float u=s/.25f;fx.Glow(Vector3.Lerp(LineFrom,LineTo,.5f),White*(4*(1-u)),0,.6f+1.2f*u,2,u,21360);}
                }
                float c=t-ClickAt;
                if(c>=0&&c<1.5f)
                {
                    float u=c/1.5f;int m=low?24:64;
                    for(int i=0;i<m;i++){float w=NativeEntranceMath.Rand(21400,i);var p=Vector3.Lerp(LineFrom,LineTo,w);var d=new Vector3(NativeEntranceMath.Signed(21400,i+40),.4f+NativeEntranceMath.Rand(21400,i+80),NativeEntranceMath.Signed(21400,i+120));fx.Glow(p+d*(1.3f*c)+new Vector3(0,-.6f*c*c,0),(i%3==0?Blossom:Crimson)*(2.2f*(1-u)),0,.06f,3,u,21410+i);}
                    if(c<.35f){float v=c/.35f;fx.Glow(Vector3.Lerp(LineFrom,LineTo,.5f),(White+Crimson)*(5*(1-v)),0,.8f+2.6f*v,2,v,21500);}
                    // The click's ring at the hip and its shockwave over the floor.
                    if(c<.5f){float v=c/.5f;int r=low?18:40;var hip=new Vector3(.15f,.95f,.05f);for(int i=0;i<r;i++){float a=i*6.2831853f/r;var d=new Vector3((float)Math.Sin(a),0,(float)Math.Cos(a));fx.Streak(new Vector3(0,floor+.03f,0)+d*(.3f+3.4f*(1-(1-v)*(1-v))),d*5,Crimson*(2*(1-v)),.025f,.15f,.03f,.7f,v,21520+i);}fx.Glow(hip,(White+Crimson)*(3*(1-v)),0,.3f+.5f*v,2,v,21580);}
                }
            }
            // Mist low over the floor all through, parting at the cut.
            int mist=low?6:14;for(int i=0;i<mist;i++){float a=((t*.12f+NativeEntranceMath.Rand(21600,i))%1+1)%1;var p=new Vector3(-4+8*NativeEntranceMath.Rand(21600,i+20),floor+.12f,-3+6*a);fx.Glow(p,Night*2+Crimson*.15f,0,1.4f,0,a,21610+i);}
        }
    }

    /// <summary>
    /// "Silent Kill", the Fangs' entrance. 7 s, the name at 5.2 s. Darkness and a low green mist; two points of venom-green light, then the
    /// pilot forms out of smoke, crouched. A shadow step: gone in a puff, back a stride ahead. The twin daggers in a flurry (green trails,
    /// venom flung), a leap and a two-handed strike into the floor: the venom blooms in a great green burst. The hero: the daggers held low,
    /// venom dripping, smoke curling up round the legs.
    /// </summary>
    public sealed class NativeFangsIntro:NativeBladeIntro
    {
        public const string Name="Silent Kill",Music="audio/intros/pulse-wave",Blurb="Out of the smoke: a shadow step, a flurry of fangs and a bloom of venom.";
        public const float Total=7f,CardAt=5.2f,End=6.7f;
        public const float FormAt=.55f,StepAt=1.6f,ArriveAt=1.85f,FlurryAt=1.95f,StrikeAt=4.25f,Hero=4.65f;
        public override string Spec=>NativeFangs.Spec;
        public override float Length=>Total;public override float SettleAt=>End;protected override float HeroAt=>CardAt;
        private static readonly Vector3 Venom=NativeEntranceFx.Hex(0x7dff3a),White=new Vector3(1,1,1),Deep=NativeEntranceFx.Hex(0x0c2408),Smoke=NativeEntranceFx.Hex(0x22262a);
        public static readonly Vector3 Before=Local(0,0,-1.4f),After=Local(0,0,0);
        public NativeFangsIntro():base(NativeFangs.Entrance,StrikeAt,0x7dff3a)
        {
            SlowRate=.3f;SlowHold=.14f;SlowRecover=.25f;SlowCatchUp=.6f;
            var stand=NativeEntranceKeys.Stand();var k=new NativeEntranceKeys();
            k.Key(0,stand).Root(Before.X,.7f,Before.Z,Facing).Planted(1).Drop(-.18f).J(B,.25f).J(LA,-.5f,0,.4f).J(RA,-.5f,0,-.4f);
            k.Key(StepAt,stand).Root(Before.X,.7f,Before.Z,Facing).Planted(1).Drop(-.18f).J(B,.25f);
            k.Key(ArriveAt,stand).Root(After.X,.78f,After.Z,Facing).Planted(1).Drop(-.1f).J(B,.15f);
            k.Key(Total,stand).Root(After.X,.84f,After.Z,Facing).Planted(1).Drop(-.04f).J(B,.08f).J(LA,-.3f,0,.3f).J(RA,-.3f,0,-.3f);Keys=k;
            Shots=new[]
            {
                // Low over the mist: the eyes in the dark.
                Shot(0,NativeEntranceCut.Cut,44,40,V(.4f,.5f,4.4f),V(.3f,.55f,3.9f),V(0,.75f,-1.4f),V(0,.8f,-1.2f),1,0),
                // The shadow step: wide and to the side.
                Shot(StepAt-.15f,NativeEntranceCut.Cut,50,46,V(-3.2f,1.2f,1.8f),V(-3f,1.25f,1.6f),V(0,.9f,-.8f),V(0,.95f,-.2f),1,0),
                // Close and circling through the flurry.
                Shot(FlurryAt+.1f,NativeEntranceCut.Whip,40,36,V(1.8f,1.1f,2.2f),V(-1.2f,1.2f,2.4f),V(0,1f,0),V(0,1f,0),1,0,.12f),
                // High over the strike and its bloom.
                Shot(StrikeAt-.3f,NativeEntranceCut.Cut,54,58,V(.3f,3.4f,3.4f),V(.6f,3.8f,3.8f),V(0,.4f,0),V(0,.3f,0),1,0),
                // The hero.
                Shot(Hero,NativeEntranceCut.Blend,40,36,V(-1.1f,.95f,3.6f),V(-.85f,1f,3.25f),V(0,1.05f,0),V(-.05f,1.08f,0),1,0,.4f),
                Shot(CardAt,NativeEntranceCut.Cut,36,33,V(-.8f,.98f,3.45f),V(-.64f,1.01f,3.2f),V(-.08f,1.12f,0),V(-.1f,1.14f,0),1,0),
            };
        }
        /// <summary>Unseen until it forms out of the smoke; gone through the step.</summary>
        public override float Solid(float t,int bone)=>t<FormAt?0:t>=StepAt+.04f&&t<ArriveAt-.03f?0:1;
        public override bool Clip(float t,out string clip,out float time)
        {
            const string A="Frank_RPG_Assassin_";
            if(t<StepAt){clip="Frank_Stealth_Kill_Sneak_Idle";time=t;return true;}
            if(t<FlurryAt){clip=A+"Evade_F_IP";time=Fit(t,StepAt,ArriveAt,FlurryAt,.05f,.35f,.5f);return true;}
            // The flurry (the pack's fifth combo, its three cuts), then the two-handed strike (its blow on StrikeAt), then the stance.
            if(t<StrikeAt-.45f){clip=A+"Combo05_All_IP";time=Fit(t,FlurryAt,FlurryAt+.85f,StrikeAt-.45f,.35f,1.217f,1.95f);return true;}
            if(t<Hero+.3f){clip=A+"Skill02_IP";time=Fit(t,StrikeAt-.45f,StrikeAt,Hero+.3f,.45f,.9f,1.6f);return true;}
            clip=A+"Equip_Idle";time=t-Hero;return true;
        }
        public override Vector3 CameraJolt(float t)
        {
            float s=NativeEntranceMath.Pulse(t,StrikeAt,.01f,.4f)*.04f+NativeEntranceMath.Pulse(t,ArriveAt,.01f,.2f)*.01f;
            return s<=0?Vector3.Zero:new Vector3((float)Math.Sin(t*71)*s,(float)Math.Sin(t*83+1)*s,(float)Math.Sin(t*59+2)*s);
        }
        public override void Lights(float t,NativeEntranceLights l)
        {
            float up=NativeEntranceMath.Ramp(t,StrikeAt,CardAt);l.DriveProbe=true;NativeEntranceStage.Rig(l,.05f+.45f*up,Accent,.25f+.5f*up,.06f+.35f*up,.08f+.45f*up);
            l.Add(new Vector3(0,.3f,0),Venom*(.8f+.4f*(float)Math.Sin(t*2.1f)),5);
            float strike=NativeEntranceMath.Pulse(t,StrikeAt,.01f,.8f);if(strike>.01f)l.Add(new Vector3(0,.5f,.4f),(Venom*2+White)*(4*strike),8);
            if(t>=CardAt-.2f){float k=NativeEntranceMath.Ramp(t,CardAt-.2f,CardAt+.3f);l.Add(new Vector3(2.2f,2.2f,1.8f),Venom*(1.8f*k),8);}
        }
        protected override float Pylon(int i,float t)=>t<StrikeAt?.04f:PowerOn(i,t-StrikeAt+.05f,.12f);
        public override void Props(float t,INativeEntranceProps props,float q)=>Stage(props,t,t<StrikeAt?.02f:.08f+.3f*NativeEntranceMath.Ramp(t,StrikeAt,CardAt),StrikeAt,Accent);
        public override void Effects(float t,INativeEntranceFx fx,float q)
        {
            float floor=NativeEntranceStage.FloorY;bool low=q<.5f;
            // The low venom mist, always.
            int mist=low?8:20;for(int i=0;i<mist;i++){float a=((t*.18f+NativeEntranceMath.Rand(22000,i))%1+1)%1;var p=new Vector3(-4+8*NativeEntranceMath.Rand(22000,i+20),floor+.1f+.15f*a,-3+6*NativeEntranceMath.Rand(22000,i+40));fx.Glow(p,Deep*2+Venom*(.25f*(1-a)),0,1.3f,0,a,22010+i);}
            // Two eyes in the dark before it forms.
            if(t<FormAt+.2f){float k=NativeEntranceMath.Ramp(t,.1f,.35f)*(1-NativeEntranceMath.Ramp(t,FormAt,FormAt+.2f));var head=Before+new Vector3(0,1.25f,0);fx.Glow(head+Local(-.05f,0,.12f),Venom*(3*k),0,.05f,1,0,22100);fx.Glow(head+Local(.05f,0,.12f),Venom*(3*k),0,.05f,1,0,22101);}
            // The smoke it forms out of, and the step's two puffs.
            Puff(fx,t,FormAt,Before+new Vector3(0,.8f,0),low,22200);Puff(fx,t,StepAt,Before+new Vector3(0,.8f,0),low,22300);Puff(fx,t,ArriveAt-.03f,After+new Vector3(0,.8f,0),low,22400);
            // The flurry: venom flung off the blades at each cut.
            foreach(var cut in new[]{FlurryAt+.45f,FlurryAt+.85f,FlurryAt+1.45f})
            {
                float s=t-cut;if(s<0||s>.7f)continue;int m=low?10:24;
                for(int i=0;i<m;i++){var d=new Vector3(NativeEntranceMath.Signed(22500+(int)(cut*100),i),.3f+.5f*NativeEntranceMath.Rand(22500,i),NativeEntranceMath.Signed(22540,i));var p=new Vector3(0,1.1f,0)+d*(2.2f*s)+new Vector3(0,-4.5f*s*s,0);fx.Glow(p,Venom*(2*(1-s/.7f)),0,.04f,3,s/.7f,22510+i+(int)(cut*1000)%400);}
            }
            // The strike: the bloom: a great green burst, petals of venom, a shockwave over the floor, the mist blown out.
            {
                float s=t-StrikeAt;if(s>=0&&s<1.6f)
                {
                    float u=s/1.6f;fx.Glow(new Vector3(0,.4f,0),(Venom*2+White*.5f)*(5*(1-NativeEntranceMath.Clamp01(s/.4f))),0,.8f+3.5f*NativeEntranceMath.Clamp01(s/.4f),2,u,22600);
                    int petals=low?10:22;for(int i=0;i<petals;i++){float a=i*6.2831853f/petals;var d=new Vector3((float)Math.Sin(a),0,(float)Math.Cos(a));float r=.3f+2.6f*(1-(1-u)*(1-u));for(int j=0;j<3;j++){var p=new Vector3(0,floor+.05f+.25f*j,0)+d*(r*(1-.15f*j));fx.Streak(p,d*4+new Vector3(0,2,0),Venom*(2.4f*(1-u)),.04f,.25f,.03f,.6f,u,22620+i*3+j);}}
                    int ring=low?20:48;for(int i=0;i<ring;i++){float a=i*6.2831853f/ring;var d=new Vector3((float)Math.Sin(a),0,(float)Math.Cos(a));fx.Streak(new Vector3(0,floor+.03f,0)+d*(.4f+4.2f*(1-(1-u)*(1-u))),d*6,Venom*(1.8f*(1-u)),.03f,.2f,.03f,.7f,u,22700+i);}
                }
            }
            // The hero: venom dripping from both blades, smoke curling round the legs.
            if(t>=Hero)
            {
                int m=low?6:14;for(int i=0;i<m;i++){float born=Hero+(i/(float)m)*1.4f,a=((t-born)%1.4f+1.4f)%1.4f/1.4f;var hand=Anchor(i%2==0?NativeEntranceAnchor.RightHand:NativeEntranceAnchor.LeftHand,t);fx.Glow(hand+new Vector3(0,-.25f-.6f*a*a,0),Venom*(1.6f*(1-a)),0,.03f,3,a,22800+i);}
                int sm=low?5:12;for(int i=0;i<sm;i++){float a=((t*.5f+NativeEntranceMath.Rand(22900,i))%1+1)%1;var p=After+new Vector3(NativeEntranceMath.Signed(22900,i)*.5f,.1f+1.2f*a,NativeEntranceMath.Signed(22920,i)*.5f);fx.Glow(p,Smoke*1.5f,0,.4f+.3f*a,0,a,22910+i);}
            }
        }
        private static void Puff(INativeEntranceFx fx,float t,float at,Vector3 c,bool low,int seed)
        {
            float s=t-at;if(s<0||s>.9f)return;float u=s/.9f;int n=low?8:18;
            for(int i=0;i<n;i++){var d=new Vector3(NativeEntranceMath.Signed(seed,i),NativeEntranceMath.Signed(seed,i+30)*.6f,NativeEntranceMath.Signed(seed,i+60));var p=c+d*(.2f+.8f*(1-(1-u)*(1-u)))+new Vector3(0,.4f*u,0);fx.Glow(p,Smoke*1.8f+Venom*(.25f*(1-u)),0,.35f+.5f*u,0,u,seed+i);}
            if(s<.2f)fx.Glow(c,Venom*(3*(1-s/.2f)),0,.4f+.4f*s/.2f,2,s/.2f,seed+90);
        }
    }
}
