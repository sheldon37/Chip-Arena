using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using ChipArena.UnityConversion;

/// <summary>
/// Felt hits, blood that stays and melee kills that slice (owner 2026-10-12): the stagger's hip cap and recovery, planted
/// feet that never slide (standing and while the authority pushes the pilot back), continuity under chained hits (a machine
/// gun, a sword combo, the Whirlwind's pulses), the splat pool's cap, fade and reset, droplets that land on the floor and a
/// wall, the per-target droplet budget, blade blood, the slice plane following the captured swing and the attacker's yaw,
/// the sever rates per weapon against the old wound-only presentation, and presentation keyed only to replicated data.
/// </summary>
internal static class HitFeelChecks
{
    private const float Dt=1/60f,D=(float)Math.PI/180;
    private static void Check(bool ok,string what){if(!ok)throw new InvalidOperationException("Hit feel: "+what);}
    public static void Run(string root,ConversionContent data)
    {
        var log=new List<string>();
        Recovery(log);Feet(log);Chains(log);Splats(log);Drops(log);Budget(log);Blade(log);Plane(log);Severs(log);Replicated(root,log);
        foreach(var line in log)Console.WriteLine("PASS hit feel: "+line);
    }

    // ---- a struck pilot on the captured-melee or gun route whose root the authority moves ----
    internal sealed class Rig
    {
        public readonly Actor A;public readonly HitVictim Kind;public readonly NativeHitLayer Layer=new NativeHitLayer();public NativeHitLedger Ledger;
        readonly MeleeMotion melee=new MeleeMotion();readonly GunMotion gun=new GunMotion();readonly MeleeMotion scratch=new MeleeMotion();readonly MeleeView view=new MeleeView();
        public readonly CapturedRig.Pose Pose=new CapturedRig.Pose();readonly NativeJuggleTrack juggle=new NativeJuggleTrack();
        public Vector3 Root;public float GroupYaw;
        public Rig(string spec,NativeHitLedger ledger)
        {
            var w=spec=="pistol"?WeaponId.Pistol:spec=="rifle"?WeaponId.Rifle:spec=="machinegun"?WeaponId.MachineGun:WeaponId.Sword;
            A=new Actor{Id=1,SourceSpec=spec,Weapon=w,Grounded=true,Hp=100,CastRate=1,VisualSet=spec=="daggers"?1:spec=="greatsword"?2:spec=="shield"?3:0,Ammo=w==WeaponId.Sword?0:GunRules.Get(w).Magazine};
            Kind=NativePoseRoute.For(A,false)==PoseRoute.Gun?HitVictim.Gun:HitVictim.Melee;Ledger=ledger;
        }
        void View(float t)
        {
            var a=A;var v=view;v.Id="1";v.Spec=a.SourceSpec;v.Yaw=-a.Yaw*D;v.Vel=new Vector3(-a.Velocity.x,a.Velocity.y,a.Velocity.z);v.Grounded=a.Grounded;v.Hp=a.Hp;
            v.LastAttack=-100;v.Windup=.14f;v.AttackRate=1;v.AttackIndex=1;v.SwingAt=v.SwingUntil=float.NaN;v.Commit=false;v.BlockTime=0;v.LastBlock=-100;v.HitStunUntil=a.StunUntil;v.RollAt=v.RollUntil=-100;v.ActionKind=null;
            v.Reaction=a.ReactionAt>-90;v.Parry=a.ReactionParry;v.ReactionAt=a.ReactionAt;v.ReactionUntil=a.ReactionUntil;v.ReactionYaw=a.ReactionYaw;v.ReactionTurn=a.ReactionTurn;v.ReactionStrength=a.ReactionStrength;
            v.ReactionDir=new Vector3(a.ReactionDir.x,a.ReactionDir.y,a.ReactionDir.z);v.ReactionPoint=new Vector3(a.ReactionPoint.x,a.ReactionPoint.y,a.ReactionPoint.z);v.ReactionZone=a.ReactionZone;v.Down=false;v.Juggle=false;
        }
        public void Step(float t,float dt)
        {
            // The rig root in the source frame (x mirrored), as CapturedAnimation places it.
            var group=new Vector3(-A.Position.x,A.Position.y+.865f,A.Position.z);Root=group;
            if(Kind==HitVictim.Melee)
            {
                View(t);melee.Rig.GroupPosition=group;melee.Rig.GroupYaw=melee.PresentedYaw(view,t,view.Yaw);GroupYaw=melee.Rig.GroupYaw;
                Layer.Bind(melee,A,Ledger,HitVictim.Melee);melee.Sample(view,t,dt);melee.Rig.WritePose(Pose);Pose.World();return;
            }
            gun.Rig.GroupPosition=group;gun.Rig.GroupYaw=-A.Yaw*D;GroupYaw=gun.Rig.GroupYaw;gun.Pose(A,t,Pose,false,juggle,out bool react);
            if(react){View(t);scratch.ReactOver(Pose,view,t);}
            if(Ledger!=null)Layer.Over(scratch.Rig,Pose,A,Ledger,HitVictim.Gun,t,gun.Rig.GroupYaw,gun.Rig.GroupPosition);gun.Finish(Pose,t);if(Ledger!=null)Layer.Replant(scratch.Rig,Pose);
        }
        /// <summary>A bone in the world (source frame).</summary>
        public Vector3 World(int bone)=>Vector3.Transform(Pose.WorldPosition[bone],MeleeMath.AxisAngle(MeleeMath.Y,GroupYaw))+Root;
    }
    private static int id;
    // A blow on the victim from `attacker` standing at `angle` degrees (0 front), its reaction and stun as NativeArena records
    // them for a melee or direct hit, and the frame's Hit event.
    private static void Strike(Rig r,string attacker,float angle,float at,float stun,float damage,string detail="direct",bool reaction=true)
    {
        float a=angle*D;var body=new Vector3(r.A.Position.x,.865f,r.A.Position.z);var from=new Vector3(-(float)Math.Sin(a),0,(float)Math.Cos(a))*1.6f+body;var dir=Vector3.Normalize(body-from);
        if(reaction)
        {
            var h=MeleeReaction.Hit(new V3(body.X,body.Y,body.Z),r.A.Yaw,new V3(dir.X,dir.Y,dir.Z),new V3(body.X,body.Y+.1f,body.Z),at,damage,false);
            var v=r.A;v.ReactionAt=h.At;v.ReactionUntil=Math.Max(h.Until,v.StunUntil);v.ReactionYaw=h.Yaw;v.ReactionTurn=h.Turn;v.ReactionStrength=h.Strength;v.ReactionDir=new V3(h.Direction.X,h.Direction.Y,h.Direction.Z);v.ReactionZone=h.Zone;v.ReactionParry=false;
            if(stun>0)v.StunUntil=Math.Max(v.StunUntil,at+stun);
        }
        var foe=new Actor{Id=2,SourceSpec=attacker,Position=new V3(from.X,0,from.Z),Hp=100,Grounded=true,CommitYaw=angle+180,Combo=0,SwingAt=at-.05f,SwingUntil=at+.1f};
        r.Ledger.Observe(new Snapshot{Actors=new[]{r.A,foe},Events=new[]{new ArenaEvent{Id=++id,Kind=EventKind.Hit,Source=2,Target=1,Time=at,Amount=damage,Detail=detail,Position=new V3(body.X,body.Y,body.Z)}}});
    }

