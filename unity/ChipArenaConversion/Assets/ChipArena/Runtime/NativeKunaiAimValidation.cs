#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        private void ValidateKunaiAim(Action<string> setup,Action<float> advance,NativePilot a,NativePilot b,GameObject wall)
        {
            void Check(bool ok,string why){if(!ok)throw new Exception("Native Kunai aim: "+why);}
            void Prepare(float height=0)
            {
                setup("daggers");b.Body.position=new Vector3(1000,100+height,1012);Physics.SyncTransforms();
                // Deliberately wrong camera/floor intent, as reported by the owner. The host must use the lock identity.
                a.Input=new InputFrame{LockTarget=b.Seat.id,Yaw=0,Pitch=-35,AbilityTarget=new V3(1000,98,1003)};
            }
            NativeMeleeShot Release()
            {advance(a.Special.Release-Time+.001f);StepSpecial(a);Check(MeleeShots.Count==1,"single release");return MeleeShots[0];}
            foreach(float height in new[]{-4,0,4})
            {
                Prepare();Check(TryMeleeAbility(a,"e")&&a.Special.LockTarget==b.Seat.id,"host accepts live lock");
                // Read current target height when releasing, not the height at button press.
                b.Body.position+=Vector3.up*height;Physics.SyncTransforms();var shot=Release();
                Vector3 expected=Vec(NativeMeleeKitRules.KunaiAim(Vec(shot.Position),Vec(b.Body.position+Vector3.up*.2f),30,18));
                Check(Vector3.Distance(shot.Velocity,expected*30)<.001f,"re-aim at release height");
                var frame=NativeFrameCodec.Clone(NetworkFrame());var effect=frame.effects.Single(f=>f.type=="melee"&&f.id==shot.Id);
                Check(effect.velocity==shot.Velocity&&effect.position==shot.Position,"wire retains authoritative trajectory");
                for(int tick=0;tick<90&&a.MeleeKit.MarkTarget==0;tick++){advance(Rules.Dt);StepMeleeShots();}
                Check(a.MeleeKit.MarkTarget==b.Seat.id&&b.Hp<100,"real swept projectile hits and marks at height "+height);
            }
            Prepare();a.Input.LockTarget=0;TryMeleeAbility(a,"e");var free=a.Special.Aim;var loose=Release();
            Check(Vector3.Distance(loose.Velocity.normalized,Vec(free))<.0001f,"unlocked manual throw unchanged");
            foreach(string refusal in new[]{"missing","dead","disconnected","ally","range","cover"})
            {
                Prepare();
                if(refusal=="missing")a.Input.LockTarget=999999;if(refusal=="dead")b.Hp=0;if(refusal=="disconnected")b.Seat.connected=false;
                bool friendly=FriendlyFire;int team=b.Seat.team;
                if(refusal=="ally"){Match.Teams=true;FriendlyFire=true;b.Seat.team=a.Seat.team;}
                if(refusal=="range")b.Body.position+=Vector3.forward*20;if(refusal=="cover")wall.SetActive(true);Physics.SyncTransforms();
                Check(TryMeleeAbility(a,"e")&&a.Special.LockTarget==0,"invalid lock falls back to manual aim: "+refusal);
                FriendlyFire=friendly;b.Seat.team=team;
            }
            Prepare();TryMeleeAbility(a,"e");var committed=a.Special.Aim;b.Hp=0;b.Body.position+=Vector3.up*5;Physics.SyncTransforms();
            var last=Release();Check(Vector3.Distance(last.Velocity,Vec(committed)*30)<.001f,"lost lock retains last valid aim without retargeting");
            Prepare();TryMeleeAbility(a,"e");var covered=Release();wall.SetActive(true);Physics.SyncTransforms();
            for(int tick=0;tick<30&&MeleeShots.Count>0;tick++){advance(Rules.Dt);StepMeleeShots();}
            Check(a.MeleeKit.MarkTarget==0&&b.Hp==100&&MeleeShots.Count==0,"corrected trajectory still collides with cover");
            Debug.Log("PASS native Kunai lock: current release height, swept hit/mark, wire trajectory, manual/lost-lock fallback, hostile/live/range/cover validation");
        }
    }
}
#endif
