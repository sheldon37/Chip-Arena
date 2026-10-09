using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // One box of the table stage, in Unity world metres (left-handed, +y up, the camera looks toward -z; screen right is -x).
    public struct TablePiece {public Vector3 Position,Size;public int Color;}
    // The table camera: position, forward (unit) and vertical field of view in degrees. Unity: LookRotation(Forward, up).
    public struct TableView {public Vector3 Position,Forward;public float Fov,Aspect;}
    // Where the pilot's arms go this frame (source rig frame, relative to the group root): the wrist goal, the IK weight
    // against the resting hand on the table (0 rest, 1 goal) and the wrist's orientation at the goal (root frame).
    public struct TableArm {public Vector3 Goal;public float Weight;public Quaternion Wrist;}

    // The chip draft's table stage (owner request 2026-10-09): the drafting pilot seated at a table (the C14 lobby chair pose
    // and chair), the offered chips laid out on it at real scale in rows by tier, seen from a high tilted camera (65 degrees),
    // the hovered chip lifted, and the pick carried to the other wrist and planted. Presentation only: nothing here reads or
    // changes offers, picks, timers or the network; callers pass the authoritative draft state in.
    public static class NativeDraftTable
    {
        // Stage metrics (Unity world, metres). The pilot sits at the origin facing +z, toward the camera; floor top y = 0.
        public const float RootY=.88f,TableTop=.70f,TableNear=.16f,TableFar=1.02f,TableHalf=.80f,TableThick=.06f;
        // A chip: a .08 m package (die, heat spreader, grade pips) with .01 m legs on two sides: .1 m overall, about half the
        // pilot's hand (palm .145 m wide, .2 m with fingers and thumb) and well under the forearm.
        public const float Chip=.08f,ChipThick=.011f,Pin=.01f,PitchX=.114f,PitchZ=.13f,RowsMid=.47f,Lift=.125f,LiftTilt=20;
        public const int MaxChips=12,MaxPerRow=6,MaxRows=3;
        public const float Pitch=65,Fov=30;
        // The pick, seconds after the authoritative pick time: reach, grab, carry to the other wrist, plant, pulse, return.
        public const float ReachEnd=.38f,GrabEnd=.48f,CarryStart=.45f,CarryEnd=.87f,PlantStart=.87f,PlantEnd=1.07f,PulseStart=.95f,PulseEnd=1.55f,ReturnStart=1.15f,ReturnEnd=1.7f,SoundAt=.9f,InstalledAt=.95f;
        public static float Reach(float age)=>Ease(age/ReachEnd);
        public static float Grab(float age)=>Ease((age-ReachEnd)/(GrabEnd-ReachEnd));
        public static float Carry(float age)=>Ease((age-CarryStart)/(CarryEnd-CarryStart));
        public static float Raise(float age)=>Ease((age-.12f)/.5f)*(1-Ease((age-ReturnStart)/(ReturnEnd-ReturnStart)));
        public static float Plant(float age)=>Ease((age-PlantStart)/(PlantEnd-PlantStart));
        public static float Pulse(float age)=>age<PulseStart||age>PulseEnd?-1:(age-PulseStart)/(PulseEnd-PulseStart);
        public static float Return(float age)=>Ease((age-ReturnStart)/(ReturnEnd-ReturnStart));
        public static float Settle(float age)=>Ease((age-.15f)/.6f);
        // Smootherstep (zero velocity and acceleration at both ends), so no motion starts or stops with a jolt.
        public static float Ease(float t){t=Math.Max(0,Math.Min(1,t));return t*t*t*(t*(t*6-15)+10);}
        // Exponential approach: the same curve at 30 and 240 Hz (`dt` clamped so a hitch cannot overshoot).
        public static float Damp(float current,float target,float rate,float dt)=>current+(target-current)*(1-(float)Math.Exp(-rate*Math.Max(0,Math.Min(.1f,dt))));
        public static Vector3 Damp(Vector3 current,Vector3 target,float rate,float dt)=>current+(target-current)*(1-(float)Math.Exp(-rate*Math.Max(0,Math.Min(.1f,dt))));
        // A chip's entrance, `t` seconds after its turn in reading order (.22 s apart, as the tray booted): it drops onto the
        // table with a small settle, and the die flickers on like the card's power-on (chip-cards.css draft-reveal).
        public static float Drop(float t){if(t<=0)return 1;if(t>=.6f)return 0;float k=t/.6f;return k<.62f?1-Ease(k/.62f):-.06f*(float)Math.Sin((k-.62f)/.38f*Math.PI);}
        private static readonly float[] FlickerAt={0,.22f,.28f,.33f,.41f,1},FlickerValue={.12f,.12f,1.7f,.35f,1.35f,1};
        public static float Bright(float t)=>t<0?0:NativeUiMotion.Keys(Math.Min(1,t),FlickerAt,FlickerValue);

        // Grade rank for the rows: the six catalogue grades, then the pilot exclusives above them.
        public static int Rank(string tier)=>tier=="common"?1:tier=="uncommon"?2:tier=="rare"?3:tier=="epic"?4:tier=="legendary"?5:tier=="mythic"?6:tier=="mishima"||tier=="ansatsuken"||tier=="l44t"||tier=="siren"?7:0;

        // Lays `count` offers out on the table: `order` gets the reading order (back row first, left to right on screen) as
        // offer indices, `slots[offer]` the chip's resting centre on the table top. Rows go by grade, rarest at the back
        // (nearest the pilot): one row per grade when that fits in three rows of six, else rows of six in grade order.
        private static readonly int[] Starts=new int[MaxChips+1];
        public static int Arrange(IReadOnlyList<string> tiers,int count,int[] order,Vector3[] slots,int[] rowOf=null)
        {
            count=Math.Min(Math.Min(count,MaxChips),Math.Min(order.Length,slots.Length));if(count<=0)return 0;
            for(int i=0;i<count;i++)order[i]=i;
            // Stable insertion sort by rank, highest first (no allocation; count <= 12).
            for(int i=1;i<count;i++){int v=order[i],r=Rank(tiers[v]),j=i-1;while(j>=0&&Rank(tiers[order[j]])<r){order[j+1]=order[j];j--;}order[j+1]=v;}
            var starts=Starts;int rows=0;
            // One row per grade (split at six); fall back to plain rows of six when that needs more than three rows.
            for(int i=0;i<count;i++){bool newGrade=i==0||Rank(tiers[order[i]])!=Rank(tiers[order[i-1]]);bool full=rows>0&&i-starts[rows-1]>=MaxPerRow;if(newGrade||full)starts[rows++]=i;}
            if(rows>MaxRows){rows=(count+MaxPerRow-1)/MaxPerRow;int per=(count+rows-1)/rows;for(int r=0;r<rows;r++)starts[r]=r*per;}
            starts[rows]=count;
            for(int r=0;r<rows;r++)
            {
                int cols=starts[r+1]-starts[r];float z=RowsMid+(r-(rows-1)*.5f)*PitchZ;
                for(int c=0;c<cols;c++){int offer=order[starts[r]+c];slots[offer]=new Vector3(((cols-1)*.5f-c)*PitchX,TableTop+ChipThick*.5f,z);if(rowOf!=null)rowOf[offer]=r;}
            }
            return rows;
        }

        // The table, its mat and legs and the floor tiles, then the C14 lobby chair (NativePortraitRules.Chair, at the stage
        // origin; the seated pilot uses it), as static boxes. Colours are sRGB hex in the game's dark voxel palette.
        public static readonly TablePiece[] Stage=MakeStage(false),ChairPieces=MakeStage(true);
        public const int TableColor=0x2b2e3a,TableEdge=0x3d4152,MatColor=0x161a24,SocketColor=0x10131b,LegColor=0x1f2129,FloorA=0x181a22,FloorB=0x1c1e27;
        private static TablePiece[] MakeStage(bool chair)
        {
            var list=new List<TablePiece>();if(chair){foreach(var p in NativePortraitRules.Chair)list.Add(new TablePiece{Position=new Vector3(-p.Position.X,p.Position.Y,p.Position.Z),Size=p.Size,Color=p.Color});return list.ToArray();}void Add(float x,float y,float z,float sx,float sy,float sz,int c)=>list.Add(new TablePiece{Position=new Vector3(x,y,z),Size=new Vector3(sx,sy,sz),Color=c});
            float zc=(TableNear+TableFar)/2,depth=TableFar-TableNear;
            Add(0,TableTop-TableThick/2,zc,TableHalf*2,TableThick,depth,TableColor);
            Add(0,TableTop-TableThick-.025f,TableFar-.03f,TableHalf*2-.1f,.05f,.04f,TableEdge);Add(0,TableTop-TableThick-.025f,TableNear+.03f,TableHalf*2-.1f,.05f,.04f,TableEdge);
            Add(0,TableTop+.002f,zc,TableHalf*2-.12f,.004f,depth-.12f,MatColor);
            foreach(float x in new[]{-TableHalf+.07f,TableHalf-.07f})foreach(float z in new[]{TableNear+.07f,TableFar-.07f})Add(x,(TableTop-TableThick)/2,z,.07f,TableTop-TableThick,.07f,LegColor);
            // Floor: .5 m voxel tiles in two shades around the table and chair.
            for(int ix=-4;ix<4;ix++)for(int iz=-3;iz<4;iz++)Add(ix*.5f+.25f,-.03f,iz*.5f+.25f,.5f,.06f,.5f,((ix+iz)&1)==0?FloorA:FloorB);
            return list.ToArray();
        }

        // ---- camera ----
        public static Vector3 Forward=>new Vector3(0,-(float)Math.Sin(Pitch*Math.PI/180),-(float)Math.Cos(Pitch*Math.PI/180));
        private static void Basis(Vector3 f,out Vector3 right,out Vector3 up){right=Vector3.Normalize(Vector3.Cross(Vector3.UnitY,f));up=Vector3.Cross(f,right);}
        // Viewport position (0..1, y up, as Unity's WorldToViewportPoint) and depth along the view.
        public static Vector2 Project(in TableView v,Vector3 p,out float depth)
        {
            Basis(v.Forward,out var r,out var u);var d=p-v.Position;depth=Vector3.Dot(d,v.Forward);float t=(float)Math.Tan(v.Fov*Math.PI/360),z=Math.Max(1e-4f,depth);
            return new Vector2(.5f+.5f*Vector3.Dot(d,r)/(z*t*v.Aspect),.5f+.5f*Vector3.Dot(d,u)/(z*t));
        }
        // Frames `count` points inside the viewport window [x0,x1] x [y0,y1] (y up) at the fixed 65 degree pitch: the nearest
        // distance that fits both axes, then the sideways and vertical offsets that centre the points in the window (moving
        // the camera across the view keeps every depth, so this is a pure image shift with no keystone).
        public static TableView Frame(Vector3[] points,int count,float aspect,float x0,float x1,float y0,float y1)
        {
            var f=Forward;Basis(f,out var r,out var u);var centre=Vector3.Zero;for(int i=0;i<count;i++)centre+=points[i];centre/=Math.Max(1,count);
            float t=(float)Math.Tan(Fov*Math.PI/360);
            // Extents for a camera at distance d: x/y image coordinates are (a - s)/(z t aspect) and (b - s)/(z t).
            void Bounds(float d,float sx,float sy,out float minX,out float maxX,out float minY,out float maxY)
            {
                minX=minY=float.MaxValue;maxX=maxY=float.MinValue;
                for(int i=0;i<count;i++){var q=points[i]-centre;float z=Math.Max(1e-3f,Vector3.Dot(q,f)+d),x=.5f+.5f*(Vector3.Dot(q,r)-sx)/(z*t*aspect),y=.5f+.5f*(Vector3.Dot(q,u)-sy)/(z*t);minX=Math.Min(minX,x);maxX=Math.Max(maxX,x);minY=Math.Min(minY,y);maxY=Math.Max(maxY,y);}
            }
            float Centre(float d,bool horizontal,float other)
            {
                // The window centre is reached by a monotonic shift: bisect it.
                float lo=-20,hi=20,want=horizontal?(x0+x1)/2:(y0+y1)/2;
                for(int k=0;k<40;k++){float m=(lo+hi)/2;Bounds(d,horizontal?m:other,horizontal?other:m,out float a,out float b,out float c,out float e);float mid=horizontal?(a+b)/2:(c+e)/2;if(mid>want)lo=m;else hi=m;}
                return (lo+hi)/2;
            }
            float dLo=.3f,dHi=40,sxBest=0,syBest=0;
            for(int k=0;k<36;k++)
            {
                float d=(dLo+dHi)/2,sx=0,sy=0;for(int pass=0;pass<2;pass++){sx=Centre(d,true,sy);sy=Centre(d,false,sx);}
                Bounds(d,sx,sy,out float a,out float b,out float c,out float e);bool fits=b-a<=x1-x0&&e-c<=y1-y0&&Vector3.Dot(points[0]-centre,f)+d>.2f;
                if(fits){dHi=d;sxBest=sx;syBest=sy;}else dLo=d;
            }
            return new TableView{Position=centre-f*dHi+r*sxBest+u*syBest,Forward=f,Fov=Fov,Aspect=aspect};
        }
        // The points the frame must hold: every chip slot at rest and lifted (with its legs), the table's front edge under the
        // chips, the pilot's head (plus a hat) and shoulders, and the raised wrist where the chip is planted.
        public static int FramePoints(Vector3[] slots,int count,Vector3 headTop,float shoulder,Vector3[] points)
        {
            int n=0;float h=Chip*.5f+Pin;
            for(int i=0;i<count&&n+4<=points.Length;i++){var s=slots[i];points[n++]=s+new Vector3(-h,0,-h);points[n++]=s+new Vector3(h,0,h);points[n++]=s+new Vector3(h,Lift,-h);points[n++]=s+new Vector3(-h,Lift,h);}
            if(n+7>points.Length)return n;
            float chipFront=RowsMid,chipX=0;for(int i=0;i<count;i++){chipFront=Math.Max(chipFront,slots[i].Z+h);chipX=Math.Max(chipX,Math.Abs(slots[i].X)+h);}
            points[n++]=new Vector3(0,TableTop,Math.Min(TableFar,chipFront+.05f));
            points[n++]=headTop;points[n++]=headTop+new Vector3(.2f,-.1f,0);points[n++]=headTop+new Vector3(-.2f,-.1f,0);
            points[n++]=new Vector3(shoulder,headTop.Y-.62f,0);points[n++]=new Vector3(-shoulder,headTop.Y-.62f,0);
            points[n++]=new Vector3(0,TableTop+.32f,.32f);
            return n;
        }

        // ---- pose (source rig frame: +x the rig's left, Unity x = -x; positions relative to the group root at RootY) ----
        public static Vector3 ToSource(Vector3 world)=>new Vector3(-world.X,world.Y-RootY,world.Z);
        public static Vector3 ToWorld(Vector3 source)=>new Vector3(-source.X,source.Y+RootY,source.Z);
        // Hands resting on the table's near edge (left/right of the chips), palms down, fingers toward the chips.
        public static Vector3 Rest(bool left)=>new Vector3(left?.43f:-.43f,TableTop+.055f-RootY,.25f);
        public static readonly Quaternion RestWrist=CapturedRig.Euler(-1.25f,0,0),ReachWrist=CapturedRig.Euler(-.95f,0,0);
        public static Quaternion PlantWrist(bool left)=>CapturedRig.Euler(-.25f,0,left?-1.45f:1.45f);
        // The raised wrist of the arm that receives the chip: forearm across the body in front of the chest.
        public static Vector3 Raised(bool left)=>new Vector3(left?.08f:-.08f,.11f,.36f);
        // The grip between palm and fingers (hand frame), where a carried chip sits.
        public static readonly Vector3 GripLocal=new Vector3(0,-.085f,.06f);
        public static Vector3 Grip(CapturedRig.Pose p,bool left){int h=left?CapturedRig.LHand:CapturedRig.RHand;return p.WorldPosition[h]+Vector3.Transform(GripLocal,p.WorldRotation[h]);}
        // The top of the receiving forearm just behind the wrist, where the chip is planted.
        public static Vector3 PlantPoint(CapturedRig.Pose p,bool left){int h=left?CapturedRig.LHand:CapturedRig.RHand,e=h-1;var wrist=p.WorldPosition[h];var dir=wrist-p.WorldPosition[e];float len=dir.Length();if(len>1e-4f)dir/=len;return wrist-dir*.06f+new Vector3(0,.095f,0);}
        // Seated idle (NativeLobbyPoses chair pose, breathing over `time`); the arms are re-solved by Arms.
        public static void Seat(MeleeRig rig,CapturedRig.Pose pose,float time,out LobbyHeld weapon,out LobbyHeld offhand)
        {NativeLobbyPoses.Sample(rig,"rifle",NativeLobbyPoses.Chair,time,out weapon,out offhand);rig.WritePose(pose);pose.World();}
        // Leans the chest toward the table (`lean` rad, eased by the caller), turns the head toward `look` (root frame, weight
        // 0..1) and solves both arms by IK against their resting hands, so every blend is continuous (no FK/IK switch).
        public static void Arms(CapturedRig.Pose p,float lean,Vector3 look,float lookWeight,in TableArm left,in TableArm right)
        {
            p.Rotation[CapturedRig.Spine]=p.Rotation[CapturedRig.Spine]*CapturedRig.Euler(lean*.55f);p.Rotation[CapturedRig.Chest]=p.Rotation[CapturedRig.Chest]*CapturedRig.Euler(lean*.25f);
            if(lookWeight>1e-3f)NativeDraftRules.Look(p,Gaze(p,look),lookWeight);
            Solve(p,true,left);Solve(p,false,right);p.World();
        }
        // The head turns fully toward the target but only half-way down (the old stage's look), so the face stays readable
        // from the high camera instead of showing the crown.
        public static Vector3 Gaze(CapturedRig.Pose p,Vector3 target){p.World();float eye=p.WorldPosition[CapturedRig.Head].Y+.165f;target.Y=eye+(target.Y-eye)*.5f;return target;}
        // Solves one arm against the pose's current lean and look (Arms does both; call again once a plant point is known).
        public static void Solve(CapturedRig.Pose p,bool left,in TableArm arm)
        {
            p.World();var chest=p.WorldRotation[CapturedRig.Chest];var inverse=Quaternion.Inverse(chest);float w=Math.Max(0,Math.Min(1,arm.Weight));
            var goal=Vector3.Lerp(Rest(left),arm.Goal,w);var wrist=Quaternion.Slerp(RestWrist,arm.Wrist,w);
            var local=Vector3.Transform(goal-p.WorldPosition[CapturedRig.Chest],inverse);
            CapturedRig.Arm(p,left,local,Quaternion.Normalize(inverse*wrist),new Vector3(left?1:-1,-.8f,-.25f));p.World();
        }
        // The wrist goal that puts the grip (between palm and fingers) on `point` with the wrist turned to `wrist` (root frame).
        public static Vector3 WristFor(Vector3 point,Quaternion wrist)=>point-Vector3.Transform(GripLocal,wrist);
        // How far forward the pilot leans to reach a point on the table (root frame): none for the near row, more for the back.
        public static float LeanFor(Vector3 goal)=>.08f+.34f*Math.Max(0,Math.Min(1,(goal.Z-.24f)/.42f));
    }

    // The table's moving parts, stepped once per rendered frame: the seated pilot (look, lean, both arms), each chip's
    // entrance, lift and pick path, and the plant pulse. Hover state eases exponentially (the same curve at 30 and 240 Hz);
    // the pick follows the authoritative pick age, so every viewer sees the same motion. Nothing is allocated per step.
    public sealed class DraftTableMotion
    {
        public readonly MeleeRig Rig=new MeleeRig();public readonly CapturedRig.Pose Pose=new CapturedRig.Pose();
        public readonly float[] Lift=new float[NativeDraftTable.MaxChips],Scale=new float[NativeDraftTable.MaxChips],Bright=new float[NativeDraftTable.MaxChips];
        public readonly bool[] Visible=new bool[NativeDraftTable.MaxChips];
        public readonly Vector3[] Position=new Vector3[NativeDraftTable.MaxChips];public readonly Quaternion[] Rotation=new Quaternion[NativeDraftTable.MaxChips];
        // The plant pulse: 0..1 while it runs (else -1), at PulseAt around the receiving forearm (PulseAxis, world).
        public float PulseK=-1;public Vector3 PulseAt,PulseAxis=Vector3.UnitX;
        private readonly int[] position=new int[NativeDraftTable.MaxChips];private readonly float[] hand=new float[2];private readonly Vector3[] goal=new Vector3[2];
        private Vector3 look,plant;private float lean=.08f;private bool started;
        // The Siren stands in her own idle (sampled into Pose by the caller) behind the table; the chip flies to her chest.
        public static readonly Vector3 SirenChest=new Vector3(0,1.32f,-.06f);
        public void Reset(){started=false;Array.Clear(Lift,0,Lift.Length);hand[0]=hand[1]=0;lean=.08f;PulseK=-1;}
        private static Vector3 Up(float y)=>new Vector3(0,y,0);
        private static Vector3 HoverGoal(Vector3 slot)=>NativeDraftTable.WristFor(NativeDraftTable.ToSource(slot+Up(NativeDraftTable.Lift+.07f)),NativeDraftTable.ReachWrist);
        private const float HoverReach=.32f;
        // slots/order/count: NativeDraftTable.Arrange. hover: the lifted chip (offer index or -1); reach: the chip the pilot
        // reaches toward (the local drafter's hover, else -1); scan: the reading position the idle gaze rests on; chosen/age:
        // the authoritative pick (-1 before it); elapsed: seconds since the offers began (entrance); time: the idle clock.
        public void Step(Vector3[] slots,int[] order,int count,float time,float elapsed,int hover,int reach,int scan,int chosen,float age,float dt,bool siren)
        {
            count=Math.Min(count,NativeDraftTable.MaxChips);dt=Math.Max(0,Math.Min(.1f,dt));
            for(int k=0;k<count;k++)position[order[k]]=k;
            if(!siren)NativeDraftTable.Seat(Rig,Pose,time,out _,out _);else Pose.World();
            bool picking=chosen>=0&&chosen<count&&age>=0;
            // Gaze: the pick, then the receiving wrist, then back to the table; else the hovered chip; else the idle scan.
            Vector3 centre=NativeDraftTable.ToSource(new Vector3(0,NativeDraftTable.TableTop,NativeDraftTable.RowsMid)),want;
            if(count==0||picking&&age>NativeDraftTable.ReturnEnd-.2f)want=centre;
            else if(picking)want=age<NativeDraftTable.CarryStart||!started?NativeDraftTable.ToSource(slots[chosen]):plant;
            else if(hover>=0&&hover<count)want=NativeDraftTable.ToSource(slots[hover]);
            else want=NativeDraftTable.ToSource(slots[order[Math.Max(0,scan)%count]]);
            look=started?NativeDraftTable.Damp(look,want,7,dt):want;
            // Hover reach: the hand on the chip's side drifts a third of the way toward it (the local drafter only).
            int side=reach>=0&&reach<count&&!picking&&!siren?(slots[reach].X<0?0:1):-1;
            if(side>=0){var g=HoverGoal(slots[reach]);goal[side]=started&&hand[side]>.02f?NativeDraftTable.Damp(goal[side],g,10,dt):g;}
            for(int h=0;h<2;h++)hand[h]=started?NativeDraftTable.Damp(hand[h],h==side?HoverReach:0,8,dt):0;
            float leanWant=.08f;for(int h=0;h<2;h++)leanWant+=(NativeDraftTable.LeanFor(goal[h])-.08f)*.4f*hand[h]/HoverReach;
            int ps=picking?(slots[chosen].X<0?0:1):0;float reachK=0,grab=0,carry=0,plantK=0,ret=0,raise=0;Vector3 chipGoal=default;
            if(picking)
            {
                reachK=NativeDraftTable.Reach(age);grab=NativeDraftTable.Grab(age);carry=NativeDraftTable.Carry(age);plantK=NativeDraftTable.Plant(age);ret=NativeDraftTable.Return(age);raise=NativeDraftTable.Raise(age);
                chipGoal=NativeDraftTable.WristFor(NativeDraftTable.ToSource(slots[chosen]+Up(NativeDraftTable.Lift*NativeDraftTable.Ease(Lift[chosen])+NativeDraftTable.ChipThick*.5f+.012f)),NativeDraftTable.ReachWrist);
                leanWant+=(NativeDraftTable.LeanFor(chipGoal)-.08f)*reachK*(1-carry)+.06f*carry*(1-ret);
            }
            lean=started?NativeDraftTable.Damp(lean,leanWant,9,dt):leanWant;
            for(int i=0;i<count;i++){bool lifted=!picking&&i==hover||picking&&i==chosen&&age<NativeDraftTable.GrabEnd;Lift[i]=started?NativeDraftTable.Damp(Lift[i],lifted?1:0,14,dt):(lifted?1:0);}
            if(!siren)
            {
                // Each hand rests on the table and drifts toward its hover goal; the pick blends on top of that, so nothing jumps.
                TableArm Base(int h)=>new TableArm{Goal=Vector3.Lerp(NativeDraftTable.Rest(h==0),goal[h],hand[h]),Weight=1,Wrist=Quaternion.Slerp(NativeDraftTable.RestWrist,NativeDraftTable.ReachWrist,hand[h])};
                var left=Base(0);var right=Base(1);TableArm pick=default;
                if(picking)
                {
                    // The picking hand goes to the chip; the other wrist rises in front of the chest to receive it.
                    var from=ps==0?left:right;var rest=ps==0?right:left;
                    pick=new TableArm{Goal=Vector3.Lerp(from.Goal,chipGoal,reachK),Weight=1,Wrist=Quaternion.Slerp(from.Wrist,NativeDraftTable.ReachWrist,reachK)};
                    var other=new TableArm{Goal=Vector3.Lerp(rest.Goal,NativeDraftTable.Raised(ps!=0),raise),Weight=1,Wrist=Quaternion.Slerp(rest.Wrist,NativeDraftTable.PlantWrist(ps!=0),raise)};
                    if(ps==0){left=pick;right=other;}else{right=pick;left=other;}
                }
                NativeDraftTable.Arms(Pose,lean,look,1,left,right);
                if(picking)
                {
                    plant=NativeDraftTable.PlantPoint(Pose,ps!=0);
                    if(carry>0||ret>0)
                    {
                        // Carry over a small arc to just above the raised wrist, press in, then return to the table.
                        var press=NativeDraftTable.WristFor(plant+Up(.018f+.014f*(1-plantK)),NativeDraftTable.ReachWrist)+Up(.07f*(float)Math.Sin(carry*Math.PI));
                        pick.Goal=Vector3.Lerp(Vector3.Lerp(pick.Goal,press,carry),NativeDraftTable.Rest(ps==0),ret);pick.Wrist=Quaternion.Slerp(NativeDraftTable.ReachWrist,NativeDraftTable.RestWrist,ret);
                        NativeDraftTable.Solve(Pose,ps==0,pick);
                    }
                }
            }
            else{NativeDraftRules.Look(Pose,NativeDraftTable.Gaze(Pose,look),1);plant=NativeDraftTable.ToSource(SirenChest);}
            // Chips: entrance drop in reading order, eased lift and tilt, the pick riding the hand into the wrist.
            var grip=siren?SirenChest:NativeDraftTable.ToWorld(NativeDraftTable.Grip(Pose,ps==0));var plantWorld=NativeDraftTable.ToWorld(plant);
            float settle=picking?NativeDraftTable.Settle(age):0;
            for(int i=0;i<NativeDraftTable.MaxChips;i++)Visible[i]=false;
            for(int i=0;i<count;i++)
            {
                bool mine=picking&&i==chosen;float t=elapsed-position[i]*.22f;if(t<0&&!mine)continue;
                float e=NativeDraftTable.Ease(Lift[i]),drop=mine?0:NativeDraftTable.Drop(t),bob=(float)Math.Sin(time*2.6f+i*1.7f)*.004f*e;
                var pos=slots[i]+Up(NativeDraftTable.Lift*e+.28f*drop+bob);var rot=Quaternion.CreateFromAxisAngle(Vector3.UnitX,NativeDraftTable.LiftTilt*e*(float)Math.PI/180);float scale=1,bright=(mine?1:NativeDraftTable.Bright(t))*(1+.6f*e);
                if(mine)
                {
                    pos=Vector3.Lerp(pos,grip,grab);if(siren)pos=Vector3.Lerp(pos,SirenChest,carry);
                    if(plantK>0){pos=Vector3.Lerp(pos,plantWorld,plantK);scale=1-plantK;}
                    if(scale<=.01f)continue;
                }
                else if(picking)bright*=1-.45f*settle;
                Visible[i]=true;Position[i]=pos;Rotation[i]=rot;Scale[i]=scale;Bright[i]=bright;
            }
            PulseK=picking?NativeDraftTable.Pulse(age):-1;PulseAt=plantWorld;
            if(!siren&&picking){int h=ps!=0?CapturedRig.LHand:CapturedRig.RHand;var d=Pose.WorldPosition[h]-Pose.WorldPosition[h-1];if(d.LengthSquared()>1e-8f)PulseAxis=Vector3.Normalize(new Vector3(-d.X,d.Y,d.Z));}
            started=true;
        }
    }
}
