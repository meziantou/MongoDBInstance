using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Meziantou.Framework.Win32;
using Mono.Unix;

namespace MongoDBInstances;

public sealed partial class MongoDBInstance : IAsyncDisposable
{
    private Process? _process;
    private JobObject? _jobObject;
    private string? _dbPath;
    private bool _dbPathMustBeDeleted;

    public event EventHandler<MongoDataReceivedEventArgs>? MongoOutput;

    public int Port { get; set; } = 27017;
    public bool EnableIPv6 { get; set; } = true;
    public string? StorageEngine { get; set; } = "ephemeralForTest";
    public bool EnableMajorityReadConcern { get; set; }

    /// <summary>
    /// If not set, a temporary directory will be created
    /// </summary>
    public string? DatabasePath { get; set; }

    /// <summary>
    /// Override the default binary path
    /// </summary>
    public string? MongoPath { get; set; }

    public string ConnectionString => "mongodb://127.0.0.1:" + Port.ToString(CultureInfo.InvariantCulture);

    private string GetBinaryLocation()
    {
        if (!string.IsNullOrEmpty(MongoPath))
            return MongoPath;

        var path = GetPath(AppContext.BaseDirectory);
        if (File.Exists(path))
            return path;

        path = GetPath(Path.GetDirectoryName(typeof(MongoDBInstance).Assembly.Location));
        if (File.Exists(path))
            return path;

        path = GetPath(Path.GetDirectoryName(Environment.ProcessPath));
        if (File.Exists(path))
            return path;

        throw new NotSupportedException($"Cannot find a compatible version of MongoDB. You can use '{nameof(MongoPath)}' to set a path to a mongod executable file.");

        static string? GetPath(string? rootPath)
        {
            if (rootPath == null)
                return null;

            if (OperatingSystem.IsWindows())
                return Path.Join(rootPath, MongoDBWindowsFileName);

            if (OperatingSystem.IsLinux() && OperatingSystem.IsIOSVersionAtLeast(5, 5))
                return Path.Join(rootPath, MongoDBUbuntu2004Filename);

            if (OperatingSystem.IsLinux())
                return Path.Join(rootPath, MongoDBUbuntu1804Filename);

            if (OperatingSystem.IsMacOS())
                return Path.Join(rootPath, MongoDBMacOSFilename);

            return null;
        }
    }

    [SuppressMessage("Design", "MA0051:Method is too long", Justification = "<Pending>")]
    public ValueTask StartAsync()
    {
        if (_process != null)
            throw new InvalidOperationException("The process is already running");

        if (DatabasePath != null)
        {
            _dbPath = DatabasePath;
            _dbPathMustBeDeleted = false;
        }
        else
        {
            _dbPathMustBeDeleted = true;
            _dbPath = Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        }

        Directory.CreateDirectory(_dbPath);
        var psi = new ProcessStartInfo
        {
            FileName = GetBinaryLocation(),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = false,
            CreateNoWindow = true,
            ErrorDialog = false,
            ArgumentList =
            {
                "--port", Port.ToString(CultureInfo.InvariantCulture),
                "--noauth",
                "--dbpath", _dbPath,
            },
        };

        if (EnableIPv6)
        {
            psi.ArgumentList.Add("--ipv6");
        }

        if (EnableMajorityReadConcern)
        {
            psi.ArgumentList.Add("--enableMajorityReadConcern=1");
        }

        if (StorageEngine != null)
        {
            psi.ArgumentList.Add("--storageEngine");
            psi.ArgumentList.Add(StorageEngine);
        }


        if (OperatingSystem.IsLinux())
        {
            var unixFileInfo = new UnixFileInfo(psi.FileName);
            unixFileInfo.FileAccessPermissions |= FileAccessPermissions.UserExecute;
        }

        _process = new Process() { StartInfo = psi };
        _process.OutputDataReceived += OnOutputDataReceived;
        _process.ErrorDataReceived += OnErrorDataReceived;
        if (!_process.Start())
        {
            _process.Dispose();
            _process = null;
            throw new InvalidOperationException("Cannot start the process");
        }

        if (OperatingSystem.IsWindows())
        {
            _jobObject = new JobObject();
            _jobObject.SetLimits(new JobObjectLimits
            {
                Flags = JobObjectLimitFlags.KillOnJobClose,
            });
            _jobObject.AssignProcess(_process);
        }

        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
        return ValueTask.CompletedTask;
    }

    private void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        var ev = MongoOutput;
        if (ev != null && e.Data != null)
        {
            ev.Invoke(this, new MongoDataReceivedEventArgs(OutputDataSource.Output, e.Data));
        }
    }

    private void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        var ev = MongoOutput;
        if (ev != null && e.Data != null)
        {
            ev.Invoke(this, new MongoDataReceivedEventArgs(OutputDataSource.Error, e.Data));
        }
    }

    public async ValueTask StopAsync()
    {
        if (_process != null)
        {
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync().ConfigureAwait(false);
            _process.Dispose();
            _process = null;
        }

        if (_jobObject != null)
        {
            _jobObject.Dispose();
            _jobObject = null;
        }

        if (_dbPathMustBeDeleted && _dbPath != null)
        {
            try
            {
                Directory.Delete(_dbPath, recursive: true);
            }
            catch
            {
            }
        }

        _dbPath = null;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
    }
}
