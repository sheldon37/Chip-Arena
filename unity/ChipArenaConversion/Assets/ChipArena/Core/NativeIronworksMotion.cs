using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>A prop's place in the rig's source frame (+x the add's left; the root at the body): which prop
    /// (NativeHordeModels.Prop*), where its origin is, its turn (its +z along the prop, +y up) and its scale.</summary>
    public struct IronProp{public int Prop;public Vector3 At;public Quaternion Turn;public float Scale;}
    /// <summary>A chain in the rig's source frame, link by link from one end to the other, sagging `Sag` m at its middle.</summary>
    public struct IronChain{public Vector3 From,To;public float Sag;}

    /// <summary>
    /// The City Ironworks kinds' motion over the zombie gait (docs/EXPEDITION-CITY-IRONWORKS.md 6): presentation only, posed
    /// from what every peer holds (the add's LastAttack and attack, its kind, the view's replicated times: IronworksView) and the
    /// presentation clock; no hit time, hitbox, damage, cooldown or movement is read back or changed. The mocap takes the kinds
    /// walk, run and stand on are their own (NativeIronworksKinds' rows); the rest is procedural over them, because the pack
    /// has no pole, ladle, hook or rope takes (the doc's "new mocap wanted": these are the stand-ins):
    /// <list type="bullet">
    /// <item>Stoker: the rake carried in both hands (the hands locked to the pole), the sweep two-handed across the front (the
    /// strike at LastAttack + .5), the pull: the rake raised (.7 s), thrust, the hook flying the 4.5 m line, the yank (.35 s),
    /// the hold (.4 s).</item>
    /// <item>Ladleman: the ladle carried on its shank in both hands; the pour: raised (1 s), tipped forward and swept across the
    /// 60 degree arc (1.2 s), lowered; the swing on the smash takes with the ladle in the fists; the spill: knocked down to a
    /// crouch for the 2 s stun, the ladle dropped.</item>
    /// <item>Slagback: the shed: reeling back, shaking the plates off (.8 s); the Cinderling: its lunge's crouch held lower.</item>
    /// <item>Rigger: the hook hung from its fist on a short chain, dragged walking; the swing: spun overhead twice in the wind-up
    /// (.6 s), swept 300 degrees on 3.2 m of chain (.5 s); off the ground (static poses: <see cref="RiggerHang"/>): hanging,
    /// sliding, climbing hand over hand, falling.</item>
    /// </list>
    /// Fixed fields, no allocation per frame.
    /// </summary>
    public sealed partial class NativeHordeGait
    {
        /// <summary>The add's Ironworks view (IronworksView.None for every other add): set before <see cref="Pose"/>.</summary>
        public IronworksView Iron=IronworksView.None;
        /// <summary>The pilot the Stoker's hook holds (the gait's frame: world over the add's scale), when known.</summary>
        public Vector3 IronTarget;public bool IronTargetKnown;
        /// <summary>The props and chains the last <see cref="PlaceProps"/> placed (source frame), for the draw.</summary>
        public readonly IronProp[] Props=new IronProp[3];public int PropCount;public readonly IronChain[] Chains=new IronChain[2];public int ChainCount;
        /// <summary>The Stoker's hook end and the Ladleman's lip (source frame) the effects read.</summary>
        public Vector3 HookTip,Lip;
        // the intended pole of the frame (set by the arms stage), the ladle dropped by a spill (where, when)
        private Vector3 poleDir,ladleLeft;private float poleLength;private bool spillSeen;private float spillFrom;private Vector3 spillAt;private Quaternion spillTurn;

        private static Quaternion Grip(Vector3 along)
        {
            var x=CapturedRig.Unit(along,Vector3.UnitZ);var up=Math.Abs(x.Y)>.95f?Vector3.UnitZ:Vector3.UnitY;var y=CapturedRig.Unit(up-x*Vector3.Dot(up,x),Vector3.UnitY);return CapturedRig.Basis(x,y,Vector3.Cross(x,y));
        }
        /// <summary>Reaches a hand to a grip point (source frame), its fist turned to hold a pole along `along`.</summary>
        private static void Hold(CapturedRig.Pose p,bool left,Vector3 grip,Vector3 along,float w,Vector3 pole)
        {
            if(w<=0)return;p.World();int hand=left?CapturedRig.LHand:CapturedRig.RHand;var wrist=Grip(along);var target=grip+Vector3.Transform(new Vector3(0,.075f,0),wrist);
            var chest=p.WorldRotation[CapturedRig.Chest];var inv=Quaternion.Inverse(chest);var cpos=p.WorldPosition[CapturedRig.Chest];
            if(w<1){target=Vector3.Lerp(p.WorldPosition[hand],target,w);wrist=Quaternion.Slerp(p.WorldRotation[hand],wrist,w);}
            CapturedRig.Arm(p,left,Vector3.Transform(target-cpos,inv),inv*wrist,Vector3.Transform(pole,inv));p.World();
        }
        /// <summary>The head turned back toward level by `k` (keeping where it looks): a hood, a mask or a hard hat sits straight,
        /// never at the Siren's crooked tilt, and stays inside the capsule.</summary>
        private static void Upright(CapturedRig.Pose p,float k)
        {
            p.World();var w=p.WorldRotation[CapturedRig.Head];var fwd=Vector3.Transform(Vector3.UnitZ,w);float yaw=(float)Math.Atan2(fwd.X,fwd.Z);
            var level=CapturedRig.Rot(Vector3.UnitY,yaw)*CapturedRig.Rot(Vector3.UnitX,Math.Max(-.25f,Math.Min(.35f,-(float)Math.Asin(Math.Max(-1,Math.Min(1,fwd.Y))))));
            var local=Quaternion.Inverse(p.WorldRotation[CapturedRig.Neck])*level;p.Rotation[CapturedRig.Head]=Quaternion.Slerp(p.Rotation[CapturedRig.Head],local,k);p.World();
        }
        private static Vector3 Yawed(float yaw,float dy){float c=(float)Math.Cos(yaw),s=(float)Math.Sin(yaw);return CapturedRig.Unit(new Vector3(s,dy,c),Vector3.UnitZ);}
        private static readonly Vector3 PoleL=new Vector3(1,-.8f,-.25f),PoleR=new Vector3(-1,-.7f,-.2f);

        // ================================================================================================================
        // The trunk stage (before the legs are solved: a lowered body bends the knees over the planted feet)
        // ================================================================================================================
        private void IronworksTrunk(in HordeMotionInput f,CapturedRig.Pose p)
        {
            int z=NativeIronworksKinds.Local(kind);if(z<0)return;float t=f.Time;var v=Iron;
            switch(z)
            {
                case NativeIronworksKinds.Cinderling:
                {
                    // The lunge's wind-up held as a crouch: lower and pitched in, the shoulders hunched.
                    if(f.Attack!=NativeHordeKinds.Attack.Lunge)break;Channels(f.AttackAge,f.Strike,f.Recover,f.Attack,out float cock,out float swing,out _,out float settle,out _);
                    float hold=(swing>0?1-swing:cock)*(1-settle);if(hold<=0)break;var b=p.Position[CapturedRig.Body];b.Y-=.07f*hold;p.Position[CapturedRig.Body]=b;
                    Bend(p,CapturedRig.Spine,.18f*hold,0,0);Bend(p,CapturedRig.Chest,.12f*hold,0,0);break;
                }
                case NativeIronworksKinds.Stoker:
                {
                    // Upright and tall in the suit, the left shoulder turned forward onto the pole (a carried spear's stance); the
                    // pull's lean: back as it raises the rake, into the throw, back on the yank.
                    Bend(p,CapturedRig.Spine,-.1f,-.14f,0);Bend(p,CapturedRig.Chest,-.04f,-.16f,0);
                    var ph=NativeIronworksKinds.Pull(v.pullAt,t,v.pullTarget>=0,out float into);float lean=0,twist=0;
                    switch(ph){case NativeIronworksKinds.PullPhase.Windup:{float u=Sm(into/.35f);lean=-.16f*u;twist=-.28f*u;break;}case NativeIronworksKinds.PullPhase.Throw:{float u=Sm(into/NativeIronworksKinds.HookFlight);lean=-.16f+.42f*u;twist=-.28f+.36f*u;break;}
                        case NativeIronworksKinds.PullPhase.Drag:{float u=Sm(into/NativeIronworksKinds.PullDragTime);lean=.26f-.5f*u;twist=.08f-.2f*u;break;}case NativeIronworksKinds.PullPhase.Root:lean=-.24f;twist=-.12f;break;
                        case NativeIronworksKinds.PullPhase.Recover:{float u=Sm(into/.45f);lean=(v.pullTarget>=0?-.24f:.26f)*(1-u);twist=(v.pullTarget>=0?-.12f:.08f)*(1-u);break;}}
                    if(lean!=0||twist!=0){Bend(p,CapturedRig.Spine,lean*.5f,twist*.5f,0);Bend(p,CapturedRig.Chest,lean*.5f,twist*.5f,0);}
                    break;
                }
                case NativeIronworksKinds.Slagback:
                {
                    // The shed: reels back, chest out, shaking (the plates fly: the presentation's), then drops back into its stance.
                    float k=NativeIronworksKinds.Shedding(v.shedAt,t);if(k<=0)break;float a=t-v.shedAt,shake=(float)Math.Sin(a*47)*.06f*k;
                    var b=p.Position[CapturedRig.Body];b.Y-=.05f*k;b.Z-=.06f*k;p.Position[CapturedRig.Body]=b;
                    Bend(p,CapturedRig.Spine,-.22f*k,shake,shake*.6f);Bend(p,CapturedRig.Chest,-.3f*k,-shake,shake);Bend(p,CapturedRig.Head,-.35f*k,shake*1.5f,0);break;
                }
                case NativeIronworksKinds.Ladleman:
                {
                    // Braced under the weight; the pour leans back as it lifts, forward as it tips, sweeping across the arc.
                    Bend(p,CapturedRig.Spine,-.05f,0,0);
                    var ph=NativeIronworksKinds.Pour(v.pourAt,t,out float into);float lean=0,twist=0;
                    if(ph==NativeIronworksKinds.PourPhase.Raise)lean=-.18f*Sm(into/NativeIronworksKinds.PourWindup);
                    else if(ph==NativeIronworksKinds.PourPhase.Pour){lean=-.18f+.42f*Sm(into/.3f);float sw=NativeIronworksKinds.Sweep(v.pourAt,t);twist=float.IsNaN(sw)?0:sw*.42f;}
                    else if(ph==NativeIronworksKinds.PourPhase.Lower)lean=.24f*(1-Sm(into/.6f));
                    if(lean!=0||twist!=0){Bend(p,CapturedRig.Spine,lean*.5f,twist*.5f,0);Bend(p,CapturedRig.Chest,lean*.5f,twist*.5f,0);}
                    // The spill: knocked down onto its haunches, head bowed, for the stun.
                    float s=NativeIronworksKinds.Spilled(v.spilledAt,t);
                    if(s>0){var b=p.Position[CapturedRig.Body];b.Y-=.4f*s;b.Z-=.05f*s;p.Position[CapturedRig.Body]=b;Bend(p,CapturedRig.Spine,.35f*s,0,0);Bend(p,CapturedRig.Chest,.3f*s,0,.08f*s);Bend(p,CapturedRig.Head,.4f*s,0,0);}
                    break;
                }
                case NativeIronworksKinds.Rigger:
                {
                    // The swing: the trunk winds with the hook overhead and turns with it through the sweep.
                    if(float.IsNaN(f.AttackAge)||f.AttackAge<0)break;NativeIronworksKinds.Swing(f.AttackAge,out float ang,out _,out bool sweeping);
                    float w=f.AttackAge<f.Recover?1:1-Sm((f.AttackAge-f.Recover)/.25f);if(w<=0)break;
                    float twist=sweeping?Math.Max(-.7f,Math.Min(.7f,ang-(float)Math.PI*.5f))*.6f:-.12f;Bend(p,CapturedRig.Spine,-.05f*w,twist*.5f*w,0);Bend(p,CapturedRig.Chest,-.05f*w,twist*.5f*w,0);
                    break;
                }
            }
        }

        // ================================================================================================================
        // The arms stage (after the take's and the reactions' arms): the hands onto the props
        // ================================================================================================================
        private void IronworksArms(in HordeMotionInput f,CapturedRig.Pose p)
        {
            int z=NativeIronworksKinds.Local(kind);poleLength=0;if(z<0)return;float t=f.Time;var v=Iron;p.World();
            float bob=(float)Math.Sin(Phase*Math.PI*4)*.02f*Clamp01(Speed/1.2f);
            switch(z)
            {
                case NativeIronworksKinds.Stoker:
                {
                    // Where the rake is: carried (the tine scraping the floor ahead), swept (a two-handed sweep across the front on the
                    // swipe's channels), or in a pull (raised overhead, thrust, yanked).
                    Vector3 grip=new Vector3(-.2f,-.05f+bob,.14f);var dir=CapturedRig.Unit(new Vector3(.32f,-.17f,1),Vector3.UnitZ);
                    if(f.Attack==NativeHordeKinds.Attack.Swipe&&!float.IsNaN(f.AttackAge)&&f.AttackAge>=0)
                    {
                        Channels(f.AttackAge,f.Strike,f.Recover,f.Attack,out float cock,out float swing,out float follow,out float settle,out float w);
                        float hold=swing>0?1-swing:cock,hit=Math.Max(swing,follow*(1-settle));float yaw=-1.9f*hold+1.15f*hit,dy=.12f*hold-.18f*hit;
                        var g=new Vector3(-.26f+.24f*hit,.1f*hold-.02f*hit,-.04f*hold+.32f*hit);float k=w*(1-settle);
                        grip=Vector3.Lerp(grip,g,k);dir=CapturedRig.Unit(Vector3.Lerp(dir,Yawed(yaw,dy),k),dir);
                    }
                    var ph=NativeIronworksKinds.Pull(v.pullAt,t,v.pullTarget>=0,out float into);
                    if(ph!=NativeIronworksKinds.PullPhase.None)
                    {
                        Vector3 g;Vector3 d;float k=1;
                        var raised=new Vector3(-.22f,.4f,-.08f);var up=CapturedRig.Unit(new Vector3(.05f,-.22f,1),Vector3.UnitZ);var thrust=new Vector3(-.1f,.12f,.46f);var level=CapturedRig.Unit(new Vector3(.02f,-.06f,1),Vector3.UnitZ);var yank=new Vector3(-.24f,.0f,.02f);
                        switch(ph)
                        {
                            case NativeIronworksKinds.PullPhase.Windup:{float u=Sm(into/.4f);g=raised;d=up;k=u;break;}
                            case NativeIronworksKinds.PullPhase.Throw:{float u=Sm(into/NativeIronworksKinds.HookFlight);g=Vector3.Lerp(raised,thrust,u);d=CapturedRig.Unit(Vector3.Lerp(up,level,u),level);break;}
                            case NativeIronworksKinds.PullPhase.Drag:{float u=Sm(into/NativeIronworksKinds.PullDragTime);g=Vector3.Lerp(thrust,yank,u);d=level;break;}
                            case NativeIronworksKinds.PullPhase.Root:g=yank;d=level;break;
                            default:{float u=Sm(into/.45f);g=v.pullTarget>=0?yank:thrust;d=level;k=1-u;break;}
                        }
                        grip=Vector3.Lerp(grip,g,k);dir=CapturedRig.Unit(Vector3.Lerp(dir,d,k),dir);
                    }
                    poleDir=dir;poleLength=NativeHordeModels.RakeTip;
                    // The left fist takes the pole where it passes nearest its shoulder (within its reach).
                    p.World();var shoulder=p.WorldPosition[CapturedRig.LArm];float along=Math.Max(.22f,Math.Min(.7f,Vector3.Dot(shoulder-grip,dir)));
                    Hold(p,false,grip,dir,1,PoleR);Hold(p,true,grip+dir*along,dir,1,PoleL);Upright(p,.6f);
                    break;
                }
                case NativeIronworksKinds.Ladleman:
                {
                    // The ladle while it is full: carried low in front, lifted and tipped in the pour; in the swing it rides the fists
                    // (the smash takes' arms stand); spilled, the hands go to the floor.
                    if(!NativeIronworksKinds.Carrying(v.spilledAt,t))
                    {
                        float s=NativeIronworksKinds.Spilled(v.spilledAt,t);
                        if(s>0){Hold(p,true,new Vector3(.24f,-.58f,.28f),new Vector3(0,0,1),s,PoleL);Hold(p,false,new Vector3(-.26f,-.58f,.22f),new Vector3(0,0,1),s,PoleR);}
                        break;
                    }
                    bool swing=f.Attack==NativeHordeKinds.Attack.Smash&&!float.IsNaN(f.AttackAge)&&f.AttackAge>=0&&f.AttackAge<f.Recover+.25f;
                    var front=new Vector3(.04f,-.1f+bob,.4f);var dir=CapturedRig.Unit(new Vector3(0,-.06f,1),Vector3.UnitZ);
                    var ph=NativeIronworksKinds.Pour(v.pourAt,t,out float into);float tip=NativeIronworksKinds.Tip(v.pourAt,t);
                    if(ph!=NativeIronworksKinds.PourPhase.None)
                    {
                        float lift=ph==NativeIronworksKinds.PourPhase.Raise?Sm(into/NativeIronworksKinds.PourWindup):ph==NativeIronworksKinds.PourPhase.Pour?1:1-Sm(into/.6f);
                        front=Vector3.Lerp(front,new Vector3(.04f,.26f,.36f),lift);
                        // tipped: the bowl end dips, the shank pitched down over the lip
                        float pitch=Math.Max(0,tip)*.7f-Math.Min(0,tip)*.8f;dir=CapturedRig.Unit(new Vector3(0,(float)-Math.Sin(pitch)-.06f,(float)Math.Cos(pitch)),dir);
                    }
                    if(swing)
                    {
                        // the ladle in the fists: along from the back hand through the front one
                        var r=p.WorldPosition[CapturedRig.RHand];var l=p.WorldPosition[CapturedRig.LHand];var d=l-r;if(d.LengthSquared()<1e-4f)d=new Vector3(0,0,1);
                        Channels(f.AttackAge,f.Strike,f.Recover,f.Attack,out _,out _,out _,out _,out float w);dir=CapturedRig.Unit(Vector3.Lerp(dir,CapturedRig.Unit(d+Vector3.Transform(new Vector3(0,0,.35f),p.WorldRotation[CapturedRig.Chest]),dir),w),dir);
                        front=Vector3.Lerp(front,l,w);ladleLeft=front;poleDir=dir;poleLength=1;break;
                    }
                    ladleLeft=front;poleDir=dir;poleLength=1;
                    Hold(p,true,front,dir,1,PoleL);Hold(p,false,front-dir*.3f,dir,1,PoleR);Upright(p,.5f);
                    break;
                }
                case NativeIronworksKinds.Rigger:
                {
                    // The swing: the fist spun over the head in the wind-up, then out along the hook's line through the sweep.
                    Upright(p,.45f);
                    if(float.IsNaN(f.AttackAge)||f.AttackAge<0||f.AttackAge>f.Recover+.25f)break;NativeIronworksKinds.Swing(f.AttackAge,out float ang,out _,out bool sweeping);
                    float w=f.AttackAge<f.Recover?Sm(f.AttackAge/.08f):1-Sm((f.AttackAge-f.Recover)/.25f);
                    Vector3 hand;if(!sweeping&&f.AttackAge<NativeIronworksKinds.SwingWindup)hand=new Vector3(-.05f+(float)Math.Sin(ang)*.24f,.72f,.06f+(float)Math.Cos(ang)*.24f);
                    else{hand=new Vector3((float)Math.Sin(ang)*.55f,.24f,(float)Math.Cos(ang)*.55f);if(hand.X>0)hand.X*=.6f;}
                    Hold(p,false,hand,new Vector3((float)Math.Sin(ang),0,(float)Math.Cos(ang)),w,PoleR);
                    break;
                }
            }
        }

        // ================================================================================================================
        // Props: where the rake, ladle, hook and chains are this frame (after Pose), in the source frame
        // ================================================================================================================
        /// <summary>
        /// Places the kind's props from the final pose (call after <see cref="Pose"/>; `f` the same input). The Stoker's rake on its
        /// grip (and in a pull its hook flying the line on a chain), the Ladleman's ladle (full, or dropped by a spill), the
        /// Rigger's hook on its chain (hanging, dragged, or swung on 3.2 m). `floorY` the floor in the source frame.
        /// </summary>
        public void PlaceProps(in HordeMotionInput f,CapturedRig.Pose p)
        {
            PropCount=0;ChainCount=0;int z=NativeIronworksKinds.Local(kind);if(z<0)return;p.World();float t=f.Time,floorY=f.Ground-f.Root.Y;var v=Iron;
            Vector3 GripOf(bool left){int h=left?CapturedRig.LHand:CapturedRig.RHand;return p.WorldPosition[h]+Vector3.Transform(new Vector3(0,-.075f,0),p.WorldRotation[h]);}
            switch(z)
            {
                case NativeIronworksKinds.Stoker:
                {
                    if(poleLength<=0)break;var grip=GripOf(false);var turn=CapturedRig.Look(poleDir,Vector3.UnitY);
                    Props[PropCount++]=new IronProp{Prop=NativeHordeModels.PropRake,At=grip,Turn=turn,Scale=1};HookTip=grip+poleDir*NativeHordeModels.RakeTip;
                    // In the pull the hook leaves the rake's end on its chain and flies the line (the rake's own tine stays): HookOut
                    // is measured from the Stoker, so the hook leaves the tip only once the line is longer than the rake's reach.
                    var flat=CapturedRig.Unit(new Vector3(poleDir.X,0,poleDir.Z),Vector3.UnitZ);
                    float out_=NativeIronworksKinds.HookOut(v.pullAt,t,v.pullTarget>=0)/NativeHordeKinds.Scale[kind]-Math.Max(0,HookTip.X*flat.X+HookTip.Z*flat.Z);
                    if(out_>.05f)
                    {
                        var end=HookTip+flat*out_;end.Y=Math.Max(floorY+.5f,HookTip.Y-.15f*out_);
                        var ph=NativeIronworksKinds.Pull(v.pullAt,t,v.pullTarget>=0,out _);
                        if(IronTargetKnown&&(ph==NativeIronworksKinds.PullPhase.Drag||ph==NativeIronworksKinds.PullPhase.Root)){var tg=ToLocal(IronTarget,f.Root);end=new Vector3(tg.X,Math.Max(floorY+.55f,tg.Y+.25f),tg.Z);}
                        Chains[ChainCount++]=new IronChain{From=HookTip,To=end,Sag=.02f};Props[PropCount++]=new IronProp{Prop=NativeHordeModels.PropHook,At=end+new Vector3(0,.3f,0),Turn=CapturedRig.Look(flat,Vector3.UnitY),Scale=.7f};HookTip=end;
                    }
                    break;
                }
                case NativeIronworksKinds.Ladleman:
                {
                    if(!NativeIronworksKinds.Carrying(v.spilledAt,t))
                    {
                        // Dropped where it was when it spilled: it falls and tips onto its side in .4 s, then lies there, empty.
                        if(!spillSeen){spillSeen=true;spillFrom=v.spilledAt;spillAt=ladleLeft;spillTurn=CapturedRig.Look(poleDir,Vector3.UnitY);}
                        float a=Clamp01((t-spillFrom)/.4f);var rest=new Vector3(spillAt.X,floorY+.2f,spillAt.Z+.15f);
                        var at=Vector3.Lerp(spillAt,rest,a*a);var turn=Quaternion.Slerp(spillTurn,CapturedRig.Look(new Vector3(.4f,-.15f,1),new Vector3(.9f,.4f,0)),Sm(a));
                        Props[PropCount++]=new IronProp{Prop=NativeHordeModels.PropLadleEmpty,At=at,Turn=turn,Scale=1};Lip=at;break;
                    }
                    spillSeen=false;if(poleLength<=0)break;var front=GripOf(true);var turn2=CapturedRig.Look(poleDir,Vector3.UnitY);
                    Props[PropCount++]=new IronProp{Prop=NativeHordeModels.PropLadle,At=front,Turn=turn2,Scale=1};
                    Lip=front+Vector3.Transform(new Vector3(0,.125f,NativeHordeModels.LadleBowl+NativeHordeModels.LadleBowlRadius+.05f),turn2);
                    break;
                }
                case NativeIronworksKinds.Rigger:
                {
                    var fist=GripOf(false);float sc=NativeHordeKinds.Scale[kind];
                    if(!float.IsNaN(f.AttackAge)&&f.AttackAge>=0&&f.AttackAge<f.Recover+.25f)
                    {
                        // Swung: the hook circling the fist in the wind-up, then at the end of 3.2 m of chain sweeping its arc, reeled in after.
                        NativeIronworksKinds.Swing(f.AttackAge,out float ang,out float rad,out _);rad/=sc;
                        var hook=new Vector3((float)Math.Sin(ang)*rad,f.AttackAge<NativeIronworksKinds.SwingWindup?fist.Y+.12f:Math.Max(floorY+.6f,.15f),(float)Math.Cos(ang)*rad);
                        if(f.AttackAge<NativeIronworksKinds.SwingWindup)hook=new Vector3(fist.X+(float)Math.Sin(ang)*rad,fist.Y+.1f,fist.Z+(float)Math.Cos(ang)*rad);
                        Chains[ChainCount++]=new IronChain{From=fist,To=hook,Sag=f.AttackAge<NativeIronworksKinds.SwingWindup?.02f:.08f};
                        var outward=CapturedRig.Unit(new Vector3(hook.X-fist.X,0,hook.Z-fist.Z),Vector3.UnitZ);
                        Props[PropCount++]=new IronProp{Prop=NativeHordeModels.PropHook,At=hook,Turn=CapturedRig.Look(new Vector3(-outward.Z,0,outward.X),-outward),Scale=1};HookTip=hook;break;
                    }
                    // Carried: hanging from the fist on a short chain standing; dragged behind on the floor walking (sparks off its point).
                    float moving=Clamp01((Speed-.3f)/.8f);var hang=fist+new Vector3(0,-.28f,0);
                    var drag=new Vector3(fist.X-.12f,floorY+.06f,fist.Z-.7f);var eye=Vector3.Lerp(hang,drag+new Vector3(0,.12f,.42f),moving);
                    Chains[ChainCount++]=new IronChain{From=fist,To=eye,Sag=.04f+.08f*moving};
                    var lay=Quaternion.Slerp(CapturedRig.Look(Vector3.UnitZ,Vector3.UnitY),CapturedRig.Look(new Vector3(0,-1,0),new Vector3(0,0,1)),moving*.85f);
                    Props[PropCount++]=new IronProp{Prop=NativeHordeModels.PropHook,At=eye,Turn=lay,Scale=1};HookTip=eye+Vector3.Transform(new Vector3(0,-.5f,.1f),lay);break;
                }
            }
        }

        // ================================================================================================================
        // The Rigger off the ground (static poses: the presentation places the root on the chain)
        // ================================================================================================================
        public enum RiggerState{Hang,Slide,Climb,Throw,Fall,Stun}
        /// <summary>
        /// The Rigger on its chain: `state` and `u` its progress (the slide's 0..1, the climb's seconds, the fall's seconds, the
        /// stun's seconds), `sway` the chain's swing (rad). Hanging: both fists over its head on the chain, the hips under them, the
        /// legs loose and swinging with the chain; sliding: the legs tucked, one fist above the other; climbing: hand over hand
        /// (the hands alternate every .5 s), the knees pulling up; throwing: the right arm flung up; falling: limbs thrown out;
        /// stunned: down on its haunches, the hands on the floor. The hands' grip on the chain is <see cref="RiggerHands"/> above
        /// the root (rig metres).
        /// </summary>
        public static void RiggerHang(CapturedRig.Pose p,uint seed,RiggerState state,float u,float time,float sway)
        {
            p.Reset();float H(int k)=>NativeHordeKinds.Hash(seed,320+k);
            p.Rotation[CapturedRig.Body]=CapturedRig.Euler(sway*.6f,0,sway*.35f);
            void Legs(float hip,float knee,float swing)
            {
                for(int i=0;i<2;i++){int h=i==0?CapturedRig.LHip:CapturedRig.RHip;float s=(float)Math.Sin(time*2.3f+i*1.9f+H(i)*3)*swing;p.Rotation[h]=CapturedRig.Euler(-hip+s,0,(i==0?1:-1)*.06f);p.Rotation[h+1]=CapturedRig.Euler(knee-s*.5f);p.Rotation[h+2]=CapturedRig.Euler(.45f);}
            }
            switch(state)
            {
                case RiggerState.Hang:Legs(.12f,.35f,.12f);Bend(p,CapturedRig.Chest,-.08f,0,0);Bend(p,CapturedRig.Head,.25f+.1f*(float)Math.Sin(time*.7f),.3f*(float)Math.Sin(time*.4f+H(3)*4),0);break;
                case RiggerState.Slide:Legs(.9f*Sm(u/.4f),1.6f*Sm(u/.4f),.05f);Bend(p,CapturedRig.Head,.45f,0,0);break;
                case RiggerState.Climb:{float c=(float)Math.Sin(u*Math.PI*2);Legs(.55f+.35f*c,1.1f+.4f*c,.0f);Bend(p,CapturedRig.Head,-.2f,0,0);break;}
                case RiggerState.Throw:Legs(.1f,.2f,0);break;
                case RiggerState.Fall:{float flail=(float)Math.Sin(u*17)*.4f;Legs(.4f+flail*.5f,.7f-flail*.4f,0);p.Rotation[CapturedRig.Body]=CapturedRig.Euler(-.5f*Sm(u/.4f),0,flail*.3f);break;}
                case RiggerState.Stun:
                {
                    float rise=Sm((u-NativeIronworksKinds.FallStun+.35f)/.35f);p.Position[CapturedRig.Body]=new Vector3(0,-.36f*(1-rise),0);
                    Legs(1.45f*(1-rise),2.1f*(1-rise),0);Bend(p,CapturedRig.Spine,.45f*(1-rise),0,0);Bend(p,CapturedRig.Head,.5f*(1-rise)+.1f*(float)Math.Sin(time*3),0,.2f*(1-rise));
                    p.World();var chest=p.WorldRotation[CapturedRig.Chest];var inv=Quaternion.Inverse(chest);var cpos=p.WorldPosition[CapturedRig.Chest];
                    for(int i=0;i<2;i++){bool left=i==0;float sx=left?1:-1;var target=Vector3.Lerp(new Vector3(sx*.28f,-.72f,.32f),new Vector3(sx*.3f,-.4f,.05f),rise);CapturedRig.Arm(p,left,Vector3.Transform(target-cpos,inv),inv*CapturedRig.Euler(-.3f),Vector3.Transform(new Vector3(sx,-.5f,-.3f),inv));}
                    p.World();return;
                }
            }
            // The fists on the chain over the head (the climb alternating them, the throw flinging the right arm up).
            p.World();var ch=p.WorldRotation[CapturedRig.Chest];var iv=Quaternion.Inverse(ch);var cp=p.WorldPosition[CapturedRig.Chest];
            for(int i=0;i<2;i++)
            {
                bool left=i==0;float sx=left?.05f:-.05f;float y=RiggerHands;
                if(state==RiggerState.Climb){float c=(float)Math.Sin(u*Math.PI*2+(left?0:Math.PI));y+=.16f*c;}
                else if(state==RiggerState.Slide)y+=left?.08f:-.08f;
                var target=new Vector3(sx,y,.06f);
                if(state==RiggerState.Throw)target=left?new Vector3(.32f,.15f,.12f):new Vector3(-.12f,.62f+.15f*Sm(u/.25f),.2f);
                if(state==RiggerState.Fall)target=new Vector3((left?1:-1)*(.45f+.1f*(float)Math.Sin(u*13+i)),.35f+.15f*(float)Math.Sin(u*11+i*2),.1f);
                CapturedRig.Arm(p,left,Vector3.Transform(target-cp,iv),iv*CapturedRig.Euler(-3f,0,0),Vector3.Transform(new Vector3(left?1:-1,.2f,-.4f),iv));
            }
            p.World();
        }
        /// <summary>The fists' grip on the chain above the root while hanging (rig metres).</summary>
        public const float RiggerHands=.64f;
    }
}
