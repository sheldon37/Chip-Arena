#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
namespace ChipArena.UnityConversion
{
    // The Deployment Station's runtime presentation (Core NativeDeployModels, NativeDeployArt, NativeDeployStage; Runtime
    // NativeDeployScene, NativeDeployCast, NativeDeployConsoleUI, NativeDeployPreview): every model builds the same twice with
    // clean geometry inside its declared footprint and triangle budget, its lights unlit (the stage grade leaves them raw) and
    // its glass see-through; the moving parts' pivots sit on their bodies; the screens carry their uv; the stage, driven through
    // a real authority's lifecycle, lands every reel and dial on the result, stamps PENDING before Deploy and ACTIVE only after,
    // opens the gate, freezes the map, lights the station and allocates nothing per frame; the art rasterises; the runtime draws
    // from the stage, uses Preview, and only the DEV preview asks the authority.
    internal static class DeployModelChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Deploy models: "+what);count++;}
        private static readonly ExpeditionInstanceTheme[] Pool={new ExpeditionInstanceTheme{id="exp-ward-zero",theme="quarantine"},new ExpeditionInstanceTheme{id="exp-transit",theme="rail"},new ExpeditionInstanceTheme{id="exp-foundry",theme="industrial"}};
        private static readonly string[] Mutators={"surge","storm","darkness","brittle","windfall","second-wind"};
        public static void Run(string root)
        {
            count=0;Models();Pivots();Screens();Stage();Allocation();Art();Wiring(root);
            Console.WriteLine("PASS Deploy models: "+count+" checks (deterministic clean models inside their footprints and budgets, unlit lights and see-through glass, "+
                "pivots on their bodies, screen uvs; the stage through a real authority: reels and dials land on the result, PENDING until Deploy and ACTIVE after, the gate, "+
                "the frozen map, eight practical lights, no per-frame allocation; the art; the runtime wiring)");
        }
        private static readonly (string id,Func<WeaponMesh> build,int budget)[] Built=
        {
            (NativeDeployModels.Instance,NativeDeployModels.InstanceMachine,22000),(NativeDeployModels.Mutator,NativeDeployModels.MutatorMachine,15000),
            (NativeDeployModels.Table,NativeDeployModels.WarTable,13000),(NativeDeployModels.Pad,NativeDeployModels.PartyPad,1800),(NativeDeployModels.Gate,NativeDeployModels.GateFrame,6000),
            (NativeDeployModels.Console,NativeDeployModels.DeployConsole,2500),(NativeDeployModels.Room,NativeDeployModels.StationRoom,32000),
        };
        private static void Models()
        {
            int total=0;
            foreach(var (id,build,budget) in Built)
            {
                var m=build();Clean(m,id);Check(Same(m,build()),id+" builds the same twice");Check(m.Triangles>200&&m.Triangles<=budget,id+" budget ("+m.Triangles+")");total+=m.Triangles;
                NativeDeployModels.Footprint(id,out var lo,out var hi);m.Bounds(-1,out var min,out var max);const float e=.002f;
                Check(min.X>=lo.X-e&&min.Y>=lo.Y-e&&min.Z>=lo.Z-e&&max.X<=hi.X+e&&max.Y<=hi.Y+e&&max.Z<=hi.Z+e,id+" inside its footprint ("+min+" .. "+max+")");
                Check(max.X-min.X>(hi.X-lo.X)*.8f&&max.Y-min.Y>(hi.Y-lo.Y)*.7f,id+" fills its footprint");
                Check(m.Sections.Any(s=>s.Bone==NativeDeployModels.Body)&&m.Sections.Any(s=>s.Bone==NativeDeployModels.Lights),id+" has a body and lights");
            }
            var parts=new (string id,WeaponMesh m,int budget)[]{("lever",NativeDeployModels.Lever(),2500),("drum",NativeDeployModels.Drum(),400),("rims",NativeDeployModels.DrumRims(),2500),("card",NativeDeployModels.CardDial(),200),
                ("map",NativeDeployModels.MapPlane(),400),("holo",NativeDeployModels.HoloVolume(),400),("beacon",NativeDeployModels.Beacon(),300),("door-left",NativeDeployModels.DoorLeaf(-1),4000),
                ("door-right",NativeDeployModels.DoorLeaf(1),4000),("lift",NativeDeployModels.LiftDeck(),2500),("lamp",NativeDeployModels.Lamp(),400),("beam",NativeDeployModels.Beam(),1200),("quad",NativeDeployModels.Quad(),4)};
            foreach(var (id,m,budget) in parts){Clean(m,id);Check(m.Triangles>0&&m.Triangles<=budget,id+" budget ("+m.Triangles+")");total+=m.Triangles;}
            Check(total<130000,"the whole station's budget ("+total+")");
            Check(Same(NativeDeployModels.Drum(),NativeDeployModels.Drum())&&Same(NativeDeployModels.CardDial(),NativeDeployModels.CardDial())&&Same(NativeDeployModels.Lever(),NativeDeployModels.Lever()),"the moving parts build the same twice");
            // the right leaf mirrors the left about the gate's centre
            NativeDeployModels.DoorLeaf(-1).Bounds(-1,out var l0,out var l1);NativeDeployModels.DoorLeaf(1).Bounds(-1,out var r0,out var r1);
            Check(Math.Abs(l0.X+r1.X)<1e-3f&&Math.Abs(l1.X+r0.X)<1e-3f&&l1.X<=.01f&&l0.X>=-2.5f,"the leaves meet at the centre and mirror ("+l0+" .. "+l1+")");
        }
        private static void Clean(WeaponMesh mesh,string what)
        {
            foreach(var s in mesh.Sections)
            {
                Check(s.Positions.Count==s.Normals.Count&&s.Positions.Count==s.Colors.Count&&s.Positions.Count==s.Emission.Count&&s.Indices.Count%3==0&&s.Indices.All(i=>i>=0&&i<s.Positions.Count),what+" section arrays agree");
                Check(s.Positions.All(p=>float.IsFinite(p.X)&&float.IsFinite(p.Y)&&float.IsFinite(p.Z))&&s.Normals.All(n=>Math.Abs(n.Length()-1)<1e-3f),what+" finite positions and unit normals");
                Check(s.Colors.All(c=>c.X>=0&&c.Y>=0&&c.Z>=0&&c.X<=1.0001f&&c.Y<=1.0001f&&c.Z<=1.0001f&&c.W<=1&&(c.W>0||s.Bone==NativeDeployModels.Screen&&c.W>=0))&&s.Emission.All(v=>v.W==0),what+" colours in range (a screen may fade to clear), no emission tags");
                if(s.Bone==NativeDeployModels.Glass)Check(s.Colors.All(c=>c.W<1),what+" glass is see-through");
                else if(s.Bone!=NativeDeployModels.Screen)Check(s.Colors.All(c=>c.W==1),what+" solid parts are opaque");
                // lights (bone 1 and the extra light bones) are unlit: colour equal to emission, so the stage's grade leaves them raw
                if(s.Bone>=NativeDeployModels.Lights)for(int i=0;i<s.Colors.Count;i++){var c=s.Colors[i];var e=s.Emission[i];if(Math.Abs(c.X-e.X)+Math.Abs(c.Y-e.Y)+Math.Abs(c.Z-e.Z)>1e-4f||e.X+e.Y+e.Z<=0){Check(false,what+" light bone "+s.Bone+" is unlit");break;}}
                if(s.Bone==NativeDeployModels.Screen)Check(s.Data.Count==s.Positions.Count,what+" screen section carries its uv");
            }
        }
        private static bool Same(WeaponMesh a,WeaponMesh b)
        {
            if(a.Sections.Count!=b.Sections.Count)return false;
            for(int i=0;i<a.Sections.Count;i++){var x=a.Sections[i];var y=b.Sections[i];if(x.Bone!=y.Bone||!x.Positions.SequenceEqual(y.Positions)||!x.Indices.SequenceEqual(y.Indices)||!x.Colors.SequenceEqual(y.Colors)||!x.Data.SequenceEqual(y.Data))return false;}
            return true;
        }
        private static bool Near(WeaponMesh m,int bone,Vector3 at,float within)=>m.Sections.Where(s=>s.Bone==bone).SelectMany(s=>s.Positions).Any(p=>Vector3.Distance(p,at)<within);
        private static void Pivots()
        {
            var inst=NativeDeployModels.InstanceMachine();var mut=NativeDeployModels.MutatorMachine();var gate=NativeDeployModels.GateFrame();
            // the lever's hub face is on the cabinet's side at its pivot, and the lever stays in the footprint at rest
            var lp=NativeDeployModels.LeverPivot;Check(Near(inst,0,new Vector3(-1.2f,lp.Y,lp.Z+.13f),.03f)&&Near(inst,0,new Vector3(-1.2f,lp.Y+.13f,lp.Z),.03f),"the lever's hub sits on the cabinet at its pivot");
            var lever=NativeDeployModels.Lever();lever.Bounds(-1,out var a0,out var a1);Check(a0.X>=-.24f&&a0.Y>-.15f&&a1.Y>1,"the lever stands out from its hub ("+a0+" .. "+a1+")");
            for(float pull=0;pull<=1.001f;pull+=.125f)foreach(var p in lever.Sections.SelectMany(x=>x.Positions)){var q=Vector3.Transform(p,Matrix4x4.CreateRotationX(NativeDeployModels.LeverAngle(pull))*Matrix4x4.CreateTranslation(lp));if(q.X>-1.03f&&Vector3.Distance(q,lp)>.16f){Check(false,"the lever clears the cabinet's cheek at pull "+pull+" "+q);break;}}
            NativeDeployModels.Footprint(NativeDeployModels.Instance,out var lo,out var hi);
            foreach(var p in lever.Sections.SelectMany(s=>s.Positions)){var q=Vector3.Transform(p,Matrix4x4.CreateRotationX(NativeDeployModels.LeverAngle(0))*Matrix4x4.CreateTranslation(lp));if(q.X<lo.X-.002f||q.Y>hi.Y||q.Z<lo.Z||q.Z>hi.Z){Check(false,"the lever at rest inside the footprint "+q);break;}}
            Check(NativeDeployModels.LeverAngle(0)>0&&NativeDeployModels.LeverAngle(1)<-.9f&&NativeDeployModels.LeverAngle(2)==NativeDeployModels.LeverAngle(1),"the lever rests back and pulls forward, clamped");
            // each reel's axle has its stubs in the cavity, and the drum turns clear of the cavity's walls
            for(int r=0;r<3;r++){var at=NativeDeployModels.ReelAt(r);Check(Near(inst,0,at+new Vector3(-(NativeDeployModels.DrumWidth/2+.02f),0,0),.035f)&&Near(inst,0,at+new Vector3(NativeDeployModels.DrumWidth/2+.02f,0,0),.035f),"reel "+r+" axle stubs");}
            Check(Math.Abs(2*Math.PI*NativeDeployModels.DrumRadius/NativeDeployModels.DrumSlots-NativeDeployModels.DrumWidth)<.005,"the reel's symbols are square");
            // each dial's spindle hub is on the dome deck under its card; the card turns inside the dome
            for(int r=0;r<3;r++){var c=NativeDeployModels.CardAt(r);Check(Near(mut,0,new Vector3(c.X,1.14f,c.Z),.02f)&&Near(mut,0,new Vector3(c.X,c.Y+NativeDeployModels.CardHeight/2+.05f,c.Z),.04f),"dial "+r+" hub and bearing");}
            Check(NativeDeployModels.CardWidth/2<.3f-.01f,"neighbouring cards never touch as they turn");
            // the doors close inside the opening under the header, the lift stands in the bay
            NativeDeployModels.DoorLeaf(-1).Bounds(-1,out var d0,out var d1);Check(d0.Y>=-.001f&&d1.Y<=4.46f&&Near(gate,0,new Vector3(-2.45f,2,.0f),.8f),"the leaves close under the header");
            NativeDeployModels.LiftDeck().Bounds(-1,out var f0,out var f1);var la=NativeDeployModels.LiftAt;Check(la.Z+f0.Z>.25f&&la.Z+f1.Z<4.1f&&f1.X<=2.45f&&f0.X>=-2.45f,"the lift's deck in the bay");
            for(int i=0;i<NativeDeployPresentation.Pads;i++){var g=NativeDeployModels.GateLampAt(i);Check(Near(gate,0,g+new Vector3(0,0,.01f),.08f),"gate lamp socket "+i);}
            // the machines face the room's middle; Root places a model there
            Check(Vector3.Dot(NativeDeployModels.Facing(NativeDeployPresentation.InstanceYaw),Vector3.Normalize(NativeDeployPresentation.Table-NativeDeployPresentation.InstanceMachineAt))>.6f,"the instance machine faces the table");
            Check(Vector3.Dot(NativeDeployModels.Facing(NativeDeployPresentation.MutatorYaw),Vector3.Normalize(NativeDeployPresentation.Table-NativeDeployPresentation.MutatorMachineAt))>.6f,"the mutator machine faces the table");
            Check(Vector3.Distance(Vector3.Transform(Vector3.Zero,NativeDeployModels.Root(NativeDeployPresentation.InstanceMachineAt,55)),NativeDeployPresentation.InstanceMachineAt)<1e-4f,"Root places the model");
        }
        private static void Screens()
        {
            // the drum's band: uv x across, y the strip position in symbols, 0 at the front, the seam at the back
            var drum=NativeDeployModels.Drum();var band=drum.Sections.Single(s=>s.Bone==NativeDeployModels.Screen);
            Check(band.Data.All(d=>d.X>=0&&d.X<=1&&d.Y>=-NativeDeployModels.DrumSlots/2f-1e-3f&&d.Y<=NativeDeployModels.DrumSlots/2f+1e-3f),"the band's uv in range");
            for(int i=0;i<band.Positions.Count;i++){var p=band.Positions[i];if(p.Z<-NativeDeployModels.DrumRadius*.999f&&Math.Abs(band.Data[i].Y)>1e-3f){Check(false,"the front reads symbol 0");break;}if(p.Y>0&&p.Z<0&&band.Data[i].Y<=0){Check(false,"above the front reads later symbols");break;}}
            for(int i=0;i+2<band.Indices.Count;i+=3){var a=band.Positions[band.Indices[i]];var n=band.Normals[band.Indices[i]];if(Vector3.Dot(n,new Vector3(0,a.Y,a.Z))<0){Check(false,"the band faces out");break;}}
            // the card: each face reads left to right from its own side
            var card=NativeDeployModels.CardDial().Sections.Single(s=>s.Bone==NativeDeployModels.Screen);
            for(int i=0;i<card.Positions.Count;i++){var p=card.Positions[i];float u=card.Data[i].X;bool front=p.Z<0;if(front&&(p.X>0)!=(u>.5f)||!front&&(p.X<0)!=(u>.5f)){Check(false,"card face "+(front?"front":"back")+" reads from its side");break;}}
            Check(card.Data.All(d=>d.X>=.5f-NativeDeployModels.CardArt/2-1e-3f&&d.X<=.5f+NativeDeployModels.CardArt/2+1e-3f),"the card shows the tile's art band");
            // the map plane: uv (0,0) south-west, matching MapPoint
            var map=NativeDeployModels.MapPlane().Sections.Single();int sw=Enumerable.Range(0,map.Positions.Count).OrderBy(i=>map.Positions[i].X+map.Positions[i].Z).First();
            Check(map.Data[sw].X<1e-3f&&map.Data[sw].Y<1e-3f&&Vector3.Distance(map.Positions[sw],NativeDeployModels.MapPoint(-NativeDeployPresentation.WorldHalfX,-NativeDeployPresentation.WorldHalfZ))<1e-3f,"the map's south-west corner");
            Check(NativeDeployModels.MapWidth/2*NativeDeployModels.MapWidth/2+NativeDeployModels.MapDepth/2*NativeDeployModels.MapDepth/2<1.44f*1.44f,"the map fits over the table's glass");
        }
        private static DeployInput Input(ExpeditionDeploymentState s,float si,float sm,float sd,float t,bool host=true,bool ready=true)
            =>new DeployInput{State=s,SinceInstanceRoll=si,SinceMutatorRoll=sm,SinceDeploy=sd,Time=t,Host=host,PartyReady=ready,Seats=4,Ready=new[]{true,true,ready,true,false,false,false,false},Instances=Pool,Mutators=Mutators};
        private static int Tile(in NativeDeployStage.Item it,int n)=>((int)Math.Round(it.Scroll*n-.5f)%n+n)%n;
        private static IEnumerable<NativeDeployStage.Item> Of(NativeDeployStage st,NativeDeployStage.Model m){for(int i=0;i<st.Count;i++)if(st.Items[i].Model==m)yield return st.Items[i];}
        private static void Stage()
        {
            var auth=new NativeExpeditionDeployment("preview","host",77);var st=new NativeDeployStage();
            st.Frame(Input(auth.Snapshot(),float.NaN,float.NaN,float.NaN,1),-1);
            Check(st.InstanceLook.Phase==NativeDeployPresentation.Phase.Dormant&&st.Shot==NativeDeployPresentation.Shot.Overview&&st.Count>40&&st.Count<=NativeDeployStage.Capacity,"the idle station");
            Check(Of(st,NativeDeployStage.Model.DrumBand).All(b=>Math.Abs(Frac(b.Scroll*3)-0f)<1e-3f||Math.Abs(Frac(b.Scroll*3)-1)<1e-3f),"dormant reels rest between symbols");
            Check(!Of(st,NativeDeployStage.Model.Quad).Any(q=>q.Texture==NativeDeployStage.TexStamp),"no stamps before a roll");
            Check(Of(st,NativeDeployStage.Model.Card).All(c=>Math.Abs(c.Scroll*(st.Strip(1,0).Length+1)-st.Strip(1,0).Length)<1e-3f),"the dials show the wild-card back before a roll");
            Check(st.Controls.Roll&&!st.Controls.Mutate&&!st.Controls.Skip&&!st.Controls.Deploy,"the host may only roll destinations first");
            for(int roll=0;roll<4;roll++)
            {
                Check(auth.RollInstances("host",Pool),"roll "+roll);var s=auth.Snapshot();
                // through the spin: the instance shot, no landing early; then each reel lands on its district
                for(float t=0;t<=NativeDeployPresentation.RollDuration+.5f;t+=1/30f)
                {
                    st.Frame(Input(s,t,float.NaN,float.NaN,t),1/30f);
                    if(t<NativeDeployPresentation.RollDuration-.01f)Check(st.Shot==NativeDeployPresentation.Shot.InstanceMachine&&!st.Controls.Roll,"spinning frames the instance machine and holds the buttons");
                    var lever=Of(st,NativeDeployStage.Model.Lever).Single();var expect=Matrix4x4.CreateRotationX(NativeDeployModels.LeverAngle(NativeDeployPresentation.Lever(t)))*Matrix4x4.CreateTranslation(NativeDeployModels.LeverPivot)*NativeDeployModels.Root(NativeDeployPresentation.InstanceMachineAt,NativeDeployPresentation.InstanceYaw);
                    Check(MatrixNear(lever.World,expect),"the lever follows Lever()");
                }
                var bands=Of(st,NativeDeployStage.Model.DrumBand).ToArray();Check(bands.Length==3,"three reels");
                for(int r=0;r<3;r++){var strip=st.Strip(0,r);Check(strip[Tile(bands[r],strip.Length)]==s.districts[r].instance&&st.Poses[r].Landed,"reel "+r+" lands on district "+r+" (roll "+roll+")");
                    var c=WeaponPaint.Linear(NativeDeployPresentation.DestinationOf(s.districts[r].instance,s.districts[r].theme).Color);var seg=Of(st,NativeDeployStage.Model.Instance).Single().Tint(NativeDeployModels.Approach+r);
                    Check(Math.Abs(seg.X/Math.Max(1e-4f,seg.Y)-c.X/Math.Max(1e-4f,c.Y))<.02f||c.Y<.01f,"approach segment "+r+" in its district's colour");}
            }
            Check(st.MutatorLook.Phase==NativeDeployPresentation.Phase.Optional&&st.Controls.Mutate&&!st.Controls.Skip,"mutators optional after the destinations");
            Check(auth.RollMutators("host",Mutators),"mutator roll");var m=auth.Snapshot();
            for(float t=0;t<=NativeDeployPresentation.RollDuration+.5f;t+=1/30f)
            {
                st.Frame(Input(m,9,t,float.NaN,t),1/30f);
                foreach(var q in Of(st,NativeDeployStage.Model.Quad))if(q.Texture==NativeDeployStage.TexStamp)Check(q.Scroll==0,"never ACTIVE before Deploy");
            }
            var dials=Of(st,NativeDeployStage.Model.Card).ToArray();var stamps=Of(st,NativeDeployStage.Model.Quad).Where(q=>q.Texture==NativeDeployStage.TexStamp).ToArray();
            for(int r=0;r<3;r++){var strip=st.Strip(1,r);int n=strip.Length+1;int tile=(int)Math.Round(dials[r].Scroll*n);Check(strip[tile]==m.mutators[r],"dial "+r+" lands on mutator "+r);}
            Check(stamps.Length==3&&stamps.All(q=>q.Scroll==0),"three PENDING stamps");
            Check(st.Controls.Skip&&st.Controls.Deploy,"the host may skip or deploy");
            st.Frame(Input(m,9,9,float.NaN,20,false),-1);Check(!st.Controls.Roll&&!st.Controls.Deploy&&!st.Controls.Skip&&!st.Controls.Mutate,"a guest has no buttons");
            int mapBefore=st.MapRevision;Check(mapBefore==m.revision,"the map follows the revision");
            Check(auth.Deploy("host",true),"deploy");var d=auth.Snapshot();
            float lastGate=-1;
            for(float t=0;t<=NativeDeployPresentation.TravelAt+.5f;t+=1/30f)
            {
                st.Frame(Input(d,9,9,t,30+t),1/30f);var left=Of(st,NativeDeployStage.Model.DoorLeft).Single();float gate=-left.World.M41/NativeDeployModels.DoorTravel;
                Check(gate>=lastGate-1e-5f&&Math.Abs(gate-NativeDeployPresentation.Deploying(t).Gate)<1e-4f,"the doors part with Deploying().Gate");lastGate=gate;
                Check(st.Shot==NativeDeployPresentation.Shot.Deploy&&!st.Controls.Roll&&st.MapRevision==mapBefore,"deploying frames the gate, locks the console and freezes the map");
                var ph=st.MutatorLook.Phase;var stamp=Of(st,NativeDeployStage.Model.Quad).Where(q=>q.Texture==NativeDeployStage.TexStamp).ToArray();Check(ph==NativeDeployPresentation.Phase.Active&&stamp.Length==3&&stamp.All(q=>q.Scroll==.5f),"ACTIVE after Deploy");
            }
            Check(st.Deploy.Travel&&st.PilotShown.Take(4).All(x=>x<.01f),"the party is lifted out before travel");
            Check(Vector3.Dot(st.PilotFacing[0],Vector3.UnitZ)>.9f,"the party turns to the gate");
            // the lights: eight practicals and the key, in the source frame
            Check(st.Lights.Points==8&&st.Lights.Directionals==1&&st.Lights.Point.All(p=>p.R+p.G+p.B>0),"eight practical lights and a key");
            // a skipped machine shows the backs and no stamps
            var auth2=new NativeExpeditionDeployment("preview","host",78);auth2.RollInstances("host",Pool);auth2.RollMutators("host",Mutators);auth2.SkipMutators("host");var sk=auth2.Snapshot();var st2=new NativeDeployStage();
            st2.Frame(Input(sk,9,9,float.NaN,40),-1);Check(st2.MutatorLook.Phase==NativeDeployPresentation.Phase.Skipped&&!Of(st2,NativeDeployStage.Model.Quad).Any(q=>q.Texture==NativeDeployStage.TexStamp),"skipped: no stamps");
            Check(Of(st2,NativeDeployStage.Model.Card).All(c=>Math.Abs(c.Scroll-st2.Strip(1,0).Length/(float)(st2.Strip(1,0).Length+1))<1e-3f),"skipped: the dials show the back");
            // a late joiner sees every result landed at once
            var st3=new NativeDeployStage();st3.Frame(Input(m,float.NaN,float.NaN,float.NaN,50),-1);var b3=Of(st3,NativeDeployStage.Model.DrumBand).ToArray();
            for(int r=0;r<3;r++)Check(st3.Strip(0,r)[Tile(b3[r],st3.Strip(0,r).Length)]==m.districts[r].instance&&st3.Poses[r].Landed&&st3.Poses[3+r].Landed,"a late joiner sees reel "+r+" landed");
            // the camera eases (no cut) toward the shot
            var st4=new NativeDeployStage();st4.Frame(Input(rolledFor(),float.NaN,float.NaN,float.NaN,0),-1);var from=st4.Camera.Eye;st4.Frame(Input(rolledFor(),.1f,float.NaN,float.NaN,.1f),1/60f);
            Check(Vector3.Distance(st4.Camera.Eye,from)>1e-3f&&Vector3.Distance(st4.Camera.Eye,from)<.5f,"the camera eases between shots");
            // the clock: the first state seen stays landed; later rolls and the deploy are timed
            var clock=new NativeDeployClock();clock.Observe(m,0);Check(!(clock.SinceInstanceRoll(1)>=0)&&!(clock.SinceMutatorRoll(1)>=0),"a late joiner's clock has no roll time");
            clock.Observe(d,2);Check(Math.Abs(clock.SinceDeploy(3)-1)<1e-5f,"the deploy is timed");
        }
        private static ExpeditionDeploymentState rolledFor(){var a=new NativeExpeditionDeployment("preview","host",79);a.RollInstances("host",Pool);return a.Snapshot();}
        private static float Frac(float x)=>x-(float)Math.Floor(x);
        private static bool MatrixNear(Matrix4x4 a,Matrix4x4 b){var d=a-b;float s=Math.Abs(d.M11)+Math.Abs(d.M12)+Math.Abs(d.M13)+Math.Abs(d.M21)+Math.Abs(d.M22)+Math.Abs(d.M23)+Math.Abs(d.M31)+Math.Abs(d.M32)+Math.Abs(d.M33)+Math.Abs(d.M41)+Math.Abs(d.M42)+Math.Abs(d.M43);return s<1e-3f;}
        // ---- per frame, nothing allocates ----
        private static void Allocation()
        {
            var auth=new NativeExpeditionDeployment("preview","host",80);auth.RollInstances("host",Pool);var rolled=auth.Snapshot();auth.RollMutators("host",Mutators);var mut=auth.Snapshot();auth.Deploy("host",true);var dep=auth.Snapshot();
            var st=new NativeDeployStage();var inputs=new[]{Input(rolled,.7f,float.NaN,float.NaN,1),Input(mut,9,1.2f,float.NaN,2),Input(dep,9,9,1.5f,3),Input(dep,9,9,3f,4)};
            for(int warm=0;warm<2;warm++)
            {
                long before=GC.GetAllocatedBytesForCurrentThread();
                for(int f=0;f<240;f++){var input=inputs[f/60];input.Time+=f/60f;input.SinceInstanceRoll+=f/600f;input.SinceMutatorRoll+=f/600f;input.SinceDeploy+=f/600f;st.Frame(input,1/60f);}
                long used=GC.GetAllocatedBytesForCurrentThread()-before;if(warm==1)Check(used==0,"the stage allocates nothing per frame ("+used+" bytes)");
            }
        }
        private static void Art()
        {
            int px=NativeDeployArt.TilePixels;
            foreach(var p in Pool)
            {
                var t=NativeDeployArt.DestinationTile(p.id,p.theme);Check(t.Length==px*px*4&&t.All(float.IsFinite),p.id+" tile size");Check(t.SequenceEqual(NativeDeployArt.DestinationTile(p.id,p.theme)),p.id+" tile is deterministic");
                var c=NativeDeployPresentation.DestinationOf(p.id,p.theme).Color;float r=((c>>16)&255)/255f,g=((c>>8)&255)/255f,b=(c&255)/255f;int hits=0;
                for(int i=0;i<t.Length;i+=4)if(Math.Abs(t[i]-r)+Math.Abs(t[i+1]-g)+Math.Abs(t[i+2]-b)<.06f)hits++;Check(hits>px*px/60,p.id+" emblem in its colour ("+hits+")");
            }
            foreach(var id in Mutators.Concat(new[]{"unknown-thing"})){var t=NativeDeployArt.MutatorTile(id);Check(t.Length==px*px*4&&t.Where((v,i)=>i%4==3).All(a=>a>.99f),id+" card is opaque");}
            var st=NativeDeployArt.StampTile(false);Check(st.Length==px*NativeDeployArt.StampRows*4&&st.Where((v,i)=>i%4==3).Any(a=>a>.9f)&&st.Where((v,i)=>i%4==3).Any(a=>a<.05f),"the stamp has ink and clear paper");
            var glow=NativeDeployArt.GlowTile();int gp=NativeDeployArt.GlowPixels;Check(glow[3]<.01f&&glow[((gp/2)*gp+gp/2)*4+3]>.9f,"the glow falls off to its edge");
            var strip=NativeDeployArt.Stack(new[]{NativeDeployArt.BackTile(),NativeDeployArt.BackTile()});Check(strip.Length==2*px*px*4,"strips stack tiles");
            // every word the station letters exists in the stroke font
            var words=new List<string>{"DESTINATIONS","DESTINATION","WILD CARD","SKIP","OPTIONAL","DEPLOY","DEPLOYMENT STATION","SECTOR SCAN","ROUTE PLAN","PENDING","ACTIVE","BOON","BANE","CHAOS","UNKNOWN"};
            words.AddRange(NativeDeployModels.EntranceLabels);foreach(var p in Pool)words.Add(NativeDeployPresentation.DestinationOf(p.id,p.theme).Name);foreach(var id in Mutators)words.Add(NativeDeployPresentation.MutatorOf(id).Name);
            foreach(var w in words)Check(w.All(NativeDeployArt.Lettered),"the stroke font letters "+w);
            Check(NativeDeployArt.Lines("CENTRAL STATION").Length==2&&NativeDeployArt.Lines("WARD ZERO").Length==1,"long names split in two lines");
        }
        // ---- the runtime wiring (text scan: Unity is not loaded here) ----
        private static void Wiring(string root)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,f));const string Rt="Assets/ChipArena/Runtime/";
            string scene=R(Rt+"NativeDeployScene.cs"),cast=R(Rt+"NativeDeployCast.cs"),console=R(Rt+"NativeDeployConsoleUI.cs"),preview=R(Rt+"NativeDeployPreview.cs"),game=R(Rt+"ConversionGame.cs"),stage=R("Assets/ChipArena/Core/NativeDeployStage.cs");
            Check(stage.Contains("NativeDeployPresentation.Reel(r,f.SinceInstanceRoll,from[r],land[r],n)")&&stage.Contains("NativeDeployPresentation.Reel(r,f.SinceMutatorRoll,from[3+r],land[3+r],n)"),"the reels and dials are posed by Reel()");
            Check(stage.Contains("NativeDeployPresentation.Lever(f.SinceInstanceRoll)")&&stage.Contains("NativeDeployPresentation.Deploying(")&&stage.Contains("NativeDeployPresentation.Framing(")&&stage.Contains("NativeDeployPresentation.ShotOf("),"the lever, the deploy sequence and the shots come from the rules");
            Check(scene.Contains("Stage.Frame(input,dt)")&&scene.Contains("NativeDeployPresentation.Preview(copy,")&&scene.Contains("NativeLighting.Entrance(Stage.Lights,camera)")&&scene.Contains("Resources.Load<Shader>(\"NativeDeployScreen\")"),"the scene draws the stage, the map from Preview and the stage's lights");
            Check(scene.Contains("Stage.MapRevision!=mapAsked"),"the map regenerates only on a new revision");
            var calls=new Regex(@"\.(RollInstances|RollMutators|SkipMutators|Deploy)\(");
            foreach(var (name,text) in new[]{("NativeDeployScene",scene),("NativeDeployCast",cast),("NativeDeployConsoleUI",console),("NativeDeployStage",stage)})Check(!calls.IsMatch(text)&&!text.Contains("new NativeExpeditionDeployment("),name+" never asks the authority");
            Check(calls.Matches(preview).Count==4&&preview.Contains("new NativeExpeditionDeployment(\"preview\",deployHost,"),"only the DEV preview asks its own local authority");
            foreach(var f in Directory.GetFiles(Path.Combine(root,"Assets/ChipArena/Runtime"),"*.cs"))if(!f.EndsWith("NativeDeployPreview.cs"))Check(!calls.IsMatch(File.ReadAllText(f))||!File.ReadAllText(f).Contains("NativeExpeditionDeployment"),Path.GetFileName(f)+" does not drive a deployment authority");
            Check(preview.Contains("\"--ca-deploy-preview\"")&&preview.Contains("private bool DeployPreviewShown=>DeployPreviewRequested&&")&&!Regex.IsMatch(preview,@"SendServer|SavePrefs|Save\(|ExpeditionStart|StartExpedition|HostRoom|JoinRoom"),"the preview is behind its flag and touches no room, network or save");
            Check(game.Contains("if(DeployPreviewGui(width,height))return;")&&game.Contains("if(DeployPreviewBackdrop())return;"),"the preview's hooks in the menu frame");
            Check(console.Contains("public static DeployIntent Draw(")&&preview.Contains("DeployPreviewIntent(intent)"),"the console raises intents; the preview answers them");
            // the draw path allocates nothing: Draw's body has no reference allocation, LINQ or lambda
            int a0=scene.IndexOf("public void Draw(Camera camera",StringComparison.Ordinal),a1=scene.IndexOf("public void Dispose()",StringComparison.Ordinal);var draw=scene.Substring(a0,a1-a0);
            Check(!Regex.IsMatch(draw,@"new (?!Vector[234]\(|Matrix4x4\()[A-Z]")&&!draw.Contains("=>")&&!Regex.IsMatch(draw,@"\.(Select|Where|ToArray|ToList)\("),"the scene's draw allocates nothing");
            Check(cast.Contains("cast.Weapon[i]=\"weapon-\"+spec")&&!Regex.IsMatch(cast.Substring(cast.IndexOf("private void DeployCastFrame",StringComparison.Ordinal)),@"""weapon-""\+look"),"the cast caches its weapon ids");
            Check(R(Rt+"NativeVisualBodyPresentation.cs").Contains("if(DeployOutfit(id,out var deploy))return deploy;")&&R(Rt+"NativeWeaponSkinPresentation.cs").Contains("if(DeploySkins(id,out var deploy))return deploy;"),"the deploy pilots wear their seats' outfits and skins");
            // the shader and every new file are registered with Unity
            var shader=Path.Combine(root,"Assets/ChipArena/Resources/NativeDeployScreen.shader");Check(File.Exists(shader)&&File.Exists(shader+".meta")&&File.ReadAllText(shader).Contains("Shader \"ChipArena/NativeDeployScreen\""),"the screen shader and its meta");
            // HLSL reserves these words (geometry-shader primitives, the sample modifier): a variable named after one fails to compile
            var hlsl=File.Exists(shader)?File.ReadAllText(shader):"";
            foreach(var word in new[]{"line","lineadj","point","triangle","triangleadj","sample"})Check(!System.Text.RegularExpressions.Regex.IsMatch(hlsl,@"\b(float|float2|float3|float4|half|int|bool)\s+"+word+@"\b"),"the screen shader names no variable `"+word+"`");
            var guids=new HashSet<string>();
            foreach(var f in new[]{"Core/NativeDeployModels.cs","Core/NativeDeployArt.cs","Core/NativeDeployStage.cs","Runtime/NativeDeployScene.cs","Runtime/NativeDeployCast.cs","Runtime/NativeDeployConsoleUI.cs","Runtime/NativeDeployPreview.cs","Resources/NativeDeployScreen.shader"})
            {var meta=Path.Combine(root,"Assets/ChipArena",f+".meta");Check(File.Exists(meta),f+" has a meta");var g=Regex.Match(File.ReadAllText(meta),@"guid: ([0-9a-f]{32})").Groups[1].Value;Check(g.Length==32&&guids.Add(g),f+" has its own guid");}
        }
    }
}
#endif
