using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// THE HUM's DEV preview (DEV panel · Camera · "Level 0 boss preview", on the Level 0 map): a scripted record that plays the whole fight on the presentation clock, in the carved room drawn in place of the map's layout: the intro; a walk; every move of HUM-BUZZ; the
    /// change into BLACKOUT; its moves, a choke and a clipped stick; the change into NOCLIP; it turns over onto the ceiling and walks there, double Row Burst, a lunge, a shift; back down; the death. Presentation only: nothing is spawned, nothing collides or hurts, the
    /// record never reaches the authority or the network. A pure function of the clock. The capture tool reads <see cref="On"/>, <see cref="Start"/> and <see cref="ShownClock"/> by name.
    /// </summary>
    public static class NativeHumBossPreview
    {
        public static bool On;public static double Start=double.NaN,ShownClock;
        public static HumBossView Shown=HumBossView.None;
        public const float SpawnAt=.5f;
        /// <summary>The room the script's numbers live in: a carved Level 0 room at this cell centre (a 16 x 16 layout).</summary>
        public const int RoomCellX=8,RoomCellZ=8;
        public static readonly HumRoom Room=new HumRoom(RoomCellX-NativeHumArena.Cells/2,RoomCellZ-NativeHumArena.Cells/2,Vector3.Zero);
        private static Vector3 Spawn;private static float SpawnYaw;
        /// <summary>The stand-in pilots (x, z): targets, the rows' reasons and the camera's scale.</summary>
        public static readonly Vector2[] Pilots=new Vector2[4];
        private struct Act{public string Id;public float At,Hit,Dur;public int Target;public float LaneX0,LaneZ0,LaneX1,LaneZ1;public int RowAxis,RowMask;}
        private struct Key{public float At,X,Z,Yaw;public byte Kind;}
        /// <summary>How the camera frames this stretch: yaw off the boss's facing, distance, eye height, look height, fov, and whether it also pans round slowly.</summary>
        public struct Frame{public float At,Yaw,Dist,EyeY,LookY,Fov,Orbit;}
        private const byte Smooth=0,Throw=1;
        private static readonly Act[] acts;private static readonly Key[] keys;private static readonly Frame[] frames;
        public static float IntroEnd,ReachAt,StoopAt,RowAt,LungeAt,TearAt,ShiftAt,Phase2At,PulseAt,CutAt,HandAt,VoicesAt,ChokeAt,ClipLungeAt,ClipAt,Phase3At,TurnAt,Row2At,Lunge3At,Shift3At,FloorAt,DeathAt,Loop;
        private static readonly float[] tornTable=new float[9],voiceTable=new float[6*3];
        static NativeHumBossPreview()
        {
            NativeHumArena.Spawn(Room,out Spawn,out SpawnYaw);var C=Room.Centre;
            // The party stands south of it, spread.
            Pilots[0]=new Vector2(C.X+1.5f,C.Z-9);Pilots[1]=new Vector2(C.X-7,C.Z-6);Pilots[2]=new Vector2(C.X+8,C.Z-3);Pilots[3]=new Vector2(C.X-2,C.Z-13);
            var a=new System.Collections.Generic.List<Act>();var k=new System.Collections.Generic.List<Key>();var fr=new System.Collections.Generic.List<Frame>();
            float t=SpawnAt+HumBossBeats.Fallback.Hold;IntroEnd=t;
            void K(float at,float x,float z,float yaw,byte kind=Smooth)=>k.Add(new Key{At=at,X=x,Z=z,Yaw=yaw,Kind=kind});
            float A(string id,float at,int target=0,float lx0=float.NaN,float lz0=float.NaN,float lx1=float.NaN,float lz1=float.NaN,int axis=0,int mask=0)
            {var m=NativeHumBoss.Move(id);a.Add(new Act{Id=id,At=at,Hit=at+m.Wind,Dur=m.Duration,Target=target,LaneX0=lx0,LaneZ0=lz0,LaneX1=lx1,LaneZ1=lz1,RowAxis=axis,RowMask=mask});return at+m.Duration;}
            void F(float at,float yaw,float dist,float eyeY,float lookY,float fov,float orbit=4)=>fr.Add(new Frame{At=at,Yaw=yaw,Dist=dist,EyeY=eyeY,LookY=lookY,Fov=fov,Orbit=orbit});
            float x0=Spawn.X,z0=Spawn.Z,y0=SpawnYaw;
            K(0,x0,z0,y0);K(t,x0,z0,y0);
            // ---- HUM-BUZZ: a walk toward the party, Reach at the near pilot ----
            F(t,12,12.5f,1.7f,2.25f,56,3);
            K(t+.6f,x0,z0,y0);K(t+4.2f,x0+1,z0-8.4f,y0+6);K(t+4.8f,x0+1,z0-8.4f,y0);
            ReachAt=t+4.8f;F(ReachAt-.6f,28,8.6f,1.5f,2.1f,58,3);t=A("reach",ReachAt,0);
            // Stoop: closer, from low to the side: the head coming down in reach.
            K(t+.2f,x0+1,z0-8.4f,y0);K(t+1.1f,x0+1,z0-10.2f,y0);StoopAt=t+1.2f;F(StoopAt-.7f,62,7.2f,.9f,1.9f,60,-3);t=A("stoop",StoopAt,0);
            // Row Burst: wide, watching the rows flare and burst the length of the room.
            K(t+.2f,x0+1,z0-10.2f,y0);RowAt=t+.7f;F(RowAt-.4f,150,13,1.5f,2.6f,66,5);t=A("row",RowAt,0,axis:0,mask:0b010101);
            // Noclip: it backs to the side wall, its hand into the paper; then down a lane across the room, through a partition.
            float laneZ=C.Z-5.3f,lx0=Room.MaxX-3.5f,lx1=lx0-26;
            K(t+.2f,x0+1,z0-10.2f,y0);K(t+3.2f,lx0,laneZ,-90);K(t+3.8f,lx0,laneZ,-90);LungeAt=t+3.8f;
            F(t+.3f,110,10,1.6f,2.2f,58,4);F(LungeAt-.3f,-45,12,1.4f,2.1f,52,0);
            var lm=NativeHumBoss.Move("lunge");K(LungeAt+lm.Wind,lx0,laneZ,-90);K(LungeAt+lm.Wind+lm.Strike+lm.Follow,lx1,laneZ,-90,Throw);t=A("lunge",LungeAt,1,lx0,laneZ,lx1,laneZ);
            // Peripheral Shift, then Fall Through under the camper: it stands and looks; the carpet does the rest.
            K(t+.3f,lx1,laneZ,-90);K(t+2.4f,lx1+3,laneZ+1,20);F(t,20,11,1.5f,2.1f,58,3);
            ShiftAt=t+1.1f;a.Add(new Act{Id="shift",At=ShiftAt,Hit=ShiftAt,Dur=.45f});
            TearAt=t+2.6f;F(TearAt-.2f,-30,6.5f,3.1f,.5f,62,1);t=A("tear",TearAt,3);t=TearAt+4;
            // ---- the change into BLACKOUT ----
            K(t,lx1+3,laneZ+1,20);K(t+1.8f,C.X,C.Z+3,180);Phase2At=t+2;F(Phase2At-.4f,8,10.5f,1.4f,2.5f,62,2);t=Phase2At+NativeHumBoss.PhaseChangeSeconds;
            K(t,C.X,C.Z+3,180);
            // Max Hum: from the chest; the ring through everything.
            K(t+1,C.X,C.Z+3,180);PulseAt=t+1;F(PulseAt-.5f,18,9,1.1f,2.5f,64,3);t=A("pulse",PulseAt,0);
            // Lights Out: everything stops; it isn't where you left it.
            K(t+.4f,C.X,C.Z+3,180);CutAt=t+.8f;var cm=NativeHumBoss.Move("cut");F(CutAt-.3f,140,9,1.5f,2.2f,58,0);
            K(CutAt+.4f,C.X,C.Z+3,180);K(CutAt+.42f,Pilots[0].X-2.8f,Pilots[0].Y+.6f,100);K(CutAt+cm.Wind,Pilots[0].X-2.8f,Pilots[0].Y+.6f,100);t=A("cut",CutAt,0);
            // Wall Hand: the arm comes out of the wall by the pilot on the far side.
            K(t+.3f,Pilots[0].X-2.8f,Pilots[0].Y+.6f,100);K(t+1.1f,Pilots[0].X-2.8f,Pilots[0].Y+.6f,60);HandAt=t+1.2f;F(HandAt-.2f,-60,12,1.3f,2.1f,60,2);t=A("hand",HandAt,2);
            // Familiar Voices: faces in the walls round each pilot.
            K(t+.2f,Pilots[0].X-2.8f,Pilots[0].Y+.6f,60);K(t+1.5f,C.X,C.Z-1,60);VoicesAt=t+1.6f;F(VoicesAt-.2f,90,9,1.5f,2.3f,60,5);t=A("voices",VoicesAt,1);
            // A tube charge choked: three broken in Max Hum: the stagger.
            K(t+.2f,C.X,C.Z-1,60);K(t+1.1f,C.X,C.Z-1,180);ChokeAt=t+1.2f;F(ChokeAt-.4f,160,5.8f,2.3f,3.1f,52,4);t=A("pulse",ChokeAt,0);ChokeBreak0=ChokeAt+.25f;ChokeBreak1=ChokeAt+.62f;ChokeBreak2=ChokeAt+1.05f;t=ChokeAt+3.2f;
            // A lunge baited into the east wall: stuck half inside it, glitching.
            K(t,C.X,C.Z-1,180);K(t+1.0f,C.X,C.Z-1,90);ClipLungeAt=t+1.1f;float wx=Room.MaxX-.8f;float zl=C.Z-1;F(ClipLungeAt,-55,10.5f,1.5f,2.3f,56,0);
            K(ClipLungeAt+lm.Wind,C.X,zl,90);ClipAt=ClipLungeAt+lm.Wind+.72f;K(ClipAt,wx,zl,90,Throw);a.Add(new Act{Id="lunge",At=ClipLungeAt,Hit=ClipLungeAt+lm.Wind,Dur=lm.Duration,Target=2,LaneX0=C.X,LaneZ0=zl,LaneX1=wx,LaneZ1=zl});t=ClipAt+NativeHumBoss.ClippedSeconds+.5f;
            // ---- the change into NOCLIP: it rears through the ceiling ----
            K(t,wx,zl,90);K(t+1.7f,C.X+1,C.Z,180);Phase3At=t+1.9f;F(Phase3At-.6f,14,9.5f,.9f,3.2f,68,2);t=Phase3At+NativeHumBoss.PhaseChangeSeconds;
            K(t,C.X+1,C.Z,180);
            // It turns over onto the ceiling, walking; the slow turn; walks there; a double Row Burst from above; a lunge; a shift.
            TurnAt=t+.4f;F(t,50,10,1.2f,2.5f,62,4);
            K(TurnAt,C.X+1,C.Z,180);K(TurnAt+NativeHumBossMotion.TurnOver+.6f,C.X+1,C.Z-3,180);K(TurnAt+NativeHumBossMotion.TurnOver+3.4f,C.X+1,C.Z-9.4f,180);t=TurnAt+NativeHumBossMotion.TurnOver+3.5f;
            Row2At=t;F(Row2At,120,10.5f,1.2f,2.8f,66,5);A("row",Row2At,0,axis:1,mask:0b101010);Row2Second=Row2At+1.3f;t=Row2At+NativeHumBoss.Move("row").Duration+1.4f;
            K(t,C.X+1,C.Z-9.4f,180);K(t+2.6f,C.X-6,C.Z-11,90);Lunge3At=t+2.7f;F(t+.3f,-30,10,1.3f,2.6f,58,3);var lz=C.Z-11;
            K(Lunge3At+lm.Wind,C.X-6,lz,90);K(Lunge3At+lm.Wind+lm.Strike+lm.Follow,C.X+14,lz,90,Throw);t=A("lunge",Lunge3At,2,C.X-6,lz,C.X+14,lz);
            Shift3At=t+.2f;a.Add(new Act{Id="shift",At=Shift3At,Hit=Shift3At,Dur=.45f});
            K(t+.3f,C.X+14,lz,90);K(t+2.4f,C.X+10,lz+4,-20);FloorAt=t+2.5f;K(FloorAt,C.X+10,lz+4,-20);var fdir=BossMath.Forward(-20*BossMath.Deg);float dx=C.X+10+fdir.X*4,dz=lz+4+fdir.Z*4;K(FloorAt+NativeHumBossMotion.TurnOver+.3f,dx,dz,-20);t=FloorAt+NativeHumBossMotion.TurnOver+.4f;F(FloorAt,-120,11,1.4f,2.4f,60,4);
            // ---- the death ----
            K(t+1.5f,dx,dz,-20);DeathAt=t+1.7f;F(DeathAt-.5f,38,8.5f,1.15f,1.9f,52,4);
            F(DeathAt+2.9f,-150,13,1.55f,1.7f,62,1.5f);
            Loop=DeathAt+8;K(Loop,dx,dz,-20);
            acts=a.ToArray();keys=k.ToArray();fr.Sort((p,q)=>p.At.CompareTo(q.At));frames=fr.ToArray();
            // The fixed tables the record points at: where Fall Through's holes stay, the voices' wall points (round the pilots), when parts broke.
            tornTable[0]=Pilots[3].X;tornTable[1]=Pilots[3].Y;tornTable[2]=Phase3At+1;tornTable[3]=C.X+7;tornTable[4]=C.Z-4;tornTable[5]=Phase3At+3;tornTable[6]=C.X-8;tornTable[7]=C.Z+6;tornTable[8]=Phase3At+6;
            for(int i=0;i<3;i++){var p=Pilots[i];float d=NativeHumArena.NearestWall(Room,0,p.X,p.Y,out float vx,out float vz,out float nx,out float nz);int o=i*6;voiceTable[o]=vx;voiceTable[o+1]=2.05f;voiceTable[o+2]=vz;voiceTable[o+3]=nx;voiceTable[o+4]=nz;voiceTable[o+5]=VoicesAt+NativeHumBoss.Move("voices").Wind+i*.2f;}
        }
        private static float ChokeBreak0,ChokeBreak1,ChokeBreak2,Row2Second;
        /// <summary>The record `p` seconds into the script, its times offset to `baseAt`.</summary>
        public static void View(float p,float baseAt,ref HumBossView v,out Vector3 velocity,out Vector2 target)
        {
            v=HumBossView.None;v.live=true;v.spawned=baseAt+SpawnAt;v.maxHp=8000;
            Root(p,out float x,out float z,out float yaw);Root(p+1/240f,out float x2,out float z2,out _);Root(p-1/240f,out float x1,out float z1,out _);
            v.x=x;v.z=z;v.y=0;v.yaw=yaw;velocity=new Vector3((x2-x1)*120,0,(z2-z1)*120);
            if(Math.Abs(velocity.X)>40||Math.Abs(velocity.Z)>40)velocity=Vector3.Zero;
            v.phase=p>=Phase3At?3:p>=Phase2At?2:1;
            if(p>=Phase2At)v.phaseAt=baseAt+(p>=Phase3At?Phase3At:Phase2At);
            v.hp=v.maxHp*(p<Phase2At?Math.Max(.66f,1-.34f*p/Phase2At):p<Phase3At?Math.Max(.31f,.65f-.34f*(p-Phase2At)/(Phase3At-Phase2At)):Math.Max(0,.3f-.3f*(p-Phase3At)/(DeathAt-Phase3At)));
            target=Pilots[0];
            // The move playing: the latest started.
            for(int i=acts.Length-1;i>=0;i--)
            {
                var a=acts[i];if(p<a.At)continue;if(p>a.At+a.Dur+1.2f)break;
                v.action=a.Id;v.actionId=1000+i+(int)Math.Round(baseAt*4)%997*100;v.actionAt=baseAt+a.At;v.hitAt=baseAt+a.Hit;v.duration=a.Dur;v.actionTarget=a.Target;target=Pilots[a.Target];
                if(!float.IsNaN(a.LaneX0)){v.laneX0=a.LaneX0;v.laneZ0=a.LaneZ0;v.laneX1=a.LaneX1;v.laneZ1=a.LaneZ1;}
                break;
            }
            // Row Burst's rows hold after the move, so the scorch and the return play out.
            if(p>=RowAt&&p<RowAt+NativeHumBoss.Move("row").Wind+HumBossStage.RowDark+1){v.rowAxis=0;v.rowMask=0b010101;v.rowAt=baseAt+RowAt+NativeHumBoss.Move("row").Wind;}
            if(p>=Row2At&&p<Row2At+NativeHumBoss.Move("row").Wind+HumBossStage.RowDark+2)
            {
                v.rowAxis=1;v.rowMask=0b101010;v.rowAt=baseAt+Row2At+NativeHumBoss.Move("row").Wind;
                if(p>=Row2Second){v.row2Axis=0;v.row2Mask=0b101010;v.row2At=baseAt+Row2Second+NativeHumBoss.Move("row").Wind;}
            }
            // Fall Through under the pilot who stood still; the meter builds before it.
            float tm=NativeHumBoss.Move("tear").Wind;
            if(p>=TearAt-6&&p<TearAt+tm+6){v.heard=BossMath.Clamp01((p-(TearAt-6))/6);v.heardX=Pilots[3].X;v.heardZ=Pilots[3].Y;if(p>=TearAt){v.tearX=Pilots[3].X;v.tearZ=Pilots[3].Y;v.tearAt=baseAt+TearAt+tm;v.heard=0;}}
            // The holes that stay.
            if(p>=Phase3At){v.torn=TornAt(baseAt);v.tornCount=3;}
            // Max Hum's ring, for both charges.
            float pm=NativeHumBoss.Move("pulse").Wind;
            if(p>=PulseAt+pm&&p<PulseAt+pm+5){Root(PulseAt,out float rx,out float rz,out _);v.ringX=rx;v.ringZ=rz;v.ringAt=baseAt+PulseAt+pm;}
            // Wall Hand: the east side, by pilot 2.
            if(p>=HandAt-.2f&&p<HandAt+4)
            {
                var pl=Pilots[2];NativeHumArena.NearestWall(Room,0,pl.X,pl.Y,out float wx,out float wz,out float nx,out float nz);
                v.handX=wx;v.handY=1.5f;v.handZ=wz;v.handNx=nx;v.handNz=nz;v.handAt=baseAt+HandAt+NativeHumBoss.Move("hand").Wind;v.handUntil=baseAt+HandAt+NativeHumBoss.Move("hand").Wind+.25f+1.5f;
            }
            if(p>=VoicesAt&&p<VoicesAt+6){v.voices=VoicesAtBase(baseAt);v.voiceCount=3;}
            // The shifts.
            if(p>=Shift3At){v.shiftAt=baseAt+Shift3At;v.shiftFrom=0b01010101;v.shiftMask=0b10101010;}else if(p>=ShiftAt){v.shiftAt=baseAt+ShiftAt;v.shiftFrom=0;v.shiftMask=0b01010101;}
            // Circuits it has killed in the blackout.
            if(p>=Phase2At+8&&p<Phase3At){v.circuitMask=0b00000011;v.circuitAt=baseAt+Phase2At+8;}
            // Breaks: three tubes in the charge: the choke.
            v.partAt=PartsAt(baseAt,p);
            if(p>=ChokeBreak0)v.parts|=1<<6;if(p>=ChokeBreak1)v.parts|=1<<4;if(p>=ChokeBreak2)v.parts|=1<<2;
            if(p>=ChokeBreak2&&p<ChokeBreak2+NativeHumBoss.ChokeStagger+.5f){v.staggerAt=baseAt+ChokeBreak2;v.staggerSeconds=NativeHumBoss.ChokeStagger;v.staggerCause="choke";v.staggerPushX=0;v.staggerPushZ=1;v.cancelAt=baseAt+ChokeBreak2;}
            // Stuck in the wall.
            if(p>=ClipAt&&p<ClipAt+NativeHumBoss.ClippedSeconds+1){v.clipAt=baseAt+ClipAt;v.clipSeconds=NativeHumBoss.ClippedSeconds;v.cancelAt=baseAt+ClipAt;}
            // Over and back.
            if(p>=TurnAt){v.ceilingAt=baseAt+TurnAt;if(p>=FloorAt)v.floorAt=baseAt+FloorAt;}
            if(p>=DeathAt){v.diedAt=baseAt+DeathAt;v.hp=0;}
        }
        private static readonly float[] tornBuf=new float[9],voiceBuf=new float[18],partBuf=new float[8];
        private static float[] TornAt(float baseAt){for(int i=0;i<9;i++)tornBuf[i]=i%3==2?tornTable[i]+baseAt:tornTable[i];return tornBuf;}
        private static float[] VoicesAtBase(float baseAt){for(int i=0;i<18;i++)voiceBuf[i]=i%6==5?voiceTable[i]+baseAt:voiceTable[i];return voiceBuf;}
        private static float[] PartsAt(float baseAt,float p)
        {
            for(int i=0;i<8;i++)partBuf[i]=float.NaN;if(p>=ChokeBreak0)partBuf[6]=baseAt+ChokeBreak0;if(p>=ChokeBreak1)partBuf[4]=baseAt+ChokeBreak1;if(p>=ChokeBreak2)partBuf[2]=baseAt+ChokeBreak2;return partBuf;
        }
        /// <summary>The scripted root at `p`, on the record's floor plan.</summary>
        public static void Root(float p,out float x,out float z,out float yaw)
        {
            int i=0;while(i<keys.Length-1&&keys[i+1].At<=p)i++;
            if(i>=keys.Length-1){var e=keys[keys.Length-1];x=e.X;z=e.Z;yaw=e.Yaw;return;}
            var a=keys[i];var b=keys[i+1];float u=b.At>a.At?BossMath.Clamp01((p-a.At)/(b.At-a.At)):1,f;
            if(b.Kind==Throw){float T=b.At-a.At,acc=Math.Min(.18f,T*.2f),skid=Math.Min(.55f,T*.3f),cruise=T-acc-skid,s=p-a.At;float vmax=1/(acc*.5f+cruise+skid*.5f);
                f=s<acc?.5f*vmax*s*s/acc:s<acc+cruise?vmax*(acc*.5f+(s-acc)):s<T?vmax*(acc*.5f+cruise+(s-acc-cruise)-.5f*(s-acc-cruise)*(s-acc-cruise)/skid):1;}
            else f=BossMath.Smoother(u);
            x=a.X+(b.X-a.X)*f;z=a.Z+(b.Z-a.Z)*f;yaw=a.Yaw+BossMath.WrapDeg(b.Yaw-a.Yaw)*BossMath.Smoother(u);
        }
        /// <summary>The preview's own blows: a burst from the party in the first walk, and through the choke.</summary>
        public static int Blows(float p,float baseAt,float[] at,Vector3[] push,float[] size)
        {
            int n=0;for(int k=0;k<6&&n<at.Length;k++){float b=IntroEnd+1.4f+k*.48f;if(p<b)break;at[n]=baseAt+b;push[n]=Vector3.Normalize(new Vector3(k%2==0?.3f:-.4f,0,1));size[n]=k==4?1.2f:.6f;n++;}
            return n;
        }
        /// <summary>The directed camera after the intro: a slow orbit that reframes for each move, from the frames' table, blended between them, on the record's root; of the places near the wanted one, the first with a clear view (nothing right by the eye, no partition across the shot). Pure: the presenter springs its eye toward this.</summary>
        public static void Camera(float p,Vector3 head,Vector3 root,float yawDegrees,int mask,out Vector3 eye,out Vector3 look,out float fov)
        {
            int i=0;while(i<frames.Length-1&&frames[i+1].At<=p)i++;var a=frames[i];var b=i<frames.Length-1?frames[i+1]:a;
            float blendFrom=b.At-1.1f;float u=b.At>a.At?BossMath.Smoother((p-blendFrom)/1.1f):0;
            float orbit=(p-a.At)*a.Orbit;float yawOff=a.Yaw+orbit+BossMath.WrapDeg(b.Yaw-(a.Yaw+orbit))*u;float dist=a.Dist+(b.Dist-a.Dist)*u,ey=a.EyeY+(b.EyeY-a.EyeY)*u,ly=a.LookY+(b.LookY-a.LookY)*u;fov=a.Fov+(b.Fov-a.Fov)*u;
            look=new Vector3(root.X*.5f+head.X*.5f,ly,root.Z*.5f+head.Z*.5f);var c=new Vector3(root.X,0,root.Z);eye=default;bool found=false;
            for(int k=0;k<=12&&!found;k++)for(int sign=-1;sign<=1&&!found;sign+=2)
            {
                if(k==0&&sign>0)continue;float ang=(yawDegrees+yawOff+sign*k*15)*BossMath.Deg;var e=c+BossMath.Forward(ang)*dist;e.Y=ey;
                if(!NativeHumArena.ClearEye(Room,mask,e,look,1.5f))continue;eye=e;found=true;
            }
            if(!found){float ang=(yawDegrees+yawOff)*BossMath.Deg;eye=c+BossMath.Forward(ang)*dist;eye.Y=ey;NativeHumArena.PushEye(Room,mask,ref eye,look);}
            float inset=.55f;eye.X=BossMath.Clamp(eye.X,Room.MinX+inset,Room.MaxX-inset);eye.Z=BossMath.Clamp(eye.Z,Room.MinZ+inset,Room.MaxZ-inset);
        }
        /// <summary>Which pilot the record's target is on the floor plan (for the presenter).</summary>
        public static Vector3 TargetPoint(Vector2 t)=>new Vector3(t.X,1.6f,t.Y);
    }
}
