using System.Net.Http;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;

namespace DockerHome.Services
{
    public class DockerHubClient
    {
        private readonly HttpClient _http;

        public DockerHubClient(HttpClient http)
        {
            _http = http;
        }

        /// <summary>
        /// Gets a Docker Hub description and logo URL for a repository.
        /// Image name may include user/org e.g. "library/redis".
        /// </summary>
        public async Task<(string description, string logoUrl)> GetDockerHubInfo(string imageName)
        {
            try
            {
                string repo = NormalizeImageName(imageName);

                // 1) Fetch description (official API)
                string desc = await FetchDescription(repo);
                if (desc == null)
                {
                    return (null, null);
                }

                // 2) Fetch logo URL (HTML scraping)
                string logo = await FetchLogoUrl(repo);

                return (desc, logo);
            }
            catch
            {
                return (null, null);
            }
        }

        private string NormalizeImageName(string imageName)
        {
            if (string.IsNullOrWhiteSpace(imageName))
                return string.Empty;

            imageName = imageName.Trim().ToLower();

            // Remove Docker Hub registry prefix
            if (imageName.StartsWith("docker.io/"))
                imageName = imageName.Substring("docker.io/".Length);

            // Remove @sha256 digest if present
            int digestIndex = imageName.IndexOf('@');
            if (digestIndex >= 0)
                imageName = imageName.Substring(0, digestIndex);

            // Remove :tag if present
            int tagIndex = imageName.LastIndexOf(':');
            if (tagIndex > 0) // ensure it's not "http://"
                imageName = imageName.Substring(0, tagIndex);

            // Add library/ for official images
            if (!imageName.Contains("/"))
                imageName = "library/" + imageName;

            return imageName;
        }


        private async Task<string> FetchDescription(string repo)
        {
            string url = $"https://hub.docker.com/v2/repositories/{repo}/";
            var response = await _http.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                return null;

            var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (json.RootElement.TryGetProperty("description", out var descProp))
                return descProp.GetString();

            return null;
        }

        private async Task<string> FetchLogoUrl(string repo)
        {
            
            string url = $"https://hub.docker.com/api/media/repos_logo/v1/{HttpUtility.UrlEncode(repo)}?type=logo";
            return url;
            var html = await _http.GetStringAsync(url);

            // regex → finds: /api/media/repos_logo/v1/library%2Fredis?type=logo
            var match = Regex.Match(html, @"src=""(/api/media/repos_logo/v1/[^""]+)""");

            if (!match.Success)
                return null;

            return "https://hub.docker.com" + match.Groups[1].Value;
        }
    }
}
