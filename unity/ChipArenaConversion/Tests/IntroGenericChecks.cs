using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using ChipArena.UnityConversion;

// The five generic intros (docs/INTROS-GENERIC.md; owner 2026-11-20: "5 new intros ... great animations and effects, non-block based
// background and designs ... great camera work"): Cloudbreak (the new default), Whiteout, Slipstream, Threshold and Flashbulb. Each
// is registered with its own length and name beat, is deterministic under a scrub, allocates nothing per frame, keeps inside the
// v2 budgets with and without its bank, flashes no faster than 3 Hz in Reduced effects, lays every long streak on its head's camera
// plane, and plays its cues on its beats with sounds that exist. Presentation only.
internal static class IntroGenericChecks
{
    private static int count;
    private static void Check(bool ok,string what){if(!ok)throw new Exception("Generic intros: "+what);count++;}
    /// <summary>The five, as registered (the default is Cloudbreak).</summary>
    public static NativeIntroGenericScript[] Make()=>new NativeIntroGenericScript[]
    {
        new NativeIntroCloudbreak(),new NativeIntroWhiteout(),new NativeIntroSlipstream(),new NativeIntroThreshold(),new NativeIntroFlashbulb(),
    };
    public static void Run(string root)
    {
        // (A review aid, not a check: with INTRO_GENERIC_VIEWS set to a folder, each intro's camera, anchors and set pieces every
        // 0.1 s are written there as JSON for an offline Blender preview of the framing; tools/intros/generic_preview.py.)
        var views=Environment.GetEnvironmentVariable("INTRO_GENERIC_VIEWS");if(!string.IsNullOrEmpty(views))foreach(var s in Make())Dump(s,views);
        // (The intros' local motion bank, when this machine has it (docs/INTROS-V2.md 9d): Slipstream's run, take-off and slide play from
        // its pack clips, as in the game; Slipstream is checked again without it, its keys alone, as CI and a build without it play it.)
        bool bank=IntroPackChecks.LoadLocal(root,out _);
        // (Likewise INTRO_GENERIC_MODEL: the body as the model draws it, bound bone for bone on the measured skeleton every 1/30 s, in
        // the source frame, for the stick-figure reviews of the walks, contacts and silhouettes: tools/intros/generic_sticks.py. With
        // the bank, Slipstream's keys alone are written beside it as slipstream-keys-model.json.)
        var model=Environment.GetEnvironmentVariable("INTRO_GENERIC_MODEL");
        if(!string.IsNullOrEmpty(model))foreach(var s in Make())
        {
            DumpModel(s,model,"");
            if(bank&&s is NativeIntroSlipstream){var local=NativeIntroPack.Local;NativeIntroPack.Local=null;try{DumpModel(s,model,"-keys");}finally{NativeIntroPack.Local=local;}}
        }
        count=0;var names=new HashSet<string>();var ids=new HashSet<string>();var rig=ModelPoseChecks.Measured();
        foreach(var s in Make())
        {
            string n=s.Title;Check(names.Add(n)&&ids.Add(s.Style),n+" has its own name and id");
            Registry(s);Timeline(s,root);Determinism(s);Budgets(s);Reduced(s);Streaks(s);Closing(s);Feet(s,rig);
            if(s is NativeIntroSlipstream ss)
            {
                Tunnel(ss,rig);Roof(ss,rig);
                if(bank){Clips(ss);var local=NativeIntroPack.Local;NativeIntroPack.Local=null;try{Feet(ss,rig);Tunnel(ss,rig);Roof(ss,rig);Budgets(ss);}finally{NativeIntroPack.Local=local;}}
            }
        }
        Check(NativeIntroCatalog.Find(NativeIntroCatalog.DefaultId)?.Make() is NativeIntroCloudbreak,"the new default is Cloudbreak");
        Console.WriteLine("PASS generic intros ("+count+" checks)");
    }

