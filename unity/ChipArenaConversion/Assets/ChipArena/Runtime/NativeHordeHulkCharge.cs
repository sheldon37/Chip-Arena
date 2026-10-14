using System;
using System.Collections.Generic;
using UnityEngine;
using NVector=System.Numerics.Vector3;
using NVector2=System.Numerics.Vector2;

namespace ChipArena.UnityConversion
{
    // The Hulk's charge at a pilot and its blows, drawn (owner 2026-10-14, Windows test build: "The hulk needs to punch and charge
    // at the player too, not just the wall. The charge is avoidable if they move out of the way in time. (Hulk can collide and
    // break walls too.) They literally can charge from across the map to the player breaking the walls if they are hunting them
    // and 'know' where they are."). Presentation only, on the contract v2 state (docs/SIREN-ZOMBIES.md 10.5, Core/NativeHulkCharge,
    // Core/NativeHulkMoves): host, guests and the killcam draw the same from the replicated add, its breach records and the
    // charge's events; no hit, collider, timing, damage, movement or network state is read back or changed.
    //  - The tell (the wind-up, so a pilot can read the lane): the seam glow gathering at the chest as it rears and bellows (the
    //    jaw), dust off the right foot it scrapes back and a furrow behind it, the braced feet's dust, a faint line along its
    //    facing; at the lock a ground streak sweeps out along the locked heading with the lane's edges and chevrons.
    //  - The run: the heavy gait's own footfalls (dust, cracks, a thump of shake), streaks off the lowered shoulder; walls in its
    //    lane shake and dust a moment before it reaches them (their drawn copy rattled along the heading, their colliders never
    //    moved; on the revamp through the block dressing); a wall it crashes through bursts on the breach's own effects, a puff of
    //    dust and grit off its shoulder, a shake round it, and it runs on.
    //  - The ends: a pilot struck (an impact where the shoulder met them, the pilot's burst, a kick for the struck view), a solid
    //    stop (a heavier impact and shake), spent (skid furrows and dust).
    //  - The warning, for the hunted pilot only (the charge's target seat): HulkWarning (the HUD's marker, Runtime/
    //    NativeHulkWarningHud.cs; the audio's rumble and footfalls through walls, NativeHulkChargeAudio.cs) and a gentle shake at
    //    each of its footfalls as it closes. Settings · camera shake gates the shakes (Reduced effects halves them); Reduced
    //    effects thins the effects as everywhere.
    //  - The punch and the swipe (a heavy add's reported move): the striking fist's glow through the wind-up, its trail over the
    //    drive (straight back along the facing; the arm's sweep), the burst at the fist on the strike, the swipe's crescent at the
    //    body's scale, and for a pilot inside the proposed volume a burst at their chest and a kick of their view (the presentation's
    //    guess, as the smash's disc already does; the authority's own test decides the real hit).
    //  - The Dev Sandbox gallery's fourth Hulk: a punch and a swipe at a hardlight dummy, a turn to the local pilot, the wind-up
    //    and lock at them, a cross-map charge through two walls of its own, and alternately a pilot hit (a second dummy) and a solid
    //    stop (a dark pillar); the warning plays for the local pilot.
    // Fixed pools and cached lists: no allocation per frame (the lane's wall list is built once per arena).
    public sealed partial class ArenaPresentation
    {
        // ================================================================================================================
        // The frame's charges (live adds and the gallery), collected while posing, drawn in DevEffects
        // ================================================================================================================
        private struct HordeChargeView
        {
            public int Id,Target;public NativeHulkMoves.ChargePhase Phase;public NativeHulkMoves.ChargeEnd How;public float Age,EndAge,BreakAge;
            public Vector3 Floor,Velocity,FootL,FootR,LockAt;public Vector2 Heading;public uint Seed;public bool Gallery,Landed,Broke,Locked;
        }
        private readonly HordeChargeView[] hordeCharges=new HordeChargeView[8];private int hordeChargeCount;
        private HordeChargeView hordeChargeNext;private bool hordeChargePending;
        /// <summary>The local pilot's charge warning this frame (the most urgent charge hunting them; Active false: none). Read by the
        /// HUD and the audio, which ignore one older than a couple of frames.</summary>
        public NativeHulkCharge.Warning HulkWarning{get;private set;}
        /// <summary>HordeHulkInput (and the gallery): this add is charging; the view is completed once its gait has posed it.</summary>
        private void HordeChargeBegin(int id,in NativeHulkCharge.State c,NativeHulkMoves.ChargePhase ph,float age,float endAge,Vector3 floor,Vector3 vel,float headingYaw,uint seed,bool gallery,float breakAge)
        {
            var h=NativeHulkCharge.Heading(headingYaw);
            hordeChargeNext=new HordeChargeView{Id=id,Target=c.Target,Phase=ph,How=c.How,Age=age,EndAge=endAge,Floor=floor,Velocity=vel,Heading=new Vector2(h.X,h.Y),Seed=seed,Gallery=gallery,
                Broke=!float.IsNaN(breakAge),BreakAge=breakAge,Locked=c.Locked,LockAt=c.Locked?new Vector3(c.LockAt.X,c.LockAt.Y,c.LockAt.Z):floor};
            hordeChargePending=true;
        }
        /// <summary>After the gait posed a charging add: its feet and footfalls complete the view; the walls ahead on its lane start
        /// to shake (`now` the clock the walls are drawn on).</summary>
        private void HordeHulkPosed(HordeKindState s,float now)
        {
            if(!hordeChargePending)return;hordeChargePending=false;var v=hordeChargeNext;var g=s.Gait;float sc=s.Scale;
            var l=g.FootWorld[0]*sc;var r=g.FootWorld[1]*sc;v.FootL=new Vector3(l.X,v.Floor.y,l.Z);v.FootR=new Vector3(r.X,v.Floor.y,r.Z);v.Landed=(g.Landed&3)!=0;
            if(hordeChargeCount<hordeCharges.Length)hordeCharges[hordeChargeCount++]=v;
            if(v.Phase==NativeHulkMoves.ChargePhase.Run)HordeLaneAhead(v,now);
        }
        private void ClearHordeCharges(){hordeChargeCount=0;hordeChargePending=false;HulkWarning=default;ClearHordeTrembles();}

