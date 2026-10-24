using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using NVector=System.Numerics.Vector3;
using C=ChipArena.UnityConversion.NativeCyberTheme;
using H=ChipArena.UnityConversion.NativeHudChrome;
using U=ChipArena.UnityConversion.NativeUi;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// A dynamic mesh of Core RoundsFieldVertex (NativeRoundsFields: the field surfaces, or the distortion spheres) drawn with one
    /// material, layer 0, no shadows: the vertex array uploaded as Core built it with the advanced Mesh API, only the used range.
    /// One draw. Allocation free per frame.
    /// </summary>
    internal sealed class NativeRoundsSurfaceDraw:IDisposable
    {
        private static readonly VertexAttributeDescriptor[] Layout=
        {
            new VertexAttributeDescriptor(VertexAttribute.Position,VertexAttributeFormat.Float32,3),new VertexAttributeDescriptor(VertexAttribute.Normal,VertexAttributeFormat.Float32,3),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0,VertexAttributeFormat.Float32,4),new VertexAttributeDescriptor(VertexAttribute.TexCoord1,VertexAttributeFormat.Float32,4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord2,VertexAttributeFormat.Float32,4)
        };
        private const MeshUpdateFlags Quiet=MeshUpdateFlags.DontValidateIndices|MeshUpdateFlags.DontRecalculateBounds|MeshUpdateFlags.DontNotifyMeshUsers|MeshUpdateFlags.DontResetBoneBounds;
        private static readonly Bounds Everywhere=new Bounds(Vector3.zero,Vector3.one*4000);
        private readonly Mesh mesh;private readonly Material material;private int vertexCapacity=-1,indexCapacity=-1;
        private NativeRoundsSurfaceDraw(Shader shader,string name)
        {
            material=new Material(shader){name=name,hideFlags=HideFlags.HideAndDontSave};
            mesh=new Mesh{name=name,hideFlags=HideFlags.HideAndDontSave};mesh.MarkDynamic();
        }
        /// <summary>The draw, or null when the shader is missing or unsupported (that layer is then not drawn).</summary>
        public static NativeRoundsSurfaceDraw Create(string shader,string name){var s=Resources.Load<Shader>(shader);return s!=null&&s.isSupported?new NativeRoundsSurfaceDraw(s,name):null;}
        public bool Draw(RoundsSurface m,Camera camera)
        {
            if(m==null||m.VertexCount==0||m.IndexCount==0||camera==null)return false;
            if(vertexCapacity!=m.V.Length){mesh.SetVertexBufferParams(m.V.Length,Layout);vertexCapacity=m.V.Length;}
            if(indexCapacity!=m.T.Length){mesh.SetIndexBufferParams(m.T.Length,IndexFormat.UInt32);indexCapacity=m.T.Length;mesh.subMeshCount=1;}
            mesh.SetVertexBufferData(m.V,0,0,m.VertexCount,0,Quiet);mesh.SetIndexBufferData(m.T,0,0,m.IndexCount,Quiet);
            mesh.SetSubMesh(0,new SubMeshDescriptor(0,m.IndexCount){bounds=Everywhere,firstVertex=0,vertexCount=m.VertexCount},Quiet);mesh.bounds=Everywhere;
            Graphics.DrawMesh(mesh,Matrix4x4.identity,material,0,camera,0,null,ShadowCastingMode.Off,false);return true;
        }
        public void Dispose(){CapturedAnimation.Release(mesh);CapturedAnimation.Release(material);}
    }

    public sealed partial class ArenaPresentation
    {
        // The ROUNDS adaptation packs' bespoke visuals (Core NativeRoundsFx): the pack fields' surfaces, distortion and particles,
        // Escort Array's (and its siblings') satellites and homing shots, Drone Nest's combat drones, the swaps, and the procs and
        // buffs. Every input is replicated state (seat chips, chip fields, toy minions with their expiry and their death records with
        // their owner, events including the swap event, the pilots' shieldUntil and frozenUntil, shots, beams), so host, guests,
        // late joiners and the killcam draw the same; the DEV preview feeds stand-ins through the same frame. Queued into the shared
        // effects renderer in RenderGlow (before it submits); the field surfaces and the distortion are their own two draws.
        private NativeRoundsFx roundsFx;private NativeRoundsSurfaceDraw roundsGround,roundsWarp;private bool roundsGroundFailed,roundsWarpFailed;
        private NativeArena roundsArena;private int roundsGeneration=-1,roundsEvent;
        private readonly Dictionary<int,(Vector3 at,float floor)> roundsFloors=new Dictionary<int,(Vector3,float)>();
        private readonly RaycastHit[] roundsHits=new RaycastHit[16];private PhysicsScene roundsPhysics;private bool roundsWorld;
        private readonly Dictionary<int,(string hex,NVector rgb)> roundsAccent=new Dictionary<int,(string,NVector)>();
        /// <summary>Whether Drone Nest draws minion `id` (the toy soldier's rig, break and hit puffs then stand aside).</summary>
        internal static bool RoundsDrone(NativeMinion m)=>m!=null&&m.Pve==null&&m.Kind=="toy"&&NativeRoundsLooks.Drone(m.Owner?.Seat.chips);
        // Whether Drone Nest draws summon death record `d`: a toy's, of a drone this client saw or whose record names a Drone Nest
        // holder as its owner (a late joiner, a killcam that starts after the summon).
        internal bool RoundsDroneDeath(NativeArena arena,NativeDeath d)=>d!=null&&d.model=="summon-toy"&&(roundsFx!=null&&roundsFx.Units.Known(d.id)||RoundsDroneOwner(arena,d.owner)!=null);
        private static NativePilot RoundsDroneOwner(NativeArena arena,int seat){if(seat==0||arena==null)return null;var p=RoundsPilot(arena,seat);return p!=null&&NativeRoundsLooks.Drone(p.Seat.chips)?p:null;}
        private void ClearRoundsFx(){roundsFx?.Clear();roundsFloors.Clear();roundsEvent=0;roundsArena=null;roundsGeneration=-1;}
        private void DisposeRoundsFx(){roundsGround?.Dispose();roundsGround=null;roundsWarp?.Dispose();roundsWarp=null;}
        private float RoundsFloor(int key,Vector3 at)
        {
            if(roundsFloors.TryGetValue(key,out var known)&&(known.at-at).sqrMagnitude<.0004f)return known.floor;
            double f=roundsWorld?NativeBlastPresentation.FloorUnder(roundsPhysics,at,roundsHits):NativeBlastSim.NoFloor;float floor=f<=NativeBlastSim.NoFloor*.5?at.y-NativeRoundsFields.PilotCentre:(float)f;
            roundsFloors[key]=(at,floor);return floor;
        }
        private float RoundsFloorNow(Vector3 at){double f=roundsWorld?NativeBlastPresentation.FloorUnder(roundsPhysics,at,roundsHits):NativeBlastSim.NoFloor;return f<=NativeBlastSim.NoFloor*.5?at.y-NativeRoundsFields.PilotCentre:(float)f;}
        private static NVector RoundsVec(Vector3 v)=>new NVector(v.x,v.y,v.z);
        private static int HexDigit(char c)=>c>='0'&&c<='9'?c-'0':c>='a'&&c<='f'?c-'a'+10:c>='A'&&c<='F'?c-'A'+10:-1;
        // The owner's accent as a linear colour, parsed once per string (no allocation once seen).
        private NVector RoundsAccent(NativeSeat seat)
        {
            string s=seat?.accentColor;if(s==null)return new NVector(.1f,.85f,.75f);
            if(roundsAccent.TryGetValue(seat.id,out var c)&&ReferenceEquals(c.hex,s))return c.rgb;
            int hex=0,n=0;for(int i=0;i<s.Length&&n<6;i++){int d=HexDigit(s[i]);if(d<0)continue;hex=hex*16+d;n++;}
            var lin=NativeVfxDraw.Linear(hex);var rgb=n==6?new NVector(lin.r,lin.g,lin.b):new NVector(.1f,.85f,.75f);roundsAccent[seat.id]=(s,rgb);return rgb;
        }
        private static NativePilot RoundsPilot(NativeArena arena,int seat){var list=arena.Pilots;for(int i=0;i<list.Count;i++)if(list[i].Seat.id==seat)return list[i];return null;}
        // Whether a pilot holds any ROUNDS pack chip (the packs are optional and off by default: without one this frame costs a
        // pilot scan and nothing else, unless something is still fading out).
        private static bool RoundsHolder(NativePilot p){var chips=p?.Seat.chips;if(chips==null)return false;foreach(var kv in chips)if(kv.Value>0&&kv.Key.StartsWith("rounds-",StringComparison.Ordinal))return true;return false;}
        private void RoundsFx(NativeArena arena,Camera camera,float now)
        {
            if(vfx==null||arena==null)return;
            bool packs=false;for(int i=0;i<arena.Pilots.Count&&!packs;i++)packs=RoundsHolder(arena.Pilots[i]);
            if(!packs&&!NativeRoundsPreview.On&&(roundsFx==null||roundsFx.Idle))return;
            roundsFx??=new NativeRoundsFx();
            bool reset=arena!=roundsArena||arena.Generation!=roundsGeneration;if(reset){roundsArena=arena;roundsGeneration=arena.Generation;roundsEvent=0;roundsFloors.Clear();}
            roundsWorld=NativeBlastPresentation.World(arena,out roundsPhysics);
            var fx=roundsFx;fx.Begin(now,BlastQuality,LowGraphics,reset);
            // Events first (procs, drone summons), in order, once each.
            var events=arena.Events;
            for(int i=0;i<events.Count;i++)
            {
                var e=events[i];if(e.Id<=roundsEvent)continue;roundsEvent=e.Id;if(now-e.Time>1.5f)continue;
                var source=RoundsPilot(arena,e.Source);if(source==null)continue;var chips=source.Seat.chips;
                if(e.Kind==EventKind.Summon&&e.Detail=="toy"&&NativeRoundsLooks.Drone(chips)){fx.Units.Summoned(e.Target,e.Source,e.Time,new NVector(e.Position.x,e.Position.y,e.Position.z));continue;}
                var procs=NativeRoundsLooks.Procs(chips);if(procs==RoundsProc.None)continue;
                var target=e.Kind==EventKind.Hit||e.Kind==EventKind.Headshot||e.Kind==EventKind.Freeze?RoundsPilot(arena,e.Target):null;
                bool executes=target!=null&&target.Stats!=null&&target.Hp+e.Amount<target.Stats["hp"]*.3;
                bool ground=e.Kind==EventKind.BlockPulse||e.Kind==EventKind.Explosion;
                fx.Procs.Event(e,procs,target!=null,executes,NativeRoundsLooks.StasisLook(chips),ground?RoundsFloorNow(NativeArena.Vec(e.Position)):0);
            }
            // The pack fields (their effect is the vanilla mechanic; the owner's chips say which pack chip made it).
            var fields=arena.ChipFields;bool reacts=false;
            for(int i=0;i<fields.Count;i++)
            {
                var f=fields[i];int look=NativeRoundsLooks.FieldLook(f.Effect,f.Owner?.Seat.chips);if(look<0)continue;var st=NativeRoundsLooks.Looks[look].Style;reacts|=st==RoundsStyle.Time||st==RoundsStyle.Pull;
                fx.Fields.Observe(new RoundsFieldInput{Id=f.Id,Look=look,Centre=RoundsVec(f.Position),Floor=RoundsFloor(f.Id,f.Position),Radius=f.Radius,Born=f.Born,Until=f.Until},now);
            }
            if(roundsFloors.Count>256)roundsFloors.Clear();
            // Pilots: satellites, proc bodies, the freeze's replicated end.
            bool shots=reacts||NativeRoundsPreview.On;
            for(int i=0;i<arena.Pilots.Count;i++)
            {
                var p=arena.Pilots[i];if(p?.Body==null)continue;var chips=p.Seat.chips;int sat=NativeRoundsLooks.SatelliteLook(chips);var procs=NativeRoundsLooks.Procs(chips);shots|=sat>=0||(procs&RoundsProc.LastRound)!=0;
                fx.Pilot(p.Seat.id,RoundsVec(p.Body.transform.position),p.Yaw*Mathf.Deg2Rad,p.Hp>0&&p.Seat.connected,sat,procs,NativeRoundsLooks.StasisLook(chips),p.FrozenUntil,p.ShieldUntil);
            }
            // Drone Nest's toys (with their replicated expiry: a replica stores an unknown one as infinity), their death records (with
            // their owner: 0 from an old host) and their shot beams.
            for(int i=0;i<arena.Minions.Count;i++)
            {
                var m=arena.Minions[i];if(!RoundsDrone(m)||m.Hp<=0||m.Body==null)continue;var vel=arena.Replica?m.NetworkVelocity:m.Body.linearVelocity;
                fx.Units.Observe(new RoundsDroneInput{Id=m.Id,Owner=m.Owner.Seat.id,At=RoundsVec(m.Body.transform.position),Velocity=RoundsVec(vel),Yaw=m.Yaw*Mathf.Deg2Rad,LastAttack=m.LastAttack,LastHit=m.LastHitAt,Health=m.MaxHp>0?m.Hp/m.MaxHp:1,Accent=RoundsAccent(m.Owner.Seat),Until=float.IsInfinity(m.Until)?0:m.Until},now);
            }
            for(int i=0;i<arena.MinionDeaths.Count;i++){var d=arena.MinionDeaths[i];if(d.model!="summon-toy")continue;var owner=RoundsDroneOwner(arena,d.owner);fx.Units.Died(d.id,d.at,RoundsVec(d.position),d.yaw*Mathf.Deg2Rad,owner?.Seat.id??0,owner!=null?RoundsAccent(owner.Seat):default);}
            for(int i=0;i<arena.AbilityEffects.Count;i++){var b=arena.AbilityEffects[i];if(b.Beam&&b.Element==0&&b.Radius<.2f)fx.Units.Beam(new RoundsBeamInput{From=RoundsVec(b.From),To=RoundsVec(b.Position),Until=b.Until},now);}
            // Shots at their drawn positions: every one for the slow-time echoes and the gravity pull; homing shots (the satellites'
            // flashes and trails) and Final Cartridge's last round (a holder's shot first seen with its magazine empty).
            if(shots)for(int i=0;i<arena.Bullets.Count;i++)
            {
                var b=arena.Bullets[i];var v=new NativeShotView{Kind=NativeShotKind.Bullet,Id=b.Id,X=b.Position.x,Y=b.Position.y,Z=b.Position.z,VX=b.Velocity.x,VY=b.Velocity.y,VZ=b.Velocity.z,Radius=b.Radius,FuseLeft=1e9};
                if(shotTracker.Recall(ref v)){if(!arena.ReplaySampledShots)shotTracker.Present(ref v,now);shotTracker.Launched(ref v);}
                var at=new NVector((float)v.X,(float)v.Y,(float)v.Z);var vel=new NVector((float)v.VX,(float)v.VY,(float)v.VZ);if(reacts)fx.Shot(at,vel);
                var owner=RoundsPilot(arena,b.Original!=0?b.Original:b.Owner);if(owner==null)continue;
                bool homing=b.Mods!=null&&b.Mods.TryGetValue("orbitals",out int n)&&n>0;int look=homing?NativeRoundsLooks.SatelliteLook(owner.Seat.chips):-1;
                bool holder=(NativeRoundsLooks.Procs(owner.Seat.chips)&RoundsProc.LastRound)!=0;if(look<0&&!holder)continue;
                fx.Units.Shot(new RoundsShotInput{Id=b.Id,Owner=owner.Seat.id,Look=Math.Max(0,look),Homing=look>=0,LastRound=look<0&&owner.Ammo==0&&now-b.Born<.12f,P=at,V=vel,Born=b.Born},now,fx.Pilots,fx.PilotCount);
            }
            // The DEV preview's stand-ins round the local pilot.
            var me=arena.Local;
            if(NativeRoundsPreview.On&&me?.Body!=null){var body=me.Body.transform.position;NativeRoundsPreview.Fill(fx,NativeRoundsPreview.Entry,now,me.Seat.id,RoundsVec(body),me.Yaw*Mathf.Deg2Rad,RoundsFloor(-1,body));}
            fx.End();
            NativeBossVfxSubmit.Submit(fx.Particles,vfx);vfx.LitPieces(fx.Lit.V,fx.Lit.Vertices,fx.Lit.I,fx.Lit.Indices);
            if(fx.Ground.VertexCount>0){if(roundsGround==null&&!roundsGroundFailed){roundsGround=NativeRoundsSurfaceDraw.Create("NativeRoundsField","ROUNDS field surfaces");if(roundsGround==null){roundsGroundFailed=true;Debug.LogWarning("ROUNDS visuals: NativeRoundsField shader missing or unsupported; field surfaces not drawn");}}roundsGround?.Draw(fx.Ground,camera);}
            if(!LowGraphics&&fx.Warp.VertexCount>0){if(roundsWarp==null&&!roundsWarpFailed){roundsWarp=NativeRoundsSurfaceDraw.Create("NativeRoundsWarp","ROUNDS field distortion");if(roundsWarp==null)roundsWarpFailed=true;}roundsWarp?.Draw(fx.Warp,camera);}
        }
    }

    public sealed partial class ConversionGame
    {
        // The DEV panel's ROUNDS effects preview (developers only, presentation only): Core NativeRoundsPreview feeds one entry's
        // stand-ins round the local pilot through the live NativeRoundsFx; nothing is spawned, damaged or sent.
        private static string[] roundsPreviewNames;
        private void RoundsPreviewToggle()
        {
            NativeRoundsPreview.On=!NativeRoundsPreview.On;
            DevSay(NativeRoundsPreview.On?"ROUNDS effects preview · on ("+NativeRoundsPreview.Name(NativeRoundsPreview.Entry)+" round your pilot on a loop; presentation only)":"ROUNDS effects preview · off",false);
        }
        private float RoundsPreviewPanel(float x,float y,float w)
        {
            y=DevHead(x,y,w,"ROUNDS PACK EFFECTS (DEV PREVIEW)");DevToggleAct(new Rect(x,y,w,32),"preview-rounds-fx",NativeRoundsPreview.On);y+=38;
            if(roundsPreviewNames==null){roundsPreviewNames=new string[NativeRoundsPreview.Count];for(int i=0;i<roundsPreviewNames.Length;i++)roundsPreviewNames[i]=NativeRoundsPreview.Name(i).Replace(" field","").Replace(" satellites"," sats");}
            int k=DevChoice(x,ref y,w,roundsPreviewNames,NativeRoundsPreview.Entry,NativeDevState.Live,NativeDevActions.Local,3,"ROUNDS preview entry");NativeRoundsPreview.Entry=k;
            return DevNote(x,y,w,"Any map: the chosen pack effect plays round your pilot on a loop, at its authority radius and life: each field ahead of you, the satellites on you firing homing volleys, three Drone Nest drones (one fights, one expires, one is shot down), the procs and buffs in turn. Presentation only: nothing is spawned, damaged or sent.",C.Muted());
        }
        private const string RoundsPreviewLabel="DEV PREVIEW · ROUNDS PACK EFFECTS · NOT GAMEPLAY";
        private void RoundsPreviewHud(float width)
        {
            if(!NativeRoundsPreview.On||Event.current.type!=EventType.Repaint)return;
            string text=RoundsPreviewLabel;var size=U.Measure(text,11,true,true,.12f);var r=new Rect(width/2-size.x/2-14,150,size.x+28,24);U.Fill(r,C.Void(.75f));H.Outline(r,C.Magenta(.9f),0,H.Corners.None);U.Text(r,text,11,C.Magenta(),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.12f);
            var n=NativeRoundsPreview.Name(NativeRoundsPreview.Entry);var s2=U.Measure(n,11,true,true,.08f);var r2=new Rect(width/2-s2.x/2-10,176,s2.x+20,20);U.Fill(r2,C.Void(.6f));U.Text(r2,n,11,C.CyanSoft(),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.08f);
        }
    }
}