    private static void Recovery(List<string> log)
    {
        // One hit of each weight on a still pilot: the hips' peak, and how soon they are back.
        var lines=new List<string>();
        foreach(var (attacker,dmg,stun,name) in new[]{("pistol",23f,0f,"pistol flinch"),("rifle",58f,0f,"rifle step"),("sword",30f,.18f,"sword step"),("greatsword",52f,.28f,"greatsword heavy")})
        {
            foreach(var victim in new[]{"sword","rifle"})
            {
                var r=new Rig(victim,new NativeHitLedger());float peak=0,peakAt=0,back=float.NaN;bool struck=false;
                for(float t=1;t<3;t+=Dt)
                {
                    if(!struck&&t>=2-1e-4f){struck=true;Strike(r,attacker,0,t,stun,dmg,reaction:attacker!="pistol"&&attacker!="rifle"||true);}
                    r.Step(t,Dt);float s=r.Layer.Stagger.HipShift;if(s>peak){peak=s;peakAt=t-2;}
                    if(struck&&float.IsNaN(back)&&peak>0&&t-2>peakAt&&s<peak*.15f)back=t-2;
                }
                var w=NativeStaggerRules.Classify(attacker,"direct",dmg);float want=NativeStaggerRules.Peak(w);
                Check(peak<=NativeStaggerRules.HardHip&&peak>want*.6f&&peak<want*1.35f+.01f,name+" on "+victim+": the hips peak at "+peak+" m (rule "+want+")");
                Check(!float.IsNaN(back)&&back<=.52f,name+" on "+victim+": the hips are back (under 15% of the peak) within .52 s ("+back+")");
                if(victim=="sword")lines.Add(name+" "+(peak*100).ToString("0.0")+" cm at "+(peakAt*1000).ToString("0")+" ms, back by "+(back*1000).ToString("0")+" ms");
            }
        }
        // Any string of hits: never past the hard bound.
        var rnd=new Random(41);float worst=0;
        for(int run=0;run<20;run++)
        {
            var r=new Rig(rnd.Next(2)==0?"sword":"rifle",new NativeHitLedger());string[] specs={"machinegun","greatsword","rifle","sword","pistol"};
            for(float t=1;t<3.5f;t+=Dt){if(rnd.NextDouble()<.12)Strike(r,specs[rnd.Next(specs.Length)],(float)rnd.NextDouble()*360,t,rnd.NextDouble()<.3?.2f:0,(float)rnd.NextDouble()*60+5);r.Step(t,Dt);worst=Math.Max(worst,r.Layer.Stagger.HipShift);}
        }
        Check(worst<=NativeStaggerRules.HardHip+1e-4f,"random strings of hits never shift the hips past "+NativeStaggerRules.HardHip+" m ("+worst+")");
        log.Add("hip shift by weight ("+string.Join("; ",lines)+"), at most "+(worst*100).ToString("0.0")+" cm over 20 random strings (hard bound "+(NativeStaggerRules.HardHip*100).ToString("0")+" cm)");
    }

