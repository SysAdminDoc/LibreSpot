function Start-LibreSpotOwnedProcess {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$FilePath,
        [string]$ArgumentList = '',
        [string]$WorkingDirectory = '',
        [string]$RedirectStandardOutput = '',
        [string]$RedirectStandardError = '',
        [ValidateSet('Normal', 'Hidden', 'Minimized', 'Maximized')][string]$WindowStyle = '',
        [switch]$NoNewWindow
    )

    if (-not ('LibreSpotProcessJob' -as [type])) {
        Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

public sealed class LibreSpotProcessJob : IDisposable
{
    private const uint JobObjectExtendedLimitInformation = 9;
    private const uint JobObjectLimitKillOnJobClose = 0x2000;
    private IntPtr handle;

    [StructLayout(LayoutKind.Sequential)]
    private struct BasicLimitInformation
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

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ExtendedLimitInformation
    {
        public BasicLimitInformation BasicLimitInformation;
        public IoCounters IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateJobObject(IntPtr jobAttributes, string jobName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(IntPtr job, uint informationClass, ref ExtendedLimitInformation information, uint informationLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool TerminateJobObject(IntPtr job, uint exitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    private LibreSpotProcessJob(IntPtr job)
    {
        handle = job;
    }

    public static LibreSpotProcessJob Create()
    {
        var job = CreateJobObject(IntPtr.Zero, null);
        if (job == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateJobObject failed");
        }

        var information = new ExtendedLimitInformation();
        information.BasicLimitInformation.LimitFlags = JobObjectLimitKillOnJobClose;
        if (!SetInformationJobObject(
            job,
            JobObjectExtendedLimitInformation,
            ref information,
            (uint)Marshal.SizeOf(typeof(ExtendedLimitInformation))))
        {
            var error = new Win32Exception(Marshal.GetLastWin32Error(), "SetInformationJobObject failed");
            CloseHandle(job);
            throw error;
        }

        return new LibreSpotProcessJob(job);
    }

    public void Assign(Process process)
    {
        if (process == null)
        {
            throw new ArgumentNullException("process");
        }
        if (handle == IntPtr.Zero)
        {
            throw new ObjectDisposedException("LibreSpotProcessJob");
        }
        if (!AssignProcessToJobObject(handle, process.Handle))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "AssignProcessToJobObject failed");
        }
    }

    public void Terminate()
    {
        if (handle != IntPtr.Zero && !TerminateJobObject(handle, 1))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "TerminateJobObject failed");
        }
    }

    public void Dispose()
    {
        var job = handle;
        handle = IntPtr.Zero;
        if (job != IntPtr.Zero)
        {
            CloseHandle(job);
        }
        GC.SuppressFinalize(this);
    }

    ~LibreSpotProcessJob()
    {
        var job = handle;
        handle = IntPtr.Zero;
        if (job != IntPtr.Zero)
        {
            CloseHandle(job);
        }
    }
}
'@
    }

    $startParameters = @{
        FilePath = $FilePath
        PassThru = $true
        Wait = $false
        ErrorAction = 'Stop'
    }
    if (-not [string]::IsNullOrWhiteSpace($ArgumentList)) { $startParameters.ArgumentList = $ArgumentList }
    if (-not [string]::IsNullOrWhiteSpace($WorkingDirectory)) { $startParameters.WorkingDirectory = $WorkingDirectory }
    if (-not [string]::IsNullOrWhiteSpace($RedirectStandardOutput)) { $startParameters.RedirectStandardOutput = $RedirectStandardOutput }
    if (-not [string]::IsNullOrWhiteSpace($RedirectStandardError)) { $startParameters.RedirectStandardError = $RedirectStandardError }
    if (-not [string]::IsNullOrWhiteSpace($WindowStyle)) { $startParameters.WindowStyle = $WindowStyle }
    if ($NoNewWindow) { $startParameters.NoNewWindow = $true }

    $job = $null
    $process = $null
    try {
        $job = [LibreSpotProcessJob]::Create()
        $process = Start-Process @startParameters
        if ($null -eq $process) {
            throw 'Start-Process returned no process handle.'
        }
        $job.Assign($process)
        return [pscustomobject]@{ Process = $process; Job = $job }
    } catch {
        if ($job) {
            try { $job.Terminate() } catch {}
            try { $job.Dispose() } catch {}
        }
        if ($process) {
            try { $process.Kill() } catch {}
            try { $process.WaitForExit(5000) } catch {}
            try { $process.Dispose() } catch {}
        }
        throw "LibreSpot could not contain external process '$FilePath': $($_.Exception.Message)"
    }
}
