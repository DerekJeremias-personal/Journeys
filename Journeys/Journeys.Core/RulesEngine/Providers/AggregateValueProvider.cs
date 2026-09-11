using Backend.Dto.Dynamic;
using Journeys.Core.RulesEngine.Engine;
using Journeys.Core.RulesEngine.Providers.Enums;
using Journeys.Core.RulesEngine.Rules;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.Core.RulesEngine.Providers
{
    public class AggregateValueProvider : ProviderBase, IValueProvider
    {
        public AggregateValueProvider() { }
        public AggregateValueProvider(AggregateType aggregateType, IValueProvider rowProvider, IValueProvider rowPropertyProvider)
        {
            AggregateType = aggregateType;
            RowProvider = rowProvider;
            RowPropertyProvider = rowPropertyProvider;
        }

        public override string Kind => ProviderKindDiscriminators.AggregateValueProvider;

        private static IValueProvider ValidateProvider(IValueProvider? provider)
        {
            if (provider == null)
            {
                throw new ArgumentNullException("RowPropertyProvider must NOT be null prior to applying aggregate operations.");
            }

            return provider;
        }

        private static async Task<List<decimal>> GetValuesAsync(IEnumerable<RulesEngineState> nodes, IValueProvider provider, CancellationToken token)
        {
            var values = nodes.Select(async node =>
            {
                var value = await ValidateProvider(provider).GetValue<object>(node, token);
                return value switch
                {
                    decimal d => d, // if it's a decimal, use it directly
                    double dbl => (decimal)dbl, // convert double to decimal
                    long l => (decimal)l, // convert long to decimal
                    string str => decimal.TryParse(str, out var result)
                        ? result : throw new InvalidCastException($"Cannot convert string {str} to decimal"), // Attempt string conversion
                    _ => throw new InvalidCastException("Unsupported type") // handle unsupported types
                };
            }).ToList();

            var results = await Task.WhenAll(values); // Get the results after all tasks are complete
            return results.ToList(); //.Select(value => value.Result);
        }

        private static Dictionary<AggregateType, Func<IEnumerable<RulesEngineState>, IValueProvider, CancellationToken, Task<decimal?>>>
            _aggregateFunctions = new Dictionary<AggregateType, Func<IEnumerable<RulesEngineState>, IValueProvider, CancellationToken, Task<decimal?>>>
            {
                    { AggregateType.Average, async (nodes, provider, token) => {

                            var values = await GetValuesAsync(nodes, provider, token);
                            return values.Average(v => v);
                        }
                    },
                    { AggregateType.Min, async (nodes, provider, token) => {
                            var values = await GetValuesAsync(nodes, provider, token);
                            return values.Min(v => v);
                        }
                    },
                    { AggregateType.Max, async (nodes, provider, token) => {
                            var values = await GetValuesAsync(nodes, provider, token);
                            return values.Max(v => v);
                        }
                    },
                    { AggregateType.Sum, async (nodes, provider, token) => {
                            var results = await GetValuesAsync(nodes, provider, token);
                            return results.Sum(v => v); // Sum the converted values
                        }
                    },
                    { AggregateType.Count, async (nodes, provider, token) => nodes.Count() }
            };

        public AggregateType AggregateType { get; set; }
        public IValueProvider? RowProvider { get; set; }
        public IValueProvider? RowPropertyProvider { get; set; }
        /// <summary>
        /// Optional rule evaluated per row. When set, only rows for which Constraint.Evaluate returns true are included in the aggregate.
        /// The state passed to Evaluate is a clone with Event = the current row (see RulesEngineState.NavigatePayload), so use
        /// PropertyPath "event" (or "event.sku", etc.) to reference the row. Use a TaxonomicRule here to filter by taxonomy SKU
        /// (e.g. only sum price for items whose sku is under Electronics.TV).
        /// </summary>
        public RuleBase? Constraint { get; set; }
        public async Task<T?> GetValue<T>(RulesEngineState entity, CancellationToken token)
        {
            if (!typeof(T).IsAssignableFrom(typeof(decimal)))
            {
                throw new ArgumentException("AggregateValueProvider only supports decimal values.");
            }

            if (RowProvider == null)
            {
                throw new NullReferenceException("RowProvider must NOT be null prior to applying aggregate operations.");
            }

            if (RowPropertyProvider == null)
            {
                throw new NullReferenceException("RowPropertyProvider must NOT be null prior to applying aggregate operations.");
            }

            var rows = await RowProvider.GetValue<DynamicList>(entity, token);
            if (rows == null)
            {
                return default;
            }

            if (!_aggregateFunctions.ContainsKey(AggregateType))
            {
                throw new ArgumentException($"AggregateType {AggregateType} is not supported.");
            }

            var filteredRows = new List<RulesEngineState>();
            if (Constraint != null)
            {
                foreach (var row in rows)
                {
                    var cloneState = entity.NavigatePayload(row);
                    //We need to move the globals and loyalty account objects to the level of the row.
                    //TODO: Fix this, when write node triggers it is not actually writing the value.

                    if (await Constraint.Evaluate(cloneState, token))
                    {
                        lock (filteredRows)
                        {
                            filteredRows.Add(cloneState);
                        }
                    }
                };
            }
            else
            {
                filteredRows = rows.Select(x => entity.NavigatePayload(x)).ToList();
            }

            var result = await _aggregateFunctions[AggregateType](filteredRows, RowPropertyProvider, token);
            return (T?)(object)result;
        }
    }
}
