using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // The Standard weapon skins (owner 2026-10-12: "much higher quality than what we have now"; the exported source models
    // stay as the Prototype Series, NativeWeaponSkins). Each weapon is sculpted in code (NativeWeaponSculpt) at three tiers in
    // the same weapon frame and around the same sockets the Prototype models define, so every consumer keeps working unchanged:
    // the muzzle (NativeWeaponDressing.MuzzleZ), the blade tip (BladeTip) and visual blade length, the grip at the hand joint,
    // the support grips (gun .025,-.03,.10 / pistol .06,-.10,-.01, two-hander 0,0,-.11, staff guard-grip 0,0,.40), the staff
    // tip and the chip modules, enchants, trails and muzzle effects placed from them. The designs are in
    // NativeWeaponModels.Guns.cs, .Melee.cs and .Magic.cs (magic schools, L33t H4x and the DEV fighters' gauntlets).
    // Presentation only: meshes never define reach, hit volumes or timing (weapon-visuals.ts:20).
    public static partial class NativeWeaponModels
    {
        public const int Full=0,Reduced=1,Low=2,Tiers=3;
        /// <summary>Triangle ceilings per tier for one model (both hands of a pair counted).</summary>
        public static readonly int[] Budget={4800,2800,1400};
        public const int LeftHand=9,RightHand=13;
        /// <summary>One weapon's design: how it is built and the sockets it carries (source weapon frame).</summary>
        public sealed class Design
        {
            public string Spec;public Action<WeaponSculpt> Build;
            /// <summary>Melee reach: the blade from Base to the tip socket stretches; everything behind Base stays rigid.</summary>
            public float StretchBase=float.NaN,StretchTip=float.NaN;
            /// <summary>Built on both hands (daggers, fists); the left copy is mirrored across the blade plane when MirrorLeft.</summary>
            public bool Dual,MirrorLeft;
            /// <summary>The joint a single weapon rides: the right hand, or the left for the staffs and L33t H4x.</summary>
            public int Bone=RightHand;
            /// <summary>The sockets this model is built around (source frame; NaN components: none).</summary>
            public Vector3 Muzzle=new Vector3(float.NaN),BladeTip=new Vector3(float.NaN),BladeBase=new Vector3(float.NaN),Support=new Vector3(float.NaN),StaffTip=new Vector3(float.NaN);
        }
        private static Dictionary<string,Design> designs;
        static partial void Guns(Dictionary<string,Design> d);
        static partial void Melee(Dictionary<string,Design> d);
        static partial void Magic(Dictionary<string,Design> d);
        private static Dictionary<string,Design> Designs()
        {
            if(designs!=null)return designs;var d=new Dictionary<string,Design>(StringComparer.Ordinal);Guns(d);Melee(d);Magic(d);return designs=d;
        }
        public static bool Designed(string spec)=>spec!=null&&Designs().ContainsKey(spec);
        public static Design Get(string spec)=>spec!=null&&Designs().TryGetValue(spec,out var d)?d:null;
        public static IEnumerable<string> Specs=>Designs().Keys;
        /// <summary>A fresh tier of `spec`'s Standard model (null when it has none): every section in the source weapon frame.</summary>
        public static WeaponMesh Build(string spec,int tier)
        {
            var d=Get(spec);if(d==null)return null;tier=Math.Max(Full,Math.Min(Low,tier));var mesh=new WeaponMesh{Spec=spec,Tier=tier};
            var sculpt=new WeaponSculpt(mesh,tier);sculpt.Into(d.Bone);d.Build(sculpt);mesh.Sections.RemoveAll(s=>s.Positions.Count==0);
            if(d.Dual)
            {
                // The off-hand copy: the same sections on the left hand, mirrored across x when the design asks for it.
                var right=mesh.Sections.ToArray();
                foreach(var s in right)
                {
                    var copy=mesh.Section(LeftHand,s.Stretch);bool mirror=d.MirrorLeft;int start=copy.Positions.Count;
                    for(int i=0;i<s.Positions.Count;i++)
                    {
                        var p=s.Positions[i];var n=s.Normals[i];if(mirror){p.X=-p.X;n.X=-n.X;}
                        copy.Positions.Add(p);copy.Normals.Add(n);copy.Colors.Add(s.Colors[i]);copy.Emission.Add(s.Emission[i]);
                    }
                    for(int i=0;i+2<s.Indices.Count;i+=3){copy.Indices.Add(start+s.Indices[i]);copy.Indices.Add(start+s.Indices[mirror?i+2:i+1]);copy.Indices.Add(start+s.Indices[mirror?i+1:i+2]);}
                }
            }
            return mesh;
        }
        /// <summary>
        /// The dropped / thrown copy (the 'loose-' bake): one weapon, every section merged on the model root (bone -1), full tier.
        /// </summary>
        public static WeaponMesh Loose(string spec)
        {
            var built=Build(spec,Full);if(built==null)return null;var d=Get(spec);var mesh=new WeaponMesh{Spec=spec,Tier=Full};var o=mesh.Section(-1,WeaponStretch.None);
            foreach(var s in built.Sections)
            {
                if(d.Dual&&s.Bone==LeftHand)continue;int start=o.Positions.Count;
                o.Positions.AddRange(s.Positions);o.Normals.AddRange(s.Normals);o.Colors.AddRange(s.Colors);o.Emission.AddRange(s.Emission);foreach(int i in s.Indices)o.Indices.Add(start+i);
            }
            return mesh;
        }
        /// <summary>
        /// The reach stretch on a Standard section, applied after the draw's weapon matrix (which, as for the Prototype models,
        /// already scales the weapon frame's z by reach: character-animation.ts:57). The rigid hilt (pommel, grip, guard) is
        /// scaled back to its own length; the blade maps [base, tip] onto [base, tip x reach], so the stretched tip lands exactly
        /// where the trails, the floor guard and the sweeps read the stretched blade-tip socket, and the grip keeps its length.
        /// Returns the local z scale and offset to apply (z' = z * scale + offset) inside the reach-scaled frame.
        /// </summary>
        public static bool Stretch(string spec,WeaponStretch kind,float reach,out float scale,out float offset)
        {
            scale=1;offset=0;var d=Get(spec);if(d==null||kind==WeaponStretch.None||!(reach>0)||Math.Abs(reach-1)<1e-4f||float.IsNaN(d.StretchBase))return false;
            if(kind==WeaponStretch.Rigid){scale=1/reach;return true;}
            float b=d.StretchBase,t=d.StretchTip,s=(t*reach-b)/(t-b);
            // In the reach-scaled frame a local z ends at z*reach; the blade point z should end at b + (z - b) * s.
            scale=s/reach;offset=b*(1-s)/reach;return true;
        }
        /// <summary>Where a section point at local z ends along the weapon frame after the reach stretch (for the checks).</summary>
        public static float Stretched(string spec,WeaponStretch kind,float reach,float z)
        {
            if(!(reach>0))reach=1;
            return Stretch(spec,kind,reach,out float scale,out float offset)?(z*scale+offset)*reach:z*reach;
        }

        // ---- shared palette (sRGB), close to weapon-visuals.ts so both series read as one family ----
        internal const int Steel=0x8fa6ac,SteelBright=0xc4d2d6,Edge=0xeaf6f4,Dark=0x263a43,GunMetal=0x24333b,Guard=0x5d7079,Black=0x182025,
            Gold=0xe3b779,GoldDark=0xa87b3c,Wrap=0x3b2a24,WrapDark=0x271b17,Polymer=0x33414a,PolymerDark=0x27323a,Rubber=0x15191d,Brass=0xd6a24a;
        internal static WeaponPaint Paint(int hex,float sheen=0)=>WeaponPaint.Solid(hex,sheen);
        internal static readonly WeaponPaint AccentStrip=WeaponPaint.Accent(1.7f,.4f),AccentDim=WeaponPaint.Accent(.9f,.3f);
        internal static Vector3 V(float x,float y,float z)=>new Vector3(x,y,z);
        internal const float Pi=(float)Math.PI;
    }
}
