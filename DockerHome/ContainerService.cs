using Docker.DotNet;
using Docker.DotNet.Models;
using System.Diagnostics;

namespace DockerHome.Services
{
    public class ContainerService
    {
        private readonly DockerClient _docker;
        private readonly ILogger<ContainerService> _logger;
        private readonly IConfiguration _config;

        public ContainerService(IConfiguration config, ILogger<ContainerService> logger)
        {
            _config = config;
            _logger = logger;

            _logger.LogInformation("[Docker] Auto-detecting Docker endpoint...");

            var endpoint = DetectEndpointAsync().GetAwaiter().GetResult();
            if (endpoint == null)
            {
                _logger.LogCritical("[Docker] Could not find any valid Docker API endpoint!");
                throw new Exception("No Docker API endpoint detected.");
            }

            _logger.LogInformation("[Docker] Using endpoint: {EP}", endpoint);

            var cfg = new DockerClientConfiguration(new Uri(endpoint));
            _docker = cfg.CreateClient();
        }

        // --------------------------------------------------------------------

        private async Task<string?> DetectEndpointAsync()
        {
            var endpoints = new List<string>();

            // 1) Linux socket – works for Linux host and Linux container on Windows
            if (File.Exists("/var/run/docker.sock"))
            {
                _logger.LogInformation("[Docker] Found /var/run/docker.sock");
                endpoints.Add("unix:///var/run/docker.sock");
            }
            else
            {
                _logger.LogInformation("[Docker] /var/run/docker.sock not found");
            }

            // 2) Windows bare metal using npipe
            if (OperatingSystem.IsWindows())
            {
                _logger.LogInformation("[Docker] Windows OS detected → enabling npipe test");
                endpoints.Add("npipe://./pipe/docker_engine");
            }

            // 3) Docker Desktop optional TCP
            endpoints.Add("http://host.docker.internal:2375");

            // 4) Very last fallback
            endpoints.Add("http://127.0.0.1:2375");

            foreach (var ep in endpoints)
            {
                if (await TestEndpointAsync(ep))
                {
                    return ep;
                }
            }

            return null;
        }

        // --------------------------------------------------------------------

        private async Task<bool> TestEndpointAsync(string ep)
        {
            _logger.LogInformation("[Docker Test] Testing: {EP}", ep);

            try
            {
                var cfg = new DockerClientConfiguration(new Uri(ep));
                using var client = cfg.CreateClient();

                using var cts = new CancellationTokenSource(300);

                var sw = Stopwatch.StartNew();
                var v = await client.System.GetVersionAsync(cts.Token);
                sw.Stop();

                _logger.LogInformation("[Docker Test] SUCCESS {EP} → API {V} in {MS}ms",
                    ep, v.APIVersion, sw.ElapsedMilliseconds);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Docker Test] FAILED {EP}");
                return false;
            }
        }

        // --------------------------------------------------------------------
        // API methods
        // --------------------------------------------------------------------

        public async Task<List<ContainerDto>> GetAllContainersAsync()
        {
            var list = await _docker.Containers.ListContainersAsync(
                new ContainersListParameters { All = true });

            return list.Select(Map).ToList();
        }

        public async Task<List<ContainerDto>> GetRunningContainersAsync()
        {
            var list = await _docker.Containers.ListContainersAsync(
                new ContainersListParameters { All = false });

            return list.Select(Map).ToList();
        }

        public async Task<bool> StartContainerAsync(string id)
        {
            try
            {
                await _docker.Containers.StartContainerAsync(id, new ContainerStartParameters());
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not start container {ID}", id);
                return false;
            }
        }

        public async Task<bool> StopContainerAsync(string id)
        {
            try
            {
                await _docker.Containers.StopContainerAsync(id, new ContainerStopParameters());
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not stop container {ID}", id);
                return false;
            }
        }

        // --------------------------------------------------------------------

        private ContainerDto Map(ContainerListResponse c)
        {
            var ports = c.Ports?
                .Where(p => p.PublicPort > 0)
                .Select(p => p.PublicPort)
                .ToList() ?? new List<ushort>();

            var urls = c.Ports?
                .Where(p => p.PublicPort > 0 && 
                       (p.PrivatePort == 80 || p.PrivatePort == 443 || p.PrivatePort == 8080))
                .Select(p =>
                {
                    if (p.PrivatePort == 443)
                        return $"https://{_config["defaulthost"]}:{p.PublicPort}";
                    return $"http://{_config["defaulthost"]}:{p.PublicPort}";
                })
                .Distinct()
                .ToList();

            return new ContainerDto
            {
                Id = c.ID,
                Name = c.Names?.FirstOrDefault()?.Trim('/'),
                Image = c.Image,
                Running = c.State == "running",
                Ports = ports,
                Urls = urls,
                ComposeProject = c.Labels.TryGetValue("com.docker.compose.project", out var proj)
                    ? proj
                    : "uncategorized"
            };
        }
    }

    // DTO
    public class ContainerDto
    {
        public string Id { get; set; }
        public string? Name { get; set; }
        public bool? Running { get; set; }
        public string? Image { get; set; }
        public string? ComposeProject { get; set; }
        public List<ushort>? Ports { get; set; }
        public List<string>? Urls { get; set; }
        public bool Selected { get; set; }
        public string? Description { get; set; }
        public string? IconUrl { get; set; }
    }
}
