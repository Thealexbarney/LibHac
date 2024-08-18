//-----------------------------------------------------------------------------
// This file was automatically generated.
// Changes to this file will be lost when the file is regenerated.
//
// To change this file, modify /build/CodeGen/results.csv at the root of this
// repo and run the build script.
//
// The script can be run with the "codegen" option to run only the
// code generation portion of the build.
//-----------------------------------------------------------------------------

namespace LibHac.Htc;

public static class ResultHtc
{
    public const int ModuleHtc = 18;

    /// <summary>Error code: 2018-0001; Inner value: 0x212</summary>
    public static Result.Base ConnectionFailure => new Result.Base(ModuleHtc, 1);
    /// <summary>Error code: 2018-0002; Inner value: 0x412</summary>
    public static Result.Base NotFound => new Result.Base(ModuleHtc, 2);
    /// <summary>Error code: 2018-0003; Inner value: 0x612</summary>
    public static Result.Base NotEnoughBuffer => new Result.Base(ModuleHtc, 3);
    /// <summary>Error code: 2018-0101; Inner value: 0xca12</summary>
    public static Result.Base Cancelled => new Result.Base(ModuleHtc, 101);
    /// <summary>Error code: 2018-1023; Inner value: 0x7fe12</summary>
    public static Result.Base Result1023 => new Result.Base(ModuleHtc, 1023);
    /// <summary>Error code: 2018-2001; Inner value: 0xfa212</summary>
    public static Result.Base Result2001 => new Result.Base(ModuleHtc, 2001);
    /// <summary>Error code: 2018-2003; Inner value: 0xfa612</summary>
    public static Result.Base InvalidTaskId => new Result.Base(ModuleHtc, 2003);
    /// <summary>Error code: 2018-2011; Inner value: 0xfb612</summary>
    public static Result.Base InvalidSize => new Result.Base(ModuleHtc, 2011);
    /// <summary>Error code: 2018-2021; Inner value: 0xfca12</summary>
    public static Result.Base TaskCancelled => new Result.Base(ModuleHtc, 2021);
    /// <summary>Error code: 2018-2022; Inner value: 0xfcc12</summary>
    public static Result.Base TaskNotCompleted => new Result.Base(ModuleHtc, 2022);
    /// <summary>Error code: 2018-2023; Inner value: 0xfce12</summary>
    public static Result.Base TaskQueueNotAvailable => new Result.Base(ModuleHtc, 2023);
    /// <summary>Error code: 2018-2101; Inner value: 0x106a12</summary>
    public static Result.Base Result2101 => new Result.Base(ModuleHtc, 2101);
    /// <summary>Error code: 2018-2102; Inner value: 0x106c12</summary>
    public static Result.Base OutOfRpcTask => new Result.Base(ModuleHtc, 2102);
    /// <summary>Error code: 2018-2123; Inner value: 0x109612</summary>
    public static Result.Base InvalidCategory => new Result.Base(ModuleHtc, 2123);
}