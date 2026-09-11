using Journeys.Core.RulesEngine.Comparitors.Enums;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Journeys.Core.RulesEngine.Comparitors
{
    public class TemporalEvaluation
    {
        private readonly ILogger<TemporalEvaluation> _logger;

        public TemporalEvalType Comparison { get; set; }

        #region Inclusion Evaluation...
        public bool EvaluateTemporalInclusion(DateTimeOffset timeOfOccurrence, TimeSpan time)
        {
            var compDate = CalculateComparisonDate(DateTime.UtcNow, time);
            switch (Comparison)
            {
                case TemporalEvalType.OnOrAfter:
                    return timeOfOccurrence >= compDate;
                case TemporalEvalType.After:
                    return timeOfOccurrence > compDate;
                //TODO: Likely should split before to be a DateTimeOffset comparison and After a TimeSpan...
                case TemporalEvalType.OnOrBefore:
                    return timeOfOccurrence <= compDate;
                case TemporalEvalType.Before:
                    return timeOfOccurrence < compDate;
            }
            throw new NotSupportedException($"A Comparison type of {Comparison} is not supported.");
        }
        private DateTimeOffset CalculateComparisonDate(DateTimeOffset timeOfOccurrence, TimeSpan time)
        {
            switch (Comparison)
            {
                case TemporalEvalType.After:
                case TemporalEvalType.OnOrAfter:
                    return timeOfOccurrence.Subtract(time);
                case TemporalEvalType.Before:
                case TemporalEvalType.OnOrBefore:
                    return timeOfOccurrence.Add(time);
                default:
                    throw new NotSupportedException($"A Comparison type of {Comparison} is not supported.");
            }
        }
        #endregion

        public DateTimeOffset CalculateDecayDate(DateTimeOffset timeOfOccurrence, TimeSpan time)
        {
            try
            {
                if (!EvaluateTemporalInclusion(timeOfOccurrence, time))
                {
                    throw new InvalidOperationException("A temporal evaluation cannot have a decay date if the evaluation does not pass.");
                }

                switch (Comparison)
                {
                    case TemporalEvalType.After:
                    case TemporalEvalType.OnOrAfter:
                        return timeOfOccurrence.Add(time);
                    //Below is wrong... Leaving it for now to show the error...
                    //I'm thinking through how we handled variations of decay other than
                    //relative time ago or static dates.
                    case TemporalEvalType.Before:
                    case TemporalEvalType.OnOrBefore:
                        return timeOfOccurrence.Add(time);
                    default:
                        throw new NotSupportedException($"A Comparison type of {Comparison} is not supported.");
                }
            }
            catch (NotSupportedException ex)
            {
                _logger.LogError(ex);
                throw;
            }
        }

        [JsonConstructor]
        public TemporalEvaluation(TemporalEvalType comparison)
        {
            Comparison = comparison;
        }
    }
}
