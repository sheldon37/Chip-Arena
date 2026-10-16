using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    public enum ExpeditionRuntimeLoad { Created, Ready, Faulted, Disposed }
    public interface IExpeditionAreaRuntime : IDisposable
    {
        string Area { get; }
        int Occupants { get; }
        ExpeditionRuntimeLoad Load { get; }
        void Step(long hostTick);
        void SetPresented(bool shown);
    }
    // M1 owns exactly two logical areas. Empty runtimes retain encounter state but
    // do not advance. Membership/transfer authority is deliberately outside this owner.
    public sealed class ExpeditionAreaRegistry<T> : IDisposable where T:class,IExpeditionAreaRuntime
    {
        private readonly string hub,instance;private readonly Func<string,T> create;
        private readonly Dictionary<string,T> areas=new Dictionary<string,T>(StringComparer.Ordinal);
        private long lastTick=-1;private bool stepping,disposed,faulted;
        public ExpeditionAreaRegistry(string hub,string instance,Func<string,T> create)
        {
            if(string.IsNullOrWhiteSpace(hub)||string.IsNullOrWhiteSpace(instance)||hub==instance||create==null)throw new ArgumentException("Two distinct areas and a factory are required.");
            this.hub=hub;this.instance=instance;this.create=create;
        }
        public int Count=>areas.Count;
        public string Presented {get;private set;}
        public bool Faulted=>faulted;
        public T Find(string area)=>area!=null&&areas.TryGetValue(area,out var value)?value:null;
        public T Acquire(string area)
        {
            Guard();if(stepping)throw new InvalidOperationException("Area creation during a host step is forbidden.");
            if(area!=hub&&area!=instance)throw new ArgumentException("Area is outside this run.");
            if(areas.TryGetValue(area,out var existing))return existing;
            var runtime=create(area);
            if(runtime==null||runtime.Area!=area){runtime?.Dispose();throw new InvalidOperationException("Area factory returned the wrong identity.");}
            try{runtime.SetPresented(false);areas.Add(area,runtime);return runtime;}catch{runtime.Dispose();throw;}
        }
        public void Present(string area)
        {
            Guard();if(stepping)throw new InvalidOperationException("Cannot rebind presentation during a host step.");
            var selected=Find(area);if(selected==null||selected.Load!=ExpeditionRuntimeLoad.Ready)throw new InvalidOperationException("Displayed area is not ready.");
            foreach(var value in areas.Values)value.SetPresented(false);
            Presented=null;selected.SetPresented(true);Presented=area;
        }
        public bool Step(long hostTick,Action<double> sharedClock)
        {
            Guard();if(sharedClock==null||hostTick<0)throw new ArgumentException("A host tick and shared clock are required.");
            if(stepping)throw new InvalidOperationException("Reentrant area step.");
            if(hostTick<=lastTick)return false;
            // Claim before any callback. A partially failed tick must never be replayed.
            lastTick=hostTick;stepping=true;
            try
            {
                sharedClock(1d/60);
                StepOne(hub,hostTick);StepOne(instance,hostTick);return true;
            }
            catch{faulted=true;throw;}
            finally{stepping=false;}
        }
        private void StepOne(string id,long tick)
        {if(areas.TryGetValue(id,out var value)&&value.Occupants>0&&value.Load==ExpeditionRuntimeLoad.Ready)value.Step(tick);}
        private void Guard(){if(disposed)throw new ObjectDisposedException(nameof(ExpeditionAreaRegistry<T>));if(faulted)throw new InvalidOperationException("Area scheduler faulted; recover from a checkpoint.");}
        public void Dispose()
        {
            if(disposed)return;if(stepping)throw new InvalidOperationException("Cannot dispose during a host step.");disposed=true;
            List<Exception> failures=null;foreach(var value in areas.Values)try{value.Dispose();}catch(Exception e){(failures??=new List<Exception>()).Add(e);}
            areas.Clear();if(failures!=null)throw new AggregateException(failures);
        }
    }
}
