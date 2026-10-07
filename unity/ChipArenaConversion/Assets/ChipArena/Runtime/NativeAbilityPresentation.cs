using UnityEngine;

namespace ChipArena.UnityConversion
{
    public sealed partial class ArenaPresentation
    {
        public void RenderAbilities(NativeArena arena,Vector3? preview=null)
        {
            foreach(var b in batches)b.Clear();
            foreach(var shot in arena.AbilityShots)
            {
                int color=shot.Owner.Pose.MagicVisual>0?6+shot.Owner.Pose.MagicVisual:6;
                Vector3 scale=Vector3.one*Mathf.Max(.16f,shot.Radius*2);
                if(shot.Definition.id=="tidal-wave")scale=new Vector3(2,.8f,.35f);
                Add(color,Matrix4x4.TRS(shot.Position,shot.Velocity.sqrMagnitude>.01f?Quaternion.LookRotation(shot.Velocity):Quaternion.identity,scale));
                if(shot.Definition.id=="ball-lightning")Ring(shot.Position,shot.Radius*1.5f,color,arena.Time*80);
            }
            foreach(var zone in arena.AbilityZones)
            {
                int color=zone.Owner.Pose.MagicVisual>0?6+zone.Owner.Pose.MagicVisual:6;
                Ring(zone.Position+Vector3.up*.06f,zone.Radius,color,arena.Time*15);
                if(zone.Kind=="bomb")
                {
                    float pulse=.3f+.1f*Mathf.Sin(arena.Time*16);
                    Add(color,Matrix4x4.TRS(zone.Position+Vector3.up*.14f,Quaternion.Euler(0,45,0),new Vector3(pulse,.12f,pulse)));
                }
                else if(zone.Kind=="black-hole")
                {
                    Add(10,Matrix4x4.TRS(zone.Position+Vector3.up*.7f,Quaternion.Euler(arena.Time*50,arena.Time*80,0),Vector3.one*.75f));
                    Ring(zone.Position+Vector3.up*.8f,zone.Radius*.4f,10,-arena.Time*70);
                }
                else for(int i=0;i<12;i++)
                {float a=i*Mathf.PI/6;Vector3 p=zone.Position+new Vector3(Mathf.Cos(a)*zone.Radius*.65f,.15f,Mathf.Sin(a)*zone.Radius*.65f);Add(7,Matrix4x4.TRS(p,Quaternion.identity,new Vector3(.13f,.25f+.15f*Mathf.Sin(arena.Time*12+i),.13f)));}
            }
            foreach(var fx in arena.AbilityEffects)
            {
                int color=fx.Element>0?6+fx.Element:6;
                if(fx.Beam){Vector3 d=fx.Position-fx.From;if(d.sqrMagnitude>.001f)Add(color,Matrix4x4.TRS((fx.Position+fx.From)*.5f,Quaternion.LookRotation(d),new Vector3(.055f,.055f,d.magnitude)));}
                else Ring(fx.Position,fx.Radius*(1-(fx.Until-arena.Time)/.22f),color,0);
            }
            if(preview.HasValue)
            {
                var d=arena.Content.Ability(arena.Local.Seat.spec,"e");
                Ring(preview.Value+Vector3.up*.08f,Mathf.Max(.3f,NativeAbilityRules.Radius(d,arena.Local.Stats)),4,0);
            }
            DrawBatches(null,0,false);
        }
        private void Ring(Vector3 center,float radius,int color,float angle)
        {
            const int count=32;float segment=2*Mathf.PI*radius/count;
            for(int i=0;i<count;i++)
            {float a=(i*360f/count+angle)*Mathf.Deg2Rad;Add(color,Matrix4x4.TRS(center+new Vector3(Mathf.Sin(a)*radius,0,Mathf.Cos(a)*radius),Quaternion.Euler(0,a*Mathf.Rad2Deg+90,0),new Vector3(.055f,.05f,Mathf.Max(.01f,segment))));}
        }
    }
}
