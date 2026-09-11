using Journeys.Core.RulesEngine.Providers.Historical;
using Journeys.Core.RulesEngine.Providers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Journeys.Core.RulesEngine.Comparitors
{
    [JsonDerivedType(typeof(DateEvaluation), typeDiscriminator: nameof(DateEvaluation))]
    [JsonDerivedType(typeof(NumericEvaluation), typeDiscriminator: nameof(NumericEvaluation))]
    [JsonDerivedType(typeof(StringEvaluation), typeDiscriminator: nameof(StringEvaluation))]
    [JsonDerivedType(typeof(BoolEvaluation), typeDiscriminator: nameof(BoolEvaluation))]
    public interface IEvaluatable
    {
        /// <summary>
        /// Evaluates a boolean function against two objects.
        /// Formula reads: left [comparison type] right evaluates to true|false
        /// </summary>
        /// <typeparam name="T">The type of the values to compare</typeparam>
        /// <param name="left">The left value</param>
        /// <param name="right">The right value</param>
        /// <returns>The output of the comparison</returns>
        bool Evaluate<T>(T left, T right);
    }
}
