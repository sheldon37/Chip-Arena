using UnityEngine;

namespace ChipArena.UnityConversion
{
    public sealed partial class ArenaPresentation
    {
        public void RenderAbilities(NativeArena arena,Vector3? preview=null)
        {
            foreach(var b in batches)b.Clear();SourceWorld(arena);DeathPresentation(arena);WorldEffects(arena);
            foreach(var shot in arena.AbilityShots)
            {
                int color=shot.Owner.Pose.MagicVisual>0?6+shot.Owner.Pose.MagicVisual:6;
                Vector3 scale=Vector3.one*Mathf.Max(.16f,shot.Radius*2);
                if(shot.Definition.id=="tidal-wave")scale=new Vector3(2,.8f,.35f);
                Add(color,Matrix4x4.TRS(shot.Position,shot.Velocity.sqrMagnitude>.01f?Quaternion.LookRotation(shot.Velocity):Quaternion.identity,scale));
            }
            foreach(var shot in arena.MeleeShots)
            {
                float age=arena.Time-shot.At;
                Quaternion direction=shot.Velocity.sqrMagnitude>.001f?Quaternion.LookRotation(shot.Velocity):Quaternion.identity;
                Quaternion spin=shot.Shield?Quaternion.Euler(90,0,age*24*Mathf.Rad2Deg):Quaternion.Euler(age*2160,0,0);
                var matrix=Matrix4x4.TRS(shot.Position,direction*spin,Vector3.one);
                if(!shot.Shield)matrix*=Matrix4x4.Translate(new Vector3(0,0,-.44f));
                Weapon(matrix,shot.Shield?3:1,shot.Original.Seat.id==arena.Local?.Seat.id?0:1);
            }
            foreach(var impact in arena.MeleeImpacts)
            {
                float age=arena.Time-impact.At,life=Mathf.Clamp01(1-age/1.1f);
                // Low voxel dust across the slam volume; source intentionally has no outlined damage ring.
                for(int i=0;i<20;i++)
                {
                    float angle=i*2.399963f,r=impact.Radius*Mathf.Sqrt((i+.5f)/20)*Mathf.Clamp01(age*4);
                    Vector3 at=impact.Position+new Vector3(Mathf.Sin(angle)*r,.06f+Mathf.Sin(Mathf.Clamp01(age)*Mathf.PI)*(.12f+(i%3)*.07f),Mathf.Cos(angle)*r);
                    Add(4,Matrix4x4.TRS(at,Quaternion.Euler(i*17,age*60,i*29),Vector3.one*(.10f+.04f*(i%3))*life));
                }
            }
            foreach(var zone in arena.AbilityZones)
            {
                int color=zone.Owner.Pose.MagicVisual>0?6+zone.Owner.Pose.MagicVisual:6;
                if(zone.Kind=="bomb")
                {
                    float pulse=.3f+.1f*Mathf.Sin(arena.Time*16);
                    Add(color,Matrix4x4.TRS(zone.Position+Vector3.up*.14f,Quaternion.Euler(0,45,0),new Vector3(pulse,.12f,pulse)));
                }
                else if(zone.Kind=="black-hole")
                {
                    Add(10,Matrix4x4.TRS(zone.Position+Vector3.up*.7f,Quaternion.Euler(arena.Time*50,arena.Time*80,0),Vector3.one*.75f));
                }
                else for(int i=0;i<12;i++)
                {float a=i*Mathf.PI/6;Vector3 p=zone.Position+new Vector3(Mathf.Cos(a)*zone.Radius*.65f,.15f,Mathf.Sin(a)*zone.Radius*.65f);Add(7,Matrix4x4.TRS(p,Quaternion.identity,new Vector3(.13f,.25f+.15f*Mathf.Sin(arena.Time*12+i),.13f)));}
            }
            foreach(var fx in arena.AbilityEffects)
            {
                int color=fx.Element>0?6+fx.Element:6;
                if(fx.Beam){Vector3 d=fx.Position-fx.From;if(d.sqrMagnitude>.001f)Add(color,Matrix4x4.TRS((fx.Position+fx.From)*.5f,Quaternion.LookRotation(d),new Vector3(.055f,.055f,d.magnitude)));}
            }
            if(preview.HasValue)
            {
                var d=arena.Content.Ability(arena.ViewPilot.Seat.spec,"e");
                Ring(preview.Value+Vector3.up*.08f,Mathf.Max(.3f,NativeAbilityRules.Radius(d,arena.ViewPilot.Stats)),4,0);
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
