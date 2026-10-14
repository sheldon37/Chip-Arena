using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Hulk's charge at a pilot, presentation side (owner 2026-10-14, Windows test build: "The hulk needs to punch and charge
    /// at the player too, not just the wall. The charge is avoidable if they move out of the way in time. (Hulk can collide and
    /// break walls too.) They literally can charge from across the map to the player breaking the walls if they are hunting them
    /// and 'know' where they are."). The rules are Astra's (docs/SIREN-ZOMBIES.md section 10.5, contract v2, proposed; the
    /// numbers in <see cref="NativeHulkMoves"/>); this is what every peer draws from the replicated state:
    /// <list type="bullet">
    /// <item><see cref="State"/>: one Hulk's charge as the presentation tracks it, from the frame fields once they exist
    /// (<see cref="State.Frame"/>) or the Impact events until then (<see cref="State.Event"/>): its start, end, how it ended, the
    /// pilot it hunts, and the heading it locked at the end of the wind-up (read from the add's replicated yaw). A run holds for
    /// as long as the authority keeps it running (cross-map, any length); a lost end event is caught by the add standing still.</item>
    /// <item>The lane (<see cref="LaneHit"/>, <see cref="Lateral"/>): which walls lie ahead on the locked heading (they shake and
    /// dust a moment before it breaks through) and whether a pilot stands in it.</item>
    /// <item>The warning for the hunted pilot only (<see cref="Warn"/>, <see cref="Urgency"/>, <see cref="Layout"/>): a direction
    /// marker toward the Hulk even through walls, its urgency rising as the distance closes and with the closing speed, a rumble
    /// and a gentle shake; the marker keeps clear of the crosshair.</item>
    /// </list>
    /// Nothing here is read by the authority: no hit timing, hit volume, damage, cooldown, movement or network change. Engine
    /// free, no clock, no allocation.
    /// </summary>
    public static class NativeHulkCharge
    {
        private static float Sm(float x)=>MeleeMath.Smooth(x);
        private static float Clamp01(float x)=>x<0?0:x>1?1:x;
        /// <summary>The unit heading of a Unity yaw (degrees): x, z.</summary>
        public static Vector2 Heading(float yaw){float r=yaw*(float)Math.PI/180;return new Vector2((float)Math.Sin(r),(float)Math.Cos(r));}

        // ================================================================================================================
        // One Hulk's charge as tracked by the presentation
        // ================================================================================================================
        /// <summary>A run the authority keeps reporting as running is held for as long as it runs (cross-map, at any length); only
        /// one still reported running this long after its start (s), while the add keeps moving, is drawn as spent (a lost end
        /// event: the frame fields cannot lose it).</summary>
        public const float RunMost=60;
        /// <summary>A run whose add stands (under <see cref="StallSpeed"/> m/s for <see cref="StallSeconds"/>) after its launch is
        /// drawn as spent until the authority's end arrives (a lost end event; the authority's end replaces it).</summary>
        public const float StallSpeed=1.2f,StallSeconds=.5f,StallFrom=.6f;
        public struct State
        {
            /// <summary>The start (the wind-up's), the end, how it ended, the pilot it hunts (a seat id, 0 unknown).</summary>
            public float ChargeAt,ChargeEndAt;public NativeHulkMoves.ChargeEnd How;public int Target;
            /// <summary>The heading (Unity yaw) it locked at the end of the wind-up, when it was read (the first frame past it), and
            /// where the add stood then (its floor point: the lane's streak starts there).</summary>
            public float LockYaw,LockedAt;public bool Locked;public Vector3 LockAt;
            /// <summary>Whether the end was drawn by the presentation (a lost end event: a stall or the longest run), not reported.</summary>
            public bool Assumed;
            private float slowSince;
            public static State None=>new State{ChargeAt=-100,ChargeEndAt=-100,LockYaw=float.NaN,LockedAt=-100,slowSince=float.NaN};
            public bool Known=>ChargeAt>-90;
            public bool Ended=>ChargeEndAt>-90&&ChargeEndAt>=ChargeAt;
            /// <summary>A new charge starting at `at` for pilot `target` (the lock and any end forgotten).</summary>
            public void Start(float at,int target){ChargeAt=at;ChargeEndAt=-100;How=NativeHulkMoves.ChargeEnd.Running;Target=target;LockYaw=float.NaN;LockedAt=-100;Locked=false;Assumed=false;slowSince=float.NaN;}
            /// <summary>The charge ended at `at`, how (the authority's word replaces an assumed end).</summary>
            public void End(float at,NativeHulkMoves.ChargeEnd how){ChargeEndAt=at;How=how;Assumed=false;}
            /// <summary>An Impact event (the fallback until the frame fields exist): `hulk-charge` (Amount the hunted seat) and
            /// `hulk-charge-end` (Amount 1 pilot, 2 solid, 3 spent). False for any other detail.</summary>
            public bool Event(string detail,float amount,float time)
            {
                if(detail==NativeHulkMoves.ChargeEvent){if(!(Known&&Math.Abs(ChargeAt-time)<.02f))Start(time,amount>0&&amount<1e7f?(int)Math.Round(amount):0);return true;}
                if(detail==NativeHulkMoves.ChargeEndEvent){if(Known&&time>=ChargeAt-.02f)End(time,NativeHulkMoves.EndOf((int)Math.Round(amount)));return true;}
                return false;
            }
            /// <summary>The proposed frame fields (chargeAt -100 none, chargeEndAt -100 while running, chargeEnd 0..3, chargeTarget).
            /// A new chargeAt restarts it; the rest follow the frame.</summary>
            public void Frame(float chargeAt,float chargeEndAt,int chargeEnd,int target)
            {
                if(chargeAt<-90){if(Known&&!Ended&&!Assumed){ChargeEndAt=-100;ChargeAt=-100;}return;}
                if(!Known||Math.Abs(ChargeAt-chargeAt)>.02f)Start(chargeAt,target);else if(target!=0)Target=target;
                if(chargeEndAt>-90&&chargeEndAt>=chargeAt)End(chargeEndAt,NativeHulkMoves.EndOf(chargeEnd));
            }
            /// <summary>
            /// The phase at `now` (`yaw` the add's replicated yaw, `speed` its ground speed m/s, `at` its floor point): the wind-up, the
            /// run (the heading locked from the yaw on the first frame past the wind-up), the stop after its end, or none. `age` s since
            /// the start, `endAge` s since the end (NaN while running).
            /// </summary>
            public NativeHulkMoves.ChargePhase Step(float now,float yaw,float speed,Vector3 at,out float age,out float endAge)
            {
                age=float.NaN;endAge=float.NaN;if(!Known||now<ChargeAt-.001f)return NativeHulkMoves.ChargePhase.None;age=now-ChargeAt;
                if(!Ended)
                {
                    if(age>=NativeHulkMoves.ChargeWind)
                    {
                        if(!Locked){Locked=true;LockYaw=yaw;LockedAt=now;LockAt=at;}
                        // A lost end event: the add standing still well after its launch (or a run absurdly long).
                        if(age>RunMost){ChargeEndAt=ChargeAt+RunMost;How=NativeHulkMoves.ChargeEnd.Spent;Assumed=true;}
                        else if(age>NativeHulkMoves.ChargeWind+StallFrom&&speed<StallSpeed){if(float.IsNaN(slowSince))slowSince=now;else if(now-slowSince>=StallSeconds){ChargeEndAt=slowSince;How=NativeHulkMoves.ChargeEnd.Spent;Assumed=true;}}
                        else slowSince=float.NaN;
                    }
                    if(!Ended)return age<NativeHulkMoves.ChargeWind?NativeHulkMoves.ChargePhase.Wind:NativeHulkMoves.ChargePhase.Run;
                }
                endAge=now-ChargeEndAt;return NativeHulkMoves.PhaseOf(age,endAge,How);
            }
            /// <summary>Whether a break at `breakAt` fell in this charge's run (the wind-up's end to its end, a frame's grace).</summary>
            public bool RanThrough(float breakAt)=>Known&&breakAt>=ChargeAt+NativeHulkMoves.ChargeWind-.05f&&(!Ended||breakAt<=ChargeEndAt+.1f);
        }

        // ================================================================================================================
        // The lane: the locked heading from the Hulk's position
        // ================================================================================================================
        /// <summary>The lane's half width for the warning (the Hulk's capsule .95 m, a pilot's .45, a little margin): a pilot
        /// further off the line is out of its way. The authority's own hit test decides the real hit.</summary>
        public const float LaneHalf=1.65f;
        /// <summary>A point's offset from the lane through `from` along `heading` (unit, x z): `along` ahead (negative behind),
        /// `lateral` off the line (m, unsigned).</summary>
        public static float Lateral(Vector3 from,Vector2 heading,Vector3 at,out float along)
        {
            float dx=at.X-from.X,dz=at.Z-from.Z;along=dx*heading.X+dz*heading.Y;return Math.Abs(dx*heading.Y-dz*heading.X);
        }
        /// <summary>
        /// Whether a box (centre, yaw in degrees, half extents) lies on the lane ahead: the Hulk's footprint (`radius`) swept from
        /// `from` (its floor point) along `heading` for `reach` m, and the box overlapping the body's height (`height` m from the
        /// floor). `distance` is how far along it the body first meets the box (0 when it already touches it).
        /// </summary>
        public static bool LaneHit(Vector3 from,Vector2 heading,float reach,float radius,float height,Vector3 center,float yaw,Vector3 half,out float distance)
        {
            distance=0;if(from.Y+height<=center.Y-half.Y+.03f||from.Y>=center.Y+half.Y-.03f)return false;
            float r=-yaw*(float)Math.PI/180,c=(float)Math.Cos(r),s=(float)Math.Sin(r);
            // Into the box's frame (Unity yaw turns +z toward +x).
            float ox=from.X-center.X,oz=from.Z-center.Z,lx=ox*c+oz*s,lz=-ox*s+oz*c,dx=heading.X*c+heading.Y*s,dz=-heading.X*s+heading.Y*c;
            float lo=0,hi=reach;
            if(!Slab(lx,dx,-half.X-radius,half.X+radius,ref lo,ref hi)||!Slab(lz,dz,-half.Z-radius,half.Z+radius,ref lo,ref hi))return false;
            distance=lo;return true;
        }
        private static bool Slab(float o,float d,float min,float max,ref float lo,ref float hi)
        {
            if(Math.Abs(d)<1e-6f)return o>=min&&o<=max;float a=(min-o)/d,b=(max-o)/d;if(a>b){float t=a;a=b;b=t;}lo=Math.Max(lo,a);hi=Math.Min(hi,b);return lo<=hi;
        }
        /// <summary>How long before the body reaches a wall in its lane that wall starts to shake and dust (s).</summary>
        public const float TrembleLead=.55f;
        /// <summary>A wall ahead's shake (m along the heading, signed) `toContact` s before the body reaches it at `time`: building
        /// from nothing at <see cref="TrembleLead"/> to its most at the contact, a heavy rattle (never more than 4 cm).</summary>
        public static float Tremble(float toContact,float time,uint seed)
        {
            if(!(toContact<TrembleLead)||toContact<-.05f)return 0;float k=Sm(1-Math.Max(0,toContact)/TrembleLead),ph=NativeHordeKinds.Hash(seed,57)*6.28f;
            return .036f*k*(.62f*(float)Math.Sin(time*47+ph)+.38f*(float)Math.Sin(time*71+ph*1.7f));
        }

        // ================================================================================================================
        // The warning for the hunted pilot
        // ================================================================================================================
        /// <summary>What the hunted pilot's view shows of a charge at them (presentation only).</summary>
        public struct Warning
        {
            public bool Active,Running;public int Id,Target;
            /// <summary>The Hulk's floor point and chest (Unity world), the unit direction from the pilot to it (horizontal), the
            /// locked heading (unit x z; the facing during the wind-up).</summary>
            public Vector3 Hulk,Chest,Toward;public Vector2 Heading;
            /// <summary>Horizontal distance (m), closing speed (m/s, positive while it comes at the pilot), the pilot's offset off the
            /// lane (m) and whether that is inside it, the urgency (0..1), s into the phase, the fade (0..1; out after the end).</summary>
            public float Distance,Closing,Lateral,Urgency,Age,Fade;public bool InLane;
            /// <summary>The frame it was made on (the HUD and the audio ignore a stale one).</summary>
            public int Frame;
        }
        /// <summary>Distances (m) at which the warning's urgency starts to rise from its floor, and the time to contact (s) at which
        /// the running charge's urgency starts to climb toward its most (reached at <see cref="WarnSoon"/>).</summary>
        public const float WarnFar=60,WarnHorizon=4,WarnSoon=.4f;
        /// <summary>
        /// The warning's urgency (0..1) for a charge `distance` m away closing at `closing` m/s, running or still winding up. Never
        /// falls as the distance closes at a given closing speed, nor as the closing speed rises at a given distance: a floor for
        /// any charge at the pilot, more the nearer it is, and, running, the time to contact pushing it to the top.
        /// </summary>
        public static float Urgency(float distance,float closing,bool running)
        {
            if(float.IsNaN(distance))return 0;float d=Math.Max(0,distance),near=Clamp01(1-d/WarnFar);
            if(!running)return .3f+.3f*near*near;
            float floor=.45f+.25f*near,soon=0;
            if(closing>.05f){float ttc=d/closing;soon=Clamp01(1-(ttc-WarnSoon)/(WarnHorizon-WarnSoon));soon=soon*soon*(3-2*soon);}
            return Math.Min(1,Math.Max(floor,soon));
        }
        /// <summary>
        /// The warning a charge gives the pilot `local` (a seat id) standing at `pilot`: only the hunted pilot gets one (the charge's
        /// `target`), during the wind-up and the run, fading over .45 s after its end. `hulk` its floor point, `velocity` its ground
        /// velocity, `heading` the locked heading (the facing in the wind-up), `phase`/`age`/`endAge` the tracked phase.
        /// </summary>
        public static bool Warn(int local,int target,int id,NativeHulkMoves.ChargePhase phase,float age,float endAge,Vector3 hulk,float chestHeight,Vector3 velocity,Vector2 heading,Vector3 pilot,int frame,out Warning w)
        {
            w=default;if(local<=0||target!=local)return false;
            float fade=phase==NativeHulkMoves.ChargePhase.Wind||phase==NativeHulkMoves.ChargePhase.Run?1:phase==NativeHulkMoves.ChargePhase.Stop&&endAge<.45f?1-endAge/.45f:0;if(fade<=0)return false;
            bool running=phase!=NativeHulkMoves.ChargePhase.Wind;float dx=hulk.X-pilot.X,dz=hulk.Z-pilot.Z,d=(float)Math.Sqrt(dx*dx+dz*dz);
            var toward=d>1e-4f?new Vector3(dx/d,0,dz/d):new Vector3(heading.X,0,heading.Y);
            // Closing: the Hulk's speed toward the pilot (a passing charge closes less and less).
            float closing=-(velocity.X*toward.X+velocity.Z*toward.Z);
            float lateral=Lateral(hulk,heading,pilot,out float along);
            w=new Warning{Active=true,Running=running,Id=id,Target=target,Hulk=hulk,Chest=hulk+new Vector3(0,chestHeight,0),Toward=toward,Heading=heading,Distance=d,Closing=closing,Lateral=lateral,InLane=lateral<=LaneHalf&&along>-.5f,
                Urgency=Urgency(d,phase==NativeHulkMoves.ChargePhase.Stop?0:closing,running&&phase!=NativeHulkMoves.ChargePhase.Stop)*fade,Age=phase==NativeHulkMoves.ChargePhase.Stop?endAge:age,Fade=fade,Frame=frame};
            return true;
        }
        /// <summary>The most urgent of two warnings (several Hulks after one pilot).</summary>
        public static bool Over(in Warning a,in Warning b)=>a.Active&&(!b.Active||a.Urgency>b.Urgency);
        /// <summary>The gentle shake the hunted pilot's view feels at each of the charging Hulk's footfalls (peak, m; Settings ·
        /// camera shake gates it, Reduced effects halves it), and the rumble's gain (0..1).</summary>
        public static float CloseShake(float urgency)=>urgency<=0?0:.003f+.009f*urgency*urgency;
        public static float RumbleGain(float urgency)=>urgency<=0?0:.3f+.7f*Clamp01(urgency);

        // ---- the HUD marker (viewport units: x 0 left .. 1 right, y 0 bottom .. 1 top) ----
        /// <summary>The crosshair's clear circle (a fraction of the screen height round the centre): no marker is drawn in it.</summary>
        public const float AimClear=.16f;
        /// <summary>The off-screen arrows ride an ellipse this far in from the screen's edges (fractions of the height).</summary>
        public const float EdgeInX=.08f,EdgeInY=.09f;
        public struct Marker
        {
            /// <summary>On screen: a bracket round the Hulk centred at (X, Y), `Size` half wide and <see cref="BracketTall"/> times that
            /// half tall (height units), only the corners in `Corners` (bit i: corner i, x then y sign) drawn, the rest being inside the
            /// crosshair's clear circle; with none left, `Ring`: two short arcs just outside that circle. Off screen: an arrow at (X, Y)
            /// pointing along `Angle` (radians, 0 right, counter-clockwise), `Chevrons` deep, `Size` long.</summary>
            public bool OnScreen,Ring;public float X,Y,Angle,Size,Alpha,Pulse;public int Chevrons,Corners;
        }
        /// <summary>The bracket's height over its width, and each corner's legs (a share of the half width, drawn inward).</summary>
        public const float BracketTall=1.25f,Leg=.42f;
        /// <summary>How far a bracket over the crosshair may open out of its clear circle (times its size round the body).</summary>
        public const float GrowMost=2.2f;
        private static bool Clear(in Marker m,float aspect){for(int i=0;i<4;i++)if(Corner(m,i,aspect).Length()-m.Size*Leg<AimClear*1.04f)return false;return true;}
        /// <summary>The bracket's corner i (x then y sign) relative to the screen centre (height units) on a screen `aspect` wide.</summary>
        public static Vector2 Corner(in Marker m,int i,float aspect)=>new Vector2((m.X-.5f)*aspect+((i&1)==0?-m.Size:m.Size),m.Y-.5f+((i&2)==0?-m.Size*BracketTall:m.Size*BracketTall));
        /// <summary>
        /// The marker for a Hulk seen at viewport (vx, vy) at view depth `vz` (negative behind the camera), `half` its projected half
        /// height (height units), on a screen `aspect` wide per its height. `time` drives the pulse (none with `reduced`); the
        /// urgency its size, brightness, pulse rate and chevrons. Nothing is ever drawn inside <see cref="AimClear"/>.
        /// </summary>
        public static Marker Layout(float vx,float vy,float vz,float half,float aspect,float urgency,float time,bool reduced)
        {
            var m=new Marker();float u=Clamp01(urgency);aspect=Math.Max(.5f,aspect);
            float rate=1.1f+3.4f*u,wave=reduced?1:.75f+.25f*(float)Math.Cos(time*rate*2*Math.PI);m.Pulse=wave;m.Chevrons=1+(u>=.5f?1:0)+(u>=.8f?1:0);
            bool on=vz>0&&vx>=.04f&&vx<=.96f&&vy>=.06f&&vy<=.94f;
            float cx=aspect*.5f,px=vx*aspect,py=vy;
            if(on)
            {
                // A bracket round the body (at least a readable size). Over the crosshair it opens out of the clear circle (up to
                // GrowMost times its size); a corner still over it is left out, and with none left two arcs flank the circle instead.
                m.OnScreen=true;m.X=vx;m.Y=vy;m.Size=Math.Max(.03f,Math.Min(.3f,half*.62f));m.Alpha=(.55f+.45f*u)*wave;
                float most=m.Size*GrowMost;for(int it=0;it<16&&!Clear(m,aspect)&&m.Size<most;it++)m.Size=Math.Min(most,m.Size*1.1f);
                for(int i=0;i<4;i++)if(Corner(m,i,aspect).Length()-m.Size*Leg>=AimClear*1.04f)m.Corners|=1<<i;
                m.Ring=m.Corners==0;return m;
            }
            // Behind the camera (the projection mirrored): the arrow points down (turn round), toward the side it is on.
            float ex=px-cx,ey=py-.5f;if(vz<=0){ex=-ex;ey=-(Math.Abs(ey)+.4f);}
            if(Math.Abs(ex)+Math.Abs(ey)<1e-5f)ey=-1;
            float a=(float)Math.Atan2(ey,ex),rx=cx-EdgeInX,ry=.5f-EdgeInY;
            m.Angle=a;m.X=(cx+(float)Math.Cos(a)*rx)/aspect;m.Y=.5f+(float)Math.Sin(a)*ry;m.Size=.024f+.022f*u;m.Alpha=(.6f+.4f*u)*wave;return m;
        }
        /// <summary>The marker's nearest drawn part to the screen centre (height units) on a screen `aspect` wide: the nearest drawn
        /// corner, the ring's arcs, or the arrow (its chevrons trail back from the tip along its angle).</summary>
        public static float FromCenter(in Marker m,float aspect)
        {
            if(!m.OnScreen){float x=(m.X-.5f)*aspect,y=m.Y-.5f;return (float)Math.Sqrt(x*x+y*y)-m.Size*(1+.9f*Math.Max(0,m.Chevrons-1))-m.Size;}
            if(m.Ring)return AimClear*1.12f;
            float best=float.MaxValue;for(int i=0;i<4;i++)if((m.Corners&(1<<i))!=0)best=Math.Min(best,Corner(m,i,aspect).Length()-m.Size*Leg);return best;
        }

        // ================================================================================================================
        // The tell (the wind-up) and the run's marks
        // ================================================================================================================
        /// <summary>The ground streak along the locked heading at the lock (`since` s after it): its length (m, swept out quickly),
        /// and opacity (held, then gone by 1.2 s).</summary>
        public static bool LockStreak(float since,out float length,out float alpha)
        {
            length=alpha=0;if(since<0||since>1.2f)return false;length=StreakLength*Sm(since/.2f);alpha=since<.5f?1:1-Sm((since-.5f)/.7f);return true;
        }
        public const float StreakLength=12;
        /// <summary>The wind-up's build (0..1 over it): the dust, the scrape and the glow gather toward the lock.</summary>
        public static float Build(float age)=>age<0?0:Clamp01(age/NativeHulkMoves.ChargeWind);
        /// <summary>The wind-up's foot scrape (s into it): the right foot dragged back from .24 to .54 s (NativeHordeGait's scrape).</summary>
        public const float ScrapeFrom=.24f,ScrapeTo=.54f;
        /// <summary>The stops' impacts (the effects renderer's look, held where it struck): a pilot struck by the shoulder, a solid
        /// it could not break.</summary>
        public static readonly NativeZombieFx.Impact HitImpact=new NativeZombieFx.Impact(1.7f,1.3f,.42f,.7f,6,1.8f,12,1.3f,6,1.6f,5,2,false,false,true);
        public static readonly NativeZombieFx.Impact SolidImpact=new NativeZombieFx.Impact(2.4f,1.7f,.5f,.95f,9,2.6f,16,1.7f,10,2.2f,8,4,false,false,true);
        /// <summary>A crash through a wall on the run (`since` s after the break): the body's flinch weight (0..1, sharp and short).</summary>
        public static float Flinch(float since)=>since<0||since>.45f?0:Sm(since/.035f)*(1-Sm((since-.06f)/.36f));
    }
}
