using Journeys.Core.RulesEngine.Rules;

namespace Journeys.Core.RulesEngine.Rules.Composite
{
    public abstract class CompositeRuleBase : RuleBase
    {
        public IList<RuleBase> Children { get; set; } = new List<RuleBase>();

        public List<T> FlattenChildrenToRulesOfType<T>()
        {
            var result = new List<T>();
            foreach (var child in Children)
            {
                if (child is T typedChild)
                {
                    result.Add(typedChild);
                }

                if (child is CompositeRuleBase compositeRule)
                {
                    result.AddRange(compositeRule.FlattenChildrenToRulesOfType<T>());
                }
            }
            return result;
        }
    }

}