        // ================================================================================================================
        // The lane: the walls a charge would break (the contract's: Ward Zero's interior wall blocks, the breach panels), shaking
        // and dusting a moment before it reaches them
        // ================================================================================================================
        private struct HordeLaneWall{public string Block;public Renderer Renderer;public MeshFilter Filter;public Material[] Materials;public Vector3 Center,Half;public float Yaw;public int Layer;}
        private readonly List<HordeLaneWall> hordeLaneWalls=new List<HordeLaneWall>(64);private NativeArena hordeLaneArena;private int hordeLaneGeneration=-1;
        private readonly HordeLaneWall[] hordeGalleryLane=new HordeLaneWall[2];private int hordeGalleryLaneCount;
        private struct HordeTremble{public bool Live,Hidden,Dressed,Gallery;public int Wall;public float ContactAt,Seen,Born;public Vector3 Push;public uint Seed;}
        private readonly HordeTremble[] hordeTrembles=new HordeTremble[8];
        private static HordeLaneWall LaneWall(GameObject go,string block,Vector3 center,Vector3 half,float yaw)
        {
            var r=go.GetComponent<MeshRenderer>();var f=go.GetComponent<MeshFilter>();
            return new HordeLaneWall{Block=block,Renderer=r,Filter=f,Materials=r!=null?r.sharedMaterials:null,Center=center,Half=half,Yaw=yaw,Layer=go.layer};
        }
        /// <summary>The arena's breakable walls for the lane (built once per arena and generation): its interior wall blocks the
        /// breach rule allows (NativeHulkBreach.Eligible) and its breach panels ("breach:&lt;id&gt;" segments).</summary>
        private void HordeLaneWallsFor(NativeArena arena)
        {
            if(arena==hordeLaneArena&&arena.Generation==hordeLaneGeneration)return;
            ClearHordeTrembles();hordeLaneWalls.Clear();hordeLaneArena=arena;hordeLaneGeneration=arena.Generation;if(arena==null)return;
            var map=arena.Map;string panel=NativeBreach.ObjectName("");
            for(int list=0;list<2;list++)
            {
                var props=list==0?arena.StaticBlocks:arena.Props;
                for(int i=0;i<props.Count;i++)
                {
                    var p=props[i];if(p==null)continue;var d=p.Definition;
                    if(list==0&&d!=null&&map!=null&&NativeHulkBreach.Eligible(map,d)){hordeLaneWalls.Add(LaneWall(p.gameObject,d.id,new Vector3(d.pos.x,d.pos.y,d.pos.z),new Vector3(d.size.x,d.size.y,d.size.z)*.5f,0));continue;}
                    if(p.gameObject.name.StartsWith(panel,StringComparison.Ordinal)){var r=p.GetComponent<MeshRenderer>();if(r==null)continue;var b=r.bounds;hordeLaneWalls.Add(LaneWall(p.gameObject,null,b.center,b.extents,0));}
                }
            }
        }
        private HordeLaneWall LaneWallAt(bool gallery,int i)=>gallery?hordeGalleryLane[i]:hordeLaneWalls[i];
        /// <summary>A running charge's lane ahead: each breakable wall it will meet within <see cref="NativeHulkCharge.TrembleLead"/>
        /// s starts (or keeps) shaking, timed to the predicted contact.</summary>
        private void HordeLaneAhead(in HordeChargeView v,float now)
        {
            if(hordeKindArena!=null)HordeLaneWallsFor(hordeKindArena);
            float speed=Mathf.Max(1.5f,new Vector2(v.Velocity.x,v.Velocity.z).magnitude),reach=speed*NativeHulkCharge.TrembleLead+.2f;
            var from=ToN(v.Floor);var hd=new NVector2(v.Heading.x,v.Heading.y);float radius=NativeHordeKinds.WorldRadius(NativeHordeKinds.Hulk),height=NativeHordeKinds.WorldHeight(NativeHordeKinds.Hulk);
            for(int g=0;g<2;g++)
            {
                int n=g==0?hordeLaneWalls.Count:hordeGalleryLaneCount;
                for(int i=0;i<n;i++)
                {
                    var w=LaneWallAt(g==1,i);if(w.Renderer==null)continue;
                    if(!NativeHulkCharge.LaneHit(from,hd,reach,radius,height,ToN(w.Center),w.Yaw,ToN(w.Half),out float dist))continue;
                    if(HordeWallBreached(w,now))continue;
                    HordeTrembleAt(g==1,i,now+dist/speed,now,new Vector3(v.Heading.x,0,v.Heading.y),v.Seed^(uint)(i*7919+g));
                }
            }
        }
        private void HordeTrembleAt(bool gallery,int wall,float contactAt,float now,Vector3 push,uint seed)
        {
            int free=-1;
            for(int i=0;i<hordeTrembles.Length;i++){ref var t=ref hordeTrembles[i];if(t.Live&&t.Gallery==gallery&&t.Wall==wall){t.ContactAt=contactAt;t.Seen=now;t.Push=push;return;}if(!t.Live&&free<0)free=i;}
            if(free<0)return;hordeTrembles[free]=new HordeTremble{Live=true,Gallery=gallery,Wall=wall,ContactAt=contactAt,Seen=now,Born=now,Push=push,Seed=seed};
        }
        /// <summary>Whether a breach is drawing that wall broken (from its break on): the breach owns its renderers then.</summary>
        private bool HordeWallBreached(in HordeLaneWall w,float now)
        {
            for(int i=0;i<hordeBreaches.Length;i++)
            {
                var b=hordeBreaches[i];if(!b.Live||now<b.BreakAt-.02f)continue;
                if(b.Dynamic?w.Block!=null&&string.Equals(b.Block,w.Block,StringComparison.Ordinal):b.Renderers!=null&&Array.IndexOf(b.Renderers,w.Renderer)>=0)return true;
            }
            return false;
        }
        private bool HordeBreachHides(Renderer r){for(int i=0;i<hordeBreaches.Length;i++){var b=hordeBreaches[i];if(b.Live&&b.Hidden&&b.Renderers!=null&&Array.IndexOf(b.Renderers,r)>=0)return true;}return false;}
        /// <summary>SourceWorld (HordeKindsEnd, after the breaches): the shaking walls drawn rattled along the heading (their renderer
        /// off meanwhile; on the revamp the block's dressing nudged), given back when the charge passes, stops or breaks them.</summary>
        private void HordeTremblesDraw(float now)
        {
            for(int i=0;i<hordeTrembles.Length;i++)
            {
                ref var t=ref hordeTrembles[i];if(!t.Live)continue;
                if(t.Gallery?t.Wall>=hordeGalleryLaneCount:t.Wall>=hordeLaneWalls.Count){t=default;continue;}
                var w=LaneWallAt(t.Gallery,t.Wall);float to=t.ContactAt-now;
                if(w.Renderer==null||now-t.Seen>.2f||to<-.15f||HordeWallBreached(w,now)){HordeTrembleRelease(ref t,w);continue;}
                var by=t.Push*NativeHulkCharge.Tremble(to,now,t.Seed);
                if(w.Renderer.forceRenderingOff&&w.Block!=null){t.Dressed=true;NudgeWardBlock(w.Block,by);continue;}
                if(w.Filter==null||w.Filter.sharedMesh==null||w.Materials==null)continue;
                if(!t.Hidden){if(!w.Renderer.enabled)continue;w.Renderer.enabled=false;t.Hidden=true;}
                var m=Matrix4x4.Translate(by)*w.Filter.transform.localToWorldMatrix;var mesh=w.Filter.sharedMesh;
                for(int s=0;s<w.Materials.Length&&s<mesh.subMeshCount;s++)if(w.Materials[s]!=null)Graphics.DrawMesh(mesh,m,w.Materials[s],w.Layer,null,s,null,w.Renderer.shadowCastingMode,w.Renderer.receiveShadows);
            }
        }
        private void HordeTrembleRelease(ref HordeTremble t,in HordeLaneWall w)
        {
            if(t.Dressed&&w.Block!=null)NudgeWardBlock(w.Block,Vector3.zero);
            // A wall the breach now draws broken stays hidden (the breach gives it back at its restore).
            if(t.Hidden&&w.Renderer!=null&&!HordeBreachHides(w.Renderer))w.Renderer.enabled=true;
            t=default;
        }
        private void ClearHordeTrembles()
        {
            for(int i=0;i<hordeTrembles.Length;i++){ref var t=ref hordeTrembles[i];if(!t.Live)continue;bool ok=t.Gallery?t.Wall<hordeGalleryLaneCount:t.Wall<hordeLaneWalls.Count;if(ok)HordeTrembleRelease(ref t,LaneWallAt(t.Gallery,t.Wall));else t=default;}
        }
        // The shaking walls' dust and grit (DevEffects): dust sifting off the top edge and out of the near face, grit falling, the
        // near face's cracks starting where the shoulder will meet it.
        private static readonly Color ChargeDust=NativeVfxDraw.Linear(NativeZombieFx.DustHex),ChargeDustDark=NativeVfxDraw.Linear(NativeZombieFx.DustDarkHex),ChargeRock=NativeVfxDraw.Linear(0x4b4654);
        private void HordeTremblesFx(float now,Vector3 eye)
        {
            if(vfx==null)return;var z=zombieFx;
            for(int i=0;i<hordeTrembles.Length;i++)
            {
                var t=hordeTrembles[i];if(!t.Live)continue;var w=LaneWallAt(t.Gallery,t.Wall);float to=t.ContactAt-now;if(to>NativeHulkCharge.TrembleLead||to<-.1f)continue;
                if(z!=null&&!z.Within(Vector3.Distance(eye,w.Center),true))continue;
                float k=Mathf.SmoothStep(0,1,1-Mathf.Max(0,to)/NativeHulkCharge.TrembleLead),age=now-t.Born;
                // The wall's frame: along its long side, its thin side toward the Hulk.
                float yr=w.Yaw*Mathf.Deg2Rad;var ax=new Vector3(Mathf.Cos(yr),0,-Mathf.Sin(yr));var az=new Vector3(Mathf.Sin(yr),0,Mathf.Cos(yr));
                bool longX=w.Half.x>=w.Half.z;var along=longX?ax:az;var across=longX?az:ax;float halfLen=longX?w.Half.x:w.Half.z,thick=longX?w.Half.z:w.Half.x;
                if(Vector3.Dot(across,t.Push)>0)across=-across;
                float top=w.Center.y+w.Half.y,floor=w.Center.y-w.Half.y;int n=LowGraphics?4:8;
                for(int d=0;d<n;d++)
                {
                    float u=NativeHordeFx.Hash(t.Seed,d,61)*2-1,ph=Mathf.Repeat(age*1.6f+NativeHordeFx.Hash(t.Seed,d,62),1),x=u*Mathf.Min(halfLen,2.4f);
                    var p=w.Center+along*x+Vector3.up*(w.Half.y-.05f-ph*1.4f)+across*(thick+.08f+ph*.25f);p.y=Mathf.Max(floor+.2f,p.y);
                    vfx.Vapor(p,d%3==0?ChargeDustDark:ChargeDust,.5f*k*(1-ph),.35f+.6f*ph+.3f*k,ph,(float)((t.Seed+d*37)%8191),floor,0);
                    // grit off the top edge
                    float gph=Mathf.Repeat(age*2.3f+NativeHordeFx.Hash(t.Seed,d,63),1),gy=top-.05f-5.5f*gph*gph*.5f;
                    if(gy>floor&&k>.2f)vfx.Rubble(w.Center+along*(x*.8f)+across*(thick+.05f+.3f*gph)+Vector3.up*(gy-w.Center.y),Quaternion.Euler(gph*400,d*40,gph*170),.035f+.03f*NativeHordeFx.Hash(t.Seed,d,64),(int)(t.Seed%977)+d,ChargeRock,Color.clear);
                }
            }
        }

