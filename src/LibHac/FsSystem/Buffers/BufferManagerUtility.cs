using System;
using System.Runtime.CompilerServices;
using System.Threading;
using LibHac.Diag;
using LibHac.Fs;
using Buffer = LibHac.Fs.Buffer;

namespace LibHac.FsSystem.Buffers
{
    public struct BufferManagerContext
    {
        private bool _needsBlocking;

        public bool IsNeedBlocking() => _needsBlocking;
        public void SetNeedBlocking(bool needsBlocking) => _needsBlocking = needsBlocking;
    }

    public struct ScopedBufferManagerContextRegistration : IDisposable
    {
        private BufferManagerContext _oldContext;

        // ReSharper disable once UnusedParameter.Local
        public ScopedBufferManagerContextRegistration(int unused = default)
        {
            _oldContext = BufferManagerUtility.GetBufferManagerContext();
        }

        public void Dispose()
        {
            BufferManagerUtility.RegisterBufferManagerContext(in _oldContext);
        }
    }

    internal static class BufferManagerUtility
    {
        // Todo: Use TimeSpan
        private const int RetryWait = 10;

        [ThreadStatic]
        private static BufferManagerContext _context;

        public delegate bool IsValidBufferFunction(in Buffer buffer);

        public static Result DoContinuouslyUntilBufferIsAllocated(Func<Result> function, Func<Result> onFailure,
            [CallerMemberName] string callerName = "")
        {
            const int bufferAllocationRetryLogCountMax = 10;
            const int bufferAllocationRetryLogInterval = 100;

            Result result;

            for (int count = 1; ; count++)
            {
                result = function();
                if (!ResultFs.BufferAllocationFailed.Includes(result))
                    break;

                // Failed to allocate. Wait and try again.
                if (1 <= count && count <= bufferAllocationRetryLogCountMax ||
                    count % bufferAllocationRetryLogInterval == 0)
                {
                    // Todo: Log allocation failure
                }

                Result rc = onFailure();
                if (rc.IsFailure()) return rc;

                Thread.Sleep(RetryWait);
            }

            return result;
        }

        public static Result DoContinuouslyUntilBufferIsAllocated(Func<Result> function,
            [CallerMemberName] string callerName = "")
        {
            return DoContinuouslyUntilBufferIsAllocated(function, static () => Result.Success, callerName);
        }

        public static void RegisterBufferManagerContext(in BufferManagerContext context)
        {
            _context = context;
        }

        public static ref BufferManagerContext GetBufferManagerContext() => ref _context;

        public static void EnableBlockingBufferManagerAllocation()
        {
            ref BufferManagerContext context = ref GetBufferManagerContext();
            context.SetNeedBlocking(true);
        }

        public static Result AllocateBufferUsingBufferManagerContext(out Buffer outBuffer, IBufferManager bufferManager,
            int size, IBufferManager.BufferAttribute attribute, IsValidBufferFunction isValidBuffer,
            [CallerMemberName] string callerName = "")
        {
            Assert.SdkNotNullOut(out outBuffer);
            Assert.SdkNotNull(bufferManager);
            Assert.SdkNotNull(callerName);

            // Clear the output.
            outBuffer = new Buffer();
            var tempBuffer = new Buffer();

            // Get the context.
            ref BufferManagerContext context = ref GetBufferManagerContext();

            Result AllocateBufferImpl()
            {
                Buffer buffer = bufferManager.AllocateBuffer(size, attribute);

                if (!isValidBuffer(in buffer))
                {
                    if (!buffer.IsNull)
                    {
                        bufferManager.DeallocateBuffer(buffer);
                    }

                    return ResultFs.BufferAllocationFailed.Log();
                }

                tempBuffer = buffer;
                return Result.Success;
            }

            if (!context.IsNeedBlocking())
            {
                // If we don't need to block, just allocate the buffer.
                Result rc = AllocateBufferImpl();
                if (rc.IsFailure()) return rc;
            }
            else
            {
                // Otherwise, try to allocate repeatedly.
                Result rc = DoContinuouslyUntilBufferIsAllocated(AllocateBufferImpl);
                if (rc.IsFailure()) return rc;
            }

            Assert.SdkAssert(!tempBuffer.IsNull);
            outBuffer = tempBuffer;
            return Result.Success;
        }
    }
}
