using Journeys.DAL.Interfaces;
using Journeys.Interfaces.DataStorage;
using Journeys.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.DAL.Adapters
{
    public class OrderAdapter : ReferenceableBaseAdapter<Order>, IOrderAdapter
    {
        private const string ORDER_SCHEMA_ID = "a6edbbc5-bf43-4c57-b2f1-e015b9efaf03";

        public OrderAdapter(IDynamicDataAdapter dynAdapter, IExternalReferenceAdapter extReferenceAdapter) : base(dynAdapter, extReferenceAdapter)
        {
        }

        ///// <summary>
        ///// Ensure external reference is made
        ///// </summary>
        //public override Func<Order, Task<string>> ExtIdProvider { get; } = async entity => entity.ExtOrderId;

        public async Task<Order> FetchOrderAsync(string tenantId, string orderId)
        {
            return await base.FetchEntityAsync(tenantId, orderId, ORDER_SCHEMA_ID).ConfigureAwait(false);
        }

        public async Task<List<Order>> GetOrdersAsync(string tenantId, List<string> filters)
        {
            return await base.GetEntitiesByFiltersAsync(tenantId, ORDER_SCHEMA_ID, filters).ConfigureAwait(false);
        }

        public async Task<Order> UpsertOrderAsync(string tenantId, Order order)
        {
            return await base.UpsertEntityAsync(tenantId, ORDER_SCHEMA_ID, order).ConfigureAwait(false);
        }

        public async Task DeleteOrderAsync(string tenantId, string orderId)
        {
            await base.DeleteEntityAsync(tenantId, ORDER_SCHEMA_ID, orderId).ConfigureAwait(false);
        }

        public async Task DeleteOrderAsync(string tenantId, Order order)
        {
            await base.DeleteEntityAsync(tenantId, ORDER_SCHEMA_ID, order).ConfigureAwait(false);
        }


    }
}
