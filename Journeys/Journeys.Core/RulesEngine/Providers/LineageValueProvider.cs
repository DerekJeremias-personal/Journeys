using Backend.Dto.Interfaces;
using Journeys.Core.RulesEngine.Rules;

namespace Journeys.Core.RulesEngine.Providers
{
    public class LineageValueProvider : PathValueProvider
    {
        public override string Kind => ProviderKindDiscriminators.LineageValueProvider;

        public async Task<T?> GetValue<T>(IDynamicEntity entity, CancellationToken token)
        {
            //Get the key from the object
            //Bag the value for normalization
            //Wait for normalization batch to complete
            //Return the normalized value with it's lineage
            throw new NotImplementedException();
        }
    }
}
