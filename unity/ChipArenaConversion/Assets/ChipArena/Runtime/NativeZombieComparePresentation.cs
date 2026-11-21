using System;
using UnityEngine;
using NVector=System.Numerics.Vector3;
using C=ChipArena.UnityConversion.NativeCyberTheme;

namespace ChipArena.UnityConversion
{
    // DEV panel · Previews · Zombie animation compare (owner 2026-11-04, Core/NativeZombieCompare): two zombies side by side ahead of the local
    // pilot, the capture on the left and the game's gait on the right, on the model or the voxel body, on one route. The floor's
    // half-metre grid is the route's own: a planted foot stands still on it. Presentation only, and only while it is on.
    public sealed partial class ArenaPresentation
    {
        public bool ZombieCompareOn;
        public readonly NativeZombieCompare ZombieCompare=new NativeZombieCompare();
        /// <summary>The voxel body instead of the model; the pause; the grid and the root, velocity and foot marks; walking the route across the
        /// floor instead of on the spot; the view's angle (deg: 90 from the side); the clock's rate.</summary>
        public bool ZombieCompareVoxel,ZombieComparePaused,ZombieCompareMarks=true,ZombieCompareTravel;
        public float ZombieCompareView=90,ZombieCompareRate=1;
        /// <summary>The pair lifted clear of the arena onto a plain floor of its own (nothing in the way; a fly camera or the capture tool looks at it).</summary>
        public bool ZombieCompareClear;
        /// <summary>Where the pair stands and the way the pilot faced as it started.</summary>
        public Vector3 ZombieCompareAnchor=>zcAt;public float ZombieCompareYaw=>zcYaw;
        private int zcSteps;private CapturedAnimation.Player zcLeft,zcRight;private Vector3 zcAt;private float zcYaw=float.NaN;private int zcKind=-1,zcLeftId,zcRightId;private NVector zcOrigin;
        /// <summary>One frame on, paused.</summary>
        public void ZombieCompareFrame(){zcSteps++;ZombieComparePaused=true;}
        /// <summary>Starts the route over, ahead of the pilot.</summary>
        public void ZombieCompareRestart(){zcYaw=float.NaN;ZombieCompare.Reset();}
        private void ZombieCompareTick(NativeArena arena)
        {
            var me=arena.Local;if(me?.Body==null||captured==null)return;
            if(float.IsNaN(zcYaw)||zcLeft==null||zcKind!=ZombieCompare.Kind)
            {
                float yr=me.Yaw*Mathf.Deg2Rad;zcYaw=me.Yaw;zcAt=me.Body.position+new Vector3(Mathf.Sin(yr),0,Mathf.Cos(yr))*4.5f;if(ZombieCompareClear)zcAt+=Vector3.up*140;
                if(zcKind!=ZombieCompare.Kind||zcLeft==null)
                {
                    zcKind=ZombieCompare.Kind;zcLeft?.Dispose();zcRight?.Dispose();
                    // (Two ids that wear one outfit: nothing but the motion differs.)
                    zcLeftId=995100;zcRightId=995101;var look=NativeZombieLooks.Of(zcKind,zcLeftId,zombieTheme);while(!ReferenceEquals(NativeZombieLooks.Of(zcKind,zcRightId,zombieTheme),look)&&zcRightId<995400)zcRightId++;
                    zcLeft=captured.Create(transform,zcLeftId);zcRight=captured.Create(transform,zcRightId);
                }
                ZombieCompare.Reset();zcOrigin=ZombieCompare.Body;
            }
            bool model=!ZombieCompareVoxel&&ZombieModelled(zcKind,NativeHordeModels.Full);
            ZombieCompare.Legs=model?ZombieModelLegs(zcLeftId,zcKind,NativeHordeModels.Full):1;
            float dt=ZombieComparePaused?0:Mathf.Min(.05f,Time.unscaledDeltaTime)*ZombieCompareRate;if(zcSteps>0){dt=1/60f;zcSteps--;}
            ZombieCompare.Step(dt,minionPose,zombieCompareRight);
            // Across the view (ZombieCompareView 90) or facing it; the two a shoulder's width apart.
            float baseYaw=zcYaw+180+ZombieCompareView;Vector3 aside=Quaternion.Euler(0,zcYaw,0)*Vector3.right;
            float sc=NativeHordeKinds.Scale[NativeHordeKinds.Index(zcKind)],apart=.95f*sc;var kindGround=zcAt.y-.85f;
            var root=ZombieCompare.Input.Root;
            if(ZombieCompareClear&&sourceModels!=null)sourceModels.QueuePiece(WeaponShape.Cube,false,Matrix4x4.TRS(new Vector3(zcAt.x,kindGround-.03f,zcAt.z),Quaternion.identity,new Vector3(40,.04f,40)),new Color(.2f,.22f,.26f));
            if(ZombieCompareTravel&&(root-zcOrigin).Length()>9){zcOrigin=root;}
            Vector3 Offset(NVector v){var d=v-(ZombieCompareTravel?zcOrigin:new NVector(root.X,root.Y,root.Z));return Quaternion.Euler(0,baseYaw,0)*new Vector3(d.X,0,d.Z)*sc;}
            ZombieCompareDraw(zcLeft,zcLeftId,ZombieCompare.Left,minionPose,zcAt-aside*apart,baseYaw,kindGround,sc,model,Offset);
            // (ZombieModelPose reads the gait's pose from minionPose: the right's goes through it after the left's.)
            for(int b=0;b<CapturedRig.Bones.Length;b++){minionPose.Position[b]=zombieCompareRight.Position[b];minionPose.Rotation[b]=zombieCompareRight.Rotation[b];}
            ZombieCompareDraw(zcRight,zcRightId,ZombieCompare.Right,minionPose,zcAt+aside*apart,baseYaw,kindGround,sc,model,Offset);
        }
        private readonly CapturedRig.Pose zombieCompareRight=new CapturedRig.Pose();
        private void ZombieCompareDraw(CapturedAnimation.Player rig,int id,NativeHordeGait g,CapturedRig.Pose pose,Vector3 at,float baseYaw,float ground,float sc,bool model,Func<NVector,Vector3> offset)
        {
            var input=ZombieCompare.Input;Vector3 pos=at+offset(input.Root);pos.y=ground+NativeHordeKinds.DrawRoot(zcKind,false);float yaw=baseYaw+g.Yaw;
            pose.World();NativePoseWriter.Write(rig.Bones,pose,"the zombie comparison");
            rig.Root.transform.SetPositionAndRotation(pos,Quaternion.Euler(0,yaw,0));rig.Root.transform.localScale=Vector3.one*sc;
            if(!model||!ZombieModelPose(rig,id,zcKind,NativeHordeModels.Full,true,false,false,g,0,0,1,0,0,0))
            {var glow=HordeGlow(id);HordeQueue(rig,NativeHordeModels.Id(zcKind,0,NativeHordeModels.Full),id,0,0,0,glow,1);HordeQueueJaw(rig,zcKind,0,NativeHordeModels.Full,0,glow,1);}
            if(!ZombieCompareMarks||sourceModels==null)return;
            // The floor's grid, the root's facing (cyan), its velocity (yellow) and the feet: planted green, in the air red.
            Vector3 Mark(NVector v){var w=at+offset(v);w.y=ground+.012f;return w;}
            var c=input.Root;float gx=Mathf.Round(c.X*2)/2,gz=Mathf.Round(c.Z*2)/2;
            for(int k=-5;k<=5;k++)
            {
                ZombieLine(Mark(new NVector(gx+k*.5f,0,gz-2.5f)),Mark(new NVector(gx+k*.5f,0,gz+2.5f)),.012f,new Color(.3f,.34f,.4f));
                ZombieLine(Mark(new NVector(gx-2.5f,0,gz+k*.5f)),Mark(new NVector(gx+2.5f,0,gz+k*.5f)),.012f,new Color(.3f,.34f,.4f));
            }
            Vector3 hip=pos;hip.y=ground+.04f;float yr=g.Yaw*Mathf.Deg2Rad;var face=new NVector(Mathf.Sin(yr),0,Mathf.Cos(yr));
            ZombieLine(hip,Mark(c+face*.7f)+Vector3.up*.03f,.03f,new Color(.2f,.9f,1));
            var vel=input.Velocity;vel.Y=0;if(vel.Length()>.05f)ZombieLine(hip,Mark(c+NVector.Normalize(vel)*.2f*Mathf.Min(6,vel.Length()))+Vector3.up*.05f,.024f,new Color(1,.85f,.2f));
            for(int i=0;i<2;i++)
            {
                var w=Mark(g.FootWorld[i]);bool air=g.FootAir[i];float turn=baseYaw+(air?g.Yaw:g.FootTurn(i));
                sourceModels.QueuePiece(WeaponShape.Cube,true,Matrix4x4.TRS(w,Quaternion.Euler(0,turn,0),new Vector3(.1f,.012f,.2f)),air?new Color(1,.25f,.2f):new Color(.25f,1,.4f));
            }
        }
        private void ZombieLine(Vector3 a,Vector3 b,float width,Color c)
        {
            var d=b-a;float n=d.magnitude;if(n<1e-3f)return;
            sourceModels.QueuePiece(WeaponShape.Cube,true,Matrix4x4.TRS((a+b)*.5f,Quaternion.LookRotation(d/n,Vector3.up),new Vector3(width,.006f,n)),c);
        }
        private void ClearZombieCompare(){zcLeft?.Dispose();zcRight?.Dispose();zcLeft=zcRight=null;zcYaw=float.NaN;zcKind=-1;}
    }
    public sealed partial class ConversionGame
    {
        private string[] zombieCompareTakes;private int[] zombieCompareClips;
        private float ZombieComparePanel(float x,float y,float w)
        {
            y=DevHead(x,y,w,"ZOMBIE ANIMATION COMPARE (DEV)");DevToggleAct(new Rect(x,y,w,32),"preview-zombie-compare",presentation!=null&&presentation.ZombieCompareOn);y+=38;
            if(presentation==null||!presentation.ZombieCompareOn)return DevNote(x,y,w,"Two zombies on one route: left, the capture as converted; right, the game's gait. Choose the take, speed and turns; pause and step; switch off the gait's stages.",C.Muted());
            var z=presentation.ZombieCompare;const NativeDevState L=NativeDevState.Live;const string why=NativeDevActions.Local;
            if(zombieCompareTakes==null)
            {
                NativeZombieMotion.Warm();var names=new System.Collections.Generic.List<string>{"Kind's own"};var clips=new System.Collections.Generic.List<int>{-1};
                for(int i=0;i<NativeZombieClips.Count;i++)if(NativeZombieClips.Clip(i).Loop&&NativeZombieClips.Speed(i)>0){names.Add(NativeZombieClips.Key(i));clips.Add(i);}
                zombieCompareTakes=names.ToArray();zombieCompareClips=clips.ToArray();
            }
            int k=Array.IndexOf(NativeZombieCompare.Kinds,z.Kind);k=DevChoice(x,ref y,w,NativeZombieCompare.KindNames,Mathf.Max(0,k),L,why,5,"Kind");
            if(NativeZombieCompare.Kinds[k]!=z.Kind){z.Kind=NativeZombieCompare.Kinds[k];var st=NativeHordeKinds.ProposedRow(z.Kind);z.Speed=st.walk;}
            int take=DevChoice(x,ref y,w,zombieCompareTakes,Mathf.Max(0,Array.IndexOf(zombieCompareClips,z.Take)),L,why,4,"Take");if(zombieCompareClips[take]!=z.Take){z.Take=zombieCompareClips[take];presentation.ZombieCompareRestart();}
            int route=DevChoice(x,ref y,w,NativeZombieCompare.RouteNames,(int)z.Route,L,why,4,"Route");if(route!=(int)z.Route){z.Route=(NativeZombieCompare.RouteKind)route;presentation.ZombieCompareRestart();}
            z.Speed=DevSlider(new Rect(x,y,w,34),"Speed",z.Speed,0,6,1," m/s",L,why,out _);y+=42;
            z.TurnRate=DevSlider(new Rect(x,y,w,34),"Circle's turn rate",z.TurnRate,0,360,0," deg/s",L,why,out _);y+=42;
            presentation.ZombieCompareView=DevSlider(new Rect(x,y,w,34),"View angle",presentation.ZombieCompareView,0,360,0," deg",L,why,out _);y+=42;
            presentation.ZombieCompareRate=DevSlider(new Rect(x,y,w,34),"Clock",presentation.ZombieCompareRate,.1f,1,2,"x",L,why,out _);y+=42;
            float half=(w-6)/2;
            if(DevToggle(new Rect(x,y,half,32),"Right: foot placement",z.Plant,L,why))z.Plant=!z.Plant;if(DevToggle(new Rect(x+half+6,y,half,32),"Left: foot placement",z.LeftPlant,L,why))z.LeftPlant=!z.LeftPlant;y+=38;
            if(DevToggle(new Rect(x,y,half,32),"Right: posture",z.Posture,L,why))z.Posture=!z.Posture;if(DevToggle(new Rect(x+half+6,y,half,32),"Left: posture",z.LeftPosture,L,why))z.LeftPosture=!z.LeftPosture;y+=38;
            if(DevToggle(new Rect(x,y,half,32),"Right: retiming",z.Retime,L,why))z.Retime=!z.Retime;if(DevToggle(new Rect(x+half+6,y,half,32),"Left: retiming",z.LeftRetime,L,why))z.LeftRetime=!z.LeftRetime;y+=38;
            if(DevToggle(new Rect(x,y,half,32),"Voxel body",presentation.ZombieCompareVoxel,L,why))presentation.ZombieCompareVoxel=!presentation.ZombieCompareVoxel;if(DevToggle(new Rect(x+half+6,y,half,32),"Grid, root, velocity, feet",presentation.ZombieCompareMarks,L,why))presentation.ZombieCompareMarks=!presentation.ZombieCompareMarks;y+=38;
            if(DevToggle(new Rect(x,y,half,32),"Travel across the floor",presentation.ZombieCompareTravel,L,why))presentation.ZombieCompareTravel=!presentation.ZombieCompareTravel;if(DevButton(new Rect(x+half+6,y,half,32),"Start over ahead of me",L,why))presentation.ZombieCompareRestart();y+=38;
            if(DevToggle(new Rect(x,y,half,32),"Pause",presentation.ZombieComparePaused,L,why))presentation.ZombieComparePaused=!presentation.ZombieComparePaused;if(DevButton(new Rect(x+half+6,y,half,32),"One frame on",L,why))presentation.ZombieCompareFrame();y+=38;
            z.Readout(out float rate,out float stretch,out float steps,out float own);
            int shown=z.Right.RunNow>.5f?z.Right.RunClip:z.Right.WalkClip;
            return DevNote(x,y,w,(shown>=0?NativeZombieClips.Key(shown):"procedural")+": playback "+rate.ToString("0.00")+"x its own, stride "+stretch.ToString("0.00")+"x the capture's, "+steps.ToString("0.0")+" steps/s (capture "+own.ToString("0.0")+"). Green feet are planted, red in the air; cyan is the facing, yellow the velocity.",C.Muted());
        }
    }
}
