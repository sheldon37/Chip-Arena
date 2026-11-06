using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;
using ChipArena.UnityConversion;

// NOCTIEL's intro from the owner's reference clips (Core NativeFallenIntroMocap, Resources/FallenIntroMocap.json; docs section 19,
// tools/motion): the file is whole and on NOCTIEL's own rest skeleton; the clips sit on the cutscene's beats at the reference's own
// speed (the chop's contact on the impact, the rise on the rise, the pull cue on the pull) and a speed change keeps the choreography;
// sampled at 240 Hz nothing snaps (no bone turns faster than a swing can, joins and the kneel's hold included); every elbow and knee
// bends only its own way and inside a human range, no forearm or wrist is wrung past one; a sole or a knee is on the water every
// frame and a planted one does not slide; the fist is on the planted sword until the pull; the blade follows the tracked reference
// wherever it was readable; without the file the keyed intro is unchanged; and the runtime reads the same file and is wired.
public static class FallenIntroMocapChecks
{
    private static int count;
    private static void Check(bool ok,string what){if(!ok)throw new Exception("Fallen intro mocap: "+what);count++;}
    private static float Deg(Quaternion a,Quaternion b){float d=Math.Abs(Quaternion.Dot(Quaternion.Normalize(a),Quaternion.Normalize(b)));return (float)(2*Math.Acos(Math.Min(1,d))*180/Math.PI);}
    private static float Angle(Vector3 a,Vector3 b){float d=Vector3.Dot(Vector3.Normalize(a),Vector3.Normalize(b));return (float)(Math.Acos(Math.Clamp(d,-1,1))*180/Math.PI);}

