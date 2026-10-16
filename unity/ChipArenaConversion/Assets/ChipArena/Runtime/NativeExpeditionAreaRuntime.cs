using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Stopwatch=System.Diagnostics.Stopwatch;

namespace ChipArena.UnityConversion
{
    // Foundation only: not selected by the live Expedition launch path until all
    // cohort/wire/native/Windows gates have passed. Never reuse ConversionGame.match.
    public sealed class NativeExpeditionAreaRuntime : IExpeditionAreaRuntime
    {
        public string Area {get;}
        public NativeArena Arena {get;private set;}
        public Scene Scene {get;private set;}
        public ExpeditionRuntimeLoad Load {get;private set;}=ExpeditionRuntimeLoad.Created;
        public int Occupants {get;private set;}
        public InputFrame Input;
        public long LastTick {get;private set;}=-1;
        public double StepMs {get;private set;}
        public double PhysicsMs {get;private set;}
        public double NavigationMs {get;private set;}
        private readonly Action<Scene> release;private bool ownsScene;
        public NativeExpeditionAreaRuntime(string area,ConversionContent content,string map,Action<NativeMatch> configure,
            Func<string,Scene> createScene=null,Action<Scene> releaseScene=null)
        {
            if(string.IsNullOrWhiteSpace(area)||content==null||configure==null)throw new ArgumentException("Area, content and match configuration required.");
            Area=area;release=releaseScene??(s=>{SceneManager.UnloadSceneAsync(s);});
            try
            {
                Scene=(createScene??(name=>SceneManager.CreateScene(name,new CreateSceneParameters(LocalPhysicsMode.Physics3D))))("Expedition area "+area);
                if(!Scene.IsValid()||!Scene.isLoaded||!Scene.GetPhysicsScene().IsValid()||Scene.GetPhysicsScene()==Physics.defaultPhysicsScene)
                    throw new InvalidOperationException("Area requires a loaded independent physics scene.");
                ownsScene=true;var match=new NativeMatch(content);configure(match);
                Arena=new NativeArena(content,match,map,areaScene:Scene);
                Arena.SetPresentationEnabled(false);Arena.Reset();
            }
            catch{Arena?.Dispose();if(ownsScene&&Scene.IsValid())release(Scene);Load=ExpeditionRuntimeLoad.Faulted;throw;}
        }
        public void Ready(Action<NativeArena> initialize=null)
        {
            if(Load!=ExpeditionRuntimeLoad.Created)throw new InvalidOperationException("Area is already initialized.");
            try{initialize?.Invoke(Arena);Load=ExpeditionRuntimeLoad.Ready;}
            catch{Load=ExpeditionRuntimeLoad.Faulted;throw;}
        }
        public void SetOccupants(int count)
        {if(count<0||count>4||Load==ExpeditionRuntimeLoad.Disposed)throw new ArgumentOutOfRangeException(nameof(count));Occupants=count;}
        public void SetPresented(bool shown)
        {if(Load==ExpeditionRuntimeLoad.Disposed)throw new ObjectDisposedException(nameof(NativeExpeditionAreaRuntime));Arena.SetPresentationEnabled(shown);}
        public void Step(long hostTick)
        {
            if(hostTick<0)throw new ArgumentOutOfRangeException(nameof(hostTick));
            if(Load!=ExpeditionRuntimeLoad.Ready||Occupants==0||hostTick<=LastTick)return;
            LastTick=hostTick;long start=Stopwatch.GetTimestamp();double nav=Arena.NavigationMs;PhysicsMs=0;
            try
            {
                bool held=Arena.Pve?.Holding==true;Arena.Step(Input);Input=Input.HeldOnly();
                if(!held&&Arena.Pve?.Holding!=true)
                {
                    long physics=Stopwatch.GetTimestamp();Arena.BeforePhysics();Arena.PhysicsWorld.Simulate(1f/60);Arena.AfterPhysics();PhysicsMs=Elapsed(physics);
                }
            }
            catch{Load=ExpeditionRuntimeLoad.Faulted;throw;}
            finally{StepMs=Elapsed(start);NavigationMs=Arena.NavigationMs-nav;}
        }
        private static double Elapsed(long start)=>(Stopwatch.GetTimestamp()-start)*1000d/Stopwatch.Frequency;
        public void Dispose()
        {
            if(Load==ExpeditionRuntimeLoad.Disposed)return;Load=ExpeditionRuntimeLoad.Disposed;Occupants=0;
            try{Arena?.Dispose();}finally{if(ownsScene&&Scene.IsValid())release(Scene);}
        }
    }
}
