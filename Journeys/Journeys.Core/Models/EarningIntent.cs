namespace Journeys.Core.Models;

/// <summary>Whether a campaign accrues loyalty points (drives the order-event default in EventModels).</summary>
public enum EarningIntent
{
    Unknown = 0,
    PointEarning = 1,
    NotPointEarning = 2
}