        // ================================================================================================================
        // DevEffects: the warning, the shakes, the tell, the run's and the ends' effects
        // ================================================================================================================
        private static readonly Color ChargeFlash=new Color(1.4f,1.25f,1.1f,0);
        /// <summary>DrawHordeKindFx (after HordeLocal): this frame's charges into the warning, the shakes and their effects.</summary>
        private void HordeChargesFx(NativeFxDraw draw,NativeArena arena,float now,Vector3 eye,Vector3 right,Vector3 up,bool low)
        {
            var best=default(NativeHulkCharge.Warning);int frame=Time.frameCount;int local=arena.LocalId;
            for(int i=0;i<hordeChargeCount;i++)
            {
                var v=hordeCharges[i];
                if(hordeLocalKnown&&NativeHulkCharge.Warn(local,v.Target,v.Id,v.Phase,v.Age,v.EndAge,ToN(v.Floor),2.2f,ToN(v.Velocity),new NVector2(v.Heading.x,v.Heading.y),ToN(hordeLocalAt),frame,out var w))
                {
                    if(NativeHulkCharge.Over(w,best))best=w;
                    // The hunted view feels each of its footfalls as it closes (gently; through walls).
                    if(v.Landed&&v.Phase==NativeHulkMoves.ChargePhase.Run)NativeHordeFx.AddShake(hordeShakes,ToN(hordeLocalAt),now,NativeHulkCharge.CloseShake(w.Urgency),6,0,v.Seed^(uint)frame*2654435761u,.32f,0);
                }
                HordeChargeDraw(draw,arena,v,now,eye,right,up,low);
            }
            HulkWarning=best;hordeChargeCount=0;
            HordeTremblesFx(now,eye);HordeDummiesDraw(draw,now,eye,right,up);
        }
        private void HordeChargesSkip(){hordeChargeCount=0;hordeChargePending=false;HulkWarning=default;}
        // A band on the floor from `from` along `fwd` (`len` m, the body's width), fading toward its far end, its edges brighter.
        private void HordeLaneBand(NativeFxDraw draw,Vector3 from,Vector3 fwd,Vector3 side,float half,float len,float alpha,Color seam,float chevrons)
        {
            if(len<.05f||alpha<=.01f)return;var lift=Vector3.up*.02f;int seg=6;
            for(int k=0;k<seg;k++)
            {
                float a=k/(float)seg,b=(k+1)/(float)seg,fa=alpha*(1-a*.85f),fb=alpha*(1-b*.85f);Vector3 p0=from+fwd*len*a+lift,p1=from+fwd*len*b+lift;
                draw.Quad(draw.Effects,p0-side*half,p1-side*half,p1+side*half,p0+side*half,Alpha(seam,.16f*(fa+fb)*.5f),NativeFxDraw.Authored+NativeFxDraw.Additive);
                for(int e=-1;e<=1;e+=2){Vector3 q0=p0+side*half*e,q1=p1+side*half*e,o=side*.05f*e;draw.Quad(draw.Effects,q0-o,q1-o,q1+o,q0+o,Alpha(seam,.75f*(fa+fb)*.5f),NativeFxDraw.Authored+NativeFxDraw.Additive);}
            }
            // Chevrons along it, lit one after another away from the Hulk.
            for(int c=0;c<3&&chevrons>0;c++)
            {
                float at=len*(.25f+.25f*c),k=Mathf.Clamp01(chevrons*3-c)*alpha*(1-at/Mathf.Max(1,len)*.6f);if(k<=.01f)continue;Vector3 tip=from+fwd*(at+.45f)+lift,back=from+fwd*at+lift;
                for(int e=-1;e<=1;e+=2){Vector3 wing=back+side*half*.55f*e,o=fwd*.08f;draw.Quad(draw.Effects,wing-o,tip-o,tip+o,wing+o,Alpha(seam,.9f*k),NativeFxDraw.Authored+NativeFxDraw.Additive);}
            }
        }
        // A dark furrow on the floor from a to b (the scrape, the skid).
        private static void HordeFurrow(NativeFxDraw draw,Vector3 a,Vector3 b,float width,float alpha)
        {
            var d=b-a;d.y=0;if(d.sqrMagnitude<1e-4f||alpha<=.01f)return;var s=Vector3.Cross(Vector3.up,d.normalized)*width;var lift=Vector3.up*.01f;
            draw.Quad(draw.Effects,a-s+lift,b-s*.4f+lift,b+s*.4f+lift,a+s+lift,new Color(.03f,.025f,.035f,alpha),NativeFxDraw.Authored);
        }
        private void HordeChargeDraw(NativeFxDraw draw,NativeArena arena,in HordeChargeView v,float now,Vector3 eye,Vector3 right,Vector3 up,bool low)
        {
            var z=zombieFx;float dist=Vector3.Distance(eye,v.Floor);bool near=z!=null&&z.Within(dist,true);if(dist>90)return;
            var seam=NativeArena.ColorOf(NativeHordeKinds.Seam[NativeHordeKinds.Hulk]);var fwd=new Vector3(v.Heading.x,0,v.Heading.y);var side=new Vector3(fwd.z,0,-fwd.x);
            float R=NativeHordeKinds.WorldRadius(NativeHordeKinds.Hulk);var chest=v.Floor+Vector3.up*2.25f+fwd*.35f;
            if(v.Phase==NativeHulkMoves.ChargePhase.Wind)
            {
                float b=NativeHulkCharge.Build(v.Age);
                // The seam's glow gathering at the chest as it rears and bellows.
                draw.Sprite(draw.Effects,chest,right,up,.7f+1.1f*b,Alpha(seam,.15f+.32f*b*b),NativeFxDraw.Authored+NativeFxDraw.Additive);
                // The right foot scraped back: dust off it and a furrow behind it; the braced feet's dust as it coils.
                float s=v.Age-NativeHulkCharge.ScrapeFrom;
                if(s>=0){float k=Mathf.Clamp01(s/(NativeHulkCharge.ScrapeTo-NativeHulkCharge.ScrapeFrom));HordeFurrow(draw,v.FootR+fwd*(.34f*(1-k)+.05f),v.FootR+fwd*.05f-fwd*.1f*k,.09f,.55f*Mathf.Min(1,k*3));if(near&&s<.9f)z.Footfall(v.FootR,s,1.3f,v.Seed^0x5cu);}
                if(near&&v.Age>=.45f)z.Footfall(v.FootL,v.Age-.45f,.85f,v.Seed^0x5du);
                // A faint line along its facing, the lane-to-be.
                if(v.Age>=.4f){float k=Mathf.Clamp01((v.Age-.4f)/.5f);HordeLaneBand(draw,v.Floor+fwd*(R+.2f),fwd,side,R*.35f,NativeHulkCharge.StreakLength*.4f*k,.35f*k,seam,0);}
                return;
            }
            if(v.Phase==NativeHulkMoves.ChargePhase.Run)
            {
                // The lock: a streak swept out along the locked heading from where it stood, the lane's edges and its chevrons.
                if(v.Locked&&NativeHulkCharge.LockStreak(v.Age-NativeHulkMoves.ChargeWind,out float len,out float al))HordeLaneBand(draw,v.LockAt+fwd*(R*.5f),fwd,side,R,len,al,seam,Mathf.Clamp01((v.Age-NativeHulkMoves.ChargeWind)/.3f));
                // Streaks off the lowered shoulder (the run's speed).
                float speed=new Vector2(v.Velocity.x,v.Velocity.z).magnitude;
                if(speed>2&&!low)for(int k=0;k<3;k++){var p=v.Floor+Vector3.up*(1.25f+.3f*k)+side*(-.6f+.35f*k)-fwd*.2f;HordeStreak(draw,p,p-fwd*(.4f+.12f*speed),.025f,Alpha(seam,.2f),eye);}
                // A wall crashed through: dust and grit off the shoulder, a shake round it.
                if(v.Broke&&v.BreakAge>=0&&v.BreakAge<1.2f)
                {
                    float born=now-v.BreakAge;var sh=v.Floor+Vector3.up*2.1f+fwd*(R+.3f)-side*.4f;
                    if(v.BreakAge<.12f)NativeHordeFx.AddShake(hordeShakes,ToN(v.Floor),born,.07f,16,0,v.Seed^0x9e37u^(uint)(born*60),.45f,0);
                    if(near&&vfx!=null)for(int d=0;d<(low?4:9);d++){float u=v.BreakAge/1.2f,a=d*2.39996f,r=.25f+.9f*u;var p=sh+(side*Mathf.Cos(a)+Vector3.up*Mathf.Sin(a)*.7f)*r-fwd*(1.2f*v.BreakAge)+Vector3.up*(-.6f*u);vfx.Smoke(p,d%3==0?ChargeDustDark:ChargeDust,.8f*(1-u),.6f+1.4f*u,u,(float)((v.Seed+d*53)%8191),v.Floor.y);}
                    if(v.BreakAge<.1f)draw.Sprite(draw.Effects,sh,right,up,2.2f*(1-v.BreakAge/.1f),Alpha(ChargeFlash,.5f*(1-v.BreakAge/.1f)),NativeFxDraw.Authored+NativeFxDraw.Additive);
                }
                return;
            }
            if(v.Phase!=NativeHulkMoves.ChargePhase.Stop)return;
            float since=v.EndAge,ended=now-since;int ink=NativeHordeKinds.Seam[NativeHordeKinds.Hulk];
            if(v.How==NativeHulkMoves.ChargeEnd.Pilot)
            {
                var at=v.Floor+fwd*(R+.55f);HordeImpactAt(v.Id,at,ended,v.Seed^0x51u,NativeHulkCharge.HitImpact,ink);
                if(since<.12f)NativeHordeFx.AddShake(hordeShakes,ToN(at),ended,.08f,14,0,v.Seed^0x52u,.6f,0);
                // The struck pilot (whoever stands at the shoulder): a burst at their feet and a streak along the throw; the struck view kicked.
                HordeShove(draw,arena,at+fwd*.4f,1.5f,since,fwd,right,up);
                if(hordeLocalKnown&&since<.12f){var d=hordeLocalAt-at;d.y=0;if(d.magnitude<2.2f&&Mathf.Abs(hordeLocalAt.y-at.y)<3)NativeHordeFx.AddShake(hordeShakes,ToN(hordeLocalAt),ended,.12f,6,7,v.Seed^0x53u,.9f,5);}
                if(since<.1f)draw.Sprite(draw.Effects,at+Vector3.up*1.6f,right,up,2.6f*(1-since/.1f),Alpha(ChargeFlash,.7f*(1-since/.1f)),NativeFxDraw.Authored+NativeFxDraw.Additive);
            }
            else if(v.How==NativeHulkMoves.ChargeEnd.Solid)
            {
                var at=v.Floor+fwd*(R+.15f);HordeImpactAt(v.Id,at,ended,v.Seed^0x54u,NativeHulkCharge.SolidImpact,ink);
                if(since<.12f)NativeHordeFx.AddShake(hordeShakes,ToN(at),ended,.12f,18,0,v.Seed^0x55u,.8f,0);
                if(since<.12f)draw.Sprite(draw.Effects,at+Vector3.up*2f,right,up,3.2f*(1-since/.12f),Alpha(ChargeFlash,.8f*(1-since/.12f)),NativeFxDraw.Authored+NativeFxDraw.Additive);
            }
            else if(v.How==NativeHulkMoves.ChargeEnd.Spent)
            {
                // Skid furrows behind both feet, dust kicked off them as it brakes.
                float k=Mathf.Clamp01(since/.35f),fade=1-Mathf.Clamp01((since-1.2f)/.6f);
                HordeFurrow(draw,v.FootL-fwd*(.3f+1.6f*k),v.FootL,.11f,.5f*fade);HordeFurrow(draw,v.FootR-fwd*(.3f+1.4f*k),v.FootR,.11f,.5f*fade);
                if(near){z.Footfall(v.FootL,since,1.1f,v.Seed^0x56u);if(since>=.2f)z.Footfall(v.FootR,since-.2f,1.1f,v.Seed^0x57u);}
            }
        }

