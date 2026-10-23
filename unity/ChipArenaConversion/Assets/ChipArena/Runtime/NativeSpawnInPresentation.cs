using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using NVector=System.Numerics.Vector3;
using NVector4=System.Numerics.Vector4;
using U=ChipArena.UnityConversion.NativeUi;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The match-start spawn-in (Core/NativeSpawnIn.cs; docs/SPAWN-IN.md), drawn from what every peer presents:
    ///  - Render (ArenaPresentation.cs): each pilot's presented feet are raised to the beam's hold (the float, held on purpose,
    ///    then the drop or the lowering), and its rig is posed on the ground while the beam holds it (no Fall loop) and in the
    ///    air for the drop (the jump layer lands it); the replicated flag and position are restored after posing.
    ///  - SourceCharacter (NativeSourcePresentation.cs): the body, its hat and its weapon form part by part from the feet up
    ///    (per-bone dithered reveal from the posed rig's part heights), tinted in the team colour (the pilot's accent outside
    ///    a teams match, and always for my own pilot, as the bands are); a hologram slice of the pilot's own body skin leads
    ///    the solid front; the completion flashes and splits into magenta and cyan. Body Chips, weapon dressing, the Cyber
    ///    Lord's hardlight look, team bands and the armour halos wait for their parts.
    ///  - RenderGlow: the beam column, its strike and release, the floor ring and tick pulses, the glow pool, the motes, the
    ///    scan ring and the landing's impact rings in one additive mesh (NativeGlow.shader), the landing's dust and sparks in
    ///    the shared effects renderer.
    ///  - ConversionGame: my own camera rides the hold and the drop in first person and kicks at the touchdown (camera shake
    ///    on; Reduced .4), and my first-person screen gets the scanline wipe, veil, vignette, edge split and GO flash.
    /// One additive mesh, lists and per-pilot arrays reused; cached delegates; nothing allocates per frame once a pilot is seen.
    /// Presentation only: the simulation, its timing, hitboxes, cooldowns, movement and the network are untouched.
    /// </summary>
    public sealed partial class ArenaPresentation
    {
        private sealed class SpawnView
        {
            public NativeArena Arena;public double End=double.NaN;public SpawnInPlace Place;public SpawnInFrame Frame;public bool Shown,Built;public float Raise;
            public Color Holo,Tint,Display;
            public readonly float[] Solid=new float[CapturedRig.Bones.Length],Ghost=new float[CapturedRig.Bones.Length],Heat=new float[CapturedRig.Bones.Length];
        }
        private sealed class SpawnGeometry:ISpawnInSink
        {
            public readonly List<Vector3> Points=new List<Vector3>(2048),Uv=new List<Vector3>(2048);public readonly List<Color> Colors=new List<Color>(2048);public readonly List<int> Indices=new List<int>(6144);
            public int Count=>Points.Count;
            public int Vertex(NVector p,NVector uv,NVector4 c){Points.Add(new Vector3(p.X,p.Y,p.Z));Uv.Add(new Vector3(uv.X,uv.Y,uv.Z));Colors.Add(new Color(c.X,c.Y,c.Z,c.W));return Points.Count-1;}
            public void Triangle(int a,int b,int c){Indices.Add(a);Indices.Add(b);Indices.Add(c);}
            public void Clear(){Points.Clear();Uv.Clear();Colors.Clear();Indices.Clear();}
        }
        private readonly Dictionary<int,SpawnView> spawnViews=new Dictionary<int,SpawnView>();
        private readonly SpawnGeometry spawnGeometry=new SpawnGeometry();
        private Mesh spawnMesh;private Material spawnMaterial;private bool spawnFailed;
        // SourceCharacter's pilot this call, and the delegates its draws read (made once).
        private SpawnView spawnNow;private Func<int,float> spawnReveal,spawnGhost;private Func<int,Color> spawnTint,spawnBaseTint;private Func<int,int,float> spawnJoint;
        private CapturedAnimation.Player spawnRig;private Vector3 spawnOffset;private Func<int,Matrix4x4> spawnBone;
        private NativeSourceModels spawnGhostBank;private readonly string[] spawnGhosts=new string[NativeBodySkins.Count*NativeBodySkins.Tiers];
        private static readonly Color SpawnDust=new Color(.62f,.66f,.72f,1);

        private void ClearSpawnIn(){spawnViews.Clear();spawnGeometry.Clear();spawnNow=null;spawnRig=null;}
        private void DisposeSpawnIn(){ClearSpawnIn();if(spawnMesh!=null)CapturedAnimation.Release(spawnMesh);if(spawnMaterial!=null)CapturedAnimation.Release(spawnMaterial);spawnMesh=null;spawnMaterial=null;}
        private static NVector SpawnN(Vector3 v)=>new NVector(v.x,v.y,v.z);
        private static NativePilot SpawnPilotOf(NativeArena arena,int id){if(arena==null)return null;foreach(var p in arena.Pilots)if(p?.Seat!=null&&p.Seat.id==id)return p;return null;}

        // ---- the pilot's spawn-in this frame ----
        /// <summary>
        /// The spawn-in of `pilot` (its presented feet `feet`) at presentation time `time`, placed on its first sight in a new
        /// fight start; null when none shows. `raise`: how far the beam holds its feet over where they are presented.
        /// </summary>
        private SpawnView SpawnEvaluate(NativeArena arena,NativePilot pilot,Vector3 feet,float time,out float raise)
        {
            raise=0;if(arena==null||pilot?.Seat==null||sourceModels==null||Viewer||pilot.Hp<=0||!pilot.Seat.connected||pilot.Seat.spec=="siren"||pilot.Seat.AuthorityBoss)return null;
            int id=pilot.Seat.id;var m=arena.Match;if(m==null)return null;
            // A live guest's own pilot is predicted ahead of the snapshot clock: its drop starts when its controls unlock.
            bool predicted=arena.Replica&&!arena.ReplicaScene.IsValid()&&id==arena.LocalId;
            float age=NativeSpawnIn.Age(m.Phase,m.PhaseEnd,m.FightAt,time+(predicted?NativeSpawnIn.GuestLead:0));
            if(float.IsNaN(age)||age>.3f+NativeSpawnIn.LandSeconds)return null;
            double end=NativeSpawnIn.End(m.Phase,m.PhaseEnd,m.FightAt);
            if(!spawnViews.TryGetValue(id,out var v)){v=new SpawnView();spawnViews.Add(id,v);}
            if(v.Arena!=arena||double.IsNaN(v.End)||Math.Abs(v.End-end)>.25)SpawnPlaceView(v,arena,pilot,feet,end);
            v.Frame=NativeSpawnIn.Evaluate(age,v.Place.Mode,v.Place.Hold,v.Place.Drop,v.Place.Seed,WeaponEffectsQuality<.5f);
            if(!v.Frame.Active)return null;
            float target=v.Place.Floor+NativeSpawnIn.RestFeet+v.Frame.Lift;raise=v.Frame.Lift>0&&feet.y<target?target-feet.y:0;
            return v;
        }
        // Once per spawn-in: the floor under the spawn (and the ceiling over it), the authored spawn's height (the mode, hold
        // and drop), the seed both peers agree on and the beam colour.
        private void SpawnPlaceView(SpawnView v,NativeArena arena,NativePilot pilot,Vector3 feet,double end)
        {
            var physics=arena.Root!=null?arena.Root.scene.GetPhysicsScene():default;bool query=arena.Root!=null&&physics.IsValid();var map=arena.Map;
            float centre=feet.y+NativeSpawnIn.FeetOffset;if(NativeSpawnIn.AuthoredSpawn(arena.Content,map,feet.x,feet.z,out float authored))centre=authored;
            // The floor the arena's colliders hold under the spawn, else the map's authored blocks, else the presented rest.
            float floor=feet.y-NativeSpawnIn.RestFeet,top=float.NaN;
            if(query&&physics.Raycast(feet+Vector3.up*1.2f,Vector3.down,out var ground,4,NativeArena.WorldMask,QueryTriggerInteraction.Ignore))floor=ground.point.y;
            else if(NativeSpawnIn.AuthoredFloor(map,feet.x,feet.z,centre,out float authoredFloor))floor=authoredFloor;
            if(query&&physics.Raycast(new Vector3(feet.x,floor+2.2f,feet.z),Vector3.up,out var roof,14,NativeArena.WorldMask,QueryTriggerInteraction.Ignore))top=roof.point.y-.05f;
            if(float.IsNaN(top))top=floor+16;
            // The team colour (the bands' rule: none outside a teams match, never on my own pilot), else the pilot's accent.
            var m=arena.Match;int band=NativeStatusVisuals.PilotBand(m.Teams,m.Cooperative,pilot.Seat.AuthorityBoss,pilot.Seat.team,pilot.Seat.id==arena.LocalId);
            var lin=NativePortraitRules.Linear(band>=0?NativeStatusVisuals.TeamColors[band]:NativePortraitRules.Parse(pilot.Seat.accentColor,NativePortraitRules.ExportAccent));
            float max=Math.Max(lin.X,Math.Max(lin.Y,lin.Z));lin=max>.02f?lin/max:NativePortraitRules.Linear(NativeCyberTheme.CyanHex);
            var linear=new Color(lin.X,lin.Y,lin.Z,1);v.Holo=linear*1.25f;v.Holo.a=1;v.Tint=new Color(lin.X*1.9f,lin.Y*1.9f,lin.Z*1.9f,1);v.Display=linear.gamma;
            var drawn=QualitySettings.activeColorSpace==ColorSpace.Linear?linear:linear.gamma;
            v.Place=NativeSpawnIn.Place(SpawnN(feet),floor,top,centre,NativeSpawnIn.Seed(pilot.Seat.id,end),new NVector(drawn.r,drawn.g,drawn.b));
            v.Arena=arena;v.End=end;v.Built=false;for(int b=0;b<v.Solid.Length;b++){v.Solid[b]=0;v.Ghost[b]=0;v.Heat[b]=1;}
        }
        private void SpawnBegin(){foreach(var v in spawnViews.Values)v.Shown=false;}
        /// <summary>Render: actor `a`'s presented position with its feet held by the beam (unchanged without a spawn-in).</summary>
        private Vector3 SpawnPlace(Actor a,Vector3 position,float time)
        {
            var arena=WeaponLoadout;if(arena==null||a==null||!a.Alive)return position;
            var v=SpawnEvaluate(arena,SpawnPilotOf(arena,a.Id),position,time,out float raise);if(v==null)return position;
            v.Shown=true;v.Raise=raise;position.y+=raise;return position;
        }
        /// <summary>The contact flag actor `id`'s rig is posed with: on the ground while the beam holds it, in the air for the
        /// drop, else the replicated flag.</summary>
        private bool SpawnContact(int id,bool replicated)
        {
            if(!spawnViews.TryGetValue(id,out var v)||!v.Shown)return replicated;
            return v.Frame.Held||!v.Frame.Falling&&replicated;
        }
        /// <summary>After posing: each part's height over the presented feet against the build front.</summary>
        private void SpawnBones(Actor a)
        {
            if(!spawnViews.TryGetValue(a.Id,out var v)||!v.Shown||!rigs.TryGetValue(a.Id,out var rig)||rig?.Root==null)return;
            var f=v.Frame;v.Built=f.Built>=1;if(v.Built)return;
            var root=rig.Root.transform;float feet=root.position.y-NativeSpawnIn.FeetOffset*root.localScale.y;int n=Math.Min(v.Solid.Length,rig.Bones.Length);
            for(int b=0;b<n;b++)
            {
                int child=NativeSpawnIn.PartChild(b);float h=NativeSpawnIn.PartHeight(b,rig.Bones[b].position.y,child>=0&&child<n?rig.Bones[child].position.y:0)-feet;
                v.Solid[b]=NativeSpawnIn.Solid(f.Front,h);v.Ghost[b]=NativeSpawnIn.Ghost(f.Front,h)*NativeSpawnIn.GhostOpacity;v.Heat[b]=NativeSpawnIn.Heat(f.Front,h);
            }
        }

        // ---- SourceCharacter's hooks ----
        /// <summary>The spawn-in a SourceCharacter call draws: an arena rig or my first-person view rig in a live spawn-in.</summary>
        private SpawnView SpawnFor(CapturedAnimation.Player rig,Actor a,bool first)
        {
            if(rig==null||a==null||!spawnViews.TryGetValue(a.Id,out var v)||!v.Shown)return null;
            if(first)return rig==viewRig?v:null;
            return rigs.TryGetValue(a.Id,out var own)&&own==rig?v:null;
        }
        /// <summary>SourceCharacter: the per-bone reveal while the body forms (the caller's own reveal, an entrance's, wins).</summary>
        private Func<int,float> SpawnReveal(SpawnView v,Func<int,float> reveal)=>reveal!=null||v==null||v.Built?reveal:spawnReveal??=SpawnSolidOf;
        private float SpawnSolidOf(int b){var v=spawnNow;return v==null||v.Built?1:v.Solid[b>=0&&b<v.Solid.Length?b:0];}
        private float SpawnGhostOf(int b){var v=spawnNow;return v==null||v.Built?0:v.Ghost[b>=0&&b<v.Ghost.Length?b:0];}
        /// <summary>A part (the hat's head, the weapon's hand, the Chips' chest) shows once it is half solid.</summary>
        private bool SpawnShows(int bone)=>spawnNow==null||spawnNow.Built||SpawnSolidOf(bone)>.5f;
        /// <summary>SourceCharacter: the hardlight tint over the rig's own (the block and Siren tints), hottest where it just formed.</summary>
        private Func<int,Color> SpawnTinted(SpawnView v,Func<int,Color> tint)
        {
            if(v==null||v.Frame.Tint<=1e-3f&&v.Frame.Flash<=1e-3f)return tint;spawnBaseTint=tint;return spawnTint??=SpawnTintOf;
        }
        private Color SpawnTintOf(int b)
        {
            var c=spawnBaseTint!=null?spawnBaseTint(b):Color.white;var v=spawnNow;if(v==null)return c;
            float heat=v.Heat[b>=0&&b<v.Heat.Length?b:0],k=Mathf.Clamp01(v.Frame.Tint*(.35f+.65f*heat))*.8f,flash=v.Frame.Flash*.7f;
            var t=Color.Lerp(Color.white,v.Tint,k);t=Color.Lerp(t,new Color(2.2f,2.3f,2.5f,1),flash);return new Color(c.r*t.r,c.g*t.g,c.b*t.b,c.a);
        }
        /// <summary>
        /// SourceCharacter, for a pilot its visual body draws (NativeVisualBodyPresentation.cs VisualBodyLook): this frame's
        /// spawn-in for its shader to apply per pixel (Resources/NativeVisualBodyFx.cginc) as the voxel parts take it per part.
        /// The solid front's world height over the presented feet, with the band, heat span and slice lead; the hardlight tint
        /// and its share; the completion flash, the RGB split (Full effects) and its copies' opacity, the scan clock; the
        /// hologram's colour and slice opacity, and whether the hologram draws (never on the low graphics tier, as
        /// SpawnHologram). False when no spawn-in shows.
        /// </summary>
        private bool SpawnVisualLook(SpawnView v,CapturedAnimation.Player rig,ref VisualSpawnLook look)
        {
            if(v==null||rig?.Root==null)return false;var f=v.Frame;var root=rig.Root.transform;
            float feet=root.position.y-NativeSpawnIn.FeetOffset*root.localScale.y;bool holo=!LowGraphics;float split=holo&&WeaponEffectsQuality>=.5f?f.Split:0;
            look.Front=new Vector4(v.Built?1e5f:feet+f.Front-NativeSpawnIn.SolidLag,NativeSpawnIn.Band,NativeSpawnIn.HeatSpan,NativeSpawnIn.SolidLag+NativeSpawnIn.GhostLead);
            look.Tint=new Vector4(v.Tint.r,v.Tint.g,v.Tint.b,f.Tint);
            look.Glitch=new Vector4(f.Flash,split,split>1e-4f?.42f*Mathf.Clamp01(f.Split/NativeSpawnIn.SplitMax+.4f):0,f.Age);
            look.Holo=new Vector4(v.Holo.r,v.Holo.g,v.Holo.b,holo&&!v.Built?NativeSpawnIn.GhostOpacity:0);
            look.Ghost=holo&&(!v.Built||f.Flash>.01f||split>1e-4f);
            return true;
        }
        /// <summary>SourceCharacter (arena rigs): the hologram slice leading the solid body, the completion's flash silhouette and
        /// the RGB split, in the pilot's own body skin at its tier (the ghost shader; none on the low graphics tier).</summary>
        private void SpawnHologram(CapturedAnimation.Player rig,SpawnView v,int skin,int layer,Camera camera)
        {
            if(v==null||rig?.Root==null||LowGraphics)return;var f=v.Frame;bool slice=!v.Built,flash=f.Flash>.01f,split=f.Split>1e-4f&&WeaponEffectsQuality>=.5f;
            if(!slice&&!flash&&!split)return;
            string model=SpawnGhostModel(skin,BodyTier(rig.Root.transform.position,false));if(model==null)return;
            spawnRig=rig;spawnOffset=Vector3.zero;spawnBone??=SpawnBoneOf;var root=rig.Root.transform.localToWorldMatrix;var scan=new Color(1,f.Age,0,0);
            if(slice)sourceModels.Queue(model,root,spawnBone,null,layer,camera,v.Holo,scan,1,partOpacity:spawnGhost??=SpawnGhostOf);
            if(flash)sourceModels.Queue(model,root,spawnBone,null,layer,camera,new Color(.8f,1,1.1f,1),new Color(0,f.Age,0,0),f.Flash*.55f);
            if(split)
            {
                var eye=WorldEye;Vector3 across=eye!=null?eye.transform.right:Vector3.right;float a=.42f*Mathf.Clamp01(f.Split/NativeSpawnIn.SplitMax+.4f);
                NativeBlastSim.Linear(NativeCyberTheme.MagentaHex,out double mr,out double mg,out double mb);NativeBlastSim.Linear(NativeCyberTheme.CyanHex,out double cr,out double cg,out double cb);
                spawnOffset=across*f.Split;sourceModels.Queue(model,root,spawnBone,null,layer,camera,new Color((float)mr,(float)mg,(float)mb,1),scan,a);
                spawnOffset=-across*f.Split;sourceModels.Queue(model,root,spawnBone,null,layer,camera,new Color((float)cr,(float)cg,(float)cb,1),scan,a);
                spawnOffset=Vector3.zero;
            }
        }
        private Matrix4x4 SpawnBoneOf(int b)
        {
            var rig=spawnRig;if(rig==null||b<0||b>=rig.Bones.Length)return Matrix4x4.identity;var m=rig.Bones[b].localToWorldMatrix;
            if(spawnOffset.sqrMagnitude>0){m.m03+=spawnOffset.x;m.m13+=spawnOffset.y;m.m23+=spawnOffset.z;}return m;
        }
        private string SpawnGhostModel(int skin,int tier)
        {
            if(sourceModels==null)return null;if(spawnGhostBank!=sourceModels){spawnGhostBank=sourceModels;Array.Clear(spawnGhosts,0,spawnGhosts.Length);}
            int k=Mathf.Clamp(skin,0,NativeBodySkins.Count-1)*NativeBodySkins.Tiers+Mathf.Clamp(tier,0,NativeBodySkins.Tiers-1);
            if(spawnGhosts[k]==null)spawnGhosts[k]=sourceModels.EntranceGhost(BodyModel(skin,tier))??"";return spawnGhosts[k].Length==0?null:spawnGhosts[k];
        }
        /// <summary>RenderStatus and the combat glow: whether pilot `id`'s part `bone` has formed (its band, its armour halo).</summary>
        private float SpawnJointOf(int id,int bone)=>!spawnViews.TryGetValue(id,out var v)||!v.Shown||v.Built?1:v.Solid[bone>=0&&bone<v.Solid.Length?bone:0];
        private bool SpawnShowsFor(int id,int bone)=>SpawnJointOf(id,bone)>.5f;
        /// <summary>The overhead health bar waits for the body (NativeCombatHud).</summary>
        public bool SpawnHidesHud(int id)=>spawnViews.TryGetValue(id,out var v)&&v.Shown&&!v.Built;

        // ---- the beam, the floor, the landing ----
        private void SpawnInFx(NativeArena arena,Camera camera)
        {
            spawnGeometry.Clear();if(arena==null||camera==null||spawnViews.Count==0)return;
            var tier=NativeSpawnIn.Tier(WeaponEffectsQuality,LowGraphics);var cam=camera.transform;var eye=SpawnN(cam.position);var right=SpawnN(cam.right);var up=SpawnN(cam.up);int view=arena.ViewPilot?.Seat.id??-1;
            foreach(var pair in spawnViews)
            {
                var v=pair.Value;if(!v.Shown||v.Arena!=arena)continue;var f=v.Frame;
                Vector3 feet=visualPositions.TryGetValue(pair.Key,out var at)?at:new Vector3(v.Place.Centre.X,v.Place.Floor,v.Place.Centre.Z);
                NativeSpawnIn.Draw(spawnGeometry,f,v.Place,SpawnN(feet),eye,right,up,tier,!(FirstPerson&&pair.Key==view));
                if(vfx==null||f.Land<0)continue;
                var hot=Color.Lerp(new Color(v.Holo.r,v.Holo.g,v.Holo.b,0),new Color(1,1,1,0),.45f);var dust=Color.Lerp(SpawnDust,new Color(v.Display.r,v.Display.g,v.Display.b,1),.15f);
                for(int k=0;k<tier.Dust;k++)if(NativeSpawnIn.Dust(k,tier.Dust,v.Place,f.Land,out var d,out float size,out float opacity))
                    vfx.SoftSmoke(new Vector3(d.X,d.Y,d.Z),dust,opacity,size,f.Land/NativeSpawnIn.DustLife,v.Place.Seed*13+k,v.Place.Floor);
                for(int k=0;k<tier.Sparks;k++)if(NativeSpawnIn.Spark(k,tier.Sparks,v.Place,f.Land,out var head,out var velocity,out float fade))
                    vfx.Streak(new Vector3(head.X,head.Y,head.Z),new Vector3(velocity.X,velocity.Y,velocity.Z)*.04f,hot*(2.2f*fade*tier.Gain),.03f,.1f,1,.8f,f.Land/.35f,v.Place.Seed*31+k);
            }
            if(spawnGeometry.Count==0||!EnsureSpawnMesh())return;
            spawnMesh.Clear();spawnMesh.SetVertices(spawnGeometry.Points);spawnMesh.SetColors(spawnGeometry.Colors);spawnMesh.SetUVs(0,spawnGeometry.Uv);spawnMesh.SetTriangles(spawnGeometry.Indices,0);spawnMesh.RecalculateBounds();
            Graphics.DrawMesh(spawnMesh,Matrix4x4.identity,spawnMaterial,0,camera,0,null,ShadowCastingMode.Off,false);
        }
        private bool EnsureSpawnMesh()
        {
            if(spawnMaterial!=null&&spawnMesh!=null)return true;if(spawnFailed)return false;
            var shader=Resources.Load<Shader>("NativeGlow");if(shader==null||!shader.isSupported){spawnFailed=true;return false;}
            spawnMaterial=new Material(shader){name="Native spawn-in beam"};spawnMesh=new Mesh{name="Native spawn-in",indexFormat=IndexFormat.UInt32};spawnMesh.MarkDynamic();return true;
        }

        // ---- my own camera and screen ----
        /// <summary>
        /// ConversionGame.LateFrame (camera shake on): in first person my view rides the beam's hold, bob and drop with my body;
        /// the touchdown kicks the camera in first or third person (Reduced .4). Applied after the aim point is taken, as the
        /// other camera kicks are, and rebuilt from the look angles next frame.
        /// </summary>
        public void SpawnCamera(Transform camera,NativeArena arena,float time,bool shake,bool reduced)
        {
            if(camera==null||arena?.Local?.Body==null||!shake)return;var p=arena.Local;
            var v=SpawnEvaluate(arena,p,p.Body.transform.position-Vector3.up*NativeSpawnIn.FeetOffset,time,out float raise);if(v==null)return;
            float kick=NativeSpawnIn.Kick(v.Frame,v.Place.Mode,out float pitch)*(reduced?.4f:1);
            camera.position+=Vector3.up*((FirstPerson?raise:0)-kick);if(pitch>0)camera.rotation*=Quaternion.Euler(pitch*(reduced?.4f:1),0,0);
        }
        /// <summary>My first-person spawn-in screen (ConversionGame.SpawnInScreen) and its tint (display colour).</summary>
        public bool SpawnScreenFor(int id,out SpawnInScreen screen,out Color tint)
        {
            screen=default;tint=Color.white;if(!FirstPerson||!spawnViews.TryGetValue(id,out var v)||!v.Shown)return false;
            screen=NativeSpawnIn.Screen(v.Frame,WeaponEffectsQuality<.5f);tint=v.Display;return screen.Any;
        }
    }

    public sealed partial class ConversionGame
    {
        private static readonly float[] SpawnVignetteAt={0,.5f,.82f,1},SpawnVignetteAlpha={0,0,.55f,1};
        /// <summary>
        /// My own spawn-in in first person, under the HUD: a veil over the screen above the scanline that wipes up with my body's
        /// build, a soft vignette in my colour while the beam holds me, an edge split at the completion and a gentle flash at GO.
        /// Reduced effects keeps only the softer veil, vignette and flash.
        /// </summary>
        private void SpawnInScreen()
        {
            if(menu||arena?.Local==null||presentation==null||ReplayActive||cinematic.Directing||Spectator!=null)return;
            if(!presentation.SpawnScreenFor(arena.Local.Seat.id,out var s,out var tint))return;
            float W=U.W,H=U.H,line=H*(1-s.Line);
            if(s.Veil>1e-3f){float band=Mathf.Min(28,line);U.Fill(new Rect(0,0,W,line-band),NativeCyberTheme.Navy(s.Veil));U.Fade(new Rect(0,line-band,W,band),NativeCyberTheme.Navy(s.Veil),true);}
            if(s.LineGlow>1e-3f){U.Fade(new Rect(0,line,W,22),U.A(tint,.22f*s.LineGlow),true);U.Fill(new Rect(0,line-1,W,2),U.A(Color.Lerp(tint,Color.white,.5f),.75f*s.LineGlow));}
            if(s.Vignette>1e-3f)U.RadialStops(new Rect(-W*.1f,-H*.1f,W*1.2f,H*1.2f),U.A(tint,s.Vignette),SpawnVignetteAt,SpawnVignetteAlpha);
            if(s.Split>1e-3f){float a=.28f*s.Split;U.HGradient(new Rect(0,0,W*.14f,H),NativeCyberTheme.Magenta(a),Color.clear);U.HGradient(new Rect(W*.86f,0,W*.14f,H),Color.clear,NativeCyberTheme.Cyan(a));}
            if(s.Flash>1e-3f)U.Fill(new Rect(0,0,W,H),U.A(Color.Lerp(tint,Color.white,.6f),s.Flash));
        }
    }
}
