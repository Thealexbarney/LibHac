using System;
using System.IO;
using Octokit;

namespace LibHacBuild.CodeGen.Stage2;

// Some codegen depends on classes in LibHac.
// The part that does is split out into a separate project so the main build project
// doesn't depend on LibHac.
public static class RunStage2
{
    private const string SolutionFileName = "LibHac.sln";
    public static int Main(string[] args)
    {
        if (!File.Exists(SolutionFileName))
        {
            Console.Error.WriteLine($"Could not find the solution file {SolutionFileName}.");
            return 1;
        }

        KeysCodeGen.Run();

        return 0;
    }
}
