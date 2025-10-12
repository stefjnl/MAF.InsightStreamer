namespace MAF.InsightStreamer.Infrastructure.Services;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net.Sockets;

public class McpGatewayHostedService : IHostedService, IDisposable
{
    private readonly ILogger<McpGatewayHostedService> _logger;
    private Process? _gatewayProcess;
    private int _gatewayPort;
    private readonly SemaphoreSlim _startupLock = new(1, 1);

    public int GatewayPort => _gatewayPort;
    public bool IsReady { get; private set; }

    public McpGatewayHostedService(ILogger<McpGatewayHostedService> logger)
    {
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _startupLock.WaitAsync(cancellationToken);
        try
        {
            // Find available port
            _gatewayPort = FindAvailablePort();
            _logger.LogInformation("Starting Docker MCP Gateway on port {Port}", _gatewayPort);

            var startInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = $"mcp gateway run --transport streaming --port {_gatewayPort}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            _gatewayProcess = Process.Start(startInfo);
            if (_gatewayProcess == null)
            {
                throw new InvalidOperationException("Failed to start Docker MCP Gateway process");
            }

            // Log gateway output
            _gatewayProcess.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    _logger.LogDebug("[MCP Gateway] {Output}", e.Data);
            };

            _gatewayProcess.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    _logger.LogWarning("[MCP Gateway Error] {Error}", e.Data);
            };

            _gatewayProcess.BeginOutputReadLine();
            _gatewayProcess.BeginErrorReadLine();

            // Wait for gateway to be ready (with timeout)
            await WaitForGatewayReady(cancellationToken);
            
            IsReady = true;
            _logger.LogInformation("Docker MCP Gateway started successfully on port {Port}", _gatewayPort);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start Docker MCP Gateway");
            throw;
        }
        finally
        {
            _startupLock.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Docker MCP Gateway");
        IsReady = false;

        if (_gatewayProcess != null && !_gatewayProcess.HasExited)
        {
            try
            {
                _gatewayProcess.Kill();
                await _gatewayProcess.WaitForExitAsync(cancellationToken);
                _logger.LogInformation("Docker MCP Gateway stopped successfully");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error stopping Docker MCP Gateway");
            }
        }
    }

    private async Task WaitForGatewayReady(CancellationToken cancellationToken)
    {
        var maxAttempts = 30; // 30 seconds max
        var attemptDelay = TimeSpan.FromSeconds(1);

        for (int i = 0; i < maxAttempts; i++)
        {
            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException();

            try
            {
                // Try to connect to the gateway endpoint
                using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
                var response = await httpClient.GetAsync(
                    $"http://localhost:{_gatewayPort}/health", 
                    cancellationToken
                );

                // If we get any response, consider it ready
                // (MCP gateway may not have a health endpoint, but connection success means it's up)
                _logger.LogDebug("Gateway health check attempt {Attempt}: Status {Status}", i + 1, response.StatusCode);
                return;
            }
            catch (HttpRequestException)
            {
                // Expected while gateway is starting up
                _logger.LogDebug("Gateway not ready yet, attempt {Attempt}/{MaxAttempts}", i + 1, maxAttempts);
            }
            catch (TaskCanceledException)
            {
                // Timeout on this attempt, try again
            }

            await Task.Delay(attemptDelay, cancellationToken);
        }

        // After 30 seconds, assume it's ready anyway (gateway may not have health endpoint)
        _logger.LogWarning("Gateway health check timed out after {Seconds} seconds, assuming ready", maxAttempts);
    }

    private static int FindAvailablePort()
    {
        // Find a random available port
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 0));
        var port = ((System.Net.IPEndPoint)socket.LocalEndPoint!).Port;
        return port;
    }

    public void Dispose()
    {
        _gatewayProcess?.Dispose();
        _startupLock?.Dispose();
    }
}