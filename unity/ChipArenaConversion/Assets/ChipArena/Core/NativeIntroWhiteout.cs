using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// "Whiteout" (docs/INTROS-GENERIC.md "Whiteout"). 8 s, the name at 6.2 s. Night, a blizzard, a polar outpost lost in it: the frame
    /// is a wall of snow backlit by floodlights. A shape kneels in it, frosted white. Its visor lights; heat lines glow up its suit and
    /// steam rises; the frost cracks and falls away. It rises, steam pouring off it, clenches a fist, and a pulse of heat blows the snow
    /// off the ground round it in a cloud of steam. The storm parts: the outpost, its masts, the aurora above. Camera: telephoto (one
    /// slow push into the storm), a close insert, a tilt up with the rise, a low wide pulse, a reveal as the storm parts, the hero.
    /// </summary>
    public sealed class NativeIntroWhiteout:NativeIntroGenericScript
    {
        public const string Id=NativeIntroCatalog.Prefix+"whiteout";
        public const float Total=8f,CardAt=6.2f;
        public const float VisorAt=1.4f,HeatAt=2.0f,CrackAt=2.8f,RiseAt=3.4f,UpAt=4.3f,PulseAt=4.5f,PartAt=5.0f;
        public override string Title=>"Whiteout";
        private static readonly Vector3 Ice=new Vector3(.86f,.93f,1f),Heat=NativeEntranceFx.Hex(0xff7a2c),HeatHot=NativeEntranceFx.Hex(0xffc27a),Visor=NativeEntranceFx.Hex(0x7fe8ff),
            Steam=new Vector3(.86f,.88f,.9f),Storm=NativeEntranceFx.Hex(0xb8c9da),Night=NativeEntranceFx.Hex(0x0b1422),Lamp=NativeEntranceFx.Hex(0xffbe6a),Flood=NativeEntranceFx.Hex(0xe6efff),Red=NativeEntranceFx.Hex(0xff3524);
        // The set (Unity space, generic_whiteout.py): the floodlight head and the radio beacon.
        private static readonly Vector3 FloodAt=U(5.6f,8.9f,-7.4f),BeaconAt=U(-15.5f,17.1f,-21f),HutDoor=U(-7.2f+3.5f*(float)Math.Sin(.46f),2.1f,-10.4f+3.5f*(float)Math.Cos(.46f));
        /// <summary>The wind: from the pilot's left and behind, low over the snow (source frame, unit), and its speed (m/s).</summary>
        private static readonly Vector3 Wind=Vector3.Normalize(new Vector3(-1,-.06f,.32f));private const float Gust=13;
        public NativeIntroWhiteout():base(Id,Total,CardAt,PulseAt,0x7fe8ff)
        {
            // Kneeling: the left foot forward, the right knee in the snow; then standing on the mark.
            Start=Stance(.2f,.42f,.12f,-.2f,-.35f,-.45f);
            Steps=new[]{Step(false,RiseAt+.35f,RiseAt+.8f,-.19f,.18f,-.42f,.1f),Step(true,UpAt+.05f,UpAt+.35f,.18f,.24f,.1f,.05f)};
            var stand=NativeEntranceKeys.Stand();var k=new NativeEntranceKeys();
            float[] kneel(float head,float lean)=>NativeEntranceKeys.With(stand,v=>{v[NativeEntranceKeys.RootY]=.88f;v[NativeEntranceKeys.RootZ]=.12f;v[NativeEntranceKeys.Yaw]=Facing;v[NativeEntranceKeys.BodyY]=-.5f;
                v[NativeEntranceKeys.PlantL]=1;v[NativeEntranceKeys.PlantR]=0;
                NativeEntranceKeys.Set(v,B,lean,0,0);NativeEntranceKeys.Set(v,Sp,.1f,0,0);NativeEntranceKeys.Set(v,C,.06f,0,0);NativeEntranceKeys.Set(v,Hd,head,0,0);
                NativeEntranceKeys.Set(v,LL,-1.35f,0,.15f);NativeEntranceKeys.Set(v,LK,1.65f,0,0);NativeEntranceKeys.Set(v,RL,.3f,0,-.08f);NativeEntranceKeys.Set(v,RK,1.75f,0,0);
                NativeEntranceKeys.Set(v,LA,-.75f,0,.25f);NativeEntranceKeys.Set(v,LE,-1.15f,0,0);NativeEntranceKeys.Set(v,RA,-.15f,0,-.12f);NativeEntranceKeys.Set(v,RE,-.35f,0,0);});
            k.Key(0,kneel(.55f,.34f));
            k.Key(HeatAt,kneel(.5f,.32f));
            // The frost breaks: a shudder, the head lifts.
            k.Key(CrackAt-.04f,kneel(.48f,.33f));
            k.Key(CrackAt+.08f,kneel(.2f,.26f)).J(C,-.04f).ImpactKey();
            k.Key(RiseAt,kneel(-.08f,.22f)).J(RA,-.25f,0,-.25f).J(RE,-.6f);
            // The rise: the trailing leg comes under, up through the knees.
            k.Key(RiseAt+.55f,stand).Root(0,.88f,.18f,Facing).Planted(1,.6f).Drop(-.3f).J(B,.28f).J(Sp,.08f).J(Hd,-.05f).J(LA,-.45f,0,.35f).J(LE,-.8f).J(RA,-.3f,0,-.3f).J(RE,-.6f).PassKey();
            k.Key(UpAt,stand).Root(0,.88f,.22f,Facing).Planted(1).Drop(-.03f).J(B,.04f).J(Hd,-.08f).J(LA,-.15f,0,.25f).J(LE,-.35f).J(RA,-.2f,0,-.25f).J(RE,-.5f);
            // The fist, drawn in and clenched; the pulse: a sink and the arms thrown down and open, palms out.
            k.Key(PulseAt-.12f,stand).Root(0,.88f,.22f,Facing).Planted(1).Drop(-.05f).J(C,-.08f).J(Hd,.06f).J(RA,-.55f,0,-.45f).J(RE,-1.75f).J(LA,-.4f,0,.35f).J(LE,-1.2f);
            k.Key(PulseAt+.06f,stand).Root(0,.88f,.22f,Facing).Planted(1).Drop(-.14f).J(B,.08f).J(C,-.1f).J(Hd,-.04f).J(RA,.22f,0,-.62f).J(RE,-.25f).J(LA,.22f,0,.62f).J(LE,-.25f).ImpactKey();
            k.Key(PartAt,stand).Root(0,.88f,.22f,Facing).Planted(1).Drop(-.06f).J(C,-.06f).J(Hd,-.15f,.12f,0).J(RA,.05f,0,-.42f).J(RE,-.3f).J(LA,.05f,0,.42f).J(LE,-.3f);
            k.Key(CardAt-.3f,NativeEntranceKeys.Hero()).Root(0,.88f,.22f,Facing).Planted(1).J(Hd,-.1f,.06f,0);
            k.Key(Total,NativeEntranceKeys.Hero()).Root(0,.88f,.22f,Facing).Planted(1).J(C,-.02f,-.12f,0).J(Hd,-.12f,.1f,0);Keys=k;
            // The camera: telephoto into the storm, a close insert on the frost, a tilt up with the rise, the pulse low and wide, the
            // reveal pulling out as the storm parts, the hero.
            Rig.Dolly(0,new Vector3(1.3f,.62f,13.5f),new Vector3(.75f,.72f,7.4f),new Vector3(0,.72f,.1f),new Vector3(0,.8f,.1f),17,19).Ease(NativeIntroEase.InOut).Focus(0,0,.6f).Handheld(.35f)
               .Dolly(HeatAt,new Vector3(.62f,1.12f,1.45f),new Vector3(.52f,1.06f,1.22f),new Vector3(0,-.05f,0),new Vector3(0,-.02f,0),27,24).Follow(NativeEntranceAnchor.Head).Focus(0,0,.5f)
               .Crane(CrackAt+.2f,new Vector3(1.7f,.45f,3.1f),new Vector3(1.45f,.55f,2.7f),.35f,new Vector3(0,.05f,0),new Vector3(0,.15f,0),34,37).Follow(NativeEntranceAnchor.Chest,.06f)
               .Dolly(PulseAt-.25f,new Vector3(-2.6f,.32f,4.3f),new Vector3(-2.75f,.36f,4.6f),new Vector3(0,1.05f,.1f),new Vector3(0,1.1f,.1f),52,54)
               .Crane(PartAt+.05f,new Vector3(-2.75f,.36f,4.6f),new Vector3(-4.0f,.9f,8.2f),1.1f,new Vector3(0,1.1f,.1f),new Vector3(-.8f,2.6f,-6),54,50).Ease(NativeIntroEase.InOut).In(NativeIntroCut.Blend,.45f)
               .Hero(CardAt-.45f,1,1.12f,3.4f,33).In(NativeIntroCut.Blend,.6f)
               .Bars(0,.9f).Bars(CardAt-.6f,.45f);
            Ramps.Ramp(PulseAt-.02f,.42f,.3f,.7f);
        }
        /// <summary>How thick the storm is (1 the whiteout, 0 parted).</summary>
        public static float StormLevel(float t)=>1-NativeEntranceMath.Ramp(t,PartAt-.15f,PartAt+1.1f);
        /// <summary>The heat in the suit (0 cold .. 1 the pulse), and its steam.</summary>
        public static float Warmth(float t)=>NativeEntranceMath.Ramp(t,HeatAt,CrackAt)*.7f+NativeEntranceMath.Ramp(t,UpAt,PulseAt)*.3f-.35f*NativeEntranceMath.Ramp(t,PartAt,CardAt);
        /// <summary>The thawed ground's radius (m) from the pulse.</summary>
        public static float Thaw(float t)=>t<PulseAt?0:2.9f*NativeEntranceMath.EaseOut((t-PulseAt)/.7f);
        public override string AccentAt(float t)=>t<VisorAt?"#16242c":t<HeatAt?"#7fe8ff":t<PulseAt?"#ff9a48":t<PartAt?"#ffd28a":"#ffae5a";
        protected override Vector3 Focus(float t,Vector3 chest)=>chest;

        // ---- the set ----
        protected override bool Scene(float t,INativeIntroSet set)
        {
            if(!set.Has("whiteout/ground"))return false;
            float storm=StormLevel(t),clear=1-storm;
            set.Piece("whiteout/sky",Vector3.Zero,Still,1,White,White*(.5f+.5f*clear));
            set.Piece("whiteout/ground",Vector3.Zero,Still,1,White,Vector3.Zero);
            set.Piece("whiteout/outpost",Vector3.Zero,Still,1,White,Vector3.Zero);
            set.Piece("whiteout/windows",Vector3.Zero,Still,1,White,Lamp*2.4f);
            set.Piece("whiteout/lamps",Vector3.Zero,Still,1,White,Lamp*3.2f);
            set.Piece("whiteout/flood",Vector3.Zero,Still,1,White,Flood*(4.5f+1.5f*Flash(t,PulseAt,.05f,.5f)));
            set.Piece("whiteout/beacon",Vector3.Zero,Still,1,White,Red*(.2f+4f*Blink(t,1.8f,.3f,.5f)));
            float r=Thaw(t);if(r>.02f)set.Piece("whiteout/thaw",new Vector3(0,NativeEntranceStage.FloorY+.003f,.15f),Still,r,White,Vector3.Zero);
            float aur=NativeEntranceMath.Ramp(t,PartAt+.2f,PartAt+1.8f);
            if(aur>.01f)set.Piece("whiteout/aurora",Vector3.Zero,UYaw(.02f*(float)Math.Sin(t*.35f)),1,White,White*(aur*(1.6f+.25f*(float)Math.Sin(t*.8f))));
            return true;
        }
        protected override void Fallback(float t,INativeIntroSet set)=>Plain(set,t,NativeEntranceFx.Hex(0xc9d6e4),Vector3.Lerp(Visor,Heat,Warmth(t)),.4f+1.6f*Flash(t,PulseAt,.04f,.6f)+.6f*Warmth(t),Flood);

        // ---- the light ----
        public override void Lights(float t,NativeEntranceLights l)
        {
            float storm=StormLevel(t),clear=1-storm,warm=Math.Max(0,Warmth(t));
            l.Clear();l.DriveProbe=true;
            var fog=Vector3.Lerp(Night,Storm,storm);l.Background=fog;l.Fog=fog;l.FogNear=NativeEntranceMath.Lerp(28,.6f,storm);l.FogFar=NativeEntranceMath.Lerp(240,11,storm);
            l.Sky=Vector3.Lerp(NativeEntranceFx.Hex(0x5c6c9c,.4f),NativeEntranceFx.Hex(0xd2deea,.65f),storm);l.Ground=Vector3.Lerp(NativeEntranceFx.Hex(0x7d8aa0,.35f),NativeEntranceFx.Hex(0xe6eef6,.55f),storm);
            // The floodlights behind it (the backlight), the moon's fill once the storm parts.
            l.Directional(FloodAt,Flood*(.85f+.4f*clear));
            l.Directional(new Vector3(.4f,.6f,.8f),NativeEntranceFx.Hex(0x9fb4ff,.12f+.3f*clear));
            var head=Anchor(NativeEntranceAnchor.Head,t);var chest=Anchor(NativeEntranceAnchor.Chest,t);
            float visor=NativeEntranceMath.Ramp(t,VisorAt,VisorAt+.25f);if(visor>.01f)l.Add(head+new Vector3(0,0,.25f),Vector3.Lerp(Visor,HeatHot,NativeEntranceMath.Ramp(t,HeatAt,CrackAt))*(1.4f*visor),1.8f);
            if(warm>.01f)l.Add(chest+new Vector3(0,0,.3f),Heat*(3.2f*warm),3.4f);
            float pulse=Flash(t,PulseAt,.04f,.55f);if(pulse>.01f)l.Add(chest,HeatHot*(14*pulse),7);
            l.Add(FloodAt+new Vector3(0,-1,1.5f),Flood*(6+4*clear),18);
            if(clear>.01f){l.Add(HutDoor+new Vector3(0,-.2f,.6f),Lamp*(3.5f*clear),6);}
            if(t>=CardAt-.3f){float c=NativeEntranceMath.Ramp(t,CardAt-.3f,CardAt+.3f);l.Add(chest+new Vector3(1.6f,.8f,1.8f),HeatHot*(1.2f*c),6);l.Add(chest+new Vector3(-1.8f,1.2f,-1.2f),NativeEntranceFx.Hex(0x7ff0c0)*(1.6f*c),7);}
        }

        // ---- effects ----
        public override void Effects(float t,INativeEntranceFx fx,float q)
        {
            Look(t);float floor=NativeEntranceStage.FloorY,storm=StormLevel(t),warm=Math.Max(0,Warmth(t));bool low=q<.5f||Context.Low;
            // The blizzard: snow driving across in streaks, thinning to a drift as the storm parts; big flakes near the lens.
            {
                int n=low?50:150;float level=.15f+.85f*storm;
                for(int i=0;i<n;i++)
                {
                    if(NativeEntranceMath.Rand(22000,i)>level)continue;
                    var basePos=new Vector3(NativeEntranceMath.Signed(22001,i)*7,.05f+3.6f*NativeEntranceMath.Rand(22002,i)*NativeEntranceMath.Rand(22003,i),-5+19*NativeEntranceMath.Rand(22004,i));
                    float sp=Gust*(.7f+.6f*NativeEntranceMath.Rand(22005,i))*(.35f+.65f*storm);var v=Wind*sp+new Vector3(0,(float)Math.Sin(t*2.3f+i)*.6f,0);
                    var p=basePos+v*t;p.X=Wrap(p.X,-7,7);p.Z=Wrap(p.Z,-5,14);
                    fx.Streak(p,v,Ice*(.55f+.35f*storm),.012f+.008f*NativeEntranceMath.Rand(22006,i),.04f,.035f,.7f,0,22000+i);
                }
                int m=low?10:26;
                for(int i=0;i<m;i++)
                {
                    var basePos=new Vector3(NativeEntranceMath.Signed(22200,i)*5,.2f+2.8f*NativeEntranceMath.Rand(22201,i),-2+12*NativeEntranceMath.Rand(22202,i));
                    var p=basePos+Wind*(Gust*.5f*t);p.X=Wrap(p.X,-5,5);p.Z=Wrap(p.Z,-2,10);
                    fx.Glow(p,Ice*(.22f*level),0,.05f+.04f*NativeEntranceMath.Rand(22203,i),3,0,22200+i);
                }
                // Spindrift: snow streaming low over the ground.
                int d=low?5:12;
                for(int i=0;i<d;i++)
                {
                    float u=((t*.45f+i/(float)d)%1f);var p=new Vector3(6-12*u,floor+.08f+.12f*NativeEntranceMath.Rand(22300,i),NativeEntranceMath.Signed(22301,i)*5+2);
                    fx.Smoke(p,Ice,.18f*level*(float)Math.Sin(Math.PI*u),.5f+.6f*u,u,22300+i,floor);
                }
            }
            var head=Anchor(NativeEntranceAnchor.Head,t);var chest=Anchor(NativeEntranceAnchor.Chest,t);
            // The visor waking.
            float visor=NativeEntranceMath.Ramp(t,VisorAt,VisorAt+.25f);
            if(visor>.01f){var f=Vector3.Transform(Vector3.UnitZ,Quaternion.CreateFromAxisAngle(Vector3.UnitY,Heading(t)));fx.Glow(head+f*.13f,Vector3.Lerp(Visor,HeatHot,NativeEntranceMath.Ramp(t,HeatAt,CrackAt))*(1.1f*visor+.8f*Flash(t,VisorAt,.04f,.3f)),0,.12f,1,0,22400);}
            // Heat lines up the limbs, steady in Reduced effects.
            float lines=NativeEntranceMath.Ramp(t,HeatAt,HeatAt+.5f)*(1-.6f*NativeEntranceMath.Ramp(t,PartAt,CardAt));
            if(lines>.01f)
            {
                Span<NativeEntranceAnchor> path=stackalloc NativeEntranceAnchor[]{NativeEntranceAnchor.LeftHand,NativeEntranceAnchor.LeftElbow,NativeEntranceAnchor.LeftShoulder,NativeEntranceAnchor.Chest,NativeEntranceAnchor.RightShoulder,NativeEntranceAnchor.RightElbow,NativeEntranceAnchor.RightHand,NativeEntranceAnchor.Pelvis,NativeEntranceAnchor.LeftKnee,NativeEntranceAnchor.RightKnee};
                for(int i=0;i<path.Length;i++)
                {
                    float beat=Context.Reduced?1:.75f+.25f*(float)Math.Sin(t*4.2f-i*.7f);
                    fx.Glow(Anchor(path[i],t),Heat*(.55f*lines*beat),0,.09f,0,0,22410+i);
                }
            }
            // Steam: off the shoulders as it warms, pouring as it rises, curling after.
            {
                int n=low?6:16;float level=NativeEntranceMath.Ramp(t,HeatAt+.2f,CrackAt)*.5f+NativeEntranceMath.Ramp(t,CrackAt,RiseAt)*.5f-.45f*NativeEntranceMath.Ramp(t,PartAt+.5f,Total);
                if(level>.01f)for(int i=0;i<n;i++)
                {
                    float life=1.3f+.6f*NativeEntranceMath.Rand(22500,i),u=((t+i*life/n)/life)%1f;
                    var from=i%3==0?Anchor(NativeEntranceAnchor.LeftShoulder,t-u*life*.3f):i%3==1?Anchor(NativeEntranceAnchor.RightShoulder,t-u*life*.3f):chest;
                    var p=from+new Vector3(NativeEntranceMath.Signed(22501,i)*.15f,.1f+1.1f*u,NativeEntranceMath.Signed(22502,i)*.12f)+Wind*(1.4f*u*u*(.3f+.7f*storm));
                    fx.Smoke(p,Steam,.32f*level*(float)Math.Sin(Math.PI*u),.18f+.7f*u,u,22500+i,floor);
                }
            }
            // The frost breaking: shards flicked off, a burst of steam and powder.
            {
                float a=t-CrackAt;
                if(a>=0&&a<1.2f)
                {
                    int n=low?10:26;
                    for(int i=0;i<n;i++)
                    {
                        float life=.6f+.5f*NativeEntranceMath.Rand(22600,i);if(a>life)continue;float u=a/life;
                        var dir=Vector3.Normalize(new Vector3(NativeEntranceMath.Signed(22601,i),.3f+.8f*NativeEntranceMath.Rand(22602,i),NativeEntranceMath.Signed(22603,i)));
                        var from=chest+new Vector3(NativeEntranceMath.Signed(22604,i)*.22f,NativeEntranceMath.Signed(22605,i)*.35f,NativeEntranceMath.Signed(22606,i)*.12f);
                        var v=dir*(1.6f+1.6f*NativeEntranceMath.Rand(22607,i));var p=from+v*a+new Vector3(0,-4.9f*a*a,0);if(p.Y<floor+.01f)continue;
                        fx.Streak(p,v-new Vector3(0,9.8f*a,0),Ice*(1.4f*(1-u)),.018f,.05f,.03f,.3f,u,22600+i);
                    }
                    int m=low?5:12;
                    for(int i=0;i<m;i++){float u=Math.Min(1,a/1.2f);var d=Vector3.Normalize(new Vector3(NativeEntranceMath.Signed(22650,i),.6f,NativeEntranceMath.Signed(22651,i)));fx.Smoke(chest+d*(.2f+1.0f*u)+new Vector3(0,.3f*u,0),Steam,.45f*(1-u),.3f+.9f*u,u,22650+i,floor);}
                    float pop=Flash(t,CrackAt,.02f,.2f);if(pop>.01f)fx.Glow(chest,HeatHot*(1.4f*pop),0,.9f,1,0,22690);
                }
            }
            // The pulse: a dome of steam and blown snow racing out over the ground, the ground left dark and wet.
            {
                float a=t-PulseAt;
                if(a>=0&&a<2.2f)
                {
                    int n=low?12:28;
                    for(int i=0;i<n;i++)
                    {
                        float ang=i*6.2831853f/n+NativeEntranceMath.Signed(22700,i)*.1f,life=1.4f+.7f*NativeEntranceMath.Rand(22701,i);if(a>life)continue;float u=a/life,reach=.4f+4.2f*NativeEntranceMath.EaseOut(u);
                        var dir=new Vector3((float)Math.Cos(ang),0,(float)Math.Sin(ang));var p=new Vector3(0,floor,.2f)+dir*reach+new Vector3(0,.25f+1.3f*u*(.3f+.7f*NativeEntranceMath.Rand(22702,i)),0);
                        fx.Smoke(p,i%2==0?Steam:Ice,.5f*(1-u)*NativeEntranceMath.Ramp(a,0,.08f),.6f+1.8f*u,u,22700+i,floor);
                    }
                    if(a<.7f){int m=low?10:24;for(int i=0;i<m;i++){float ang=i*6.2831853f/m+.13f,u=a/.7f;var dir=new Vector3((float)Math.Cos(ang),0,(float)Math.Sin(ang));fx.Streak(new Vector3(0,floor+.04f+.1f*NativeEntranceMath.Rand(22750,i),.2f)+dir*(.5f+4.5f*u),dir*6,Ice*(1.1f*(1-u)),.014f,.18f,.04f,.8f,u,22750+i);}}
                    float flash=Flash(t,PulseAt,.04f,.45f);if(flash>.01f){fx.Glow(chest,HeatHot*(2.6f*flash),0,1.8f,1,0,22790);fx.Glow(new Vector3(0,floor+.2f,.2f),Heat*(1.8f*flash),0,3.2f,1,0,22791);}
                }
                // The wet ground steaming after.
                float r=Thaw(t);if(r>.05f){int m=low?3:8;for(int i=0;i<m;i++){float u=((t*.5f+i/(float)m)%1f),ang=i*2.39f;var p=new Vector3((float)Math.Cos(ang)*r*.6f,floor+.05f+.7f*u,.2f+(float)Math.Sin(ang)*r*.6f);fx.Smoke(p,Steam,.14f*(float)Math.Sin(Math.PI*u)*(1-.5f*NativeEntranceMath.Ramp(t,CardAt,Total)),.3f+.5f*u,u,22800+i,floor);}}
            }
            // The stars over the outpost once the storm parts.
            {
                float clear=1-storm;if(clear>.02f){int n=low?20:60;for(int i=0;i<n;i++){var d=Vector3.Normalize(new Vector3(NativeEntranceMath.Signed(22900,i),.25f+.75f*NativeEntranceMath.Rand(22901,i),-.3f-.7f*NativeEntranceMath.Rand(22902,i)));fx.Glow(d*150,White*(.5f*clear*(.5f+.5f*NativeEntranceMath.Rand(22903,i))),0,.45f,3,0,22900+i);}}
            }
        }
        private static float Wrap(float x,float lo,float hi){float w=hi-lo;x=(x-lo)%w;if(x<0)x+=w;return lo+x;}
        /// <summary>The frost coat: a pale shell over the body until the frost breaks.</summary>
        public override int Ghosts(float t,Span<NativeEntranceGhost> into)
        {
            float w=1-NativeEntranceMath.Ramp(t,CrackAt-.05f,CrackAt+.45f);if(w<.02f||into.Length<1)return 0;
            into[0]=new NativeEntranceGhost{Lag=0,Opacity=.5f*w,Color=Ice};return 1;
        }

        // ---- the name card and the sound ----
        public override void Card(float t,ref NativeIntroCardState card)
        {
            base.Card(t,ref card);card.Style=NativeIntroCardStyle.Wipe;card.Ink=new Vector3(.94f,.97f,1);card.Glow=HeatHot*1.3f;
        }
        private static readonly NativeIntroCue[] cues=
        {
            new NativeIntroCue(0,"intro2.whiteout.storm","field.storm","dash",.5f,.8f),
            new NativeIntroCue(VisorAt,"intro2.whiteout.visor","ui.install.surge","install",.5f),
            new NativeIntroCue(HeatAt,"intro2.whiteout.heat","chip.delayed.charge","electric-hook",.5f),
            new NativeIntroCue(CrackAt,"intro2.whiteout.crack","status.frost-shatter","stone",.85f),
            new NativeIntroCue(CrackAt+.04f,"intro2.whiteout.steam","chip.bomb.fuse-hiss","fire",.45f),
            new NativeIntroCue(RiseAt+.05f,"intro2.whiteout.rise","move.step.snow","wood",.55f),
            new NativeIntroCue(PulseAt,"intro2.whiteout.pulse","blast.frost","explosion",.75f),
            new NativeIntroCue(PartAt,"intro2.whiteout.calm","chip.heal","install",.45f),
            new NativeIntroCue(CardAt,"intro2.whiteout.name","chip.critical","install",.75f),
        };
        public override NativeIntroCue[] Cues=>cues;
    }
}
