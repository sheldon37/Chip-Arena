using System;
using System.Numerics;
using ChipArena.UnityConversion;

// The seven melee weapon intros (docs/INTROS-MELEE.md; owner 2026-11-20: "a much higher quality standard, great animations and effects"; then
// "we dont need the matches own annimation system ... they have unique animations"): each on its weapon's slot, and, on the model as the
// runtime binds it (Tests/IntroPoseChecks' binding, the grip's grasp included), the contacts their stories make: no drawn weapon through the
// drawn head or torso; the great sword's point in the floor at the blow and resting on it at the end; the Katana's left hand on the saya as
// drawn and its blade along the saya while it is sheathed; the Fangs' feet on the lintel until they leap; the Glaive's ring leaving the
// drawn hand and coming back to it, not drawn in the hand while it flies, and the orrery's rings never through the floor. IntroV2Checks and
// IntroPoseChecks hold the rest (determinism, budgets, cues, the limits). Presentation only.
internal static class IntroMeleeChecks
{
    private static int count;
    private static void Check(bool ok,string what){if(!ok)throw new Exception("Melee intros: "+what);count++;}
    private static readonly (string spec,Type type,string title)[] Slots=
    {
        ("sword",typeof(NativeIntroSeveredDawn),NativeIntroSeveredDawn.Title),("daggers",typeof(NativeIntroAfterimage),NativeIntroAfterimage.Title),
        ("shield",typeof(NativeIntroHoldTheLine),NativeIntroHoldTheLine.Title),("greatsword",typeof(NativeIntroFaultline),NativeIntroFaultline.Title),
        (NativeKatana.Spec,typeof(NativeIntroStillRain),NativeIntroStillRain.Title),(NativeGlaive.Spec,typeof(NativeIntroOrrery),NativeIntroOrrery.Title),
        (NativeFangs.Spec,typeof(NativeIntroApex),NativeIntroApex.Title),
    };
    public static void Run(string root)
    {
        count=0;var rig=ModelPoseChecks.Measured();
        foreach(var (spec,type,title) in Slots)
        {
            var e=NativeIntroCatalog.Find(NativeIntroCatalog.WeaponPrefix+spec);
            Check(e!=null&&e.Kind==NativeIntroKind.Weapon&&e.Spec==spec&&e.Author=="I3"&&e.Name==title,spec+" has its melee intro registered ("+title+")");
            var s=e.Make();Check(s.GetType()==type,spec+"'s intro is "+type.Name);
            Clear(e,s,rig);
        }
        Faultline(rig);StillRain(rig);Apex(rig);Orrery(rig);Afterimage();
        Console.WriteLine("PASS melee intros ("+count+" checks: seven on their slots; on the model no drawn weapon through the head or torso, the great sword's point in the floor, the saya in the left hand, the Fangs on the lintel, Afterimage's walk on its legs and torso only, the Glaive's ring out of the hand and back, the orrery's rings over the floor)");
    }

    // ---- Afterimage's walk: an everyday pack walk over the keyed walk (docs/INTROS-V2.md 9d), its legs and torso only (the daggers keyed) ----
    private static void Afterimage()
    {
        var s=(NativeIntroAfterimage)NativeIntroCatalog.Find(NativeIntroCatalog.WeaponPrefix+"daggers").Make();var g=s.Walk;
        Check(g!=null&&g.Clip==NativeIntroAfterimage.WalkClip,"Afterimage's walk is its pack segment");
        Check(g.From==0&&g.To<=NativeIntroAfterimage.StopAt&&g.Loop&&g.Root==NativeIntroPack.RootMode.Path,"Afterimage's walk plays on the lane until the stop");
        Check(g.Mask==(NativeIntroPack.Legs|NativeIntroPack.Torso)&&g.Body==NativeIntroPack.BodyTurn.Clip,"Afterimage's walk takes the legs and the torso, never the arms (the daggers stay keyed)");
        // (with the local melee bank: the clip at its rate walks at the lane's pace, so the drawn feet hold)
        if(NativeIntroPack.Local==null)IntroPackChecks.LoadLocal(IntroPackChecks.Root(),out _);
        if(NativeIntroPack.Local!=null&&NativeIntroPack.Local.TryGet(NativeIntroAfterimage.WalkClip,out var c))
            Check(Math.Abs(c.Speed*NativeIntroAfterimage.WalkRate-NativeIntroAfterimage.LanePace)<.1f,"Afterimage's walk clip at its rate keeps the lane's pace ("+(c.Speed*NativeIntroAfterimage.WalkRate).ToString("0.00")+" against "+NativeIntroAfterimage.LanePace.ToString("0.00")+" m/s)");
    }

