using Journeys.Core.JsonConverters;
using Journeys.Core.RulesEngine.Journey;
using Journeys.Core.RulesEngine.Journey.Enums;
using Journeys.Core.Models;
using Journeys.Core.RulesEngine.Outcomes;
using Journeys.Core.RulesEngine.Rules;
using Journeys.Core.RulesEngine.Rules.Composite;
using Journeys.Core.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Journeys.Core.RulesEngine
{
    public class RuleSet : ModelBase
    {
        public RuleSet() { }

        [JsonConstructor]
        public RuleSet(string name, JsonElement? ruleJsonElement, string? rootRuleDiscriminator, JsonElement? outcomesJsonElement,
                        string? id) : base(id)
        {
            Name = name;
            Rule.Serialized = ruleJsonElement;
            RootRuleDiscriminator = rootRuleDiscriminator;
            OutcomeSet.Serialized = outcomesJsonElement;
        }

        string _id;
        public new string Id
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(_id) && !_id.Trim().ToLower().Equals("undefined"))
                    return _id;
                
                return Name;
            }
            set
            {
                _id = value;
            }
        }
        public string Name { get; set; }

        public JsonProperty<RuleBase> Rule { get; } = new JsonProperty<RuleBase>();
        public JsonProperty<List<OutcomeBase>> OutcomeSet { get; } = new JsonProperty<List<OutcomeBase>>();

        public string? RootRuleDiscriminator { get; set; }

        public JsonElement? RuleJsonElement
        {
            get => Rule.Serialized;
            set => Rule.Serialized = value;
        }

        public JsonElement? OutcomesJsonElement
        {
            get => OutcomeSet.Serialized;
            set => OutcomeSet.Serialized = value;
        }

        [JsonIgnore]
        public RuleBase? RuleTree
        {
            get => Rule.Deserialized;
            set => Rule.Deserialized = value;
        }

        [JsonIgnore]
        public List<OutcomeBase>? Outcomes
        {
            get => OutcomeSet.Deserialized;
            set => OutcomeSet.Deserialized = value;
        }

        public List<T> FlattenToRulesOfType<T>() where T : RuleBase
        {
            var result = new List<T>();
            if (RuleTree == null)
            {
                return result;
            }

            if (RuleTree is T tree)
                result.Add(tree);

            if (RuleTree is CompositeRuleBase compositeRule)
            {
                result.AddRange(compositeRule.FlattenChildrenToRulesOfType<T>());
            }

            return result;
        }
    }
}
