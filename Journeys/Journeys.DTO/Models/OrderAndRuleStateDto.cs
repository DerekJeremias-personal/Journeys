using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Journeys.DTO.Models
{
    public class OrderAndRuleStateDto
    {
        [JsonPropertyName("accountid")]
        public string AccountId { get; set; }

        [JsonPropertyName("naturalkey")]
        public string NaturalKey { get; set; }

        [JsonPropertyName("timeofoccurrence")]
        public DateTime TimeOfOccurrence { get; set; }

        [JsonPropertyName("lastprocessed")]
        public DateTime LastProcessed { get; set; }

        [JsonPropertyName("event")]
        public LoyaltyOrderEvent Event { get; set; }

        [JsonPropertyName("appliedcampaigns")]
        public List<string> AppliedCampaigns { get; set; }

        [JsonPropertyName("appliedrulesetids")]
        public List<string> AppliedRuleSetIds { get; set; }

        [JsonPropertyName("tenantId")]
        public string TenantId { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }
    }


    public class LoyaltyOrderEvent
    {
        [JsonPropertyName("eventtype")]
        public string EventType { get; set; }

        [JsonPropertyName("dealername")]
        public string DealerName { get; set; }

        [JsonPropertyName("distributor")]
        public string Distributor { get; set; }

        [JsonPropertyName("distributorcode")]
        public string DistributorCode { get; set; }

        [JsonPropertyName("invoicenumber")]
        public string InvoiceNumber { get; set; }

        [JsonPropertyName("invoicedate")]
        public DateTime InvoiceDate { get; set; }

        [JsonPropertyName("calculateonly")]
        public bool CalculateOnly { get; set; }

        [JsonPropertyName("items")]
        public List<OrderItem> Items { get; set; }
    }
    public class OrderItem
    {
        [JsonPropertyName("itemnumber")]
        public string ItemNumber { get; set; }

        [JsonPropertyName("quantity")]
        public int Quantity { get; set; }

        [JsonPropertyName("value")]
        public decimal Value { get; set; }

        [JsonPropertyName("desc")]
        public string Description { get; set; }

        [JsonPropertyName("desc2")]
        public string Description2 { get; set; }

        [JsonPropertyName("loaddate")]
        public DateTime LoadDate { get; set; }
    }

}
