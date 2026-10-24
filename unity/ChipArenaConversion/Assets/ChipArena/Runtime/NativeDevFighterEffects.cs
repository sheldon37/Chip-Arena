using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    public sealed partial class NativeSourceModels
    {
        private Material ghostMaterial;private bool ghostFailed;
        /// <summary>
        /// demon-afterimages.ts ghostMesh: a model drawn as a flat unlit see-through silhouette (Resources/NativeDevGhost.shader;
        /// colour and opacity come in the body tint). Null when the model or the shader is missing.
        /// </summary>
        public string Ghost(string model)
        {
            if(string.IsNullOrEmpty(model))return null;string id="ghost-"+model;if(models.ContainsKey(id))return id;if(!models.TryGetValue(model,out var source))return null;
            if(ghostMaterial==null)
            {
                if(ghostFailed)return null;var shader=Resources.Load<Shader>("NativeDevGhost");if(shader==null||!shader.isSupported){ghostFailed=true;return null;}
                ghostMaterial=new Material(shader){enableInstancing=true,name="Native Demon Step ghost"};materials.Add(ghostMaterial);
            }
            var parts=new Part[source.Length];for(int i=0;i<parts.Length;i++)parts[i]=new Part{Name=source[i].Name,Bone=source[i].Bone,Mesh=source[i].Mesh,Material=ghostMaterial,NoShadow=true};
            models[id]=parts;return id;
        }
    }

    // C12 part 2: the DEV fighters' effects (client/emperor-effects.ts, matrix-effects.ts, hadouken-visual.ts and
    // demon-afterimages.ts) on the pure Core port (Core/NativeDevFx.cs), drawn with part 1's NativeFxDraw. Everything comes from
    // data host, guests and the killcam all hold: the replicated pose (SourceAction/SourceAt, LastAttack, JuggleUntil, RollAt),
    // Hit events from an Emperor seat (their damage kind), the 'leet-nuke' explosion tag, the replicated Leet fields, storm
    // trails and bullets (Spec, Hadoken, Mods). Presentation only: no hit timing, hitbox, damage, cooldown, movement or network change.
    public sealed partial class ArenaPresentation
    {
        private sealed class FighterTrack
        {
            public float ElectricAt=-100,ElectricKey=float.NaN,PunchAt=-100,PrevPunchAt=-100,ElectricPunchAt=-100;public bool PunchElectric,PrevPunchElectric;public int Frame;
            public readonly NativeJuggleTrack Juggle=new NativeJuggleTrack();
            /// <summary>Whether the punch landing at t (Actor.LastAttack, stamped on every strike) was the Electric Hook's.</summary>
            public bool ElectricBy(float t)=>Mathf.Abs(t-PunchAt)<.03f?PunchElectric:Mathf.Abs(t-PrevPunchAt)<.03f&&PrevPunchElectric;
        }
        private sealed class LeetMemo{public string Kind;public int Hops,Owner,Frame;public float Until;public Vector3 Slender;public bool HasSlender;}
        private sealed class GhostPose
        {
            public readonly Matrix4x4[] Bones=new Matrix4x4[22],Weapon=new Matrix4x4[22];public Matrix4x4 Root;public string Hat;public bool Armed;
            public readonly Func<int,Matrix4x4> Bone,WeaponBone;
            public GhostPose(){Bone=b=>b>=0&&b<22?Bones[b]:Root;WeaponBone=b=>b>=0&&b<22?Weapon[b]:Root;}
        }
        private struct NukeBlast{public Vector3 At;public float Born;}
        private NativeEmperorBolts emperorBolts;private NativeGlyphPool glyphPool;private NativeDemonGhosts demonGhosts;private GhostPose[] ghostPoses;
        private readonly Dictionary<int,FighterTrack> fighterTracks=new Dictionary<int,FighterTrack>();private readonly Dictionary<int,LeetMemo> leetMemos=new Dictionary<int,LeetMemo>();
        private readonly Dictionary<int,Quaternion> hadoukenTurns=new Dictionary<int,Quaternion>();private readonly Dictionary<string,string> ghostHats=new Dictionary<string,string>();
        private readonly List<NukeBlast> nukeBlasts=new List<NukeBlast>();private readonly List<int> devStale=new List<int>();private readonly double[] tongue=new double[NativeHadoukenFx.Points*2*3];
        private int devEvent,devFrame;private string ghostPilot,ghostGloves;private bool ghostsLoaded;private readonly string[] demonGloves=new string[NativeDemonGhosts.Slots],demonBodies=new string[NativeDemonGhosts.Slots];
        private static readonly Color BoltGlow=Alpha(NativeArena.ColorOf(NativeEmperorBolts.GlowColor),NativeEmperorBolts.GlowOpacity),BoltDark=Alpha(NativeArena.ColorOf(NativeEmperorBolts.DarkColor),NativeEmperorBolts.DarkOpacity),
            BoltCore=Alpha(NativeArena.ColorOf(NativeEmperorBolts.CoreColor),NativeEmperorBolts.CoreOpacity),LeetGreen=NativeArena.ColorOf(NativeLeetFx.FieldGreen),BarkTint=NativeArena.ColorOf(NativeLeetFx.Bark).linear,
            GhostViolet=NativeArena.ColorOf(NativeDemonGhosts.Violet).linear,GhostIndigo=NativeArena.ColorOf(NativeDemonGhosts.Indigo).linear;
        private static readonly NativeShotMesh UnitBox=NativeShotGeometry.Box(1,1,1);
        private const float Authored=NativeFxDraw.Authored,Glowing=NativeFxDraw.Authored+NativeFxDraw.Additive;

        private void ClearDevFighters()
        {
            emperorBolts?.Clear();glyphPool?.Clear();demonGhosts?.Clear();fighterTracks.Clear();leetMemos.Clear();hadoukenTurns.Clear();nukeBlasts.Clear();devEvent=0;
        }
        /// <summary>Called from DevEffects after the Siren effects, before the shared layers draw.</summary>
        private void DevFighters(NativeArena arena,Camera camera,float now)
        {
            if(emperorBolts==null){emperorBolts=new NativeEmperorBolts();glyphPool=new NativeGlyphPool();demonGhosts=new NativeDemonGhosts();ghostPoses=new GhostPose[NativeDemonGhosts.Slots];for(int i=0;i<ghostPoses.Length;i++)ghostPoses[i]=new GhostPose();}
            devFrame++;double q=BlastQuality;Vector3 eye=camera.transform.position;
            FighterTracks(arena,now);DevEvents(arena,now,q);LeetFields(arena,camera,now,q);EmperorBolts(arena,now,q,eye);Hadoukens(arena,now,q);DemonSteps(arena,now,q);
        }
        private Vector3 BodyCentre(NativeArena arena,NativePilot p)
        {bool own=FirstPerson&&arena.ViewPilot==p;return !own&&rigs.TryGetValue(p.Seat.id,out var rig)?rig.Root.transform.position:p.Body.position;}
        // Per pilot: the Electric Hook's start (electricAt), the last two punches (Actor.LastAttack moves on every strike, with
        // SourceAction naming the action) and the last Electric Hook strike, and the juggle (NativeJuggleTrack: JuggleUntil moves
        // to the hit + 1.25 s, a bound or wall splat + .85 s).
        private void FighterTracks(NativeArena arena,float now)
        {
            foreach(var p in arena.Pilots)
            {
                var a=p.Pose;if(a==null)continue;int seat=p.Seat.id;if(!fighterTracks.TryGetValue(seat,out var t)){t=new FighterTrack();fighterTracks.Add(seat,t);}t.Frame=devFrame;
                string action=string.IsNullOrEmpty(a.SourceAction)?null:a.SourceAction;
                if(a.SourceSpec=="emperor")
                {
                    if(NativeEmperorTriggers.Fist(a.SourceSpec,action)&&a.SourceAt!=t.ElectricKey){t.ElectricKey=a.SourceAt;t.ElectricAt=a.SourceAt;}
                    if(a.LastAttack!=t.PunchAt){t.PrevPunchAt=t.PunchAt;t.PrevPunchElectric=t.PunchElectric;t.PunchAt=a.LastAttack;t.PunchElectric=action=="electric";if(t.PunchElectric)t.ElectricPunchAt=a.LastAttack;}
                }
                t.Juggle.Observe(a.JuggleUntil,now);
            }
            devStale.Clear();foreach(var pair in fighterTracks)if(pair.Value.Frame!=devFrame)devStale.Add(pair.Key);foreach(int seat in devStale)fighterTracks.Remove(seat);
        }
        // renderer.ts event loop (events older than .4 s are skipped): emperor-hit/emperor-bound impacts and the nuke blast.
        private void DevEvents(NativeArena arena,float now,double q)
        {
            int newest=0;foreach(var e in arena.Events)newest=Math.Max(newest,e.Id);if(newest<devEvent)devEvent=0;
            foreach(var e in arena.Events)
            {
                if(e.Id<=devEvent)continue;Vector3 at=NativeArena.Vec(e.Position);
                // matrix-effects.ts keeps its own 1.7 s window for the nuke.
                if(e.Kind==EventKind.Explosion&&e.Detail=="leet-nuke"){if(now-e.Time<NativeLeetFx.BlastLife&&nukeBlasts.Count<8)nukeBlasts.Add(new NukeBlast{At=at,Born=e.Time});continue;}
                if(now-e.Time>.4f||e.Kind!=EventKind.Hit&&e.Kind!=EventKind.Headshot||e.Amount<=0)continue;
                var source=SeatPilot(arena,e.Source);if(source?.Pose==null||!fighterTracks.TryGetValue(e.Source,out var t))continue;
                // emperor.ts confirm: a punch (direct) is 1.3 for the Electric Hook, else .65, at the target + .2. Of the follow-ups
                // (secondary), a bound or wall splat resets the juggle to + .85 s (emperor-bound 1.8), a storm-trail SHOCK has no
                // radius (.75, at the target) and the Twin echo confirms as electric (1.3).
                // The echo and storm-trail hits arrive tagged "direct" too (only bound and wall splats are direct:false), so tell
                // them apart here (NativeEmperorTriggers): a tracked punch, else a storm trail near the target, else the Twin echo
                // .13 s after an Electric Hook strike; a secondary hit only as the bound or wall splat that reset the juggle.
                bool punch=Mathf.Abs(e.Time-t.PunchAt)<.03f||Mathf.Abs(e.Time-t.PrevPunchAt)<.03f,storm=!punch&&e.Detail=="direct"&&NearStorm(arena,e.Source,at);
                bool splat=e.Detail=="secondary"&&Mathf.Abs((SeatPilot(arena,e.Target)?.Pose?.JuggleUntil??0)-(e.Time+.85f))<.05f;
                double size=NativeEmperorTriggers.Impact(source.Pose.SourceSpec,e.Detail,punch,punch&&t.ElectricBy(e.Time),e.Time-t.ElectricPunchAt,storm,splat);
                if(size<=0)continue;
                emperorBolts.Impact(-at.x,at.y+(e.Detail=="direct"&&!storm?.2f:0),at.z,now,e.Id,size);
            }
            devEvent=Math.Max(devEvent,newest);
        }
        private static bool TreeEndedQuietly(NativeArena arena,int owner,float now)
        {
            foreach(var f in arena.LeetFields)if(f.Kind=="tree"&&f.Owner!=null&&f.Owner.Seat.id==owner)return true;
            foreach(var p in arena.Pilots)if(p.ScareUntil>now&&now-p.ScareAt>=0&&now-p.ScareAt<.3f)return true;
            return false;
        }
        // A juggle hit from an Emperor's basic jab (NativeEmperorTriggers.JuggleBolt): no bolt.
        private bool JabJuggle(float hit)
        {foreach(var pair in fighterTracks){var t=pair.Value;if(!NativeEmperorTriggers.JuggleBolt(hit,t.PunchElectric?-100:t.PunchAt,t.PrevPunchElectric?-100:t.PrevPunchAt))return true;}return false;}
        private static bool NearStorm(NativeArena arena,int owner,Vector3 at)
        {foreach(var s in arena.StormTrails)if(s.Owner!=null&&s.Owner.Seat.id==owner&&(s.Position-at).sqrMagnitude<1.6f*1.6f)return true;return false;}
        // emperor-effects.ts update: Electric Hook fist, fresh juggle hits, impacts, storm-step trails (Full only); three layers.
        private void EmperorBolts(NativeArena arena,float now,double q,Vector3 eye)
        {
            var bolts=emperorBolts;bolts.Begin();
            foreach(var p in arena.Pilots)
            {
                var a=p.Pose;if(a==null||p.Hp<=0||!fighterTracks.TryGetValue(p.Seat.id,out var t))continue;
                float age=now-t.ElectricAt;
                if(a.SourceSpec=="emperor"&&age>=0&&age<NativeEmperorBolts.FistSeconds)
                {
                    bool own=FirstPerson&&arena.ViewPilot==p;Vector3 hand=!own&&rigs.TryGetValue(p.Seat.id,out var rig)?rig.Bones[CapturedRig.RHand].position:p.Body.position+Vector3.up*.4f;
                    bolts.Bolt(-hand.x,hand.y,hand.z,now,p.Seat.id,NativeEmperorBolts.FistSize);
                }
                // A juggle hit in the last .28 s (FighterTracks: when the replicated JuggleUntil moved); only Emperor confirms and the
                // Siren's launch juggle a pilot.
                if(NativeJugglePose.Active(a.JuggleUntil,now)&&now-t.Juggle.LastHit<NativeEmperorBolts.JuggleSeconds&&!JabJuggle(t.Juggle.LastHit)){Vector3 c=BodyCentre(arena,p);bolts.Bolt(-c.x,c.y,c.z,now,t.Juggle.Hits,NativeEmperorBolts.JuggleSize);}
            }
            bolts.DrawImpacts(now);
            if(q>=.5)foreach(var s in arena.StormTrails)bolts.Bolt(-s.Position.x,NativeEmperorBolts.TrailHeight,s.Position.z,now,Math.Floor(s.At*10),NativeEmperorBolts.TrailSize);
            var draw=fxDraw;
            for(int layer=0;layer<3;layer++)for(int i=0;i<bolts.Count;i++)
            {
                bolts.Box(i,layer,out double cx,out double cy,out double cz,out double qx,out double qy,out double qz,out double qw,out double sx,out double sy,out double sz);
                var m=Matrix4x4.TRS(new Vector3(-(float)cx,(float)cy,(float)cz),new Quaternion((float)qx,-(float)qy,-(float)qz,(float)qw),new Vector3((float)sx,(float)sy,(float)sz));
                draw.Geometry(draw.Effects,m,UnitBox,layer==0?BoltGlow:layer==1?BoltDark:BoltCore,layer==0?Glowing:Authored,eye);
            }
        }
        // matrix-effects.ts update: static columns (Slender hops and death), the nuke blast, glyph trails, the nuke field and tree.
        private void LeetFields(NativeArena arena,Camera camera,float now,double q)
        {
            var pool=glyphPool;var draw=fxDraw;Vector3 eye=camera.transform.position;
            foreach(var f in arena.LeetFields)
            {
                if(!leetMemos.TryGetValue(f.Id,out var memo)){memo=new LeetMemo{Kind=f.Kind,Hops=f.Hops};leetMemos.Add(f.Id,memo);}
                if(f.Hops>memo.Hops)
                {
                    // leet-slender-hop: a column where Slender stood and one where it lands.
                    pool.StaticColumn(-f.From.x,f.From.y,f.From.z,now,q);
                    if(f.Slender?.Body!=null){Vector3 feet=f.Slender.Body.position-Vector3.up*1.23f;pool.StaticColumn(-feet.x,feet.y,feet.z,now,q);}
                }
                memo.Hops=f.Hops;memo.Until=f.Until;memo.Owner=f.Owner?.Seat.id??0;memo.Frame=devFrame;if(f.Slender?.Body!=null){memo.Slender=f.Slender.Body.position;memo.HasSlender=true;}
            }
            devStale.Clear();
            foreach(var pair in leetMemos)
            {
                var memo=pair.Value;if(memo.Frame==devFrame)continue;devStale.Add(pair.Key);
                // A tree gone before its time while its owner lives: Slender was killed (minionDeath 'slender' at its body).
                var owner=SeatPilot(arena,memo.Owner);
                // A successful scare or a recast also ends the tree early, with no minionDeath: skip those.
                if(memo.Kind=="tree"&&memo.HasSlender&&now<memo.Until-.1f&&owner!=null&&owner.Seat.connected&&owner.Hp>0&&!TreeEndedQuietly(arena,memo.Owner,now))pool.StaticColumn(-memo.Slender.x,memo.Slender.y,memo.Slender.z,now,q);
            }
            foreach(int id in devStale)leetMemos.Remove(id);
            // The mushroom: cap, stem and a ground ring racing out, fading over 1.7 s.
            for(int i=nukeBlasts.Count-1;i>=0;i--)
            {
                var b=nukeBlasts[i];float age=now-b.Born;if(age>NativeLeetFx.BlastLife){nukeBlasts.RemoveAt(i);continue;}if(age<0)continue;
                NativeLeetFx.Blast(age,out double s,out double ring,out double opacity);var g=Matrix4x4.TRS(b.At,Quaternion.identity,Vector3.one*(float)s);var c=Alpha(NativeArena.ColorOf(NativeLeetFx.BlastGreen),opacity);
                draw.Geometry(draw.Effects,g*Matrix4x4.TRS(new Vector3(0,2.5f,0),Quaternion.identity,new Vector3(4,1.3f,4)),NativeLeetFx.BlastSphere,c,Authored,eye);
                draw.Geometry(draw.Effects,g*Matrix4x4.TRS(new Vector3(0,1,0),Quaternion.identity,new Vector3(1,2,1)),NativeLeetFx.BlastSphere,c,Authored,eye);
                draw.Geometry(draw.Effects,g*Matrix4x4.TRS(new Vector3(0,.04f,0),Quaternion.Euler(90,0,0),Vector3.one*(float)ring),NativeLeetFx.BlastRing,c,Authored,eye);
            }
            pool.BeginTrails();
            foreach(var b in arena.Bullets){if(b.Spec!="leet")continue;NativeShotViews.Bullet(arena,b,shotTracker,now,out var v);pool.Trail(b.Id,-v.X,v.Y,v.Z,now,q);}
            pool.EndTrails();pool.Expire(now);
            Quaternion facing=camera.transform.rotation;
            for(int i=0;i<pool.Count;i++)
            {
                pool.Pose(i,now,out double x,out double y,out double z,out double scale,out double r,out double g,out double b);
                var color=new Color((float)r,(float)g,(float)b,1).gamma;color.a=1;
                draw.Geometry(draw.Effects,Matrix4x4.TRS(new Vector3(-(float)x,(float)y,(float)z),facing,Vector3.one*(float)scale),NativeLeetFx.Glyphs[pool.Bit[i]],color,Glowing,eye);
            }
            foreach(var f in arena.LeetFields)
            {
                float age=now-f.At;
                if(f.Kind=="nuke")
                {
                    // Green ring at the blast radius and 28 rising digits, on the ground.
                    Vector3 at=new Vector3(f.Position.x,.03f,f.Position.z);draw.Ring(draw.Solid,at+Vector3.up*.035f,10.7f/11,11,LeetGreen,Authored);
                    for(int i=0;i<NativeLeetFx.FieldGlyphs;i++)
                    {
                        NativeLeetFx.FieldGlyph(i,age,now,out double x,out double y,out double z,out double turn);
                        draw.Geometry(draw.Solid,Matrix4x4.TRS(at+new Vector3(-(float)x,(float)y,(float)z),MirrorEuler(0,turn,0),new Vector3(.25f,.4f,.25f)),NativeLeetFx.Glyphs[i%2],LeetGreen,Authored);
                    }
                }
                else if(f.Kind=="tree")
                {
                    // The dark Lambert tree grows out of the ground over .8 s (Slenderman is SlenderModel's).
                    var g=Matrix4x4.TRS(f.Position,Quaternion.identity,new Vector3(1,(float)NativeLeetFx.TreeHeight(age),1));
                    for(int k=0;k<=NativeLeetFx.Branches;k++)
                    {
                        NativeLeetFx.TreePart(k,out double px,out double py,out double pz,out double rx,out double ry,out double rz,out double sx,out double sy,out double sz);
                        sourceModels.WoundCube(g*Matrix4x4.TRS(new Vector3(-(float)px,(float)py,(float)pz),MirrorEuler(rx,ry,rz),new Vector3((float)sx,(float)sy,(float)sz)),BarkTint);
                    }
                }
            }
        }
        // arcade-effects.ts / hadouken-visual.ts: a dome per hadouken (36, 16 on Reduced) over C10's sphere, halo and trail.
        private void Hadoukens(NativeArena arena,float now,double q)
        {
            var draw=fxDraw;int limit=q<.5?NativeHadoukenFx.ReducedLimit:NativeHadoukenFx.Limit,shown=0;devStale.Clear();
            foreach(var b in arena.Bullets)
            {
                if(!b.Hadoken||shown>=limit)continue;shown++;NativeShotViews.Bullet(arena,b,shotTracker,now,out var v);bool red=b.Mods!=null&&b.Mods.TryGetValue("arcade-red",out int reds)&&reds>0;
                if(!hadoukenTurns.TryGetValue(b.Id,out var turn))turn=Quaternion.identity;
                double vx=-v.VX,vy=v.VY,vz=v.VZ;
                if(vx*vx+vy*vy+vz*vz>.0001){NativeDevFxMath.Normalize(ref vx,ref vy,ref vz);NativeDevFxMath.UnitRotation(0,0,1,vx,vy,vz,out double qx,out double qy,out double qz,out double qw);turn=new Quaternion((float)qx,-(float)qy,-(float)qz,(float)qw);}
                hadoukenTurns[b.Id]=turn;
                // The dome's own frame is the source's: Unity = TRS(position, turn) · mirror · source-local.
                var frame=Matrix4x4.TRS(new Vector3((float)v.X,(float)v.Y,(float)v.Z),turn,Vector3.one)*Matrix4x4.Scale(new Vector3(-1,1,1));
                NativeHadoukenFx.Frame(b.Radius,now,out double r,out double pulse);
                Color hot=Alpha(NativeArena.ColorOf(NativeHadoukenFx.Hot(red)),NativeHadoukenFx.HotOpacity),energy=Alpha(NativeArena.ColorOf(NativeHadoukenFx.Energy(red)),NativeHadoukenFx.EnergyOpacity),haze=Alpha(NativeArena.ColorOf(NativeHadoukenFx.Haze(red)),NativeHadoukenFx.HazeOpacity);
                NativeHadoukenFx.Shell(r,out double z,out double sx,out double sy,out double sz);draw.Geometry(draw.Effects,frame*Matrix4x4.TRS(new Vector3(0,0,(float)z),Quaternion.identity,new Vector3((float)sx,(float)sy,(float)sz)),NativeHadoukenFx.Sphere,haze,Glowing);
                NativeHadoukenFx.Core(r,pulse,out z,out sx,out sy,out sz);draw.Geometry(draw.Effects,frame*Matrix4x4.TRS(new Vector3(0,0,(float)z),Quaternion.identity,new Vector3((float)sx,(float)sy,(float)sz)),NativeHadoukenFx.Sphere,hot,Glowing);
                NativeHadoukenFx.RimPart(r,pulse,now,out z,out double rs,out double roll);
                draw.Geometry(draw.Effects,frame*Matrix4x4.TRS(new Vector3(0,0,(float)z),new Quaternion(0,0,Mathf.Sin((float)roll/2),Mathf.Cos((float)roll/2)),Vector3.one*(float)rs),NativeHadoukenFx.Rim,energy,Glowing);
                for(int i=0;i<NativeHadoukenFx.Tongues;i++){if(!NativeHadoukenFx.TongueVisible(i,q))continue;NativeHadoukenFx.Tongue(i,r,now,tongue);draw.Strip(draw.Effects,frame,tongue,NativeHadoukenFx.TongueIndices,NativeHadoukenFx.TongueHot(i)?hot:energy,Glowing);}
            }
            foreach(var pair in hadoukenTurns){bool live=false;foreach(var b in arena.Bullets)if(b.Id==pair.Key&&b.Hadoken){live=true;break;}if(!live)devStale.Add(pair.Key);}
            foreach(int id in devStale)hadoukenTurns.Remove(id);
        }
        // demon-afterimages.ts: freeze the stepping Ansatsuken's posed model every .045 s and fade the ghosts over .28 s.
        private void DemonSteps(NativeArena arena,float now,double q)
        {
            var ghosts=demonGhosts;
            if(!ghostsLoaded){ghostsLoaded=true;ghostPilot=sourceModels.Ghost("pilot");ghostGloves=sourceModels.Ghost("weapon-ansatsuken");}
            if(ghostPilot==null)return;
            foreach(var p in arena.Pilots)
            {
                var a=p.Pose;if(a==null||a.SourceSpec!="ansatsuken"||p.Hp<=0||!p.Seat.connected||!a.Rolling(now)||FirstPerson&&arena.ViewPilot==p||!rigs.TryGetValue(p.Seat.id,out var rig))continue;
                if(!ghosts.Due(p.Seat.id,now,a.RollAt,q))continue;int slot=ghosts.Take(p.Seat.id,now,q);var pose=ghostPoses[slot];demonBodies[slot]=BodyGhost(BodySkin(p.Seat.id),BodyTier(rig.Root.transform.position,false))??ghostPilot;demonGloves[slot]=NativeWeaponSkins.Index(p.Seat.weaponSkins,"ansatsuken")==NativeWeaponSkins.PrototypeIndex?ghostGloves:sourceModels.Ghost(SkinnedHeld(a))??ghostGloves;
                pose.Root=rig.Root.transform.localToWorldMatrix;for(int b=0;b<22&&b<rig.Bones.Length;b++)pose.Bones[b]=rig.Bones[b].localToWorldMatrix;
                pose.Armed=ghostGloves!=null&&!a.WeaponAway;if(pose.Armed){float reach=WeaponReach(rig,a,now);for(int b=0;b<22&&b<rig.Bones.Length;b++)pose.Weapon[b]=SourceWeaponBone(rig,a,now,false,b,reach);}
                pose.Hat=null;if(!string.IsNullOrEmpty(a.Hat)){if(!ghostHats.TryGetValue(a.Hat,out var hat)){hat=sourceModels.Ghost("hat-"+a.Hat);ghostHats[a.Hat]=hat;}pose.Hat=hat;}
            }
            for(int s=0;s<NativeDemonGhosts.Slots;s++)
            {
                if(!ghosts.Live(s,now))continue;var owner=SeatPilot(arena,ghosts.Owner[s]);if(owner==null||owner.Hp<=0)continue;
                var pose=ghostPoses[s];Color tint=NativeDemonGhosts.Color(s)==NativeDemonGhosts.Indigo?GhostIndigo:GhostViolet;float opacity=(float)NativeDemonGhosts.Fade(now-ghosts.At[s]);
                sourceModels.Queue(demonBodies[s]??ghostPilot,pose.Root,pose.Bone,null,0,null,tint,null,opacity);
                if(pose.Armed)sourceModels.Queue(demonGloves[s]??ghostGloves,pose.Root,pose.WeaponBone,null,0,null,tint,null,opacity);
                if(pose.Hat!=null)sourceModels.Queue(pose.Hat,pose.Root,pose.Bone,null,0,null,tint,null,opacity);
            }
        }
    }
}
