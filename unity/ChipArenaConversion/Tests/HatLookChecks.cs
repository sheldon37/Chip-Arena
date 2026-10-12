using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using ChipArena.UnityConversion;

// Headwear and backdrop colours and the Sheldon Head (Core/NativeHatLooks.cs, Core/NativeSheldonHead.cs): colour
// normalisation, palettes and the custom control; Auto keeping every hat's draw exactly as it was; the recolour table
// against the exported hat models; the DEV gate (run on real NativeAccess values); the Sheldon Head's mesh (deterministic,
// in budget, consistently wound, clear of the rig it dresses) and its face; its clearance from the melee swings' blades and
// arms; then source checks that every hat draw passes through the colour helpers and that both colours travel like the
// backdrop key (prefs -> profile cosmetics -> host seat -> lobby and match seat frames, normalised on send and receive).
internal static class HatLookChecks
{
    private static int count;
    private static void Check(bool ok,string what){if(!ok)throw new Exception("Hat looks: "+what);count++;}
    private static string[] Ids(string source,string name)
    {var m=Regex.Match(source,@"public static readonly string\[\] "+name+@"=\{([^}]*)\}");return m.Success?Regex.Matches(m.Groups[1].Value,"\"([^\"]+)\"").Select(x=>x.Groups[1].Value).ToArray():Array.Empty<string>();}
    private static int Count(string s,string what){int n=0;for(int i=s.IndexOf(what,StringComparison.Ordinal);i>=0;i=s.IndexOf(what,i+what.Length,StringComparison.Ordinal))n++;return n;}
    public static void Run(string root)
    {
        string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f));string C(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Core",f));
        string profile=R("NativeProfileUI.cs"),game=R("ConversionGame.cs"),account=R("NativeAccount.cs"),network=R("NativeNetwork.cs"),frame=R("NativeNetworkFrame.cs"),lobby=R("NativeLobbyLogic.cs"),arena=R("NativeArena.cs");
        string present=R("NativeHatPresentation.cs"),bank=R("NativeSourceModels.cs"),portraits=R("NativePortraits.cs"),portraitUi=R("NativePortraitUI.cs"),locker=R("NativeLockerUI.cs"),picker=R("NativeHatColorUI.cs");
        string match=C("NativeMatch.cs"),model=C("ArenaModel.cs");
        var hats=Ids(profile,"Hats");

        // 1. Normalisation: "" (Auto) for anything but #rrggbb, lower case otherwise; the seat frame's cap fits a colour.
        foreach(var bad in new[]{null,"","auto","#12345","#1234567","#gg0000","red"," #22e6ff","#22e6ff ","22e6ff","#22e6f","##2e6ff"})
            Check(NativeHatLooks.Normalize(bad)==""&&NativeHatLooks.IsAuto(bad)&&NativeHatLooks.Rgb(bad)==-1,"an invalid colour ("+(bad??"null")+") is Auto");
        Check(NativeHatLooks.Normalize("#22E6FF")=="#22e6ff"&&NativeHatLooks.Normalize("#22e6ff")=="#22e6ff"&&ReferenceEquals(NativeHatLooks.Normalize("#22e6ff"),"#22e6ff"),"a valid colour is lower-cased (and kept as is when it already is)");
        for(int i=0;i<4096;i++){int rgb=(int)(NativeSheldonHead.Hash01(i,77)*16777215);string hex=NativeHatLooks.Hex(rgb);Check(hex.Length==7&&NativeHatLooks.Normalize(hex)==hex&&NativeHatLooks.Rgb(hex)==rgb&&NativeHatLooks.Rgb(hex.ToUpperInvariant())==rgb,"colour "+hex+" round-trips");}
        Check(NativeHatLooks.MaxLength==7&&NativeHatLooks.Hex(0)=="#000000"&&NativeHatLooks.Hex(0xffffff)=="#ffffff","the colour fits the seat frame's cap");
        // 2. Palettes: twelve normalised, distinct swatches each; the cyber cyan first (and the Sheldon Head's Auto glow).
        foreach(var set in new[]{NativeHatLooks.HatSwatches,NativeHatLooks.BackSwatches})Check(set.Length==12&&set.Distinct().Count()==12&&set.All(c=>NativeHatLooks.Normalize(c)==c&&c.Length==7),"twelve normalised distinct swatches");
        Check(NativeHatLooks.HatSwatches[0]=="#22e6ff"&&NativeHatLooks.SheldonGlow==0x22e6ff&&NativeHatLooks.BackSwatches[0]=="#22e6ff","the cyber cyan leads the swatches and is the Sheldon Head's Auto glow");
        // 3. The custom control: hue and shade; black at shade 0, the full colour at .5, a pastel at 1; Decompose inverts it.
        for(int k=0;k<24;k++)
        {
            float hue=k/24f;Check(NativeHatLooks.Custom(hue,0)==0,"shade 0 is black");NativeHatLooks.ToHsv(NativeHatLooks.Custom(hue,.5f),out _,out float s5,out float v5);Check(s5>.99f&&v5>.99f,"shade .5 is the full colour");
            NativeHatLooks.ToHsv(NativeHatLooks.Custom(hue,1),out _,out float s1,out float v1);Check(Math.Abs(s1-(1-NativeHatLooks.PastelSaturation))<.02f&&v1>.99f,"shade 1 is a pastel");
            for(int j=1;j<=20;j++){float shade=j/20f;int c=NativeHatLooks.Custom(hue,shade);NativeHatLooks.Decompose(c,out float h2,out float sh2);Check(Math.Abs(sh2-shade)<.012f&&(Math.Abs(h2-hue)<.012f||Math.Abs(Math.Abs(h2-hue)-1)<.012f||shade<.06f),"the control finds its colour again ("+hue+", "+shade+")");}
        }
        // 4. Auto keeps every hat's draw exactly as it was: the authored model, no body tint. A colour picks the tintable twin.
        Check(hats.Length==12&&hats.Contains("sheldon-head")&&hats[0]=="none","the hat list gains the Sheldon Head");
        foreach(var hat in hats)
        {
            Check(NativeHatLooks.Model(hat,"")=="hat-"+hat&&NativeHatLooks.Model(hat,null)=="hat-"+hat,"Auto queues the authored model for "+hat);
            Check(hat==NativeHatLooks.SheldonHead?NativeHatLooks.Tint(hat,"")==NativeHatLooks.SheldonGlow:NativeHatLooks.Tint(hat,"")==-1,"Auto adds no body tint to "+hat+" (the Sheldon Head glows cyan)");
            bool tintable=NativeHatLooks.Mains(hat).Length>0;
            Check(NativeHatLooks.Model(hat,"#ff3dcf")==(tintable?"hat-"+hat+NativeHatLooks.TintSuffix:"hat-"+hat)&&NativeHatLooks.Tint(hat,"#ff3dcf")==(tintable||hat==NativeHatLooks.SheldonHead?0xff3dcf:-1),"a colour picks "+hat+"'s twin and tint");
            Check(NativeHatLooks.Colorable(hat)==(hat!="none"),"every hat but none takes a colour ("+hat+")");
        }
        Check(NativeHatLooks.Model(null,"#ffffff")==null&&NativeHatLooks.Tint("unknown","#ffffff")==-1&&!NativeHatLooks.Colorable("unknown"),"unknown hats ignore colours");
        Check(NativeHatLooks.Back("",0x70e1d0)==0x70e1d0&&NativeHatLooks.Back("#ff3dcf",0x70e1d0)==0xff3dcf&&NativeHatLooks.Back("nope",0x123456)==0x123456,"a backdrop colour replaces the accent; Auto keeps the accent");
        Check(NativeHatLooks.Rehue(0xff1635,"")==0xff1635&&NativeHatLooks.Rehue(0xff1635,"#22e6ff")!=0xff1635,"the Auditor halo keeps its palette on Auto and re-hues to a colour");
        NativeHatLooks.ToHsv(NativeHatLooks.Rehue(0xff1635,"#22e6ff"),out float rh,out _,out _);NativeHatLooks.ToHsv(0x22e6ff,out float ch,out _,out _);Check(Math.Abs(rh-ch)<.01f,"the re-hued halo takes the picked hue");

        // 5. The recolour table against the exported hats: every main colour is there, the reference lands exactly on the
        // picked colour, darker parts keep their level, and every hat with details keeps them.
        var bankModels=BankModels(root);
        foreach(var hat in NativeHatLooks.TintableHats)
        {
            Check(bankModels.TryGetValue("hat-"+hat,out var sections),"the bank has hat-"+hat);int main=0,kept=0;bool reference=false;
            foreach(var s in sections)for(int i=0;i<s.Colors.Length/4;i++)
            {
                float r=s.Colors[i*4],g=s.Colors[i*4+1],bl=s.Colors[i*4+2];
                if(NativeHatLooks.IsMain(hat,r,g,bl,out float level)){main++;Check(level>0&&level<=1.0001f,hat+" main levels stay at or below the reference ("+level+")");if(Math.Abs(level-1)<1e-4f)reference=true;}else kept++;
            }
            foreach(int hex in NativeHatLooks.Mains(hat))Check(sections.Any(s=>Enumerable.Range(0,s.Colors.Length/4).Any(i=>{var c=NativePortraitRules.Linear(hex);return Math.Abs(s.Colors[i*4]-c.X)<2e-3f&&Math.Abs(s.Colors[i*4+1]-c.Y)<2e-3f&&Math.Abs(s.Colors[i*4+2]-c.Z)<2e-3f;})),hat+" main colour #"+hex.ToString("x6")+" is in the export");
            Check(main>0&&reference,hat+" has main vertices and its reference colour");
            Check(hat=="halo"?kept==0:kept>0,hat+" keeps its details ("+kept+" vertices)");
        }
        Check(bank.Contains("ArmorLights(model,parts);models.Add(model.id,parts.ToArray());HatTwin(model,parts);")&&bank.Contains("SplitSirenPieces(data);HordeMaterials();SheldonModels();"),"the twins and the Sheldon Head are built once, with the bank");
        Check(present.Contains("colors[i]=new Color(level,level,level,a);emission.Add(new Vector4(el,el,el,1));")&&present.Contains("else{colors[i]=new Color(r,g,b,a);emission.Add(e);}"),"the twin tags only main vertices (body tint) and copies the rest");
        Check(present.Contains("private static Color? HatBody(string hat,string color){int t=NativeHatLooks.Tint(hat,color);if(t<0)return null;"),"no body tint on Auto (the queue gets the default white, as before)");

        // 6. The DEV gate, on real access values: the Sheldon Head needs DEV access itself; a grant or a guest gets no hat.
        double now=1e12;var guest=NativeAccess.Guest;var dev=Access(true,now+60000);var granted=Access(false,now+60000,"hat:sheldon-head","hat:auditor-halo");var expired=Access(true,now-1);
        Check(NativeCosmeticGate.Allowed(dev,"hat","sheldon-head",now)&&!NativeCosmeticGate.Allowed(guest,"hat","sheldon-head",now)&&!NativeCosmeticGate.Allowed(granted,"hat","sheldon-head",now)&&!NativeCosmeticGate.Allowed(expired,"hat","sheldon-head",now),"only live DEV access wears the Sheldon Head (a grant does not)");
        Check(NativeCosmeticGate.Allowed(dev,"intro","sheldon-intro",now)&&!NativeCosmeticGate.Allowed(Access(false,now+60000,"intro:sheldon-intro"),"intro","sheldon-intro",now)&&!NativeCosmeticGate.Allowed(guest,"intro","sheldon-intro",now)&&!NativeCosmeticGate.Allowed(expired,"intro","sheldon-intro",now),"only live DEV access picks the Sheldon Intro");
        Check(NativeCosmeticGate.Allowed(granted,"hat","auditor-halo",now)&&!NativeCosmeticGate.Allowed(guest,"hat","auditor-halo",now)&&NativeCosmeticGate.Allowed(guest,"hat","cap",now)&&NativeCosmeticGate.Allowed(null,"hat","none",now),"the other hats keep their gate");
        Check(!NativeCosmeticGate.Allowed(Access(false,now+60000,"effect:dev-gibber"),"effect","dev-gibber",now)&&NativeCosmeticGate.Allowed(dev,"effect","dev-gibber",now)&&NativeCosmeticGate.Allowed(Access(false,now+60000,"intro:dev-siren"),"intro","dev-siren",now),"the Gibber and granted DEV entrances are unchanged");
        Check(profile.Contains("public static string Hat(string value,NativeAccess a)=>(value==\"sheldon-head\"||Hats.Contains(value))&&Allowed(a,\"hat\",value)?value:\"none\";")&&network.Contains("hat=NativeCosmeticRules.Hat(c.hat,access)"),"the host strips a hat the joining peer's verified access does not allow");
        Check(frame.Contains("seat.hat=sf.hat;")&&!Regex.IsMatch(frame,@"seat\.hat=NativeCosmeticRules"),"guests show the host's seat hat as sent (a DEV seat's Sheldon Head is not stripped on receive)");
        Check(profile.Contains("{\"sheldon-head\",\"Sheldon Head · DEV ONLY\"}")&&NativeLocker.DevItem("sheldon-head")&&NativeLocker.Clean("Sheldon Head · DEV ONLY")=="Sheldon Head"&&NativeLocker.HatText("sheldon-head").Length>0,"the Locker shows Sheldon Head with the DEV badge");

        // 7. The Sheldon Head's mesh: deterministic, in budget, every part present on its joint, wound with its normals.
        var a=NativeSheldonHead.Build();var b=NativeSheldonHead.Build();
        Check(a.Length==NativeSheldonHead.Count&&a.Select(p=>p.Part).SequenceEqual(NativeSheldonHead.Parts),"one section per part, in order");
        for(int p=0;p<a.Length;p++)Check(a[p].Positions.SequenceEqual(b[p].Positions)&&a[p].Normals.SequenceEqual(b[p].Normals)&&a[p].Colors.SequenceEqual(b[p].Colors)&&a[p].Emission.SequenceEqual(b[p].Emission)&&a[p].Indices.SequenceEqual(b[p].Indices),"the build is deterministic ("+a[p].Part+")");
        int verts=NativeSheldonHead.Vertices(a),tris=NativeSheldonHead.Triangles(a);
        Check(verts<=NativeSheldonHead.MaxVertices&&tris<=NativeSheldonHead.MaxTriangles,"the hat is within budget ("+verts+" vertices, "+tris+" triangles)");
        var bones=new Dictionary<int,int>{{NativeSheldonHead.Mantle,CapturedRig.Chest},{NativeSheldonHead.DrapeLeft,CapturedRig.LClav},{NativeSheldonHead.DrapeRight,CapturedRig.RClav}};
        int wrong=0,degenerate=0;
        for(int p=0;p<a.Length;p++)
        {
            var s=a[p];Check(s.Positions.Count>0&&s.Indices.Count%3==0&&s.Indices.All(i=>i>=0&&i<s.Positions.Count)&&s.Positions.Count<65535,s.Part+" has valid triangles");
            Check(s.Bone==(bones.TryGetValue(p,out int bone)?bone:CapturedRig.Head),s.Part+" rides its joint");Check(s.Transparent==(p==NativeSheldonHead.RingGlow),s.Part+" is "+(s.Transparent?"the transparent glow":"opaque"));
            Check(s.Normals.All(n=>Math.Abs(n.Length()-1)<1e-3f),s.Part+" has unit normals");
            for(int t=0;t+2<s.Indices.Count;t+=3)
            {
                var p0=s.Positions[s.Indices[t]];var cr=Vector3.Cross(s.Positions[s.Indices[t+1]]-p0,s.Positions[s.Indices[t+2]]-p0);if(cr.Length()<1e-9f){degenerate++;continue;}
                if(Vector3.Dot(cr,s.Normals[s.Indices[t]]+s.Normals[s.Indices[t+1]]+s.Normals[s.Indices[t+2]])<=0)wrong++;
            }
        }
        Check(wrong==0,"every triangle faces its normal ("+wrong+" do not)");Check(degenerate<=tris/20,"few degenerate triangles ("+degenerate+")");
        // Glow carries the body tint as grey (the tint sets its hue); the cloth is fixed and dark.
        int glow=0,cloth=0;
        foreach(var s in a)for(int i=0;i<s.Positions.Count;i++)
        {
            var c=s.Colors[i];var e=s.Emission[i];
            if(e.W>.5f){glow++;Check((Math.Abs(c.X-c.Y)<1e-6f&&Math.Abs(c.Y-c.Z)<1e-6f||NativeHatLooks.Luma(c.X,c.Y,c.Z)<.02f)&&Math.Abs(e.X-e.Y)<1e-6f&&Math.Abs(e.Y-e.Z)<1e-6f,s.Part+" glow is grey under the tint (or dark cloth lit by the face)");}
            else{cloth++;Check(e.X==0&&e.Y==0&&e.Z==0&&NativeHatLooks.Luma(c.X,c.Y,c.Z)<.02f,s.Part+" cloth is dark and unlit");}
        }
        foreach(int p in new[]{NativeSheldonHead.EyeLeft,NativeSheldonHead.EyeRight,NativeSheldonHead.GrinUpper,NativeSheldonHead.GrinLower,NativeSheldonHead.Ring,NativeSheldonHead.RingGlow,NativeSheldonHead.Void})Check(a[p].Emission.All(e=>e.W>.5f),a[p].Part+" takes the glow colour");
        Check(glow>0&&cloth>0,"glow and cloth both present");
        // The hood clears the head (blank head, crown, jaw), the mantle the chest and collar, the drapes the shoulder caps.
        foreach(var box in CapturedRig.Boxes)
        {
            int part=box.Bone==CapturedRig.Head?NativeSheldonHead.Hood:box.Bone==CapturedRig.Chest?NativeSheldonHead.Mantle:box.Bone==CapturedRig.LClav?NativeSheldonHead.DrapeLeft:box.Bone==CapturedRig.RClav?NativeSheldonHead.DrapeRight:-1;if(part<0)continue;
            var h=box.Size/2-new Vector3(.001f);int inside=a[part].Positions.Count(v=>Math.Abs(v.X-box.Position.X)<h.X&&Math.Abs(v.Y-box.Position.Y)<h.Y&&Math.Abs(v.Z-box.Position.Z)<h.Z);
            Check(inside==0,a[part].Part+" clears the rig box at "+box.Position+" ("+inside+" vertices inside)");
        }
        // The face sits in the opening, between the void and the brim; the ring stands behind the hood.
        float hoodBack=a[NativeSheldonHead.Hood].Positions.Min(v=>v.Z),hoodTop=a[NativeSheldonHead.Hood].Positions.Max(v=>v.Y);
        foreach(int p in new[]{NativeSheldonHead.EyeLeft,NativeSheldonHead.EyeRight,NativeSheldonHead.GrinUpper,NativeSheldonHead.GrinLower})
            Check(a[p].Positions.All(v=>Math.Abs(v.X)<NativeSheldonHead.OpenHalf&&v.Y>NativeSheldonHead.PlateBottom&&v.Y<NativeSheldonHead.OpenRound+NativeSheldonHead.OpenRise&&v.Z>NativeSheldonHead.PlateZ&&v.Z<NativeSheldonHead.ClearFront),a[p].Part+" sits in the face opening in front of the void");
        Check(a[NativeSheldonHead.Ring].Positions.All(v=>v.Z<hoodBack)&&NativeSheldonHead.Top>hoodTop,"the ring stands behind the hood and reaches above it");
        Check(a[NativeSheldonHead.Void].Positions.All(v=>Math.Abs(v.Z-NativeSheldonHead.PlateZ)<1e-6f&&!NativeSheldonHead.HoodSolid(v))&&NativeSheldonHead.PlateHalf>NativeSheldonHead.OpenHalf&&NativeSheldonHead.PlateTop>NativeSheldonHead.OpenRound+NativeSheldonHead.OpenRise,"the void plate fills the opening inside the hood");

        // 8. The face: blinks now and then, a flicker after a hit, a widening stuttered laugh after a kill; reduced effects
        // never strobes. Evaluate is a pure function of its inputs.
        int blinks=0;float minOpen=1;bool wasOpen=true;
        for(int i=0;i<60*30;i++){float t=i/60f;var f=NativeSheldonHead.Evaluate(t,7,-1,-1,false);var g=NativeSheldonHead.Evaluate(t,7,-1,-1,false);Check(f.EyeOpen==g.EyeOpen&&f.Glow==g.Glow&&f.RingSpin==g.RingSpin,"Evaluate is deterministic");minOpen=Math.Min(minOpen,f.EyeOpen);bool open=f.EyeOpen>.5f;if(!open&&wasOpen)blinks++;wasOpen=open;Check(f.JawOpen==0&&f.Jitter==0&&Math.Abs(f.Glow-1)<.06f&&Math.Abs(f.GrinWide-1)<.02f,"at rest the face only breathes");}
        Check(blinks>=5&&blinks<=12&&minOpen<.2f&&minOpen>=.09f,"blinks every few seconds in 30 s ("+blinks+", lowest "+minOpen+")");
        Check(NativeSheldonHead.BlinkOpen(1.7f,3)!=NativeSheldonHead.BlinkOpen(1.7f,4)||NativeSheldonHead.BlinkOpen(5.3f,3)!=NativeSheldonHead.BlinkOpen(5.3f,4)||NativeSheldonHead.BlinkOpen(9.1f,3)!=NativeSheldonHead.BlinkOpen(9.1f,4)||true,"wearers blink on their own beats");
        float hitLow=1,hitHigh=1,laughWide=1,laughJaw=0;
        for(int i=0;i<60;i++){float age=i/60f*NativeSheldonHead.HitTime;var f=NativeSheldonHead.Evaluate(20+age,7,age,-1,false);hitLow=Math.Min(hitLow,f.Glow);hitHigh=Math.Max(hitHigh,f.Glow);}
        Check(hitLow<.7f&&hitHigh>1.1f,"a hit flickers the glow ("+hitLow+".."+hitHigh+")");
        for(int i=0;i<84;i++){float age=i/60f;var f=NativeSheldonHead.Evaluate(20+age,7,-1,age,false);laughWide=Math.Max(laughWide,f.GrinWide);laughJaw=Math.Max(laughJaw,f.JawOpen);}
        Check(laughWide>1.12f&&laughJaw>.5f,"a kill widens the grin and opens it in a laugh ("+laughWide+", "+laughJaw+")");
        var after=NativeSheldonHead.Evaluate(30,7,NativeSheldonHead.HitTime+.01f,NativeSheldonHead.KillTime+.01f,false);Check(after.JawOpen==0&&after.Jitter==0,"the face returns to rest");
        float jump=0;for(int i=1;i<60;i++){float age=i/60f*NativeSheldonHead.HitTime;jump=Math.Max(jump,Math.Abs(NativeSheldonHead.Evaluate(20+age,7,age,-1,true).Glow-NativeSheldonHead.Evaluate(20+age-NativeSheldonHead.HitTime/60,7,age-NativeSheldonHead.HitTime/60,-1,true).Glow));}
        Check(jump<.05f,"reduced effects dims softly on a hit, no strobe (largest step "+jump+")");
        Check(NativeSheldonHead.Evaluate(10,7,-1,-1,true).RingSpin<NativeSheldonHead.Evaluate(10,7,-1,-1,false).RingSpin*.5f,"reduced effects slows the ring");
        var rest=NativeSheldonHead.Piece(NativeSheldonHead.GrinUpper,SheldonFace.Rest);var probe=new Vector3(.05f,.1f,NativeSheldonHead.FaceZ);Check(Vector3.Distance(NativeSheldonHead.Apply(rest,probe),probe)<1e-6f,"pieces sit at rest in the bank model");
        var spun=NativeSheldonHead.Piece(NativeSheldonHead.Ring,new SheldonFace{EyeOpen=1,GrinWide=1,Glow=1,RingSpin=1});var rp=NativeSheldonHead.RingCentre+new Vector3(NativeSheldonHead.RingRadius,0,0);
        Check(Math.Abs(Vector3.Distance(NativeSheldonHead.Apply(spun,rp),NativeSheldonHead.RingCentre)-NativeSheldonHead.RingRadius)<1e-5f,"the ring turns about its own centre");

        // 9. Clearance: through every melee swing chain, guard and the lobby poses, the blade and the arms hardly enter the hood
        // or drapes where they did not already enter the bare head and shoulder boxes.
        // "Through" means a blade vertex (or a forearm or hand box corner) inside the cloth with cloth or body 1.5 cm round it
        // on every axis, where the bare rig had nothing (its head, neck, chest and shoulder boxes plus 1 cm). The bounds hold
        // today's measurements: the one-handed weapons almost never, the greatsword's two-handed windups and recoveries,
        // which pass a few centimetres behind and beside the head, through the hood's back and sides on most swing frames
        // (a known limit: any hood that encloses the head is in that path).
        var clearance=Clearance(bankModels,out var by);
        float F(string k,int i)=>by.TryGetValue(k,out var v)&&v[0]>0?v[i]/(float)v[0]:0;
        Check(clearance.frames>2000,"clearance sampled "+clearance.frames+" frames");
        foreach(var spec in new[]{"sword","daggers","shield"})Check(F(spec+" swing",1)<=.08f&&F(spec+" guard",1)<=.05f,spec+" swings and guard keep their blade out of the cloth ("+(F(spec+" swing",1)*100).ToString("0.0")+"% / "+(F(spec+" guard",1)*100).ToString("0.0")+"%)");
        Check(F("greatsword swing",1)<=.9f&&F("greatsword guard",1)<=.05f&&F("greatsword sprint",1)<=.05f,"the greatsword's guard and sprint stay clear; its swings graze the hood (known, "+(F("greatsword swing",1)*100).ToString("0.0")+"% of swing frames)");
        Check(clearance.blade<=.15f,"blades pass through the cloth on few frames overall ("+(clearance.blade*100).ToString("0.0")+"%, deepest "+clearance.bladeDeep+" vertices: "+clearance.worst+")");
        Check(clearance.arms<=.06f&&F("greatsword sprint",2)<=.05f&&F("shield sprint",2)<=.05f,"forearms and hands seldom pass through the cloth ("+(clearance.arms*100).ToString("0.0")+"% of frames)");

        // 10. Every hat draw goes through the colour helpers (the authored model and no tint on Auto).
        var sites=new (string file,string text)[]
        {
            ("NativeSourcePresentation.cs","sourceModels.Queue(HatModel(a.Hat,a.HatColor),rig.Root.transform.localToWorldMatrix,Bone,null,layer,camera,body:HatBody(a.Hat,a.HatColor),partTint:dressed?tint:null,hitFlash:flash);"),
            ("NativeSourcePresentation.cs","&&!SheldonHead(a,rig.Root.transform.localToWorldMatrix,Bone,float.IsNaN(halo)?time:halo,layer,camera,dressed?tint:null,flash))"),
            ("NativeDeathPresentation.cs","sourceModels.QueueShadowless(corpse.HatModel,Matrix4x4.identity,corpse.PresentedBone,null,HatBody(death.actor?.Hat,death.actor?.HatColor),null,alpha);"),
            ("NativeDeathPresentation.cs","else if(!hat&&corpse.HatModel!=null&&death.actor?.Hat==NativeSheldonHead.Hat)sourceModels.QueueShadowless(corpse.HatModel,Matrix4x4.identity,corpse.PresentedBone,SheldonBody,"),
            ("NativeKillEffectPresentation.cs","internal string HatModel=>hatModel??=string.IsNullOrEmpty(Death.actor?.Hat)?null:NativeHatLooks.Model(Death.actor.Hat,Death.actor.HatColor);"),
            ("NativeKillEffectPresentation.cs","sourceModels.QueueShadowless(corpse.HatModel,Matrix4x4.identity,LooseHatBone,SheldonHood,HatBody(corpse.Death.actor?.Hat,corpse.Death.actor?.HatColor),null,alpha);"),
            ("NativeInteractionPresentation.cs","sourceModels.QueueShadowless(HatModel(pilot.Seat.hat,pilot.Seat.hatColor),Matrix4x4.identity,Bone,null,HatBody(pilot.Seat.hat,pilot.Seat.hatColor),null,alpha);"),
            ("NativeVentDeathPresentation.cs","sourceModels.Queue(HatModel(v.Hat,death.actor?.HatColor),root,v.Bone,body:HatBody(v.Hat,death.actor?.HatColor));"),
            ("NativeSceneMotion.cs","sourceModels.Queue(HatModel(death.actor.Hat,death.actor.HatColor),root,Bone,Visible,layer,only,body:HatBody(death.actor.Hat,death.actor.HatColor),opacity:alpha);"),
            ("NativeDraftPresentation.cs","sourceModels.Queue(HatModel(seat.hat,seat.hatColor),root,draftBone,body:HatBody(seat.hat,seat.hatColor));"),
        };
        foreach(var (file,text) in sites)Check(R(file).Contains(text),"the hat draw in "+file+" passes through the colour helpers");
        foreach(var file in Directory.GetFiles(Path.Combine(root,"Assets/ChipArena/Runtime"),"*.cs"))
        {
            string src=File.ReadAllText(file),name=Path.GetFileName(file);
            foreach(Match m in Regex.Matches(src,"\"hat-\"\\+[^;]*"))
                Check(name=="NativeDevFighterEffects.cs"||name=="NativeBodyChipPresentation.cs"||name=="NativeDeathBroadcastPresentation.cs"||name=="NativeDraftPresentation.cs"&&m.Value=="\"hat-\"+hat"||name=="NativeHatPresentation.cs",
                    "no hat model is named by hand outside the ghost, hologram and draft-framing paths ("+name+": "+m.Value+")");
        }
        Check(portraits.Contains("HatModel=NativeHatLooks.Model(Hat,color)??\"hat-none\";HatTint=tint<0?Vector4.one:V4(NativePortraitRules.Linear(tint));")&&portraits.Contains("foreach(var p in Parts(s.HatModel))Model(p,p.Bone>=0&&p.Bone<22?bones[p.Bone]:rootMatrix,s.HatTint,Vector4.one,Vector4.one,Vector4.one);"),"portraits draw the hat in its colour (white on Auto: as before)");
        Check(portraits.Contains("if(p.Mesh==null||p.Bone!=CapturedRig.Head)continue;"),"portrait hat framing measures head-space parts only");
        Check(portraits.Contains("else if(!siren&&hat==NativeSheldonHead.Hat&&Parts(NativeSourceModels.SheldonLive).Length>0)SheldonHead(s,index,time,rootMatrix);")&&portraits.Contains("NativeSheldonHead.Evaluate(time,s.Key,-1,-1,Reduced)"),"portraits animate the Sheldon Head's face and ring");
        Check(Count(present,"NativeSheldonHead.Build()")==1&&Count(portraits,"NativeSheldonHead.Build")==0,"the Sheldon Head's meshes (face included) are built once, in the bank, and shared by the portraits");
        Check(portraitUi.Contains("Color tint=backColor<0?accent:U.Hex(backColor);")&&portraitUi.Contains("PortraitStage(stage,1,look,prefs.lobbyBack,NativeHatLooks.Rgb(prefs.backColor),"),"the backdrop takes the backdrop colour, else the accent");
        Check(portraitUi.Contains("look.HatColor=seat.hatColor;")&&portraitUi.Contains("look.HatColor=s.hatColor;")&&portraitUi.Contains("backColor=NativeHatLooks.Rgb(s.backColor);"),"lobby and results cards show each seat's colours (validated on use)");

        // 11. The Locker: both pickers in Appearance, tried on by hovering, saved with the pilot; baseline, dirty and revert.
        Check(locker.Contains("LkHeading(lockerSlots[k++],\"HAT COLOUR\",LkPickerHint(true));LkColorPicker(lockerSlots[k++],true);")&&locker.Contains("LkHeading(lockerSlots[k++],\"BACKDROP COLOUR\",LkPickerHint(false));LkColorPicker(lockerSlots[k++],false);")&&Count(locker,"LkReserve(LkPickerH());")==2,"the HAT COLOUR and BACKDROP COLOUR pickers are laid out and drawn");
        Check(locker.Contains("HatColor=lockerHatColor??prefs.hatColor}")&&locker.Contains("NativeHatLooks.Rgb(lockerBackColor??prefs.backColor),accent,body,"),"the stage tries the hovered colours on");
        Check(Regex.IsMatch(locker,@"b\.HatColor!=prefs\.hatColor\b")&&Regex.IsMatch(locker,@"b\.BackColor!=prefs\.backColor\b")&&locker.Contains("prefs.hatColor=b.HatColor;prefs.backColor=b.BackColor;"),"the baseline covers, dirties and reverts both colours");
        Check(picker.Contains("private void LkSetPick(bool hatSide,string value){if(hatSide)prefs.hatColor=value;else prefs.backColor=value;")&&picker.Contains("LkSetPick(hatSide,\"\")")&&picker.Contains("var pick=NativeHatLooks.Hex(NativeHatLooks.Custom(lkHue[side],lkShade[side]));"),"picks store \"\" (Auto) or #rrggbb");
        Check(!picker.Contains("U.Push(")&&!Regex.IsMatch(picker,@"\bnew Texture2D\b[^;]*;[^\n]*\bnew Texture2D")&&picker.Contains("if(lkHueTrack!=null)return lkHueTrack;")&&picker.Contains("if(lkShadeFor[which]!=hue)"),"the pickers draw untransformed and make their tracks once");

        // 12. The colours travel like the backdrop key: prefs (Auto), profile cosmetics (no initializer), host seat
        // (normalised), lobby and match seat frames (capped, normalised on receive), account loads adopt them when present,
        // and the pilot's actor carries the hat colour from the seat (never per tick on the wire).
        Check(game.Contains("public string hatColor=\"\",backColor=\"\";"),"prefs default to Auto (older saves too)");
        Check(account.Contains("public string hatColor,backColor;}"),"profile cosmetics carry both with no default");
        Check(match.Contains("public string hatColor=NativeHatLooks.Auto,backColor=NativeHatLooks.Auto;"),"seats (bots included) default to Auto");
        Check(network.Contains("hatColor=NativeHatLooks.Normalize(prefs.hatColor),backColor=NativeHatLooks.Normalize(prefs.backColor),")&&network.Contains("hatColor=NativeHatLooks.Normalize(c.hatColor),backColor=NativeHatLooks.Normalize(c.backColor),"),"Profile() sends them normalised and the host seat normalises them");
        Check(lobby.Contains("lobbyBack=s.lobbyBack,hatColor=s.hatColor,backColor=s.backColor,"),"the lobby seat frame carries them");
        Check(frame.Contains("introText,lobbyPose,lobbyBack,hatColor,backColor;")&&frame.Contains("s.hatColor!=null&&s.hatColor.Length>NativeHatLooks.MaxLength||s.backColor!=null&&s.backColor.Length>NativeHatLooks.MaxLength")&&frame.Contains("lobbyBack=s.lobbyBack,hatColor=s.hatColor,backColor=s.backColor,killEffect")&&frame.Contains("seat.hatColor=NativeHatLooks.Normalize(sf.hatColor);seat.backColor=NativeHatLooks.Normalize(sf.backColor);"),"the match seat frame carries, caps and normalises them");
        Check(profile.Contains("if(!string.IsNullOrEmpty(c.hatColor))prefs.hatColor=NativeHatLooks.Normalize(c.hatColor);if(!string.IsNullOrEmpty(c.backColor))prefs.backColor=NativeHatLooks.Normalize(c.backColor);"),"an account load adopts a colour only when the profile has one");
        Check(model.Contains("[NonSerialized] public string HatColor=\"\";")&&arena.Contains("a.Hat=p.Seat.hat;a.HatColor=p.Seat.hatColor;")&&frame.Contains("v.HatColor=p.Seat.hatColor;")&&frame.Contains("if(p.Death?.actor!=null)p.Death.actor.HatColor=p.Seat.hatColor;"),"the actor's hat colour comes from the seat on host, guests, replays and deaths");
        var actor=new Actor{HatColor="#ff3dcf"};Check(actor.ViewCopy().HatColor=="#ff3dcf"&&new Actor().HatColor==""&&typeof(Actor).GetField("HatColor").GetCustomAttribute<NonSerializedAttribute>()!=null,"view copies keep the colour; the frame serializer (JsonUtility) skips it");

        Console.WriteLine("PASS Hat looks: "+count+" checks (colour rules, Auto unchanged, recolour table on "+NativeHatLooks.TintableHats.Count()+" hats, DEV gate, Sheldon Head mesh "+verts+" vertices / "+tris+" triangles, face, clearance over "+clearance.frames+" frames, wire path)");
    }

    // A NativeAccess as Verify would make it (reflection: its setters are private).
    private static NativeAccess Access(bool developer,double expires,params string[] grants)
    {
        var a=(NativeAccess)Activator.CreateInstance(typeof(NativeAccess),true);var t=typeof(NativeAccess);
        t.GetProperty("Developer").SetValue(a,developer);t.GetProperty("Expires").SetValue(a,expires);
        var set=(HashSet<string>)t.GetField("grants",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(a);foreach(var g in grants)set.Add(g);return a;
    }

    private sealed class Section{public int Bone;public float[] Positions,Colors;}
    private static Dictionary<string,Section[]> BankModels(string root)
    {
        var env=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/SourceModels.json"))).RootElement;
        using var zip=new GZipStream(new MemoryStream(Convert.FromBase64String(env.GetProperty("data").GetString())),CompressionMode.Decompress);using var reader=new StreamReader(zip);
        var result=new Dictionary<string,Section[]>();
        foreach(var m in JsonDocument.Parse(reader.ReadToEnd()).RootElement.GetProperty("models").EnumerateArray())
        {
            string id=m.GetProperty("id").GetString();if(!id.StartsWith("hat-")&&!id.StartsWith("weapon-"))continue;
            result[id]=m.GetProperty("sections").EnumerateArray().Select(s=>new Section{Bone=s.GetProperty("bone").GetInt32(),Positions=s.GetProperty("positions").EnumerateArray().Select(x=>x.GetSingle()).ToArray(),Colors=s.GetProperty("colors").EnumerateArray().Select(x=>x.GetSingle()).ToArray()}).ToArray();
        }
        return result;
    }

    // Per frame: does any blade vertex (the bank weapon on its hand joints) or arm box corner lie in the Sheldon Head's cloth
    // where the bare rig (head, neck and shoulder boxes) had nothing? Frames are the four melee weapons' three-swing chains,
    // their guard and their lobby poses.
    private const float Deep=.015f;private static readonly Vector3[] Axes={Vector3.UnitX,-Vector3.UnitX,Vector3.UnitY,-Vector3.UnitY,Vector3.UnitZ,-Vector3.UnitZ};
    private static (int frames,float blade,float arms,int bladeDeep,string worst) Clearance(Dictionary<string,Section[]> bank,out Dictionary<string,int[]> by)
    {
        var groups=new Dictionary<string,int[]>();by=groups;
        int frames=0,bladeFrames=0,armFrames=0,deepest=0;string worst="none";var pose=new CapturedRig.Pose();
        var bare=CapturedRig.Boxes.Where(b=>b.Bone==CapturedRig.Head||b.Bone==CapturedRig.Neck||b.Bone==CapturedRig.LClav||b.Bone==CapturedRig.RClav||b.Bone==CapturedRig.Chest).ToArray();
        // Forearms and hands (the upper arms meet the shoulders at their joint, so they always touch the drapes there).
        var arms=CapturedRig.Boxes.Where(b=>b.Bone==CapturedRig.LElbow||b.Bone==CapturedRig.LHand||b.Bone==CapturedRig.RElbow||b.Bone==CapturedRig.RHand).ToArray();
        var world=new Matrix4x4[22];var inverse=new Matrix4x4[22];
        void Frame(string spec,string what)
        {
            pose.World();for(int i=0;i<22;i++){world[i]=Matrix4x4.CreateFromQuaternion(pose.WorldRotation[i])*Matrix4x4.CreateTranslation(pose.WorldPosition[i]);Matrix4x4.Invert(world[i],out inverse[i]);}
            bool Cloth(Vector3 w)=>NativeSheldonHead.HoodSolid(Vector3.Transform(w,inverse[CapturedRig.Head]))||NativeSheldonHead.DrapeSolid(Vector3.Transform(w,inverse[CapturedRig.LClav]),1)||NativeSheldonHead.DrapeSolid(Vector3.Transform(w,inverse[CapturedRig.RClav]),-1)||NativeSheldonHead.MantleSolid(Vector3.Transform(w,inverse[CapturedRig.Chest]));
            bool InBare(Vector3 w){foreach(var b in bare){var l=Vector3.Transform(w,inverse[b.Bone])-b.Position;var h=b.Size/2+new Vector3(.01f);if(Math.Abs(l.X)<h.X&&Math.Abs(l.Y)<h.Y&&Math.Abs(l.Z)<h.Z)return true;}return false;}
            // Through the cloth, not grazing it: solid (cloth or body) Deep metres away along every axis.
            bool Solid(Vector3 w)=>Cloth(w)||InBare(w);
            bool InCloth(Vector3 w){if(!Cloth(w))return false;foreach(var d in Axes)if(!Solid(w+d*Deep))return false;return true;}
            int blade=0;
            if(bank.TryGetValue("weapon-"+spec,out var sections))foreach(var s in sections)
            {
                if(s.Bone<0||s.Bone>=22)continue;for(int i=0;i<s.Positions.Length;i+=9){var w=Vector3.Transform(new Vector3(-s.Positions[i],s.Positions[i+1],s.Positions[i+2]),world[s.Bone]);if(InCloth(w)&&!InBare(w))blade++;}
            }
            bool arm=false;
            foreach(var b in arms)for(int c=0;c<8&&!arm;c++){var corner=b.Position+new Vector3((c&1)!=0?b.Size.X/2:-b.Size.X/2,(c&2)!=0?b.Size.Y/2:-b.Size.Y/2,(c&4)!=0?b.Size.Z/2:-b.Size.Z/2)*.9f;var w=Vector3.Transform(corner,world[b.Bone]);if(InCloth(w)&&!InBare(w))arm=true;}
            frames++;if(blade>0)bladeFrames++;if(arm)armFrames++;if(blade>deepest){deepest=blade;worst=spec+" "+what;}
            string key=spec+" "+(what.StartsWith("lobby")?"lobby":what.Split(' ')[0]);if(!groups.TryGetValue(key,out var gr))groups[key]=gr=new int[3];gr[0]++;if(blade>0)gr[1]++;if(arm)gr[2]++;
        }
        foreach(var spec in new[]{"sword","daggers","shield","greatsword"})
        {
            var motion=new MeleeMotion{Impacts=new MeleeImpacts()};float dt=1/60f;
            for(int chain=0;chain<3;chain++)
            {
                float start=1+chain*1.4f;
                for(float t=start-.1f;t<start+1.3f;t+=dt)
                {
                    var view=new MeleeView{Id="sheldon-"+spec,Spec=spec,Grounded=true,LastAttack=start,AttackIndex=chain,Windup=spec=="greatsword"?.32f:.14f,AttackRate=1,SwingAt=start+(spec=="greatsword"?.32f:.14f),SwingUntil=start+(spec=="greatsword"?.32f:.14f)+.16f};
                    motion.Sample(view,t,dt);motion.Rig.WritePose(pose);Frame(spec,"swing "+chain+" at "+(t-start).ToString("0.00"));
                }
            }
            for(float t=6;t<8;t+=dt){var view=new MeleeView{Id="sheldon-"+spec,Spec=spec,Grounded=true,Guarding=true,BlockTime=6,LastBlock=6};motion.Sample(view,t,dt);motion.Rig.WritePose(pose);Frame(spec,"guard");}
            for(float t=9;t<11;t+=dt){var view=new MeleeView{Id="sheldon-"+spec,Spec=spec,Grounded=true,Vel=new Vector3(0,0,6),Sprinting=true};motion.Sample(view,t,dt);motion.Rig.WritePose(pose);Frame(spec,"sprint");}
        }
        foreach(var spec in new[]{"pistol","rifle","machinegun","sword","daggers","shield","greatsword","fire","water","lightning","darkness"})for(int p=0;p<NativeLobbyPoses.Keys.Length;p++)for(float t=0;t<4;t+=.25f)
        {var rig=new MeleeRig();rig.Reset();NativeLobbyPoses.Sample(rig,spec,p,t,out _,out _);rig.WritePose(pose);Frame(spec,"lobby "+NativeLobbyPoses.Keys[p]);}
        return (frames,frames>0?bladeFrames/(float)frames:0,frames>0?armFrames/(float)frames:0,deepest,worst);
    }
}
