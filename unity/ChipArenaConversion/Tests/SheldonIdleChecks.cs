using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using ChipArena.UnityConversion;

// The DEV Sheldon Idle lobby pose (Claude; docs/CYBER-LORD.md "Sheldon Idle"; Core/NativeLobbyPoses.cs SheldonIdle):
//  1. the picker and the gate: a fifth pick after the source's four, listed and kept only with live DEV access (the Sheldon
//     Head's gate), unknown ids still 'weapon'; the host seat, the account load and the guests' frames take it through the
//     gate or the normaliser; the match never reads the pick;
//  2. the pose for many classes: the Cyber Lord stance's body, twitches and planted feet joint for joint; an armed pilot's
//     weapon on its own hand, the hand reaching its hold, two-handed weapons' support hand on the grip, the free hand the
//     stance's fist, the weapon clear of the podium and riding the twitches (smaller with reduced effects); the fighters and
//     the Cyber Lord in fists; the glitch the Cyber Lord's only; a Cyber Lord's standing pose is this stance;
//  3. determinism and no allocation.
// Pixels are checked by the offline renders; nothing here runs Unity.
internal static class SheldonIdleChecks
{
    private static int count;
    private static void Check(bool ok,string what){if(!ok)throw new Exception("Sheldon Idle: "+what);count++;}
    private static NativeAccess Access(bool developer,double expires,string grant=null)
    {
        var a=new NativeAccess();var t=typeof(NativeAccess);t.GetProperty("Developer").SetValue(a,developer);t.GetProperty("Expires").SetValue(a,expires);
        if(grant!=null)((HashSet<string>)t.GetField("grants",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(a)).Add(grant);
        return a;
    }
    private static readonly int[] BodyJoints={CapturedRig.Body,CapturedRig.Pelvis,CapturedRig.Spine,CapturedRig.Chest,CapturedRig.Neck,CapturedRig.Head,
        CapturedRig.LHip,CapturedRig.LKnee,CapturedRig.LFoot,CapturedRig.LToe,CapturedRig.RHip,CapturedRig.RKnee,CapturedRig.RFoot,CapturedRig.RToe};
    private static float Diff(MeleeRig a,MeleeRig b,int[] joints)
    {
        float d=Vector3.Distance(a.P[CapturedRig.Body],b.P[CapturedRig.Body]);
        foreach(int j in joints)d=Math.Max(d,Math.Max(Vector3.Distance(a.P[j],b.P[j]),1-Math.Abs(Quaternion.Dot(a.Q[j],b.Q[j]))));
        return d;
    }
    private static int[] Arm(bool left)=>left?new[]{CapturedRig.LClav,CapturedRig.LArm,CapturedRig.LElbow,CapturedRig.LHand}:new[]{CapturedRig.RClav,CapturedRig.RArm,CapturedRig.RElbow,CapturedRig.RHand};
    // Where a spec's hands hold: the weapon hand(s), whether the off hand is a support grip on the weapon, which hand stays free.
    private static bool TwoHand(string spec)=>spec=="greatsword"||spec=="rifle"||spec=="machinegun";
    private static int Free(string spec)=>spec=="daggers"||TwoHand(spec)?-1:NativeLobbyPoses.Magic(spec)||NativeBowWire.Is(spec)?CapturedRig.RHand:CapturedRig.LHand;

    public static IEnumerable<string> Run(string root)
    {
        // ------------------------------------------------------------------------------------------------------------------
        // 1. The picker and the gate
        // ------------------------------------------------------------------------------------------------------------------
        int c0=count;const string Id=NativeLobbyPoses.SheldonKey;const double Now=1000;
        Check(Id=="sheldon-idle"&&NativeCosmeticGate.SheldonIdle==Id&&NativeLobbyPoses.SheldonIdle==NativeLobbyPoses.Keys.Length,"the id, after the source's poses");
        Check(NativeLobbyPoses.Keys.SequenceEqual(new[]{"weapon","chair","sleep","crossed"})&&NativeLobbyPoses.Labels.Length==4&&NativeLobbyPoses.Icons.Length==4&&NativeLocker.PoseTexts.Length==4,"the source's four poses unchanged (LobbyPoseChecks pins them to lobby-presentation.ts)");
        Check(NativeLobbyPoses.Count(false)==4&&NativeLobbyPoses.Count(true)==5&&NativeLobbyPoses.Key(4)==Id&&NativeLobbyPoses.Label(4)=="Sheldon Idle"&&NativeLobbyPoses.Icon(4)=="activity"&&NativeLobbyPoses.Key(1)=="chair"&&NativeLobbyPoses.Label(3)=="Arms crossed","the picker: the four, then Sheldon Idle");
        Check(NativeLocker.PoseText(4).Length>0&&NativeLocker.PoseText(5)==""&&NativeLocker.DevItem(Id)&&NativeCosmeticGate.DevOnly(Id)&&NativeCosmeticGate.Restricted(Id),"its card text and DEV badge, DEV-only like the Sheldon Head");
        Check(NativeLobbyPoses.Index(Id)==4&&NativeLobbyPoses.Normalize(Id)==Id&&NativeLobbyPoses.Normalize("sheldon")=="weapon"&&NativeLobbyPoses.Normalize("crossed-arms")=="weapon"&&NativeLobbyPoses.Normalize(null)=="weapon"&&NativeLobbyPoses.Normalize("sleep")=="sleep","the normaliser knows it; unknown ids fall back to 'weapon'");
        Check(NativeLobbyPoses.Effective("siren",Id)==NativeLobbyPoses.Weapon&&NativeLobbyPoses.Effective("pistol",Id)==4&&!NativeLobbyPoses.UsesIdle("emperor",4)&&!NativeLobbyPoses.UsesIdle("siren",4),"the Siren still stands as herself; nobody samples a baked idle under it");
        var guest=NativeAccess.Guest;var dev=Access(true,1e9);var granted=Access(false,1e9,"pose:"+Id);var lapsed=Access(true,500);
        Check(NativeCosmeticGate.Pose(guest,Id,Now)=="weapon"&&NativeCosmeticGate.Pose(null,Id,Now)=="weapon"&&NativeCosmeticGate.Pose(granted,Id,Now)=="weapon"&&NativeCosmeticGate.Pose(lapsed,Id,Now)=="weapon","without live DEV access (a guest, a grant, lapsed access) it is 'weapon'");
        Check(NativeCosmeticGate.Pose(dev,Id,Now)==Id&&NativeCosmeticGate.Pose(dev,"bogus",Now)=="weapon"&&NativeCosmeticGate.Pose(guest,"crossed",Now)=="crossed"&&NativeCosmeticGate.Pose(guest,null,Now)=="weapon","DEV access keeps it; the other poses pass as before");
        Check(NativeLobbyPoses.Count(NativeCosmeticGate.Allowed(guest,"pose",Id,Now))==4&&NativeLobbyPoses.Count(NativeCosmeticGate.Allowed(granted,"pose",Id,Now))==4&&NativeLobbyPoses.Count(NativeCosmeticGate.Allowed(dev,"pose",Id,Now))==5,"the Locker lists it only with live DEV access");
        var ui=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeLockerUI.cs"));var profile=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeProfileUI.cs"));
        var network=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeNetwork.cs"));var frame=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeNetworkFrame.cs"));
        Check(ui.Contains("lockerPoses=NativeLobbyPoses.Count(NativeCosmeticRules.Allowed(access,\"pose\",NativeLobbyPoses.SheldonKey));")&&ui.Contains("LkReserve(LkGrid(lockerW,lockerPoses,LkRowMin,LkRowH));")&&ui.Contains("for(int i=0;i<lockerPoses;i++)")
            &&ui.Contains("NativeLobbyPoses.Label(i),NativeLocker.PoseText(i),null,out bool hot,-1,NativeLocker.DevItem(id))&&!on)prefs.lobbyPose=id;")&&!ui.Contains("NativeLobbyPoses.Keys.Length"),"the Locker's IDLE POSE cards draw the gated count, with the DEV badge");
        Check(profile.Contains("public static string Pose(string value,NativeAccess a)=>NativeCosmeticGate.Pose(a,value,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());")&&profile.Contains("prefs.lobbyPose=NativeCosmeticRules.Pose(c.lobbyPose,account.Access);"),"an account load keeps it only with DEV access");
        Check(network.Contains("lobbyPose=NativeCosmeticRules.Pose(c.lobbyPose,access),")&&!network.Contains("NativeLobbyPoses.Normalize(c.lobbyPose)"),"the host gates a joining seat's pick with its verified access");
        Check(frame.Contains("seat.lobbyPose=NativeLobbyPoses.Normalize(sf.lobbyPose);")&&frame.Contains("s.lobbyPose!=null&&s.lobbyPose.Length>32"),"guests take the host's gated pick through the normaliser (the frame's length cap holds it)");
        foreach(var file in new[]{"Assets/ChipArena/Core/CyberLordMotion.cs","Assets/ChipArena/Core/CyberLordIdle.cs","Assets/ChipArena/Core/MeleeMotion.cs","Assets/ChipArena/Runtime/CapturedAnimation.cs","Assets/ChipArena/Runtime/NativeCyberLordPresentation.cs"})
        {var src=File.ReadAllText(Path.Combine(root,file));Check(!src.Contains("lobbyPose")&&!src.Contains("SheldonKey")&&!src.Contains("NativeLobbyPoses.SheldonIdle"),"the match never reads the pick ("+Path.GetFileName(file)+")");}
        yield return $"Sheldon Idle picker: {count-c0} checks (a fifth, DEV-only idle pose after the source's four; the Sheldon Head's gate on the Locker list, the account load and the host seat; unknown ids 'weapon'; never read by the match)";

        // ------------------------------------------------------------------------------------------------------------------
        // 2. The pose for many classes
        // ------------------------------------------------------------------------------------------------------------------
        c0=count;
        string[] specs={"sword","greatsword","daggers","shield","pistol","rifle","machinegun","fire","water","lightning","darkness","leet","bow","emperor","ansatsuken","cyberlord","mystery"};
        var cyber=new MeleeRig();var r=new MeleeRig();var again=new MeleeRig();var pose=new CapturedRig.Pose();
        float podium=NativePortraitRules.GroupY-(NativePortraitRules.PodiumY+NativePortraitRules.PodiumHeight/2);float worstClear=float.MaxValue,worstSupport=0,worstReach=0;string worstSpec="";
        int armed=0;
        foreach(var spec in specs)
        {
            bool grips=NativeLobbyPoses.Grips(spec),fists=!grips,cyberlord=spec==CyberLordSpec.Id;if(grips)armed++;
            var mesh=grips?NativeWeaponModels.Build(spec,NativeWeaponModels.Full):null;if(grips)Check(mesh!=null&&mesh.Sections.Count>0,spec+": a Standard model to hold");
            foreach(bool reduced in new[]{false,true})
            {
                var range=new Vector3[2];range[0]=new Vector3(float.MaxValue);range[1]=new Vector3(float.MinValue);float jump=0,fistJump=0;Vector3 last=Vector3.Zero,lastFist=Vector3.Zero;bool first=true;
                for(float t=0;t<45;t+=1/30f)
                {
                    const int seed=7;
                    // The reference: the Cyber Lord's stance itself (its glitch's tear and jolt only for the Cyber Lord).
                    var cg=CyberLordIdlePose.Stand(cyber,t,seed,reduced,cyberlord,out _);
                    NativeLobbyPoses.Sample(r,spec,NativeLobbyPoses.SheldonIdle,t,"none",reduced,seed,out var w,out var o,out var g);string at=spec+(reduced?" reduced":"")+" @"+t.ToString("0.00");
                    Check(Diff(r,cyber,BodyJoints)<1e-5f,at+": the Cyber Lord stance's body, twitches and planted feet");
                    if(cyberlord)Check(g.Visible==cg.Visible&&g.LClench==cg.LClench&&g.RClench==cg.RClench&&g.Slice==cg.Slice&&g.Split==cg.Split&&g.Scan==cg.Scan,at+": the Cyber Lord's glitch and clench");
                    else Check(!g.Visible&&g.LClench==0&&g.RClench==0&&g.Slice==0,at+": no glitch on anyone else");
                    Check(w.Visible==(!cyberlord)&&o.Visible==(!cyberlord&&NativeLobbyPoses.OffHand(spec)),at+": the weapon shows as standing");
                    if(w.Visible)Check(w.Bone==NativeLobbyPoses.HeldBone(spec)&&(!o.Visible||o.Bone==CapturedRig.LHand),at+": held on its own hand");
                    if(fists){Check(Diff(r,cyber,Arm(true))<1e-5f&&Diff(r,cyber,Arm(false))<1e-5f,at+": the stance's fists");continue;}
                    int free=Free(spec);if(free>=0)Check(Diff(r,cyber,Arm(free==CapturedRig.LHand))<1e-5f,at+": the free hand keeps the stance's fist");
                    // The weapon hand reaches its hold (never stretched to the arm's clamp); the support hand sits on the grip.
                    int hand=w.Bone;float reach=Vector3.Distance(r.ChainPoint(hand,-1),r.ChainPoint(hand==CapturedRig.LHand?CapturedRig.LArm:CapturedRig.RArm,-1));worstReach=Math.Max(worstReach,reach);
                    Check(reach<.512f,at+": the weapon hand reaches its hold ("+reach.ToString("0.000")+" m)");
                    if(spec=="daggers"){float lr=Vector3.Distance(r.ChainPoint(CapturedRig.LHand,-1),r.ChainPoint(CapturedRig.LArm,-1));Check(lr<.512f,at+": the off dagger's hand reaches its hold");}
                    if(TwoHand(spec))
                    {
                        var grip=spec=="greatsword"?MeleeRig.SupportGrip:NativeLobbyPoses.GunSupportGrip;var point=r.ChainPoint(CapturedRig.RHand,-1)+Vector3.Transform(r.Reached(grip),r.ChainQuat(CapturedRig.RHand,-1));
                        float gap=Vector3.Distance(point,r.ChainPoint(CapturedRig.LHand,-1));worstSupport=Math.Max(worstSupport,gap);Check(gap<.02f,at+": the support hand on the grip ("+(gap*100).ToString("0.0")+" cm)");
                    }
                    // The weapon clear of the podium (every vertex of the Standard model on the hand(s) it is drawn on).
                    r.WritePose(pose);pose.World();float low=float.MaxValue;
                    foreach(var s in mesh.Sections)
                    {
                        var held=s.Bone==w.Bone?w:o.Visible&&s.Bone==o.Bone?o:default;if(!held.Visible)continue;var q=pose.WorldRotation[s.Bone];var p=pose.WorldPosition[s.Bone];
                        foreach(var v in s.Positions)low=Math.Min(low,(p+Vector3.Transform(held.Position+Vector3.Transform(v,held.Rotation),q)).Y+podium);
                    }
                    Check(low>.02f,at+": the weapon clear of the podium ("+(low*100).ToString("0.0")+" cm)");if(low<worstClear){worstClear=low;worstSpec=spec;}
                    var wrist=r.ChainPoint(hand,-1);var fist=cyber.ChainPoint(hand,-1);range[0]=Vector3.Min(range[0],wrist);range[1]=Vector3.Max(range[1],wrist);
                    if(!first){jump=Math.Max(jump,Vector3.Distance(wrist,last));fistJump=Math.Max(fistJump,Vector3.Distance(fist,lastFist));}last=wrist;lastFist=fist;first=false;
                    if(t==0)foreach(int s in new[]{3,99}){NativeLobbyPoses.Sample(again,spec,NativeLobbyPoses.SheldonIdle,0,"none",reduced,s,out _,out _,out _);Check(Diff(again,r,BodyJoints)<1e-6f&&Diff(again,r,Arm(true))<1e-6f&&Diff(again,r,Arm(false))<1e-6f,spec+": portrait time 0 (reduced motion) holds still for any pilot");}
                }
                if(fists)continue;
                float span=(range[1]-range[0]).Length();
                Check(span>.02f,spec+(reduced?" reduced":"")+": the weapon rides the twitches ("+(span*100).ToString("0.0")+" cm)");Check(jump<1.5f*fistJump+.01f,spec+(reduced?" reduced":"")+": the weapon snaps no harder than the stance's fist does ("+(jump*100).ToString("0.0")+" cm a frame at most against "+(fistJump*100).ToString("0.0")+")");
                if(!reduced)fullSpan[spec]=span;else Check(span<fullSpan[spec],spec+": reduced effects move the weapon less ("+(span*100).ToString("0.0")+" against "+(fullSpan[spec]*100).ToString("0.0")+" cm)");
            }
            // Deterministic: a fresh rig gives the same pose.
            foreach(float t in new[]{.4f,7.3f,21.9f})
            {
                NativeLobbyPoses.Sample(r,spec,NativeLobbyPoses.SheldonIdle,t,"none",false,42,out _,out _,out _);var fresh=new MeleeRig();NativeLobbyPoses.Sample(fresh,spec,NativeLobbyPoses.SheldonIdle,t,"none",false,42,out _,out _,out _);
                Check(Diff(r,fresh,BodyJoints)<1e-6f&&Diff(r,fresh,Arm(true))<1e-6f&&Diff(r,fresh,Arm(false))<1e-6f,spec+": deterministic on a fresh rig @"+t);
            }
        }
        // The Cyber Lord's standing pose is the Sheldon Idle (its default), and the plain Sample (seed 0) is the same stance.
        foreach(float t in new[]{0f,1.3f,8.8f})
        {
            NativeLobbyPoses.Sample(r,CyberLordSpec.Id,NativeLobbyPoses.Weapon,t,"none",false,5,out var cw,out _,out var cg1);NativeLobbyPoses.Sample(cyber,CyberLordSpec.Id,NativeLobbyPoses.SheldonIdle,t,"none",false,5,out _,out _,out var cg2);
            Check(Diff(r,cyber,BodyJoints)<1e-6f&&Diff(r,cyber,Arm(true))<1e-6f&&Diff(r,cyber,Arm(false))<1e-6f&&cg1.Visible==cg2.Visible&&!cw.Visible,"a Cyber Lord stands in the Sheldon Idle by default @"+t);
            NativeLobbyPoses.Sample(r,"rifle",NativeLobbyPoses.SheldonIdle,t,out var pw,out _);NativeLobbyPoses.Sample(again,"rifle",NativeLobbyPoses.SheldonIdle,t,"none",false,0,out _,out _,out _);
            Check(pw.Visible&&Diff(r,again,BodyJoints)<1e-6f&&Diff(r,again,Arm(false))<1e-6f,"the plain Sample is the seed-0 stance @"+t);
        }
        yield return $"Sheldon Idle pose: {count-c0} checks ({specs.Length} classes, {armed} armed, 45 s full and reduced: the Cyber Lord stance's body and twitches joint for joint, the weapon on its own hand, the hand reaching its hold (at most {worstReach:0.000} m), two-handed support grips within {worstSupport*100:0.0} cm, the weapon at least {worstClear*100:0.0} cm above the podium ({worstSpec}), riding the twitches, less with reduced effects; fists for the fighters and the Cyber Lord; the glitch the Cyber Lord's only; deterministic)";

        // ------------------------------------------------------------------------------------------------------------------
        // 3. No allocation
        // ------------------------------------------------------------------------------------------------------------------
        c0=count;
        {
            void Frame(float t){foreach(var spec in specs)NativeLobbyPoses.Sample(r,spec,NativeLobbyPoses.SheldonIdle,t,"none",t%2>1,9,out _,out _,out _);NativeLobbyPoses.Normalize(Id);NativeLobbyPoses.Index("crossed");}
            for(float t=0;t<1;t+=1/30f)Frame(t);
            long before=GC.GetAllocatedBytesForCurrentThread();for(float t=1;t<4;t+=1/30f)Frame(t);long used=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(used==0,"no allocation per frame ("+used+" bytes)");
        }
        yield return $"Sheldon Idle allocation: {count-c0} checks (every class's pose and the normaliser allocate nothing per frame; the gate runs only on a seat or a load)";
    }
    private static readonly Dictionary<string,float> fullSpan=new Dictionary<string,float>();
}
