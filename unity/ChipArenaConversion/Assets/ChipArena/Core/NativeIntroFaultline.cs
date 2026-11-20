using System;
using System.Numerics;
using static ChipArena.UnityConversion.NativeIntroMelee;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// "Faultline", the greatsword's intro (intros v2; docs/INTROS-MELEE.md): the weight that cracks the ground. 8.0 s, the name at 6.4 s.
    /// Night in the Rift (a basalt quarry: columnar walls in benches, a molten seam smouldering in a corner, a timber derrick, quarry
    /// blocks). The camera glides low over the floor to the pilot standing with the great sword; embers drift up from the faults. It leaps
    /// and brings the blade down two-handed on its own spot (the plunging slam, slowed into the blow): the floor round it breaks into slabs
    /// that heave up and tilt on a wave running outward, molten light wells up through the joints, cracks race to the walls, the quarry's
    /// seams flare, grit and sparks fly. The camera cranes up over the broken ring; the pilot rises from the blow and stands in the glow.
    /// The model plays the great sword set's clips (NativeBladeIntro); its first frames are the set's idle, the keyed rig the game's own
    /// capture of it, so the great sword sits in the hands as the match holds it. Presentation only.
    /// </summary>
    public sealed class NativeIntroFaultline:NativeIntroMeleeBlade
    {
        public const string Title="Faultline";
        public const float Total=8.0f,CardAt=6.4f;
        /// <summary>The beats (presentation s): the leap, the blow, the cracks reaching the walls, the rise, the stance.</summary>
        public const float LeapAt=2.2f,SlamAt=LeapAt+PlungeHit,SpreadAt=SlamAt+.5f,RiseAt=LeapAt+PlungeEnd,StandAt=RiseAt+.25f;
        private const float PlungeHit=.63f,PlungeEnd=1.833f;
        private static readonly Vector3 Ember=NativeEntranceFx.Hex(0xff8a3c),Molten=NativeEntranceFx.Hex(0xff5a14),Gold=NativeEntranceFx.Hex(0xffcb87),Night=NativeEntranceFx.Hex(0x0a0806),
            Dust=NativeEntranceFx.Hex(0x6e655c),Stone=NativeEntranceFx.Hex(0x3a3d42),Moon=NativeEntranceFx.Hex(0x9aaccc);
        public override string Spec=>"greatsword";
        // The rift set's fracture disc: its slabs' rests (Unity x, z), as melee_rift.py builds them.
        private static readonly Vector2[] Slabs={new Vector2(.015f,.015f),new Vector2(-.716f,-1.836f),new Vector2(1.448f,-1.371f),new Vector2(1.577f,1.162f),new Vector2(-.437f,1.956f),new Vector2(-1.993f,.140f),
            new Vector2(-.054f,-3.591f),new Vector2(2.655f,-2.502f),new Vector2(3.492f,.040f),new Vector2(2.336f,2.789f),new Vector2(-.329f,3.610f),new Vector2(-2.733f,2.273f),new Vector2(-3.705f,-.413f),new Vector2(-2.803f,-2.416f)};
        private static readonly string[] slabProps=MakeNames();
        private static string[] MakeNames(){var n=new string[14];for(int i=0;i<14;i++)n[i]="rift/slab-"+i;return n;}
        // The cracks that race from the disc to the walls (Unity x, z polylines on the floor).
        private static readonly Vector2[][] Cracks=
        {
            new[]{new Vector2(1.6f,-4.4f),new Vector2(2.9f,-6.8f),new Vector2(4.45f,-9.6f)},
            new[]{new Vector2(-1.8f,-4.3f),new Vector2(-3.0f,-6.9f),new Vector2(-4.05f,-9.45f)},
            new[]{new Vector2(-4.2f,1.9f),new Vector2(-6.2f,3.3f),new Vector2(-8.3f,4.5f)},
            new[]{new Vector2(4.6f,.6f),new Vector2(6.8f,1.4f),new Vector2(9.6f,.9f)},
            new[]{new Vector2(2.8f,3.8f),new Vector2(3.6f,6.2f),new Vector2(4.4f,9.0f)},
            new[]{new Vector2(-1.4f,4.5f),new Vector2(-2.0f,7.0f),new Vector2(-2.9f,9.8f)},
        };
        private const float CrackSpeed=11f;

        public NativeIntroFaultline():base(NativeIntroCatalog.WeaponPrefix+"greatsword",Total,CardAt,SlamAt,0xffcb87)
        {
            var stand=NativeEntranceKeys.Stand();var k=new NativeEntranceKeys();
            k.Key(0,stand).Root(0,.88f,0,Facing).Planted(1);
            k.Key(LeapAt,stand).Root(0,.84f,0,Facing).Planted(1).Drop(-.05f);
            k.Key(SlamAt,stand).Root(0,.62f,.1f,Facing).Planted(1).Drop(-.28f).J(B,.5f).ImpactKey();
            k.Key(RiseAt,stand).Root(0,.8f,.1f,Facing).Planted(1).Drop(-.08f).J(B,.15f);
            k.Key(Total,stand).Root(0,.88f,.1f,Facing).Planted(1);Keys=k;
            // The camera: low over the floor to the pilot; wide and low as it leaps; the blow, craning up over the breaking ring; high over the
            // cracks racing to the walls; the hero.
            Rig.Dolly(0,V(-2.6f,.42f,5.4f),V(-1.55f,.55f,3.55f),V(0,.85f,0),V(0,1.12f,0),44,40).Ease(NativeIntroEase.Out).Handheld(.3f)
               .Dolly(LeapAt-.15f,V(3.4f,.38f,1.7f),V(3.55f,.42f,1.95f),V(0,1.6f,0),V(0,1.2f,.1f),52,52)
               .Crane(SlamAt-.06f,V(-2.4f,1.0f,3.1f),V(-1.5f,1.5f,3.4f),5.2f,V(0,.35f,0),V(0,.1f,-.2f),50,60).Ease(NativeIntroEase.InOut)
               .Dolly(SlamAt+1.15f,V(.9f,9.6f,3.4f),V(.4f,9.1f,1.9f),V(0,0,-.8f),V(0,0,-.4f),58,54);// (on the blow's clock: the cracks' later start must not crowd it into the hero)
            HeroFull(RiseAt+.3f,1,5.6f,33,.4f,0,.22f);// (back and up: the great sword held high stays in frame)// (a cut: from high over the ring a blend would plunge 8 m)
            Rig.Bars(0,1).Bars(CardAt-.4f,.5f).Black(0,1).Black(.05f,0);
            Ramps.Ramp(SlamAt-.2f,.25f,.3f,.9f);
        }
        public override string SetBank=>Bank;

        // ---- the body: the set's idle (with the game's capture under it), the plunging slam, the idle again ----
        public override bool Clip(float t,out string clip,out float time)
        {
            if(t<LeapAt){clip="2Hand-Sword-Idle";time=t;return true;}
            if(t<RiseAt){clip=NativeDevGreatswordMotion.GreatswordPlunge;time=t-LeapAt;return true;}
            clip="2Hand-Sword-Idle";time=.6f+(t-RiseAt);return true;
        }
        public override float Fade(float t)=>t>=LeapAt-.02f&&t<LeapAt+.25f?.18f:t>=RiseAt-.02f&&t<RiseAt+.5f?.45f:.12f;
        public override void Pose(float t,CapturedRig.Pose pose,out Vector3 root,out Quaternion rotation)
        {
            base.Pose(t,pose,out root,out rotation);
            // The game's own capture of the idle while the model plays it (the hold), and again under the closing stance.
            if(t<LeapAt-.05f)Captured(2,0,t,pose);else if(t>=StandAt)Captured(2,0,.6f+(t-RiseAt),pose);
        }

        // ---- the fracture: each slab heaves on the wave from the blow and settles tilted outward ----
        /// <summary>Slab `k`'s pose at `t` (source frame): its rest, lifted and tilted after the wave reaches it.</summary>
        public static void Slab(int k,float t,out Vector3 at,out Quaternion turn)
        {
            var c=Slabs[k];var rest=U(c.X,0,c.Y);at=rest;turn=Quaternion.Identity;float d=new Vector2(rest.X,rest.Z).Length(),reach=SlamAt+d/9f,s=t-reach;if(s<=0)return;
            // (the centre slab, where the pilot stands, only shudders: the feet stay on it)
            float h=NativeEntranceMath.Rand(36001,k),tilt=k==0?0:.12f+.16f*h,lift=k==0?0:.04f+.16f*h,jump=k==0?.025f:.18f+.22f*h*(1-d/6f);
            // A jump on the wave, damped, settling on its new height and tilt.
            float settle=1-(float)Math.Exp(-7*s),bounce=(float)(Math.Exp(-5.5*s)*Math.Sin(Math.Min(s*14,3.1416)));
            at.Y+=lift*settle+jump*bounce;
            var radial=d>.05f?Vector3.Normalize(new Vector3(rest.X,0,rest.Z)):Vector3.UnitZ;var axis=Vector3.Normalize(Vector3.Cross(Vector3.UnitY,radial));
            float spin=.25f*(h-.5f)*settle;turn=Quaternion.CreateFromAxisAngle(axis,-tilt*settle-tilt*.6f*bounce)*Quaternion.CreateFromAxisAngle(Vector3.UnitY,spin*.2f);
        }
        /// <summary>How hot the quarry's cracks and seam run at `t` (the glow sub-mesh's level: the seam, the faults and the light under the disc).</summary>
        private float Heat(float t)=>(.55f+.15f*(float)Math.Sin(t*1.7f)*(Context.Reduced?0:1)+2.6f*NativeEntranceMath.Pulse(t,SlamAt,.06f,.9f)+.9f*NativeEntranceMath.Ramp(t,SlamAt,SlamAt+.6f))
            *(1-.45f*NativeEntranceMath.Ramp(t,RiseAt+.2f,CardAt+.2f));// (cooling under the name: the seam ran as a bright bar through its band)

        // ---- the set ----
        protected override bool Scene(float t,INativeIntroSet set)
        {
            if(!set.Has("rift/static"))return false;
            set.Piece("rift/static",SetAt,Quaternion.Identity,1,White,White*Heat(t));
            for(int k=0;k<Slabs.Length;k++){Slab(k,t,out var at,out var turn);set.Piece(slabProps[k],at,turn,1,White,White);}
            return true;
        }
        protected override void Fallback(float t,INativeIntroSet set)=>Plain(set,NativeEntranceFx.Hex(0x1a1714),Molten,.6f+.8f*NativeEntranceMath.Ramp(t,SlamAt,SlamAt+.5f),NativeEntranceFx.Hex(0x4a2412));

        // ---- the light ----
        public override void Lights(float t,NativeEntranceLights l)
        {
            WarmAnchors(t);l.Clear();l.Background=Night;l.Fog=NativeEntranceFx.Hex(0x120b07);l.FogNear=8;l.FogFar=42;l.DriveProbe=true;
            float heat=Heat(t),blow=Flash(t,SlamAt,.02f,.35f),after=NativeEntranceMath.Ramp(t,SlamAt,SlamAt+.5f);
            l.Sky=NativeEntranceFx.Hex(0x3a2a22,.24f+.1f*after);l.Ground=NativeEntranceFx.Hex(0x1a0e08,.3f+.4f*after);
            l.Directional(new Vector3(-.5f,1,-.4f),Moon*.32f);
            // The seam in the back corner, the light welling up through the broken disc, the blow, a warm rim behind, the name.
            l.Add(U(4.6f,1.6f,-9.6f),Molten*(4.5f*heat),10);
            l.Add(U(0,.15f,0),(Molten*1.5f+Ember)*(3.2f*after+6*blow),7);
            if(blow>.01f)l.Add(new Vector3(0,1.0f,.6f),(White*2+Gold)*(9*blow),9);
            l.Add(new Vector3(.5f,2.6f,-3.2f),Ember*1.8f,7);
            l.Add(new Vector3(-2.6f,1.6f,3.2f),Moon*.9f,7);
            float name=NativeEntranceMath.Ramp(t,CardAt-.25f,CardAt+.35f);if(name>.01f)l.Add(new Vector3(1.6f,2.0f,1.8f),Gold*(1.8f*name),8);
        }

        // ---- the effects ----
        public override void Effects(float t,INativeEntranceFx fx,float q)
        {
            Look(t);bool low=q<.5f||Context.Low;float floor=NativeEntranceStage.FloorY,after=t-SlamAt;
            // Embers drifting up off the quarry's faults and seam all through; many more off the broken disc after the blow.
            NativeEntranceFx.Embers(fx,t,-4,Total,U(3.0f,0,-7.0f),3.5f,low?6:14,.6f,2.6f,Ember*1.8f,.03f,36100,q,.3f);
            NativeEntranceFx.Embers(fx,t,SlamAt,Total,new Vector3(0,floor+.05f,0),4.2f,low?10:26,.9f,2.2f,Ember*2.2f,.035f,36200,q,.5f);
            // Dust hanging in the moonlight.
            for(int i=0,n=low?6:14;i<n;i++){var p=new Vector3(NativeEntranceMath.Signed(36301,i)*6,.6f+3*NativeEntranceMath.Rand(36303,i),NativeEntranceMath.Signed(36305,i)*6)+new Vector3((float)Math.Sin(t*.2f+i)*.3f,(float)Math.Sin(t*.13f+i*2)*.15f,0);fx.Glow(p,Moon*.06f,0,.03f,0,0,36310+i);}
            // The leap: grit kicked off the floor.
            NativeEntranceFx.GroundDust(fx,t,LeapAt+.12f,new Vector3(0,floor,0),low?5:10,.3f,1.6f,.8f,Dust,.35f,.45f,36400,q,floor);
            if(after<0)return;
            // The blow: a white-gold flash at the blade, sparks thrown wide and molten drops spraying out, a ring of dust running out.
            var hit=new Vector3(0,floor+.05f,.7f);
            if(after<.22f)fx.Glow(hit+new Vector3(0,.25f,0),(White*2+Gold)*(2.6f*(1-after/.22f)),0,.4f+1.4f*after,1,after/.22f,36500);
            NativeEntranceFx.Sparks(fx,t,SlamAt,hit,Vector3.UnitY,1.6f,low?14:36,8.5f,1.0f,8,.8f,(White+Gold)*1.8f,Molten*2,.016f,36510,q,floor);
            if(after<1.4f)
            {
                int m=low?12:30;
                for(int i=0;i<m;i++)
                {
                    float life=.7f+.6f*NativeEntranceMath.Rand(36601,i);if(after>life)continue;float u=after/life,ang=i*6.2831853f/m+.3f*NativeEntranceMath.Signed(36603,i),sp=2.4f+3.2f*NativeEntranceMath.Rand(36605,i),up=2.2f+3f*NativeEntranceMath.Rand(36607,i);
                    var v0=new Vector3((float)Math.Cos(ang)*sp,up,(float)Math.Sin(ang)*sp);var p=new Vector3(0,floor+.1f,0)+v0*after+new Vector3(0,-4.9f*after*after,0);if(p.Y<floor)continue;
                    fx.Streak(p,v0+new Vector3(0,-9.8f*after,0),(i%3==0?Gold:Molten)*(2.2f*(1-u)),.012f,.03f,.03f,.6f,u,36610+i);
                }
            }
            NativeEntranceFx.GroundDust(fx,t,SlamAt,new Vector3(0,floor,0),low?10:22,1.2f,7.5f,1.6f,Dust,.55f,1.2f,36700,q,floor);
            // Chips of stone tumbling out of the joints.
            if(after<1.2f){int m=low?8:18;for(int i=0;i<m;i++){float life=.6f+.5f*NativeEntranceMath.Rand(36801,i);if(after>life)continue;float u=after/life,ang=NativeEntranceMath.Rand(36803,i)*6.2831853f,r0=.8f+3.6f*NativeEntranceMath.Rand(36805,i),reach=r0/9f;float a=after-reach;if(a<0)continue;
                var o=new Vector3((float)Math.Cos(ang)*r0,floor+.05f,(float)Math.Sin(ang)*r0);var v0=new Vector3((float)Math.Cos(ang)*1.2f,2.4f+2.4f*NativeEntranceMath.Rand(36807,i),(float)Math.Sin(ang)*1.2f);var p=o+v0*a+new Vector3(0,-4.9f*a*a,0);if(p.Y<floor)continue;
                fx.Streak(p,v0+new Vector3(0,-9.8f*a,0),Stone*(1.4f*(1-u)),.03f,.04f,.015f,.2f,u,36810+i);}}
            // The cracks racing out from the disc to the walls, molten in their throats, cooling to a dull glow.
            for(int c=0;c<Cracks.Length;c++)
            {
                var path=Cracks[c];float run=CrackSpeed*Math.Max(0,t-SpreadAt-.05f*c),done=0;float cool=1-.55f*NativeEntranceMath.Ramp(t,SpreadAt+1.2f,Total);
                for(int s=0;s+1<path.Length;s++)
                {
                    var a=U(path[s].X,.025f,path[s].Y);var b=U(path[s+1].X,.025f,path[s+1].Y);float len=Vector3.Distance(a,b);float shown=Math.Min(len,run-done);done+=len;if(shown<=0)break;
                    var dir=(b-a)/len;var head=a+dir*shown;Line(fx,head,dir*shown,Molten*(1.8f*cool),.05f,.2f,0,36900+c*4+s);Line(fx,head,dir*shown,Gold*(1.2f*cool),.018f,.2f,0,36950+c*4+s);
                }
            }
            // Smoke climbing off the broken ring.
            if(after<4){int m=low?4:9;for(int i=0;i<m;i++){float u=((after*.32f+i/(float)m)%1f);float ang=i*2.4f;var p=new Vector3((float)Math.Cos(ang)*(1.5f+1.5f*NativeEntranceMath.Rand(37001,i)),floor+.2f+2.6f*u,(float)Math.Sin(ang)*(1.5f+1.5f*NativeEntranceMath.Rand(37003,i)));fx.Smoke(p,new Vector3(.16f,.13f,.12f),.25f*(1-u)*NativeEntranceMath.Ramp(after,0,.4f),.6f+1.4f*u,u,37010+i,floor);}}
            // Grit falling off the quarry walls after the blow.
            if(after<2.5f){int m=low?6:14;for(int i=0;i<m;i++){float born=.2f+1.2f*NativeEntranceMath.Rand(37101,i),a=after-born;if(a<0||a>1.2f)continue;var o=U(-9.5f+13.6f*NativeEntranceMath.Rand(37103,i),1.4f+1.1f*NativeEntranceMath.Rand(37105,i),-9.5f+.1f*NativeEntranceMath.Rand(37107,i));fx.Streak(o-new Vector3(0,4.9f*a*a,0),new Vector3(0,-9.8f*a,0),Dust*1.1f,.02f,.03f,.02f,.2f,a/1.2f,37110+i);}}
        }
        // ---- the sound ----
        public override NativeIntroCue[] Cues=>cues;
        private static readonly NativeIntroCue[] cues=
        {
            new NativeIntroCue(0,"intro2.faultline.bed",null,"fire",.25f,.7f),
            new NativeIntroCue(LeapAt,"intro2.faultline.leap","melee.greatsword.swing","greatsword",.8f,.85f),
            new NativeIntroCue(SlamAt,"intro2.faultline.slam","blast.quake","explosion",1f,.8f),
            new NativeIntroCue(SlamAt+.05f,"intro2.faultline.crack","structure.crack","stone",.9f),
            new NativeIntroCue(SpreadAt+.25f,"intro2.faultline.rumble","structure.debris","stone",.7f,.7f),
            new NativeIntroCue(RiseAt,"intro2.faultline.lift","melee.greatsword.hit","metal",.4f,.8f),
            new NativeIntroCue(SpreadAt+.8f,"intro2.faultline.hiss","magic.fire.hit","fire",.4f,.7f),
            new NativeIntroCue(CardAt,"intro2.faultline.name","structure.thud","greatsword",.45f,.9f),
        };
        public override void Card(float t,ref NativeIntroCardState card)
        {
            card=NativeIntroCardState.Default(t-IntroNameAt,new Vector3(1,.97f,.92f),Ember*1.3f);card.Style=NativeIntroCardStyle.Slam;NameBelow(ref card);card.Exit=NativeEntranceMath.Ramp(t,IntroTotal-.35f,IntroTotal);
        }
    }
}