        // ================================================================================================================
        // The punch and the swipe (a heavy add's reported move): trail, burst, the struck pilot
        // ================================================================================================================
        /// <summary>DrawHordeKindFx's lunge and swipe for a heavy kind (the reported punch and swipe).</summary>
        private void HordeHulkBlowFx(NativeFxDraw draw,NativeArena arena,in HordeJob j,float a,float H,Vector3 fwd,Vector3 side,Vector3 eye,Vector3 right,Vector3 up,bool half,float now)
        {
            var seam=NativeArena.ColorOf(NativeHordeKinds.Seam[j.Kind]);bool punch=j.Attack==NativeHordeKinds.Attack.Lunge;int arm=j.Side<0?-1:1;var hand=arm>0?j.HandR:j.HandL;float sc=j.Scale;
            // The striking fist glows through the wind-up (the telegraph), brightest at the strike.
            float cock=a<H?Mathf.Clamp01(a/(H*.8f)):Mathf.Max(0,1-(a-H)/.18f);
            if(cock>0)draw.Sprite(draw.Effects,hand,right,up,.3f*sc*cock,Alpha(seam,.75f*cock),NativeFxDraw.Authored+NativeFxDraw.Additive);
            // Its trail over the drive: straight back along the facing (the punch), along the sweep behind the arm (the swipe).
            if(a>=H-.16f&&a<H+.14f)
            {
                float k=a<H?Mathf.SmoothStep(0,1,(a-(H-.16f))/.16f):1-(a-H)/.14f;
                // (The swipe sweeps from the arm's side across the facing: the trail lies back toward that side.)
                var back=punch?-fwd:(side*arm*.85f-fwd*.35f).normalized;float len=(punch?1.5f:1.8f)*sc*.55f*k;
                for(int i=0;i<(half?2:3);i++){var o=(punch?side*((i-1)*.1f):fwd*((i-1)*.12f))*sc+Vector3.up*((i-1)*.04f*sc);HordeStreak(draw,hand+o,hand+o+back*len*(1-.2f*Mathf.Abs(i-1)),.045f*sc*.55f,Alpha(seam,.6f*k*(i==1?1:.6f)),eye);}
            }
            // The swipe's crescent, at the body's scale.
            if(!punch&&NativeHordeFx.Slash(a,H,out float sweep,out float fade)){HordeSlashMatrix(j.Root+Vector3.up*(.05f*sc),j.PresentedYaw,arm,sc);draw.Crescent(draw.Effects,hordeSlash,sweep,fade,new Color(.02f,.01f,.03f),seam);}
            if(a<H||a>H+.4f)return;float s=a-H,kk=1-s/.4f;
            // The burst at the fist on the strike: a flash, a star of streaks round the blow, the air thrown ahead.
            if(s<.12f)draw.Sprite(draw.Effects,hand,right,up,(.9f+3f*s)*sc*.6f,Alpha(ChargeFlash,.85f*(1-s/.12f)),NativeFxDraw.Authored+NativeFxDraw.Additive);
            for(int i=0;i<(half?4:7);i++)
            {
                float ang=i*(6.2832f/7)+NativeHordeKinds.Hash(j.Seed,i)*.5f;var d=(side*Mathf.Cos(ang)+Vector3.up*Mathf.Sin(ang)).normalized;var p0=hand+d*(.12f+1.4f*s)*sc*.5f+fwd*(punch?.25f*s*sc:0);
                HordeStreak(draw,p0,p0+d*(.22f+.5f*(1-s))*sc*.45f,.025f*sc*.55f,Alpha(seam,.75f*kk*kk),eye);
            }
            if(s<.3f)draw.Ring(draw.Effects,new Vector3(hand.x,j.Ground+.02f,hand.z)+fwd*.3f,.2f+s*3*sc*.5f,.35f+s*4*sc*.5f,Alpha(seam,.35f*(1-s/.3f)),NativeFxDraw.Authored+NativeFxDraw.Additive);
            // The pilots inside the proposed volume: a burst at the chest, a streak along the shove; the struck view kicked.
            var list=arena.Pilots;float yr=j.Yaw*Mathf.Deg2Rad;Vector3 f2=new Vector3(Mathf.Sin(yr),0,Mathf.Cos(yr)),r2=new Vector3(f2.z,0,-f2.x);
            for(int i=0;i<list.Count;i++)
            {
                var p=list[i];if(p.Hp<=0||p.Body==null)continue;var d=p.Body.position-j.Root;float x=Vector3.Dot(d,r2),zf=Vector3.Dot(d,f2),dy=p.Body.position.y-.9f-j.Ground;
                if(punch?!NativeHulkMoves.InPunch(x,zf,dy,.45f):!NativeHulkMoves.InSwipe(x,zf,dy,.45f))continue;
                // (The swipe throws across the facing, away from the swinging arm's side.)
                var shove=punch?f2:(-r2*arm*.9f+f2*.4f).normalized;var c=p.Body.position+Vector3.up*.3f;
                if(s<.1f)draw.Sprite(draw.Effects,c,right,up,1.4f*(1-s/.1f),Alpha(ChargeFlash,.7f*(1-s/.1f)),NativeFxDraw.Authored+NativeFxDraw.Additive);
                for(int k=0;k<3;k++){var a0=c+Vector3.up*(-.35f+.35f*k)-shove*(.15f+s*2.2f);HordeStreak(draw,a0,a0-shove*(.8f*kk),.03f,Alpha(new Color(1,.92f,.82f),.45f*kk),eye);}
                if(p.Seat.id==arena.LocalId&&s<.12f)NativeHordeFx.AddShake(hordeShakes,ToN(p.Body.position),now-s,punch?.07f:.06f,6,punch?5:2.5f,j.Seed^(uint)j.Id^0x77u,.6f,punch?2:-6*arm);
            }
        }

