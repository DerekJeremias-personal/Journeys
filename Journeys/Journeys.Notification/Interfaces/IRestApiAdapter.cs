using System.Threading.Tasks;
using Journeys.Notification.Models;

namespace Journeys.Notification.Interfaces
{
    public interface IRestApiAdapter
    {
        Task<bool> SendAsync(RestApiConfig config, object payload);
    }
} 