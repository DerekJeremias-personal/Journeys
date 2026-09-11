using Journeys.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.Interfaces.DataStorage
{
    public interface IOrderAdapter
    {
        Task<Order> FetchOrderAsync(string tenantId, string orderId);

        Task<List<Order>> GetOrdersAsync(string tenantId, List<string> filters);

        Task<Order> UpsertOrderAsync(string tenantId, Order order);

        Task DeleteOrderAsync(string tenantId, string orderId);

        Task DeleteOrderAsync(string tenantId, Order order);
    }
}
