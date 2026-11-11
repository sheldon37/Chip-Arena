using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    /// <summary>
    /// A startup bank decoded off the main thread while the logo intro plays (NativeSourceMotion, NativeSceneMotion; docs/BOOT-
    /// INTRO.md). The main thread only loads the resource (Resources.LoadAsync) and copies its bytes; the envelope, base64, gzip,
    /// the JSON and the bank's own checks run on a worker (all of it plain C#: JsonUtility may run off the main thread, and the
    /// build touches no Unity object). Join waits for it and rethrows its failure where the synchronous load threw, so whoever
    /// reads the bank first, the loading step or a caller that came early, reads it complete or not at all.
    /// </summary>
    internal sealed class NativeBankLoad<T> where T:class
    {
        public readonly string Resource;
        private readonly Func<string,Action<T>,T> build;
        private ResourceRequest request;private Task<T> task;
        /// <summary>What the build had started to fill when it failed (null if it failed before), as the synchronous load left it.</summary>
        public T Partial{get{lock(this)return partial;}}
        private T partial;
        /// <summary>Main thread: starts loading the resource now; build(json, begin) decodes and checks it on a worker, calling begin
        /// with the bank it fills before filling it.</summary>
        public NativeBankLoad(string resource,Func<string,Action<T>,T> build){Resource=resource;this.build=build;request=Resources.LoadAsync<TextAsset>(resource);}
        /// <summary>Main thread, once a frame: hands the resource to the worker as soon as it is in.</summary>
        public void Step(){if(task==null&&request!=null&&request.isDone)Start();}
        public bool Started=>task!=null;
        /// <summary>The worker has finished (or failed): Join no longer waits.</summary>
        public bool Done=>task!=null&&task.IsCompleted;
        private void Start()
        {
            // ResourceRequest.asset completes a load still in flight (Join before the resource came in).
            var asset=request.asset as TextAsset;request=null;
            if(asset==null){task=Task.FromException<T>(new InvalidOperationException(Resource+" is missing"));return;}
            byte[] raw=asset.bytes;string name=Resource;
            task=Task.Run(()=>build(NativeSourceModels.Unwrap(name,Text(raw)),Begin));
        }
        private void Begin(T filling){lock(this)partial=filling;}
        // TextAsset.text reads the file as UTF-8 (a byte order mark skipped); the same off the main thread.
        private static string Text(byte[] raw){using(var reader=new StreamReader(new MemoryStream(raw,false),System.Text.Encoding.UTF8,true))return reader.ReadToEnd();}
        /// <summary>Main thread: the decoded bank, waiting for the worker if it has not finished; its failure is rethrown here.</summary>
        public T Join(){if(task==null)Start();return task.GetAwaiter().GetResult();}
    }
}