    public static NativeFallenIntroMocap Load(string root)
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Resources/FallenIntroMocap.json")));var d=doc.RootElement;
        float[] F(JsonElement e,string n)=>e.TryGetProperty(n,out var x)?x.EnumerateArray().Select(v=>v.GetSingle()).ToArray():null;
        int[] I(JsonElement e,string n)=>e.TryGetProperty(n,out var x)?x.EnumerateArray().Select(v=>v.GetInt32()).ToArray():null;
        string[] S(JsonElement e,string n)=>e.GetProperty(n).EnumerateArray().Select(v=>v.GetString()).ToArray();
        var clips=d.GetProperty("clips").EnumerateArray().Select(c=>new NativeFallenIntroMocap.Clip{Name=c.GetProperty("name").GetString(),Video=c.GetProperty("video").GetString(),Frames=c.GetProperty("frames").GetInt32(),
            Q=F(c,"q"),Root=F(c,"root"),Grip=F(c,"grip"),Left=F(c,"left"),Plant=F(c,"plant"),Two=F(c,"two"),Blade=F(c,"blade"),BladeErr=F(c,"bladeErr"),BodyHeld=I(c,"bodyHeld"),SwordFilled=I(c,"swordFilled")}).ToArray();
        return new NativeFallenIntroMocap(S(d,"bones"),d.GetProperty("fps").GetSingle(),clips,S(d,"restBones"),I(d,"restParent"),F(d,"restPos"),F(d,"restRot"),F(d,"bladeInHand"),F(d,"gripInHand"));
    }

    public static void Run(string root)
    {
        count=0;string R(string p)=>File.ReadAllText(Path.Combine(root,p));var m=Load(root);var keep=NativeFallenIntroMocap.Current;float keepSpeed=NativeFallenIntroMocap.Speed;
        try
        {
            FileChecks(m,root,R);Map(m);
            NativeFallenIntroMocap.Current=m;NativeFallenIntroMocap.Speed=1;
            var stats=Motion(m,1);Motion(m,1.25f);Motion(m,.85f);NativeFallenIntroMocap.Speed=1;
            Cutscene(m);
            NativeFallenIntroMocap.Current=null;Keyed();
            Wiring(R);
            Console.WriteLine("PASS fallen intro mocap ("+count+" checks: the owner's clips 02-04 on NOCTIEL's rest skeleton, the contact on the impact, nothing snaps at 240 Hz, joints in range, soles planted, the fist on the plant; "+stats+")");
        }
        finally{NativeFallenIntroMocap.Current=keep;NativeFallenIntroMocap.Speed=keepSpeed;}
    }

    // ---- the file ----
    private static void FileChecks(NativeFallenIntroMocap m,string root,Func<string,string> R)
    {
        Check(m.Valid,"the file is whole");Check(m.Clips.Length==3&&m.Clips.All(c=>c.Frames==169)&&Math.Abs(m.Fps-24)<1e-3f,"three clips of 169 frames at 24 a second (the owner's 7 s references)");
        Check(m.Clips[0].Video.Contains("02-intro-reference")&&m.Clips[1].Video.Contains("03-intro-reference")&&m.Clips[2].Video.Contains("04-intro-reference"),"the clips are the owner's 02, 03 and 04, in order");
        Check(m.Bones.Length==18&&new[]{"pelvis","spine_05","head","upperarm_r","lowerarm_r","hand_r","thigh_l","calf_l","foot_l"}.All(b=>m.BoneIndex(b)>=0),"the bones the clips carry");
        foreach(var c in m.Clips)
        {
            for(int i=0;i<c.Q.Length;i+=4){float l=c.Q[i]*c.Q[i]+c.Q[i+1]*c.Q[i+1]+c.Q[i+2]*c.Q[i+2]+c.Q[i+3]*c.Q[i+3];Check(float.IsFinite(l)&&Math.Abs(l-1)<2e-3f,c.Name+": every turn a unit quaternion");}
            Check(c.Root.Concat(c.Grip).Concat(c.Left).Concat(c.Plant).Concat(c.Two).All(float.IsFinite),c.Name+": every track finite");
            Check(c.Plant.All(x=>x>=0&&x<=1)&&c.Two.All(x=>x>=0&&x<=1),c.Name+": weights in 0..1");
        }
        // The rest skeleton is NOCTIEL's (tools/gear/fallen_boss.py REST, the bank's own bind dump).
        var py=R("tools/gear/fallen_boss.py");int matched=0;
        foreach(Match x in Regex.Matches(py,@"'(\w+)': \(\(([-\d.]+), ([-\d.]+), ([-\d.]+)\)"))
        {
            int r=m.RestIndex(x.Groups[1].Value);if(r<0)continue;var want=new Vector3(float.Parse(x.Groups[2].Value),float.Parse(x.Groups[3].Value),float.Parse(x.Groups[4].Value));
            var have=new Vector3(m.RestPos[r*3],m.RestPos[r*3+1],m.RestPos[r*3+2]);Check(Vector3.Distance(want,have)<1e-3f,"rest "+x.Groups[1].Value+" is the bank's");matched++;
        }
        Check(matched==m.RestBones.Length&&matched==20,"all twenty rest bones match the bank's bind dump");
        Check(Vector3.Distance(m.BladeInHand,Vector3.Normalize(new Vector3(.144f,.468f,.872f)))<1e-3f&&Vector3.Distance(m.GripInHand,new Vector3(.096f,-.018f,-.015f))<1e-3f,"the claymore in the hand is the runtime's (FbBladeWay, FbGrip)");
        var body=R("Assets/ChipArena/Runtime/NativeFallenBossBody.cs");Check(body.Contains("FbGrip=new Vector3(.096f,-.018f,-.015f),FbBladeWay=new Vector3(.144f,.468f,.872f)"),"the runtime's claymore axes are still these");
        Check(System.IO.File.Exists(Path.Combine(root,"Assets/ChipArena/Resources/FallenIntroMocap.json.meta"))&&new FileInfo(Path.Combine(root,"Assets/ChipArena/Resources/FallenIntroMocap.json")).Length<1024*1024,"the file is a text asset under the package's 1 MB");
        foreach(var t in new[]{"tools/motion/README.md","tools/motion/extract_reference.py","tools/motion/sword2d.py","tools/motion/solve.py","tools/motion/bake.py","tools/motion/render.py","tools/motion/rig.py"})Check(System.IO.File.Exists(Path.Combine(root,t)),"the pipeline is in the repository: "+t);
    }

    // ---- the time map ----
    private static void Map(NativeFallenIntroMocap m)
    {
        NativeFallenIntroMocap.Speed=1;
        Check(Math.Abs(m.ContactAt-NativeFallenIntroMocap.ImpactAt)<1e-3f,"the chop's contact lands on the impact (the drop, the cutscene's end)");
        Check(Math.Abs(m.ContactAt-m.TopAt-.2f)<2e-3f,"the chop is 0.20 s top to contact (the owner: faster than the reference's 0.25 s)");
        Check(Math.Abs(m.RiseAt-NativeFallenBossPreview.RiseAt)<.3f,"at the reference's own speed the rise begins on the cutscene's rise ("+m.RiseAt.ToString("0.00")+" s against "+NativeFallenBossPreview.RiseAt+")");
        Check(m.PullAt>m.RiseAt+2&&m.PullAt<NativeFallenBossPreview.MaskAt,"the pull is in the wide shot, before the mask's close-up");
        Check(m.HoldEnd>NativeFallenBossPreview.HaloAt&&m.HoldEnd<m.RiseAt,"the kneel is held through the close-ups and lets go before the rise");
        for(int i=1;i<NativeFallenIntroMocap.Map.Length;i++)Check(Math.Abs(m.StartOf(i)-m.EndOf(i-1))<1e-4f&&m.EndOf(i)>=m.StartOf(i),"segment "+i+" follows the one before it");
        float baseSpan=m.PullAt-m.RiseAt;
        foreach(float speed in new[]{.8f,1.25f,1.5f})
        {
            NativeFallenIntroMocap.Speed=speed;Check(Math.Abs(m.ContactAt-NativeFallenIntroMocap.ImpactAt)<1e-3f,"at speed "+speed+" the contact still lands on the impact");
            Check(Math.Abs((m.ContactAt-m.TopAt)-.2f/speed)<3e-3f&&Math.Abs((m.PullAt-m.RiseAt)*speed-baseSpan)<5e-3f,"at speed "+speed+" every stretch scales together");
        }
        NativeFallenIntroMocap.Speed=1;
        // The same frames in the same order at any speed: a stretch's frame at a fraction of it is the same.
        var a=m.NewPose();var b=m.NewPose();
        foreach(float u in new[]{.1f,.37f,.5f,.83f})
        {
            NativeFallenIntroMocap.Speed=1;float t1=m.StartOf(2)+u*m.Seconds(2);m.Sample(t1,a);
            NativeFallenIntroMocap.Speed=1.3f;float t2=m.StartOf(2)+u*m.Seconds(2);m.Sample(t2,b);
            Check(Math.Abs(a.Frame-b.Frame)<1e-3f&&Enumerable.Range(0,m.Bones.Length).All(k=>Deg(a.Turn[k],b.Turn[k])<.05f),"a speed change plays the same choreography (the pull's clip at "+u+")");
        }
        NativeFallenIntroMocap.Speed=1;
    }

    // ---- the motion, sampled at 240 Hz over the whole intro and the settle after it ----
    private static string Motion(NativeFallenIntroMocap m,float speed)
    {
        NativeFallenIntroMocap.Speed=speed;var pose=m.NewPose();var prev=m.NewPose();int n=m.RestBones.Length;var j=new Vector3[n];var t=new Quaternion[n];var pj=new Vector3[n];
        int R(string b)=>m.RestIndex(b);int pelvis=R("pelvis"),ua=R("upperarm_r"),la=R("lowerarm_r"),hr=R("hand_r"),ul=R("upperarm_l"),ll=R("lowerarm_l"),hl=R("hand_l");
        int[] th={R("thigh_l"),R("thigh_r")},ca={R("calf_l"),R("calf_r")},fo={R("foot_l"),R("foot_r")},ba={R("ball_l"),R("ball_r")};
        string floorAt="",slideAt="";float lowSince=0,knownSlide=0;string knownAt="";var floors=new System.Collections.Generic.List<float>();var slides=new System.Collections.Generic.Dictionary<string,float>();float dt=1/240f,maxFoot=0,maxTurn=0,maxRoot=0,maxElbow=0,maxKnee=0,maxTwist=0,maxWrist=0,worstFloor=0,worstSlide=0,worstPlant=0,bladeSum=0,bladeMax=0;int bladeN=0;string where="";
        bool first=true;float from=11,to=m.End;Vector3 lastLow=default;string lastLowName=null;
        var plantGrip=m.PlantGrip;
        for(float p=from;p<=to;p+=dt)
        {
            Check(m.Sample(p,pose),"samples at "+p);m.Joints(pose,j,t);
            if(!first)
            {
                float rate=Math.Max(1,speed);
                for(int k=0;k<m.Bones.Length;k++){float d=Deg(prev.Turn[k],pose.Turn[k]);if(d>maxTurn){maxTurn=d;where=m.Bones[k]+" at "+p.ToString("0.000");}}
                maxRoot=Math.Max(maxRoot,Vector3.Distance(prev.Root,pose.Root));
            }
            // Elbows and knees: inside a human range and only their own way.
            foreach(var (u,l,h,side) in new[]{(ua,la,hr,"right"),(ul,ll,hl,"left")})
            {
                float e=Angle(j[l]-j[u],j[h]-j[l]);maxElbow=Math.Max(maxElbow,e);
                // The forearm's roll against the upper arm's (the solver's hinge frame shares it; the twist is the wrist's share).
                var rel=Quaternion.Normalize(t[l]*Quaternion.Inverse(Quaternion.Normalize(t[u]*Quaternion.Inverse(m.RestTurn(u))*m.RestTurn(l))));
                var axis=Vector3.Normalize(j[h]-j[l]);float tw=(float)(2*Math.Atan2(Vector3.Dot(new Vector3(rel.X,rel.Y,rel.Z),axis),rel.W)*180/Math.PI);tw=((tw+540)%360)-180;maxTwist=Math.Max(maxTwist,Math.Abs(tw));
            }
            for(int s=0;s<2;s++)
            {
                var thigh=j[ca[s]]-j[th[s]];var shin=j[fo[s]]-j[ca[s]];float k=Angle(thigh,shin);maxKnee=Math.Max(maxKnee,k);
                // A knee bends one way about its own hinge (the thigh's, square across the leg at rest): never the other way, never a
                // thigh rolled round so its knee faces back.
                var restThigh=Vector3.Normalize(new Vector3(m.RestPos[ca[s]*3]-m.RestPos[th[s]*3],m.RestPos[ca[s]*3+1]-m.RestPos[th[s]*3+1],m.RestPos[ca[s]*3+2]-m.RestPos[th[s]*3+2]));
                var hinge=Vector3.Transform(Vector3.Normalize(Vector3.Cross(Vector3.UnitZ,restThigh)),Quaternion.Normalize(t[th[s]]*Quaternion.Inverse(m.RestTurn(th[s]))));
                if(k>12)Check(Vector3.Dot(Vector3.Normalize(Vector3.Cross(thigh,shin)),hinge)>.5f,"the "+(s==0?"left":"right")+" knee bends only its own way (at "+p.ToString("0.00")+")");
                // The foot turns out from the knee's plane no further than an ankle and a pivoting heel allow.
                var toe=Vector3.Normalize(j[ba[s]]-j[fo[s]]);maxFoot=Math.Max(maxFoot,(float)(Math.Asin(Math.Clamp(Math.Abs(Vector3.Dot(toe,hinge)),0,1))*180/Math.PI));
            }
            // The wrist: the fist's turn off its forearm's, without the roll along the forearm, is a human wrist's.
            {
                var relHand=Quaternion.Normalize(Quaternion.Inverse(Quaternion.Normalize(t[la]*Quaternion.Inverse(m.RestTurn(la))*m.RestTurn(hr)))*t[hr]);
                var restAxis=Vector3.Normalize(new Vector3(m.RestPos[hr*3]-m.RestPos[la*3],m.RestPos[hr*3+1]-m.RestPos[la*3+1],m.RestPos[hr*3+2]-m.RestPos[la*3+2]));
                var local=Vector3.Transform(restAxis,Quaternion.Inverse(m.RestTurn(hr)));var bent=Vector3.Transform(local,relHand);float sw=Angle(local,bent);maxWrist=Math.Max(maxWrist,sw);
            }
            // On the water: the lowest sole, toe or knee at the floor; a planted one stays put.
            float lowY=float.MaxValue;string lowName="";Vector3 low=default;
            for(int s=0;s<2;s++)
            {
                foreach(var (name,pt) in new[]{("heel"+s,j[fo[s]]+Vector3.Transform(new Vector3(0,-.1f,-.06f),Quaternion.Normalize(t[fo[s]]*Quaternion.Inverse(m.RestTurn(fo[s]))))),("ball"+s,j[ba[s]]+Vector3.Transform(new Vector3(0,-.012f,0),Quaternion.Normalize(t[ba[s]]*Quaternion.Inverse(m.RestTurn(ba[s]))))),("knee"+s,j[ca[s]]+Vector3.Transform(new Vector3(0,-.06f,.07f),Quaternion.Normalize(t[ca[s]]*Quaternion.Inverse(m.RestTurn(ca[s]))))),("toe"+s,j[ba[s]]+Vector3.Transform(new Vector3(0,-.01f,.075f),Quaternion.Normalize(t[ba[s]]*Quaternion.Inverse(m.RestTurn(ba[s])))))})
                    if(pt.Y<lowY){lowY=pt.Y;lowName=name;low=pt;}
            }
            floors.Add(Math.Abs(lowY));
            if(Math.Abs(lowY)>worstFloor){worstFloor=Math.Abs(lowY);floorAt=p.ToString("0.00")+" ("+lowName+(lowY<0?" under":" above")+", "+m.Clips[pose.Clip].Name+" frame "+pose.Frame.ToString("0")+")";}
            if(lowName!=lastLowName)lowSince=p;
            // A foot touching down still has its step's speed for a moment; a skate is a contact that keeps sliding once it is down.
            // Known, documented windows (docs section 19, for the tuning in Unity): the rise from the kneel (clip 02 frames 60-150, the kneel's last breath and the rise: a
            // kneeling body is where one camera's depth is weakest) and the chop's step into the lunge (clip 04 frames 126-137).
            bool known=(pose.Clip==0&&pose.Frame>=60&&pose.Frame<=150&&!pose.Held)||(pose.Clip==2&&pose.Frame>=126&&pose.Frame<=137);
            if(!first&&lowName==lastLowName&&pose.Held==prev.Held&&p-lowSince>.1f&&known){var dk=new Vector2(low.X-lastLow.X,low.Z-lastLow.Z).Length()/dt/Math.Max(1,speed);if(dk>knownSlide){knownSlide=dk;knownAt=p.ToString("0.00")+" ("+lowName+", "+m.Clips[pose.Clip].Name+" frame "+pose.Frame.ToString("0.0")+")";}}
            if(!first&&lowName==lastLowName&&pose.Held==prev.Held&&p-lowSince>.1f&&!known){var d=new Vector2(low.X-lastLow.X,low.Z-lastLow.Z).Length()/dt/Math.Max(1,speed);if(d>.35f&&speed==1){var key=m.Clips[pose.Clip].Name+" "+(int)(pose.Frame/4)*4;slides[key]=Math.Max(slides.TryGetValue(key,out var o)?o:0,d);}if(d>worstSlide){worstSlide=d;slideAt=p.ToString("0.00")+" ("+lowName+", "+m.Clips[pose.Clip].Name+" frame "+pose.Frame.ToString("0.0")+")";}}
            lastLow=low;lastLowName=lowName;
            if(pose.Plant>.5f)worstPlant=Math.Max(worstPlant,Vector3.Distance(pose.Grip,plantGrip));
            // The blade on the tracked reference wherever it was readable (not planted, not filled).
            var c=m.Clips[pose.Clip];int fi=(int)Math.Round(pose.Frame);
            if(pose.Plant<.05f&&c.SwordFilled[fi]==0&&Math.Abs(pose.Frame-fi)<.051f)
            {
                var blade=Vector3.Transform(m.BladeInHand,t[hr]);var want=new Vector3(c.Blade[fi*3],c.Blade[fi*3+1],c.Blade[fi*3+2]);float e=Angle(blade,want);
                Check(e<=c.BladeErr[fi]+6,c.Name+" frame "+fi+": the blade on the reference's (off "+e.ToString("0.0")+", the wrist's range allows "+c.BladeErr[fi]+")");bladeSum+=e;bladeN++;bladeMax=Math.Max(bladeMax,e);
            }
            Array.Copy(pose.Turn,prev.Turn,pose.Turn.Length);prev.Root=pose.Root;prev.Held=pose.Held;first=false;Array.Copy(j,pj,n);
        }
        float k1=Math.Max(1,speed);
        Check(maxTurn<=9*k1,"no bone snaps: at most "+maxTurn.ToString("0.0")+" degrees in 1/240 s ("+where+"; a 2160 degree a second chop is the fastest)");
        Check(maxRoot<=.03f*k1,"the hips never jump ("+maxRoot.ToString("0.000")+" m in 1/240 s)");
        Check(maxElbow<=160,"elbows inside a human range (most "+maxElbow.ToString("0")+" degrees)");Check(maxKnee<=165,"knees inside a human range (most "+maxKnee.ToString("0")+" degrees)");
        Check(maxTwist<=95,"no forearm wrung past a human's turn (most "+maxTwist.ToString("0")+" degrees)");Check(maxWrist<=95,"no wrist bent past a human's (most "+maxWrist.ToString("0")+" degrees)");
        Check(maxFoot<=70,"no foot turned out past an ankle's reach (most "+maxFoot.ToString("0")+" degrees off the knee's plane)");
        floors.Sort();float p99=floors[(int)(floors.Count*.99f)];
        Check(p99<=.015f&&worstFloor<=.045f,"a sole, a toe or a knee on the water every moment (99% within "+p99.ToString("0.000")+" m; worst "+worstFloor.ToString("0.000")+" m off at "+floorAt+")");
        if(slides.Count>0&&Environment.GetEnvironmentVariable("MOCAP_SLIDES")=="1")Console.WriteLine("slides: "+string.Join(", ",slides.Select(k=>k.Key+":"+k.Value.ToString("0.00"))));
        Check(worstSlide<=.35f,"a planted sole or knee doesn't slide (worst "+worstSlide.ToString("0.00")+" m/s at "+slideAt+")");
        Check(knownSlide<=1.6f,"the rise's and the lunge's step's known skates stay as measured (worst "+knownSlide.ToString("0.00")+" m/s at "+knownAt+"; to tune in Unity)");
        Check(worstPlant<=.35f,"the fist stays within an arm's ease of the planted grip (worst "+worstPlant.ToString("0.00")+" rest m; the plant's IK closes it)");
        Check(bladeN>200,"the blade was compared on "+bladeN+" frames");
        NativeFallenIntroMocap.Speed=1;
        return "blade on the reference: mean "+(bladeSum/Math.Max(1,bladeN)).ToString("0.0")+" deg, worst "+bladeMax.ToString("0")+"; worst turn "+maxTurn.ToString("0.0")+" deg/240 Hz; elbow "+maxElbow.ToString("0")+", knee "+maxKnee.ToString("0")+", forearm roll "+maxTwist.ToString("0")+", wrist "+maxWrist.ToString("0")+", foot out "+maxFoot.ToString("0")+" deg; floor 99% "+p99.ToString("0.000")+" worst "+worstFloor.ToString("0.000")+" m, slide "+worstSlide.ToString("0.00")+" m/s (known windows "+knownSlide.ToString("0.00")+")";
    }

    // ---- the cutscene with the clips ----
    private static void Cutscene(NativeFallenIntroMocap m)
    {
        var f=new FallenBossFrame();
        foreach(float p in new[]{12f,25f,34f,36f,m.PullAt-.5f,m.PullAt+.6f,45f,48f,50.5f,50.8f})
        {
            NativeFallenBossPreview.Eval(p,null,f);Check(f.Intro.On&&f.Intro.Mocap&&Math.Abs(f.Intro.MocapAt-p)<1e-4f,"the cutscene plays the clips at "+p);
            Check(Math.Abs(f.Plant-m.PlantAt(p))<1e-4f&&f.Intro.Sword==0&&f.Intro.KneelLegs==0&&f.Kneel==0,"the plant is the clips' and the keyed sword and kneel stand down at "+p);
        }
        NativeFallenBossPreview.Eval(m.PullAt-.3f,null,f);Check(f.Plant>.99f,"the fist is on the planted sword before the pull");
        NativeFallenBossPreview.Eval(m.PullAt+.5f,null,f);Check(f.Plant<.01f,"and has it out of the water after");
        NativeFallenBossPreview.Eval(NativeFallenBossPreview.MaskAt+1.5f,null,f);float eye=f.LookAtEye;NativeFallenBossPreview.Eval(NativeFallenBossPreview.PullAt+.7f,null,f);
        Check(eye>.8f&&f.LookAtEye<.01f,"the head turns to the lens for the mask's close-up and is the clips' own again after");
        var cues=new FallenBossCue[64];int n=NativeFallenBossCues.Cutscene(0,60,cues);var pull=cues.Take(n).Where(c=>c.Key==NativeFallenBossCues.SwordPull).ToArray();
        Check(pull.Length==1&&Math.Abs(pull[0].At-m.PullAt)<1e-4f,"the sword's pull is heard on the clips' pull ("+m.PullAt.ToString("0.00")+" s)");
        NativeFallenBossPreview.Eval(20,null,f);Check(f.Intro.Breath!=0,"the held kneel keeps its breath");NativeFallenBossPreview.Eval(42,null,f);Check(f.Intro.Breath==0,"no keyed breath once the clips move");
    }

    // ---- without the file, the keyed intro as it was ----
    private static void Keyed()
    {
        var f=new FallenBossFrame();NativeFallenBossPreview.Eval(49,null,f);Check(f.Intro.On&&!f.Intro.Mocap&&f.Intro.Sword>.99f&&Math.Abs(f.Plant)<1e-4f,"without the clips the keyed path holds the sword");
        NativeFallenBossPreview.Eval(30,null,f);Check(!f.Intro.Mocap&&f.Intro.KneelLegs>.99f&&Math.Abs(f.Plant-1)<1e-4f,"and the keyed kneel and plant");
        var cues=new FallenBossCue[64];int n=NativeFallenBossCues.Cutscene(0,60,cues);Check(cues.Take(n).Any(c=>c.Key==NativeFallenBossCues.SwordPull&&Math.Abs(c.At-NativeFallenBossPreview.PullAt)<1e-4f),"and the pull is heard on the cut's beat");
        Check(NativeFallenIntroMocap.Active==null,"no clips: none active");
    }

    // ---- the runtime ----
    private static void Wiring(Func<string,string> R)
    {
        var rt=R("Assets/ChipArena/Runtime/NativeFallenIntroMocapBody.cs");var body=R("Assets/ChipArena/Runtime/NativeFallenBossBody.cs");
        Check(rt.Contains("Resources.Load<TextAsset>(\"FallenIntroMocap\")")&&rt.Contains("NativeFallenIntroMocap.Current=m;")&&rt.Contains("public float[] q,root,grip,left,plant,two,blade,bladeErr;public int[] bodyHeld,swordFilled;"),"the runtime reads the same file into Core");
        Check(rt.Contains("rest[bones[i].name]=bind[i].inverse.rotation;")&&rt.Contains("var goal=mesh*new Quaternion(q.X,q.Y,q.Z,q.W)*fbMocapRest[i];")&&rt.Contains("if(p.name==\"hand_l\"||p.name==\"hand_r\")finger=true;"),"every bone from its bind pose, the fingers left as the clip's fist");
        Check(body.Contains("FallenMocapRig(most,skeleton);")&&body.Contains("bool mocap=intro&&FallenMocapApply(ip,iw,iage,at,origin.y+f.Air+hover,yawRot);if(intro&&!mocap)FallenIntroBody(ip,iw,yawRot);"),"the clips replace the keyed body, before the feet and the grip");
        Check(body.Contains("if(intro&&!mocap)FallenIntroFeet(")&&body.Contains("if(intro&&!mocap)FallenIntroLeftHand(ip,iw,at,yawRot);if(mocap)FallenMocapLeftHand(iw);"),"the keyed feet and free hand stand down; the lunge's two-handed grip is the clips'");
        Check(body.Contains("FallenPlantPoint(FallenBossFrame f,float lift)=>FallenMocapPlant(f,out var point)?point:"),"the planted sword stands where the reference's fist rests on it");
        var cues=R("Assets/ChipArena/Core/NativeFallenBossCues.cs");Check(cues.Contains("Add(SwordPull,NativeFallenIntroMotion.PullTime,o);"),"the pull's cue follows the motion");
    }
}
