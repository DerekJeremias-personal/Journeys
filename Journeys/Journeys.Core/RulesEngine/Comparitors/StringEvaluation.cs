using Journeys.Core.RulesEngine.Providers.Historical;
using Journeys.Core.RulesEngine.Providers;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Journeys.Core.RulesEngine.Comparitors.Enums;
using Microsoft.Extensions.Logging;
using Backend.Dto.Interfaces;
using Backend.Dto.Dynamic;

namespace Journeys.Core.RulesEngine.Comparitors
{
    public class StringEvaluation : IEvaluatable
    {
        private readonly ILogger<StringEvaluation> _logger;

        public StringEvaluation(ILogger<StringEvaluation> logger)
        {
            _logger = logger;
        }

        private static Dictionary<StringEvalType, Func<string, string, bool>> _comparisonMap = new Dictionary<StringEvalType, Func<string, string, bool>>
            {
                // Mapping of StringEvalTypes to comparison functions
                { StringEvalType.Equal, (l, r) => l == r },
                { StringEvalType.Contains, (l, r) => l.Contains(r) },
                { StringEvalType.StartsWith, (l, r) => l.StartsWith(r) },
                { StringEvalType.EndsWith, (l, r) => l.EndsWith(r) },
                { StringEvalType.MatchesRegex, (l, r) => Regex.IsMatch(l, r) },
                { StringEvalType.NotEqual, (l, r) => l != r },
            };

        public StringEvaluation() { }
        public StringEvaluation(StringEvalType comparison)
        {
            Comparison = comparison;
        }
        public StringEvalType Comparison { get; set; }
        public bool Evaluate<T>(T left, T right)
        {
            try
            {
                if (left == null || right == null) return false;

                if (Comparison == StringEvalType.InCollection)
                {
                    if (!typeof(T).IsAssignableTo(typeof(IDynamicEntity)))
                        throw new ArgumentException("InCollection comparison requires IDynamicEntity type");

                    var leftNode = left as IDynamicEntity;
                    var rightNode = right as DynamicList;
                    if (leftNode == null || rightNode == null)
                    {
                        throw new InvalidCastException("InCollection comparison requires IDynamicEntity and DynamicList types");
                    }
                    var leftNodeValue = (string)leftNode.GetValue();
                    return rightNode.Any(x => ((string)x.GetValue()).Equals(leftNodeValue));
                }
                else if (_comparisonMap.TryGetValue(Comparison, out var func))
                {
                    return func(Convert.ToString(left) ?? string.Empty, Convert.ToString(right) ?? string.Empty);
                }
                throw new InvalidOperationException($"Invalid comparison type of {Enum.GetName(Comparison)}");
            }
            catch (InvalidCastException ex)
            {
                _logger.LogError(ex);
                throw;
            }
            catch (InvalidOperationException ex) 
            {
                _logger.LogError(ex);
                throw;
            }
        }
    }
}
