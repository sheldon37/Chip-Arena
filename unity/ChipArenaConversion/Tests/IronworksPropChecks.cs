#if ARENA_TESTS
using System;
using System.IO;
using System.Numerics;
using System.Text.RegularExpressions;
namespace ChipArena.UnityConversion
{
    // The City Ironworks kinds' held props on every presentation path (owner 2026-10-19: "The Forge enemy that has a prod thing
    // too, (In the white suit) it seems to be missing that now for some reason"). The zombie motion pass (a784379, merged into
    // shared by 97046f7) poses every horde add on the horde clock (hordeNow), while the Ironworks hand-over (IronworksQueue) kept
    // matching the gait's input against arena.Time, so it skipped every live add: the Stoker's rake and pull hook, the Ladleman's
    // ladle, the Rigger's hook and chain, the Slagback's crust, the lamp and the kinds' effects were gone on the host, guests and
    // the killcam (the DEV gallery queues its own and kept them). These checks bind the hand-over to the clock the add is posed on,
    // run the live path's two clocks (Core HordeClock, host and guest) through its guard, and keep each prop in the fists through
    // the gait, the attacks and the specials, on the live path's inputs (the presented root, both tiers) and the gallery's script.
    internal static class IronworksPropChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok){if(Environment.GetEnvironmentVariable("IRONWORKS_ALL")=="1"){Console.WriteLine("FAIL ironworks props: "+what);return;}throw new Exception("Ironworks props: "+what);}count++;}
        private static string R(string root,string path)=>File.ReadAllText(Path.Combine(root,path));
        private static string Between(string s,string from,string to){int a=s.IndexOf(from,StringComparison.Ordinal);if(a<0)return "";int b=s.IndexOf(to,a+from.Length,StringComparison.Ordinal);return b<0?s.Substring(a):s.Substring(a,b-a);}
        /// <summary>How far a fist may be from its prop's line (m, the rig's): the pole is .022 m thick, the fist about .1 m.</summary>
        public const float HoldBound=.03f,LadleBound=.04f,GripBound=.01f;
        public static void Run(string root)
        {
            string clock=HandOver(root,out string guard);Clocks(clock,guard,out int hostSkipped,out int guestSkipped);Live();Gallery();PresentationOnly(root);
            Console.WriteLine("PASS ironworks props ("+count+" checks: the hand-over matches the gait input on "+clock+", the clock HordeKindAdd poses on, for host, guest, killcam and the DEV preview (arena.Time skipped "+hostSkipped+"/600 host and "+guestSkipped+"/600 guest frames); the rake and the ladle's shank within "+HoldBound+" and "+LadleBound+" m of both fists, the hook's chain on its fist, through the gait, attacks and specials, live and in the gallery; presentation only)");
        }

        // ---- the hand-over: the guard compares the gait input with the clock the add is posed on, on every path ----
        private static string HandOver(string root,out string compared)
        {
            var horde=R(root,"Assets/ChipArena/Runtime/NativeHordeKindPresentation.cs");var iron=R(root,"Assets/ChipArena/Runtime/NativeIronworksKindsPresentation.cs");
            var add=Between(horde,"private bool HordeKindAdd(","private void HordeMouth(");Check(add.Length>0,"HordeKindAdd is where it was");
            var posed=Regex.Match(add,@"var s=HordeState\(m\.Id\);float now=([A-Za-z.]+);");Check(posed.Success,"HordeKindAdd names the clock it poses on");string clock=posed.Groups[1].Value;
            Check(add.Contains("f=new HordeMotionInput{Kind=kind,Look=s.Look,Id=m.Id,Seed=NativeHordeKinds.Seed(m.Id),Time=now,",StringComparison.Ordinal),"the gait's input is stamped with that clock");
            int pose=add.IndexOf("s.Gait.Pose(f,minionPose,s.React);",StringComparison.Ordinal),queue=add.IndexOf("IronworksQueue(arena,rig,s,m,ground,glow,flash);",StringComparison.Ordinal);
            Check(pose>0&&queue>pose,"the Ironworks hand-over follows the gait's pose of the add");
            Check(add.Contains("kind=HordeDrawKind(real,m.Id)",StringComparison.Ordinal),"the DEV preview's kind (Settings, minion preview) draws through the same add");
            var q=Between(iron,"private void IronworksQueue(","private void IronworksProps(");
            var guard=Regex.Match(q,@"f\.Id!=m\.Id\|\|f\.Time!=([A-Za-z.]+)\)return;");Check(guard.Success,"IronworksQueue guards on the gait's own frame of this add");
            compared=guard.Success?guard.Groups[1].Value:"?";Check(guard.Success&&guard.Groups[1].Value==clock,"the guard compares the gait input with "+clock+", the clock HordeKindAdd poses on (it compares "+(guard.Success?guard.Groups[1].Value:"?")+")");
            Check(!iron.Contains("f.Time!=arena.Time",StringComparison.Ordinal)&&!iron.Contains("f.Time==arena.Time",StringComparison.Ordinal),"no Ironworks hand-over matches a gait input against arena.Time");
            int g=q.IndexOf(guard.Value,StringComparison.Ordinal);
            foreach(var after in new[]{"s.Gait.PlaceProps(f,minionPose);IronworksProps(rig,s.Gait,s.Tier,glow,flash,s.Scale);","IronworksCrust(rig,","IronworksLamp(rig);","IronworksJob(rig,s,z,f,"})
                Check(q.IndexOf(after,StringComparison.Ordinal)>g,"past the guard the live add gets `"+after+"`");
            // Host, guests and the killcam's replay world draw their adds through SourceWorld into HordeKindAdd.
            Check(R(root,"Assets/ChipArena/Runtime/NativeSourcePresentation.cs").Contains("if(add!=null&&HordeKindAdd(arena,m,rig,pos))continue;",StringComparison.Ordinal),"every peer's adds (host, guest, killcam) are drawn by HordeKindAdd");
            // The Rigger on its chain queues its hook and chain itself; the DEV gallery queues the props of its own rigs.
            var hang=Between(iron,"private bool IronworksAdd(","// ====");Check(hang.Contains("sourceModels.Queue(NativeHordeModels.PropId(NativeHordeModels.PropHook,tier)",StringComparison.Ordinal)&&hang.Contains("IronworksChain(anchor,fists+Vector3.up*.05f,0,tier);",StringComparison.Ordinal),"the hanging Rigger queues its chain and hook");
            var gal=Between(iron,"private void IronworksGallery(","private void ClearIronworksGallery(");Check(gal.Contains("s.Gait.PlaceProps(f,minionPose);IronworksProps(rig,s.Gait,NativeHordeModels.Full,gl,1,sc);",StringComparison.Ordinal),"the DEV gallery queues its rigs' props");
            Check(horde.Contains("if(MinionGallery)IronworksGallery(arena);",StringComparison.Ordinal),"the gallery runs in the Dev Sandbox's minion gallery");
            return clock;
        }

        // ---- the live path's clocks: the add posed on the horde clock, the guard's clock, host and guest, frame by frame ----
        private static void Clocks(string clock,string guardClock,out int hostSkipped,out int guestSkipped)
        {
            hostSkipped=guestSkipped=0;
            for(int peer=0;peer<2;peer++)
            {
                bool guest=peer==1;var line=new HordeClock();double authority=40;int queued=0,old=0;
                for(int i=0;i<600;i++)
                {
                    // the host: the visual clock between fixed steps; a guest: the replica render time behind the newest frame
                    float dt=1/75f;authority+=dt;double tick=Math.Floor(authority*60)/60;float arenaTime=(float)tick;
                    double field=guest?arenaTime-.1+(authority-tick):authority;line.Step(field,dt,guest,guest?arenaTime:double.PositiveInfinity);
                    float hordeNow=(float)line.Time;float Value(string name)=>name=="hordeNow"?hordeNow:name=="arena.Time"?arenaTime:float.NaN;
                    float input=Value(clock);if(input==Value(guardClock))queued++;if(input!=arenaTime)old++;
                }
                Check(queued==600,(guest?"a guest":"the host")+": the hand-over takes the posed add on every frame ("+queued+"/600)");
                if(guest)guestSkipped=old;else hostSkipped=old;
            }
        }

        // ---- the live path's gait (the presented root: Flow, both tiers) through gait, attacks and specials ----
        private static Vector3 Grip(CapturedRig.Pose p,int hand)=>p.WorldPosition[hand]+Vector3.Transform(new Vector3(0,-.075f,0),p.WorldRotation[hand]);
        private struct Worst{public float Off,Grip,Along,At,Now;public int Frames,Missing;public void Max(float off,float grip,float along){if(off>Off){Off=off;At=Now;}Grip=Math.Max(Grip,grip);Along=Math.Max(Along,along);}}
        /// <summary>One frame's props against the fists: the kind's own rules (the Stoker's rake in both fists, the Ladleman's ladle
        /// in the front fist and on the back one while carried, the Rigger's chain from its fist to its hook).</summary>
        private static void Frame(int z,NativeHordeGait g,CapturedRig.Pose p,in HordeMotionInput f,ref Worst w)
        {
            w.Frames++;w.Now=f.Time;p.World();
            switch(z)
            {
                case NativeIronworksKinds.Cinderling:case NativeIronworksKinds.Slagback:if(g.PropCount!=0)w.Missing++;break;
                case NativeIronworksKinds.Stoker:
                {
                    if(g.PropCount<1||g.Props[0].Prop!=NativeHordeModels.PropRake){w.Missing++;break;}var pr=g.Props[0];var dir=Vector3.Transform(Vector3.UnitZ,pr.Turn);
                    var rel=Grip(p,CapturedRig.LHand)-pr.At;float along=Vector3.Dot(rel,dir);
                    float bad=along<.1f?.1f-along:along>.8f?along-.8f:0;
                    float hook=g.ChainCount>0?(g.Chains[0].From-(pr.At+dir*NativeHordeModels.RakeTip)).Length():0;
                    w.Max((rel-dir*along).Length(),Math.Max((Grip(p,CapturedRig.RHand)-pr.At).Length(),hook),bad);break;
                }
                case NativeIronworksKinds.Ladleman:
                {
                    if(g.PropCount<1){w.Missing++;break;}var pr=g.Props[0];if(pr.Prop==NativeHordeModels.PropLadleEmpty)break;if(pr.Prop!=NativeHordeModels.PropLadle){w.Missing++;break;}
                    float grip=(Grip(p,CapturedRig.LHand)-pr.At).Length();bool swing=f.Attack==NativeHordeKinds.Attack.Smash&&!float.IsNaN(f.AttackAge)&&f.AttackAge>=0&&f.AttackAge<f.Recover+.25f;
                    // carried and poured on its shank in both fists; swung in the smash takes' fists (it rides the front one)
                    if(swing){w.Max(0,grip,0);break;}
                    var dir=Vector3.Transform(Vector3.UnitZ,pr.Turn);var rel=Grip(p,CapturedRig.RHand)-pr.At;float along=Vector3.Dot(rel,dir);
                    w.Max((rel-dir*along).Length(),grip,along>.05f?along-.05f:along<-.6f?-.6f-along:0);break;
                }
                case NativeIronworksKinds.Rigger:
                {
                    if(g.PropCount<1||g.ChainCount<1||g.Props[0].Prop!=NativeHordeModels.PropHook){w.Missing++;break;}
                    w.Max(0,Math.Max((g.Chains[0].From-Grip(p,CapturedRig.RHand)).Length(),(g.Chains[0].To-g.Props[0].At).Length()),0);break;
                }
            }
        }
        private static void Done(string what,in Worst w,int z)
        {
            Check(w.Frames>0&&w.Missing==0,what+": the prop is there on every frame ("+(w.Frames-w.Missing)+"/"+w.Frames+")");
            if(z==NativeIronworksKinds.Cinderling||z==NativeIronworksKinds.Slagback)return;
            Check(w.Grip<=GripBound,what+": the prop is on its holding fist ("+w.Grip.ToString("0.000")+" m)");
            Check(w.Off<=(z==NativeIronworksKinds.Ladleman?LadleBound:HoldBound)&&w.Along<=0,what+": the other fist on the pole ("+w.Off.ToString("0.000")+" m off its line at "+w.At.ToString("0.00")+" s, "+w.Along.ToString("0.000")+" m past its grip range)");
        }
        private static readonly string[] Names={"cinderling","stoker","slagback","ladleman","rigger"};
        private static void Live()
        {
            for(int z=0;z<NativeIronworksKinds.KindCount;z++)
            {
                int k=NativeIronworksKinds.Kind(z);var row=NativeHordeKinds.ProposedRow(k);float sc=NativeHordeKinds.Scale[k],strike=NativeHordeKinds.Strike(row,false,0),recover=NativeHordeKinds.Recover(row,false,0);
                var attack=NativeHordeKinds.AttackOf(k,row.attack,false);float walk=row.walk,run=Math.Min(row.run,3.4f);
                foreach(var sc0 in new[]{"idle","walk","run","turn","attack","attack-walking","pull-hooked","pull-missed","pour"})
                {
                    if(sc0.StartsWith("pull",StringComparison.Ordinal)&&z!=NativeIronworksKinds.Stoker||sc0=="pour"&&z!=NativeIronworksKinds.Ladleman)continue;
                    for(int tier=NativeHordeModels.Full;tier<=NativeHordeModels.Reduced;tier++)
                    {
                        var g=new NativeHordeGait();var p=new CapturedRig.Pose();var w=new Worst();int id=76000+z*10+tier;var v=IronworksView.None;
                        if(sc0=="pull-hooked"){v.pullAt=101.5f;v.pullTarget=1;}if(sc0=="pull-missed")v.pullAt=101.5f;if(sc0=="pour"){v.pourAt=101.5f;v.pourYaw=0;}
                        float speed=sc0=="walk"||sc0=="turn"||sc0=="attack-walking"?walk:sc0=="run"?run:0;Vector3 at=Vector3.Zero;float yaw=0;
                        for(int i=0;i<360;i++)
                        {
                            float t=100+i/60f,dt=1/60f;if(sc0=="turn")yaw+=50*dt;float yr=yaw*(float)Math.PI/180;var vel=new Vector3((float)Math.Sin(yr),0,(float)Math.Cos(yr))*speed;at+=vel*dt;
                            float age=float.NaN;if(sc0.StartsWith("attack",StringComparison.Ordinal)&&t>=101){age=(t-101)%(recover+.6f);}
                            var f=new HordeMotionInput{Kind=k,Id=id,Seed=NativeHordeKinds.Seed(id),Time=t,Yaw=yaw,Ground=0,Root=(at+new Vector3(0,NativeHordeKinds.DrawRoot(k,false),0))/sc,Velocity=vel/sc,Health=1,
                                AttackAge=age,Strike=strike,Recover=recover,Attack=attack,Side=1,HasTarget=true,Target=(at+new Vector3(0,.85f,3))/sc,Flow=true,YawRate=sc0=="turn"?50*(float)Math.PI/180:0,Tier=tier};
                            g.Iron=v;g.IronTarget=f.Target;g.IronTargetKnown=true;g.Pose(f,p);g.PlaceProps(f,p);
                            if(i>=60)Frame(z,g,p,f,ref w);
                        }
                        Done("live "+Names[z]+" "+sc0+" (tier "+NativeHordeModels.TierNames[tier]+")",w,z);
                    }
                }
            }
        }

        // ---- the DEV gallery's script (NativeIronworksGallery) posed as the gallery poses it, every ground frame of the loop ----
        private static void Gallery()
        {
            for(int z=0;z<NativeIronworksKinds.KindCount;z++)
            {
                int k=NativeIronworksKinds.Kind(z),id=991000+z;var row=NativeHordeKinds.ProposedRow(k);float sc=NativeHordeKinds.Scale[k],strike=NativeHordeKinds.Strike(row,false,0),recover=NativeHordeKinds.Recover(row,false,0);var attack=NativeHordeKinds.AttackOf(k,row.attack,false);
                var g=new NativeHordeGait();var p=new CapturedRig.Pose();var w=new Worst();const float start=200;var me=new Vector3(0,.85f,5);
                for(float gg=0;gg<NativeIronworksGallery.DeathAt;gg+=1/60f)
                {
                    var fr=NativeIronworksGallery.At(z,gg,start,5,0,0,0);if(fr.State!=NativeIronworksGallery.State.Ground){g.Reset();continue;}
                    var pos=fr.Offset;var vel=fr.Velocity;float yaw=vel.LengthSquared()>.01f?(float)(Math.Atan2(vel.X,vel.Z)*180/Math.PI):0;float rootY=NativeHordeKinds.DrawRoot(k,false);
                    var f=new HordeMotionInput{Kind=k,Id=id,Seed=NativeHordeKinds.Seed(id),Time=start+gg,Yaw=yaw,Ground=0,Root=new Vector3(pos.X,rootY,pos.Z)/sc,Velocity=vel/sc,AttackAge=fr.AttackAge,Strike=strike,Recover=recover,Attack=attack,Side=1,Health=1,
                        HasTarget=!fr.Entering,Target=me/sc,Entering=fr.Entering,EntryAge=gg,EntryWalk=NativeIronworksGallery.Entrance};
                    g.Iron=fr.View;g.IronTarget=me/sc;g.IronTargetKnown=true;g.Pose(f,p);g.PlaceProps(f,p);
                    if(gg>=.5f)Frame(z,g,p,f,ref w);
                }
                Done("gallery "+Names[z],w,z);
            }
        }

        // ---- presentation only: the hand-over and the arms stage read the view and the clock, write no authority state ----
        private static void PresentationOnly(string root)
        {
            var iron=R(root,"Assets/ChipArena/Runtime/NativeIronworksKindsPresentation.cs");var motion=R(root,"Assets/ChipArena/Core/NativeIronworksMotion.cs");
            foreach(var bad in new[]{".Hp=","Damage(","LastAttack=","m.Yaw=",".radius=",".height=","Body.position=","linearVelocity=","isKinematic=",".Pve.Entry=","NativeNet","Replica="})
                Check(!iron.Contains(bad,StringComparison.Ordinal)&&!motion.Contains(bad,StringComparison.Ordinal),"the Ironworks hand-over and motion never write `"+bad+"`");
            Check(!motion.Contains("NativeArena",StringComparison.Ordinal)&&!motion.Contains("NativePilot",StringComparison.Ordinal)&&!motion.Contains("UnityEngine",StringComparison.Ordinal),"the Ironworks motion reads no authority state");
            var sim=R(root,"Assets/ChipArena/Runtime/NativePveHorde.cs");Check(!sim.Contains("Ironworks",StringComparison.Ordinal)&&!sim.Contains("PlaceProps",StringComparison.Ordinal),"the authority's horde has no prop code");
        }
    }
}
#endif
