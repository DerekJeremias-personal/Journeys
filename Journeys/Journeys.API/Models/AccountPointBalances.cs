namespace Journeys.API.Models
{
    public class AccountPointBalance
    {
        public string PointAccountTypeId { get; set; }
        public string AccountId { get; set; }
        public decimal? CurrentBalance { get; set; }
        public decimal? LifetimeTotal { get; set; }
    }
}