    // ---- the body as the runtime binds it, and the drawn weapon on it ----
    private sealed class Bound
    {
        public readonly NativeVisualRig.Rig Rig;public readonly NativeVisualRig.State St;public readonly CapturedRig.Pose Pose=new CapturedRig.Pose();public Vector3 Root;public Quaternion Rot;
        public Bound(NativeVisualRig.Rig rig){Rig=rig;St=new NativeVisualRig.State(rig);}
        public Vector3 Bone(string name){int i=Array.IndexOf(Rig.Names,name);return i<0?Vector3.Zero:Root+Vector3.Transform(St.Position[i],Rot);}
    }
    private static void Bind(NativeEntranceScript s,string spec,float t,Bound b)
    {
        s.Pose(t,b.Pose,out b.Root,out b.Rot);b.Pose.World();
        var grasp=new NativeVisualRig.Grasp{Exact=IntroPoseChecks.Packed(s,t,NativeIntroPack.Arms)};
        if(s is INativeIntroGrip g&&g.GripAt(t,out var at)&&at.Weight>.001f)NativeIntroGrip.Grasp(spec,at,b.Pose,ref grasp);
        NativeVisualRig.Pose(b.St,b.Pose,grasp,false,false);
    }
    /// <summary>The drawn weapon in hand `left` (world): its grip and its far end (the drawn grip anchor on the keyed hand's turn).</summary>
    private static bool Weapon(Bound b,string spec,bool left,out Vector3 grip,out Vector3 tip)
    {
        grip=tip=default;if(!NativeIntroWeapon.Holds(spec,left)||!NativeIntroWeapon.Tip(spec,left,out var far))return false;
        var turn=b.Pose.WorldRotation[left?CapturedRig.LHand:CapturedRig.RHand];var anchor=left?b.St.LeftAnchor:b.St.RightAnchor;
        grip=b.Root+Vector3.Transform(anchor,b.Rot);tip=b.Root+Vector3.Transform(anchor+Vector3.Transform(far,turn),b.Rot);return true;
    }
    private static float SegSeg(Vector3 p0,Vector3 p1,Vector3 q0,Vector3 q1)
    {
        var d1=p1-p0;var d2=q1-q0;var r=p0-q0;float a=Vector3.Dot(d1,d1),e=Vector3.Dot(d2,d2),f=Vector3.Dot(d2,r),s,t;
        if(a<1e-9f&&e<1e-9f)return r.Length();
        if(a<1e-9f){s=0;t=Math.Clamp(f/e,0,1);}
        else{float c=Vector3.Dot(d1,r);if(e<1e-9f){t=0;s=Math.Clamp(-c/a,0,1);}else{float bb=Vector3.Dot(d1,d2),den=a*e-bb*bb;s=den>1e-9f?Math.Clamp((bb*f-c*e)/den,0,1):0;t=(bb*s+f)/e;if(t<0){t=0;s=Math.Clamp(-c/a,0,1);}else if(t>1){t=1;s=Math.Clamp((bb-c)/a,0,1);}}}
        return Vector3.Distance(p0+d1*s,q0+d2*t);
    }

