using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog.Sinks.SystemConsole.Themes;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss}] [{Level:u3}] {Message:lj}{NewLine}{Exception}",
        theme: AnsiConsoleTheme.Code
    )
    .WriteTo.File("Logs/Lifeguard_Log_.txt",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        fileSizeLimitBytes: 10 * 1024 * 1024,
        rollOnFileSizeLimit: true,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss}] [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

const string appMutexName = "Global\\LifeguardSystem_SingleInstance_Mutex";
using Mutex appMutex = new Mutex(true, appMutexName, out bool isNewInstance);

if (!isNewInstance)
{
    Log.Warning("⚠️ Lifeguard is already running. Single-instance protection triggered. This instance will close automatically.");

    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("\n[WARNING] The Lifeguard system is already running in the background!");
    Console.WriteLine("To prevent conflicting monitoring logic, this window will automatically close in 3 seconds...");
    Console.ResetColor();

    Thread.Sleep(3000); // Pause for 3 seconds to allow the user to read the message.
    return;
}

try
{
    AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
    {
        var ex = args.ExceptionObject as Exception;
        Log.Fatal(ex, "🔥 [CRITICAL ERROR] An uncaught background exception occurred. Lifeguard is crashing!");
        Log.CloseAndFlush();
    };

    Directory.SetCurrentDirectory(AppContext.BaseDirectory);
    try { if (!Console.IsOutputRedirected) Console.OutputEncoding = Encoding.UTF8; } catch { } // On error resume next

    Console.WriteLine($"Starting Universal Lifeguard System (Worker Mode)...\n");
    Log.Information("🚀 Lifeguard Watchdog is starting up...");

    var builder = Host.CreateApplicationBuilder(args);

    builder.Logging.ClearProviders();
    builder.Logging.AddSerilog(Log.Logger);

    builder.Configuration.SetBasePath(AppContext.BaseDirectory)
                         .AddJsonFile("config.json", optional: false, reloadOnChange: true);

    builder.Services.Configure<Lifeguard.Config>(builder.Configuration);
    builder.Services.AddHttpClient();
    builder.Services.AddHostedService<Lifeguard.LifeguardWorker>();

    using var host = builder.Build();

    Log.Information("Lifeguard System Application Started.");
    host.Run();
}
catch (Exception ex)
{
    Console.WriteLine($"Fatal Error: {ex.Message}");
    Log.Fatal(ex, "💥 [FATAL ERROR] Lifeguard Watchdog Main Thread terminated unexpectedly!");
}
finally
{
    Log.Information("🛑 Lifeguard Watchdog has completely shut down.");
    Log.CloseAndFlush();
}

namespace Lifeguard
{
    public class TelegramMessagePayload
    {
        [JsonPropertyName("chat_id")]
        public string? ChatId { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("parse_mode")]
        public string? ParseMode { get; set; }
    }

    public class HaNodeClient { 
        public string? ExePath { get; set; }
        public int HeartbeatTimeoutMs { get; set; }
    }

    public class Config
    {
        public string? NodeId { get; set; } = "LifeguardNode";
        public required Dictionary<string, string> MT4Instances { get; set; }
        public HaNodeClient? HaNodeClient { get; set; }
        public int TimeoutMilliseconds { get; set; } = 10000;
        public int InitialDelayMs { get; set; } = 30000;
        public int MaxRetries { get; set; } = 3;
        public int RestartCooldownMs { get; set; } = 15000;
        public int AlertReminderIntervalMs { get; set; } = 3600000;
        public string? TelegramBotToken { get; set; }
        public string? TelegramChatId { get; set; }
    }

    [JsonSerializable(typeof(Config))]
    [JsonSerializable(typeof(TelegramMessagePayload))]
    internal partial class LifeguardJsonContext : JsonSerializerContext { }

    public class LifeguardWorker : BackgroundService
    {
        private readonly IOptionsMonitor<Config> _configMonitor;
        private readonly ILogger<LifeguardWorker> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private bool _isFirstRun = true;
        private DateTime _lastErrorTime = DateTime.MinValue;
        private readonly object _consoleLock = new object();
        private volatile bool _isHaNodeClientServerDown = false;
        private ConcurrentDictionary<string, byte> _deadInstances = new ConcurrentDictionary<string, byte>();
        private CancellationTokenSource _reloadCts = new CancellationTokenSource();

        public LifeguardWorker(IOptionsMonitor<Config> configMonitor, ILogger<LifeguardWorker> logger, IHttpClientFactory httpClientFactory)
        {
            _configMonitor = configMonitor;
            _logger = logger;
            _httpClientFactory = httpClientFactory;

            _configMonitor.OnChange(newConfig =>
            {
                _logger.LogInformation("🔄 HOT RELOAD: config.json changes applied dynamically.");

                var oldCts = _reloadCts;
                _reloadCts = new CancellationTokenSource();
                oldCts.Cancel();
            });
        }

        private async Task NotifyAdminAsync(string message, CancellationToken token)
        {
            var config = _configMonitor.CurrentValue;
            if (string.IsNullOrEmpty(config.TelegramBotToken) || string.IsNullOrEmpty(config.TelegramChatId)) return;
            string nodeName = string.IsNullOrEmpty(config.NodeId) ? "Unknown-Node" : config.NodeId;
            string finalMessage = $"🖥️ <b>主機: {nodeName}</b>\n{message}";

            try
            {
                string url = $"https://api.telegram.org/bot{config.TelegramBotToken}/sendMessage";

                var payload = new TelegramMessagePayload
                {
                    ChatId = config.TelegramChatId,
                    Text = finalMessage,
                    ParseMode = "HTML"
                };

                string jsonString = JsonSerializer.Serialize(payload, LifeguardJsonContext.Default.TelegramMessagePayload);
                var content = new StringContent(jsonString, Encoding.UTF8, "application/json");

                var client = _httpClientFactory.CreateClient();
                HttpResponseMessage response = await client.PostAsync(url, content, token);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Telegram notification failed. Status Code: {StatusCode}", response.StatusCode);
                }
                else
                {
                    _logger.LogInformation("Telegram notification sent successfully.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending Telegram notification.");
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var currentConfig = _configMonitor.CurrentValue;
            int delayMs = currentConfig.InitialDelayMs > 0 ? currentConfig.InitialDelayMs : 30000;
            _logger.LogInformation("System booting... Grace period of {DelaySeconds} seconds started. Allowing MT4 instances to initialize...", delayMs / 1000);

            try
            {
                await Task.Delay(delayMs, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                _logger.LogInformation("Shutdown signal received during grace period. Exiting.");
                return;
            }

            _logger.LogInformation("Grace period ended. Starting monitoring tasks.");
            List<Task> monitoringTasks = new List<Task>();

            if (currentConfig.MT4Instances != null)
            {
                foreach (var kvp in currentConfig.MT4Instances)
                {
                    string id = kvp.Key;
                    string path = kvp.Value;
                    monitoringTasks.Add(Task.Run(() => MonitorSingleMT4(id, path, stoppingToken), stoppingToken));
                }
            }

            monitoringTasks.Add(Task.Run(() => MonitorHaNodeClientServer(stoppingToken), stoppingToken));
            monitoringTasks.Add(Task.Run(() => StatusReporter(currentConfig.TimeoutMilliseconds, stoppingToken), stoppingToken));

            await Task.WhenAll(monitoringTasks);
            _logger.LogInformation("Shutdown signal received. All monitoring engines stopped gracefully.");
        }

        private void MonitorHaNodeClientServer(CancellationToken token)
        {
            string eventName = @"Local\HaNodeClient_Heartbeat";
            int consecutiveFailures = 0;
            bool isDead = false;
            using EventWaitHandle heartbeatEvent = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);
            bool justWokeUp = true;

            while (!token.IsCancellationRequested)
            {
                var currentConfig = _configMonitor.CurrentValue;
                string? exePath = currentConfig.HaNodeClient?.ExePath;
                int nodeTimeoutMs = currentConfig.HaNodeClient?.HeartbeatTimeoutMs ?? 60000;
                int maxRetries = currentConfig.MaxRetries > 0 ? currentConfig.MaxRetries : 3;
                int cooldownMs = currentConfig.RestartCooldownMs > 0 ? currentConfig.RestartCooldownMs : 15000;

                if (string.IsNullOrEmpty(exePath))
                {
                    justWokeUp = true;
                    WaitHandle.WaitAny(new[] { token.WaitHandle, _reloadCts.Token.WaitHandle });
                    continue;
                }

                if (justWokeUp)
                {
                    _logger.LogInformation("[HaNodeClient] Monitoring activated. Listening on Event '{EventName}'...", eventName);
                    justWokeUp = false;
                }

                if (nodeTimeoutMs <= 0) nodeTimeoutMs = 15000;
                var reloadToken = _reloadCts.Token;
                int reminderMs = currentConfig.AlertReminderIntervalMs > 0 ? currentConfig.AlertReminderIntervalMs : 3600000;
                int waitTimeout = isDead ? reminderMs : nodeTimeoutMs;

                // 💡 Note: Since this has already been offloaded to a background thread via Task.Run, 
                // using a blocking WaitAny here is completely safe and reasonable.
                int waitResult = WaitHandle.WaitAny(new[] { heartbeatEvent, token.WaitHandle, reloadToken.WaitHandle }, waitTimeout);

                if (waitResult == 1 || token.IsCancellationRequested) break;
                if (waitResult == 2 || reloadToken.IsCancellationRequested) continue;
                bool receivedSignal = (waitResult == 0);

                if (receivedSignal)
                {
                    if (isDead)
                    {
                        isDead = false;
                        _isHaNodeClientServerDown = false;
                        _logger.LogInformation("RESURRECTED: [HaNodeClient] is back online!");
                    }
                    consecutiveFailures = 0;
                }
                else if (waitResult == WaitHandle.WaitTimeout)
                {
                    if (isDead)
                    {
                        _logger.LogWarning("REMINDER: [HaNodeClient] is still offline after {Minutes} minutes. Sending repeated alert.", reminderMs / 60000);
                        var duration = TimeSpan.FromMilliseconds(reminderMs);
                        string reminderMessage = $"⚠️ <b>Persistent Downtime Alert</b> ⚠️\n<b>[HaNodeClient]</b> has not recovered after <b>{(int)duration.TotalMinutes} minutes</b>. Immediate manual intervention required!";
                        _ = Task.Run(() => NotifyAdminAsync(reminderMessage, token));
                        continue;
                    }

                    var status = CheckProcessStatus(exePath);
                    if (!status.Exists)
                    {
                        _logger.LogError("CRITICAL: [HaNodeClient] Process is completely GONE (Crashed/Closed).");
                    }
                    else if (!status.IsResponding)
                    {
                        _logger.LogWarning("WARNING: [HaNodeClient] Process exists (PID: {ProcessId}) but is NOT RESPONDING (Stuck/High CPU Load).", status.Pid);
                    }
                    else
                    {
                        _logger.LogWarning("WARNING: [HaNodeClient] Process is RESPONDING but missed heartbeat (Logical hang or delayed).");
                    }

                    consecutiveFailures++;
                    _lastErrorTime = DateTime.Now;
                    _isHaNodeClientServerDown = true;
                    _logger.LogError("ERROR: [HaNodeClient] Timeout. (Failure {Failures}/{MaxRetries})", consecutiveFailures, maxRetries);

                    if (consecutiveFailures >= maxRetries)
                    {
                        isDead = true;
                        _logger.LogCritical("FATAL: [HaNodeClient] reached max retries. Suspending restarts.");
                        string alertMessage = $"🚨 <b>CRITICAL ALERT</b> 🚨\n<b>[HaNodeClient.exe]</b> has crashed and automatic restart failed. Please log in and check immediately!";
                        _ = Task.Run(() => NotifyAdminAsync(alertMessage, token));

                        continue;
                    }

                    RestartProcess("HaNodeClient", exePath, "");
                    token.WaitHandle.WaitOne(cooldownMs);
                }
            }
        }

        private void MonitorSingleMT4(string instanceId, string exePath, CancellationToken token)
        {
            string eventName = $@"Local\EA_Heartbeat_{instanceId}";
            int consecutiveFailures = 0;
            bool isDead = false;
            using EventWaitHandle heartbeatEvent = new EventWaitHandle(false, EventResetMode.AutoReset, eventName);
            if (token.WaitHandle.WaitOne(new Random().Next(10, 500))) return;
            _logger.LogInformation("[{InstanceId}] Monitoring activated. Listening on Event '{EventName}'...", instanceId, eventName);

            while (!token.IsCancellationRequested)
            {
                var currentConfig = _configMonitor.CurrentValue;
                int maxRetries = currentConfig.MaxRetries > 0 ? currentConfig.MaxRetries : 3;
                int cooldownMs = currentConfig.RestartCooldownMs > 0 ? currentConfig.RestartCooldownMs : 15000;
                int timeoutMs = currentConfig.TimeoutMilliseconds > 0 ? currentConfig.TimeoutMilliseconds : 60000;
                int reminderMs = currentConfig.AlertReminderIntervalMs > 0 ? currentConfig.AlertReminderIntervalMs : 3600000;

                var reloadToken = _reloadCts.Token;
                int waitTimeout = isDead ? reminderMs : timeoutMs;
                int waitResult = WaitHandle.WaitAny(new[] { heartbeatEvent, token.WaitHandle, reloadToken.WaitHandle }, waitTimeout);
                if (waitResult == 1 || token.IsCancellationRequested) break;
                if (waitResult == 2 || reloadToken.IsCancellationRequested) continue;

                bool receivedSignal = (waitResult == 0);

                if (receivedSignal)
                {
                    if (isDead)
                    {
                        isDead = false;
                        _deadInstances.TryRemove(instanceId, out _);
                        _logger.LogInformation("RESURRECTED: MT4 [{InstanceId}] is back online!", instanceId);
                    }
                    consecutiveFailures = 0;
                }
                else if (waitResult == WaitHandle.WaitTimeout)
                {
                    if (isDead)
                    {
                        _logger.LogWarning("REMINDER: MT4 [{InstanceId}] is still offline after {Minutes} minutes. Sending repeated alert.", instanceId, reminderMs / 60000);
                        var duration = TimeSpan.FromMilliseconds(reminderMs);
                        string reminderMessage = $"⚠️ <b>Persistent Downtime Alert</b> ⚠️\n<b>[{instanceId}]</b> has not recovered after <b>{(int)duration.TotalMinutes} minutes</b>. Immediate manual intervention required!";
                        _ = Task.Run(() => NotifyAdminAsync(reminderMessage, token));
                        continue;
                    }

                    var status = CheckProcessStatus(exePath);
                    if (!status.Exists)
                    {
                        _logger.LogError("CRITICAL: MT4 [{InstanceId}] Process is completely GONE (Crashed/Closed).", instanceId);
                    }
                    else if (!status.IsResponding)
                    {
                        _logger.LogWarning("WARNING: MT4 [{InstanceId}] Process exists (PID: {ProcessId}) but is NOT RESPONDING (Stuck/High CPU Load).", instanceId, status.Pid);
                    }
                    else
                    {
                        _logger.LogWarning("WARNING: MT4 [{InstanceId}] Process is RESPONDING but missed heartbeat (Logical hang or delayed).", instanceId);
                    }

                    consecutiveFailures++;
                    _lastErrorTime = DateTime.Now;

                    if (consecutiveFailures >= maxRetries)
                    {
                        isDead = true;
                        _deadInstances.TryAdd(instanceId, 1);
                        _logger.LogCritical("FATAL: MT4 [{InstanceId}] reached max retries. Suspending restarts.", instanceId);
                        string alertMessage = $"🚨 <b>CRITICAL ALERT</b> 🚨\nMT4 Instance <b>[{instanceId}]</b> has crashed and automatic restart failed after {maxRetries} attempts. Please log in and check immediately!";
                        _ = Task.Run(() => NotifyAdminAsync(alertMessage, token));

                        continue;
                    }

                    _logger.LogWarning("[{InstanceId}] Heartbeat timeout. Initiating restart... (Failure {Failures}/{MaxRetries})", instanceId, consecutiveFailures, maxRetries);

                    RestartProcess(instanceId, exePath, "/portable");
                    token.WaitHandle.WaitOne(cooldownMs);
                }
            }
        }

        private void RestartProcess(string instanceId, string exePath, string arguments = "/portable")
        {
            _logger.LogInformation("RECOVERY: Initiating restart sequence for [{InstanceId}]...", instanceId);

            try
            {
                string procName = Path.GetFileNameWithoutExtension(exePath);
                Process[] processes = Process.GetProcessesByName(procName);
                int currentSessionId = Process.GetCurrentProcess().SessionId;

                foreach (Process proc in processes)
                {
                    try
                    {
                        if (proc.SessionId == currentSessionId &&
                            proc.MainModule?.FileName is string path &&
                            path.Equals(exePath, StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogWarning("[{InstanceId}] Found stuck process (PID: {ProcessId}). Closing...", instanceId, proc.Id);

                            proc.CloseMainWindow();
                            if (!proc.WaitForExit(2000))
                            {
                                proc.Kill(true);
                            }
                        }
                    }
                    catch { } // Intentionally ignore insufficient permission errors (如系統核心進程)
                }

                // 💡 Note: Since the outer loop (MonitorSingleMT4) is already running on a background thread via Task.Run, 
                // using Thread.Sleep(3000) here will only briefly block this specific background thread without freezing 
                // the main system. Therefore, this approach is perfectly safe.
                Thread.Sleep(3000);

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = arguments,
                    WorkingDirectory = Path.GetDirectoryName(exePath),
                    WindowStyle = ProcessWindowStyle.Minimized,
                    UseShellExecute = true
                };

                Process.Start(startInfo);
                _logger.LogInformation("[{InstanceId}] RECOVERY COMPLETE.", instanceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{InstanceId}] RECOVERY FAILED: {ErrorMessage}", instanceId, ex.Message);
            }
        }

        private void StatusReporter(int timeoutMs, CancellationToken token)
        {
            double reportIntervalMs = Math.Max(60000.0, timeoutMs);
            DateTime lastReportTime = DateTime.MinValue;

            while (!token.IsCancellationRequested)
            {
                // Checks the status every second; if a shutdown signal (token) is received during this period, safely exits the loop immediately.
                if (token.WaitHandle.WaitOne(1000)) break;

                bool hasErrorInLastMinute = (DateTime.Now - _lastErrorTime).TotalSeconds <= 60;
                bool hasDeadMT4 = !_deadInstances.IsEmpty;

                if (OperatingSystem.IsWindows())
                {
                    if (_isHaNodeClientServerDown)
                        Console.Title = $"Lifeguard: HaNodeClient DOWN | Time: {DateTime.Now:HH:mm:ss}";
                    else if (hasDeadMT4)
                        Console.Title = $"Lifeguard: CRITICAL ({_deadInstances.Count} EA Offline) | Time: {DateTime.Now:HH:mm:ss}";
                    else if (hasErrorInLastMinute)
                        Console.Title = $"Lifeguard: ERROR DETECTED | Time: {DateTime.Now:HH:mm:ss}";
                    else
                        Console.Title = $"Lifeguard: ALL OK | Time: {DateTime.Now:HH:mm:ss}";
                }

                if (!hasDeadMT4 && !_isHaNodeClientServerDown && !hasErrorInLastMinute)
                {
                    if ((DateTime.Now - lastReportTime).TotalMilliseconds >= reportIntervalMs)
                    {
                        if (_isFirstRun)
                        {
                            Console.WriteLine("[{0}] [INF] Status: Starting the monitor systems.", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                            _isFirstRun = false;
                        }
                        else
                        {
                            Console.WriteLine("[{0}] [INF] Status: All monitored systems are running perfectly.", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                        }

                        lastReportTime = DateTime.Now;
                    }
                }
                else
                {
                    // 💡 Note: Reset the last report time if the system is in an error state. 
                    // This ensures that as soon as the crisis is resolved, an "ALL OK" status is printed immediately.
                    lastReportTime = DateTime.MinValue;
                }
            }
        }

        private (bool Exists, int? Pid, bool IsResponding) CheckProcessStatus(string exePath)
        {
            if (string.IsNullOrEmpty(exePath)) return (false, null, false);

            try
            {
                string procName = Path.GetFileNameWithoutExtension(exePath);
                Process[] processes = Process.GetProcessesByName(procName);
                int currentSessionId = Process.GetCurrentProcess().SessionId;

                foreach (Process p in processes)
                {
                    try
                    {
                        if (p.SessionId == currentSessionId &&
                            p.MainModule?.FileName is string path &&
                            path.Equals(exePath, StringComparison.OrdinalIgnoreCase))
                        {
                            // 💡 Query the OS to check if the window/process is in a "Not Responding" state
                            bool isResponding = p.Responding; 
                            int pid = p.Id;
                            return (true, pid, isResponding);
                        }
                    }
                    catch { } // Ignore permission errors from core system processes
                }
            }
            catch { }

            // Process not found, indicating it has completely crashed or closed
            return (false, null, false); 
        }
    }
}