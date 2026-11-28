using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>One body-chip piece: a weapon-visuals primitive in the frame of the rig joint its 'body-chips' socket hangs from.</summary>
    public struct BodyChip
    {
        public WeaponPiece Piece;public int Bone;public bool Jet,Visible;
        // What the piece shows and which of its kind it is (the model's own pieces, NativeBodyChipGear, are laid out by these).
        public BodyChipRole Role;public int Index;
    }
    /// <summary>A body-chip piece's part in the build: the chest pips, the frame's plates, the orbs, the stamina pips, the boots, the
    /// dash jets and the block nodes.</summary>
    public enum BodyChipRole{Vital,Frame,FrameArm,Orb,Stamina,Boot,Jet,Block}
    /// <summary>
    /// The body chips from client/weapon-visuals.ts bodyModifiers at 05950ea: the build a pilot's installed Chips show on the rig
    /// (renderer.ts:83,129 runs it on every pilot rig and on the first-person rig). Red vital pips on the chest (up to six), a grey
    /// frame plate on the chest and both upper arms (frame, fortress, juggernaut or thorns), violet mana orbs orbiting the chest,
    /// green stamina pips beside it, knockback/debris boot plates (gold with Salvage), cyan dash jets behind the feet while the
    /// pilot dashes, and three teal block nodes over the chest. In the source joint frames (+x the rig's left; the Unity boundary
    /// mirrors x once, as for weapon pieces). Presentation only: reads the replicated seat chips and the stats every client
    /// recomputes from them, never the authority, the network frame or the hit rules.
    /// </summary>
    public static class NativeBodyChips
    {
        /// <summary>6 vital + chest frame + 2 arm plates + 6 orbs + 10 stamina + 2 boot plates + 2 jets + 3 block nodes.</summary>
        public const int MaxPieces=32;
        private static int Count(IReadOnlyDictionary<string,int> chips,string id)=>chips!=null&&chips.TryGetValue(id,out int n)&&n>0?n:0;
        private static void Add(BodyChip[] o,ref int n,BodyChipRole role,int index,int bone,WeaponShape shape,bool glow,int color,float x,float y,float z,float sx,float sy,float sz,int orbit=-1,bool jet=false,float rx=0)
        {
            if(n>=o.Length)return;
            o[n++]=new BodyChip{Role=role,Index=index,Bone=bone,Jet=jet,Visible=true,Piece=new WeaponPiece{Shape=shape,Glow=glow,Color=color,Position=new Vector3(x,y,z),Scale=new Vector3(sx,sy,sz),Rotation=CapturedRig.Euler(rx),Orbit=orbit}};
        }
        /// <summary>
        /// weapon-visuals.ts:114-120 in build order: chest pips, the frame plates (chest, then each upper arm), mana orbs, stamina
        /// pips (the left column, then the right), each foot's boot plate and dash jet, then the block nodes. Chip counts drive the
        /// pips, plates and Salvage colour; the stats drive the orbs, stamina pips, boot plates, jets and nodes, exactly as the
        /// source reads them. Returns the piece count (no allocation).
        /// </summary>
        public static int Build(IReadOnlyDictionary<string,int> chips,ChipStats stats,BodyChip[] o)
        {
            int n=0;if(stats==null||o==null)return 0;
            int vital=Math.Min(6,Count(chips,"vital"));
            for(int i=0;i<vital;i++)Add(o,ref n,BodyChipRole.Vital,i,CapturedRig.Chest,WeaponShape.Cube,true,0xff7478,(i%3-1)*.12f,.12f+i/3*.1f,.18f,.085f,.06f,.045f);
            if(Count(chips,"frame")>0||Count(chips,"fortress")>0||Count(chips,"juggernaut")>0||Count(chips,"thorns")>0)
            {
                Add(o,ref n,BodyChipRole.Frame,0,CapturedRig.Chest,WeaponShape.Cube,false,0x718899,0,.1f,.19f,.59f,.25f,.09f);
                foreach(int arm in Arms)Add(o,ref n,BodyChipRole.FrameArm,arm==CapturedRig.LArm?0:1,arm,WeaponShape.Cube,false,0x718899,0,-.065f,0,.26f,.16f,.26f);
            }
            // weapon-visuals.ts:121 animates the orbs through r.chest.getObjectByName('body-chips'), a depth-first search that
            // reaches the left upper arm's socket (the chest's arms come before its own socket) whenever the frame plates
            // exist; the orbs then hold their build position (0, .2, -.28) behind the chest instead of circling.
            int orbs=Math.Min(6,(int)Math.Ceiling((stats["maxMana"]-100)/25));bool plated=n>vital;
            for(int i=0;i<orbs;i++)Add(o,ref n,BodyChipRole.Orb,i,CapturedRig.Chest,WeaponShape.Sphere,true,0xad8cff,0,.2f,-.28f,.065f,.065f,.065f,orbit:plated?-1:i);
            if(stats["maxStamina"]>100){int pips=Math.Min(5,(int)Math.Ceiling((stats["maxStamina"]-100)/25));for(int side=-1;side<=1;side+=2)for(int i=0;i<pips;i++)Add(o,ref n,BodyChipRole.Stamina,(side<0?0:5)+i,CapturedRig.Chest,WeaponShape.Cube,true,0x8bffb6,side*.25f,.02f+i*.05f,.18f,.035f,.025f,.025f);}
            bool boots=stats["knockbackTaken"]<1||stats["debrisTaken"]<1,jets=stats["dashCooldown"]<1;int boot=Count(chips,"salvage")>0?0xdab078:0x7e97ab;
            foreach(int foot in Feet)
            {
                if(boots)Add(o,ref n,BodyChipRole.Boot,foot==CapturedRig.LFoot?0:1,foot,WeaponShape.Cube,false,boot,0,.06f,0,.26f,.13f,.28f);
                if(jets)Add(o,ref n,BodyChipRole.Jet,foot==CapturedRig.LFoot?0:1,foot,WeaponShape.Cone,true,0x63e5ff,0,0,-.17f,.075f,.30f,.075f,jet:true,rx:-(float)Math.PI/2);
            }
            if(stats["blockForce"]>1||stats["blockHeal"]!=0||stats["blockAmmo"]!=0||stats["blockCooldown"]!=1||stats["blockWindow"]!=0||stats["reflectDamage"]>1||stats["blockPulseDamage"]!=0)
                for(int i=0;i<3;i++)Add(o,ref n,BodyChipRole.Block,i,CapturedRig.Chest,WeaponShape.Cube,true,0x78f7e7,(i-1)*.12f,.27f,.17f,.075f,.04f,.045f);
            return n;
        }
        private static readonly int[] Arms={CapturedRig.LArm,CapturedRig.RArm},Feet={CapturedRig.LFoot,CapturedRig.RFoot};
        /// <summary>
        /// weapon-visuals.ts:121-122 per frame: each orb circles the chest (a = 1.5 t + orbit π/3), and a dash jet shows only while
        /// dashTime &gt; 0, stretched to .5 (its .1 rest length is never seen). Everything else is static.
        /// </summary>
        public static void Animate(ref BodyChip c,float time,float dashTime)
        {
            if(c.Piece.Orbit>=0){double a=time*1.5+c.Piece.Orbit*Math.PI/3;c.Piece.Position=new Vector3((float)(Math.Cos(a)*.40),(float)(.26+Math.Sin(a*2)*.10),(float)(Math.Sin(a)*.36));}
            if(c.Jet){bool on=dashTime>0;c.Visible=on;c.Piece.Scale.Y=on?.5f:.1f;}
        }
        /// <summary>
        /// The first-person rig hides the legs (renderer.ts:77), so the boot plates and jets never show there, while the chest
        /// chips stay (only the torso's own meshes are hidden). The arm plates follow the upper arms, which the Unity view model
        /// does not draw (NativeViewRules.ViewModelBone).
        /// </summary>
        public static bool FirstPerson(int bone)=>bone==CapturedRig.Chest;
        /// <summary>
        /// The source's dashTime as the dash jets read it (movement.ts:25-27,47): the rest of a dodge roll while it lasts
        /// (roll.until - time; the Ansatsuken's roll is its Demon Step), else an Emperor wavedash's .18 s push. A hit, juggle or
        /// knockdown clears it (emperor.ts/arcade.ts confirm). `wave` is the pilot's own wavedash track.
        /// </summary>
        public static float DashTime(Actor a,NativeWaveDash wave,float time)
        {
            if(a==null||!a.Alive)return 0;
            if(a.Rolling(time))return a.RollAt+a.VisualRollDuration-time;
            if(a.SourceSpec!="emperor"||wave==null)return 0;
            if(a.StunUntil>time||NativeJugglePose.Active(a.JuggleUntil,time)||NativeSpecialPose.Down(a,time)){wave.Stop(a.SourceAction,a.SourceAt);return 0;}
            return wave.DashTime(a.SourceAction,a.SourceAt,a.SourceUntil,time);
        }
    }
}
