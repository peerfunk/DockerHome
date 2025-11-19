using Docker.DotNet;
using Docker.DotNet.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DockerHome.Services
{
    public class ContainerService
    {
        private readonly IConfiguration _config;
        private readonly DockerClient _docker;

        public ContainerService(IConfiguration configuration)
        {
            _config = configuration;
            DockerClientConfiguration config;

            if (OperatingSystem.IsWindows())
            {
                // Windows Docker Desktop default endpoint
                config = new DockerClientConfiguration(new Uri("npipe://./pipe/docker_engine"));
            }
            else
            {
                // Linux endpoint when running inside a container
                config = new DockerClientConfiguration(new Uri("unix:///var/run/docker.sock"));
            }

            _docker = config.CreateClient();
        }

        // -----------------------------
        // ⚡ Helper: Convert container
        // -----------------------------
        private  ContainerDto Map(ContainerListResponse c)
        {
            var ports = c.Ports?
                .Where(p => p.PublicPort >0)
                .Select(p => p.PublicPort)
                .ToList() ?? new List<ushort>();
            var urls = c.Ports?
                .Where(p => p.PublicPort >0 && p.PrivatePort == 443 || p.PrivatePort  == 80 || p.PrivatePort == 8080)
                .Select(p =>
                {
                    if (p.PrivatePort == 80 || p.PrivatePort == 8080)
                    {
                        return $"http://{_config["Hostname"]}" + (p.PrivatePort == 80? "": ":"+ p.PublicPort);
                    }
                    else if (p.PrivatePort == 443)
                    {
                        return $"https://{_config["Hostname"]}" + (p.PrivatePort == 443? "": ":"+ p.PublicPort);
                    }

                    return "";
                })
                .ToList();
            c.Labels.TryGetValue("com.docker.compose.project", out var composeProject);

            return new ContainerDto
            {
                Id = c.ID,
                Name = c.Names?.FirstOrDefault()?.Trim('/') ?? "",
                Image = c.Image,
                Running = c.State == "running",
                Ports = ports,
                Urls = urls,
                ComposeProject = composeProject ?? "uncategorized"
            };
        }

        // -----------------------------
        // 🔍 Get all containers
        // -----------------------------
        public async Task<List<ContainerDto>> GetAllContainersAsync()
        {
            var containers = await _docker.Containers.ListContainersAsync(
                new ContainersListParameters { All = true });

            return containers.Select(Map).ToList();
        }

        // -----------------------------
        // 🔍 Get only running containers
        // -----------------------------
        public async Task<List<ContainerDto>> GetRunningContainersAsync()
        {
            var containers = await _docker.Containers.ListContainersAsync(
                new ContainersListParameters { All = false });

            return containers.Select(Map).ToList();
        }

        // -----------------------------
        // 🔍 Get container by ID
        // -----------------------------
        public async Task<ContainerDto?> GetContainerByIdAsync(string id)
        {
            var containerList = await GetAllContainersAsync();
            return containerList.FirstOrDefault(x => x.Id.StartsWith(id));
        }

        // -----------------------------
        // ▶ Start container
        // -----------------------------
        public async Task<bool> StartContainerAsync(string id)
        {
            try
            {
                await _docker.Containers.StartContainerAsync(id, new ContainerStartParameters());
                return true;
            }
            catch
            {
                return false;
            }
        }

        // -----------------------------
        // ■ Stop container
        // -----------------------------
        public async Task<bool> StopContainerAsync(string id)
        {
            try
            {
                await _docker.Containers.StopContainerAsync(id, new ContainerStopParameters
                {
                    WaitBeforeKillSeconds = 5
                });
                return true;
            }
            catch
            {
                return false;
            }
        }

        // -----------------------------
        // 📦 Group by compose project
        // -----------------------------
        public async Task<Dictionary<string, List<ContainerDto>>> GetComposeProjectsAsync()
        {
            var containers = await GetAllContainersAsync();

            return containers
                .GroupBy(x => x.ComposeProject)
                .ToDictionary(g => g.Key, g => g.ToList());
        }
    }

    // ----------------------------------------
    // DTO for frontend
    // ----------------------------------------
    public class ContainerDto
    {
        public string Id { get; set; }
        public string? Name { get; set; }
        public bool? Running { get; set; }
        public string? Image { get; set; }
        public string? ComposeProject { get; set; }
        public List<ushort>? Ports { get; set; }    
        public string? Description { get; set; } = "";
        public string? IconUrl { get; set; } = "";
        public List<string>? Urls { get; set; }
        public string? HubUrl { get; set; }
        public bool Selected { get; set; }
    }
}
