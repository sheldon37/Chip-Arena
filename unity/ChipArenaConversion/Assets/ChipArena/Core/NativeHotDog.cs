using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // The Hot Dog (owner 2026-11-04: a playable skin; 2026-11-08, with the model made in Tripo: "Implement this model as a dev skin
    // (Do clean up on it if necessary)", "Id like to also give the body physics (Its a hotdog lol)"; docs/HOT-DOG-SKIN.md):
    // the owner's model on the model's own skeleton, worn in place of the whole outfit. It is picked as a piece of the outfit map
    // (NativeLook's upper slot, "upper:hot-dog"), so the Locker, prefs, the seat frames, replays, portraits and corpses carry it as
    // they carry any other top, and it is DEV only (NativeLook.DevOnly, as Sheldon's clothes are). tools/gear/hotdog_tripo.py
    // retargets the owner's FBX into a local bank (kept out of git, shipped in the Drive zip); Runtime/NativeHotDogPresentation.cs
    // draws it, its sausage jiggling on its own bones (Jiggle). Presentation only: no rule, hit volume, timing or network
    // authority reads it.
    public static class NativeHotDog
    {
        public const string Id="hot-dog",Label="Hot Dog";

        // ---- Hats on the crown (owner 2026-11-11: "Hats don't fit on hotdog model correctly") ----
        // Every hat is made for the pilot's skull (NativeModelHatMeshes: its band BandY over the head bone, BandX to each side and
        // BandDepth ahead and behind BandZ; a closed hat's crown at CrownTop). The sausage's crown, in the head bone's frame at the
        // bind, is that skull's size at the band (a half width of 0.12, as deep) but sits 2.2 cm further ahead, and its dome rises
        // to 0.24, 2.5 cm over a closed hat's crown; its three sprigs stand 3 to 5 cm over that. So on the Hot Dog each hat is
        // moved, as the helmet moves hats (NativeRobotHelm.HatFit): scaled about the band's middle (HatPivot) and lifted and moved
        // ahead by its own measure, so its band sits round the sausage and its crown clears the dome; a closed hat (HidesSprigs)
        // also hides the sprigs under it (the runtime draws the mesh without them). The runtime places the hat in the frame of the
        // crown's own skinning (its bones' blend), so it stays on the crown however the head turns and as the sausage jiggles.
        public static readonly Vector3 HatPivot=new Vector3(0,NativeModelHatMeshes.BandY,NativeModelHatMeshes.BandZ);
        private static readonly string[] FitHats={"cap","beanie","top","wizard","cowboy","cone","crown","antenna","halo",NativeAuditorHalo.Hat,NativeHatLooks.SheldonHead};
        private static readonly float[] FitScale={1,1,1,1,1,1,1,1,1,1,1},
            FitLift={.03f,.03f,.03f,.045f,.03f,.03f,.03f,.03f,.03f,.03f,.03f}, // (the wizard's floppy brim higher, clear of the eyes)
            FitAhead={.022f,.022f,.022f,.022f,.022f,.022f,.022f,.022f,.022f,.022f,.022f};
        /// <summary>Hat `hat`'s fit on the Hot Dog's crown: a point p of it (the head bone's frame) goes to p * scale + move (a hat
        /// not listed takes the cap's).</summary>
        public static void HatFit(string hat,out float scale,out Vector3 move)
        {
            int i=Math.Max(0,Array.IndexOf(FitHats,hat));scale=FitScale[i];move=HatPivot*(1-scale)+new Vector3(0,FitLift[i],FitAhead[i]);
        }
        /// <summary>Whether a hat covers the crown, so the sprigs are hidden under it: the closed hats (those that press the
        /// pilot's hair) and the Sheldon Head's hood.</summary>
        public static bool HidesSprigs(string hat)=>NativeModelHats.Presses(hat);
        /// <summary>The sprigs: the parts of the mesh wholly within this far of its top (m), each smaller than SprigMost vertices.</summary>
        public const float SprigDepth=.08f;public const int SprigMost=200;
        /// <summary>Whether a look wears it. Allocation free.</summary>
        public static bool Worn(string look)=>NativeLook.Of(look,NativeLook.Upper)==Id;

        // ---- The sausage's jiggle ----
        // A damped spring on the sausage's top (its offset from where the pose puts it, m, world): it lags behind the body's
        // changes of velocity (a start, a stop, a turn of the run, a jump's take-off and landing) and wobbles back. It steps on
        // the pilot's floor point, not the pelvis, so the run's own bob (already in the pose) doesn't set it bouncing. Its sideways
        // part leans the sausage (more toward the top: a bend), its upright part squashes or stretches it (a landing squashes
        // it). The runtime turns it into the sausage bones' frames (NativeHotDogPresentation.cs HotDogJiggle); the arms and legs
        // never move with it.
        /// <summary>The sausage's height over the pelvis (m; the lean's lever and the squash's measure).</summary>
        public const float Height=.85f;
        /// <summary>The spring's natural frequency (rad/s) and damping ratio: a soft wobble, about two and a half a second, dying
        /// away in about four.</summary>
        public const float Omega=15,Damping=.25f;
        /// <summary>How much of a change in the body's velocity the top keeps, sideways and upright (it lags by this share, then
        /// springs back): a start or a stop leans it well over, a landing squashes it.</summary>
        public const float Kick=.8f,KickUp=.35f;
        /// <summary>The most a frame's change of velocity counts (m/s: a teleport or a respawn is not a lurch).</summary>
        public const float KickMax=6;
        /// <summary>The furthest it leans (m, at the top) and the most it squashes or stretches (a share of its height).</summary>
        public const float MaxLean=.14f,MaxSquash=.16f;
        /// <summary>The longest step it integrates in one go (s).</summary>
        public const float Substep=1f/240;

        public struct Jiggle
        {
            public Vector3 Offset,Velocity,LastAt,LastVelocity;public bool Primed;
        }

        /// <summary>Advance `j` by `dt` seconds with the body's floor point now at `at` (world, m). A first frame, a long gap or a
        /// jump of more than two metres starts it at rest.</summary>
        public static void Step(ref Jiggle j,Vector3 at,float dt)
        {
            if(!j.Primed||!(dt>0)||dt>.25f||Vector3.DistanceSquared(at,j.LastAt)>4){j=default;j.LastAt=at;j.Primed=true;return;}
            var velocity=(at-j.LastAt)/dt;var change=velocity-j.LastVelocity;
            float size=change.Length();if(size>KickMax)change*=KickMax/size;
            j.Velocity-=new Vector3(change.X*Kick,change.Y*KickUp,change.Z*Kick);
            int n=Math.Max(1,(int)Math.Ceiling(dt/Substep));float h=dt/n;
            for(int i=0;i<n;i++)
            {
                var accel=-Omega*Omega*j.Offset-2*Damping*Omega*j.Velocity;
                j.Velocity+=accel*h;j.Offset+=j.Velocity*h;
                Clamp(ref j);
            }
            j.LastAt=at;j.LastVelocity=velocity;
        }

        // The offset held within its lean and squash (the velocity into a limit stopped).
        private static void Clamp(ref Jiggle j)
        {
            var side=new Vector3(j.Offset.X,0,j.Offset.Z);float lean=side.Length();
            if(lean>MaxLean)
            {
                var dir=side/lean;j.Offset=new Vector3(dir.X*MaxLean,j.Offset.Y,dir.Z*MaxLean);
                float out_=Vector3.Dot(new Vector3(j.Velocity.X,0,j.Velocity.Z),dir);if(out_>0)j.Velocity-=dir*out_;
            }
            float most=MaxSquash*Height;
            if(j.Offset.Y>most){j.Offset.Y=most;if(j.Velocity.Y>0)j.Velocity.Y=0;}
            else if(j.Offset.Y<-most){j.Offset.Y=-most;if(j.Velocity.Y<0)j.Velocity.Y=0;}
        }

        /// <summary>The lean the offset makes (radians about `axis`, which is up crossed with the offset's direction, so a turn of
        /// `angle` about it, Unity's AngleAxis, tips the top the offset's way) and its squash (positive: shorter).</summary>
        public static void Pose(in Jiggle j,out Vector3 axis,out float angle,out float squash)
        {
            var side=new Vector3(j.Offset.X,0,j.Offset.Z);float lean=side.Length();
            angle=(float)Math.Atan2(lean,Height);
            axis=lean>1e-5f?Vector3.Cross(Vector3.UnitY,side/lean):Vector3.UnitX;
            squash=-j.Offset.Y/Height;
        }

        /// <summary>How much of the lean a sausage bone `height` m over the pelvis takes (0 at the pelvis, 1 at the top, more
        /// toward the top so it bends).</summary>
        public static float Bend(float height)
        {
            float f=Math.Max(0,Math.Min(1,height/Height));return (float)Math.Pow(f,1.4);
        }
    }
}