    private sealed class Record:INativeIntroSet
    {
        public bool Ready=>true;public bool Has(string prop)=>true;public readonly System.Text.StringBuilder Out=new System.Text.StringBuilder();public bool First=true;
        private static string F(float x)=>x.ToString("0.####",System.Globalization.CultureInfo.InvariantCulture);
        public void Piece(string prop,Vector3 at,Quaternion r,float scale,Vector3 tint,Vector3 glow){Out.Append(First?"":",");First=false;Out.Append("[\""+prop+"\","+F(at.X)+","+F(at.Y)+","+F(at.Z)+","+F(r.X)+","+F(r.Y)+","+F(r.Z)+","+F(r.W)+","+F(scale)+","+F(glow.X+glow.Y+glow.Z)+"]");}
        public void Primitive(WeaponShape shape,bool glow,Vector3 at,Quaternion rotation,Vector3 scale,Vector3 color){}
        public void Batch(string prop,ReadOnlySpan<Vector3> at,ReadOnlySpan<Quaternion> turns,float scale,Vector3 tint){}
    }
    private sealed class RecordFx:INativeEntranceFx,INativeEntranceFire
    {
        public readonly System.Text.StringBuilder Out=new System.Text.StringBuilder();public bool First=true;
        private static string F(float x)=>x.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture);
        private void Add(int kind,Vector3 p,float size,Vector3 c,Vector3 a){Out.Append(First?"":",");First=false;Out.Append("["+kind+","+F(p.X)+","+F(p.Y)+","+F(p.Z)+","+F(size)+","+F(c.X)+","+F(c.Y)+","+F(c.Z)+","+F(a.X)+","+F(a.Y)+","+F(a.Z)+"]");}
        public void Glow(Vector3 at,Vector3 color,float coverage,float size,int profile,float u,float seed)=>Add(0,at,size,color,Vector3.Zero);
        public void Streak(Vector3 head,Vector3 velocity,Vector3 color,float width,float minLength,float blur,float taper,float u,float seed){var a=velocity*blur;if(a.Length()<minLength&&a.Length()>1e-6f)a*=minLength/a.Length();Add(1,head,width,color,a);}
        public void Smoke(Vector3 at,Vector3 albedo,float opacity,float size,float u,float seed,float floor)=>Add(2,at,size,albedo*opacity,Vector3.Zero);
        public void Fire(Vector3 at,Vector3 tint,float opacity,float size,float u,float seed,float floor)=>Add(0,at,size,tint*opacity,Vector3.Zero);
    }
    private static void Dump(NativeIntroGenericScript s,string folder)
    {
        string F(float x)=>x.ToString("0.####",System.Globalization.CultureInfo.InvariantCulture);string P(Vector3 v)=>"["+F(v.X)+","+F(v.Y)+","+F(v.Z)+"]";
        var anchors=new[]{NativeEntranceAnchor.Head,NativeEntranceAnchor.Chest,NativeEntranceAnchor.Pelvis,NativeEntranceAnchor.LeftShoulder,NativeEntranceAnchor.LeftElbow,NativeEntranceAnchor.LeftHand,NativeEntranceAnchor.RightShoulder,NativeEntranceAnchor.RightElbow,NativeEntranceAnchor.RightHand,NativeEntranceAnchor.LeftKnee,NativeEntranceAnchor.LeftFoot,NativeEntranceAnchor.RightKnee,NativeEntranceAnchor.RightFoot};
        var sb=new System.Text.StringBuilder();sb.Append("{\"title\":\""+s.Title+"\",\"frames\":[");
        s.Context=new NativeIntroContext{Quality=1,Name="Pilot"};
        for(int i=0;i<=(int)(s.Length*10);i++)
        {
            float age=i/10f,t=s.Clock(age);s.Camera(age,out var v);var rec=new Record();s.Set(t,rec);var rfx=new RecordFx();s.Effects(t,rfx,1);
            sb.Append(i>0?",":"").Append("{\"age\":"+F(age)+",\"t\":"+F(t)+",\"eye\":"+P(v.Eye)+",\"look\":"+P(v.Look)+",\"fov\":"+F(v.Fov)+",\"solid\":"+F(s.Solid(t,0))+",\"anchors\":[");
            for(int k=0;k<anchors.Length;k++)sb.Append(k>0?",":"").Append(P(s.Anchor(anchors[k],t)));
            sb.Append("],\"pieces\":["+rec.Out+"],\"fx\":["+rfx.Out+"]}");
        }
        sb.Append("]}");Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,s.Title.ToLowerInvariant()+".json"),sb.ToString());
    }
    /// <summary>The model's skeleton as drawn (Runtime VisualIntroPose binds a v2 intro's keyed pose bone for bone, its arms as keyed,
    /// the feet unplanted, on the rig's root), with the keyed anchors and the camera, every 1/30 s.</summary>
    private static readonly string[] ModelBones={"pelvis","spine_03","spine_05","neck_01","head","upperarm_l","lowerarm_l","hand_l","middle_01_l","upperarm_r","lowerarm_r","hand_r","middle_01_r","thigh_l","calf_l","foot_l","ball_l","thigh_r","calf_r","foot_r","ball_r"};
    private static void DumpModel(NativeIntroGenericScript s,string folder,string suffix)
    {
        string F(float x)=>x.ToString("0.####",System.Globalization.CultureInfo.InvariantCulture);string P(Vector3 v)=>"["+F(v.X)+","+F(v.Y)+","+F(v.Z)+"]";
        var rig=ModelPoseChecks.Measured();var st=new NativeVisualRig.State(rig);var pose=new CapturedRig.Pose();var at=new int[ModelBones.Length];
        for(int b=0;b<ModelBones.Length;b++)at[b]=Array.IndexOf(rig.Names,ModelBones[b]);
        var anchors=new[]{NativeEntranceAnchor.Head,NativeEntranceAnchor.Chest,NativeEntranceAnchor.Pelvis,NativeEntranceAnchor.LeftShoulder,NativeEntranceAnchor.LeftElbow,NativeEntranceAnchor.LeftHand,NativeEntranceAnchor.RightShoulder,NativeEntranceAnchor.RightElbow,NativeEntranceAnchor.RightHand,NativeEntranceAnchor.LeftKnee,NativeEntranceAnchor.LeftFoot,NativeEntranceAnchor.RightKnee,NativeEntranceAnchor.RightFoot,NativeEntranceAnchor.LeftToe,NativeEntranceAnchor.RightToe};
        var sb=new System.Text.StringBuilder();sb.Append("{\"title\":\""+s.Title+"\",\"length\":"+F(s.Length)+",\"nameAt\":"+F(s.IntroNameAt)+",\"floor\":"+F(NativeEntranceStage.FloorY)+",\"bones\":[\""+string.Join("\",\"",ModelBones)+"\"],\"frames\":[");
        s.Context=new NativeIntroContext{Quality=1,Name="Pilot"};
        for(int i=0;i<=(int)(s.Length*30);i++)
        {
            float age=i/30f,t=s.Clock(age);s.Camera(age,out var v);s.Pose(t,pose,out var root,out var rot);pose.World();
            float relax=Math.Clamp(s.ArmsRelax(t),0,1);NativeVisualRig.Pose(st,pose,new NativeVisualRig.Grasp{Relax=relax,Exact=IntroPoseChecks.Packed(s,t,NativeIntroPack.Arms)},false,false);
            var head=Vector3.Transform(Vector3.UnitZ,Quaternion.Normalize(rot*pose.WorldRotation[CapturedRig.Head]));var chest=Vector3.Transform(Vector3.UnitZ,Quaternion.Normalize(rot*pose.WorldRotation[CapturedRig.Chest]));
            var pelvis=Vector3.Transform(Vector3.UnitZ,Quaternion.Normalize(rot*pose.WorldRotation[CapturedRig.Pelvis]));
            sb.Append(i>0?",":"").Append("{\"age\":"+F(age)+",\"t\":"+F(t)+",\"solid\":"+F(s.Solid(t,0))+",\"root\":"+P(root)+",\"eye\":"+P(v.Eye)+",\"look\":"+P(v.Look)+",\"fov\":"+F(v.Fov)+",\"headFwd\":"+P(head)+",\"chestFwd\":"+P(chest)+",\"pelvisFwd\":"+P(pelvis)+",\"model\":[");
            for(int b=0;b<at.Length;b++)sb.Append(b>0?",":"").Append(P(at[b]<0?Vector3.Zero:root+Vector3.Transform(st.Position[at[b]],rot)));
            sb.Append("],\"keyed\":[");for(int k=0;k<anchors.Length;k++)sb.Append(k>0?",":"").Append(P(s.KeyedAnchor(anchors[k],t)));sb.Append("]}");
        }
        sb.Append("]}");Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,s.Title.ToLowerInvariant()+suffix+"-model.json"),sb.ToString());
    }

    // ---- the feet on the model ----
    /// <summary>
    /// The body as the model draws it (the keyed pose bound bone for bone on the measured skeleton, the feet unplanted, as Runtime
    /// VisualIntroPose binds it), every 1/30 s while it is shown: a foot flat on the floor (its ball within 1.2 cm of it, its ankle
    /// within 13 cm) stays put (its ball under .45 m/s; the walks used to slide a planted foot back at 1 m/s, the model's legs being
    /// 1.7 times the keyed rig's on a shared root) except where the intro slides it (Sliding); and no knee, ankle or ball goes more
    /// than 4 cm into the floor (the kneels sat the hips on the floor with a knee 30 cm through it).
    /// </summary>
    private static void Feet(NativeIntroGenericScript s,NativeVisualRig.Rig rig)
    {
        string n=s.Title;var st=new NativeVisualRig.State(rig);var pose=new CapturedRig.Pose();float floor=NativeEntranceStage.FloorY;
        string[] names={"ball_l","ball_r","foot_l","foot_r","calf_l","calf_r"};float[] under={0,0,.05f,.05f,.055f,.055f};var at=new int[names.Length];
        for(int b=0;b<names.Length;b++){at[b]=Array.IndexOf(rig.Names,names[b]);Check(at[b]>=0,"the measured skeleton has "+names[b]);}
        int frames=(int)(s.Length*30);var p=new Vector3[frames+1,names.Length];var t=new float[frames+1];
        for(int i=0;i<=frames;i++)
        {
            t[i]=s.Clock(i/30f);s.Pose(t[i],pose,out var root,out var rot);pose.World();NativeVisualRig.Pose(st,pose,new NativeVisualRig.Grasp{Relax=Math.Clamp(s.ArmsRelax(t[i]),0,1),Exact=IntroPoseChecks.Packed(s,t[i],NativeIntroPack.Arms)},false,false);
            for(int b=0;b<names.Length;b++)p[i,b]=root+Vector3.Transform(st.Position[at[b]],rot);
        }
        float worst=0,deepest=0;
        for(int i=1;i<frames;i++)
        {
            if(s.Solid(t[i],0)<.5f)continue;
            for(int b=0;b<names.Length;b++){float d=floor+under[b]-p[i,b].Y;deepest=Math.Max(deepest,d);Check(d<.04f,n+": the model's "+names[b]+" is "+(d*100).ToString("0")+" cm into the floor at "+t[i].ToString("0.00")+" s");}
            if(s.Sliding(t[i]))continue;
            for(int side=0;side<2;side++)
            {
                var ball=p[i,side];if(ball.Y-floor>.012f||p[i,2+side].Y-floor>.13f)continue;
                var d=p[i+1,side]-p[i-1,side];d.Y=0;float v=d.Length()*15;worst=Math.Max(worst,v);
                Check(v<.45f,n+": the model's planted "+(side==0?"left":"right")+" foot slides at "+v.ToString("0.00")+" m/s at "+t[i].ToString("0.00")+" s");
            }
        }
        Check(true,n+"'s planted feet on the model: at most "+worst.ToString("0.00")+" m/s; at most "+(deepest*100).ToString("0")+" cm into the floor");
    }

    /// <summary>
    /// Slipstream in its tunnel (the owner: "head in tunnel"; the helmet reached into the curved ceiling as the pilot stood and ran on
    /// the roof): every 1/60 s while the model's head is inside the tunnel (beyond its mouth), the helmet's top (the head bone + .25 m)
    /// stays at least 5 cm under the ceiling over it (NativeIntroSlipstream.TunnelCeiling: the set's lining less its ribs), and the
    /// camera's eye stays inside the lining (under its ceiling, between its walls) while it is in the tunnel.
    /// </summary>
    private static void Tunnel(NativeIntroSlipstream s,NativeVisualRig.Rig rig)
    {
        var st=new NativeVisualRig.State(rig);var pose=new CapturedRig.Pose();int head=Array.IndexOf(rig.Names,"head");Check(head>=0,"the measured skeleton has a head");
        s.Context=new NativeIntroContext{Quality=1,Name="Pilot"};float least=9,eyeLeast=9;int inside=0;
        for(float age=0;age<=s.Length;age+=1/60f)
        {
            float t=s.Clock(age);if(t>=NativeIntroSlipstream.LeapAt)break;
            s.Pose(t,pose,out var root,out var rot);pose.World();NativeVisualRig.Pose(st,pose,new NativeVisualRig.Grasp{Exact=IntroPoseChecks.Packed(s,t,NativeIntroPack.Arms)},false,false);
            var h=root+Vector3.Transform(st.Position[head],rot);
            if(h.X<NativeIntroSlipstream.MouthX)
            {
                inside++;float room=NativeIntroSlipstream.TunnelCeiling(h.Z)-(h.Y+.25f);least=Math.Min(least,room);
                Check(room>=.05f,"Slipstream: the helmet is "+((.05f-room)*100).ToString("0")+" cm too near the tunnel's ceiling at "+t.ToString("0.00")+" s");
            }
            s.Camera(age,out var v);
            if(v.Eye.X<NativeIntroSlipstream.MouthX-.3f)
            {
                float room=NativeIntroSlipstream.TunnelCeiling(v.Eye.Z)-v.Eye.Y;eyeLeast=Math.Min(eyeLeast,room);
                Check(room>.15f&&v.Eye.Z>-5.15f&&v.Eye.Z<-.75f,"Slipstream: the camera leaves the tunnel's lining at "+age.ToString("0.00")+" s ("+v.Eye.Z.ToString("0.00")+", "+v.Eye.Y.ToString("0.00")+")");
            }
        }
        Check(inside>30,"Slipstream rides the tunnel ("+inside+" frames)");
        Check(true,"Slipstream's helmet at least "+(least*100).ToString("0")+" cm under the tunnel's ceiling, the camera "+(eyeLeast*100).ToString("0")+" cm");
    }

    /// <summary>
    /// Slipstream's feet on the moving roof (the body is posed in the car's frame: NativeIntroGenericScript.Carried): every 1/60 s until
    /// the leap, a foot down on the roof (its ball within 1.2 cm of where the feet stand on it, its ankle within 13 cm) stays put on the
    /// roof (its ball under .45 m/s against the car, which runs at 14 m/s), and nothing of the legs goes 4 cm into the roof.
    /// </summary>
    private static void Roof(NativeIntroSlipstream s,NativeVisualRig.Rig rig)
    {
        string n=s.Title+(NativeIntroPack.Local==null?" (its keys alone)":" (the pack's clips)");var st=new NativeVisualRig.State(rig);var pose=new CapturedRig.Pose();
        string[] names={"ball_l","ball_r","foot_l","foot_r","calf_l","calf_r"};float[] under={0,0,.05f,.05f,.055f,.055f};var at=new int[names.Length];
        for(int b=0;b<names.Length;b++)at[b]=Array.IndexOf(rig.Names,names[b]);
        var p=new List<Vector3[]>();var times=new List<float>();
        for(float age=0;;age+=1/60f)
        {
            float t=s.Clock(age);if(t>=NativeIntroSlipstream.LeapAt-.01f)break;
            s.Pose(t,pose,out var root,out var rot);pose.World();NativeVisualRig.Pose(st,pose,new NativeVisualRig.Grasp{Exact=IntroPoseChecks.Packed(s,t,NativeIntroPack.Arms)},false,false);
            // (in the car's frame: the car's travel since 0 taken off)
            var car=new Vector3(NativeIntroSlipstream.CarX(t)-NativeIntroSlipstream.CarX(0),0,0);var f=new Vector3[names.Length];
            for(int b=0;b<names.Length;b++)f[b]=root+Vector3.Transform(st.Position[at[b]],rot)-car;p.Add(f);times.Add(t);
        }
        // (where the feet stand on the roof: the lowest a ball comes while the pilot crouches, before it springs up)
        float top=9;for(int i=0;i<p.Count&&times[i]<NativeIntroSlipstream.StandAt;i++)top=Math.Min(top,Math.Min(p[i][0].Y,p[i][1].Y));
        Check(Math.Abs(top-NativeIntroSlipstream.RoofY)<.06f,n+": the feet stand on the roof ("+top.ToString("0.000")+" m, the roof "+NativeIntroSlipstream.RoofY+")");
        float worst=0,deepest=0;int down=0;
        for(int i=1;i+1<p.Count;i++)
        {
            for(int b=0;b<names.Length;b++){float d=top+under[b]-p[i][b].Y;deepest=Math.Max(deepest,d);Check(d<.04f,n+": the model's "+names[b]+" is "+(d*100).ToString("0")+" cm into the roof at "+times[i].ToString("0.00")+" s");}
            for(int side=0;side<2;side++)
            {
                if(p[i][side].Y-top>.012f||p[i][2+side].Y-top>.13f)continue;down++;
                var d=p[i+1][side]-p[i-1][side];d.Y=0;float dt=times[i+1]-times[i-1];float v=dt>1e-5f?d.Length()/dt:0;worst=Math.Max(worst,v);
                Check(v<.45f,n+": the model's "+(side==0?"left":"right")+" foot slides on the roof at "+v.ToString("0.00")+" m/s at "+times[i].ToString("0.00")+" s");
            }
        }
        Check(down>60,n+": its feet are down on the roof ("+down+" foot-frames)");
        Check(true,n+"'s feet on the moving roof: at most "+worst.ToString("0.00")+" m/s against it, at most "+(deepest*100).ToString("0")+" cm into it");
    }
    /// <summary>Slipstream's timing is the local bank's clips' (with the bank): the sprint at Run m/s, a footfall every Pace s (the left
    /// foot down at the clip's start, the right at Pace into it), the take-off's left foot down at its start and both up by .1 s, the
    /// slide's right foot coming down at SlideDown.</summary>
    private static void Clips(NativeIntroSlipstream s)
    {
        var bank=NativeIntroPack.Local;bool Has(string c,out NativeIntroPack.Clip clip){bool ok=bank.TryGet(c,out clip);Check(ok,"the local motion bank has Slipstream's "+c);return ok;}
        const float frame=1/60f;
        if(Has(NativeIntroSlipstream.Dash,out var dash))
        {
            Check(Math.Abs(dash.Speed-NativeIntroSlipstream.Run)<.02f*NativeIntroSlipstream.Run,"Slipstream's sprint runs at its clip's speed ("+dash.Speed.ToString("0.00")+" m/s, keyed "+NativeIntroSlipstream.Run+")");
            Check(Math.Abs(dash.Length*.5f-NativeIntroSlipstream.Pace)<frame,"Slipstream's stride is its clip's ("+(dash.Length*.5f).ToString("0.000")+" s)");
            Check(dash.OnFloor(.5f*frame,true,true)&&!dash.OnFloor(dash.Length-1.5f*frame,true,true),"the sprint clip's left foot comes down at its start");
            Check(dash.OnFloor(NativeIntroSlipstream.Pace+.5f*frame,true,false)&&!dash.OnFloor(NativeIntroSlipstream.Pace-1.5f*frame,true,false),"the sprint clip's right foot comes down at Pace");
        }
        if(Has(NativeIntroSlipstream.TakeOff,out var off))Check(off.OnFloor(.5f*frame,false,true)&&!off.OnFloor(.1f,false,true)&&!off.OnFloor(.1f,false,false),"the take-off clip's left foot is down at its start and both are up at .1 s");
        if(Has(NativeIntroSlipstream.SlideClip,out var slide))Check(slide.OnFloor(NativeIntroSlipstream.SlideDown+.5f*frame,false,false)&&!slide.OnFloor(NativeIntroSlipstream.SlideDown-1.5f*frame,false,false),"the slide clip's right foot comes down at SlideDown");
    }

    // ---- the registry and the timeline ----
    private static void Registry(NativeIntroGenericScript s)
    {
        var e=NativeIntroCatalog.Find(s.Style);string n=s.Title;
        Check(e!=null,n+" is registered");
        Check(e.Name==n,n+"'s Locker name is its title ("+e.Name+")");
        Check(Math.Abs(e.Duration-s.Length)<1e-4f&&Math.Abs(e.NameAt-s.IntroNameAt)<1e-4f,n+": the registry's length and name beat are the script's");
        Check(s.Length>=6&&s.Length<=8.5f,n+" lasts 6-8.5 s ("+s.Length+")");
        Check(s.IntroNameAt>3&&s.IntroNameAt<s.Length-.8f,n+"'s name beat is clear of its end ("+s.IntroNameAt+")");
        Check(s.Ready&&s.SetBank==NativeIntroGenericScript.Bank,n+" is ready and reads the generic bank");
        Check(e.Kind==(s is NativeIntroCloudbreak?NativeIntroKind.Default:NativeIntroKind.Generic),n+" is listed as "+(s is NativeIntroCloudbreak?"the default":"a generic intro"));
        Check(NativeIntroRules.Duration(s.Style)==e.Duration&&NativeIntroRules.NameAt(s.Style)==e.NameAt,n+": the match reads the registry's timing");
    }
    private static void Timeline(NativeIntroGenericScript s,string root)
    {
        string n=s.Title,slug=n.ToLowerInvariant();var cues=s.Cues;float last=-1;
        Check(cues.Length>=6,n+" has a cue timeline");
        string clips=Path.Combine(root,"Assets/Conversion/Resources/Audio/audio");
        var keys=new HashSet<string>();foreach(var d in NativeSoundKeys.All)if(d.Space!=NativeSoundSpace.Loop)keys.Add(d.Key);
        // A stand-in plays only from a filled one-shot entry: the bank refuses a loop, and a key the installed bank leaves empty (the
        // audio inventory's "Unpopulated registry key" rows) drops the cue to its built-in clip (I5's finding, 2026-11-20).
        string inventory=Path.Combine(root,"../../docs/audio/audio-inventory-2026-11-16.csv");
        if(File.Exists(inventory))foreach(var line in File.ReadLines(inventory))
            if(line.StartsWith("Unpopulated registry key,")){var f=line.Split(',');if(f.Length>2)keys.Remove(f[2]);}
        foreach(var c in cues)
        {
            Check(c.At>=last&&c.At>=0&&c.At<=s.Length,n+": cue "+c.Key+" is in order inside the intro");last=c.At;
            Check(c.Key!=null&&c.Key.StartsWith("intro2."+slug+"."),n+": cue "+c.Key+" is named for its intro");
            Check(c.Alt==null||keys.Contains(c.Alt),n+": cue "+c.Key+"'s stand-in "+c.Alt+" is a filled one-shot effects-bank key");
            Check(c.Clip==null||File.Exists(Path.Combine(clips,c.Clip+".mp3"))||File.Exists(Path.Combine(clips,c.Clip+".wav"))||File.Exists(Path.Combine(clips,c.Clip+".ogg")),n+": cue "+c.Key+"'s built-in clip "+c.Clip+" ships");
            if(c.Alt==null&&c.Clip==null)Check(c.Key=="intro2.flashbulb.cheer",n+": only the crowd's cheer waits silent for its generated take ("+c.Key+")");
            Check(c.Gain>0&&c.Gain<=1.2f&&c.Rate>.4f&&c.Rate<2,n+": cue "+c.Key+" has a sane gain and rate");
        }
        Check(Array.Exists(cues,c=>Math.Abs(c.At-s.IntroNameAt)<.02f),n+" has a cue on its name beat");
        // The clock: ramps repay before the name, so the name lands on its age.
        Check(Math.Abs(s.Clock(s.IntroNameAt)-s.IntroNameAt)<.01f,n+": the clock is back on time at the name ("+s.Clock(s.IntroNameAt)+")");
        float prev=-1;for(float a=0;a<=s.Length+.5f;a+=1/120f){float t=s.Clock(a);Check(t>=prev-1e-5f,n+": the clock never runs back");prev=t;}
    }

    // The name in a band under the feet: from the card's arrival to the end, on a 16:9 screen, the whole figure (the crown .3 m over
    // the head anchor, the skull's base; the soles .06 m under the foot anchors) sits above the kicker's top (the name centred at
    // card.Y, .075 of the height tall in a box 1.2 times that, the kicker 24 px above it at 1080) and below the top letterbox.
    private static void Closing(NativeIntroGenericScript s)
    {
        string n=s.Title;var card=default(NativeIntroCardState);s.Context=new NativeIntroContext{Quality=1,Name="Pilot"};
        float kicker=NativeIntroGenericScript.CardY-.6f*.075f-24f/1080f,worst=0,crown=1;
        for(float age=s.IntroNameAt;age<=s.Length-.25f;age+=1/30f)
        {
            float t=s.Clock(age);s.Card(t,ref card);if(!card.Shown)continue;
            Check(Math.Abs(card.Y-NativeIntroGenericScript.CardY)<1e-4f,n+": the name sits in the band under the feet");
            s.Camera(age,out var v);
            float Y(Vector3 p)
            {
                var f=Vector3.Normalize(v.Look-v.Eye);var r=Vector3.Normalize(Vector3.Cross(f,Vector3.UnitY));var u=Vector3.Cross(r,f);
                var d=p-v.Eye;float z=Vector3.Dot(d,f);if(z<.05f)return 2;
                return .5f-.5f*Vector3.Dot(d,u)/(z*(float)Math.Tan(v.Fov*Math.PI/360));
            }
            var head=s.Anchor(NativeEntranceAnchor.Head,t);
            float soles=Math.Max(Y(s.Anchor(NativeEntranceAnchor.LeftFoot,t)-new Vector3(0,.06f,0)),Y(s.Anchor(NativeEntranceAnchor.RightFoot,t)-new Vector3(0,.06f,0)));
            worst=Math.Max(worst,soles);crown=Math.Min(crown,Y(head+new Vector3(0,.3f,0)));
        }
        Check(worst<kicker-.02f,n+": the closing frame's soles ("+worst.ToString("0.000")+") sit above the name's band ("+kicker.ToString("0.000")+")");
        Check(crown>.07f,n+": the closing frame keeps the crown ("+crown.ToString("0.000")+") under the top letterbox");
    }

    // ---- fakes ----
    private sealed class Fx:INativeEntranceFx,INativeEntranceFire
    {
        public int Calls;public double Sum;public float Light;public bool Bad;public readonly List<(Vector3 head,Vector3 axis)> Long=new List<(Vector3,Vector3)>(64);public bool Record;
        public void Reset(){Calls=0;Sum=0;Light=0;Bad=false;Long.Clear();}
        private void Fin(Vector3 v,float a=0){if(!float.IsFinite(v.X)||!float.IsFinite(v.Y)||!float.IsFinite(v.Z)||!float.IsFinite(a))Bad=true;}
        public void Glow(Vector3 at,Vector3 color,float coverage,float size,int profile,float u,float seed){Calls++;Fin(at,size);Fin(color);Sum+=at.X*.31+at.Y*.17+at.Z*.13+color.X+size;Light+=(color.X+color.Y+color.Z)*size*size;}
        public void Streak(Vector3 head,Vector3 velocity,Vector3 color,float width,float minLength,float blur,float taper,float u,float seed)
        {Calls++;Fin(head,width);Fin(velocity);Sum+=head.X*.23+head.Z*.11+velocity.Y*.07+color.Y+width;var axis=velocity*blur;if(Record&&axis.Length()>1.05f)Long.Add((head,axis));Light+=(color.X+color.Y+color.Z)*width*Math.Max(minLength,axis.Length());}
        public void Smoke(Vector3 at,Vector3 albedo,float opacity,float size,float u,float seed,float floor){Calls++;Fin(at,size);Sum+=at.Y*.19+opacity+size;}
        public void Fire(Vector3 at,Vector3 tint,float opacity,float size,float u,float seed,float floor){Calls++;Fin(at,size);Sum+=at.X+opacity;}
    }
    private sealed class Set:INativeIntroSet
    {
        public bool Bank;public int Pieces,Primitives;public double Sum;public readonly HashSet<string> Missing=new HashSet<string>();
        public bool Ready=>Bank;
        public bool Has(string prop)=>Bank;
        public void Piece(string prop,Vector3 at,Quaternion rotation,float scale,Vector3 tint,Vector3 glow){Pieces++;Sum+=at.X*.7+at.Y*.3+at.Z*.11+rotation.W+scale+glow.X+glow.Y+tint.Z;if(prop==null||!prop.Contains("/"))Missing.Add(prop??"null");}
        public void Primitive(WeaponShape shape,bool glow,Vector3 at,Quaternion rotation,Vector3 scale,Vector3 color){Primitives++;Sum+=at.Y+scale.X+color.X;}
        public int Copies;
        public void Batch(string prop,ReadOnlySpan<Vector3> at,ReadOnlySpan<Quaternion> turns,float scale,Vector3 tint){Copies+=at.Length;for(int i=0;i<at.Length;i++)Sum+=at[i].X*.01+at[i].Y*.02;}
        public void Reset(){Pieces=0;Primitives=0;Copies=0;Sum=0;}
    }
    private sealed class Props:INativeEntranceProps
    {
        public int Count;
        public bool Model(string id,Vector3 position,Quaternion rotation,float scale,Vector3 tint){Count++;return false;}
        public void Piece(WeaponShape shape,bool glow,Vector3 position,Quaternion rotation,Vector3 scale,Vector3 color){Count++;}
    }
    private static readonly CapturedRig.Pose pose=new CapturedRig.Pose();
    private static readonly NativeEntranceLights lights=new NativeEntranceLights();
    private static readonly Fx fx=new Fx();private static readonly Set set=new Set();private static readonly Props props=new Props();
    /// <summary>One whole frame at match age `age`; a checksum of everything it drew.</summary>
    private static double Frame(NativeIntroGenericScript s,float age,bool bank,float quality,bool low,bool reduced)
    {
        s.Context=new NativeIntroContext{Reduced=reduced,Low=low,Quality=quality,Name="Pilot"};
        float t=s.Clock(age);double sum=0;
        s.Pose(t,pose,out var root,out var rot);sum+=root.X+root.Y*3+root.Z*5+rot.W;for(int b=0;b<22;b+=3)sum+=pose.Rotation[b].X+pose.Rotation[b].Y;
        s.Camera(age,out var view);sum+=view.Eye.X+view.Eye.Y*2+view.Look.Z+view.Fov;
        s.Lights(t,lights);sum+=lights.Points+lights.Fog.X+lights.FogFar*.01;for(int i=0;i<lights.Points;i++)sum+=lights.Point[i].R+lights.Point[i].X;
        props.Count=0;s.Props(t,props,quality);sum+=props.Count;
        set.Bank=bank;set.Reset();s.Set(t,set);sum+=set.Sum+set.Pieces*7+set.Primitives*3;
        fx.Reset();s.Effects(t,fx,quality);sum+=fx.Sum+fx.Calls;
        var card=new NativeIntroCardState();s.Card(t,ref card);sum+=card.Age+card.Exit+(card.Shown?1:0);
        s.Lens(t,out var lens);sum+=lens.Letterbox+lens.Black;
        return sum;
    }
    private static void Determinism(NativeIntroGenericScript s)
    {
        string n=s.Title;var a=Fresh(s);var b=Fresh(s);
        int frames=(int)(s.Length*30)+3;var order=new int[frames];for(int i=0;i<frames;i++)order[i]=i;
        var rng=new Random(7);for(int i=frames-1;i>0;i--){int j=rng.Next(i+1);(order[i],order[j])=(order[j],order[i]);}
        var seq=new double[frames];for(int i=0;i<frames;i++)seq[i]=Frame(a,i/30f,true,1,false,false);
        for(int k=0;k<frames;k++){int i=order[k];double x=Frame(b,i/30f,true,1,false,false);Check(Math.Abs(x-seq[i])<1e-3*Math.Max(1,Math.Abs(x)),n+" draws the same frame at "+(i/30f).ToString("0.000")+" s whatever was drawn before (a scrub)");}
        // Finite, and no allocation per frame once warm.
        for(float age=0;age<=s.Length;age+=1/60f){Frame(a,age,true,1,false,false);Check(!fx.Bad&&NativeIntroMotion.Finite(pose),n+" draws only finite values at "+age.ToString("0.00"));}
        for(int pass=0;pass<2;pass++)
        {
            long before=GC.GetAllocatedBytesForCurrentThread();
            for(float age=0;age<=s.Length+.4f;age+=1/60f){Frame(a,age,pass==0,1,false,false);Frame(a,age,true,.3f,true,true);}
            long used=GC.GetAllocatedBytesForCurrentThread()-before;if(pass==1)Check(used==0,n+" allocates nothing per frame ("+used+" bytes)");
        }
    }
    private static NativeIntroGenericScript Fresh(NativeIntroGenericScript s)=>(NativeIntroGenericScript)Activator.CreateInstance(s.GetType());
    private static void Budgets(NativeIntroGenericScript s)
    {
        string n=s.Title;int fxMax=0,fxLow=0,pieces=0,prims=0,points=0,voidPrims=0;
        for(float age=0;age<=s.Length;age+=1/60f)
        {
            Frame(s,age,true,1,false,false);fxMax=Math.Max(fxMax,fx.Calls);pieces=Math.Max(pieces,set.Pieces);prims=Math.Max(prims,set.Primitives+props.Count);points=Math.Max(points,lights.Points);
            Check(set.Missing.Count==0,n+" names its bank's props in its own folder");
            Frame(s,age,true,.3f,true,false);fxLow=Math.Max(fxLow,fx.Calls);
            Frame(s,age,false,1,false,false);voidPrims=Math.Max(voidPrims,set.Primitives+props.Count);Check(set.Pieces==0,n+" draws no bank piece without the bank");
        }
        Check(fxMax<=600,n+": at most 600 effects a frame ("+fxMax+")");Check(fxLow<=300,n+": at most 300 effects a frame on Low ("+fxLow+")");
        Check(pieces>0&&pieces<=80,n+": 1-80 set pieces a frame ("+pieces+")");Check(prims<=120&&voidPrims<=120,n+": at most 120 primitives ("+prims+", "+voidPrims+" without the bank)");
        Check(voidPrims>0,n+" draws a set without its bank (the plain fallback, never blocks)");Check(points<=24,n+": at most 24 point lights ("+points+")");
    }
    /// <summary>In Reduced effects nothing flashes faster than 3 Hz: the lights' and the glows' totals, sampled at 60 Hz, have no two
    /// prominent peaks closer than 1/3 s.</summary>
    private static void Reduced(NativeIntroGenericScript s)
    {
        string n=s.Title;int frames=(int)(s.Length*60);var light=new float[frames];var glow=new float[frames];
        for(int i=0;i<frames;i++)
        {
            Frame(s,i/60f,true,1,false,true);float l=0;for(int k=0;k<lights.Points;k++){var p=lights.Point[k];l+=(float)(p.R+p.G+p.B);}light[i]=l;glow[i]=fx.Light;
        }
        Peaks(n+"'s lights",light);Peaks(n+"'s glows",glow);
    }
    private static void Peaks(string what,float[] v)
    {
        float max=0;foreach(var x in v)max=Math.Max(max,x);if(max<=0)return;int lastPeak=-1000;
        for(int i=1;i<v.Length-1;i++)
        {
            if(!(v[i]>=v[i-1]&&v[i]>v[i+1]))continue;
            float left=v[i],right=v[i];for(int k=1;k<=30&&i-k>=0;k++)left=Math.Min(left,v[i-k]);for(int k=1;k<=30&&i+k<v.Length;k++)right=Math.Min(right,v[i+k]);
            float prominence=v[i]-Math.Max(left,right);if(prominence<.25f*max)continue;
            Check(i-lastPeak>=20,what+" flash no faster than 3 Hz in Reduced effects (peaks at "+(lastPeak/60f).ToString("0.00")+" and "+(i/60f).ToString("0.00")+" s)");lastPeak=i;
        }
    }
    /// <summary>Every streak longer than 1 m lies on its head's camera plane (within 2 degrees), so its ends stay on its line.</summary>
    private static void Streaks(NativeIntroGenericScript s)
    {
        string n=s.Title;int seen=0;
        foreach(bool reduced in new[]{false,true})
        for(float age=0;age<=s.Length;age+=1/30f)
        {
            fx.Record=true;Frame(s,age,true,1,false,reduced);fx.Record=false;
            s.Camera(age,out var view);var f=Vector3.Normalize(view.Look-view.Eye);
            foreach(var (head,axis) in fx.Long)
            {
                seen++;float zh=Vector3.Dot(head-view.Eye,f);if(zh<.25f)continue;
                // the tail's depth along the camera's forward equals the head's
                float off=Math.Abs(Vector3.Dot(axis,f))/axis.Length();Check(off<(float)Math.Sin(2*Math.PI/180),n+": a "+axis.Length().ToString("0.0")+" m streak at "+age.ToString("0.00")+" s lies on its head's camera plane ("+off+")");
            }
        }
        Check(true,n+" laid "+seen+" long streaks on their camera planes");
    }
}
