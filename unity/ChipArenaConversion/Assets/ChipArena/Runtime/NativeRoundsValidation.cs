#if UNITY_EDITOR || ARENA_VALIDATE
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        public void ValidateRoundsCards()
        {
            var oldChips=Content.chips;string oldRevision=Content.contentRevision;var p=Pilots[0];var held=new Dictionary<string,int>(p.Seat.chips);var oldStats=p.Stats;
            void Check(bool yes,string why){if(!yes)throw new Exception("Native ROUNDS adaptations: "+why);}
            void Hold(string id){p.Seat.chips.Clear();p.Seat.chips[NativeRoundsCards.Prefix+id]=1;p.Stats=NativeContentRules.Stats(Content,p.Seat.spec,p.Seat.chips);}
            try
            {
                NativeRoundsCards.Apply(Content);
                foreach(var chip in NativeRoundsCards.Chips())
                {
                    Check(ImplementedChip(chip),"offered mechanics implemented: "+chip.id);
                    var copy=JsonUtility.FromJson<ContentChip>(JsonUtility.ToJson(chip));Check(copy.author==chip.author&&copy.sourceCard==chip.sourceCard,"Unity attribution round trip");
                }
                p.Seat.spec="pistol";p.Weapon=Content.Weapon("pistol");p.Hp=100;p.Seat.connected=true;
                Hold("phantom");p.BlockProcUsed=false;p.ShieldUntil=0;BlockProc(p,null,p.Body.position);Check(Math.Abs(p.ShieldUntil-Time-.6f)<.001f,"successful block protection");
                p.ShieldUntil=0;BlockProc(p,null,p.Body.position);Check(p.ShieldUntil==0,"one phase grant per block activation");
                Hold("hot-potato");int n=ChipFields.Count;ChipRoll(p);Check(ChipFields.Count==n+1&&ChipFields.Last().Kind=="fire","roll fire adaptation");
                foreach(var test in new[]{("smoke-grenade","time"),("hamper","frost")})
                {Hold(test.Item1);n=ChipFields.Count;ImpactChips(p,null,null,p.Body.position,Vector3.forward,nextAttack++,0);Check(ChipFields.Count==n+1&&ChipFields.Last().Kind==test.Item2,"impact field "+test.Item1);}
                Hold("snake-attack");ImpactChips(p,null,null,p.Body.position,Vector3.forward,nextAttack++,0);Check(Minions.Any(m=>m.Owner==p&&m.Kind=="toy"),"impact summon adaptation");
                Hold("excalibur");n=Bullets.Count;var basis=new NativeBullet{Owner=p.Seat.id,Original=p.Seat.id,Position=p.Body.position,Velocity=Vector3.forward*30,Damage=10,Life=2,Radius=.1f,Mods=new Dictionary<string,int>(p.Seat.chips)};
                // Move to clear space so the satellite clearance casts are deterministic.
                p.Body.position=new Vector3(1000,1000,1000);Physics.SyncTransforms();SpawnChipShots(p,new List<NativeBullet>{basis});
                Check(Bullets.Count==n+2&&Bullets.Skip(n).All(b=>b.Homing==150&&!b.Direct&&!b.ChipImpact&&Math.Abs(b.Damage-4)<.001f),"satellite damage and recursion guard");
                var negative=new NativeBullet{Mods=new Dictionary<string,int>{{"corkscrew",-1}}};Check(Has(negative,"corkscrew"),"legacy negative shot modifier retained");
                Debug.Log("PASS native ROUNDS adaptations: all 20 definitions, Unity credit codec, block phase, roll/impact fields, summon, satellites and legacy shot modifier");
            }
            finally{Content.chips=oldChips;Content.contentRevision=oldRevision;p.Seat.chips.Clear();foreach(var kv in held)p.Seat.chips[kv.Key]=kv.Value;p.Stats=oldStats;}
        }
    }
}
#endif
