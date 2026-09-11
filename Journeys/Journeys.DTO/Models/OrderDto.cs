//using Journeys.DTO.Interfaces;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Text.Json.Serialization;
//using System.Threading.Tasks;

//namespace Journeys.DTO.Models
//{
//    public class OrderDto : DtoModelBase, ITypeIdentifier
//    {
//        public OrderDto() { } // Parameterless constructor

//        public string ExtOrderId { get; set; }
//        public string LedgerTypeId { get; set; }
//        public string AccountId { get; set; }
//        public double Subtotal { get; set; }
//        public double TaxAmount { get; set; }
//        public double TotalAmount { get; set; }
//        public DateTimeOffset TransactionDate { get; set; }
//        public string LocationId { get; set; }
//        public List<ItemDto>? Items { get; set; }
//        public List<PaymentDto>? Payments { get; set; }
//        public List<DiscountDto>? Discounts { get; set; }

//        public string EventType { get; set; }
//    }


//    public class ItemDto : DtoModelBase
//    {

//        public string ExtItemId { get; set; }
//        public decimal UnitPrice { get; set; }
//        public decimal Quantity { get; set; }
//        public Dictionary<string, decimal> Taxes { get; set; }
//        public decimal NoTaxTotal { get; set; }
//        public decimal LineTotal { get; set; }

//        //Derek's original object
//        //public ItemDto() { } // Parameterless constructor

//        //public string ExtItemId { get; set; }
//        //public string LedgerTypeId { get; set; }
//        //public string ItemNumber { get; set; }

//        //public string ModifiesExternalId { get; set; }

//        //public double Amount { get; set; }
//        //public double? WholesaleAmount { get; set; }
//        //public double? Tax { get; set; }
//        //public double TotalAmount { get; set; }
//        //public string ItemName { get; set; }
//        //public string Description { get; set; }
//        //public string Category { get; set; }
//        //public string Sku { get; set; }
//        //public List<ItemDto>? Modifiers { get; set; }

//    }

//    public class PaymentDto : DtoModelBase
//    {
//        public PaymentDto() { } // Parameterless constructor

//        public string ExtPaymentId { get; set; }
//        public string LedgerTypeId { get; set; }
//        public double Amount { get; set; }
//        public string PaymentName { get; set; }
//        public string Description { get; set; }

//    }


//    public class DiscountDto : DtoModelBase
//    {
//        public DiscountDto() { } // Parameterless constructor

//        public string ExtDiscountId { get; set; }
//        public string LedgerTypeId { get; set; }
//        public double Amount { get; set; }



//    }



//}
