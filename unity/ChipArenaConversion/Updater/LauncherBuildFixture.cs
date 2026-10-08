// Windows CI entry point for the exact compiler invocation used by the Unity Editor packager.
using ChipArena.UnityConversion.Editor;
using System;
class LauncherBuildFixture
{
    static int Main(string[] args)
    {try{NativeLauncherCompiler.Compile(args[0],args[1],args[2]);return 0;}catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
