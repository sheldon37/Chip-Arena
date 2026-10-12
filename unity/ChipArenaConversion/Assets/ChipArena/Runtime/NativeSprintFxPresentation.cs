using System.Collections.Generic;
using UnityEngine;
using NVector=System.Numerics.Vector3;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Draws the sprint's third-person effect (Core/NativeSprintFx) through the shared effects renderer: each sprinting pilot
    /// (the replicated Actor.Sprinting flag, grounded, travelling, not busy: Core/NativeSprintRules.Shows) sheds short accent
    /// speed streaks off its shoulders, hips, head and back, and kicks a little dust at each footfall. Streaks are camera-facing
    /// ribbons and dust is camera-facing puffs: nothing lies flat on the floor. The view pilot's own effect is skipped in first
    /// person (the screen lines and vignette show it there). Reduced effects and Low graphics thin and dim it. Presentation
    /// only: host, guests, killcam and spectators each draw it from the pilots they present.
    /// </summary>
    public sealed class NativeSprintFxPresentation
    {
        private struct Pilot{public float Ramp,Distance;public int Foot,Seed;public Vector3 At;public bool Seen;}
        private struct Kick{public float At,Floor;public Vector3 Foot,Travel;public int Seed;}
        private readonly NativeVfxDraw vfx;
        private readonly Dictionary<int,Pilot> pilots=new Dictionary<int,Pilot>();
        private readonly List<Kick> kicks=new List<Kick>(24);
        private NativeArena last;private int generation=-1,seeds;private float lastTime=-1;
        public int Live=>kicks.Count;
        public NativeSprintFxPresentation(NativeVfxDraw vfx){this.vfx=vfx;}
        public void Clear(){kicks.Clear();pilots.Clear();lastTime=-1;}
        /// <summary>now: the snapshot clock (ArenaPresentation.FieldClock); quality: the effects quality (1, .3 Reduced).</summary>
        public void Render(NativeArena arena,float now,float quality,bool low,int viewId,bool firstPerson)
        {
            if(arena==null||vfx==null)return;
            if(arena!=last||generation!=arena.Generation||now<lastTime-.25f){Clear();last=arena;generation=arena.Generation;}
            float dt=lastTime<0?0:Mathf.Clamp(now-lastTime,0,.05f);lastTime=Mathf.Max(lastTime,now);
            int streaks=NativeSprintFx.Count(NativeSprintFx.Streaks,NativeSprintFx.ReducedStreaks,quality,low);float shade=NativeSprintFx.Dim(quality);
            foreach(var p in arena.Pilots)
            {
                if(p?.Body==null||p.Pose==null)continue;var a=p.Pose;int id=p.Seat.id;pilots.TryGetValue(id,out var s);
                Vector3 feet=p.Body.transform.position-Vector3.up*.865f;bool on=NativeSprintRules.Shows(a,now);
                if(!s.Seen){s=new Pilot{Seen=true,Ramp=on?1:0,At=feet,Seed=++seeds};}
                s.Ramp=NativeSprintRules.Step(s.Ramp,on,dt);float weight=NativeSprintRules.Shape(s.Ramp);
                Vector3 travel=new Vector3(a.Velocity.x,0,a.Velocity.z);float speed=travel.magnitude;travel=speed>.1f?travel/speed:Vector3.zero;
                // Footfalls every StepLength of grounded sprint travel (the gait's step at sprint speed), alternating feet.
                Vector3 moved=feet-s.At;moved.y=0;float step=moved.magnitude;s.At=feet;
                if(!on||step>NativeFootsteps.MaxStep)s.Distance=0;
                else{s.Distance+=step;if(s.Distance>=NativeSprintRules.StepLength){s.Distance-=NativeSprintRules.StepLength;s.Foot^=1;
                    if(!(firstPerson&&id==viewId)){Vector3 side=new Vector3(travel.z,0,-travel.x)*(s.Foot==0?-.12f:.12f);if(kicks.Count>=24)kicks.RemoveAt(0);kicks.Add(new Kick{At=now,Foot=feet+side,Floor=feet.y,Travel=travel,Seed=++seeds});}}}
                pilots[id]=s;
                if(weight<=1e-3f||speed<=.1f||firstPerson&&id==viewId)continue;
                var tint=NativeGlowRules.ArmorTint(a.AccentColor);Color accent=new Color(tint.X,tint.Y,tint.Z,0)*(NativeSprintFx.StreakGain*shade);
                var f=new NVector(feet.x,feet.y,feet.z);var d=new NVector(travel.x,0,travel.z);
                for(int k=0;k<streaks;k++)
                    if(NativeSprintFx.Streak(k,s.Seed,now,weight,f,d,d,out var head,out var tail,out float alpha))
                        vfx.Streak(new Vector3(head.X,head.Y,head.Z),-new Vector3(tail.X,tail.Y,tail.Z),accent*alpha,NativeSprintFx.StreakWidth,tail.Length(),1,.9f,alpha,s.Seed*29+k);
            }
            for(int i=kicks.Count-1;i>=0;i--)if(now-kicks[i].At>=NativeSprintFx.DustLife||now<kicks[i].At-.25f)kicks.RemoveAt(i);
            int puffs=NativeSprintFx.Count(NativeSprintFx.DustPuffs,NativeSprintFx.ReducedDustPuffs,quality,low);
            var dust=NativePortraitRules.Linear(NativeSprintFx.DustColor);Color albedo=new Color(dust.X,dust.Y,dust.Z,0);
            foreach(var kick in kicks)
            {
                float age=now-kick.At;var foot=new NVector(kick.Foot.x,kick.Foot.y,kick.Foot.z);var travel=new NVector(kick.Travel.x,0,kick.Travel.z);
                for(int k=0;k<puffs;k++)
                    if(NativeSprintFx.Dust(k,kick.Seed,age,foot,travel,out var at,out float size,out float opacity))
                        vfx.Smoke(new Vector3(at.X,at.Y,at.Z),albedo,opacity*shade,size,age/NativeSprintFx.DustLife,kick.Seed*13+k,kick.Floor);
            }
        }
    }

    public sealed partial class ArenaPresentation
    {
        // The sprint's effects (owner 2026-10-12): the third-person streaks and dust (NativeSprintFxPresentation, queued with the
        // jump burst in RenderGlow), the first-person screen lines and vignette (OnGUI below), and the first-person camera's
        // FOV kick and bob (ConversionGame.UpdateCamera and LateFrame through SprintFov and SprintSway). The first-person weight
        // is the view layer's (NativeViewSway.Sprint: the view pilot's replicated sprint, faded out by a swing, block or roll).
        private NativeSprintFxPresentation sprintFx;
        private float screenSprint,screenClock;private int screenFrame=-1;private Color screenTint;
        private static Texture2D vignetteTexture,lineTexture;
        private void SprintFx(NativeArena arena,float now)
        {
            if(arena==null)return;
            if(vfx!=null){sprintFx??=new NativeSprintFxPresentation(vfx);sprintFx.Render(arena,now,WeaponEffectsQuality,LowGraphics,arena.ViewPilot?.Seat.id??-1,FirstPerson);}
            var view=arena.ViewPilot?.Pose;
            screenSprint=FirstPerson&&viewRig!=null&&!ScopeActive&&view!=null&&view.Alive?viewSway.Sprint:0;screenFrame=Time.frameCount;screenClock=now;
            if(view!=null){var t=NativeSprintFx.Tint(view.SourceSpec,view.AccentColor);screenTint=new Color(t.X,t.Y,t.Z,1).gamma;}
        }
        /// <summary>The first-person FOV kick (degrees) for the view pilot's sprint: none with camera shake off, half with Reduced
        /// effects (Core/NativeSprintRules.Fov).</summary>
        public float SprintFov(bool shake,bool reduced)=>FirstPerson&&viewRig!=null?NativeSprintRules.Fov(viewSway.Sprint,shake,reduced):0;
        /// <summary>The first-person camera's sprint bob and roll at the view stride (camera shake only; applied after the aim
        /// point is taken, as the other camera shakes are, and rebuilt from the look angles next frame).</summary>
        public void SprintSway(Transform camera,bool shake,bool reduced)
        {
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
                Color c=screenTint;c.a=alpha*dim;GUI.color=c;GUI.DrawTexture(new Rect(from.x,from.y-width*.5f,length,width),lineTexture,ScaleMode.StretchToFill,true);
            }
            GUI.matrix=matrix;GUI.color=keep;
        }
    }

    public sealed partial class ConversionGame
    {
        // The first-person sprint FOV kick rides on top of the camera's own FOV ease (UpdateCamera) and stays out of the view-model
        // camera, so the hands keep their framing while the world widens.
        private float sprintKick;
        // settled: the camera's FOV eased toward its target from the last frame's FOV without its kick (FieldOfView - sprintKick).
        private float SprintFov(bool first,bool aim,float settled)
        {
            sprintKick=first&&!aim&&!menu?presentation.SprintFov(prefs.cameraShake,prefs.reducedEffects):0;return settled+sprintKick;
        }
        private void SprintSway()
        {
            if(!menu&&Spectator==null&&!cinematic.Directing)presentation.SprintSway(cameraView.transform,prefs.cameraShake,prefs.reducedEffects);
        }
    }
}
