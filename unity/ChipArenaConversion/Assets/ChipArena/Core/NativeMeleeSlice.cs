using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>A melee blow as the hit ledger read it from the frame that carried its Hit event (every peer, from replicated
    /// data only): when it landed, the attacker's weapon, its committed swing (Unity yaw in degrees, attack index, hit window),
    /// whether it was a jump attack (the swing's own replicated air flag), an E special or a Q/E kit ability (their kinds), and
    /// where the attacker stood (Unity, its feet, as that frame had it). `Toward` (`Aimed`): the Hit event's own direction, the
    /// authority's unit vector from the attacker's body centre to the target's at the hit's tick (its Damage step's hit
    /// normal; the swing's .15 m origin lift cancels out), exact on every peer, where `From` is the frame's, a tick or two late.
    /// `Struck` (`Exact`): the attacker's body centre at the hit's tick, once the authority sends it on melee hits (the contract
    /// in docs/BISECT-GIB.md "Melee cuts": ArenaEvent.From with the swing's id in Projectile; no hit carries it yet).</summary>
    public struct MeleeBlow{public float At,CommitYaw,SwingAt,SwingUntil,Yaw,Amount;public int Source,Index;public string Spec,Special,Kit,Detail;public bool Air,Aimed,Exact;public V3 From,Toward,Struck;}

    public enum SliceKind{None,Clean,Cleave,Slit,Limb,Crush,Chop}

    /// <summary>How a lethal melee blow takes the body apart (presentation only): the ragdoll joints it severs (part roots,
    /// at most three), a part it crushes instead, a slit throat, the cut plane and the blade's travel (Unity world, unit), and
    /// how hard the severed parts fly along the swing (m/s), how fast they spin (rad/s) and how much the stumps bleed.
    /// `Point` is where the plane meets the body (Unity world: on the victim's centre line at `Height`, or for an upright
    /// plane its crossing beside the centre line, kept within the body); `Spec` the killer's weapon.</summary>
    public sealed class SlicePlan
    {
        public const int Max=3;
        public readonly int[] Joints=new int[Max];public int Count;public int Crush=-1;public bool Throat;public SliceKind Kind;
        public Vector3 Normal,Travel,Point;public float Height,Kick,Spin,Blood=1;public string Spec;
        public bool Has(int joint){for(int i=0;i<Count;i++)if(Joints[i]==joint)return true;return false;}
        public bool Any=>Count>0||Crush>=0||Throat;
        public void Clear(){Count=0;Crush=-1;Throat=false;Kind=SliceKind.None;Normal=Travel=Point=Vector3.Zero;Height=Kick=Spin=0;Blood=1;Spec=null;}
        internal void Add(int joint){if(Count<Max&&!Has(joint))Joints[Count++]=joint;}
    }

    /// <summary>
    /// Melee kills that slice (owner 2026-10-12: "I want more gibs from the melee weapon too, I havent seen them slice an
    /// enemy at all"). Why they never did: the authority's melee wound (NativeBodyDamage.Melee + Resolve) puts every swing's
    /// contact on the target's collider point nearest the attacker's chest, which NativeBodyDamage.Part always files under the
    /// torso, whose cut-to-sever threshold is 380 J; a sword blow carries about 64 J per unit of damage scale, a greatsword
    /// 192 and the daggers 19, so a lethal swing was a torso "cut", never a "sever" (heads and limbs, 38 and 58 J, were never
    /// the part). The authority is left alone (no rule, damage or wire change); here the presentation plans the dismemberment
    /// from the killing blow's replicated swing instead:
    /// <list type="bullet">
    /// <item>The cut plane is the captured swing's own: the posed blade (the cut the attacker played for its attack index, as
    /// MeleeSwing.VisibleSweep poses it) where it crosses the front, turned by the attacker's committed yaw, through the point
    /// where that blade passes the victim. Jump attacks and the vertical specials chop top-down; spins and dash cuts sweep
    /// level; a blow with no replicated swing (a late join) falls back to a level cut at the wound, sided by the death seed.</item>
    /// <item>Sword: one clean cut where the blade crossed (head, an arm or the trunk). Greatsword: a cleave through
    /// every joint near the plane (both arms, torso halves with the forearms, up to three parts), heavier and bloodier.
    /// Daggers: a slit throat, a limb on the combo's finishing cut or a Q/E kill. Shield: the struck head or arm crushed into
    /// fragments; its thrown edge (Aegis) severs. Jump attacks and vertical Q/E kills (Earthsplitter, Wolf Knight, Rising
    /// Dragon) split top-down: head, the arm under the blade and, for the heavy ones, the waist.</item>
    /// </list>
    /// Only for a pilot's lethal melee blow by a standard melee weapon; living pilots never lose limbs. Pure, deterministic.
    /// Where it applies (a blade, gore on, a stock ending) NativeMeleeBisect turns this plan into a cut of the body's mesh; the
    /// joints here stay the presentation everywhere else.
    /// </summary>
    public static class NativeMeleeSlice
    {
        /// <summary>The ragdoll part roots a cut can take off (NativeArmorLight.Severable).</summary>
        public static readonly int[] Joints={CapturedRig.Head,CapturedRig.Spine,CapturedRig.LArm,CapturedRig.RArm,CapturedRig.LElbow,CapturedRig.RElbow,CapturedRig.LHip,CapturedRig.RHip,CapturedRig.LKnee,CapturedRig.RKnee};
        /// <summary>Above the feet, the rig root (the body centre the authority positions) stands this high.</summary>
        public const float RootHeight=.865f;
        /// <summary>A joint's rest position above the feet in the pilot's facing frame (source frame: +x its left, +z forward).</summary>
        public static Vector3 Rest(int bone){var p=Vector3.Zero;for(int b=bone;b>=0;b=CapturedRig.Bones[b].Parent)p+=CapturedRig.Bones[b].Position;return p+new Vector3(0,RootHeight,0);}
        public static bool Covers(string spec)=>NativeMeleeRules.Standard(spec);

        // ---- the captured swing's plane ----
        /// <summary>A cut's blade at its front crossing, in the attacker's facing frame (source frame, from the rig root):
        /// the plane normal, the tip's travel and the blade's hand and tip.</summary>
        public readonly struct Cut{public readonly Vector3 Normal,Travel,Hand,Tip;public Cut(Vector3 n,Vector3 t,Vector3 h,Vector3 p){Normal=n;Travel=t;Hand=h;Tip=p;}}
        private static readonly Dictionary<CapturedClip,Cut> cuts=new Dictionary<CapturedClip,Cut>();
        public static Cut CutOf(MeleeSet set,CapturedClip c)
        {
            if(cuts.TryGetValue(c,out var cut))return cut;
            var r=new MeleeRig();var row=new float[CapturedClip.Channels];var s=MeleeSwing.For(c);var v=MeleeSwing.VisibleSweep(set,c);int steps=MeleeSwing.Visible.Steps;
            var tips=new Vector3[steps+1];var hands=new Vector3[steps+1];int lead=0;float best=float.MaxValue;
            float[] path=new float[2];var both=new Vector3[2,steps+1];var grips=new Vector3[2,steps+1];
            for(int j=0;j<=steps;j++)
            {
                c.Sample(s.SweepStart+(s.SweepEnd-s.SweepStart)*j/steps,row);r.Reset();MeleeCapture.Apply(r,set,row,CaptureOptions.Of(1,set.Fit,MeleeClear.Turn,legs:0));
                for(int h=0;h<(set.Dual?2:1);h++)
                {
                    int hand=h==0?CapturedRig.RHand:CapturedRig.LHand;grips[h,j]=r.ChainPoint(hand,-1);both[h,j]=grips[h,j]+Vector3.Transform(r.Reached(set.Tip),r.ChainQuat(hand,-1));
                    if(j>0)path[h]+=Vector3.Distance(both[h,j],both[h,j-1]);
                }
            }
            lead=set.Dual&&path[1]>path[0]?1:0;for(int j=0;j<=steps;j++){tips[j]=both[lead,j];hands[j]=grips[lead,j];}
            // The sample nearest the front within the main crossing (VisibleSweep's Enter..Leave).
            int at=Math.Max(0,Math.Min(steps,(v.Enter+v.Leave)/2));
            for(int j=v.Enter;j<=v.Leave;j++){float b=Math.Abs(v.Bearing[j]);if(b<best){best=b;at=j;}}
            int a0=Math.Max(0,at-1),a1=Math.Min(steps,at+1);Vector3 travel=tips[a1]-tips[a0];if(travel.LengthSquared()<1e-6f)travel=new Vector3(-v.Direction,0,0);
            Vector3 axis=tips[at]-hands[at];var n=Vector3.Cross(axis,travel);if(n.LengthSquared()<1e-8f)n=Vector3.UnitY;
            cut=new Cut(Vector3.Normalize(n),Vector3.Normalize(travel),hands[at],tips[at]);cuts[c]=cut;return cut;
        }

        // ---- the plan ----
        private static Vector3 Source(V3 u)=>new Vector3(-u.x,u.y,u.z);
        private static Vector3 Unity(Vector3 s)=>new Vector3(-s.X,s.Y,s.Z);
        private static Quaternion Facing(float unityYawDegrees)=>MeleeMath.AxisAngle(MeleeMath.Y,-unityYawDegrees*(float)Math.PI/180);
        private static bool Is(string kind,string part)=>kind!=null&&kind.IndexOf(part,StringComparison.Ordinal)>=0;
        /// <summary>
        /// The dismemberment of a lethal melee blow. `blow`: the ledger's record of the killing blow (`hasBlow` false when none
        /// was seen); `spec`: the killer's weapon; `wound`: the authority's fatal wound (its local point gives the contact height
        /// when no swing is known); the victim's Unity position (body centre, as NativeDeath.position) and yaw (degrees); the
        /// blow's From is the attacker's Actor.Position (its feet); `seed`: the death's seed. With the Hit event's direction
        /// (`blow.Aimed`) the attacker stands where that direction puts it, at the frame's distance: the bearing and height are
        /// the authority's at the hit's tick, so only the distance is the frame's, and a slash's plane (its normal across the
        /// bearing) does not move with it. False (and an empty plan) when this is not a standard melee kill.
        /// </summary>
        public static bool Plan(in MeleeBlow blow,bool hasBlow,string spec,NativeWound wound,V3 victim,float victimYaw,int seed,SlicePlan into)
        {
            into.Clear();if(!Covers(spec))return false;uint h=unchecked((uint)seed*2654435761u);h^=h>>15;
            var vq=Facing(victimYaw);Vector3 vp=Source(victim)-new Vector3(0,RootHeight,0);
            Vector3 J(int bone)=>vp+Vector3.Transform(Rest(bone),vq);
            string kit=hasBlow?blow.Kit:null,special=hasBlow?blow.Special:null;
            bool air=hasBlow&&blow.Air&&kit==null&&special==null;
            // Thrown: the attacker stood well beyond the weapon's reach (a dagger or the Aegis shield in flight).
            float reach=spec=="greatsword"?3.4f:spec=="sword"?2.5f:spec=="shield"?2.1f:1.75f;
            bool thrown=hasBlow&&Math.Sqrt((blow.From.x-victim.x)*(blow.From.x-victim.x)+(blow.From.z-victim.z)*(blow.From.z-victim.z))>reach+1.6f;
            bool chop=air||Is(kit,"earthsplitter")||Is(kit,"wolf-knight")||Is(kit,"rising-dragon")||Is(kit,"skyfall")||Is(special,"slam")||Is(special,"launch");
            bool spin=Is(kit,"whirlwind")||Is(kit,"spin")||Is(special,"spin")||Is(kit,"hurricane");
            // The plane (source world): normal n through C; the blade's travel t.
            Vector3 n,t,c;
            // The attacker's feet (Actor.Position is the feet; the victim's death position is its body centre).
            Vector3 attacker=hasBlow?Source(blow.From):vp+Vector3.Transform(new Vector3(0,0,1.4f),vq);
            if(hasBlow&&blow.Aimed)attacker=Attacker(blow,victim);
            float yaw=hasBlow?blow.CommitYaw:victimYaw+180;var aq=Facing(yaw);
            Vector3 toVictim=vp-attacker;toVictim.Y=0;if(toVictim.LengthSquared()<1e-4f)toVictim=Vector3.Transform(new Vector3(0,0,1),aq);toVictim=Vector3.Normalize(toVictim);
            // The attacker's left across the blow (source frame +x is a rig's left).
            Vector3 right=Vector3.Cross(Vector3.UnitY,toVictim);
            float contact=wound!=null?MeleeMath.Clamp(wound.point.y+RootHeight,.5f,1.5f):1.05f;
            if(chop)
            {
                // Top-down: a vertical plane along the blow, through the weapon arm's side of the victim.
                float side=hasBlow&&MeleeSet.For(spec)!=null&&!MeleeSet.For(spec).Dual?-.12f:((h&1)==0?.12f:-.12f);
                n=Vector3.Normalize(right);t=new Vector3(0,-1,0)*.85f+toVictim*.5f;t=Vector3.Normalize(t);c=vp+new Vector3(0,1.25f,0)+right*side;
            }
            else if(spin||thrown||!hasBlow||Is(kit,"flash-step")||Is(kit,"kunai")||Is(kit,"aegis"))
            {
                float height=spin?.78f:Is(kit,"flash-step")?1.12f:thrown?contact:hasBlow?1.08f:contact;
                n=Vector3.UnitY;float dir=(h&2)==0?1:-1;t=Vector3.Normalize(right*dir+toVictim*.25f);c=vp+new Vector3(0,height,0);
                // A level cut tilts a little the way it travels, so it reads as a swing rather than a guillotine.
                n=Vector3.Normalize(n+t*.12f);
            }
            else
            {
                var set=MeleeSet.For(spec);var cut=CutOf(set,set.Cut(Math.Max(1,blow.Index)));
                n=Vector3.Transform(cut.Normal,aq);t=Vector3.Transform(cut.Travel,aq);
                // Where the blade passes the victim: along the blade, at the victim's distance (the tip if it falls short).
                Vector3 rootAt=attacker+new Vector3(0,RootHeight,0),hand=rootAt+Vector3.Transform(cut.Hand,aq),tip=rootAt+Vector3.Transform(cut.Tip,aq);
                float d=Vector3.Distance(new Vector3(vp.X,0,vp.Z),new Vector3(attacker.X,0,attacker.Z))-.2f,hd=new Vector2(hand.X-attacker.X,hand.Z-attacker.Z).Length(),td=new Vector2(tip.X-attacker.X,tip.Z-attacker.Z).Length();
                float u=td>hd+1e-3f?MeleeMath.Clamp((d-hd)/(td-hd),0,1):1;c=Vector3.Lerp(hand,tip,u);
            }
            if(n.LengthSquared()<1e-8f)n=Vector3.UnitY;n=Vector3.Normalize(n);if(t.LengthSquared()<1e-8f)t=right;t=Vector3.Normalize(t-n*Vector3.Dot(t,n));if(t.LengthSquared()<1e-8f)t=right;
            // The plane's height on the victim's centre line, kept on the body.
            float h0=Math.Abs(n.Y)>.25f?c.Y-(n.X*(vp.X-c.X)+n.Z*(vp.Z-c.Z))/n.Y:c.Y;h0=MeleeMath.Clamp(h0-vp.Y,.3f,1.5f);
            // Where the plane meets the body: on the centre line at that height; an upright plane (a chop) where it passes beside
            // the centre line, at most .2 m off it.
            Vector3 axisAt=vp+new Vector3(0,h0,0),point=axisAt;
            if(Math.Abs(n.Y)<=.25f)point=axisAt+n*MeleeMath.Clamp(Vector3.Dot(n,c-axisAt),-.2f,.2f);
            into.Point=Unity(point);into.Spec=spec;
            float S(int bone)=>Math.Abs(Vector3.Dot(n,J(bone)-c));
            int Near(params int[] options){int pick=options[0];float least=float.MaxValue;foreach(int o in options){float s=S(o);if(s<least-.03f){least=s;pick=o;}else if(Math.Abs(s-least)<=.03f){/* the side the blade enters first */float e0=Vector3.Dot(J(pick)-vp,t),e1=Vector3.Dot(J(o)-vp,t);if(e1<e0){pick=o;least=Math.Min(least,s);}}}return pick;}
            // A clean cut takes the head, an arm or the trunk (a blade crossing below the waist cuts the trunk on its way through:
            // the diagonal torso split); the legs only go to a cleave.
            int Clean()=>h0>=1.22f?CapturedRig.Head:h0>=.98f?Near(CapturedRig.LArm,CapturedRig.RArm,CapturedRig.LElbow,CapturedRig.RElbow):CapturedRig.Spine;
            into.Normal=Unity(n);into.Travel=Unity(t);into.Height=h0;into.Spin=9;
            switch(spec)
            {
                case "greatsword":
                {
                    into.Kind=chop?SliceKind.Chop:SliceKind.Cleave;into.Kick=4.4f;into.Blood=1.6f;into.Spin=7;
                    if(chop){into.Add(CapturedRig.Head);into.Add(Near(CapturedRig.LArm,CapturedRig.RArm));into.Add(CapturedRig.Spine);break;}
                    // Every joint near the plane, nearest first, the clean cut's joint always among them.
                    into.Add(Clean());
                    for(int k=0;k<3&&into.Count<SlicePlan.Max;k++)
                    {
                        int pick=-1;float least=.13f;foreach(int j in Joints)if(!into.Has(j)){float s=S(j);if(s<least){least=s;pick=j;}}
                        if(pick<0)break;into.Add(pick);
                    }
                    break;
                }
                case "daggers":
                {
                    into.Kick=2.6f;into.Blood=1.8f;
                    bool finisher=hasBlow&&(Math.Max(1,blow.Index)%4==0||kit!=null||special!=null)||chop;
                    if(chop){into.Kind=SliceKind.Chop;into.Add(CapturedRig.Head);into.Add(Near(CapturedRig.LArm,CapturedRig.RArm));break;}
                    if(finisher){into.Kind=SliceKind.Limb;into.Add(h0>=1.05f?Near(CapturedRig.LArm,CapturedRig.RArm,CapturedRig.LElbow,CapturedRig.RElbow):Near(CapturedRig.LElbow,CapturedRig.RElbow));}
                    else{into.Kind=SliceKind.Slit;into.Throat=true;}
                    break;
                }
                case "shield":
                {
                    into.Blood=1.2f;into.Kick=3f;
                    if(thrown||Is(kit,"aegis")){into.Kind=SliceKind.Clean;into.Add(Clean());break;}
                    into.Kind=SliceKind.Crush;into.Crush=h0>=.98f&&h%3!=0?CapturedRig.Head:Near(CapturedRig.LArm,CapturedRig.RArm);break;
                }
                default:
                {
                    into.Kick=3.3f;into.Blood=1.15f;
                    if(chop){into.Kind=SliceKind.Chop;into.Add(CapturedRig.Head);into.Add(Near(CapturedRig.LArm,CapturedRig.RArm));break;}
                    into.Kind=SliceKind.Clean;into.Add(Clean());break;
                }
            }
            return into.Any;
        }
        /// <summary>
        /// The attacker's feet (source frame) at the hit's tick: the authority's own body centre when the hit carries it and it
        /// agrees with the event's direction (within 2°), else back from the victim's body centre along the Hit event's
        /// direction by the frame's horizontal distance, its height from the direction's rise. An upright direction (a blow from
        /// straight above) or a missing one keeps the frame's position.
        /// </summary>
        public static Vector3 Attacker(in MeleeBlow blow,V3 victim)
        {
            Vector3 from=Source(blow.From),centre=Source(victim);var d=Source(blow.Toward);float flat=(float)Math.Sqrt(d.X*d.X+d.Z*d.Z);
            if(blow.Exact&&blow.Aimed)
            {
                var struck=Source(blow.Struck);var to=centre-struck;float far=to.Length();
                if(far>.3f&&far<8&&Vector3.Dot(to/far,CapturedRig.Unit(d,Vector3.Zero))>Math.Cos(2*Math.PI/180))return struck-new Vector3(0,RootHeight,0);
            }
            if(!blow.Aimed||!(flat>.2f)||float.IsNaN(d.Y))return from;
            float reach=(float)Math.Sqrt((centre.X-from.X)*(centre.X-from.X)+(centre.Z-from.Z)*(centre.Z-from.Z));
            return new Vector3(centre.X-d.X/flat*reach,centre.Y-d.Y/flat*reach-RootHeight,centre.Z-d.Z/flat*reach);
        }
        /// <summary>The old presentation's only input: whether the authority's fatal wound itself severed (or crushed) a part.</summary>
        public static bool WoundTakesPart(NativeWound w)=>w!=null&&w.destroyed&&(w.kind=="sever"||w.kind=="crush");
    }
}
