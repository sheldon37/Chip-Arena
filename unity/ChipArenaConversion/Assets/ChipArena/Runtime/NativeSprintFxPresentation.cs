using System.Collections.Generic;
using UnityEngine;
using NVector=System.Numerics.Vector3;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Draws the sprint's third-person effect (Core/NativeSprintFx) through the shared effects renderer: each sprinting pilot
    /// (the replicated Actor.Sprinting flag, grounded, travelling, not busy: Core/NativeSprintRules.Shows) sheds short accent
    /// speed streaks off its shoulders, hips, head and back, and kicks a little dust at each footfall; a greatsword dragging its
    /// point (Core/NativeSprintMotion.DragBlade) throws faint scrape sparks off the floor there. Streaks are camera-facing
    /// ribbons and dust is camera-facing puffs: nothing lies flat on the floor. The view pilot's own effect is skipped in first
    /// person (the screen lines and vignette show it there). Reduced effects and Low graphics thin and dim it. Presentation
    /// only: host, guests, killcam and spectators each draw it from the pilots they present. The high-quality pass (owner
    /// 2026-11-09): pale wind sweeping back past the body, a burst as a sprint starts, grit and a flash at each footfall (here,
    /// through the shared effects renderer), and ribbons of light off the hands and feet (Trails, through the weapon effects'
    /// drawer, queued before it is submitted).
    /// </summary>
    public sealed class NativeSprintFxPresentation
    {
        private struct Pilot{public float Ramp,Distance,OffAt;public int Foot,Seed;public Vector3 At;public bool Seen,WasOn;}
        private struct Kick{public float At,Floor;public Vector3 Foot,Travel;public int Seed;public Color Tint;}
        private struct Burst{public float At;public Vector3 Feet,Forward;public int Seed;public Color Tint;}
        // A pilot's limb trails: each joint's last TrailSamples points (a ring, newest at Newest) and their times, and where each
        // joint is this frame (the head of its trail).
        private sealed class Trail{public readonly Vector3[] P=new Vector3[NativeSprintFx.TrailJoints.Length*NativeSprintFx.TrailSamples];public readonly float[] T=new float[NativeSprintFx.TrailSamples];public readonly Vector3[] Live=new Vector3[NativeSprintFx.TrailJoints.Length];public int Count,Newest;}
        private readonly NativeVfxDraw vfx;
        private readonly Dictionary<int,Pilot> pilots=new Dictionary<int,Pilot>();
        private readonly List<Kick> kicks=new List<Kick>(24);private readonly List<Burst> bursts=new List<Burst>(8);
        private readonly Dictionary<int,Trail> trails=new Dictionary<int,Trail>();
        private NativeArena last;private int generation=-1,seeds;private float lastTime=-1,trailTime=-1;
        public int Live=>kicks.Count+bursts.Count;
        public NativeSprintFxPresentation(NativeVfxDraw vfx){this.vfx=vfx;}
        public void Clear(){kicks.Clear();bursts.Clear();pilots.Clear();foreach(var t in trails.Values)t.Count=0;lastTime=trailTime=-1;}
        /// <summary>The dragged greatsword's point (world) of a presented pilot whose rig shows the drag this frame, or false.</summary>
        public delegate bool DragPoint(Actor a,float time,out Vector3 tip);
        /// <summary>now: the snapshot clock (ArenaPresentation.FieldClock); quality: the effects quality (1, .3 Reduced); drag:
        /// the greatsword's dragged point (none: no scrape sparks).</summary>
        public void Render(NativeArena arena,float now,float quality,bool low,int viewId,bool firstPerson,DragPoint drag=null)
        {
            if(arena==null||vfx==null)return;
            if(arena!=last||generation!=arena.Generation||now<lastTime-.25f){Clear();last=arena;generation=arena.Generation;}
            float dt=lastTime<0?0:Mathf.Clamp(now-lastTime,0,.05f);lastTime=Mathf.Max(lastTime,now);
            int streaks=NativeSprintFx.Count(NativeSprintFx.Streaks,NativeSprintFx.ReducedStreaks,quality,low);float shade=NativeSprintFx.Dim(quality);
            int winds=NativeSprintFx.Count(NativeSprintFx.Winds,NativeSprintFx.ReducedWinds,quality,low);
            foreach(var p in arena.Pilots)
            {
                if(p?.Body==null||p.Pose==null)continue;var a=p.Pose;int id=p.Seat.id;pilots.TryGetValue(id,out var s);
                Vector3 feet=p.Body.transform.position-Vector3.up*.865f;bool on=NativeSprintRules.Shows(a,now);
                if(!s.Seen){s=new Pilot{Seen=true,Ramp=on?1:0,At=feet,Seed=++seeds,OffAt=-100,WasOn=on};}
                s.Ramp=NativeSprintRules.Step(s.Ramp,on,dt);float weight=NativeSprintRules.Shape(s.Ramp);
                Vector3 travel=new Vector3(a.Velocity.x,0,a.Velocity.z);float speed=travel.magnitude;travel=speed>.1f?travel/speed:Vector3.zero;
                var tint=NativeGlowRules.ArmorTint(a.AccentColor);Color hue=new Color(tint.X,tint.Y,tint.Z,0);bool own=firstPerson&&id==viewId;
                // The start burst: a sprint taken up after a rest (not one already running when first seen).
                if(on&&!s.WasOn&&now-s.OffAt>=NativeSprintFx.BurstRest&&!own)
                {
                    Vector3 facing=speed>.1f?travel:Quaternion.Euler(0,a.Yaw,0)*Vector3.forward;facing.y=0;facing.Normalize();
                    if(bursts.Count>=8)bursts.RemoveAt(0);bursts.Add(new Burst{At=now,Feet=feet,Forward=facing,Seed=++seeds,Tint=Color.Lerp(hue,new Color(1,1,1,0),.45f)});
                }
                if(!on&&s.WasOn)s.OffAt=now;s.WasOn=on;
                // Footfalls every StepLength of grounded sprint travel (the gait's step at sprint speed), alternating feet.
                Vector3 moved=feet-s.At;moved.y=0;float step=moved.magnitude;s.At=feet;
                if(!on||step>NativeFootsteps.MaxStep)s.Distance=0;
                else{s.Distance+=step;if(s.Distance>=NativeSprintRules.StepLength){s.Distance-=NativeSprintRules.StepLength;s.Foot^=1;
                    if(!own){Vector3 side=new Vector3(travel.z,0,-travel.x)*(s.Foot==0?-.12f:.12f);if(kicks.Count>=24)kicks.RemoveAt(0);kicks.Add(new Kick{At=now,Foot=feet+side,Floor=feet.y,Travel=travel,Seed=++seeds,Tint=Color.Lerp(hue,new Color(1,1,1,0),.35f)});}}}
                pilots[id]=s;
                if(weight<=1e-3f||speed<=.1f||own)continue;
                Color accent=hue*(NativeSprintFx.StreakGain*shade);
                var f=new NVector(feet.x,feet.y,feet.z);var d=new NVector(travel.x,0,travel.z);
                for(int k=0;k<streaks;k++)
                    if(NativeSprintFx.Streak(k,s.Seed,now,weight,f,d,d,out var head,out var tail,out float alpha))
                        vfx.Streak(new Vector3(head.X,head.Y,head.Z),-new Vector3(tail.X,tail.Y,tail.Z),accent*alpha,NativeSprintFx.StreakWidth,tail.Length(),1,.9f,alpha,s.Seed*29+k);
                // The wind: pale streaks of air sweeping back past the body, a touch of the accent in them.
                Color air=Color.Lerp(hue,new Color(1,1,1,0),.65f)*(NativeSprintFx.WindGain*shade);
                for(int k=0;k<winds;k++)
                    if(NativeSprintFx.Wind(k,s.Seed,now,weight,f,d,out var head,out var tail,out float alpha))
                        vfx.Streak(new Vector3(head.X,head.Y,head.Z),-new Vector3(tail.X,tail.Y,tail.Z),air*alpha,NativeSprintFx.WindWidth,tail.Length(),1,.95f,alpha,s.Seed*31+k);
                // The greatsword's scrape: hot sparks thrown off the floor at its dragged point, fewer as the point lifts.
                if(drag!=null&&drag(a,now,out var point))
                {
                    float contact=weight*NativeSprintFx.ScrapeContact(point.y-feet.y);int sparks=NativeSprintFx.Count(NativeSprintFx.Scrapes,NativeSprintFx.ReducedScrapes,quality,low);
                    var tip=new NVector(point.x,point.y,point.z);var velocity=new NVector(a.Velocity.x,0,a.Velocity.z);
                    for(int k=0;contact>1e-3f&&k<sparks;k++)
                        if(NativeSprintFx.Scrape(k,s.Seed,now,contact,tip,velocity,feet.y,out var spark,out var motion,out float heat))
                        {
                            var c=NativeSprintFx.ScrapeColor*(heat*shade);
                            vfx.Streak(new Vector3(spark.X,spark.Y,spark.Z),new Vector3(motion.X,motion.Y,motion.Z),new Color(c.X,c.Y,c.Z,0),NativeSprintFx.ScrapeWidth,NativeSprintFx.ScrapeWidth*2.2f,.03f,.9f,1-heat,s.Seed*37+k);
                        }
                }
            }
            for(int i=kicks.Count-1;i>=0;i--)if(now-kicks[i].At>=NativeSprintFx.DustLife||now<kicks[i].At-.25f)kicks.RemoveAt(i);
            int puffs=NativeSprintFx.Count(NativeSprintFx.DustPuffs,NativeSprintFx.ReducedDustPuffs,quality,low);
            var dust=NativePortraitRules.Linear(NativeSprintFx.DustColor);Color albedo=new Color(dust.X,dust.Y,dust.Z,0);
            int grits=NativeSprintFx.Count(NativeSprintFx.Grits,NativeSprintFx.ReducedGrits,quality,low);var grey=NativePortraitRules.Linear(NativeSprintFx.GritColor);Color grit=new Color(grey.X,grey.Y,grey.Z,1);
            foreach(var kick in kicks)
            {
                float age=now-kick.At;var foot=new NVector(kick.Foot.x,kick.Foot.y,kick.Foot.z);var travel=new NVector(kick.Travel.x,0,kick.Travel.z);
                for(int k=0;k<puffs;k++)
                    if(NativeSprintFx.Dust(k,kick.Seed,age,foot,travel,out var at,out float size,out float opacity))
                        vfx.Smoke(new Vector3(at.X,at.Y,at.Z),albedo,opacity*shade,size,age/NativeSprintFx.DustLife,kick.Seed*13+k,kick.Floor);
                // The foot's strike: a quick flash in the accent, and grit kicked back off the floor.
                float flash=NativeSprintFx.Flash(age);
                if(flash>0)vfx.Glow(kick.Foot+Vector3.up*.08f,kick.Tint*(flash*NativeSprintFx.FlashGain*shade),NativeSprintFx.FlashSize*(.55f+.45f*(1-flash)),NativeVfxDraw.CoreGlow,age/NativeSprintFx.FlashLife,kick.Seed);
                for(int k=0;k<grits;k++)
                    if(NativeSprintFx.Grit(k,kick.Seed,age,foot,travel,kick.Floor,out var bit,out float size,out float spin))
                        vfx.Rubble(new Vector3(bit.X,bit.Y,bit.Z),Quaternion.Euler(spin*57.3f,spin*41,spin*23),size,kick.Seed*7+k,grit,Color.black);
            }
            // The start bursts: the ring of air behind the chest, the dust thrown out round the feet, the streaks shot back.
            for(int i=bursts.Count-1;i>=0;i--)if(now-bursts[i].At>=NativeSprintFx.BurstLife||now<bursts[i].At-.25f)bursts.RemoveAt(i);
            int burstPuffs=NativeSprintFx.Count(NativeSprintFx.BurstPuffs,NativeSprintFx.ReducedBurstPuffs,quality,low),burstStreaks=NativeSprintFx.Count(NativeSprintFx.BurstStreaks,NativeSprintFx.ReducedBurstStreaks,quality,low);
            foreach(var b in bursts)
            {
                float age=now-b.At;var feet=new NVector(b.Feet.x,b.Feet.y,b.Feet.z);var forward=new NVector(b.Forward.x,0,b.Forward.z);
                if(NativeSprintFx.BurstRing(age,out float ring,out float strength))
                    vfx.Glow(b.Feet+Vector3.up*1.1f-b.Forward*.3f,b.Tint*(strength*NativeSprintFx.BurstGain*shade),ring,NativeVfxDraw.Ring,age/NativeSprintFx.BurstRingLife,b.Seed);
                for(int k=0;k<burstPuffs;k++)
                    if(NativeSprintFx.BurstPuff(k,burstPuffs,b.Seed,age,feet,forward,out var at,out float size,out float opacity))
                        vfx.Smoke(new Vector3(at.X,at.Y,at.Z),albedo,opacity*shade,size,age/NativeSprintFx.BurstLife,b.Seed*17+k,b.Feet.y);
                for(int k=0;k<burstStreaks;k++)
                    if(NativeSprintFx.BurstStreak(k,b.Seed,age,feet,forward,out var head,out var motion,out float alpha))
                        vfx.Streak(new Vector3(head.X,head.Y,head.Z),new Vector3(motion.X,motion.Y,motion.Z),b.Tint*(alpha*NativeSprintFx.BurstGain*shade),.018f,.25f,.05f,.9f,1-alpha,b.Seed*19+k);
            }
        }

        /// <summary>The ribbons of light off each sprinting pilot's hands and feet (NativeSprintFx.TrailJoints): each joint's
        /// path over its trail's life, brightest and widest at the joint, in the pilot's accent whitened a little, faded with the
        /// sprint's weight (as Render last stepped it). Queued into the weapon effects' drawer, facing `eye`, before it is
        /// submitted. The view pilot's own are skipped in first person; Reduced effects and Low graphics keep the hands only.</summary>
        public delegate bool LimbPoint(Actor a,int joint,out Vector3 at);
        public void Trails(NativeWeaponFxDraw draw,NativeArena arena,float now,Vector3 eye,float quality,bool low,int viewId,bool firstPerson,LimbPoint limb)
        {
            if(draw==null||arena==null||limb==null)return;const int S=NativeSprintFx.TrailSamples;
            if(arena!=last||generation!=arena.Generation||now<trailTime-.25f)foreach(var t in trails.Values)t.Count=0;trailTime=Mathf.Max(trailTime,now);
            int joints=NativeSprintFx.TrailCount(quality,low);float shade=NativeSprintFx.Dim(quality);
            foreach(var p in arena.Pilots)
            {
                if(p?.Pose==null)continue;var a=p.Pose;int id=p.Seat.id;float weight=pilots.TryGetValue(id,out var s)?NativeSprintRules.Shape(s.Ramp):0;
                if(!trails.TryGetValue(id,out var t)){if(weight<=1e-3f)continue;trails[id]=t=new Trail();}
                if(weight<=1e-3f||!a.Alive||firstPerson&&id==viewId){t.Count=0;continue;}
                bool ok=true;for(int j=0;j<joints&&ok;j++)ok=limb(a,NativeSprintFx.TrailJoints[j],out t.Live[j]);if(!ok){t.Count=0;continue;}
                if(t.Count>0&&now<t.T[t.Newest]-.25f)t.Count=0;
                if(t.Count==0||now-t.T[t.Newest]>=NativeSprintFx.TrailStep){t.Newest=(t.Newest+1)%S;t.T[t.Newest]=now;for(int j=0;j<joints;j++)t.P[j*S+t.Newest]=t.Live[j];t.Count=Mathf.Min(S,t.Count+1);}
                var tint=NativeGlowRules.ArmorTint(a.AccentColor);Color c=Color.Lerp(new Color(tint.X,tint.Y,tint.Z,0),new Color(1,1,1,0),NativeSprintFx.TrailWhite)*(NativeSprintFx.TrailGain*shade*weight);c.a=0;
                for(int j=0;j<joints;j++)
                {
                    int joint=NativeSprintFx.TrailJoints[j];float life=NativeSprintFx.TrailLifeOf(joint),width=NativeSprintFx.TrailWidthOf(joint);Vector3 b=t.Live[j];float kb=1;
                    for(int n=0;n<t.Count;n++)
                    {
                        int i=((t.Newest-n)%S+S)%S;float ka=NativeSprintFx.TrailFade(now-t.T[i],life);Vector3 at=t.P[j*S+i];
                        if((b-at).sqrMagnitude>1e-8f)TrailSpan(draw,at,b,ka,kb,eye,c,width);
                        if(ka<=0)break;b=at;kb=ka;
                    }
                }
            }
        }
        // One span of a trail from its older end `a` (strength ka) to its newer `b` (kb), turned to the eye.
        private static void TrailSpan(NativeWeaponFxDraw draw,Vector3 a,Vector3 b,float ka,float kb,Vector3 eye,Color c,float width)
        {
            Vector3 mid=(a+b)*.5f,side=Vector3.Cross(b-a,eye-mid);if(side.sqrMagnitude<1e-10f)return;side.Normalize();
            Vector3 sa=side*(.5f*NativeSprintFx.TrailWidthAt(ka,width)),sb=side*(.5f*NativeSprintFx.TrailWidthAt(kb,width));
            draw.Quad(false,a-sa,b-sb,b+sb,a+sa,new Vector2(ka,-1),new Vector2(kb,-1),new Vector2(kb,1),new Vector2(ka,1),c,NativeWeaponFxDraw.Ribbon,0);
        }
    }

    public sealed partial class ArenaPresentation
    {
        // The sprint's effects (owner 2026-10-12): the third-person streaks and dust (NativeSprintFxPresentation, queued with the
        // jump burst in RenderGlow), the first-person screen lines and vignette (OnGUI below), and the first-person camera's
        // FOV kick and bob (ConversionGame.UpdateCamera and LateFrame through SprintFov and SprintSway). The first-person weight
        // is the view layer's (NativeViewSway.Sprint: the view pilot's replicated sprint, faded out by a swing, block or roll).
        private NativeSprintFxPresentation sprintFx;private NativeSprintFxPresentation.DragPoint dragPoint;
        // The greatsword's dragged point for its scrape sparks: the blade-tip socket on the pilot's rig as posed this frame, only
        // while that rig shows the drag (its sprint layer's carry) with the main weapon out.
        private bool DragPoint(Actor a,float time,out Vector3 tip)
        {
            tip=default;
            if(a==null||a.WeaponAway||a.MainWeaponAway||!rigs.TryGetValue(a.Id,out var rig)||rig.SprintCarry!=NativeSprintRules.Drag||!NativeWeaponDressing.BladeTip(a.SourceSpec,out var local))return false;
            tip=SourceWeaponBone(rig,a,time,false,CapturedRig.RHand,rig.WeaponReach).MultiplyPoint3x4(CapturedAnimation.Vec(CapturedRig.Mirror(local)));return true;
        }
        // The limb trails' points: the joint on the pilot's visual body as posed this frame (its own bone, so a model posed by its
        // own motion trails from its own hands), else on its rig.
        private NativeSprintFxPresentation.LimbPoint limbPoint;
        private bool LimbPoint(Actor a,int joint,out Vector3 at)
        {
            at=default;if(a==null||!rigs.TryGetValue(a.Id,out var rig)||rig?.Root==null)return false;var body=VisualHolder(rig);
            if(body?.Binding?.Rig!=null&&body.Binding.Bones!=null){var of=body.Binding.Rig.Of;int v=joint<of.Length?of[joint]:-1;if(v>=0&&v<body.Binding.Bones.Length&&body.Binding.Bones[v]!=null){at=body.Binding.Bones[v].position;return true;}}
            var bone=joint>=0&&joint<rig.Bones.Length?rig.Bones[joint]:null;if(bone==null)return false;at=bone.position;return true;
        }
        /// <summary>WeaponFx, before its drawer is submitted: the sprint's limb trails (NativeSprintFxPresentation.Trails).</summary>
        private void SprintTrails(NativeWeaponFxDraw draw,NativeArena arena,float now,Vector3 eye)
        {
            if(vfx==null||arena==null)return;sprintFx??=new NativeSprintFxPresentation(vfx);
            sprintFx.Trails(draw,arena,now,eye,WeaponEffectsQuality,LowGraphics,arena.ViewPilot?.Seat.id??-1,FirstPerson,limbPoint??=LimbPoint);
        }
        private float screenSprint,screenClock,screenLast,screenRate=1;private int screenFrame=-1;private Color screenTint;
        private static Texture2D vignetteTexture,lineTexture;
        private void SprintFx(NativeArena arena,float now)
        {
            if(arena==null)return;
            if(vfx!=null){sprintFx??=new NativeSprintFxPresentation(vfx);sprintFx.Render(arena,now,WeaponEffectsQuality,LowGraphics,arena.ViewPilot?.Seat.id??-1,FirstPerson,dragPoint??=DragPoint);}
            var view=arena.ViewPilot?.Pose;bool shown=FirstPerson&&viewRig!=null&&!ScopeActive&&view!=null&&view.Alive;
            // The plunging stab's drop takes the same screen (Core/NativePlungeRules.ScreenWeight), its lines streaming faster.
            float plunge=shown?PlungeScreen(view,kitClock):0;
            screenSprint=shown?Mathf.Max(viewSway.Sprint,plunge):0;screenFrame=Time.frameCount;screenRate=Mathf.Lerp(screenRate,plunge>viewSway.Sprint?NativePlungeRules.LineRate:1,.25f);screenClock+=Mathf.Max(0,now-screenLast)*screenRate;screenLast=now;
            if(view!=null){var t=NativeSprintFx.Tint(view.SourceSpec,view.AccentColor);screenTint=new Color(t.X,t.Y,t.Z,1).gamma;}
        }
        /// <summary>The first-person FOV kick (degrees) for the view pilot's sprint: none with camera shake off, half with Reduced
        /// effects (Core/NativeSprintRules.Fov).</summary>
        public float SprintFov(bool shake,bool reduced)=>FirstPerson&&viewRig!=null?NativeSprintRules.Fov(viewSway.Sprint,shake,reduced):0;
        /// <summary>The first-person camera's sprint bob and roll at the view stride (camera shake only; applied after the aim
        /// point is taken, as the other camera shakes are, and rebuilt from the look angles next frame). It also keeps the
        /// setting for the view weapon's per-weapon bob (ApplyViewSway, NativeViewSway).</summary>
        public void SprintSway(Transform camera,bool shake,bool reduced)
        {
            viewShake=shake?(reduced?.5f:1):0;
            if(camera==null||!FirstPerson||viewRig==null)return;NativeSprintRules.CameraSway(viewSway.Sprint,viewSway.Stride,shake,reduced,out float up,out float roll);
            if(up==0&&roll==0)return;camera.SetPositionAndRotation(camera.position+camera.up*up,camera.rotation*Quaternion.Euler(0,0,roll));
        }
        private static Texture2D ScreenTexture(int w,int h,System.Func<float,float,float> alpha)
        {
            var tex=new Texture2D(w,h,TextureFormat.RGBA32,false,true){name="Native sprint screen",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear,hideFlags=HideFlags.HideAndDontSave};
            var px=new Color32[w*h];for(int y=0;y<h;y++)for(int x=0;x<w;x++)px[y*w+x]=new Color32(255,255,255,(byte)Mathf.RoundToInt(255*Mathf.Clamp01(alpha((x+.5f)/w,(y+.5f)/h))));
            tex.SetPixels32(px);tex.Apply(false,true);return tex;
        }
        // The first-person sprint screen: a soft tinted vignette and speed lines streaming out at the edges, behind the HUD.
        private void OnGUI()
        {
            if(Event.current.type!=EventType.Repaint||screenFrame!=Time.frameCount||screenSprint<=1e-3f)return;
            useGUILayout=false;GUI.depth=10;
            vignetteTexture??=ScreenTexture(64,64,NativeSprintFx.VignetteShape);lineTexture??=ScreenTexture(32,8,NativeSprintFx.LineShape);
            float quality=WeaponEffectsQuality,w=Screen.width,h=Screen.height;Color keep=GUI.color;Matrix4x4 matrix=GUI.matrix;
            Color shade=screenTint*NativeSprintFx.VignetteShade;shade.a=NativeSprintFx.Vignette(screenSprint,quality);GUI.color=shade;
            GUI.DrawTexture(new Rect(0,0,w,h),vignetteTexture,ScaleMode.StretchToFill,true);
            int lines=NativeSprintFx.Count(NativeSprintFx.Lines,NativeSprintFx.ReducedLines,quality,LowGraphics);float width=NativeSprintFx.LineWidth*h/1080,dim=NativeSprintFx.Dim(quality);
            Vector2 centre=new Vector2(w*.5f,h*.5f);
            for(int k=0;k<lines;k++)
            {
                if(!NativeSprintFx.Line(k,7919,screenClock,screenSprint,out float angle,out float inner,out float outer,out float alpha))continue;
                Vector2 dir=new Vector2(Mathf.Cos(angle)*w*.5f,Mathf.Sin(angle)*h*.5f),from=centre+dir*inner,to=centre+dir*outer,along=to-from;float length=along.magnitude;if(length<1)continue;
                GUI.matrix=matrix;GUIUtility.RotateAroundPivot(Mathf.Atan2(along.y,along.x)*Mathf.Rad2Deg,from);
                float wide=width*NativeSprintFx.LineWidthOf(k);Color c=screenTint;c.a=alpha*dim;GUI.color=c;GUI.DrawTexture(new Rect(from.x,from.y-wide*.5f,length,wide),lineTexture,ScaleMode.StretchToFill,true);
            }
            GUI.matrix=matrix;GUI.color=keep;
        }
    }

    public sealed partial class ConversionGame
    {
        // The first-person sprint FOV kick rides on top of the camera's own FOV ease (UpdateCamera) and stays out of the view-model
        // camera, so the hands keep their framing while the world widens. The shoulder cameras take the same kick and bob from
        // the view pilot's sprint (NativeShoulderCamera.cs); Classic keeps none, as before.
        private float sprintKick;
        // settled: the camera's FOV eased toward its target from the last frame's FOV without its kick (FieldOfView - sprintKick).
        private float SprintFov(bool first,bool aim,float settled)
        {
            sprintKick=first&&!aim&&!menu?presentation.SprintFov(prefs.cameraShake,prefs.reducedEffects):!first&&!aim?ShoulderSprintFov():0;return settled+sprintKick;
        }
        private void SprintSway()
        {
            if(!menu&&Spectator==null&&!cinematic.Directing){presentation.SprintSway(cameraView.transform,prefs.cameraShake,prefs.reducedEffects);ShoulderSway();}
        }
    }
}