    // The world speed of each foot while it is on the floor (both frames planted), over a struck pilot's frames.
    private static float Slip(Rig r,Vector3[] last,bool[] had,out float lift)
    {
        float worst=0;lift=0;
        for(int i=0;i<2;i++)
        {
            int bone=i==0?CapturedRig.LFoot:CapturedRig.RFoot;var w=r.World(bone);float y=r.Pose.WorldPosition[bone].Y;bool down=y<=MeleeRig.FLOOR+.002f;
            if(had[i]&&down&&last[i].Y<=MeleeRig.FLOOR+.002f)worst=Math.Max(worst,new Vector2(w.X-last[i].X,w.Z-last[i].Z).Length()/Dt);
            lift=Math.Max(lift,y-MeleeRig.FLOOR);last[i]=new Vector3(w.X,down?MeleeRig.FLOOR:y,w.Z);had[i]=true;
        }
        return worst;
    }
    private static void Feet(List<string> log)
    {
        var lines=new List<string>();
        // A still pilot, and one the authority pushes back (1.5 m/s decaying as NativeMeleeRules.External does, 3.1/s): the
        // planted feet never slide over the floor; the stagger's steps lift and plant.
        foreach(var victim in new[]{"sword","greatsword","rifle"})foreach(float push in new[]{0f,1.5f})foreach(var (attacker,dmg,stun) in new[]{("sword",30f,.18f),("greatsword",52f,.28f),("rifle",58f,0f)})
        {
            var r=new Rig(victim,new NativeHitLedger());var last=new Vector3[2];var had=new bool[2];float slip=0,lift=0;int steps=0;bool struck=false;float pos=0,v=0;
            for(float t=1;t<3.2f;t+=Dt)
            {
                if(!struck&&t>=2-1e-4f){struck=true;Strike(r,attacker,0,t,stun,dmg);v=push;}
                if(struck){pos-=v*Dt;v*=(float)Math.Exp(-3.1f*Dt);}r.A.Position=new V3(0,0,pos);r.A.Velocity=new V3(0,0,-v);
                r.Step(t,Dt);if(t<2)continue;slip=Math.Max(slip,Slip(r,last,had,out float l));lift=Math.Max(lift,l);steps=Math.Max(steps,r.Layer.Stagger.Steps);
            }
            Check(slip<.03f,victim+" hit by "+attacker+(push>0?" while pushed back":"")+": a planted foot slips "+slip+" m/s");
            if(attacker!="rifle"||victim=="rifle")lines.Add(victim+"/"+attacker+(push>0?" pushed":"")+" "+(slip*1000).ToString("0.0")+" mm/s, lift "+(lift*100).ToString("0")+" cm");
            if(attacker=="greatsword")Check(lift>.03f,victim+" hit by the greatsword"+(push>0?" while pushed":"")+": steps lift the foot ("+lift+" m)");
        }
        // A shove faster than steps can follow (a sword's 4.6 m/s, a greatsword's 8.6): the planted feet skid only with the
        // push's excess over NativeStagger.SkidSpeed and the legs never stretch past their reach.
        foreach(var (attacker,dmg,stun,push) in new[]{("sword",30f,.18f,4.6f),("greatsword",52f,.28f,8.6f)})
        {
            var r=new Rig("sword",new NativeHitLedger());var last=new Vector3[2];var had=new bool[2];float slip=0,excess=0,leg=0;bool struck=false;float pos=0,v=0;
            for(float t=1;t<3;t+=Dt)
            {
                if(!struck&&t>=2-1e-4f){struck=true;Strike(r,attacker,0,t,stun,dmg);v=push;}
                if(struck){pos-=v*Dt;v*=(float)Math.Exp(-3.1f*Dt);}r.A.Position=new V3(0,0,pos);r.A.Velocity=new V3(0,0,-v);r.Step(t,Dt);if(t<2)continue;
                bool owned=r.Layer.Stagger.OwnsFeet;float s=Slip(r,last,had,out _);if(owned){slip=Math.Max(slip,s);excess=Math.Max(excess,s-Math.Max(0,v-NativeStagger.SkidSpeed));}
                for(int i=0;i<2;i++)leg=Math.Max(leg,Vector3.Distance(r.Pose.WorldPosition[i==0?CapturedRig.LHip:CapturedRig.RHip],r.Pose.WorldPosition[i==0?CapturedRig.LFoot:CapturedRig.RFoot]));
            }
            Check(excess<.12f&&leg<=.5f,attacker+" shove ("+push+" m/s): planted feet skid at most "+excess+" m/s past the push's excess over "+NativeStagger.SkidSpeed+" m/s; legs reach at most "+leg+" m");
            lines.Add(attacker+" shove "+push+" m/s: skid with the excess (worst "+(slip).ToString("0.00")+" m/s), legs <= "+(leg*100).ToString("0")+" cm");
        }
        log.Add("planted feet never slide (under .03 m/s) unless the authority shoves faster than steps follow: "+string.Join("; ",lines));
    }
    private static void Chains(List<string> log)
    {
        // Rapid strings (a machine gun at 12 Hz, a sword combo, the Whirlwind's seven .45 s pulses): no frame jumps more than a
        // single hit's own first frames do, the hips stay bounded and the steps are few.
        var lines=new List<string>();
        foreach(var (victim,attacker,dmg,every,count,detail,stun) in new[]{("rifle","machinegun",8f,.085f,14,"direct",0f),("sword","sword",30f,.5f,3,"direct",.18f),("sword","greatsword",8.5f,.45f,7,"ability",0f),("rifle","greatsword",8.5f,.45f,7,"ability",0f)})
        {
            float refJump=0,worstJump=0,peak=0;int stepStarts=0;
            // The same blow alone (its first frames' crisp kick), then the string.
            for(int pass=0;pass<2;pass++)
            {
                int n=pass==0?1:count;var r=new Rig(victim,new NativeHitLedger());int hits=0;bool wasStep=false,has=false;Vector3 lastHip=Vector3.Zero;float jump=0;
                for(float t=1;t<5;t+=Dt)
                {
                    if(hits<n&&t>=2+hits*every-1e-4f){Strike(r,attacker,10,t,hits==0?stun:0,dmg,detail,reaction:detail=="direct");hits++;}
                    r.Step(t,Dt);var hip=r.World(CapturedRig.Body);if(has)jump=Math.Max(jump,(hip-lastHip).Length());lastHip=hip;has=true;
                    if(pass==1){peak=Math.Max(peak,r.Layer.Stagger.HipShift);bool step=r.Layer.Stagger.Steps>0;if(step&&!wasStep)stepStarts++;wasStep=step;}
                }
                if(pass==0)refJump=jump;else worstJump=jump;
            }
            Check(peak<=NativeStaggerRules.HardHip&&worstJump<=refJump*1.25f+.002f,victim+" under "+count+" "+attacker+" hits: hips at most "+peak+" m, no frame moves them more than "+worstJump+" m (one hit alone: "+refJump+")");
            Check(stepStarts<=Math.Max(3,count-1),victim+" under "+count+" "+attacker+" hits: "+stepStarts+" steps (chained, not a step per hit)");
            lines.Add(count+" x "+attacker+(detail=="ability"?" whirlwind":"")+" on "+victim+": hips <= "+(peak*100).ToString("0.0")+" cm, frame jump <= "+(worstJump*1000).ToString("0")+" mm (one hit "+(refJump*1000).ToString("0")+"), "+stepStarts+" steps");
        }
        log.Add("chained hits stay continuous: "+string.Join("; ",lines));
    }
    private static void Splats(List<string> log)
    {
        var p=new NativeBloodSplats();int surface=7;
        for(int i=0;i<1000;i++){p.Add(i,new Vector3(i*.01f,0,0),Vector3.UnitY,Vector3.UnitX,.05f,.04f,(uint)i,i<500?surface:9,i*.01f);Check(p.Steady<=NativeBloodSplats.Kept&&p.Live<=NativeBloodSplats.Capacity,"the pool never holds more than "+NativeBloodSplats.Kept+" steady splats ("+p.Steady+", "+p.Live+" live)");}
        Check(p.Steady==NativeBloodSplats.Kept,"a full pool keeps exactly "+NativeBloodSplats.Kept+" ("+p.Steady+")");
        int oldest=int.MaxValue;for(int i=0;i<NativeBloodSplats.Capacity;i++){var s=p.Items[i];if(s.Live&&s.FadeAt<0)oldest=Math.Min(oldest,(int)s.Key);}
        Check(oldest==1000-NativeBloodSplats.Kept,"the oldest splats fade first (oldest steady "+oldest+")");
        Check(p.Add(999,Vector3.Zero,Vector3.UnitY,Vector3.UnitX,.05f,.04f,1,9,10)<0,"the same droplet never splats twice");
        float now=9.99f;p.Update(now+1);Check(p.Live==p.Steady,"faded splats are dropped ("+p.Live+" live)");
        // A surface that breaks takes its splats; the round's reset clears the rest.
        var q=new NativeBloodSplats();for(int i=0;i<20;i++)q.Add(i,Vector3.Zero,Vector3.UnitY,Vector3.UnitX,.05f,.04f,(uint)i,i<8?surface:9,1);
        Check(q.FadeSurface(surface,2)==8&&q.Opacity(0,2.1f)<1&&q.Opacity(0,2+NativeBloodSplats.SurfaceFade)==0,"a broken block's splats fade with it");
        q.Update(3);Check(q.Live==12,"only that block's splats go ("+q.Live+")");
        float dark=NativeBloodRules.Darken(60);Check(Math.Abs(NativeBloodRules.Darken(0)-1)<1e-5f&&dark<.6f&&dark>=.52f&&q.Opacity(10,3)==1,"splats stay whole and darken as they dry (x"+dark.ToString("0.00")+" after a minute)");
        q.Clear();p.Clear();Check(q.Live==0&&p.Live==0&&p.Steady==0,"reset clears every splat");
        var low=new NativeBloodSplats{Limit=160};for(int i=0;i<400;i++)low.Add(i,Vector3.Zero,Vector3.UnitY,Vector3.UnitX,.05f,.04f,(uint)i,9,i);Check(low.Steady==160,"the low tier keeps 160");
        log.Add("splat pool: "+NativeBloodSplats.Kept+" kept (160 low tier), the oldest fades over "+NativeBloodSplats.FadeSeconds+" s, keyed per droplet, a broken block's splats fade, darkening to x"+dark.ToString("0.00")+", reset clears all");
    }
    // A floor at y=0 (surface 1) and a wall at x=1.2 (surface 2).
    private static bool Room(Vector3 from,Vector3 to,out Vector3 point,out Vector3 normal,out int surface)
    {
        point=normal=Vector3.Zero;surface=0;
        if(to.X>=1.2f&&from.X<1.2f){float u=(1.2f-from.X)/(to.X-from.X);point=Vector3.Lerp(from,to,u);if(point.Y>0){normal=-Vector3.UnitX;surface=2;return true;}}
        if(to.Y<=0&&from.Y>0){float u=from.Y/(from.Y-to.Y);point=Vector3.Lerp(from,to,u);normal=Vector3.UnitY;surface=1;return true;}
        return false;
    }
    private static void Drops(List<string> log)
    {
        var sim=new NativeBloodSim();NativeBloodSim.Cast cast=Room;var lines=new List<string>();int total=0;
        foreach(var kind in new[]{"cut","puncture","cavity","sever","crush","bruise"})
        {
            sim.Clear(true);int n=sim.Spray(kind,.8f,1234,77,new Vector3(0,1.1f,0),Vector3.UnitZ,Vector3.UnitX,0,1,false,true);
            for(float t=0;t<3.2f;t+=Dt)sim.Step(t,cast);int splats=sim.Splats.Live,wall=0,floor=0;
            for(int i=0;i<NativeBloodSplats.Capacity;i++){var s=sim.Splats.Items[i];if(!s.Live)continue;if(s.Surface==1){floor++;Check(Math.Abs(s.Center.Y-NativeBloodRules.Lift)<1e-3f,"a floor splat lies just above it");}else wall++;Check(s.Length<=NativeBloodRules.MaxLength&&s.Width<=NativeBloodRules.MaxWidth&&s.Length>=s.Width*.99f,"splat size bounded and stretched along its travel");}
            Check(sim.Flying==0&&splats==n,kind+": every droplet lands ("+splats+" of "+n+")");Check(floor>0,kind+": droplets reach the floor");total+=n;
            lines.Add(kind+" "+n+" ("+floor+" floor, "+wall+" wall)");
        }
        // Thinned by reduced effects and the low tier; a burn bleeds nothing.
        Check(NativeBloodRules.Count("cut",.8f,.3f,false)<NativeBloodRules.Count("cut",.8f,1,false)&&NativeBloodRules.Count("cut",.8f,1,true)<NativeBloodRules.Count("cut",.8f,1,false)&&NativeBloodRules.Count("scorch",1,1,false)==0,"reduced effects and the low tier thin the droplets; burns bleed none");
        Check(NativeBloodRules.Count("cut",1,1,false)>NativeBloodRules.Count("puncture",1,1,false),"cuts spray more than bullets");
        // A drop that hits fast and shallow stretches more than one falling straight down.
        NativeBloodRules.Splat(new Vector3(4,-1,0),Vector3.UnitY,.02f,3,out _,out float l1,out float w1);NativeBloodRules.Splat(new Vector3(0,-4,0),Vector3.UnitY,.02f,3,out _,out float l2,out float w2);
        Check(l1/w1>l2/w2*1.4f,"a grazing drop stretches ("+(l1/w1).ToString("0.0")+" vs "+(l2/w2).ToString("0.0")+")");
        // A replayed wound flies but leaves no second set of splats.
        sim.Clear(true);sim.Spray("cut",1,5,9,new Vector3(0,1,0),Vector3.UnitZ,Vector3.UnitX,0,1,false,true);for(float t=0;t<3;t+=Dt)sim.Step(t,cast);int once=sim.Splats.Live;
        sim.Spray("cut",1,5,9,new Vector3(0,1,0),Vector3.UnitZ,Vector3.UnitX,3,1,false,false);for(float t=3;t<6;t+=Dt)sim.Step(t,cast);Check(sim.Splats.Live==once,"a replayed wound adds no splats");
        sim.Spray("cut",1,5,9,new Vector3(0,1,0),Vector3.UnitZ,Vector3.UnitX,6,1,false,true);for(float t=6;t<9;t+=Dt)sim.Step(t,cast);Check(sim.Splats.Live==once,"the same droplets fed twice never double");
        log.Add("droplets land and stay on the floor and a wall: "+string.Join(", ",lines)+"; thinned for reduced effects and the low tier; grazing drops stretch; replays add none");
    }
    private static void Budget(List<string> log)
    {
        // Seven Whirlwind pulses (.45 s) of cuts on one target, and a 12 Hz machine gun for 2 s: droplets per target per second
        // stay within the budget, and a kill's spray is never cut short.
        var sim=new NativeBloodSim();int ww=0,mg=0;
        for(int k=0;k<7;k++)ww+=sim.Spray("cut",.7f,(uint)(10+k),100+k,Vector3.Zero,Vector3.UnitZ,Vector3.UnitX,10+k*.45f,1,false,false,target:3);
        for(int k=0;k<24;k++)mg+=sim.Spray("puncture",.4f,(uint)(50+k),200+k,Vector3.Zero,Vector3.UnitZ,Vector3.UnitX,20+k*.085f,1,false,false,target:4);
        float wwSpan=6*.45f,mgSpan=23*.085f;
        Check(ww<=NativeBloodSim.BudgetBurst+NativeBloodSim.BudgetRate*wwSpan+1&&ww>=7,"a Whirlwind's seven pulses spray "+ww+" droplets ("+(ww/(wwSpan+.45f)).ToString("0")+"/s)");
        Check(mg<=NativeBloodSim.BudgetBurst+NativeBloodSim.BudgetRate*mgSpan+1&&mg>=24,"a machine gun stream sprays "+mg+" droplets");
        int kill=sim.Spray("sever",1,9,999,Vector3.Zero,Vector3.UnitZ,Vector3.UnitX,20+mgSpan,1,false,false,extra:12,target:4,kill:true);
        Check(kill==NativeBloodRules.Count("sever",1,1,false)+12,"a kill's spray is whole ("+kill+")");
        log.Add("per-target droplet budget "+NativeBloodSim.BudgetRate+"/s (burst "+NativeBloodSim.BudgetBurst+"): 7 Whirlwind pulses "+ww+" drops, 24 machine-gun hits "+mg+", kill sprays whole");
    }
    private static void Blade(List<string> log)
    {
        var l=new NativeHitLedger();var victim=new Actor{Id=1,Hp=100,Position=new V3(0,0,0)};var sword=new Actor{Id=2,SourceSpec="sword",Position=new V3(0,0,1.4f),Grounded=true};
        for(int k=0;k<3;k++)l.Observe(new Snapshot{Actors=new[]{victim,sword},Events=new[]{new ArenaEvent{Id=10+k,Kind=EventKind.Hit,Source=2,Target=1,Time=5+k*.5f,Amount=30,Detail="direct"}}});
        float fresh=l.BladeBlood(2,6.1f),later=l.BladeBlood(2,13),gone=l.BladeBlood(2,16.1f);
        Check(fresh>.9f&&later>0&&later<fresh&&gone==0&&l.BladeBlood(1,6)==0,"blade blood builds with hits ("+fresh+"), fades ("+later+") and is gone 10 s on ("+gone+")");
        int shown=0;for(int i=0;i<NativeBloodRules.Slivers;i++)if(NativeBloodRules.Sliver("sword",i,fresh,out var c,out var s)){shown++;Check(c.Z>.3f&&c.Z<1.2f&&s.X>0&&s.X<.04f,"blade blood sits on the edge");}
        Check(shown>=4&&!NativeBloodRules.Sliver("sword",0,0,out _,out _)&&!NativeBloodRules.Sliver("rifle",0,1,out _,out _),"slivers along a bloody blade, none on a clean one or a gun");
        l.Clear();Check(l.BladeBlood(2,6.1f)==0,"the round's reset cleans the blade");
        // A Q/E kit hit (the Whirlwind's pulse: detail "ability") is a melee blow too: the ledger keeps its kind for the slice.
        var gs=new Actor{Id=3,SourceSpec="greatsword",Position=new V3(1,0,0),Grounded=true,MeleeKit=new NativeMeleeKit{Kind="whirlwind"}};
        l.Observe(new Snapshot{Actors=new[]{victim,gs},Events=new[]{new ArenaEvent{Id=40,Kind=EventKind.Hit,Source=3,Target=1,Time=8,Amount=8.5f,Detail="ability"}}});
        Check(l.Blow(1,8,out var kitBlow)&&kitBlow.Kit=="whirlwind"&&kitBlow.Spec=="greatsword"&&l.BladeBlood(3,8.1f)>0,"a Whirlwind pulse is recorded as the greatsword's blow (kit "+kitBlow.Kit+") and bloodies its blade");
        log.Add("blade blood: 3 hits "+fresh.ToString("0.00")+", "+later.ToString("0.00")+" 8 s after the last, 0 by 10 s; "+shown+" edge slivers; cleared with the ledger");
    }
    private static void Plane(List<string> log)
    {
        // The cut plane is the captured swing's own: turning the attacker's committed yaw turns it exactly, and the blade's
        // travel at the front crossing goes the way the visible sweep does.
        var lines=new List<string>();
        foreach(var spec in new[]{"sword","greatsword","daggers"})
        {
            var set=MeleeSet.For(spec);
            for(int index=1;index<=set.Cuts.Length;index++)
            {
                var cut=NativeMeleeSlice.CutOf(set,set.Cut(index));var v=MeleeSwing.VisibleSweep(set,set.Cut(index));
                // Source frame: the native bearing is + toward the pilot's right (-x); travel.X < 0 sweeps to the right.
                Check(Math.Sign(-cut.Travel.X)==Math.Sign(v.Direction)||Math.Abs(cut.Travel.X)<.2f,spec+" cut "+index+": the blade travels as the visible sweep ("+cut.Travel+", "+v.Direction+")");
                float tilt=(float)(Math.Acos(Math.Min(1,Math.Abs(cut.Normal.Y)))/D);lines.Add(spec+" "+index+" "+tilt.ToString("0")+"°");
                foreach(float yaw in new[]{0f,37f,-120f})
                {
                    var plan=new SlicePlan();var blow=new MeleeBlow{At=1,Spec=spec,CommitYaw=yaw,Index=index,SwingAt=.9f,SwingUntil=1.1f,From=new V3(0,0,0)};
                    var q=MeleeMath.AxisAngle(MeleeMath.Y,-yaw*D);var victim=Vector3.Transform(new Vector3(0,0,1.5f),q);
                    NativeMeleeSlice.Plan(blow,true,spec,new NativeWound{point=new V3(0,.15f,0)},new V3(-victim.X,NativeMeleeSlice.RootHeight,victim.Z),yaw+180,7,plan);
                    var n=Vector3.Transform(cut.Normal,q);var expect=new Vector3(-n.X,n.Y,n.Z);
                    Check(Math.Abs(Math.Abs(Vector3.Dot(Vector3.Normalize(plan.Normal),expect))-1)<1e-3f,spec+" cut "+index+" at yaw "+yaw+": the plan's plane turns with the attacker ("+plan.Normal+" vs "+expect+")");
                }
            }
        }
        log.Add("slice planes follow the captured swing and the committed yaw (tilt from level: "+string.Join(", ",lines)+")");
    }
    private static void Severs(List<string> log)
    {
        // Lethal blows from every cut of each weapon at random places in reach, the victim facing about the attacker: how often
        // the body is taken apart, against the authority's wound alone (what the presentation showed before).
        var rnd=new Random(2026);var lines=new List<string>();
        foreach(var spec in new[]{"sword","greatsword","daggers","shield"})
        {
            int n=400,cut=0,parts=0,crush=0,throat=0,old=0,limbFinish=0,finishers=0;var joints=new Dictionary<int,int>();
            for(int k=0;k<n;k++)
            {
                float yaw=(float)rnd.NextDouble()*360-180,dist=1.2f+(float)rnd.NextDouble()*1.1f,face=yaw+180+(float)(rnd.NextDouble()*60-30);int index=1+rnd.Next(8);
                bool air=rnd.NextDouble()<.1;string kit=rnd.NextDouble()<.1?(spec=="greatsword"?"whirlwind":spec=="sword"?"flash-step":spec=="daggers"?"kunai-recast":"bulwark-charge"):null;
                var q=MeleeMath.AxisAngle(MeleeMath.Y,-yaw*D);var at=Vector3.Transform(new Vector3(0,0,dist),q);var victim=new V3(-at.X,NativeMeleeSlice.RootHeight+(float)rnd.NextDouble()*.1f,at.Z);
                var blow=new MeleeBlow{At=1,Spec=spec,CommitYaw=yaw,Index=index,SwingAt=.95f,SwingUntil=1.1f,From=new V3(0,0,0),Air=air,Kit=kit,Detail=kit!=null?"ability":"direct"};
                // The authority's wound for that blow: the target's collider point nearest the attacker's chest, as NativeMelee.
                float damage=spec=="greatsword"?52:spec=="daggers"?15:spec=="shield"?23:30;var local=new V3(0,.15f,.4f);
                var wound=NativeBodyDamage.Resolve(NativeBodyDamage.Melee(spec,1),NativeBodyDamage.Part(local,0),damage,true);wound.point=local;wound.direction=new V3(0,0,-1);
                if(NativeMeleeSlice.WoundTakesPart(wound))old++;
                var plan=new SlicePlan();NativeMeleeSlice.Plan(blow,true,spec,wound,victim,face,k,plan);
                if(plan.Count>0){cut++;parts+=plan.Count;foreach(int j in plan.Joints.AsSpan(0,plan.Count).ToArray())joints[j]=joints.TryGetValue(j,out int c)?c+1:1;}
                if(plan.Crush>=0)crush++;if(plan.Throat)throat++;
                if(spec=="daggers"&&(index%4==0||kit!=null||air)){finishers++;if(plan.Count>0)limbFinish++;}
                Check(plan.Count<=SlicePlan.Max&&(plan.Count==0||plan.Travel.LengthSquared()>.9f),spec+": a plan cuts at most three parts along a travel");
            }
            float oldRate=old/(float)n,cutRate=cut/(float)n;
            Check(oldRate<.05f,spec+": the authority's wound alone took a part off "+(oldRate*100).ToString("0")+"% of melee kills (the old presentation)");
            switch(spec)
            {
                case "sword":Check(cutRate>=.95f,"sword kills sever ("+cutRate+")");break;
                case "greatsword":Check(cutRate>=.95f&&parts/(float)Math.Max(1,cut)>=1.5f,"greatsword kills cleave several parts ("+cutRate+", "+parts/(float)Math.Max(1,cut)+" each)");break;
                case "daggers":Check(limbFinish==finishers&&finishers>0&&throat+cut==n,"dagger finishers take a limb ("+limbFinish+"/"+finishers+"), the rest slit the throat ("+throat+")");break;
                case "shield":Check(crush>=n*.85f&&crush+cut>=n,"shield kills crush ("+crush+"/"+n+")");break;
            }
            var top=new List<string>();foreach(var pair in joints)top.Add(CapturedRig.Bones[pair.Key].Name+" "+pair.Value);
            lines.Add(spec+": "+(cutRate*100).ToString("0")+"% sever"+(spec=="greatsword"?" ("+(parts/(float)Math.Max(1,cut)).ToString("0.0")+" parts each)":"")+(crush>0?", "+crush*100/n+"% crush":"")+(throat>0?", "+throat*100/n+"% throat":"")+" vs "+(oldRate*100).ToString("0")+"% before ["+string.Join(", ",top)+"]");
        }
        // Not a standard melee weapon: no plan.
        Check(!NativeMeleeSlice.Plan(default,false,"rifle",null,new V3(),0,1,new SlicePlan())&&!NativeMeleeSlice.Plan(default,false,"emperor",null,new V3(),0,1,new SlicePlan()),"guns and fighters never slice");
        log.Add("melee kills over 400 random blows each: "+string.Join("; ",lines));
    }
    private static void Replicated(string root,List<string> log)
    {
        // The same replicated hits at 30 and 144 frames a second show the same hips (the spring is a pure function of the hit
        // times), and the layer never moves the authoritative actor.
        float[] rates={30,144};var hips=new List<float>[2];
        for(int k=0;k<2;k++)
        {
            var r=new Rig("rifle",new NativeHitLedger());hips[k]=new List<float>();float dt=1/rates[k];int hit=0;float[] at={2,2.25f,2.7f};
            for(int f=0;f*dt<3.2f;f++)
            {
                float t=1+f*dt;while(hit<at.Length&&t>=at[hit]-1e-4f){Strike(r,"sword",20,at[hit],.18f,30);hit++;}
                var before=r.A.Position;var vel=r.A.Velocity;r.Step(t,dt);Check(r.A.Position.x==before.x&&r.A.Position.z==before.z&&r.A.Velocity.x==vel.x,"the layer never moves the authoritative pilot");
                for(int s=0;s<12;s++){float sample=2+s*.1f;if(Math.Abs(t-sample)<dt*.5f-1e-5f)hips[k].Add(r.Layer.Stagger.Hip(sample).Length());}
            }
        }
        int same=Math.Min(hips[0].Count,hips[1].Count);float worst=0;for(int i=0;i<same;i++)worst=Math.Max(worst,Math.Abs(hips[0][i]-hips[1][i]));
        Check(same>=6&&worst<1e-4f,"the hips are the same at 30 and 144 Hz ("+same+" samples, "+worst+" m apart)");
        // Every input the new rules take is a replicated type: the ledger reads Snapshot events and actors, the slice a ledger
        // record (built from those) and the replicated wound, the blood the replicated wound.
        string core=Path.Combine(root,"Assets/ChipArena/Core"),rt=Path.Combine(root,"Assets/ChipArena/Runtime");
        foreach(var f in new[]{"NativeStagger.cs","NativeBlood.cs","NativeMeleeSlice.cs"}){var text=File.ReadAllText(Path.Combine(core,f));Check(!text.Contains("NativeArena")&&!text.Contains("NativePilot")&&!text.Contains("Random("),f+" reads no authority state and no clock-free randomness");}
        var blood=File.ReadAllText(Path.Combine(rt,"NativeBloodPresentation.cs"));Check(!blood.Contains(".Hp=")&&!blood.Contains("Damage(")&&!blood.Contains("Emit(")&&blood.Contains("NativeArena.WorldMask"),"the blood presentation only sweeps the arena's colliders");
        var death=File.ReadAllText(Path.Combine(rt,"NativeDeathPresentation.cs"));Check(death.Contains("has=hitLedger.Blow(death.id,death.at,out blow)")&&death.Contains("new NativeCorpse(death,rig,arena,slice)"),"the slice is planned from the ledger's replicated blow");
        var deaths=File.ReadAllText(Path.Combine(rt,"NativeDeaths.cs"));Check(!deaths.Contains("Slice")&&!deaths.Contains("Stagger"),"the authority's wounds and deaths are unchanged");
        log.Add("presentation keyed to replicated data only: the same hips at 30 and 144 Hz ("+same+" samples within "+(worst*1e6).ToString("0")+" µm), the authoritative pilot untouched, the rules read no authority state, the authority's wound code unchanged");
    }
}
