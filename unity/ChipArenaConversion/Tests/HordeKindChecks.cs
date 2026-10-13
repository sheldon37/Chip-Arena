#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    // The Siren minion kinds (Core/NativeHordeKinds, NativeHordeModels, NativeHordeMotion, NativeHordeReact, NativeHordeFx;
    // Runtime/NativeHordeKindPresentation.cs): kinds resolve from the replicated kind ids; the presentation's strike and recovery
    // are the authority's formula and content timings (read out of NativePveHorde.cs and PveContent.json); the hit volumes are
    // the authority's capsules and every posed body stays inside its own; the crowd tiers all build, under budget, on the same
    // joints; feet in stance never slide; reactions scale with weight; no allocation per frame; and the runtime only reads.
    internal static class HordeKindChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Horde kinds: "+what);count++;}
        private static string R(string root,string path)=>File.ReadAllText(Path.Combine(root,path));
        public static void Run(string root)
        {
            Kinds(root);Timing(root);Volumes(root);Models();Inside();Feet();Reactions();Effects();Allocation();Runtime(root);Spec(root);
            Console.WriteLine("PASS horde kinds ("+count+" checks: kind ids resolve, attack timing equals the authority's, hit volumes unchanged and every posed body inside its own, three tiers under budget on every joint, glow codes, planted feet, weighted reactions, telegraph timing, no allocation, presentation-only runtime, the spec)");
        }
        // ---- the kind ids the network carries (NativeMinionFrame.addKind = PveAddStats.id) ----
        private static List<JsonElement> Adds(string root){var doc=JsonDocument.Parse(R(root,"Assets/Conversion/Resources/PveContent.json"));return doc.RootElement.GetProperty("adds").EnumerateArray().ToList();}
        private static void Kinds(string root)
        {
            for(int k=0;k<NativeHordeKinds.Count;k++){Check(NativeHordeKinds.Resolve(NativeHordeKinds.Ids[k])==k,"kind "+NativeHordeKinds.Ids[k]+" resolves to itself");Check(NativeHordeKinds.Known(NativeHordeKinds.Ids[k]),"kind "+k+" is known");}
            Check(NativeHordeKinds.Resolve(null)==NativeHordeKinds.Shambler&&NativeHordeKinds.Resolve("")==NativeHordeKinds.Shambler&&NativeHordeKinds.Resolve("future-kind")==NativeHordeKinds.Shambler&&!NativeHordeKinds.Known("future-kind"),"an unknown or missing kind draws as the shambler");
            Check(NativeHordeKinds.Resolve("Brute")==NativeHordeKinds.Shambler,"ids are exact (ordinal)");
            var ids=Adds(root).Select(a=>a.GetProperty("id").GetString()).ToList();
            foreach(var id in ids)Check(NativeHordeKinds.Known(id)&&NativeHordeKinds.Shipped[NativeHordeKinds.Resolve(id)],"shipped content kind "+id+" has a model");
            for(int k=0;k<NativeHordeKinds.Count;k++)Check(NativeHordeKinds.Shipped[k]==ids.Contains(NativeHordeKinds.Ids[k]),"Shipped matches the content for "+NativeHordeKinds.Ids[k]);
            foreach(var a in Adds(root)){var attack=a.GetProperty("attack").GetString();int k=NativeHordeKinds.Resolve(a.GetProperty("id").GetString());Check(NativeHordeKinds.AttackOf(k,attack,false)==NativeHordeKinds.Default[k],"content attack "+attack+" is its kind's");}
            Check(NativeHordeKinds.AttackOf(NativeHordeKinds.Brute,"smash",true)==NativeHordeKinds.Attack.Lunge,"a crawling add lunges (the authority's crawl attack)");
            for(int k=0;k<NativeHordeKinds.Count;k++)for(int id=10000;id<10400;id++){int l=NativeHordeKinds.Look(k,id);Check(l>=0&&l<NativeHordeKinds.Looks[k],"look in range");}
            var seen=new HashSet<int>();for(int id=10000;id<10400;id++)seen.Add(NativeHordeKinds.Look(NativeHordeKinds.Shambler,id));Check(seen.Count==NativeHordeKinds.Looks[NativeHordeKinds.Shambler],"every shambler look appears in a crowd");
        }
        // ---- the authority's attack timing (StepPveHorde) and the presentation's ----
        private static void Timing(string root)
        {
            var sim=R(root,"Assets/ChipArena/Runtime/NativePveHorde.cs");
            foreach(var s in new[]{"bool left=(a.Lost&(2|8))!=0,right=(a.Lost&(4|16))!=0,armless=left&&right;","hit=a.Stats.id==\"spitter\"?a.Stats.hit:a.Crawl||armless?.34f:a.Stats.hit;","m.LastAttack=Time;m.StrikeAt=Time+hit;a.Struck=false;a.RecoverAt=Time+Math.Max(a.Stats.cycle,hit+.05f);","(a.Crawl?.15f:armless?.45f:a.Stats.lunge)/Math.Max(.05f,hit)","Vector3 center=at+dir*NativeHordeKinds.SmashAheadOf(NativeHordeKinds.Resolve(a.Stats.id));if(new Vector2(q.Body.position.x-center.x,q.Body.position.z-center.z).magnitude>NativeHordeKinds.SmashRadiusOf(NativeHordeKinds.Resolve(a.Stats.id)))continue;","if(NativeHordeKinds.Staggers(NativeHordeKinds.Resolve(a.Stats.id),amount,m.MaxHp,impulse>=400)){a.StaggerAt=Time+a.Stats.stagger;a.Struck=true;if(a.Stats.id!=\"spitter\"||actual<=0)a.RecoverAt=Time;}"})
                Check(sim.Contains(s),"the authority's attack rule is still `"+s+"` (update NativeHordeKinds to follow it)");
            Check(NativeHordeKinds.SmashAhead==1.2f&&NativeHordeKinds.SmashRadius==1.6f,"the slam telegraph is the authority's disc");
            int[] losts={0,2,8,4,16,2|4,8|16,2|16,32,1};
            foreach(var a in Adds(root))
            {
                float hit=(float)a.GetProperty("hit").GetDouble(),cycle=(float)a.GetProperty("cycle").GetDouble(),lunge=(float)a.GetProperty("lunge").GetDouble();string id=a.GetProperty("id").GetString();
                foreach(bool crawl in new[]{false,true})foreach(int lost in losts)
                {
                    bool left=(lost&(2|8))!=0,right=(lost&(4|16))!=0,armless=left&&right;float simHit=crawl||armless?.34f:hit,simRecover=Math.Max(cycle,simHit+.05f),simLunge=crawl?.15f:armless?.45f:lunge;
                    Check(NativeHordeKinds.Strike(hit,crawl,lost)==simHit,id+" strike (crawl "+crawl+", lost "+lost+")");Check(NativeHordeKinds.Recover(hit,cycle,crawl,lost)==simRecover,id+" recovery");Check(NativeHordeKinds.Lunge(lunge,crawl,lost)==simLunge,id+" lunge");
                }
                // The gallery's rows for the shipped kinds are the content's own.
                var p=NativeHordeKinds.ProposedRow(NativeHordeKinds.Resolve(id));
                foreach(var f in new[]{"hp","damage","walk","run","reach","startRange","hit","cycle","lunge","impulse","reward","limbFrac","headFrac","stagger"})
                {float want=(float)a.GetProperty(f).GetDouble(),have=(float)typeof(PveAddStats).GetField(f).GetValue(p);Check(want==have,id+" proposed row "+f+" equals the content ("+have+" != "+want+")");}
                Check(p.attack==a.GetProperty("attack").GetString(),id+" attack name");
            }
            // The shown blow: no swing before its lead, the contact exactly at the strike, held telegraph, follow-through after.
            foreach(NativeHordeKinds.Attack attack in Enum.GetValues(typeof(NativeHordeKinds.Attack)))foreach(float strike in new[]{.3f,.34f,.43f,.55f,.62f})
            {
                float recover=Math.Max(1.05f,strike+.05f),lead=Math.Min(NativeHordeGait.SwingLead(attack),strike*.45f);
                NativeHordeGait.Channels(strike,strike,recover,attack,out _,out float atSwing,out float atFollow,out _,out float w);Check(atSwing==1&&w==1&&atFollow==1,"the blow lands at the strike ("+attack+" "+strike+")");
                NativeHordeGait.Channels(strike-lead-1e-3f,strike,recover,attack,out float cock,out float before,out _,out _,out _);Check(before==0&&cock==1,"the telegraph holds until the swing ("+attack+")");
                NativeHordeGait.Channels(strike-1f/120,strike,recover,attack,out _,out float almost,out _,out _,out _);Check(almost>.5f&&almost<1,"the swing is still travelling half a frame before the strike ("+attack+")");
                NativeHordeGait.Channels(recover+.3f,strike,recover,attack,out _,out _,out _,out _,out float after);Check(after==0,"the attack hands back to the gait after the recovery ("+attack+")");
                NativeHordeGait.Channels(float.NaN,strike,recover,attack,out float c0,out float s0,out float f0,out float st0,out float w0);Check(c0+s0+f0+st0+w0==0,"no attack without a replicated LastAttack");
                Check(NativeHordeFx.Brighten(strike,strike,recover)>=NativeHordeFx.Brighten(strike*.5f,strike,recover)&&NativeHordeFx.Brighten(float.NaN,strike,recover)==1,"the wind-up's glow peaks at the strike");
                Check(NativeHordeFx.SlamTelegraph(strike,strike,out _,out float fill,out _)&&Math.Abs(fill-NativeHordeKinds.SmashRadius)<1e-5f&&!NativeHordeFx.SlamTelegraph(strike+.06f,strike,out _,out _,out _),"the slam's fill reaches the authority's rim exactly at the strike");
            }
            Check(NativeHordeFx.Shockwave(0,out _,out _,out float a0)&&a0>.8f&&!NativeHordeFx.Shockwave(-.01f,out _,out _,out _),"the shockwave starts at the strike, never before");
            Check(!NativeHordeFx.Slash(.34f-.08f,.34f,out _,out _)&&NativeHordeFx.Slash(.34f,.34f,out float sweep,out _)&&sweep>0,"the claw crescent sweeps through the strike");
        }
        // ---- hit volumes: the authority's capsules, unchanged ----
        private static void Volumes(string root)
        {
            var sim=R(root,"Assets/ChipArena/Runtime/NativePveHorde.cs");
            Check(sim.Contains("height=heavy?NativeHordeKinds.HulkHeight:1.7f"),"ordinary and Hulk heights");
            Check(sim.Contains("c.radius=heavy?NativeHordeKinds.HulkRadius:.42f;c.height=height;"),"authority uses the Hulk capsule and keeps the ordinary radius");
            Check(sim.Contains("if(a.Crawl){m.Collider.height=.88f;m.Collider.radius=.32f;}"),"the crawl capsule is still .32 x .88");
            Check(NativeHordeKinds.AddRadius==.42f&&NativeHordeKinds.AddHeight==1.7f&&NativeHordeKinds.CrawlRadius==.32f&&NativeHordeKinds.CrawlHeight==.88f,"the presentation's capsules are the authority's");
            for(int k=0;k<NativeHordeKinds.Count;k++)if(NativeHordeKinds.Shipped[k]&&k!=NativeHordeKinds.Hulk)Check(NativeHordeKinds.Radius[k]==NativeHordeKinds.AddRadius&&NativeHordeKinds.Height[k]==NativeHordeKinds.AddHeight&&NativeHordeKinds.Axis[k]==1&&NativeHordeKinds.Root[k]==.85f,"shipped "+NativeHordeKinds.Ids[k]+" is sculpted for the add capsule");
            Check(NativeHordeKinds.RootHeight(NativeHordeKinds.Brute,true)==.44f&&NativeHordeKinds.RootHeight(NativeHordeKinds.Shambler,false)==.85f,"the floor under a root as the old presentation found it");
            // Nothing in the presentation touches a collider or a body.
            foreach(var f in new[]{"Assets/ChipArena/Runtime/NativeHordeKindPresentation.cs","Assets/ChipArena/Runtime/NativeHordeKindModels.cs"})
            {var t=R(root,f);foreach(var bad in new[]{".radius=",".height=","Collider.enabled","Body.position=","linearVelocity=","isKinematic=","AddComponent<"})Check(!t.Contains(bad),Path.GetFileName(f)+" never writes `"+bad+"`");}
        }
        // ---- models: tiers, budgets, joints, glow ----
        private static void Models()
        {
            for(int k=0;k<NativeHordeKinds.Count;k++)for(int l=0;l<NativeHordeKinds.Looks[k];l++)
            {
                HordeModel[] tiers=new HordeModel[NativeHordeModels.Tiers];for(int t=0;t<tiers.Length;t++){tiers[t]=NativeHordeModels.Build(k,l,t);Check(tiers[t].Triangles>0&&tiers[t].Triangles<=NativeHordeModels.Budget[t],NativeHordeModels.Id(k,l,t)+" triangles "+tiers[t].Triangles+" within "+NativeHordeModels.Budget[t]);}
                Check(tiers[0].Triangles>tiers[1].Triangles&&tiers[1].Triangles>tiers[2].Triangles,NativeHordeModels.Id(k,l,0)+" thins out tier by tier");
                var full=new SortedSet<int>(tiers[0].Mesh.Sections.Where(s=>s.Triangles>0).Select(s=>s.Bone));
                for(int b=1;b<22;b++)Check(full.Contains(b),NativeHordeModels.Id(k,l,0)+" has a part on joint "+b);
                for(int t=1;t<3;t++){var bones=new SortedSet<int>(tiers[t].Mesh.Sections.Where(s=>s.Triangles>0).Select(s=>s.Bone));Check(bones.SetEquals(full),NativeHordeModels.Id(k,l,t)+" keeps every joint (lost limbs, the char and the burn-away work on every tier)");}
                foreach(var m in tiers)foreach(var s in m.Mesh.Sections){Check(s.Positions.Count==s.Normals.Count&&s.Positions.Count==s.Colors.Count&&s.Positions.Count==s.Emission.Count&&s.Indices.All(i=>i>=0&&i<s.Positions.Count)&&s.Indices.Count%3==0,"section arrays agree");
                    foreach(var e in s.Emission){if(e.W>2.5f){NativeMinionFx.Decode(e.W,out int cls,out double cell);Check(cls>=0&&cls<=2&&cell>=0&&cell<=1&&e.X+e.Y+e.Z>.05f,"glow code decodes and glows");}else Check(e.W==0,"plain parts carry no tint code");}
                    foreach(var c in s.Colors)Check(Math.Max(c.X,Math.Max(c.Y,c.Z))<=1.0001f,"the glow never sits in the albedo (albedo at most 1)");}
                int cells=tiers[0].Mesh.Sections.Sum(s=>s.Emission.Count(e=>e.W>2.5f));Check(cells>0,NativeHordeModels.Id(k,l,0)+" has flickering seams");
                Check(NativeHordeModels.Tongues(k,l).Length>0&&NativeHordeModels.Tongues(k,l).All(g=>g.Joint>=0&&g.Joint<22&&(g.Layout==0||g.Layout==1)&&Math.Abs(g.Qx*g.Qx+g.Qy*g.Qy+g.Qz*g.Qz+g.Qw*g.Qw-1)<1e-6),"tongue anchors are valid");
            }
            Check(NativeHordeModels.Cracked(0,2)&&!NativeHordeModels.Cracked(0,0)&&!NativeHordeModels.Cracked(2,0),"only the cracked look keeps the crack haze");
        }
        // ---- every posed body inside its hit volume (the trunk, head and legs; reaching arms a little beyond, as claws) ----
        private static void Inside()
        {
            var pose=new CapturedRig.Pose();
            for(int k=0;k<NativeHordeKinds.Count;k++)for(int l=0;l<NativeHordeKinds.Looks[k];l++)
            {
                var model=NativeHordeModels.Build(k,l,0);var st=NativeHordeKinds.ProposedRow(k);
                foreach(float world in new[]{0,st.walk,st.run})
                {
                    // The gait runs in the rig's metres (a scaled kind's authority speed over its scale).
                    float speed=world/NativeHordeKinds.Scale[k];
                    var g=new NativeHordeGait();var f=new HordeMotionInput{Kind=k,Look=l,Id=20000+k*10+l,Seed=NativeHordeKinds.Seed(20000+k*10+l),Health=1,AttackAge=float.NaN};
                    float worstTrunk=0,worstShoulder=0,worstArm=0,worstLeg=0,low=1e9f;
                    for(int i=0;i<200;i++)
                    {
                        float t=i/60f;f.Time=t;f.Root=new Vector3(0,NativeHordeKinds.Root[k],speed*t);f.Velocity=new Vector3(0,0,speed);f.Ground=0;f.Yaw=0;g.Pose(f,pose);if(i<80||i%7!=0)continue;
                        foreach(var s in model.Mesh.Sections)
                        {
                            int region=s.Bone<=CapturedRig.Head?0:s.Bone==CapturedRig.LClav||s.Bone==CapturedRig.RClav?1:s.Bone<=CapturedRig.RHand?2:3;var q=pose.WorldRotation[s.Bone];var o=pose.WorldPosition[s.Bone];
                            foreach(var v in s.Positions)
                            {
                                // Bank vertices are in Unity axes: into the source frame (x mirrored), then posed; the root stands Root above the floor.
                                var w=o+Vector3.Transform(new Vector3(-v.X,v.Y,v.Z),q);w.Y+=NativeHordeKinds.Root[k];float d=Outside(k,w);low=Math.Min(low,w.Y);
                                if(region==0)worstTrunk=Math.Max(worstTrunk,d);else if(region==1)worstShoulder=Math.Max(worstShoulder,d);else if(region==2)worstArm=Math.Max(worstArm,d);else worstLeg=Math.Max(worstLeg,d);
                            }
                        }
                    }
                    string what=NativeHordeModels.Id(k,l,0)+" at "+world+" m/s";bool run=world>=st.run*.99f&&world>0,still=world==0;
                    // The body's mass stays inside (a sprint's lean a little over); shoulders, reaching claws and striding legs as a pilot's.
                    Check(worstTrunk<=(still?.11f:run?.16f:.1f),what+": trunk and head inside the hit volume (worst "+worstTrunk.ToString("0.000")+" m out)");
                    Check(worstShoulder<=.15f,what+": shoulders at the capsule's edge (worst "+worstShoulder.ToString("0.000")+")");
                    Check(worstArm<=.42f,what+": arms and claws within reach (worst "+worstArm.ToString("0.000")+")");
                    Check(worstLeg<=(still?.09f:.45f),what+": legs inside standing, striding no farther than a step (worst "+worstLeg.ToString("0.000")+")");
                    Check(low>=-.03f,what+": no part under the floor ("+low+")");
                }
            }
        }
        // How far a source-frame point (root at the origin plus its height) lies outside kind k's capsule (0 inside).
        private static float Outside(int k,Vector3 w)
        {
            float r=NativeHordeKinds.Radius[k],h=NativeHordeKinds.Height[k];
            if(NativeHordeKinds.Axis[k]==2)
            {
                float cz=NativeHordeKinds.CenterAhead[k],cy=NativeHordeKinds.Root[k],half=h*.5f-r;float z=Math.Max(cz-half,Math.Min(cz+half,w.Z));
                return Math.Max(0,new Vector3(w.X,w.Y-cy,w.Z-z).Length()-r);
            }
            float y=Math.Max(r,Math.Min(h-r,w.Y));return Math.Max(0,new Vector3(w.X,w.Y-y,w.Z).Length()-r);
        }
        // ---- planted feet: a foot in stance never moves in the world; standing, it stays put ----
        private static void Feet()
        {
            var pose=new CapturedRig.Pose();
            for(int k=0;k<NativeHordeKinds.Count;k++)
            {
                var st=NativeHordeKinds.ProposedRow(k);int feet=k==NativeHordeKinds.Crawler?4:2;
                foreach(float world in new[]{st.walk,st.run,1.1f})foreach(float turn in new[]{0f,40f})
                {
                    float speed=world/NativeHordeKinds.Scale[k];
                    var g=new NativeHordeGait();var f=new HordeMotionInput{Kind=k,Id=30000+k,Seed=NativeHordeKinds.Seed(30000+k),Health=1,AttackAge=float.NaN};var last=new Vector3[4];var air=new bool[4];float worst=0;int steps=0;Vector3 at=new Vector3(0,.85f,0);float yaw=0;
                    // (A scaled kind steps as slowly as it is big: it walks as long again.)
                    int frames=(int)(400*NativeHordeKinds.Scale[k]);
                    for(int i=0;i<frames;i++)
                    {
                        float t=i/60f;yaw+=turn/60;float r=yaw*(float)Math.PI/180;var v=new Vector3((float)Math.Sin(r),0,(float)Math.Cos(r))*speed;at+=v/60;
                        f.Time=t;f.Root=at;f.Velocity=v;f.Yaw=yaw;g.Pose(f,pose);
                        for(int j=0;j<feet;j++){if(i>60&&!air[j]&&!g.FootAir[j])worst=Math.Max(worst,Vector3.Distance(last[j],g.FootWorld[j]));if((g.Landed&(1<<j))!=0)steps++;last[j]=g.FootWorld[j];air[j]=g.FootAir[j];}
                    }
                    Check(worst<1e-4f,NativeHordeKinds.Ids[k]+" at "+speed+" m/s turning "+turn+" deg/s: planted feet never slide (worst "+worst+" m)");
                    Check(steps>=feet*4,NativeHordeKinds.Ids[k]+" steps as it moves ("+steps+")");
                }
                // Standing: no steps, the feet stay where they are.
                {
                    var g=new NativeHordeGait();var f=new HordeMotionInput{Kind=k,Id=31000+k,Seed=NativeHordeKinds.Seed(31000+k),Health=1,AttackAge=float.NaN,Root=new Vector3(0,.85f,0)};int steps=0;
                    for(int i=0;i<300;i++){f.Time=i/60f;g.Pose(f,pose);if(i>5)steps+=g.Landed!=0?1:0;}Check(steps==0,NativeHordeKinds.Ids[k]+" stands without stepping");
                }
            }
        }
        // ---- reactions: weight by size, direction, stagger steps, hit-stop ----
        private static void Reactions()
        {
            float Peak(int kind,StaggerWeight w,float amount){var r=new NativeHordeReact();r.Bind(kind);r.Add(1,new Vector2(0,-1),w,amount,true,false,false,0);float most=0;for(int i=0;i<60;i++){r.Step(1+i/60f);most=Math.Max(most,r.Shove(1+i/60f).Length());}return most;}
            float runner=Peak(NativeHordeKinds.Runner,StaggerWeight.Heavy,48),shambler=Peak(NativeHordeKinds.Shambler,StaggerWeight.Heavy,48),brute=Peak(NativeHordeKinds.Brute,StaggerWeight.Heavy,48);
            Check(runner>shambler&&shambler>brute&&brute>0,"a greatsword shoves the runner most and the brute least ("+runner+", "+shambler+", "+brute+")");
            Check(Peak(0,StaggerWeight.Heavy,48)>Peak(0,StaggerWeight.Step,26)&&Peak(0,StaggerWeight.Step,26)>Peak(0,StaggerWeight.Flinch,14),"greatsword over sword over dagger");
            var react=new NativeHordeReact();react.Bind(0);react.Add(2,new Vector2(1,0),StaggerWeight.Heavy,48,true,false,true,.45f);
            Check(react.Hold(2.01f)==1&&react.Hold(2.2f)==0,"a melee blow's hit-stop holds, then lets go");
            Check(react.Staggering(2.1f)&&!react.Staggering(3.5f),"the stagger asks for catch steps for a moment");
            Check(!float.IsNaN(react.Reel(2.2f))&&float.IsNaN(react.Reel(3.5f)),"the authority's stagger reels for its seconds");
            for(int i=0;i<40;i++){react.Step(2+i/60f);var s=react.Shove(2+i/60f);Check(s.Length()<=NativeStaggerRules.HardHip*1.6f+1e-4f,"the shove is bounded");if(i==6)Check(s.X>0,"the hips go the way of the push");}
            // A heavy blow on a standing add makes it step.
            var g=new NativeHordeGait();var r2=new NativeHordeReact();r2.Bind(0);var pose=new CapturedRig.Pose();var f=new HordeMotionInput{Kind=0,Id=40000,Seed=NativeHordeKinds.Seed(40000),Health=1,AttackAge=float.NaN,Root=new Vector3(0,.85f,0)};int steps=0;
            for(int i=0;i<180;i++){f.Time=i/60f;if(i==60)r2.Add(1,new Vector2(0,-1),StaggerWeight.Heavy,48,true,false,true,.45f);g.Pose(f,pose,r2);if(i>60)steps+=g.Landed!=0?1:0;}
            Check(steps>=1,"a heavy blow knocks a standing add into stagger-back steps ("+steps+")");
            // Rewind (the killcam): the hits start over.
            r2.Step(.2f);Check(r2.Count==0,"a rewind forgets the hits");
        }
        // ---- effects: deterministic and bounded ----
        private static void Effects()
        {
            for(int i=0;i<NativeHordeFx.DebrisCount;i++){Check(NativeHordeFx.Debris(7,i,.3f,out var o,out float s,out _,out float a)&&o.Y>=0&&s>0&&a>0,"slam debris flies");Check(!NativeHordeFx.Debris(7,i,2,out _,out _,out _,out _),"slam debris settles out");}
            Check(NativeHordeFx.Glob(new Vector3(0,1.4f,0),new Vector3(0,0,6),.8f,.4f,out var mid)&&mid.Y>1.4f&&!NativeHordeFx.Glob(new Vector3(0,1.4f,0),new Vector3(0,0,6),.8f,.81f,out _),"the glob arcs and lands");
            Check(NativeHordeFx.Puddle(.1f,4,1.3f,out float r,out _)&&r<1.3f&&NativeHordeFx.Puddle(1,4,1.3f,out float r2,out _)&&r2>1.3f*.88f&&r2<1.3f&&NativeHordeFx.Puddle(3.5f,4,1.3f,out float r3,out _)&&Math.Abs(r3-1.3f)<1e-4f&&!NativeHordeFx.Puddle(4.1f,4,1.3f,out _,out _),"the puddle creeps out to its radius (never past it) and fades out");
            Check(NativeHordeFx.Swell(NativeHordeKinds.Attack.Spit,.5f,.55f)>.8f&&NativeHordeFx.Swell(NativeHordeKinds.Attack.Swipe,.5f,.55f)==0,"only the spitter's sac and the bloater's belly swell");
            NativeHordeFx.Gib(3,1,0,new Vector2(0,1),1,.8f,out var g0,out _,out _);NativeHordeFx.Gib(3,1,3,new Vector2(0,1),1,.8f,out var g3,out _,out _);Check(g0.Length()<1e-5f&&Math.Abs(g3.Y+.8f)<1e-4f&&g3.Z>0,"a sliced part leaves from its joint, flies along the blow and rests on the floor");
            Check(NativeHordeFx.PartVanish(1)==0&&NativeHordeFx.PartVanish(2.4f)==1,"a severed part burns away with the body");
        }
        // ---- no allocation per frame ----
        private static void Allocation()
        {
            var pose=new CapturedRig.Pose();var gaits=new NativeHordeGait[NativeHordeKinds.Count];var reacts=new NativeHordeReact[NativeHordeKinds.Count];var m=new double[16];
            for(int k=0;k<gaits.Length;k++){gaits[k]=new NativeHordeGait();reacts[k]=new NativeHordeReact();reacts[k].Bind(k);}
            void Frame(int i)
            {
                float t=i/60f;
                for(int k=0;k<gaits.Length;k++)
                {
                    var st=NativeHordeKinds.ProposedRow(k);float strike=NativeHordeKinds.Strike(st.hit,false,0),recover=NativeHordeKinds.Recover(st.hit,st.cycle,false,0);
                    if(i%90==10)reacts[k].Add(t,new Vector2(0,-1),StaggerWeight.Step,26,true,false,false,0);
                    var f=new HordeMotionInput{Kind=k,Id=50000+k,Seed=NativeHordeKinds.Seed(50000+k),Time=t,Root=new Vector3(0,.85f,t*1.4f),Velocity=new Vector3(0,0,1.4f),Health=1,AttackAge=(i%120)/60f,Strike=strike,Recover=recover,Attack=NativeHordeKinds.Default[k],Side=1,HasTarget=true,Target=new Vector3(1,.85f,t*1.4f+3)};
                    gaits[k].Pose(f,pose,reacts[k]);NativeHordeModels.TongueMatrix(k,0,i%3,t,1.2,.4,m,out _,out _);
                    NativeHordeFx.Brighten(f.AttackAge,strike,recover);NativeHordeFx.SlamTelegraph(f.AttackAge,strike,out _,out _,out _);for(int d=0;d<NativeHordeFx.DebrisCount;d++)NativeHordeFx.Debris(3,d,f.AttackAge-strike,out _,out _,out _,out _);
                    for(int b=0;b<NativeHordeFx.BurstPieces;b++)NativeHordeFx.Burst(5,b,f.AttackAge,out _,out _,out _,out _);NativeHordeFx.Gib(3,k,f.AttackAge,new Vector2(0,1),1,.7f,out _,out _,out _);
                    NativeHordeGait.Death(pose,k,9,f.AttackAge,new Vector2(0,-1));
                }
            }
            for(int i=0;i<400;i++)Frame(i);
            long before=GC.GetAllocatedBytesForCurrentThread();for(int i=400;i<2400;i++)Frame(i);long used=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(used==0,"no allocation per frame over 2000 frames of six kinds walking, attacking, hit, dying ("+used+" bytes)");
        }
        // ---- the runtime: presentation only, hooked where the adds are drawn, tiers as the source's crowd ----
        private static void Runtime(string root)
        {
            var p=R(root,"Assets/ChipArena/Runtime/NativeHordeKindPresentation.cs");
            foreach(var bad in new[]{".Hp=","Damage(","Emit(","StaggerAt=","RecoverAt=","StrikeAt=","LastAttack=","Yaw=m.","m.Yaw="})Check(!p.Contains(bad),"the kind presentation never writes `"+bad+"`");
            Check(p.Contains("LowGraphics?HordeFullRigsLow:HordeFullRigs")&&p.Contains("HordeFullRigs=24,HordeFullRigsLow=16")&&p.Contains("HordeReducedRange=40"),"the crowd tiers: 24 full (16 on the low tier), reduced to 40 m, impostors beyond");
            Check(p.Contains("NativeHordeKinds.Strike(st0,a.Crawl,a.Lost)")&&p.Contains("float age=now-m.LastAttack;"),"attacks are timed from the replicated LastAttack and the content's strike");
            Check(p.Contains("NativeHordeKinds.Interrupts(kind,attack,e.Amount,m.MaxHp,heavy)")&&p.Contains("s.CancelFor==m.LastAttack&&now>=s.CancelAt"),"a stagger in the wind-up cancels the shown blow as the authority cancels the real one");
            Check(p.Contains("NativeHordeKinds.Resolve(a.Stats?.id)"),"the kind keys off the replicated add kind");
            Check(p.Contains("HitFlashCode(m.Id,now)"),"the struck flash on the body");
            Check(p.Contains("blood.Spray(")&&p.Contains(",kill,true);"),"hits spray the Siren's ink into the blood sim");
            var source=R(root,"Assets/ChipArena/Runtime/NativeSourcePresentation.cs");Check(source.Contains("if(add!=null&&HordeKindAdd(arena,m,rig,pos))continue;")&&source.Contains("BeginHordeFx(arena);HordeKindsBegin(arena);")&&source.Contains("HordeKindsEnd(arena);PveGore(arena);"),"SourceWorld draws every horde add through the kinds");
            Check(R(root,"Assets/ChipArena/Runtime/NativeSummonPresentation.cs").Contains("if(horde&&HordeKindDeath(death,rig,age))return true;"),"deaths by kind");
            Check(R(root,"Assets/ChipArena/Runtime/NativeDevEffects.cs").Contains("DrawHordeFx(camera);DrawHordeKindFx(arena,camera);DrawHordeBursts(camera);"),"the effects with the minion effects");
            Check(R(root,"Assets/ChipArena/Runtime/NativeSourceModels.cs").Contains("PilotBodies();BowModels();HordeKindModels();"),"the models are built with the bank");
            var blood=R(root,"Assets/ChipArena/Core/NativeBlood.cs");Check(blood.Contains("Stain=stain,Ink=ink};")&&blood.Contains("surface,time,d.Ink);"),"ink drops keep their ink into their splats");
            var audio=p.Substring(p.IndexOf("public sealed partial class NativeAudio",StringComparison.Ordinal));Check(!audio.Contains("Play(")||audio.Split("BankPlay(").Length-1==audio.Split("Play(").Length-1,"the minion cues are bank-only (silent without LocalAssets)");
            foreach(var f in new[]{"NativeHordeKinds.cs","NativeHordeModels.cs","NativeHordeMotion.cs","NativeHordeReact.cs","NativeHordeFx.cs"}){var t=R(root,"Assets/ChipArena/Core/"+f);Check(!t.Contains("NativeArena")&&!t.Contains("NativePilot")&&!t.Contains("new Random(")&&!t.Contains("UnityEngine"),f+" reads no authority state and no clock-free randomness");}
            var keys=new HashSet<string>(NativeSoundKeys.All.Select(d=>d.Key));
            foreach(NativeHordeKinds.Attack a in Enum.GetValues(typeof(NativeHordeKinds.Attack))){foreach(var key in new[]{NativeHordeKinds.WindupCue(a),NativeHordeKinds.StrikeCue(a),NativeHordeKinds.StrikeUnder(a)})if(key!=null)Check(keys.Contains(key),"cue "+key+" is an existing bank key");}
            for(int k=0;k<NativeHordeKinds.Count;k++){var key=NativeHordeKinds.StepCue(k);if(key!=null)Check(keys.Contains(key),"step cue "+key+" is an existing bank key");}
        }
        // ---- the spec for Astra ----
        private static void Spec(string root)
        {
            // The spec sits in the repository's docs (a packaged Unity source without them skips this part).
            string path=Path.Combine(root,"../../docs/SIREN-MINION-VARIANTS.md");if(!File.Exists(path)){Console.WriteLine("SKIP horde kinds spec: docs/SIREN-MINION-VARIANTS.md is not in this tree");return;}var doc=File.ReadAllText(path);
            foreach(var id in NativeHordeKinds.Ids)Check(doc.Contains("`"+id+"`"),"the spec names "+id);
            foreach(var s in new[]{"minion-spit","minion-acid","minion-slam","minion-charge","minion-burst","staggerAt","Prompt for Astra"})Check(doc.Contains(s),"the spec covers "+s);
            for(int k=0;k<NativeHordeKinds.Count;k++){if(NativeHordeKinds.Shipped[k])continue;var p=NativeHordeKinds.ProposedRow(k);Check(doc.Contains("| `"+p.id+"` | "+p.hp+" | "+p.damage+" |"),"the spec's "+p.id+" row matches NativeHordeKinds.Proposed");}
        }
    }
}
#endif
