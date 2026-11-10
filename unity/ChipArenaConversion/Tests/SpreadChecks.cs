using System;
using System.IO;
using ChipArena.UnityConversion;
// The guns' spread and the reactive crosshair (owner 2026-11-10: "I think we need a reactive crosshair and spread. Currently I think we
// don't have initial spread on weapons"): Core NativeSpread's cones, their bloom and recovery, the aim's narrowing, the shot's disc, and
// that the rules and the crosshair read the same numbers.
internal static class SpreadChecks
{
    public static void Run(string root,ConversionContent data)
    {
        int n=0;void Check(bool ok,string why){if(!ok)throw new Exception("Spread: "+why);n++;}
        string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena",f));
        string[] guns={"pistol","machinegun","rifle"};
        foreach(var g in guns)
        {
            var p=NativeSpread.Of(g);var ads=data.Ads(g);
            Check(NativeSpread.Has(g)&&ads!=null,g+": a cone and an aim row");
            float hip=NativeSpread.Cone(p,0,false,0,1);Check(hip>=.8f&&hip<=3f,g+": a cone from the hip at rest ("+hip+"°)");
            Check(NativeSpread.Cone(p,1,false,0,1)>hip&&NativeSpread.Cone(p,0,true,0,1)>NativeSpread.Cone(p,1,false,0,1)-p.Move+.5f,g+": moving and the air open it");
            Check(Math.Abs(NativeSpread.Cone(p,2,false,0,1)-NativeSpread.Cone(p,1,false,0,1))<1e-5f,g+": a sprint opens it no more than a run");
            float aimed=NativeSpread.Cone(p,0,false,0,NativeSpread.Aim(true,ads.spread,ads.settle,10));
            Check(Math.Abs(aimed-hip*(float)ads.spread)<1e-4f&&NativeSpread.Aim(true,ads.spread,ads.settle,0)==1&&NativeSpread.Aim(false,ads.spread,ads.settle,10)==1,g+": aiming narrows it to the aim row's spread as it settles");
            // Firing at the gun's own rate: the bloom climbs, never past the cap, and is gone within a second and a half of the last shot.
            var w=Array.Find(data.weapons,x=>x.id==g);Check(w!=null&&w.interval>0,g+": its weapon row");
            float bloom=0,at=-100,t=0,most=0;for(int i=0;i<40;i++,t+=(float)w.interval){bloom=NativeSpread.Kick(NativeSpread.Decay(bloom,t-at,p),p);at=t;most=Math.Max(most,bloom);}
            Check(most<=p.Cap+1e-5f&&most>=p.Kick,g+": the bloom climbs under its cap ("+most.ToString("0.00")+"°)");
            Check(NativeSpread.Decay(bloom,1.5f,p)==0&&NativeSpread.Decay(bloom,p.Delay*.5f,p)==bloom,g+": it holds a moment, then recovers within 1.5 s");
        }
        // The machine gun climbs to its cap over a burst; a pistol fired as fast as it goes blooms but stays under it; the rifle settles between shots.
        float Burst(string g,int shots){var p=NativeSpread.Of(g);var w=Array.Find(data.weapons,x=>x.id==g);float b=0,at=-100,t=0;for(int i=0;i<shots;i++,t+=(float)w.interval){b=NativeSpread.Kick(NativeSpread.Decay(b,t-at,p),p);at=t;}return b;}
        Check(Math.Abs(Burst("machinegun",16)-NativeSpread.MachineGun.Cap)<1e-4f,"the machine gun reaches its cap over about a second of fire");
        Check(Burst("pistol",7)>NativeSpread.Pistol.Kick&&Burst("pistol",7)<NativeSpread.Pistol.Cap,"a pistol's magazine at its fastest blooms, under the cap");
        Check(Burst("rifle",3)<=NativeSpread.Rifle.Kick+1e-4f,"the rifle recovers between its shots");
        Check(!NativeSpread.Has("bow")&&!NativeSpread.Has(NativeBombs.Spec)&&!NativeSpread.Has("sword")&&NativeSpread.Of("bow").Hip==0,"the bow, the Bombadier and the blades keep their own");
        // The rifle is exact through its scope.
        Check(NativeSpread.Cone(NativeSpread.Rifle,1,true,4,NativeSpread.Aim(true,data.Ads("rifle").spread,data.Ads("rifle").settle,1))==0,"the rifle's settled scope fires true");
        // The shot's disc: inside the unit disc and uniform over its area (a quarter inside half its radius).
        var rng=new Random(7);int inner=0;const int draws=20000;double far=0;
        for(int i=0;i<draws;i++){NativeSpread.Disc(rng.NextDouble(),rng.NextDouble(),out float x,out float y);double r=Math.Sqrt(x*x+y*y);far=Math.Max(far,r);if(r<.5)inner++;}
        Check(far<=1.0001&&Math.Abs(inner/(double)draws-.25)<.02,"the shot lands uniformly inside the cone ("+(inner/(double)draws).ToString("0.000")+" inside half its radius)");
        // The value the shot code takes is twice the cone's tangent; on the screen, a cone of half the field of view reaches the edge.
        Check(Math.Abs(NativeSpread.Value(45f)/2-Math.Tan(30*Math.PI/180))<1e-9&&Math.Abs(NativeSpread.Value(2)/2-Math.Tan(2*Math.PI/180))<1e-9,"the shot's value is the cone's tangent each way (capped at 30°)");
        Check(Math.Abs(NativeSpread.Pixels(30,60,1080)-540)<.01f&&Math.Abs(NativeSpread.Pixels(1,60,1080)-1080*.5*Math.Tan(Math.PI/180)/Math.Tan(Math.PI/6))<.01f,"the crosshair's arms sit on the cone's edge on the screen");
        // The rules and the crosshair read the same numbers.
        var shots=R("Runtime/NativeProjectiles.cs");
        Check(shots.Contains("bool coned=!fan&&bow==null&&NativeSpread.Has(p.Seat.spec);")&&shots.Contains("spread=NativeSpread.Value(NativeSpread.Cone(coneOf,new Vector2(p.MoveVelocity.x,p.MoveVelocity.z).magnitude/NativeSpread.RunSpeed,!p.Grounded,bloom,aimNarrow));p.SpreadBloom=overdrive?bloom:NativeSpread.Kick(bloom,coneOf);p.SpreadBloomAt=Time;")
            &&shots.Contains("NativeSpread.Disc(random.NextDouble(),random.NextDouble(),out float coneX,out float coneY);dir=(coneLine+(coneSide*coneX+coneRise*coneY)*(float)(spread*.5)).normalized;"),"the shot fires inside the cone and kicks the bloom");
        Check(shots.IndexOf("bool coned=",StringComparison.Ordinal)<shots.IndexOf("if(Has(p,\"wild-bullet-hose\"))spread+=",StringComparison.Ordinal),"the chips' spread changes still apply on top of the cone");
        Check(R("Runtime/NativeArena.cs").Contains("public float SpreadBloom,SpreadBloomAt=-100;"),"the pilot keeps its bloom");
        var hud=R("Runtime/NativeFightHud.cs");
        Check(hud.Contains("return NativeSpread.Cone(sp,new Vector2(a.Velocity.x,a.Velocity.z).magnitude/NativeSpread.RunSpeed,!a.Grounded,NativeSpread.Decay(crossBloom,now-crossBloomAt,sp),aim);")
            &&hud.Contains("b=NativeSpread.Kick(b,sp);")&&hud.Contains("NativeSpread.Pixels(cone,cameraView.fieldOfView,Screen.height)"),"the crosshair follows the same cone, bloom and aim, at the camera's field of view");
        Console.WriteLine("Spread checks passed ("+n+")");
    }
}
