using UnityEngine;

namespace ChipArena.UnityConversion
{
    public sealed partial class ArenaPresentation
    {
        // The local E aim point and whether it was clamped; NativeFieldPresentation draws the source's mote preview in RenderGlow.
        private Vector3? abilityPreview;private bool abilityPreviewClamped;
        public void RenderAbilities(NativeArena arena,Vector3? preview=null,bool previewClamped=false)
        {
            foreach(var b in batches)b.Clear();SourceWorld(arena);ArenaDressing(arena);DeathPresentation(arena);RenderRevives(arena);RenderWoundSpray(arena.Time);DeathBroadcasts(arena);WorldEffects(arena);
            // Bullets and ability shots are NativeShotPresentation's source models, the Siren's shots its own effects (RenderGlow).
            foreach(var shot in arena.MeleeShots)
            {
                float age=arena.Time-shot.At;
                Quaternion direction=shot.Velocity.sqrMagnitude>.001f?Quaternion.LookRotation(shot.Velocity):Quaternion.identity;
                Quaternion spin=shot.Shield?Quaternion.Euler(90,0,age*24*Mathf.Rad2Deg):Quaternion.Euler(age*2160,0,0);
                var matrix=Matrix4x4.TRS(shot.Position,direction*spin,Vector3.one);
                if(!shot.Shield)matrix*=Matrix4x4.Translate(new Vector3(0,0,-.44f));
                Weapon(matrix,shot.Shield?3:1,shot.Original.Seat.id==arena.Local?.Seat.id?0:1);
                ThrownEnchant(shot,matrix,arena.Time);
            }
            // Slam landings are the source's dust and stones (NativeMeleeFlashPresentation, in RenderGlow). Ability zones draw
            // with the chip fields (NativeFieldPresentation: glow, singularity core; NativeBlastPresentation: volume), and beam
            // records as their source events (teleport blasts, arc lines, L33t streaks, the toy shot: NativeBlastPresentation).
            abilityPreview=preview;abilityPreviewClamped=previewClamped;
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