    // ---- no drawn weapon through the drawn head or torso ----
    /// <summary>Every 1/30 s while the weapon is drawn in the hand (not sheathed or flying), the drawn weapon's line past the fist (from 8 cm
    /// along it to its far end) keeps out of the head (a sphere of 12 cm round its centre) and the torso (a capsule of 13 cm from the pelvis
    /// to the neck), within 3 cm. The Katana's blade lying along its saya in the sheathing is the saya's, not the body's.</summary>
    private static void Clear(NativeIntroEntry e,NativeEntranceScript s,NativeVisualRig.Rig rig)
    {
        var b=new Bound(rig);var blade=s as NativeBladeIntro;float worst=0,worstAt=0;string where="";
        for(int i=0;i<=e.Duration*30;i++)
        {
            float t=s.Clock(i/30f);if(blade!=null&&blade.Sheathed(t))continue;
            if(s is NativeIntroStillRain&&t>=NativeIntroStillRain.SheatheAt&&t<NativeIntroStillRain.ClickAt)continue;
            Bind(s,e.Spec,t,b);var head=b.Bone("head")+new Vector3(0,.07f,0);var pelvis=b.Bone("pelvis");var neck=b.Bone("neck_01");
            for(int side=0;side<2;side++)
            {
                if(!Weapon(b,e.Spec,side==1,out var grip,out var tip))continue;var d=tip-grip;float l=d.Length();if(l<.1f)continue;var from=grip+d*(.08f/l);
                float h=.12f-SegSeg(from,tip,head,head),c=.13f-SegSeg(from,tip,pelvis,neck);
                if(h>worst){worst=h;worstAt=t;where="head";}if(c>worst){worst=c;worstAt=t;where="torso";}
            }
        }
        Check(worst<=.03f,e.Id+": the drawn weapon keeps out of the drawn "+where+" (worst "+(worst*100).ToString("0")+" cm into it at "+worstAt.ToString("0.00")+" s)");
    }

    // ---- Faultline: the point in the floor at the blow; the point resting on the floor at the end ----
    private static void Faultline(NativeVisualRig.Rig rig)
    {
        var s=new NativeIntroFaultline();var b=new Bound(rig);float floor=NativeEntranceStage.FloorY;
        for(float t=NativeIntroFaultline.SlamAt+.1f;t<NativeIntroFaultline.HeadUpAt;t+=.1f)
        {
            Bind(s,"greatsword",t,b);Weapon(b,"greatsword",false,out _,out var tip);
            Check(tip.Y<floor+.06f&&tip.Y>floor-.45f,"Faultline's drawn point is in the floor at the blow ("+(tip.Y-floor).ToString("0.00")+" m at "+t.ToString("0.00")+" s)");
        }
        for(float t=NativeIntroFaultline.StandAt+.3f;t<NativeIntroFaultline.Total;t+=.25f)
        {
            Bind(s,"greatsword",t,b);Weapon(b,"greatsword",false,out _,out var tip);
            Check(Math.Abs(tip.Y-floor)<.12f,"Faultline's drawn point rests on the floor at the end ("+(tip.Y-floor).ToString("0.00")+" m at "+t.ToString("0.00")+" s)");
        }
    }

    // ---- Still Rain: the left hand on the saya as drawn; the blade along it in the sheathing ----
    private static void StillRain(NativeVisualRig.Rig rig)
    {
        var s=new NativeIntroStillRain();var b=new Bound(rig);
        for(float t=0;t<NativeIntroStillRain.Total;t+=.1f)
        {
            if(t>=NativeIntroStillRain.RaiseAt-.1f&&t<NativeIntroStillRain.ChiburiAt+.05f)continue;// (both hands on the hilt for the cut down)
            Bind(s,NativeKatana.Spec,t,b);Saya(b,out var mouth,out var axis);var hand=b.Bone("hand_l");var d=hand-mouth;float along=Vector3.Dot(d,axis),off=(d-axis*along).Length();
            Check(off<.14f&&along>-.12f&&along<.28f,"Still Rain's drawn left hand keeps the saya at its mouth ("+(off*100).ToString("0")+" cm off its line, "+(along*100).ToString("0")+" cm along it at "+t.ToString("0.00")+" s)");
        }
        for(float t=NativeIntroStillRain.SheatheAt+.25f;t<NativeIntroStillRain.ClickAt;t+=.1f)
        {
            Bind(s,NativeKatana.Spec,t,b);Saya(b,out _,out var axis);Weapon(b,NativeKatana.Spec,false,out var grip,out var tip);
            float ang=(float)(Math.Acos(Math.Clamp(Vector3.Dot(Vector3.Normalize(tip-grip),axis),-1,1))*57.29578);
            Check(ang<12,"Still Rain's drawn blade lies along the saya as it goes home ("+ang.ToString("0")+" degrees off at "+t.ToString("0.00")+" s)");
        }
    }
    /// <summary>The saya's mouth and axis on the drawn pelvis (Runtime NativeBladePresentation.KatanaSaya: 11 cm left, 13 up, 15 ahead, 48
    /// degrees under level along the left thigh), the pelvis's heading from the drawn hips.</summary>
    private static void Saya(Bound b,out Vector3 mouth,out Vector3 axis)
    {
        var pelvis=b.Bone("pelvis");var turn=Quaternion.Normalize(b.Rot*b.Pose.WorldRotation[CapturedRig.Pelvis]);
        mouth=pelvis+Vector3.Transform(new Vector3(.11f,.13f,.15f),turn);axis=Vector3.Transform(Vector3.Normalize(new Vector3(.25f,-.74f,-.62f)),turn);
    }