        // ================================================================================================================
        // The Dev Sandbox gallery's charge Hulk (presentation only: a script, no authority add)
        // ================================================================================================================
        public const float GalleryChargeLoop=32;
        private const float GcPunch=1,GcSwipe=3.4f,GcTurn=6,GcWind=7.4f,GcWall0=12,GcWall1=22,GcDummyGap=4.5f;
        private CapturedAnimation.Player galleryChargeRig;private HordeKindState galleryChargeState;private float galleryChargeLast=float.NaN;
        private readonly GameObject[] galleryChargeWalls=new GameObject[2];private readonly PveEntry[] galleryChargeEntries=new PveEntry[2];private Vector3 galleryChargeMe;private bool galleryChargePlaced;
        private struct HordeDummy{public bool Live,Pillar;public Vector3 At,Push;public float HitAt;}
        private readonly HordeDummy[] hordeDummies=new HordeDummy[2];
        /// <summary>The run's distance (m from the lock) `t` s after it (6 m/s, slowed at each wall it crashes through) and its speed.</summary>
        private static float GalleryRun(float t,float c0,float c1,out float speed)
        {
            float v=NativeHulkMoves.ChargeSpeed,sl=NativeHulkMoves.ChargeSlow,dur=NativeHulkMoves.ChargeSlowFor;
            // The contact times (s into the run) at the walls' near faces c0 and c1 (m).
            float t0=c0/v,t1=t0+dur+(c1-c0-v*sl*dur)/v;
            if(t<t0){speed=v;return v*t;}
            if(t<t0+dur){speed=v*sl;return c0+v*sl*(t-t0);}
            if(t<t1){speed=v;return c0+v*sl*dur+v*(t-t0-dur);}
            if(t<t1+dur){speed=v*sl;return c1+v*sl*(t-t1);}
            speed=v;return c1+v*sl*dur+v*(t-t1-dur);
        }
        private static float GalleryContact(float c0,float c1,int k){float v=NativeHulkMoves.ChargeSpeed,t0=c0/v;return k==0?t0:t0+NativeHulkMoves.ChargeSlowFor+(c1-c0-v*NativeHulkMoves.ChargeSlow*NativeHulkMoves.ChargeSlowFor)/v;}
        /// <summary>The gallery's fourth Hulk, off to the local pilot's left: a punch and a swipe at a hardlight dummy, a turn to the
        /// pilot, the wind-up and the lock at them, a cross-map charge through two walls of its own, then a pilot hit or a solid stop.</summary>
        private void HordeGalleryHulkCharge(NativeArena arena,NativePilot me,float now,Vector3 across,float yawToMe,float ground)
        {
            int k=NativeHordeKinds.Hulk,id=990030;
            if(galleryChargeRig==null){galleryChargeRig=captured.Create(transform,id);galleryChargeState=new HordeKindState();galleryChargeState.Reset(id);galleryChargeState.Kind=k;galleryChargeState.Look=0;galleryChargeState.React.Bind(k);}
            var rig=galleryChargeRig;var s=galleryChargeState;var st=NativeHordeKinds.ProposedRow(k);uint seed=NativeHordeKinds.Seed(id);float sc=NativeHordeKinds.Scale[k];s.Scale=sc;
            Vector3 toMe=GalleryDir(yawToMe),away=-toMe;
            if(!galleryChargePlaced){galleryChargeMe=galleryAt+toMe*6;galleryChargeMe.y=ground;galleryChargePlaced=true;}
            Vector3 meAt=galleryChargeMe,S=meAt-across*34+away*1.5f;S.y=ground;Vector3 L=(meAt-S);L.y=0;float laneLen=L.magnitude;L/=laneLen;float laneYaw=Mathf.Atan2(L.x,L.z)*Mathf.Rad2Deg;
            float R=NativeHordeKinds.WorldRadius(k),root=NativeHordeKinds.DrawRoot(k,false);
            // Its two walls across the lane (the arena's block material), the breach entries they break through on contact.
            if(galleryChargeWalls[0]==null)
            {
                Material mat=null;var props=arena.Props.Count>0?arena.Props:arena.StaticBlocks;if(props.Count>0&&props[0]!=null){var r=props[0].GetComponent<Renderer>();if(r!=null)mat=r.sharedMaterial;}
                for(int w=0;w<2;w++)
                {
                    var c=S+L*(w==0?GcWall0:GcWall1);c.y=ground+2.1f;var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=NativeBreach.ObjectName("gallery-charge-"+w);Destroy(go.GetComponent<Collider>());
                    go.transform.SetPositionAndRotation(c,Quaternion.LookRotation(L));go.transform.localScale=new Vector3(5,4.2f,.5f);if(mat!=null)go.GetComponent<Renderer>().sharedMaterial=mat;galleryChargeWalls[w]=go;
                    var from=c-L*1.8f;var exit=c+L*2.8f;galleryChargeEntries[w]=new PveEntry{id="gallery-charge-"+w,kind=NativeBreach.Kind,from=new PvePoint{x=from.x,z=from.z},@exit=new PvePoint{x=exit.x,z=exit.z},yaw=laneYaw*Mathf.Deg2Rad};
                    hordeGalleryLane[w]=LaneWall(go,null,c,new Vector3(2.5f,2.1f,.25f),laneYaw);
                }
                hordeGalleryLaneCount=2;
            }
            float loopAt=Mathf.Floor((now-galleryStart)/GalleryChargeLoop),g=now-galleryStart-loopAt*GalleryChargeLoop,t0=now-g;bool solid=((int)loopAt&1)==1;
            if(!float.IsNaN(galleryChargeLast)&&g<galleryChargeLast){s.Gait.Reset();s.React.Clear();}galleryChargeLast=g;
            // The dummies: the punch's and swipe's beside its start, the charge's (or the dark pillar) short of the pilot.
            Vector3 d0=S+away*2.4f,dEnd=meAt-L*GcDummyGap;float endGap=solid?R+.5f:R+.45f;
            float c0=GcWall0-.25f-R,c1=GcWall1-.25f-R,runEnd=laneLen-GcDummyGap-endGap;
            // The run's end (s into it): where it meets the dummy or the pillar.
            float tEnd;{float lo=0,hi=12;for(int it=0;it<30;it++){float mid=(lo+hi)*.5f;if(GalleryRun(mid,c0,c1,out _)<runEnd)lo=mid;else hi=mid;}tEnd=hi;}
            float lockAt=GcWind+NativeHulkMoves.ChargeWind,endAt=lockAt+tEnd;var end=solid?NativeHulkMoves.ChargeEnd.Solid:NativeHulkMoves.ChargeEnd.Pilot;float stop=NativeHulkMoves.Stop(end);
            hordeDummies[0]=new HordeDummy{Live=true,At=new Vector3(d0.x,ground,d0.z),HitAt=g>=GcSwipe+NativeHulkMoves.SwipeHit?t0+GcSwipe+NativeHulkMoves.SwipeHit:g>=GcPunch+NativeHulkMoves.PunchHit?t0+GcPunch+NativeHulkMoves.PunchHit:-100,Push=g>=GcSwipe+NativeHulkMoves.SwipeHit?-across:away};
            hordeDummies[1]=new HordeDummy{Live=true,Pillar=solid,At=new Vector3(dEnd.x,ground,dEnd.z),HitAt=g>=endAt?t0+endAt:-100,Push=L};
            Vector3 pos=S,vel=Vector3.zero;float yawD0=Mathf.Atan2(away.x,away.z)*Mathf.Rad2Deg,yaw=yawD0;var toMeNow=me.Body.position-S;float yawMe=Mathf.Atan2(toMeNow.x,toMeNow.z)*Mathf.Rad2Deg;
            var f=new HordeMotionInput{Kind=k,Look=0,Id=id,Seed=seed,Time=now,Health=1,AttackAge=float.NaN,Attack=NativeHordeKinds.Attack.Smash,Side=1,HasTarget=true,Target=ToN(me.Body.position)/sc};
            var attack=NativeHordeKinds.Attack.Smash;float age=float.NaN,strike=NativeHordeKinds.Strike(st.hit,false,0),recover=NativeHordeKinds.Recover(st.hit,st.cycle,false,0);int side=1;
            float breakAge=float.NaN;var phase=NativeHulkMoves.ChargePhase.None;float chargeAge=g-GcWind,endAge=float.NaN;
            if(g<GcTurn)
            {
                // A punch, then a swipe with the left arm, at the dummy.
                var move=g>=GcSwipe?NativeHulkMoves.Move.Swipe:g>=GcPunch?NativeHulkMoves.Move.Punch:NativeHulkMoves.Move.Content;
                if(move!=NativeHulkMoves.Move.Content){float start=move==NativeHulkMoves.Move.Punch?GcPunch:GcSwipe;f.Move=move;side=move==NativeHulkMoves.Move.Swipe?-1:1;f.Side=side;
                    strike=NativeHulkMoves.Strike(move,strike,false,0);recover=NativeHulkMoves.Recover(move,recover,false,0);attack=move==NativeHulkMoves.Move.Punch?NativeHordeKinds.Attack.Lunge:NativeHordeKinds.Attack.Swipe;if(g-start<recover+.25f)age=g-start;}
            }
            else if(g<GcWind)yaw=yawMe;
            else if(g<endAt+stop+1.2f)
            {
                // The wind-up tracks the pilot; at the lock the heading is the lane's; the run; the end.
                phase=g<lockAt?NativeHulkMoves.ChargePhase.Wind:g<endAt?NativeHulkMoves.ChargePhase.Run:NativeHulkMoves.ChargePhase.Stop;
                yaw=phase==NativeHulkMoves.ChargePhase.Wind?yawMe:laneYaw;
                float run=Mathf.Clamp(g-lockAt,0,tEnd);float d=GalleryRun(run,c0,c1,out float sp);pos=S+L*d;if(phase==NativeHulkMoves.ChargePhase.Run)vel=L*sp;
                if(phase==NativeHulkMoves.ChargePhase.Stop){endAge=g-endAt;if(solid)pos-=L*(.45f*Mathf.SmoothStep(0,1,endAge/.45f));}
                f.Charging=g<endAt+stop+NativeHulkMoves.BlendOut;f.ChargeAge=chargeAge;f.ChargeEnded=g>=endAt;f.ChargeEndAge=g-endAt;f.ChargeHow=g>=endAt?end:NativeHulkMoves.ChargeEnd.Running;
                // The walls: each breaks on contact (no knock) and the shoulder flinches; the breach rebuilds it in hardlight.
                for(int w=0;w<2;w++)
                {
                    float hit=lockAt+GalleryContact(c0,c1,w);if(g<hit)continue;
                    var e=galleryChargeEntries[w];HordeBreachJob(arena,e,t0+hit,t0+hit,new Vector3(e.from.x,ground,e.from.z),new Vector3(e.@exit.x,ground,e.@exit.z),k,seed^(uint)(w+1),galleryChargeWalls[w],id,true);
                    if(g<endAt){breakAge=g-hit;f.ChargeBroke=true;f.ChargeBreakAge=breakAge;}
                }
            }
            else yaw=laneYaw;
            if(phase==NativeHulkMoves.ChargePhase.None&&g>=endAt)pos=S+L*runEnd-(solid?L*.45f:Vector3.zero);
            pos.y=ground+root;float inv=1/sc;
            f.Yaw=yaw;f.Ground=ground*inv;f.Root=ToN(pos)*inv;f.Velocity=ToN(vel)*inv;f.AttackAge=age;f.Strike=strike;f.Recover=recover;f.Attack=attack;
            var floor=new Vector3(pos.x,ground,pos.z);
            if(phase!=NativeHulkMoves.ChargePhase.None)
            {
                var c=NativeHulkCharge.State.None;c.Start(t0+GcWind,arena.LocalId);if(g>=lockAt){c.Locked=true;c.LockYaw=laneYaw;c.LockAt=ToN(new Vector3(S.x,ground,S.z));}if(g>=endAt)c.End(t0+endAt,end);
                HordeChargeBegin(id,c,phase,chargeAge,endAge,floor,vel,phase==NativeHulkMoves.ChargePhase.Wind?yawMe:laneYaw,seed,true,breakAge);
            }
            float roar=float.NaN;if(phase==NativeHulkMoves.ChargePhase.Wind)roar=Mathf.Min(chargeAge,.6f);else if(g>=endAt+stop+.6f&&g<endAt+stop+.6f+NativeHordeRoar.Seconds)roar=g-(endAt+stop+.6f);
            f.Roaring=!float.IsNaN(roar);f.RoarAge=roar;
            ref var input=ref hordeInput[0];input=f;
            s.Gait.Pose(input,minionPose,s.React);s.Distance=Vector3.Distance(pos,hordeEye);HordeHulkPosed(s,hordeKindArena!=null?hordeKindArena.Time:now);
            HordeKindJob(s,id,k,attack,age,strike,recover,pos,yaw,ground,st,me,rig,now,side);if(s.Gait.Landed!=0)HordeDust(s,k,ground,now);
            s.Jaw=NativeHordeJaw.Open(k,attack,age,strike,float.NaN,roar,now,seed);
            NativePoseWriter.Write(rig.Bones,minionPose,"a gallery rig");rig.Root.transform.SetPositionAndRotation(pos,Quaternion.Euler(0,s.Gait.Yaw,0));rig.Root.transform.localScale=Vector3.one*sc;
            var glow=HordeGlow(id);HordeQueue(rig,NativeHordeModels.Id(k,0,NativeHordeModels.Full),id,0,0,0,glow,1);HordeQueueJaw(rig,k,0,NativeHordeModels.Full,s.Jaw,glow,1);HordeAdd(rig,id,float.NaN,float.NaN,ground,ground,float.NaN,vel,0,true,k*8);
        }
        /// <summary>The gallery's hardlight dummies (a pilot-sized wireframe, knocked away and re-formed) and the solid stop's pillar.</summary>
        private void HordeDummiesDraw(NativeFxDraw draw,float now,Vector3 eye,Vector3 right,Vector3 up)
        {
            if(!MinionGallery)return;
            for(int i=0;i<hordeDummies.Length;i++)
            {
                var d=hordeDummies[i];if(!d.Live)continue;
                if(d.Pillar){draw.Cube(draw.Solid,d.At+Vector3.up*1.6f,Quaternion.LookRotation(d.Push),new Vector3(1.6f,3.2f,1f),new Color(.07f,.065f,.08f),NativeFxDraw.Authored);continue;}
                float since=d.HitAt>-90?now-d.HitAt:99,gone=since<1.6f?Mathf.SmoothStep(0,1,since/.6f):0,form=since>=1.6f&&since<2.2f?(since-1.6f)/.6f:1;
                var at=d.At+d.Push*(2.2f*Mathf.Min(1,since/.5f)*(since<1.6f?1:0))+Vector3.up*(since<.5f?Mathf.Sin(since/.5f*Mathf.PI)*.5f:0);float alpha=(1-gone)*form;if(alpha<=.02f)continue;
                var c=Alpha(HardlightColor,.85f*alpha);float r=.42f,h=1.8f;var ax=Vector3.Cross(Vector3.up,d.Push).normalized;
                for(int e=0;e<8;e++){float a0=e*Mathf.PI/4,a1=(e+1)*Mathf.PI/4;Vector3 o0=(ax*Mathf.Cos(a0)+d.Push*Mathf.Sin(a0))*r,o1=(ax*Mathf.Cos(a1)+d.Push*Mathf.Sin(a1))*r;
                    HordeStreak(draw,at+o0+Vector3.up*.25f,at+o1+Vector3.up*.25f,.012f,c,eye);HordeStreak(draw,at+o0+Vector3.up*(h-.25f),at+o1+Vector3.up*(h-.25f),.012f,c,eye);if(e%2==0)HordeStreak(draw,at+o0+Vector3.up*.25f,at+o0+Vector3.up*(h-.25f),.01f,c,eye);}
                draw.Sprite(draw.Effects,at+Vector3.up*(h+.05f),right,up,.42f,Alpha(HardlightColor,.6f*alpha),NativeFxDraw.Authored+NativeFxDraw.Additive);
                if(since<.12f)draw.Sprite(draw.Effects,at+Vector3.up*1.1f,right,up,2f*(1-since/.12f),Alpha(ChargeFlash,.8f*(1-since/.12f)),NativeFxDraw.Authored+NativeFxDraw.Additive);
            }
        }
        private void ClearHordeHulkChargeGallery()
        {
            galleryChargeRig?.Dispose();galleryChargeRig=null;galleryChargeState=null;galleryChargeLast=float.NaN;galleryChargePlaced=false;
            ClearHordeTrembles();
            for(int w=0;w<2;w++){if(galleryChargeWalls[w]!=null){for(int i=0;i<hordeBreaches.Length;i++)if(hordeBreaches[i].Live&&hordeBreaches[i].Entry==galleryChargeEntries[w])HordeBreachRelease(hordeBreaches[i]);Destroy(galleryChargeWalls[w]);galleryChargeWalls[w]=null;}galleryChargeEntries[w]=null;hordeGalleryLane[w]=default;}
            hordeGalleryLaneCount=0;for(int i=0;i<hordeDummies.Length;i++)hordeDummies[i]=default;
        }
    }
}
