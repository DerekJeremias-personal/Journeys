
using Backend.Dto.Dynamic;
using Backend.Dto.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.Core.Utility
{
    public static class DynamicNodeUtilities
    {


        private const string LOYALTY_ACCOUNT_ID_PATH = "loyaltyAccount.id";
        public static string GetLoyaltyAccountId(this IDynamicEntity entity)
        {
            var value = DynamicHelper.ReadNodeValue(entity, LOYALTY_ACCOUNT_ID_PATH);
            var result = value.ToString();
            if (result == null)
            {
                throw new Exception($"{LOYALTY_ACCOUNT_ID_PATH} not found");
            }
            return result;
        }

        public static List<string> GetTaxonomyKeys(this IDynamicEntity entity, string propertyPath, string path)
        {
            var value = DynamicHelper.ReadNodeValue(entity, propertyPath);
            if (value == null)
            {
                throw new Exception($"{path} not found");
            }
            if (value is string)
            {
                var result = value.ToString();
            }
            else if (value is IEnumerable<string> stringEnumerable)
            {
                // obj is any enumerable collection of strings
                return stringEnumerable.ToList(); // Convert to List if needed
            }
            throw new Exception($"GetTaxonomyKeys:: {path} results in unknown value type.");
        }


    }
}
