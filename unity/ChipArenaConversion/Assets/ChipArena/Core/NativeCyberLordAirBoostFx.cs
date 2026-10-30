using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Cyber Lord's second-jump effect (owner 2026-10-12: "Give this class a double jump as well; Second jump has a cyber
    /// effect"), part of the Cyber Lord's world effects: where a Cyber Lord's replicated air jump is seen (a NativeJumpTrack per
    /// pilot on its drawn feet, Actor.AirJumps / AirJumpAt as the explicit signal: the rule the dagger flip's burst and the jump
    /// animation use, so host, guests, killcam and spectators draw it alike), a hardlight hex platform flashes under the feet
    /// and spreads (a horizontal hexagon of light with upright spokes, kept BoostFloorClear above the floor it left: never a
    /// disc on the floor), pixel shards shed off it, a data-glitch afterimage of the body is left at the take-off (two RGB-split
    /// ghosts, flickering on 24 Hz steps), and a short trail of scanlines strings up the rise. The first-person pilot sees a
    /// gentler version: the platform's outer hexagon at half strength under it and a flare at its view fists (no spokes,
    /// shards, afterimage or trail rising into the camera). Reduced effects: one ring, a soft swell instead of the flash, steady ghosts and fewer, unflickering lines;
    /// the low graphics tier halves the counts. Until the authority grants the Cyber Lord an air jump it shows only in the Dev
    /// Sandbox preview (PreviewAirJump). Presentation only.
    /// </summary>
    public sealed partial class NativeCyberLordFx
    {
        /// <summary>A live boost: the pilot, its seen air-jump time, its feet then (Unity world), the floor it left and its yaw.</summary>
        public struct AirBoost{public int Actor;public float At,Floor,Yaw;public Vector3 Feet;public long Seed;}
        public const int MaxBoosts=8;
        private readonly AirBoost[] boosts=new AirBoost[MaxBoosts];private int boostCount;
        private readonly Dictionary<int,NativeJumpTrack> airTracks=new Dictionary<int,NativeJumpTrack>();
        private int previewActor;private float previewAt=-100;
        /// <summary>This frame's live boosts (read by the checks and the render harness).</summary>
        public int Boosts=>boostCount;public AirBoost BoostOf(int i)=>boosts[i];
        /// <summary>The Dev Sandbox preview: pilot `actor`'s stand-in air-jump time (-100: none), while the authority grants none.</summary>
        public void PreviewAirJump(int actor,float at){previewActor=actor;previewAt=at;}
        private void ClearBoosts(){boostCount=0;airTracks.Clear();previewActor=0;previewAt=-100;}
        /// <summary>Lifetimes (s): the whole boost, its hex platform, the afterimage, the scanline trail, the flash and the shards.</summary>
        public const float BoostLife=.5f,PlatformLife=.32f,AfterimageLife=.3f,ScanLife=.38f,FlashLife=.12f,ShardLife=.4f;
        /// <summary>The platform's radius (m: from, grown by), its drop below the feet, its least height over the floor left, its spin (rad).</summary>
        public const float PlatformFrom=.28f,PlatformGrow=.6f,PlatformBelow=.06f,BoostFloorClear=.22f,PlatformSpin=.5f;
        public const int ScanLines=10,ReducedScanLines=4,BoostShards=8,ReducedBoostShards=3;
        private static float Out(float u){u=Clamp01(u);return 1-(1-u)*(1-u)*(1-u);}
        private static Actor Find(IReadOnlyList<Actor> actors,int id){for(int i=0;i<actors.Count;i++)if(actors[i]!=null&&actors[i].Id==id)return actors[i];return null;}
        private void AirBoosts(ICyberFxSink s,ICyberFxScene scene,float time,float q,bool low)
        {
            var actors=scene.Actors;bool reduced=q<.5f;
            for(int i=0;i<actors.Count;i++)
            {
                var a=actors[i];if(a==null||!CyberLordFeed.Handles(a))continue;
                if(!airTracks.TryGetValue(a.Id,out var t)){t=new NativeJumpTrack();airTracks.Add(a.Id,t);}
                int before=t.AirJumps;var feet=scene.Feet(a);
                bool blocked=a.Hp<=0||a.Rolling(time)||a.StunUntil>time||a.JuggleUntil>time||NativeSpecialPose.Active(a,time)||NativeSpecialPose.Down(a,time);
                t.Explicit=true;t.Observe(time,a.Grounded,feet.Y,blocked);t.AirJumpSignal(CyberLordAirBoost.Signal(a,a.Id==previewActor?previewAt:-100));
                if(t.AirJumps>before&&a.Alive&&!blocked)
                {
                    if(boostCount>=MaxBoosts){Array.Copy(boosts,1,boosts,0,MaxBoosts-1);boostCount--;}
                    float at=t.AirJumpAt>time-.1f&&t.AirJumpAt<=time?t.AirJumpAt:time;
                    boosts[boostCount++]=new AirBoost{Actor=a.Id,At=at,Feet=feet,Floor=t.Floor,Yaw=a.Yaw,Seed=a.Id*7919L+(long)Math.Round(at*240)};
                }
            }
            for(int i=boostCount-1;i>=0;i--)if(time-boosts[i].At>=BoostLife||time<boosts[i].At-.25f){Array.Copy(boosts,i+1,boosts,i,boostCount-i-1);boostCount--;}
            for(int i=0;i<boostCount;i++)Boost(s,scene,boosts[i],time,q,low,reduced);
        }
        private void Boost(ICyberFxSink s,ICyberFxScene scene,in AirBoost b,float time,float q,bool low,bool reduced)
        {
            float age=time-b.At;if(age<0)return;bool me=scene.Viewer==b.Actor;var a=Find(scene.Actors,b.Actor);
            var f=Forward(b.Yaw);var r=Right(f);var c=b.Feet-Up*PlatformBelow;c.Y=Math.Max(c.Y,b.Floor+BoostFloorClear);long seed=b.Seed;float dim=me?.5f:1;
            // The hardlight hex platform: a horizontal hexagon spreading and spinning a little, an inner white one at first, upright
            // spokes of light from its corners, sparks on them.
            float u=age/PlatformLife;
            if(u<1)
            {
                float rad=PlatformFrom+PlatformGrow*Out(u),gain=(float)Math.Pow(1-u,1.5)*(reduced?.6f:1)*dim,spin=PlatformSpin*u;
                var x=r*(float)Math.Cos(spin)+f*(float)Math.Sin(spin);var y=f*(float)Math.Cos(spin)-r*(float)Math.Sin(spin);
                HexCell(s,false,c,x,y,rad,Lin(Cyan,3.4f*gain),me?.025f:.05f);
                if(!reduced&&!me)HexCell(s,false,c-Up*.04f,x,y,rad*1.12f,Lin(Magenta,1.6f*gain),.02f);
                if(!reduced&&!me&&u<.6f)HexCell(s,false,c,x,y,rad*.55f,Lin(White,3*(1-u/.6f)*dim),.025f);
                if(!reduced&&!me)
                    for(int k=0;k<6;k++)
                    {
                        float ang=k*(float)Math.PI/3;var v=c+(x*(float)Math.Cos(ang)+y*(float)Math.Sin(ang))*rad;
                        s.Line(false,v,v+Up*(.12f+.22f*(1-u)),Lin(Cyan,1.6f*gain),.015f,false);
                        if(!low)s.Sprite(false,v,Lin(White,2*gain),.07f*(1-u),Sparkle);
                    }
            }
            // The kick's flash (Reduced: a soft swell, never a hard flash).
            if(age<FlashLife*(reduced?1.6f:1))
            {
                float k=age/(FlashLife*(reduced?1.6f:1));
                s.Glow(c+Up*.05f,Lin(Cyan,(reduced?.7f*(float)Math.Sin(Math.PI*k):2.2f*(1-k))*dim),0,(reduced?.7f:1f)*(.5f+.5f*k),Core,k,seed);
            }
            // Pixel shards shed off the platform, falling and fading (above the floor it left).
            float us=age/ShardLife;
            if(us<1&&!me)
            {
                int n=reduced?ReducedBoostShards:Count(BoostShards,q,low);
                for(int i=0;i<n;i++)
                {
                    float h=H(seed+i,3),ang=i*2.39996323f+h,sp=.5f+.6f*H(seed+i,5);var dir=r*(float)Math.Cos(ang)+f*(float)Math.Sin(ang);
                    var at=c+dir*((.25f+.25f*h)+sp*age)-Up*(2.2f*age*age);at.Y=Math.Max(at.Y,b.Floor+.15f);float z=.045f*(1-us)*(.7f+.6f*H(seed+i,7));
                    s.Voxel(false,WeaponShape.Cube,at,Yaw(b.Yaw)*Quaternion.CreateFromAxisAngle(Up,h*3+age*6),new Vector3(z,z,z),Lin(i%2==0?Cyan:Magenta,2*dim*(1-us)));
                }
            }
            if(me)
            {
                // First person: a flare at both view fists, no afterimage or trail (they would fill the camera).
                float uf=age/.22f;if(uf>=1)return;
                for(int i=0;i<2;i++)if(scene.ViewFist(i==0,out var fist)){s.Sprite(true,fist,Lin(White,1.5f*(1-uf)),.03f,Hot);if(!reduced)HexCell(s,true,fist,r,Up,.035f*(1-.5f*uf),Lin(Cyan,2*(1-uf)),.006f);}
                return;
            }
            if(a==null)return;var now=scene.Feet(a);
            // The data-glitch afterimage: the posed body left at the take-off as two RGB-split ghosts (flickering and torn on the
            // glitch steps; Reduced: steady and fainter, eased out).
            float ug=age/AfterimageLife;
            if(ug<1&&ghostCount<MaxGhosts-2)
            {
                int step=(int)(time*24);bool show=reduced||H(seed+step*7,9)>.25f;
                if(show)
                {
                    float op=reduced?.18f*(1-MeleeMath.Smooth(ug)):.42f*(float)Math.Pow(1-ug,1.2),split=reduced?.02f:.015f+.03f*H(seed+step*11,13);
                    var back=b.Feet-now;if(!reduced)back+=r*((H(seed+step*13,15)-.5f)*.04f);
                    ghosts[ghostCount++]=new CyberGhost{Kind=CyberGhost.Split,Actor=b.Actor,Offset=back+r*split,Tint=Lin(Magenta,1.4f),Opacity=op};
                    ghosts[ghostCount++]=new CyberGhost{Kind=CyberGhost.Split,Actor=b.Actor,Offset=back-r*split,Tint=Lin(Cyan,1.4f),Opacity=op};
                }
            }
            // The scanline trail: horizontal lines strung up the rise from the take-off to the body, the lowest fading first (full
            // effects: some lines drop out per step and tear sideways a touch).
            float ut=age/ScanLife;
            if(ut<1)
            {
                int n=reduced?ReducedScanLines:Count(ScanLines,q,low);float y0=Math.Max(b.Feet.Y+.15f,b.Floor+BoostFloorClear),y1=Math.Max(y0+.2f,now.Y+1.45f);int step=(int)(time*24);
                for(int i=0;i<n;i++)
                {
                    float k=n>1?i/(float)(n-1):0,y=y0+(y1-y0)*k,along=Clamp01((y-b.Feet.Y)/Math.Max(.01f,y1-b.Feet.Y));
                    float alpha=Clamp01(1-ut*1.5f+k*.5f)*(1-ut)*(reduced?.6f:1);if(alpha<=1e-3f)continue;
                    if(!reduced&&H(seed+i*31+step,17)<.25f)continue;
                    var mid=new Vector3(MeleeMath.Lerp(b.Feet.X,now.X,along),y,MeleeMath.Lerp(b.Feet.Z,now.Z,along));if(!reduced)mid+=r*((H(seed+i*37+step,19)-.5f)*.06f);
                    float half=.2f+.12f*H(seed+i,21);
                    s.Line(false,mid-r*half,mid+r*half,Lin(i%3==0?Magenta:i%3==1?Cyan:White,1.8f*alpha),.012f+.008f*H(seed+i,23),false);
                }
            }
        }
    }
}
