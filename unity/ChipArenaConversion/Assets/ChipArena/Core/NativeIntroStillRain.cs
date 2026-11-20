using System;
using System.Numerics;
using static ChipArena.UnityConversion.NativeIntroMelee;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// "Still Rain", the Katana's intro (intros v2; docs/INTROS-MELEE.md): rain, stillness and one cut. 8.2 s, the name at 6.6 s. Night in
    /// the Rain Garden (a walled stone courtyard: the granite path between two rain pools, the roofed gate behind, the stone lantern, bamboo
    /// over the walls). The pilot stands in the rain with the blade sheathed, the left hand on the saya; far lightning lights the gate; it
    /// sinks into the iai stance, the thumb pushes the guard, and the one cut (the pack's stance-three draw): the rain stops in the air. In
    /// the frozen moment the camera rides the cut's line through the hanging drops, every drop on it split in two. Time comes back: the
    /// drops fall apart, three bamboo stalks behind slide on their cut and fall onto the wall, the line breaks into falling light. The blade
    /// goes home; at the click the rain is thrown off the pilot. Still again under the name.
    /// The model plays the Frank Slash Pack's Katana clips (NativeBladeIntro); the rain and the freeze are the script's own clock.
    /// Presentation only: pure functions of the presentation clock.
    /// </summary>
    public sealed class NativeIntroStillRain:NativeIntroMeleeBlade
    {
        public const string Title="Still Rain";
        public const float Total=8.2f,CardAt=6.6f;
        /// <summary>The beats (presentation s): the far lightning, the iai's stance, the hand taking the blade out of the saya, the cut, the
        /// freeze and its end, the stalks' slide, the sheathing, its click, the stillness under the name.</summary>
        public const float LightningAt=1.35f,DrawAt=2.2f,GrabAt=2.85f,CutAt=3.3f,FreezeAt=3.36f,ResumeAt=4.45f,SlideAt=4.5f,SheatheAt=4.95f,ClickAt=6.05f,StillAt=6.3f;
        // The iai's clip times (Frank_RPG_Katana_S3_Skill01): the blade out of the saya, the cut, held a little past it, its end.
        private const float IaiCut=1.3f,IaiHeld=1.38f,IaiEnd=2.05f,SheatheFrom=.35f,SheatheEnd=2.3f;
        private static readonly Vector3 Crimson=NativeEntranceFx.Hex(0xff3d6e),Rain=new Vector3(.52f,.6f,.74f),Night=NativeEntranceFx.Hex(0x070a12),Lamp=NativeEntranceFx.Hex(0xffb35c),
            Moon=NativeEntranceFx.Hex(0x9fb4e0),Paper=NativeEntranceFx.Hex(0xffd7a0),Steel=NativeEntranceFx.Hex(0xcfe2ff),Mist=new Vector3(.3f,.34f,.42f);
        public override string Spec=>NativeKatana.Spec;

        // ---- the cut: a plane through the courtyard (source frame), rising toward the pilot's left; its line runs from the pilot's right
        // front past its front to the bamboo behind on its left ----
        /// <summary>The cut plane's height at (x, z): Base + Rise x + Back z (source frame, on the stage floor).</summary>
        public const float PlaneBase=.95f,PlaneRise=.28f,PlaneBack=.08f;
        public static float PlaneY(float x,float z)=>NativeEntranceStage.FloorY+PlaneBase+PlaneRise*x+PlaneBack*z;
        public static readonly Vector3 PlaneNormal=Vector3.Normalize(new Vector3(-PlaneRise,1,-PlaneBack));
        private static readonly Vector3 LineFrom=OnPlane(-1.6f,1.2f),LineDir=Vector3.Normalize(OnPlane(2.8f,-3.1f)-OnPlane(-1.6f,1.2f));
        private static Vector3 OnPlane(float x,float z)=>new Vector3(x,PlaneY(x,z),z);
        /// <summary>The cut's line at `s` m from its start.</summary>
        public static Vector3 LineAt(float s)=>LineFrom+LineDir*s;
        private const float LineLength=8.2f;
        /// <summary>The side of the line away from the pilot (horizontal, unit): where the camera rides.</summary>
        private static readonly Vector3 Side=AwayFromPilot();
        private static Vector3 AwayFromPilot(){var s=Vector3.Normalize(Vector3.Cross(LineDir,Vector3.UnitY));return Vector3.Dot(s,-LineFrom)>0?-s:s;}

        // ---- the three bamboo stalks the cut takes (the set's rigs bamboo-cut-0..2: Unity x, z; the cut's height on the plane) ----
        private static readonly Vector2[] Stalks={new Vector2(-2.35f,-3.0f),new Vector2(-2.85f,-3.55f),new Vector2(-3.3f,-2.75f)};
        /// <summary>How far each falls (rad from upright) to rest on the wall's cap behind it.</summary>
        private static readonly float[] Lean={1.31f,1.30f,1.37f};
        private static readonly Vector3 FallDir=Vector3.Normalize(new Vector3(.25f,0,-1));

        public NativeIntroStillRain():base(NativeIntroCatalog.WeaponPrefix+NativeKatana.Spec,Total,CardAt,CutAt,0xff3d6e)
        {
            // The keyed rig (the anchors and the Classic body): standing, then the iai's crouch, then standing.
            var stand=NativeEntranceKeys.Stand();var k=new NativeEntranceKeys();
            k.Key(0,stand).Root(0,.88f,0,Facing).Planted(1).J(LA,-.25f,0,.25f).J(LE,-.9f).J(RA,-.2f,0,-.15f);
            k.Key(DrawAt,stand).Root(0,.88f,0,Facing).Planted(1).J(LA,-.25f,0,.25f).J(LE,-.9f).J(RA,-.2f,0,-.15f);
            k.Key(GrabAt,stand).Root(0,.8f,.05f,Facing+.2f).Planted(1).Drop(-.08f).J(B,.12f).J(LA,-.35f,0,.3f).J(LE,-1.0f).J(RA,-.9f,0,.4f).J(RE,-1.2f);
            k.Key(CutAt,stand).Root(0,.8f,.12f,Facing-.15f).Planted(1).Drop(-.1f).J(B,.1f).J(RA,-1.45f,0,-.7f).J(LA,-.4f,0,.45f).ImpactKey();
            k.Key(SheatheAt,stand).Root(0,.84f,.12f,Facing).Planted(1).Drop(-.05f).J(RA,-.9f,0,-.4f).J(LA,-.3f,0,.3f);
            k.Key(StillAt,stand).Root(0,.88f,.12f,Facing).Planted(1).J(LA,-.25f,0,.25f).J(LE,-.9f).J(RA,-.25f,0,-.15f);
            k.Key(Total,stand).Root(0,.88f,.12f,Facing).Planted(1).J(LA,-.25f,0,.25f).J(LE,-.9f).J(RA,-.25f,0,-.15f);Keys=k;
            // The camera: locked off in the rain; low in front of its left for the stance; close on the saya's mouth; the cut seen wide;
            // the frozen moment ridden along the cut's line; time back, wide on the stalks; the sheathing; the click low; the hero.
            // (the glide ends short of the bamboo clump behind on the left, looking on to the stalks the cut takes)
            var a=LineAt(-1.4f)+Side*.8f+new Vector3(0,.22f,0);var b=LineAt(.8f)+Side*.85f+new Vector3(0,.2f,0);var c=LineAt(2.6f)+Side*.9f+new Vector3(0,.16f,0);var d=LineAt(3.6f)+Side*.95f+new Vector3(0,.12f,0);
            Rig.Dolly(0,V(-.55f,1.45f,6.2f),V(-.5f,1.42f,5.75f),V(.1f,1.15f,-1.5f),V(.1f,1.12f,-1.4f),40,38).Ease(NativeIntroEase.Linear)
               .Dolly(DrawAt-.05f,V(1.75f,.72f,2.45f),V(1.3f,.8f,1.85f),V(.15f,.95f,0),V(.2f,1.0f,0),42,37).Ease(NativeIntroEase.Out)
               .Dolly(GrabAt-.08f,V(.92f,1.02f,.98f),V(.78f,1.0f,.82f),V(.2f,.92f,.12f),V(.22f,.95f,.12f),26,24).Focus(0,0,.55f)
               .Hold(CutAt-.04f,V(-2.7f,.95f,1.9f),V(.35f,1.15f,-.25f),46)
               .Spline(FreezeAt,new[]{a,b,c,d},LineAt(1.4f)+new Vector3(0,.05f,0),LineAt(6.4f),36,31).Ease(NativeIntroEase.InOut)
               .Dolly(ResumeAt,V(-1.65f,1.25f,3.95f),V(-1.75f,1.3f,4.15f),V(1.3f,1.75f,-2.5f),V(1.5f,1.6f,-2.7f),45,44)
               .Dolly(SheatheAt,V(1.6f,1.15f,1.8f),V(1.3f,1.12f,1.52f),V(.1f,1.0f,.05f),V(.12f,1.02f,.05f),36,33)
               .Dolly(ClickAt-.05f,V(.45f,.34f,3.05f),V(.38f,.38f,2.8f),V(0,.9f,0),V(0,.95f,0),40,38);
            HeroFrame(StillAt,1,1.15f,3.3f,31,.5f);
            Rig.Bars(0,1).Bars(CardAt-.4f,.5f).Black(0,1).Black(.05f,0);
        }
        public override string SetBank=>Bank;

        // ---- the model's clips (the Katana's set: NativeBladeMotion.KatanaExtra) ----
        public override bool Clip(float t,out string clip,out float time)
        {
            const string K="Frank_RPG_Katana_";
            if(t<DrawAt){clip=K+"Unequip_Idle";time=.5f+t;return true;}
            if(t<SheatheAt)
            {
                clip=NativeBladeMotion.KatanaIai;
                // The stance and the hand on the hilt at the clip's pace, the cut fast, held just past it through the freeze, then on to its end.
                if(t<GrabAt)time=NativeKatanaSaya.IaiDraw*NativeEntranceMath.Clamp01((t-DrawAt)/(GrabAt-DrawAt));
                else if(t<CutAt)time=NativeEntranceMath.Lerp(NativeKatanaSaya.IaiDraw,IaiCut,NativeEntranceMath.EaseIn(NativeEntranceMath.Clamp01((t-GrabAt)/(CutAt-GrabAt)))*.35f+.65f*NativeEntranceMath.Clamp01((t-GrabAt)/(CutAt-GrabAt)));
                else if(t<ResumeAt)time=IaiCut+(IaiHeld-IaiCut)*NativeEntranceMath.Smooth((t-CutAt)/(FreezeAt-CutAt));
                else time=NativeEntranceMath.Lerp(IaiHeld,IaiEnd,NativeEntranceMath.Clamp01((t-ResumeAt)/(SheatheAt-ResumeAt)));
                return true;
            }
            if(t<StillAt){clip=NativeBladeMotion.KatanaSheathe;time=Fit(t,SheatheAt,ClickAt,StillAt,SheatheFrom,NativeKatanaSaya.SheatheClick,SheatheEnd);return true;}
            clip=K+"Unequip_Idle";time=1.2f+(t-StillAt);return true;
        }
        public override bool Sheathed(float t)=>t<GrabAt||t>=ClickAt-.02f;
        public override float Fade(float t)=>t>=DrawAt-.02f&&t<DrawAt+.45f?.3f:t>=SheatheAt-.02f&&t<SheatheAt+.3f?.22f:t>=StillAt-.02f&&t<StillAt+.4f?.3f:.12f;

        // ---- the rain's own clock: it stops in the freeze ----
        /// <summary>The rain's time at `t`: the clock, stopped from the freeze to the resume (and carried on from there).</summary>
        public static float RainTime(float t)=>t<FreezeAt?t:t<ResumeAt?FreezeAt:t-(ResumeAt-FreezeAt);
        /// <summary>How still the world is (1 in the freeze, eased in over its first moment).</summary>
        public static float Frozen(float t)=>t<FreezeAt||t>=ResumeAt?0:NativeEntranceMath.Smooth((t-FreezeAt)/.05f);
        private const float Top=7.5f,Fall=8.2f;private static readonly Vector3 Wind=new Vector3(.35f,0,.15f);
        private const int Dense=230,Sparse=110,Drops=Dense+Sparse;
        /// <summary>The floor under (x, z) (source frame): the path's top, the pools' water, the moss.</summary>
        private static float FloorAt(float x,float z)
        {
            float ax=Math.Abs(x),y=NativeEntranceStage.FloorY;
            if(ax<.82f)return y;if(ax<3.4f&&z>-7.1f&&z<5.8f)return y-.055f;return y-.01f;
        }
        /// <summary>Drop `i` at rain time `r`: where it is, whether it is falling (else splashing, `s` s after it landed), and its cycle.</summary>
        private static bool Drop(int i,float r,out Vector3 at,out float s,out int cycle)
        {
            float phase=NativeEntranceMath.Rand(30011,i);float period=(Top/Fall)+.14f+.35f*NativeEntranceMath.Rand(30013,i);
            float u=r/period+phase;cycle=(int)Math.Floor(u);float local=(u-cycle)*period;
            int h=i*131+cycle*7919;float x,z;
            if(i<Dense){x=-4f+9f*NativeEntranceMath.Rand(30017,h);z=-5.5f+9.5f*NativeEntranceMath.Rand(30019,h);}
            else{x=-7.6f+15.2f*NativeEntranceMath.Rand(30017,h);z=-7.2f+13.7f*NativeEntranceMath.Rand(30019,h);}
            float floor=FloorAt(x,z),fall=(Top-floor)/Fall;
            if(local<fall){at=new Vector3(x+Wind.X*local,Top-Fall*local,z+Wind.Z*local);s=0;return true;}
            at=new Vector3(x+Wind.X*fall,floor,z+Wind.Z*fall);s=local-fall;return false;
        }

        // ---- the bamboo the cut takes ----
        /// <summary>Stalk `k`'s top at `t`: where its cut point is and how it is turned (source frame).</summary>
        private static void StalkTop(int k,float t,out Vector3 at,out Quaternion turn)
        {
            var p=Stalks[k];float x=-p.X,z=p.Y;at=new Vector3(x,PlaneY(x,z),z);turn=Quaternion.Identity;
            float from=SlideAt+.09f*k,s=t-from;if(s<=0)return;
            // A slide down the cut (the plane's fall line), then the fall about the cut's lip onto the wall's cap, a small bounce, rest.
            var down=Vector3.Normalize(new Vector3(PlaneNormal.X,0,PlaneNormal.Z));var downPlane=Vector3.Normalize(down-PlaneNormal*Vector3.Dot(down,PlaneNormal));
            at+=downPlane*(.14f*NativeEntranceMath.EaseIn(NativeEntranceMath.Clamp01(s/.28f)));
            float f=s-.18f;if(f<=0)return;float lean=Lean[k];float rise=(float)Math.Sqrt(2*lean/2.4f);
            float a=f<rise?.5f*2.4f*f*f:lean-.07f*lean*(float)(Math.Exp(-6*(f-rise))*Math.Sin((f-rise)*16));
            var axis=Vector3.Normalize(Vector3.Cross(Vector3.UnitY,FallDir));turn=Quaternion.CreateFromAxisAngle(axis,Math.Min(lean*1.05f,a));
        }

        // ---- the set ----
        protected override bool Scene(float t,INativeIntroSet set)
        {
            if(!set.Has("rain/static"))return false;
            // The lantern's flame and the veranda's paper (the glow sub-mesh): steady, a breath of flicker (none in Reduced effects).
            float fl=Context.Reduced?1:.96f+.04f*(float)Math.Sin(t*7.3f)*(float)Math.Sin(t*3.1f+1);
            set.Piece("rain/static",SetAt,Quaternion.Identity,1,White,White*fl);
            for(int k=0;k<Stalks.Length;k++){StalkTop(k,t,out var at,out var turn);set.Piece(stalkProps[k],at,turn,1,White,White);}
            return true;
        }
        private static readonly string[] stalkProps={"rain/bamboo-cut-0","rain/bamboo-cut-1","rain/bamboo-cut-2"};
        protected override void Fallback(float t,INativeIntroSet set)=>Plain(set,NativeEntranceFx.Hex(0x161a22),Moon,1,NativeEntranceFx.Hex(0x31405e));

        // ---- the light ----
        public override void Lights(float t,NativeEntranceLights l)
        {
            l.Clear();l.Background=Night;l.Fog=NativeEntranceFx.Hex(0x0b1019);l.FogNear=7;l.FogFar=34;l.DriveProbe=true;
            float bolt=Flash(t,LightningAt,.02f,.12f)+(Context.Reduced?0:.55f*NativeEntranceMath.Pulse(t,LightningAt+.13f,.015f,.08f));
            l.Sky=NativeEntranceFx.Hex(0x2a3550,.34f+.5f*bolt);l.Ground=NativeEntranceFx.Hex(0x0e0f14,.3f);
            // Moonlight from behind and above the gate, cold; the veranda's paper behind the lens, warm and low.
            l.Directional(new Vector3(.4f,1.0f,-.85f),Moon*(.42f+1.6f*bolt));
            l.Directional(new Vector3(-.15f,.35f,1),Paper*.12f);
            float fl=Context.Reduced?1:.93f+.07f*(float)Math.Sin(t*9.1f)*(float)Math.Sin(t*4.3f+.7f);
            l.Add(U(2.55f,1.2f,-1.9f),Lamp*(2.3f*fl),5.5f);
            l.Add(new Vector3(0,1.7f,8.4f),Paper*3.2f,13);
            l.Add(new Vector3(.6f,2.7f,-3.4f),Moon*2.4f,6.5f);
            if(bolt>.01f)l.Add(new Vector3(-2,10,-15),new Vector3(.8f,.88f,1)*(40*bolt),40);
            // The cut's flash at its line, the freeze's cold breath on the blade, the click's crimson at the hip, the crimson under the name.
            float cut=Flash(t,CutAt,.01f,.25f),still=Frozen(t),click=Flash(t,ClickAt,.01f,.3f),name=NativeEntranceMath.Ramp(t,CardAt-.25f,CardAt+.35f);
            if(cut>.01f)l.Add(LineAt(1.8f),(White*1.6f+Crimson)*(4*cut),6);
            if(still>.01f)l.Add(LineAt(2.0f)+new Vector3(0,.2f,0),(Steel*.8f+Crimson*.4f)*(1.6f*still),5);
            if(click>.01f)l.Add(new Vector3(.2f,1.0f,.2f),(Crimson*2+White)*(2.6f*click),4);
            if(name>.01f)l.Add(new Vector3(-1.9f,2.2f,2.1f),Crimson*(1.4f*name),8);
        }

        // ---- the effects ----
        public override void Effects(float t,INativeEntranceFx fx,float q)
        {
            Look(t);bool low=q<.5f||Context.Low;float r=RainTime(t),still=Frozen(t);
            float bolt=Flash(t,LightningAt,.02f,.12f);var rain=Rain*(.34f+.9f*bolt);
            // The rain: drops falling with the wind, splashing where they land; in the freeze each hangs where it was, and every drop on the
            // cut's plane (near its line) is split in two; just after, those halves fall apart.
            int n=Thin(Drops,q,60);float after=t-ResumeAt;
            for(int i=0;i<n;i++)
            {
                int idx=i*Drops/n;bool falling=Drop(idx,r,out var at,out float s,out int cycle);
                if(falling)
                {
                    float d=at.Y-PlaneY(at.X,at.Z);var toLine=at-LineFrom;float along=Vector3.Dot(toLine,LineDir);float off=(toLine-LineDir*along).Length();
                    bool onCut=Math.Abs(d)<.05f&&along>-.5f&&along<LineLength&&off<1.1f&&cycle==CycleAtFreeze(idx);
                    if(still>0)
                    {
                        if(onCut){var nrm=PlaneNormal*.018f;fx.Streak(at+nrm,new Vector3(0,.035f,0),rain*1.6f+Crimson*.25f,.0045f,.03f,1,.5f,0,31000+i);fx.Streak(at-nrm,new Vector3(0,.035f,0),rain*1.6f+Crimson*.25f,.0045f,.03f,1,.5f,0,31500+i);}
                        else fx.Streak(at,new Vector3(Wind.X,-Fall,Wind.Z)*.006f,rain*1.25f,.004f,.04f,1,.4f,0,31000+i);
                        continue;
                    }
                    if(onCut&&after>0&&after<.35f){float sp=.02f+.25f*after;var nrm=PlaneNormal*sp;var v=new Vector3(Wind.X,-Fall,Wind.Z);fx.Streak(at+nrm,v*.024f,rain,.004f,.04f,1,.4f,0,31000+i);fx.Streak(at-nrm,v*.024f,rain,.004f,.04f,1,.4f,0,31500+i);continue;}
                    var vel=new Vector3(Wind.X,-Fall,Wind.Z);fx.Streak(at,vel,rain,i<Dense?.0045f:.0038f,.08f,.026f,.4f,0,31000+i);
                }
                else if(still<=0&&s<.14f&&!low||still<=0&&s<.14f&&i%2==0)
                {
                    // The crown: two or three bright flecks thrown up and out, gone in a seventh of a second.
                    float u=s/.14f;int m=low?2:3;
                    for(int j=0;j<m;j++){float a=j*2.1f+NativeEntranceMath.Rand(30031,idx+j*977)*1.4f;var v=new Vector3((float)Math.Cos(a)*.9f,1.5f-9*s,(float)Math.Sin(a)*.9f);
                        fx.Streak(at+new Vector3(v.X*s,.012f+1.5f*s-4.5f*s*s,v.Z*s),v,rain*(1.5f*(1-u)),.003f,.015f,.02f,.5f,u,32000+i*4+j);}
                }
            }
            // Drips off the gate's front eave.
            for(int i=0;i<(low?5:10);i++)
            {
                float x=-2.5f+5f*(i+.5f)/10f,per=.55f+.2f*NativeEntranceMath.Rand(30041,i),u=(r/per+NativeEntranceMath.Rand(30043,i))%1f,fall=3.0f/8.2f;
                var top=U(x,3.0f,-6.05f);float tl=u*per;if(tl<fall&&still<=0)fx.Streak(top-new Vector3(0,8.2f*tl,0),new Vector3(0,-8.2f,0),rain,.004f,.06f,.022f,.4f,0,32600+i);
                else if(still>0&&tl<fall)fx.Streak(top-new Vector3(0,8.2f*tl,0),new Vector3(0,-.04f,0),rain*1.2f,.004f,.03f,1,.4f,0,32600+i);
            }
            // Mist low over the pools, drifting.
            for(int i=0;i<(low?4:8);i++)
            {
                float side=i%2==0?1:-1,x=side*(1.4f+1.8f*NativeEntranceMath.Rand(30051,i))+.12f*(float)Math.Sin(r*.21f+i),z=-6f+10f*NativeEntranceMath.Rand(30053,i);
                fx.Smoke(new Vector3(x,NativeEntranceStage.FloorY+.2f,z),Mist,.11f+.04f*NativeEntranceMath.Rand(30055,i),1.4f+.8f*NativeEntranceMath.Rand(30057,i),.5f,32700+i,NativeEntranceStage.FloorY-.06f);
            }
            // The lantern's flame inside its paper.
            fx.Glow(U(2.55f,1.14f,-1.9f),Lamp*(.9f+.1f*(float)Math.Sin(t*8.3f)),0,.22f,1,0,32800);
            // The far lightning behind the gate: the sky lit for a moment.
            if(bolt>.01f)fx.Glow(new Vector3(-3,10,-26),new Vector3(.75f,.82f,1)*(1.6f*bolt),0,16,1,0,32810);
            // The draw: a glint at the saya's mouth as the blade leaves it.
            if(t>=GrabAt-.05f&&t<GrabAt+.25f){Hand(t,1,out var hip);float g=NativeEntranceMath.Bump(t,GrabAt-.05f,GrabAt+.25f);NativeEntranceFx.Glint(fx,hip+new Vector3(.02f,.02f,.12f),(White+Steel)*(1.6f*g),.14f*g,Vector3.UnitX,Vector3.UnitY,32820);}
            // The cut: the blade's sweep (a razor's line, crimson fringe) from the blade as the model holds it.
            if(t>=GrabAt&&t<FreezeAt+.3f)Trail(fx,t<FreezeAt?t:FreezeAt,0,.2f,Crimson,White,.012f,1.2f,32900,true);
            if(t>=CutAt-.02f&&t<CutAt+.2f&&Held(t,0,out _,out var tip)){float g=Flash(t,CutAt,.01f,.06f);fx.Glow(tip,(White*2+Crimson)*(1.4f*g),0,.18f,1,0,32990);}
            // The line the cut leaves: drawn out along its length in a breath, held through the freeze, then broken into falling light.
            if(t>=CutAt&&t<ResumeAt+1.2f)
            {
                float grow=NativeEntranceMath.EaseOut((t-CutAt)/.1f),level=t<ResumeAt?1:1-NativeEntranceMath.Ramp(t,ResumeAt,ResumeAt+.25f);
                if(level>.004f)
                {
                    float half=LineLength*.5f*grow;var mid=LineAt(LineLength*.5f);
                    for(int side=-1;side<=1;side+=2){var dir=LineDir*side;Line(fx,mid+dir*half,dir*half,White*(2.6f*level),.006f,.9f,0,33000+side+1);Line(fx,mid+dir*(half*.94f),dir*(half*.94f),Crimson*(1.0f*level),.02f,.9f,0,33004+side);}
                }
                if(t>=ResumeAt)
                {
                    float a=t-ResumeAt;int m=low?16:40;
                    for(int i=0;i<m;i++){float life=.6f+.5f*NativeEntranceMath.Rand(33011,i);if(a>life)continue;float u=a/life;var p=LineAt(LineLength*(i+.5f)/m)+new Vector3(.08f*NativeEntranceMath.Signed(33013,i)*a,-1.4f*a*a-.15f*a,.08f*NativeEntranceMath.Signed(33015,i)*a);
                        fx.Glow(p,(i%3==0?Crimson:White*.9f+Steel*.3f)*(1.3f*(1-u)*(1-u)),0,.025f+.02f*(1-u),0,u,33100+i);}
                }
            }
            // The freeze: a few motes hang in the cold light round the blade.
            if(still>0){int m=low?6:14;for(int i=0;i<m;i++){var p=LineAt(.4f+3.2f*NativeEntranceMath.Rand(33201,i))+new Vector3(.4f*NativeEntranceMath.Signed(33203,i),.5f*NativeEntranceMath.Signed(33205,i),.4f*NativeEntranceMath.Signed(33207,i));fx.Glow(p,Steel*(.35f*still),0,.02f,0,0,33210+i);}}
            // The click: the rain thrown off the pilot in a burst of drops, low and out.
            if(t>=ClickAt&&t<ClickAt+.7f)
            {
                float a=t-ClickAt;int m=low?18:44;
                for(int i=0;i<m;i++){float ang=i*6.2831853f/m+.3f*NativeEntranceMath.Signed(33301,i),sp=2.6f+1.6f*NativeEntranceMath.Rand(33303,i),up=.8f+1.4f*NativeEntranceMath.Rand(33305,i);var v0=new Vector3((float)Math.Cos(ang)*sp,up,(float)Math.Sin(ang)*sp);
                    var p=new Vector3(0,.5f+.6f*NativeEntranceMath.Rand(33307,i),0)+v0*a+new Vector3(0,-4.9f*a*a,0);if(p.Y<NativeEntranceStage.FloorY)continue;var v=v0+new Vector3(0,-9.8f*a,0);
                    fx.Streak(p,v,rain*(1.4f*(1-a/.7f)),.004f,.03f,.03f,.5f,a/.7f,33310+i);}
                Hand(t,1,out var hip);float g=Flash(t,ClickAt,.01f,.2f);NativeEntranceFx.Glint(fx,hip+new Vector3(0,.04f,.1f),(Crimson*2+White)*(1.2f*g),.16f*g,Vector3.UnitX,Vector3.UnitY,33390);
            }
            // Under the name: a crimson glint along the saya.
            if(t>=CardAt-.1f&&t<CardAt+.6f){Hand(t,1,out var hip);float g=NativeEntranceMath.Bump(t,CardAt-.1f,CardAt+.6f);NativeEntranceFx.Glint(fx,hip+new Vector3(-.05f,-.02f,.15f),(Crimson*1.5f+White*.5f)*g,.12f*g,Vector3.UnitX,Vector3.UnitY,33400);}
        }
        /// <summary>The cycle drop `i` was in when the rain stopped (only that cycle's drop hangs split on the line).</summary>
        private static int CycleAtFreeze(int i){Drop(i,FreezeAt,out _,out _,out int c);return c;}

        // ---- the sound ----
        public override NativeIntroCue[] Cues=>cues;
        private static readonly NativeIntroCue[] cues=
        {
            new NativeIntroCue(0,"intro2.stillrain.rain","boss.noctiel.rain","water",.55f),
            new NativeIntroCue(LightningAt+.55f,"intro2.stillrain.thunder","structure.rumble","explosion",.45f,.7f),
            new NativeIntroCue(DrawAt,"intro2.stillrain.stance","move.step.stone","wood",.35f),
            new NativeIntroCue(GrabAt,"intro2.stillrain.koiguchi","topdog.blade-click","metal",.55f,1.4f),
            new NativeIntroCue(CutAt,"intro2.stillrain.cut","topdog.blade-cut","sword",.95f),
            new NativeIntroCue(FreezeAt,"intro2.stillrain.still","topdog.blade-ring","install",.6f),
            new NativeIntroCue(ResumeAt,"intro2.stillrain.resume","boss.noctiel.rain.lines","water",.6f),
            new NativeIntroCue(SlideAt+.05f,"intro2.stillrain.bamboo","break.wood","wood",.75f),
            new NativeIntroCue(SheatheAt+.05f,"intro2.stillrain.slide","topdog.blade-draw","sword",.35f,.8f),
            new NativeIntroCue(SlideAt+1.05f,"intro2.stillrain.fall","structure.land.timber","wood",.6f),
            new NativeIntroCue(ClickAt,"intro2.stillrain.click","topdog.blade-sheathe","metal",.9f),
            new NativeIntroCue(CardAt,"intro2.stillrain.name","topdog.blade-chime","install",.45f),
        };

        // ---- the name card: drawn out of a bright line, as the cut was ----
        public override void Card(float t,ref NativeIntroCardState card)
        {
            card=NativeIntroCardState.Default(t-IntroNameAt,new Vector3(.95f,.97f,1),Crimson*1.2f);card.Style=NativeIntroCardStyle.Wipe;NameBeside(ref card,true);card.Exit=NativeEntranceMath.Ramp(t,IntroTotal-.35f,IntroTotal);
        }
    }
}
