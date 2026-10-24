using System;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        private Vector3 AuthorityAim(NativePilot p,float range)
        {
            var eye=p.Body.position+Vector3.up*.52f;var direction=Vec(GunRules.Direction(p.Input.Yaw,p.Input.Pitch));
            if(root.scene.GetPhysicsScene().Raycast(eye,direction,out var hit,range,WorldMask,QueryTriggerInteraction.Ignore))return hit.point-direction*.03f;
            return eye+direction*range;
        }
        // Called by the genuine Unity build gate in an isolated preview physics scene.
        public void ValidateParityCombat()
        {
            void Check(bool ok,string message){if(!ok)throw new Exception("Parity combat: "+message);}
            var a=Pilots[0];var b=Pilots[1];
            b.StunEnd=0;float hp=b.Hp;Damage(b,a,5,Vector3.forward,0,false);Check(b.Hp<hp&&b.StunEnd==0,"gun hit cannot add generic stun");
            b.BlockEnd=Time+1;hp=b.Hp;AbilityHit(b,a,5,Vector3.zero,0);Check(b.Hp<hp,"ability damage bypasses generic block");
            hp=b.Hp;Damage(b,a,5,Vector3.zero,0,false);Check(b.Hp==hp,"primary contact remains blockable");b.BlockEnd=0;
            b.Roll=new RollValues{iframeStart=0,iframeSeconds=1};b.RollAt=Time;b.RollEnd=Time+1;hp=b.Hp;
            Damage(b,a,5,Vector3.zero,0,false,direct:false,primary:false);Check(b.Hp==hp,"splash respects roll i-frames");
            Damage(b,a,5,Vector3.zero,0,false,direct:false,primary:false,rollAvoidable:false);Check(b.Hp<hp,"burn/poison bypass roll without canceling it");Check(b.Roll!=null,"DoT cannot cancel a roll");
            a.Ready=a.ReloadEnd=Time+5;PressAbility(a,"e");Check(a.AbilityCast!=null&&a.BufferedSlot==null,"Q/E is independent of primary interval and reload");CancelAbilities(a);
            a.Input=new InputFrame{Yaw=0,Pitch=0,AbilityTarget=new V3(100,0,100)};
            var wall=new GameObject("Authority aim test wall");wall.layer=PropLayer;wall.transform.SetParent(root.transform);wall.transform.position=a.Body.position+Vector3.up*.52f+Vector3.forward*2;wall.AddComponent<BoxCollider>();Physics.SyncTransforms();
            Check(Vector3.Distance(AuthorityAim(a,20),a.Body.position)<3,"reported target cannot bypass a wall");UnityEngine.Object.DestroyImmediate(wall);
        }
    }
}