    // ---- Apex: the feet on the lintel's top until they push off ----
    private static void Apex(NativeVisualRig.Rig rig)
    {
        var s=new NativeIntroApex();var b=new Bound(rig);float top=NativeEntranceStage.FloorY+NativeIntroApex.Perch.Y;
        for(float t=0;t<NativeIntroApex.LeapAt-.1f;t+=.1f)
        {
            Bind(s,NativeFangs.Spec,t,b);
            foreach(var ball in new[]{"ball_l","ball_r"}){float y=b.Bone(ball).Y-top;Check(y>-.04f&&y<.06f,"Apex's drawn "+ball+" stands on the lintel's top ("+(y*100).ToString("0")+" cm at "+t.ToString("0.00")+" s)");}
        }
    }

    // ---- Orrery: the ring out of the hand and back; the rings over the floor ----
    private static void Orrery(NativeVisualRig.Rig rig)
    {
        var s=new NativeIntroOrrery();var b=new Bound(rig);string spec=NativeGlaive.Spec;
        Check(!s.Sheathed(NativeIntroOrrery.ReleaseAt-.02f)&&s.Sheathed(NativeIntroOrrery.ReleaseAt+.02f)&&s.Sheathed(NativeIntroOrrery.CatchAt-.02f)&&!s.Sheathed(NativeIntroOrrery.CatchAt+.02f),
            "Orrery's held ring is not drawn while it flies, and is from the catch");
        for(float t=0;t<NativeIntroOrrery.Total;t+=.05f){bool flies=NativeIntroOrrery.Flight(t,out _,out _);Check(flies==s.Sheathed(t),"Orrery's ring flies exactly while it is out of the hand ("+t.ToString("0.00")+" s)");}
        // (the flight leaves where the drawn ring is at the release and comes back to where it is drawn at the catch: the ring's middle)
        foreach(var (at,start) in new[]{(NativeIntroOrrery.ReleaseAt,true),(NativeIntroOrrery.CatchAt,false)})
        {
            Bind(s,spec,at,b);Weapon(b,spec,false,out var grip,out _);var turn=b.Pose.WorldRotation[CapturedRig.RHand];
            var middle=grip+Vector3.Transform(Vector3.Transform(new Vector3(0,0,NativeWeaponModels.GlaiveCentre),turn),b.Rot);
            NativeIntroOrrery.Flight(start?at+1e-4f:at-1e-4f,out var ring,out _);float d=Vector3.Distance(ring,middle);
            Check(d<.2f,"Orrery's ring "+(start?"leaves the drawn hand":"comes back to the drawn hand")+" ("+(d*100).ToString("0")+" cm from the drawn ring's middle)");
        }
        for(float t=0;t<NativeIntroOrrery.Total;t+=1/30f)for(int k=0;k<4;k++){float low=NativeIntroOrrery.RingLowest(k,t);Check(low>NativeEntranceStage.FloorY+.05f,"Orrery's ring "+k+" keeps over the floor ("+(low-NativeEntranceStage.FloorY).ToString("0.00")+" m at "+t.ToString("0.00")+" s)");}
    }
}
