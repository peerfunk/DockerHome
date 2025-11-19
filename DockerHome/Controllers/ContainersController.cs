using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using DockerHome.Services;
using System.Text.Json;

namespace DockerHome.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ContainersController : ControllerBase
    {
        private readonly ContainerService _containerService;
        private readonly DockerHubClient _hub;
        private readonly string _configPath = "config.json";

        public ContainersController(ContainerService containerService,DockerHubClient hub)
        {
            _containerService = containerService;
            _hub = hub;
        }


        [HttpGet("all")]
        public async Task<IActionResult> GetAllContainers()
        {
            var containers = await _containerService.GetAllContainersAsync();

            foreach (var c in containers)
            {
                var info = await _hub.GetDockerHubInfo(c.Image);
                c.Description = info.description;
                c.IconUrl = info.logoUrl;
                
            }
           

            return Ok(containers);
        }


        [HttpGet("display")]
        public async Task<IActionResult> GetDisplay()
        {
            if (!System.IO.File.Exists(_configPath))
                return Ok(new List<ContainerDto>());

            var config = JsonSerializer.Deserialize<List<ContainerDto>>(System.IO.File.ReadAllText(_configPath));
            var current = await _containerService.GetAllContainersAsync();

            foreach (var cfg in config)
            {
                var match = current.FirstOrDefault(x => x.Id == cfg.Id);
                cfg.Running = match?.Running ?? false;
            }

            return Ok(config);
        }

        [HttpPost("display")] 
        public IActionResult SaveDisplay(List<ContainerDto> containers)
        {
            System.IO.File.WriteAllText(_configPath,
                JsonSerializer.Serialize(containers, new JsonSerializerOptions { WriteIndented = true }));

            return Ok();
        }

        private async Task<DockerHubInfo?> FetchDockerHubInfoAsync(string image)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(image))
                    return null;

                // split "nginx:latest" or "mysql" or "docker.elastic.co/elasticsearch/elasticsearch:8.12"
                string cleaned = image.Split('@')[0]; // remove digests
                cleaned = cleaned.Split(':')[0]; // remove tag

                string namespacePart = "library";
                string imagePart = cleaned;

                // handle images like: redis:7-alpine → redis
                // handle images like: docker.elastic.co/elasticsearch/elasticsearch
                if (cleaned.Contains("/"))
                {
                    var seg = cleaned.Split('/');
                    namespacePart = seg[^2];
                    imagePart = seg[^1];
                }

                string url = $"https://hub.docker.com/v2/repositories/{namespacePart}/{imagePart}/";

                using var http = new HttpClient();
                var json = await http.GetStringAsync(url);

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                return new DockerHubInfo
                {
                    Description = root.GetProperty("description").GetString(),
                    HubUrl = $"https://hub.docker.com/r/{namespacePart}/{imagePart}"
                };
            }
            catch
            {
                return null; // fail silently (internal images, private registries)
            }
        }

        public class DockerHubInfo
        {
            public string Description { get; set; }
            public string HubUrl { get; set; }
        }
    }
}