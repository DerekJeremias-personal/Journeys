namespace Journeys.Core.Services
{
    public class AzureServerIdService
    {
        private readonly HttpClient _httpClient;
        private string _serverId = "Unknown Azure Server ID"; // Default fallback value

        public AzureServerIdService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task InitializeAsync()
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Add("Metadata", "true");
                _serverId = await _httpClient.GetStringAsync("http://169.254.169.254/metadata/instance/compute?api-version=2021-01-01");
            }
            catch
            {
                _serverId = "Unknown Azure Server ID";
            }
        }

        public string GetServerId() => _serverId;
    }
}
