using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RLSHub.Wpf.Native
{
    /// <summary>
    /// Puts the current process in a Windows job with JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
    /// so that when the app exits (including crash), all child processes are killed.
    /// Call once at app startup.
    /// </summary>
    internal static class WindowsJobObject
    {
        private const int JobObjectBasicLimitInformation = 2;
        private const uint JobObjectLimitKillOnJobClose = 0x2000;

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string? lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetInformationJobObject(
            IntPtr hJob,
            int JobObjectInfoClass,
            in JobBasicLimitInfo info,
            uint cbJobObjectInfoLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

        [StructLayout(LayoutKind.Sequential)]
        private struct JobBasicLimitInfo
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        private static bool _initialized;

        /// <summary>
        /// Ensures the current process is in a job that kills all children when the process exits.
        /// Safe to call multiple times; runs only once.
        /// </summary>
        public static void EnsureCurrentProcessInKillOnCloseJob()
        {
            if (_initialized) return;
            _initialized = true;

            try
            {
                var jobHandle = CreateJobObject(IntPtr.Zero, null);
                if (jobHandle == IntPtr.Zero || jobHandle == new IntPtr(-1))
                    return;

                var info = new JobBasicLimitInfo
                {
                    LimitFlags = JobObjectLimitKillOnJobClose
                };
                var size = (uint)Marshal.SizeOf<JobBasicLimitInfo>();
                if (!SetInformationJobObject(jobHandle, JobObjectBasicLimitInformation, in info, size))
                    return;

                using var currentProcess = Process.GetCurrentProcess();
                if (!AssignProcessToJobObject(jobHandle, currentProcess.Handle))
                    return;

                // Job handle is intentionally not closed so it stays alive for the process lifetime.
                // When the process exits, the OS closes all handles and the job is destroyed,
                // killing all processes in the job (our children).
            }
            catch
            {
                // Non-fatal; app will still run, we just won't auto-kill children on exit.
            }
        }
    }
}
