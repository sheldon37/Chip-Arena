using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// "Sundown", the pistol's v2 intro (I4, owner 2026-11-20: replaces Deadeye; docs/INTROS-RANGED.md). 7.8 s, the name at 6.0 s.
    /// A frontier street at sundown: the pilot walks out of the low sun's glare toward an old timber belfry at the edge of town, a
    /// tumbleweed crossing. The bell tolls by itself, once, twice; on the second toll its headstock cracks. A beat of stillness, the
    /// hand by the hip. The belfry gives way: shingles and boards come down and the bell drops through them; the pilot draws from
    /// the hip and fires one shot, slowed, the camera riding the bullet through the falling debris to strike the bell as it lands.
    /// It rings out, tumbles off its plinth and rolls down the street, rocking to rest at the pilot's boots. The pilot lifts the
    /// smoking barrel and blows the smoke away; the name, the bell's last ring.
    /// Presentation only: pure functions of the presentation clock, thinned by quality; every beat kept in Reduced effects.
    /// </summary>
    public sealed class NativeIntroSundown:NativeRangedIntro
    {
        public const string Id=NativeIntroCatalog.WeaponPrefix+"pistol";
        public const float Total=7.8f,CardAt=6.0f;
        // Beats (presentation s).
        public const float WalkTo=1.32f,Toll1=1.15f,Toll2=1.85f,Break=2.8f,FallFrom=2.82f,DrawFrom=2.86f,AimFrom=3.1f,ShotAt=3.55f,HitAt=3.72f,Plinth=3.78f,
            OffEdge=3.98f,Ground=4.18f,RollTo=5.3f,BlowFrom=4.5f,BlowAt=4.85f,BlowTo=5.2f,Rest=5.5f,LastRing=6.02f;
        // The set (Unity space, ranged_sundown.py).
        private static readonly Vector3 BelfryU=new Vector3(-2.4f,0,10.8f);private const float HangY=6.32f,BellH=1.1f,PlinthY=.9f;
        /// <summary>The bell's hang point and where it comes to rest (source frame).</summary>
        public static readonly Vector3 Hang=S(BelfryU.X,HangY,BelfryU.Z),RestAt=L(1.15f,.0f,.95f);
        private static readonly Vector3 Sun=Vector3.Normalize(SDir(0,.05f,1)),Gold=NativeEntranceFx.Hex(0xffc070),Ember=NativeEntranceFx.Hex(0xff8a3c),Dust=NativeEntranceFx.Hex(0xb08a62),Rose=NativeEntranceFx.Hex(0xff6a88);
        private static readonly Vector3 WindmillHub=S(-18.5f,9.2f+.28f,22.0f+.42f),Saloon=S(-(10f-.14f)+.04f,.42f,-7.1f+.56f);
        private const int Shingles=26,Planks=8,Splinters=18;
        private readonly Vector3[] at=new Vector3[Shingles];private readonly Quaternion[] turn=new Quaternion[Shingles];

        public NativeIntroSundown():base(Id,Total,CardAt,ShotAt,0xffc070)
        {
            // ---- the keyed body: the walk out, the stop, the hand by the hip, the draw to the bell, the shot, the blow, the rest ----
            float aimYaw=Facing+(float)Math.Atan2(2.4f,10.8f);
            Start=Stance(.14f,-1.35f+.07f,.05f,-.16f,-1.35f-.32f,-.35f);
            Steps=new[]
            {
                Step(false,.05f,.45f,-.16f,-1.35f+.42f,-.3f,.07f),Step(true,.45f,.85f,.14f,-1.35f+.82f,.05f,.07f),Step(false,.85f,1.2f,-.16f,-.12f,-.32f,.06f),
                Step(true,1.18f,1.42f,.15f,.07f,.08f,.05f),
                // squaring to the bell for the shot, and back to the street for the rest
                Step(false,2.86f,3.02f,-.2f,-.06f,-.25f,.04f,aimYaw),Step(true,Rest-.22f,Rest+.06f,.15f,.08f,.08f,.04f),
            };
            var stand=NativeEntranceKeys.Stand();var k=new NativeEntranceKeys();
            k.Key(0,stand).Root(0,.88f,-1.35f,Facing).J(RA,-.18f,0,-.2f).J(RE,-.35f).J(LA,.1f,0,.16f).J(Hd,.06f);
            k.Key(WalkTo,stand).Root(0,.88f,0,Facing).J(RA,-.16f,0,-.22f).J(RE,-.3f).J(LA,-.05f,0,.15f).J(Hd,.02f).PassKey();
            // the tolls: the head comes up toward the belfry
            k.Key(Toll2-.1f,stand).Root(0,.88f,0,Facing).J(Hd,-.12f,.12f,0).J(C,-.03f,.06f,0).J(RA,-.16f,0,-.24f).J(RE,-.3f).J(LA,-.05f,0,.15f);
            // the hand by the hip, fingers loose: the stillness
            k.Key(2.45f,stand).Root(0,.86f,0,Facing).Drop(-.03f).J(Hd,-.08f,.1f,0).J(C,0,.04f,0).J(RA,-.05f,.1f,-.32f).J(RE,-.55f).J(LA,-.05f,0,.18f).J(LE,-.2f);
            k.Key(Break,stand).Root(0,.86f,0,Facing).Drop(-.04f).J(Hd,-.1f,.12f,0).J(C,0,.04f,0).J(RA,-.02f,.1f,-.34f).J(RE,-.6f).J(LA,-.05f,0,.2f).J(LE,-.22f);
            // the draw and the aim at the bell (squared to it), the shot's kick, the follow-through
            k.Key(AimFrom,stand).Root(0,.86f,0,aimYaw).Drop(-.05f).J(C,-.02f,.05f,0).J(Hd,.02f,0,0).J(RA,-1.48f,.05f,-.08f).J(RE,-.08f).J(LA,-.3f,0,.25f).J(LE,-.6f).PassKey();
            k.Key(ShotAt,stand).Root(0,.86f,0,aimYaw).Drop(-.05f).J(C,-.02f,.05f,0).J(Hd,.03f,0,0).J(RA,-1.5f,.05f,-.08f).J(RE,-.06f).J(LA,-.3f,0,.25f).J(LE,-.6f);
            k.Key(ShotAt+.05f,stand).Root(0,.86f,0,aimYaw).Drop(-.05f).J(C,-.06f,.05f,0).J(Hd,-.02f,0,0).J(RA,-1.85f,.05f,-.08f).J(RE,-.3f).J(LA,-.3f,0,.25f).J(LE,-.6f).ImpactKey();
            k.Key(4.2f,stand).Root(0,.86f,0,aimYaw).Drop(-.05f).J(C,-.02f,.04f,0).J(Hd,.02f,0,0).J(RA,-1.45f,.05f,-.08f).J(RE,-.1f).J(LA,-.3f,0,.25f).J(LE,-.6f);
            // the barrel up by the face, the head leaning in to blow
            k.Key(BlowFrom+.25f,stand).Root(0,.87f,0,aimYaw-.1f).Drop(-.03f).J(C,0,-.05f,0).J(Hd,.05f,-.15f,0).J(RA,-.45f,.35f,-.5f).J(RE,-2.05f).J(LA,-.08f,0,.18f).J(LE,-.25f);
            k.Key(BlowAt,stand).Root(0,.87f,0,aimYaw-.1f).Drop(-.03f).J(C,.02f,-.08f,0).J(Hd,.16f,-.28f,.05f).J(RA,-.42f,.38f,-.5f).J(RE,-2.1f).J(LA,-.08f,0,.18f).J(LE,-.25f);
            k.Key(BlowTo,stand).Root(0,.87f,0,aimYaw-.1f).Drop(-.03f).J(C,0,-.04f,0).J(Hd,.0f,-.1f,0).J(RA,-.45f,.3f,-.45f).J(RE,-1.95f).J(LA,-.08f,0,.18f).J(LE,-.25f);
            // down to the hip, squared to the street again: the hero's rest, weight on the back foot
            k.Key(Rest,stand).Root(0,.88f,0,Facing).Drop(-.02f).J(C,0,.06f,0).J(Hd,-.04f,-.05f,0).J(RA,-.1f,.05f,-.24f).J(RE,-.4f).J(LA,-.08f,0,.17f).J(LE,-.25f);
            k.Key(Total,stand).Root(0,.88f,0,Facing).Drop(-.02f).J(C,0,.07f,0).J(Hd,-.05f,-.06f,0).J(RA,-.1f,.05f,-.24f).J(RE,-.4f).J(LA,-.08f,0,.17f).J(LE,-.25f);Keys=k;
            // The walk's root: the keys carry it along the facing.
            RootPath=(t,r)=>{float u=NativeEntranceMath.Clamp01(t/WalkTo);var p=Local(0,0,-1.35f*(1-u*(1.15f-.15f*u*u)));return new Vector3(p.X,r.Y,p.Z);};

            // ---- the camera ----
            var bellLook=L(2.2f,5.4f,10.6f);var muzzle=L(-.12f,1.42f,.72f);var hit=BellAt(HitAt);
            var dir=Vector3.Normalize(hit-muzzle);var side=Vector3.Normalize(Vector3.Cross(Vector3.UnitY,dir));
            Rig.Dolly(0,L(-.62f,.3f,-7.4f),L(-.48f,.36f,-6.1f),L(.6f,2.0f,9),L(.8f,2.4f,9),34,30).Ease(NativeIntroEase.Linear).Focus(3.4f,3.2f,.4f)
               .Dolly(1.08f,L(-1.18f,1.56f,.28f),L(-1.06f,1.57f,.36f),L(0,1.56f,.25f),L(0,1.58f,.25f),24,22).Focus(0,0,.55f)
               .Dolly(1.62f,L(-.78f,1.74f,-2.25f),L(-.7f,1.72f,-1.95f),bellLook,bellLook+new Vector3(0,.05f,0),16,14).Focus(0,0,.3f)
               .Dolly(2.24f,L(-1.25f,1.02f,1.45f),L(-1.05f,1.0f,1.25f),L(-.25f,.98f,.05f),L(-.22f,1.0f,.08f),26,24).Focus(0,0,.6f)
               .Dolly(2.78f,L(-5.6f,1.55f,-3.2f),L(-5.3f,1.62f,-2.75f),L(1.3f,2.6f,4.0f),L(1.4f,2.45f,4.1f),45,43).Focus(0,0,.2f)
               // the bullet: riding beside it from the muzzle to the bell, through the falling boards
               .Dolly(ShotAt-.02f,muzzle-dir*.55f+side*.32f+new Vector3(0,.16f,0),hit-dir*1.1f+side*.42f+new Vector3(0,.22f,0),muzzle+dir*3,hit+dir*.4f,52,46).Ease(NativeIntroEase.Linear)
               .Dolly(HitAt+.02f,L(4.8f,.62f,6.1f),L(4.5f,.7f,6.35f),L(2.3f,1.4f,10.2f),L(1.6f,.7f,8.4f),42,40).Focus(0,0,.2f)
               .Dolly(Ground-.04f,L(1.35f,.3f,3.2f),L(1.25f,.32f,2.9f),L(1.8f,.45f,7.4f),L(.4f,.7f,.6f),38,36).Focus(0,0,.35f)
               .Dolly(BlowFrom,L(.82f,1.62f,1.65f),L(.7f,1.64f,1.48f),L(.02f,1.6f,0),L(.02f,1.62f,.02f),30,28).Focus(0,0,.6f)
               .Dolly(Rest-.25f,L(-1.5f,.7f,3.45f),L(-1.18f,.72f,3.0f),L(.15f,1.0f,0),L(.15f,1.06f,0),34,31).Ease(NativeIntroEase.Out).In(NativeIntroCut.Blend,.55f).Focus(0,0,.45f)
               .Bars(0,1).Bars(CardAt-.35f,.55f).Black(0,1).Black(.06f,0);
            // Slow motion through the shot and the strike; repaid while the bell rolls.
            Ramps.Ramp(3.48f,.28f,.85f,1.25f);
        }
        // ---- the model's clips (the keyed rig drives the rest: the stillness, the blow, the hero) ----
        /// <summary>The blow: the gun hand brought up by the mouth, the barrel raised, then down again (the keyed rig drives the model here).</summary>
        protected override void Arms(float t,CapturedRig.Pose pose,Vector3 root,Quaternion rotation)
        {
            float w=NativeEntranceMath.Ramp(t,BlowFrom,BlowFrom+.3f)*(1-NativeEntranceMath.Ramp(t,BlowTo,Rest));if(w<=.001f)return;
            var fwd=Vector3.Transform(Vector3.UnitZ,rotation);var left=Vector3.Cross(Vector3.UnitY,fwd);pose.World();
            // The head joint is the skull's pivot, ModelHeadLift below the eyes: the muzzle goes just in front of the mouth and a little to
            // the right (the barrel upright beside the visor, never across it), so the wrist sits a barrel's length below, by the chest.
            var head=root+Vector3.Transform(pose.WorldPosition[Hd],rotation);var barrel=Vector3.Normalize(Vector3.UnitY+fwd*.08f+left*.12f);
            var muzzle=head+new Vector3(0,ModelHeadLift-.1f,0)+fwd*.3f-left*.07f;var at=muzzle-barrel*NativeWeaponDressing.MuzzleZ("pistol");
            NativeEntrancePose.Reach(pose,root,rotation,false,at,Look(barrel,-fwd),w);
        }
        public override bool Clip(float t,out string clip,out float time)
        {
            clip=null;time=0;
            if(t<WalkTo+.06f){clip="Armed-Walk-Slow";time=(t+.4f)%1.6f;return true;}
            if(t>=DrawFrom&&t<AimFrom){clip="Armed-Unsheath-R-FromArmed-Hips";time=.15f+.7f*NativeEntranceMath.Clamp01((t-DrawFrom)/(AimFrom-DrawFrom));return true;}
            if(t>=AimFrom&&t<ShotAt-.08f){clip="Armed-Idle-Pistol-R";time=t-AimFrom;return true;}
            if(t>=ShotAt-.08f&&t<ShotAt+.4f){clip="Pistol-Attack-R1";time=.32f+(t-(ShotAt-.08f));return true;}
            if(t>=ShotAt+.4f&&t<BlowFrom){clip="Armed-Idle-Pistol-R";time=.6f+(t-ShotAt-.4f);return true;}
            return false;
        }
        public override float Fade(float t)=>t<AimFrom+.05f?.1f:t<BlowFrom?.08f:.22f;

        // ---- the bell: swinging, falling, struck, off the plinth, rolling to the boots ----
        /// <summary>The bell's swing about its headstock (radians, toward the street negative) before the break.</summary>
        public static float Swing(float t)
        {
            if(t<.35f)return 0;float amp=.34f*NativeEntranceMath.Ramp(t,.35f,1.1f);return -amp*(float)Math.Sin(Math.PI/.7f*(t-.8f));
        }
        /// <summary>The bell's crown (its hang point on the bell) at `t` and its turn (source frame).</summary>
        public static Vector3 BellAt(float t)=>BellPose(t,out _);
        public static Vector3 BellPose(float t,out Quaternion q)
        {
            var axis=SDir(1,0,0);
            if(t<FallFrom)
            {
                // hung: swinging, lurching down a hand's width at the crack
                float lurch=NativeEntranceMath.Ramp(t,Toll2,Toll2+.12f)*.08f;q=Quaternion.CreateFromAxisAngle(axis,Swing(t))*Turn;
                if(t>=Toll2)q=Quaternion.CreateFromAxisAngle(SDir(0,0,1),.06f*NativeEntranceMath.Ramp(t,Toll2,Toll2+.15f))*q;
                return Hang-new Vector3(0,lurch,0);
            }
            var start=Hang-new Vector3(0,.08f,0);
            if(t<Plinth)
            {
                // free fall from the break (a little drift and spin once the shot strikes)
                float a=t-FallFrom,h=Math.Max(0,t-HitAt);var p=start+new Vector3(0,-4.9f*a*a,0)+SDir(.3f,0,-1)*(1.3f*h);
                q=Quaternion.CreateFromAxisAngle(axis,Swing(FallFrom)*(1-a)-.9f*h*h*3)*Turn;return p;
            }
            // the plinth: lands on its lip, tips forward over the edge toward the street and drops into the dirt
            var onPlinth=new Vector3(start.X,PlinthY+BellH+Floor,start.Z)+SDir(.3f,0,-1)*(1.3f*(Plinth-HitAt));
            if(t<Ground)
            {
                float u=NativeEntranceMath.Clamp01((t-Plinth)/(Ground-Plinth));float tip=NativeEntranceMath.Smoother(u)*1.57f;
                var forward=SDir(.15f,0,-1);var p=onPlinth+forward*(1.35f*u)+new Vector3(0,-(PlinthY+.42f)*u*u,0);
                q=Quaternion.CreateFromAxisAngle(Vector3.Normalize(Vector3.Cross(Vector3.UnitY,forward)),-tip)*Turn;return p;
            }
            // rolling on its side along a gentle arc to the boots, slowing, then rocking still
            float r=NativeEntranceMath.Clamp01((t-Ground)/(RollTo-Ground));float s=1-(1-r)*(1-r)*(1-r);
            var from=onPlinth+SDir(.15f,0,-1)*1.35f;from.Y=Floor+.5f;var to=RestAt+new Vector3(0,Floor+.5f,0);
            var mid=(from+to)*.5f+SDir(-1.1f,0,0);var a2=Vector3.Lerp(Vector3.Lerp(from,mid,s),Vector3.Lerp(mid,to,s),s);
            var heading=Vector3.Normalize(Vector3.Lerp(mid-from,to-mid,s));var axle=Vector3.Normalize(Vector3.Cross(Vector3.UnitY,heading));
            float rolled=Vector3.Distance(from,to)*s/.48f,rock=t>RollTo?.22f*(float)(Math.Exp(-(t-RollTo)*2.6f)*Math.Sin((t-RollTo)*7.5f)):0;
            q=Quaternion.CreateFromAxisAngle(heading,rock)*Quaternion.CreateFromAxisAngle(axle,rolled)*Look(axle,Vector3.UnitY)*Quaternion.CreateFromAxisAngle(Vector3.UnitX,1.5708f);
            return a2;
        }
        /// <summary>Debris `i` at `t` (falling from the belfry's top at the break): shingles, boards, splinters; false before it falls.</summary>
        private static bool Debris(int kind,int i,float t,out Vector3 p,out Quaternion q)
        {
            int seed=3100+kind*97+i;float born=(kind==2&&i<6?Toll2:Break)+.04f*NativeEntranceMath.Rand(seed,1);float a=t-born;p=default;q=Quaternion.Identity;if(a<0)return false;
            var top=S(BelfryU.X,kind==0?7.4f:HangY+.1f,BelfryU.Z);var spread=new Vector3(NativeEntranceMath.Signed(seed,2)*1.4f,NativeEntranceMath.Rand(seed,3)*.6f,NativeEntranceMath.Signed(seed,4)*1.4f);
            var v=new Vector3(NativeEntranceMath.Signed(seed,5)*2.2f,1.2f+2.4f*NativeEntranceMath.Rand(seed,6),NativeEntranceMath.Signed(seed,7)*2.2f)+SDir(0,0,-1)*(kind==0?.6f:1.6f);
            float flutter=kind==0?.55f:1;var q0=top+spread;float g=9.8f*flutter;var pos=q0+v*a*(kind==0?.7f:1)+new Vector3(0,-.5f*g*a*a,0);
            float floorY=Floor+(kind==0?.012f:.03f);bool rest=pos.Y<floorY;if(rest){pos.Y=floorY;}
            float spin=(rest?Math.Min(a,(float)Math.Sqrt(2*(q0.Y-floorY)/g)):a)*(3+6*NativeEntranceMath.Rand(seed,8));
            q=Quaternion.CreateFromAxisAngle(Vector3.Normalize(new Vector3(NativeEntranceMath.Signed(seed,9),.5f,NativeEntranceMath.Signed(seed,10))),spin+seed);
            if(rest)q=Quaternion.CreateFromAxisAngle(Vector3.UnitY,seed*.7f);
            p=pos;return true;
        }

        // ---- the set ----
        protected override bool Scene(float t,INativeIntroSet set)
        {
            if(!set.Has("sundown/town"))return false;View(t,out var eye,out _);var ground=new Vector3(eye.X,0,eye.Z);
            float shot=Flash(t,ShotAt,.01f,.12f);
            set.Piece("sundown/sky",ground,Turn,1,Vector3.One,Vector3.One*(1+.25f*shot));
            set.Piece("sundown/far",ground,Turn,1,Vector3.One,Vector3.One);
            set.Piece("sundown/town",Vector3.Zero,Turn,1,Vector3.One,Vector3.One);
            // the bell and its headstock (whole until the break, then two halves swinging down from their ends)
            var bell=BellPose(t,out var bq);set.Piece("sundown/bell",bell,bq,1,Vector3.One,Vector3.One);
            if(t<Break)set.Piece("sundown/headstock",Hang,Quaternion.CreateFromAxisAngle(SDir(0,0,1),t>=Toll2?.03f:0)*Turn,1,Vector3.One,Vector3.One);
            else for(int s=-1;s<=1;s+=2)
            {
                float a=t-Break,fall=Math.Min(1,a/.55f);float drop=a<.55f?0:4.9f*(a-.55f)*(a-.55f);var end=S(BelfryU.X-s*1.15f,HangY,BelfryU.Z);end.Y=Math.Max(PlinthY+.1f,end.Y-drop);
                var along=SDir(s,0,0);var axis=SDir(0,0,1);var q=Quaternion.CreateFromAxisAngle(axis,-s*1.25f*NativeEntranceMath.Smoother(fall))*Look(along,Vector3.UnitY);
                set.Piece("sundown/beam-half",end,q*Quaternion.CreateFromAxisAngle(Vector3.UnitY,-1.5708f),1,Vector3.One,Vector3.One);
            }
            // the roof: tilting back off the frame at the break and down behind it
            {
                var eaves=S(BelfryU.X,6.9f,BelfryU.Z);float a=Math.Max(0,t-Break);float tilt=NativeEntranceMath.Smoother(Math.Min(1,a/.7f))*1.1f;
                var back=SDir(0,0,1);var hinge=Vector3.Normalize(Vector3.Cross(back,Vector3.UnitY));var drop=a<.35f?0:4.9f*(a-.35f)*(a-.35f);
                var p=eaves+back*(1.4f*Math.Min(1,a/.8f))+new Vector3(0,-Math.Min(drop,6.9f-.2f),0);
                set.Piece("sundown/roof",p,Quaternion.CreateFromAxisAngle(hinge,tilt*(1+Math.Min(1,drop/6))*.9f)*Turn,1,Vector3.One,Vector3.One);
            }
            // the windmill's fan in the evening wind; the saloon's swing doors stirring
            set.Piece("sundown/blades",WindmillHub,Turn*Quaternion.CreateFromAxisAngle(Vector3.UnitZ,t*.9f),1,Vector3.One,Vector3.One);
            for(int s=0;s<2;s++)
            {
                float swing=.18f*(float)Math.Sin(t*2.1f+s*1.3f)+.35f*NativeEntranceMath.Pulse(t,Ground,.03f,.5f)*(s==0?1:-1);
                var hinge=Saloon+SDir(0,0,s==0?0:-1.12f);set.Piece("sundown/batwing",hinge,SYaw(s==0?-1.5708f+swing:1.5708f-swing),1,Vector3.One,Vector3.One);
            }
            // the tumbleweed bounding across in front of the walk
            if(t<2.4f)
            {
                float u=(t-.15f)/2.0f;if(u>0&&u<1){var p=L(6.5f-13f*u,.36f+.35f*(float)Math.Abs(Math.Sin(u*9.5f))*(1-u*.4f),2.6f+.6f*u);set.Piece("sundown/tumbleweed",p,Quaternion.CreateFromAxisAngle(Vector3.Normalize(new Vector3(.2f,.1f,1)),-u*16),1,Vector3.One,Vector3.One);}
            }
            // the debris: shingles, boards, splinters (instanced)
            Batch(set,0,Shingles,"sundown/shingle",t);Batch(set,1,Planks,"sundown/plank",t);Batch(set,2,Splinters,"sundown/splinter",t);
            return true;
        }
        private void Batch(INativeIntroSet set,int kind,int count,string prop,float t)
        {
            int n=0;for(int i=0;i<count&&n<at.Length;i++){if(Debris(kind,i,t,out var p,out var q)){at[n]=p;turn[n]=q;n++;}}
            if(n>0)set.Batch(prop,new ReadOnlySpan<Vector3>(at,0,n),new ReadOnlySpan<Quaternion>(turn,0,n),1,Vector3.One);
        }
        protected override NativeIntroVoidLook VoidLook(float t){var v=NativeIntroVoidLook.Of(Gold);v.Far=NativeEntranceFx.Hex(0x3a2418);v.Floor=NativeEntranceFx.Hex(0x4a3420);v.Pool=Gold;v.Rim=Ember;return v;}

        // ---- the light: the low sun behind the belfry, the dusk sky, the lamps; the shot's flash ----
        public override void Lights(float t,NativeEntranceLights l)
        {
            l.Clear();var haze=NativeEntranceFx.Hex(0xc98a62);l.Background=haze;l.Fog=haze;l.FogNear=18;l.FogFar=520;l.DriveProbe=true;
            l.Sky=NativeEntranceFx.Hex(0x8a7090,.55f);l.Ground=NativeEntranceFx.Hex(0x7a5236,.45f);
            l.Directional(Sun+new Vector3(0,.12f,0),NativeEntranceFx.Hex(0xffa860,2.3f));
            l.Directional(-Sun+new Vector3(0,.8f,0),NativeEntranceFx.Hex(0x6a78b8,.32f));
            var chest=Anchor(NativeEntranceAnchor.Chest,t);
            // a warm bounce off the street up into the face, the saloon's pink neon, a lamp
            l.Add(chest+SDir(0,-.8f,1.6f),NativeEntranceFx.Hex(0xffb070,1.4f),5);
            l.Add(S(-9.6f,4.8f,-7.1f),Rose*1.6f,7);l.Add(S(7.8f,2.5f,3.5f),NativeEntranceFx.Hex(0xffc27a,1.2f),5);
            float shot=Flash(t,ShotAt,.01f,.1f);if(shot>.01f)l.Add(Wrist(t)+SDir(0,.1f,.4f),NativeEntranceFx.Hex(0xfff0c0,14*shot),5);
            float ring=Flash(t,HitAt,.01f,.18f);if(ring>.01f)l.Add(BellAt(HitAt)+SDir(0,0,-.8f),NativeEntranceFx.Hex(0xffe0a0,10*ring),6);
            float card=NativeEntranceMath.Ramp(t,CardAt-.4f,CardAt+.4f);if(card>.01f)l.Add(chest+SDir(-1.6f,.6f,1.4f),Gold*(2.2f*card),6);
        }

        // ---- effects ----
        public override void Effects(float t,INativeEntranceFx fx,float q)
        {
            View(t,out var eye,out var fwd);float floor=Floor;
            // the sun: a huge low disc of light behind the belfry (in the camera's plane, so it holds its shape), its halo
            var sun=eye+Sun*165;fx.Glow(sun,NativeEntranceFx.Hex(0xffd8a0,3.2f),0,22,1,0,7000);fx.Glow(sun,NativeEntranceFx.Hex(0xff9a50,1.2f),0,60,0,0,7001);
            // gold dust hanging in the low light, wind-blown grit along the street
            Motes(fx,t,q,L(0,1.4f,2),new Vector3(5,1.4f,5),Gold*.12f,.03f,30,7010,.25f);
            int wisps=q<.5f?4:9;
            for(int i=0;i<wisps;i++){float u=((t*.35f+i/(float)wisps)%1f);var p=L(5.5f-11*u,.18f+.2f*NativeEntranceMath.Rand(7040,i),-3+8*NativeEntranceMath.Rand(7041,i));fx.Smoke(p,Dust,.18f*(float)Math.Sin(Math.PI*u),.6f+.6f*u,u,7050+i,floor);}
            // the walk's boot dust
            for(int s=0;s<4;s++){float land=StepLands[s];NativeEntranceFx.GroundDust(fx,t,land,Local(s%2==0?-.16f:.14f,0,-1.35f+.42f+.4f*s),q<.5f?3:6,.05f,.6f,.7f,Dust,.22f,.18f,7100+s*10,q,floor);}
            // the tolls: dust shaken from the frame; the crack's splinter burst
            for(int k=0;k<2;k++){float at2=k==0?Toll1:Toll2;NativeEntranceFx.Jet(fx,t,at2,at2+.25f,S(BelfryU.X,6.7f,BelfryU.Z),new Vector3(0,-1,0),24,.6f,1.3f,.35f,Dust,.25f,7200+k*20,q,floor,.05f,.6f);}
            NativeEntranceFx.Sparks(fx,t,Toll2,Hang+new Vector3(0,.15f,0),new Vector3(0,.3f,0),1,q<.5f?6:12,3.2f,.6f,9.8f,.6f,NativeEntranceFx.Hex(0xc8a070,1.2f),NativeEntranceFx.Hex(0x6a5038,.6f),.012f,7240,q,floor);
            // the break: a burst of dust and splinters from the top of the frame
            if(t>=Break&&t<Break+2.2f)
            {
                float a=t-Break;int n=q<.5f?8:18;
                for(int i=0;i<n;i++){float l=1.4f+.8f*NativeEntranceMath.Rand(7300,i);if(a>l)continue;float u=a/l;var d=NativeEntranceMath.Direction(7300,i);var p=S(BelfryU.X,6.8f,BelfryU.Z)+d*(1.6f*(1-(float)Math.Exp(-2.5f*a)))+new Vector3(0,-.6f*a,0);fx.Smoke(p,Dust,.35f*(1-u)*NativeEntranceMath.Smooth(u*6),.6f+1.4f*u,u,7300+i,floor);}
            }
            // the shot: the muzzle flash, the bullet's hot head and its fading line, the smoke
            var muzzle=Wrist(t)+Vector3.Normalize(BellAt(HitAt)-Wrist(t))*.26f;var flashDir=Vector3.Normalize(BellAt(HitAt)-muzzle);
            if(Drawn(t,out var drawn,out var drawnBore)){muzzle=drawn;flashDir=drawnBore;}
            if(t>=ShotAt-.01f&&t<ShotAt+.12f)Muzzle(fx,t,ShotAt,muzzle,flashDir,.16f,Gold,7400,q,.09f);
            if(t>=ShotAt&&t<HitAt+.05f)
            {
                var m0=Wrist(ShotAt)+Vector3.Normalize(BellAt(HitAt)-Wrist(ShotAt))*.26f;var target=BellAt(HitAt)+SDir(0,-.45f,-.3f);var d=target-m0;float len=d.Length();var dir=d/len;
                float u=NativeEntranceMath.Clamp01((t-ShotAt)/(HitAt-ShotAt));var head=m0+d*u;
                fx.Glow(head,NativeEntranceFx.Hex(0xfff2c8,3.2f),0,.05f,1,0,7420);fx.Glow(head,Gold*1.4f,0,.16f,0,0,7421);
                Line(fx,eye,fwd,head,head-dir*Math.Min(2.4f,len*u),NativeEntranceFx.Hex(0xffe0a0,3.0f),.026f,.9f,0,7422);fx.Glow(head,NativeEntranceFx.Hex(0xffd890,2.2f),0,.3f,0,0,7423);
                // the air it parts: a faint shimmer trail of tiny motes lingering behind
                int n=q<.5f?6:14;for(int i=0;i<n;i++){float v=(i+.5f)/n*u;var p=m0+d*v;float age=t-(ShotAt+(HitAt-ShotAt)*v);fx.Glow(p,Gold*(.3f*(float)Math.Exp(-age*6)),0,.03f+.05f*age,0,0,7430+i);}
            }
            // the strike: sparks off the bell's shoulder, a hot flash, a ring of dust shaken off it (puffs, not a ring line)
            var strike=BellAt(HitAt)+SDir(0,-.45f,-.3f);
            NativeEntranceFx.Sparks(fx,t,HitAt,strike,SDir(0,.4f,-1),1.1f,q<.5f?14:34,7.5f,.55f,9.8f,1.2f,NativeEntranceFx.Hex(0xfff0c0,3.2f),Ember*2.2f,.014f,7500,q,floor);
            float f=Flash(t,HitAt,.01f,.15f);if(f>.02f){fx.Glow(strike,Vector3.One*(3.2f*f),0,.4f,1,0,7540);Star(fx,eye,fwd,strike,NativeEntranceFx.Hex(0xffe6b0,2.4f*f),.7f*f+.1f,.4f,7541);}
            NativeEntranceFx.GroundDust(fx,t,Plinth,S(BelfryU.X,PlinthY,BelfryU.Z),q<.5f?8:16,.4f,2.4f,1.1f,Dust,.45f,.5f,7560,q,PlinthY);
            NativeEntranceFx.GroundDust(fx,t,Ground,BellAt(Ground+.02f)-new Vector3(0,.5f,0),q<.5f?10:22,.35f,3.2f,1.4f,Dust,.55f,.6f,7600,q,floor);
            // the roll's dust trail
            if(t>Ground&&t<RollTo+.4f){int n=q<.5f?4:8;for(int i=0;i<n;i++){float back=.12f*i;float tb=t-back;if(tb<Ground)continue;var p=BellAt(tb);p.Y=floor+.12f;float age=back;fx.Smoke(p+SDir(0,0,.0f),Dust,.25f*(1-age/(.12f*n)),.3f+.4f*age,age,7650+i,floor);}}
            // the smoke curling from the barrel after the shot, blown away at the blow
            if(t>ShotAt+.05f&&t<SmokeTo)
            {
                int n=q<.5f?5:10;float blow=NativeEntranceMath.Ramp(t,BlowAt-.05f,BlowAt+.25f);
                for(int i=0;i<n;i++){float u=((t-ShotAt)*.9f+i/(float)n)%1f;var p=muzzle+new Vector3((float)Math.Sin(i*2.3f+t*2)*.03f*(1+u),.04f+.45f*u,(float)Math.Cos(i*1.7f+t*2)*.03f*(1+u))+SDir(.6f,.1f,.4f)*(1.2f*blow*u);fx.Smoke(p,new Vector3(.8f,.78f,.76f),.28f*(1-u)*(1-blow*.7f),.05f+.18f*u,u,7700+i,floor);}
            }
            // the bell's last ring: a soft shimmer over it as the name lands
            float last=Flash(t,LastRing,.03f,.6f);if(last>.02f)fx.Glow(BellAt(t)+new Vector3(0,.3f,0),Gold*(1.2f*last),0,.6f,0,0,7760);
            // under the name: gold motes lifting in the light
            if(t>CardAt-.4f)NativeEntranceFx.Embers(fx,t,CardAt-.4f,Total,L(0,.2f,0),1.6f,q<.5f?10:22,.35f,2.4f,Gold*.6f,.03f,7800,q,.4f);
        }
        private const float SmokeTo=BlowTo+.6f;
        private static readonly float[] StepLands={.45f,.85f,1.2f,1.42f};

        // ---- sound: its own cues (no bed) ----
        private static readonly NativeIntroCue[] cues=
        {
            new NativeIntroCue(.02f,"intro2.sundown.wind","structure.whoosh","dash",.32f,.7f),
            new NativeIntroCue(.45f,"intro2.sundown.step","move.step.gravel",null,.35f),
            new NativeIntroCue(.85f,"intro2.sundown.step","move.step.gravel",null,.32f),
            new NativeIntroCue(Toll1,"intro2.sundown.toll","impact.metal","metal",.9f,.42f),
            new NativeIntroCue(1.2f,"intro2.sundown.step","move.step.gravel",null,.3f),
            new NativeIntroCue(Toll2,"intro2.sundown.toll","impact.metal","metal",.95f,.4f),
            new NativeIntroCue(Toll2+.03f,"intro2.sundown.crack","break.wood","wood",.75f),
            new NativeIntroCue(Break,"intro2.sundown.collapse","structure.debris","stone",.85f),
            new NativeIntroCue(DrawFrom+.05f,"intro2.sundown.draw","gun.ads","reload-start",.6f),
            new NativeIntroCue(ShotAt,"intro2.sundown.shot","gun.pistol.shot","pistol",1.0f),
            new NativeIntroCue(ShotAt+.02f,"intro2.sundown.whiz","gun.flyby","dash",.5f,.7f),
            new NativeIntroCue(HitAt,"intro2.sundown.ring","gun.ricochet","metal",1.0f),
            new NativeIntroCue(HitAt+.01f,"intro2.sundown.bell","impact.metal","metal",1.0f,.5f),
            new NativeIntroCue(Ground,"intro2.sundown.thud","impact.ground","stone",.9f,.8f),
            new NativeIntroCue(Ground+.08f,"intro2.sundown.roll","structure.gravel","wood",.55f),
            new NativeIntroCue(BlowAt,"intro2.sundown.blow","structure.whoosh",null,.35f,1.1f),
            new NativeIntroCue(LastRing,"intro2.sundown.ting","impact.metal","metal",.4f,1.3f),
        };
        public override NativeIntroCue[] Cues=>cues;
        public override void Card(float t,ref NativeIntroCardState card)
        {
            base.Card(t,ref card);card.X=.26f;card.Y=.82f;card.Style=NativeIntroCardStyle.Wipe;card.Ink=NativeEntranceFx.Hex(0xfff2dc);card.Glow=NativeEntranceFx.Hex(0xffa850,1.2f);
        }
    }
}
