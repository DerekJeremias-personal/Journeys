
using Backend.Dto.Structures.Model;
using Journeys.DTO.Exceptions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Journeys.Core.Utility
{
    public static class ModelUtility
    {
        private static readonly string METADATA_KEY = "modelMetaData";
        private static readonly string IS_LOYALTY_KEY = "IsLoyaltyAccount";
        private static readonly string WRAPPER_MODEL_KEY = "Wrapper";

        public static T? GetMetaData<T>(ModelDto model, string key)
        {
            if (model == null || model.ModelMetaData == null || !model.ModelMetaData.ContainsKey(key))
            {
                return default(T);
            }

            var value = model.ModelMetaData[key];
            if (typeof(T).IsAssignableFrom(typeof(string))){
                return (T)Convert.ChangeType(value, typeof(T));
            } else {
                var typedValue = JsonConvert.DeserializeObject<T>(value);
                return typedValue;
            }
        }

        public static string GetContainerModelID(ModelDto model)
        {
            var wrapperId = GetMetaData<string>(model, WRAPPER_MODEL_KEY);
            if (String.IsNullOrEmpty(wrapperId))
            {
                throw new APIErrorsException(new Dictionary<string, string> { { "INVALID_MODEL", $"The specified model of {model.Name} is not configured with as a engine wrapped object or is misconfigured, MetaData Key '{WRAPPER_MODEL_KEY}' was empty or not found." } });
            }
            return wrapperId;
        }

        public static bool IsLoyaltyAccount(ModelDto model)
        {
            var isLoyalty = GetMetaData<bool>(model, IS_LOYALTY_KEY);
            return isLoyalty;
        }
    }
}
