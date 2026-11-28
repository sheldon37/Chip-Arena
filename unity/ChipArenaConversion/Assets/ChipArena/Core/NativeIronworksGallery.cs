using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Ironworks kinds' DEV preview script (presentation only: the Dev Sandbox gallery and the offline renders play it; no
    /// authority add exists): one loop per kind of <see cref="Loop"/> seconds through its whole set, the view's fields scripted
    /// as the rules will replicate them. 0-3 s the entrance (the Rigger's hook drop onto its spot; the others wake), 3-7 a walk
    /// round a circle, 7-9 the run, 9.5 the attack, 11.5 the special (the Cinderling's second lunge, the Stoker's pull on the
    /// viewer, the Slagback's slam and its crust shedding at 13.2, the Ladleman's pour, the Rigger's swing then the climb back up
    /// at 13.4 and its next drop at 18.2), 15.6 the Ladleman's spill, then the hits and the death (the Cinderling's pop: a melee
    /// kill).
    /// </summary>
    public static class NativeIronworksGallery
    {
        public const float Loop=22,Entrance=3,Attack=9.5f,Special=11.5f,Shed=13.2f,ClimbAt=13.4f,Lock2=18.2f,Spill=15.6f,DeathAt=19.6f,Walk=2,WalkEnd=7,RunEnd=9;
        public enum State{Ground,Hang,Slide,Throw,Climb,Death}
        public struct Frame
        {
            public State State;public Vector3 Offset,Velocity;public float Yaw,AttackAge,DeathAge,SlideU;public bool Entering,MeleeKill;public IronworksView View;
            /// <summary>The Rigger's height on its chain over its spot (m, world) while off the ground.</summary>
            public float Height;
        }
        /// <summary>Kind `local`'s script at loop time `g` (0..Loop) of a loop that began at clock `start`; `hang` the Rigger's
        /// feet height over its spot; its walk speeds from the proposed row (world m/s; the offsets are world metres, its yaw
        /// relative to `baseYaw`, the pour's yaw absolute); (`spotX`, `spotZ`) the world spot it stands on, where the Rigger's
        /// drop lands.</summary>
        public static Frame At(int local,float g,float start,float hang=6,float baseYaw=0,float spotX=0,float spotZ=0)
        {
            var row=NativeIronworksKinds.Kinds[Math.Max(0,Math.Min(NativeIronworksKinds.KindCount-1,local))].Proposed;var v=IronworksView.None;
            var fr=new Frame{State=State.Ground,AttackAge=float.NaN,DeathAge=float.NaN,View=v,Height=0};
            if(g>=DeathAt){fr.State=State.Death;fr.DeathAge=g-DeathAt;fr.MeleeKill=true;return fr;}
            // the walk and run round a .75 m circle (real motion, so the feet plant)
            if(g>=Walk+Entrance-2&&g<RunEnd){float gg=g-Walk;float speed=g<WalkEnd?row.walk:Math.Min(row.run,3.2f),rad=.75f,ang=gg*speed/rad;if(gg>=0){fr.Offset=new Vector3((float)Math.Cos(ang)-1,0,(float)Math.Sin(ang))*rad;fr.Velocity=new Vector3(-(float)Math.Sin(ang),0,(float)Math.Cos(ang))*speed;fr.Yaw=(float)(Math.Atan2(fr.Velocity.X,fr.Velocity.Z)*180/Math.PI);}}
            if(g<Entrance)fr.Entering=true;
            float strike=NativeHordeKinds.Strike(row,false,0),recover=NativeHordeKinds.Recover(row,false,0);
            if(g>=Attack&&g<Attack+recover)fr.AttackAge=g-Attack;
            switch(local)
            {
                case NativeIronworksKinds.Cinderling:if(g>=Special&&g<Special+recover)fr.AttackAge=g-Special;break;
                case NativeIronworksKinds.Stoker:v.pullAt=start+Special;if(g>=Special+NativeIronworksKinds.PullWindup+NativeIronworksKinds.HookFlight)v.pullTarget=1;break;
                case NativeIronworksKinds.Slagback:if(g>=Special&&g<Special+recover)fr.AttackAge=g-Special;v.shedAt=start+Shed;break;
                case NativeIronworksKinds.Ladleman:v.pourAt=start+Special;v.pourYaw=baseYaw+fr.Yaw;if(g>=Spill)v.spilledAt=start+Spill;break;
                case NativeIronworksKinds.Rigger:
                {
                    if(g>=Special&&g<Special+recover)fr.AttackAge=g-Special;
                    // the entrance's drop (lock .4 s in: the rattle at the loop's start), the climb back up, the lurk, the next drop
                    v.hang=0;v.lockAt=start+.4f;v.lockX=spotX;v.lockZ=spotZ;
                    if(g>=ClimbAt){v.climbAt=start+ClimbAt;v.climbTo=0;}
                    if(g>=Lock2-NativeIronworksKinds.DropRattle){v.lockAt=start+Lock2;v.climbAt=float.NaN;}
                    float lockAt=v.lockAt-start;
                    var ph=NativeIronworksKinds.Drop(v.lockAt,start+g,out _);
                    if(g<ClimbAt||g>=Lock2-NativeIronworksKinds.DropRattle)
                    {
                        if(ph==NativeIronworksKinds.DropPhase.None||ph==NativeIronworksKinds.DropPhase.Rattle||ph==NativeIronworksKinds.DropPhase.Lowered){fr.State=State.Hang;fr.Height=hang;}
                        else if(ph==NativeIronworksKinds.DropPhase.Slide){fr.State=State.Slide;fr.SlideU=NativeIronworksKinds.Slid(v.lockAt,start+g);fr.Height=hang*(1-fr.SlideU);}
                        if(fr.State!=State.Ground){fr.Offset=Vector3.Zero;fr.Velocity=Vector3.Zero;fr.AttackAge=float.NaN;fr.Entering=false;}
                        else if(g>=lockAt+NativeIronworksKinds.DropLand&&g<lockAt+NativeIronworksKinds.DropLand+NativeIronworksKinds.DropRecover)fr.Entering=true;
                    }
                    else
                    {
                        float climbed=NativeIronworksKinds.Climbed(v.climbAt,start+g,hang,out bool throwing);
                        fr.Offset=Vector3.Zero;fr.Velocity=Vector3.Zero;fr.AttackAge=float.NaN;fr.Height=hang*climbed;fr.State=throwing?State.Throw:climbed>=1?State.Hang:State.Climb;
                        if(climbed>=1)v.lockAt=float.NaN;
                    }
                    if(fr.State!=State.Ground)v.hang=0;else v.hang=-1;
                    break;
                }
            }
            fr.View=v;return fr;
        }
    }
}
