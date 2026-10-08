using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace ChipArena.UnityConversion.Editor
{
    // No Unity API dependency: the Windows gate runs this exact packaging step.
    public static class NativeLauncherCompiler
    {
        private static readonly string[] Sources={"Updater/Launcher.cs","Updater/UpdateStorage.cs","Updater/UpdateInstaller.cs","Assets/ChipArena/Core/NativeUpdateRules.cs"};
        private static string FullPath(string path)
        {return Path.GetFullPath(path.Replace('/',Path.DirectorySeparatorChar));}
        public static void ValidateProject(string project)
        {
            project=FullPath(project);
            foreach(string relative in Sources)
            {
                string path=FullPath(Path.Combine(project,relative));
                if(!File.Exists(path))throw new FileNotFoundException("Native launcher source is missing. Extract the full updater source package into this project: "+path,path);
            }
            string config=Path.Combine(project,"Updater","Launcher.exe.config");
            if(!File.Exists(config))throw new FileNotFoundException("Native launcher configuration is missing: "+config,config);
        }
        public static void Compile(string project,string destination,string icon)
        {
            project=FullPath(project);destination=FullPath(destination);icon=FullPath(icon);ValidateProject(project);
            if(!File.Exists(icon))throw new FileNotFoundException("Native launcher icon is missing: "+icon,icon);
            string windows=Environment.GetFolderPath(Environment.SpecialFolder.Windows),compiler=null;
            foreach(string framework in new[]{"Framework64","Framework"})
            {string candidate=Path.Combine(windows,"Microsoft.NET",framework,"v4.0.30319","csc.exe");if(File.Exists(candidate)){compiler=candidate;break;}}
            if(compiler==null)throw new IOException("The native launcher requires the Windows .NET Framework 4.8 compiler.");
            Directory.CreateDirectory(destination);Directory.CreateDirectory(Path.Combine(project,"Builds"));
            string exe=Path.Combine(destination,NativeUpdateRules.Executable),response=Path.Combine(project,"Builds","native-launcher.rsp");
            var lines=new List<string>{"/nologo","/warnaserror+","/target:winexe","/optimize+",
                "/r:System.Runtime.Serialization.dll","/r:System.IO.Compression.dll","/r:System.IO.Compression.FileSystem.dll","/r:System.Windows.Forms.dll",
                "/out:"+NativeUpdateRules.QuoteArgument(exe),"/win32icon:"+NativeUpdateRules.QuoteArgument(icon)};
            foreach(string relative in Sources)lines.Add(NativeUpdateRules.QuoteArgument(FullPath(Path.Combine(project,relative))));
            // One compiler-owned response file avoids host-runtime parsing of mixed separators and spaces.
            File.WriteAllLines(response,lines.ToArray(),new UTF8Encoding(true));
            using(var process=Process.Start(new ProcessStartInfo{FileName=compiler,Arguments="@"+NativeUpdateRules.QuoteArgument(response),
                WorkingDirectory=project,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true}))
            {
                var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();process.WaitForExit();
                if(process.ExitCode!=0||!File.Exists(exe))throw new IOException("Native launcher compilation failed. Compiler arguments: "+response+"\n"+output.Result+error.Result);
            }
            File.Copy(Path.Combine(project,"Updater","Launcher.exe.config"),exe+".config",true);
        }
    }
}
