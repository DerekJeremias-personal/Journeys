


using Backend.Dto.Interfaces;
using Backend.Dto.Structures.Model;
using Backend.Dto.Utilities;
using Journeys.Core.Caching;
using Journeys.Core.Extensions;
using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Interfaces.Services;
using Journeys.Core.Models;
using Journeys.Core.RulesEngine.Providers;
using Journeys.Core.Services;
using Journeys.Core.Utility;
using Journeys.DTO.Exceptions;
using Journeys.DTO.Models;
using Microsoft.Azure.Amqp.Framing;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Journeys.Core.RulesEngine.Utility
{
    public class StateUtility
    {
        private const string RESERVED_EXT_ID_META_KEY = "xid";
        private const string RESERVED_ACCOUNT_EVENT_TYPE = "AccountDetails";
        private const string ACCOUNT_EXT_ID_META_KEY = "AccountXIdSymbol";
        private const string NATURAL_KEY_SYMBOLS_META_KEY = "NaturalKeySymbols";
        private const string TIME_OF_EVENT_META_KEY = "TimeOfOccurrence";
        private const string IS_LOYALTY_ACCOUNT_EVENT = "IsLoyaltyAccount";
        private const string EVENT_SYMBOL_KEY = "event";

        private static PathValueProvider IdempotencyProvider = new PathValueProvider("version");
        private static PathValueProvider NaturalKeyProvider = new PathValueProvider("naturalkey");
        private static PathValueProvider TypeProvider = new PathValueProvider("type");
        private static PathValueProvider IdProvider = new PathValueProvider("id");
        private static PathValueProvider EventTypeProvider = new PathValueProvider("eventtype");

        private const string WRAPPER_MODEL_META_KEY = "Wrapper";
        private readonly ModelCache _modelCache;
        private readonly ILoyaltyAccountService _loyaltyAccountService;
        private readonly IDynamicExternalReferenceAdapter _dynamicExtRefAdapter;
        private readonly ILogger<StateUtility> _logger;
        public StateUtility(ModelCache modelCache, ILoyaltyAccountService loyaltyAccountService, IDynamicExternalReferenceAdapter dynamicExternalReferenceAdapter, ILogger<StateUtility> logger) 
        {
            _modelCache = modelCache;
            _loyaltyAccountService = loyaltyAccountService;
            _dynamicExtRefAdapter = dynamicExternalReferenceAdapter;
            _logger = logger;
        }

        public async Task<(ModelDto eventModel, ModelDto wrapperModel)> LoadEventModels(string tenantId, string modelName)
        {
            //Get or Load the Model
            var eventModel = await _modelCache.GetOrLoadByName(tenantId, modelName);
            try
            {
                if (eventModel == null)
                    throw new APIErrorsException(new Dictionary<string, string> { { "InvalidModel", $"{modelName} was unable to be loaded or found." } });

                //We need to hydrate the apropriate wrapper object to store the eventModel appropriately.
                var wrapperModelKey = GetMetaDataValue(eventModel, WRAPPER_MODEL_META_KEY);
                //!eventModel.ModelMetaData.ContainsKey(NATURAL_KEY_SYMBOLS_META_KEY)
                if (wrapperModelKey == null)
                {
                    throw new APIErrorsException(new Dictionary<string, string> { { "InvalidModel", $"{modelName} is not correctly configured to process within the Rules Engine, please create the necessary RuleState wrapper and defaultPointAccounts the {WRAPPER_MODEL_META_KEY} Meta-Data property with the Wrapper Id on the eventModel. Also defaultPointAccounts the {NATURAL_KEY_SYMBOLS_META_KEY} property with the array of Symbols to be used for the natural key generation." } });
                }

                var wrapperModel = await _modelCache.GetOrLoadById(tenantId, eventModel.ModelMetaData[WRAPPER_MODEL_META_KEY]);

                if (wrapperModel == null)
                {
                    throw new APIErrorsException(new Dictionary<string, string> { { "InvalidModel", $"{WRAPPER_MODEL_META_KEY} is not correctly configured in the eventModel meta-data ALL engine payloads must specify the account ExternalId symbol." } });
                }

                EventModelContractValidation.ValidateNaturalKeySymbols(eventModel);

                return (eventModel, wrapperModel);
            }
            catch(APIErrorsException ex) 
            {
                _logger.LogError(ex);
                throw;
            }
        }

        public async Task<LoyaltyAccount?> GetLoyaltyAccountAsync(string tenantId, string accountId)
        {
            var loyaltyAccountEntity = await _loyaltyAccountService.GetLoyaltyAccountAsync(tenantId, accountId);
            return loyaltyAccountEntity?.FromDto();
        }

        public async Task<LoyaltyAccount?> GetLoyaltyAccountByXidAsync(string tenantId, string extRefId)
        {
            //Load the Loyalty Account from the external reference.
            var loyaltyAccountEntity = await _loyaltyAccountService.GetLoyaltyAccountByExtIdAsync(tenantId, extRefId);
            return loyaltyAccountEntity?.FromDto();
        }

        public async Task<LoyaltyAccount> LoadLoyaltyAccountAsync(string tenantId, IDynamicEntity? data, (ModelDto eventModel, ModelDto wrapperModel) modelDetails)
        {
            try
            {
                if (data == null)
                    throw new APIErrorsException(new Dictionary<string, string> { { "InvalidData", "The event object is null, a user cannot be loaded." } });

                if (!modelDetails.eventModel?.ModelMetaData?.ContainsKey(ACCOUNT_EXT_ID_META_KEY) ?? false)
                {
                    throw new APIErrorsException(new Dictionary<string, string> { { "InvalidModel", $"{ACCOUNT_EXT_ID_META_KEY} is not correctly configured in the eventModel meta-data ALL engine payloads must specify the account ExternalId symbol." } });
                }
                else
                {
                    var accountExtIdMetaKey = modelDetails.eventModel?.ModelMetaData?[ACCOUNT_EXT_ID_META_KEY];
                    if (accountExtIdMetaKey == null)
                    {
                        throw new APIErrorsException(new Dictionary<string, string> { { "InvalidModel", $"{ACCOUNT_EXT_ID_META_KEY} is not correctly configured in the eventModel meta-data ALL engine payloads must specify the account ExternalId symbol." } });
                    }
                    var isLoyaltyAccountEvent = IsLoyaltyAccountEvent(modelDetails);

                    //There always must be an external reference provided so that we are able to find the Loyaltyaccount
                    var extRefProvider = new PathValueProvider(accountExtIdMetaKey);
                    var extRefId = extRefProvider.GetValue<string>(data);
                    if (string.IsNullOrEmpty(extRefId))
                    {
                        if (isLoyaltyAccountEvent)
                        {
                            //last chance
                            var reservedExtIdMetaKey = modelDetails.eventModel?.ModelMetaData?[RESERVED_EXT_ID_META_KEY];
                            if (reservedExtIdMetaKey != null)
                            {
                                var resExtRefProvider = new PathValueProvider(reservedExtIdMetaKey);
                                extRefId = resExtRefProvider.GetValue<string>(data);
                            }
                        }
                        if (string.IsNullOrEmpty(extRefId))
                        {
                            throw new APIErrorsException(new Dictionary<string, string> { { "InvalidData", $"The property {GetMetaDataValue(modelDetails.eventModel, ACCOUNT_EXT_ID_META_KEY)} is required and must have a valid Account ExternalID." } });
                        }
                    }

                    string metaType = TypeProvider.GetValue<string>(data)
                            ?? modelDetails.eventModel?.Name
                            ?? throw new APIErrorsException(new Dictionary<string, string> { { "InvalidData", "Both the type value and the eventModel?.Name were null." } });

                    //Load the Loyalty Account from the external reference.
                    var loyaltyAccountEntity = await _loyaltyAccountService.GetLoyaltyAccountByExtIdAsync(tenantId, extRefId, IsLoyaltyAccountEvent(modelDetails));

                    //If this is any event other than a Loyalty Account Event, fail here because have no Loyalty Account to tie state to
                    // If it is a Loyalty Account Event then proceed, as this maybe a (first) creation of a Loyalty Account
                    if (loyaltyAccountEntity == null && !isLoyaltyAccountEvent)
                        throw new APIErrorsException(new Dictionary<string, string> { { "Error", $"The Loyalty Account with External Reference of {extRefId} was not found in the system." } });

                    var loyaltyAccountId = loyaltyAccountEntity?.Id; //could be null at this point
                    if (isLoyaltyAccountEvent)
                    {
                        //See if an id was provided [this would be them sending our internal Loyalty Account Id]
                        loyaltyAccountId ??= IdProvider.GetValue<string>(data); //could still be null
                    }

                    //If this is a LoyaltyAccount event, ensure LoyaltyAccount exists
                    if (isLoyaltyAccountEvent)
                    {
                        List<string> knownExtIds = new List<string> { extRefId };
                        if (loyaltyAccountEntity?.KnownExternalIds?.Any() ?? false)
                            knownExtIds.AddRange(loyaltyAccountEntity.KnownExternalIds);

                        //Creates the loyalty account or updates it.
                        loyaltyAccountEntity = await _loyaltyAccountService.UpsertLoyaltyAccountAsync(tenantId, new LoyaltyAccountDto
                        {
                            Id = string.IsNullOrEmpty(loyaltyAccountId) ? null : loyaltyAccountId,
                            ExtAccountId = extRefId,
                            TenantId = tenantId,
                            Type = metaType,
                            KnownExternalIds = knownExtIds.Distinct().ToList()
                        });

                        if (string.IsNullOrEmpty(loyaltyAccountEntity?.Id))
                        {
                            throw new APIErrorsException(new Dictionary<string, string> { { "InvalidData", $"Failed to create LoyaltyAccount for external reference Id: {extRefId}" } });
                        }

                        //Provide point zero balance point ledgers
                        var pointAccountTypes = await GetPointAccountTypes(tenantId) ?? new List<PointAccountType>();
                        if (pointAccountTypes?.Any() ?? false)
                        {
                            var defaultPointAccounts = pointAccountTypes;
                                        //.Where(x => x.Status.Equals("active", StringComparison.InvariantCultureIgnoreCase) &&
                                        //            x.LedgerType != PointLedgerTypeStrings.EXPIRED &&
                                        //            x.LedgerType != PointLedgerTypeStrings.ARCHIVE)
                                        //.ToList() ?? new List<PointAccountType>();
                            if (defaultPointAccounts.Any())
                            {
                                await _loyaltyAccountService.SetDefaultPointLedgersAsync(tenantId, loyaltyAccountEntity.FromDto(), defaultPointAccounts);
                            }
                        }

                        loyaltyAccountId = loyaltyAccountEntity.Id;
                    }

                    if (loyaltyAccountEntity == null)
                        throw new APIErrorsException(new Dictionary<string, string> { { "InvalidData", $"The LoyaltyAccount for the request was not found or could not created." } });

                    //Load loyalty account details...if not a LoyaltyAccount event
                    // we need to add it to the LoyaltAccount object for subsequent rule processing for this event
                    if (!IsLoyaltyAccountEvent(modelDetails))
                    {
                        return await _loyaltyAccountService.LoadLoyaltyAccountDetailsAsync(tenantId, loyaltyAccountEntity?.FromDto());
                    }

                    return loyaltyAccountEntity?.FromDto();
                }
            }
            catch (APIErrorsException ex)
            {
                _logger.LogError(ex);
                throw;
            }
        }

        public bool IsLoyaltyAccountEvent((ModelDto eventModel, ModelDto wrapperModel) modelDetails)
        {
            return (modelDetails.eventModel?.ModelMetaData?.ContainsKey(IS_LOYALTY_ACCOUNT_EVENT) ?? false)
                && bool.TryParse(modelDetails.eventModel?.ModelMetaData?[IS_LOYALTY_ACCOUNT_EVENT], out bool isTrue);
        }

        public static string? GetMetaDataValue(ModelDto? model, string key) 
        {
            try
            {
                if (model == null)
                    throw new APIErrorsException(new Dictionary<string, string> { { "InvalidModel", "The model object is null, metadata cannot be processed." } });

                if (model.ModelMetaData == null)
                    throw new APIErrorsException(new Dictionary<string, string> { { "InvalidModel", "The model object does not contain any metadata." } });

                if (model.ModelMetaData.TryGetValue(key, out string? value))
                {
                    return value;
                }
            }
            catch (APIErrorsException ex)
            {
                //would like to log this but no static logger
            }
            return null;
        }

        public string GenerateNaturalKey((ModelDto eventModel, ModelDto wrapperModel) modelDetails, IDynamicEntity data) 
        {
            var errors = new Dictionary<string, string>();
            //Handle Natural Key Generation for Idempotency
            var naturalKeyJson = GetMetaDataValue(modelDetails.eventModel, NATURAL_KEY_SYMBOLS_META_KEY);
            if(naturalKeyJson == null)
                throw new APIErrorsException(new Dictionary<string, string> { { "InvalidModel", $"{NATURAL_KEY_SYMBOLS_META_KEY} is not correctly configured in the eventModel meta-data ALL engine payloads must specify at least one natural key." } });
            var naturalKeySymbols = JsonHelper.DeserializeObject<string[]>(naturalKeyJson, false);
            var keys = new List<string>();
            foreach (var symbol in naturalKeySymbols)
            {
                var tmpProvider = new PathValueProvider(symbol);
                var value = tmpProvider.GetValue<string>(data);
                try
                {
                    if (value == null)
                    {
                        var isLoyaltyAccountEvent = IsLoyaltyAccountEvent(modelDetails);
                        if (isLoyaltyAccountEvent)
                        {
                            //If the symbol is "type" and this is an account event
                            // then we can default the type
                            if (symbol.ToLower().Equals("type"))
                            {
                                value = RESERVED_ACCOUNT_EVENT_TYPE;
                            }
                            else
                            {
                                //last chance
                                var reservedExtIdMetaKey = modelDetails.eventModel?.ModelMetaData?[RESERVED_EXT_ID_META_KEY];
                                if (reservedExtIdMetaKey != null)
                                {
                                    var resExtRefProvider = new PathValueProvider(reservedExtIdMetaKey);
                                    value = resExtRefProvider.GetValue<string>(data);
                                }
                            }
                        }
                        if (string.IsNullOrEmpty(value))
                        {
                            errors.Add(symbol, $"Natural Key Symbol {symbol} was not found in the payload or was null, Natural Key symbols must be constant and are required.");
                            throw new APIErrorsException(errors);
                        }
                    }
                    keys.Add(value);
                }
                catch (APIErrorsException ex)
                {
                    _logger.LogError(ex);
                    throw;
                }

            }
            var naturalKey = string.Join("|", keys);
            return naturalKey;
        }

        public (DateTimeOffset? requestTime, DateTimeOffset? eventTime)? EvaluateIdempotency(IDynamicEntity data, 
            WrappedEventPayload existingData, 
            (ModelDto eventModel, ModelDto wrapperModel) modelDetails,
            bool reprocessEvent = false)
        {
            try
            {
                var timeOfEventValue = GetMetaDataValue(modelDetails.eventModel, TIME_OF_EVENT_META_KEY);
                if (!string.IsNullOrEmpty(timeOfEventValue) &&
                    !modelDetails.eventModel.Attributes.Any(x => x.Symbol == timeOfEventValue))
                {
                    throw new APIErrorsException(new Dictionary<string, string> { { "InvalidModel", $"{TIME_OF_EVENT_META_KEY} is not correctly configured in the eventModel meta-data the value is {timeOfEventValue} but does not exist in the {modelDetails.eventModel.Name} model.." } });
                }

                (DateTimeOffset? requestTime, DateTimeOffset? eventTime)? eventTimes = null;

                //TimeOfOccurrence allows for roll forward processing where the last version
                //of each entity is what is the "final" version to process rules and outcomes
                //WARNING: If a Model does not contain TimeOfOccurrence any payload passed will
                //assume overwrite is intentional.
                if (!string.IsNullOrEmpty(timeOfEventValue) &&
                    modelDetails.eventModel.Attributes.Any(x => x.Symbol == timeOfEventValue))
                {
                    var TimeOfOccurrenceProvider = new PathValueProvider(timeOfEventValue);
                    var requestTime = EventOccurrenceResolver.TryGet(data, timeOfEventValue);
                    if (requestTime.IsNullOrMinDate())
                        requestTime = DateTimeOffset.UtcNow;

                    var existingTime = TimeOfOccurrenceProvider.GetValue<DateTimeOffset?>(existingData.Event);
                    if (existingTime.IsNullOrMinDate()) 
                        existingTime = existingData.TimeOfOccurrence;
                    
                    eventTimes = (requestTime, existingTime);

                    if (!existingTime.IsNullOrMinDate() && existingTime >= requestTime && !reprocessEvent && existingData.OutcomeStates?.Count > 0)
                    {
                        _logger.LogWarning($"StateUtility::EvaluateIdempotency: stale request for account - {existingData.NaturalKey}, {existingData.AccountId}, request time: {requestTime}");
                        throw new APIErrorsException(new Dictionary<string, string> { { "StaleRequest", $"Your request is stale, a request with newer or same timestamp of {((DateTimeOffset)existingTime).ToString("0:MM/dd/yy H:mm:ss zzz")} UTC is already stored, if this update is intentional please update your timeOfOccurrence and reprocess the request." } });
                    }

                    //TimeOfOccurrence short circuit passed, update Cosmos Idempotency to pass
                    var existingIdempotency = IdempotencyProvider.GetValue<string>(existingData.Event);
                    if (existingIdempotency != null)
                    {
                        IdempotencyProvider.SetValue(ref data, existingIdempotency);
                    }
                    //Since incoming is newer, retain the requestTime ...make sure it get's migrated with other state to retain!
                    existingData.TimeOfOccurrence = (DateTimeOffset)requestTime;
                }
                else
                    throw new APIErrorsException(new Dictionary<string, string> { { "InvalidRequest", "The object must contain a time of occurrence." } });

                return eventTimes;
            } 
            catch (APIErrorsException ex)
            {
                _logger.LogError(ex);
                throw;
            }
        }

        public async Task<WrappedEventPayload> GenerateAndHydrateEventWrapper(string tenantId, 
            string? loyaltyAccountId, 
            IDynamicEntity data, 
            (ModelDto eventModel, ModelDto wrapperModel) modelDetails, 
            bool reprocessEvent = false)
        {
            try
            {
                WrappedEventPayload? existingObj = null;
                string metaType = TypeProvider.GetValue<string>(data) ?? modelDetails.eventModel.Name;
                var naturalKey = GenerateNaturalKey(modelDetails, data);

                var tmp = await _dynamicExtRefAdapter.GetEntityByExtId(tenantId: tenantId, modelId: modelDetails.wrapperModel.ID, extId: naturalKey, extIdType: metaType, pk: loyaltyAccountId);
                if (tmp != null && tmp.Value.ValueKind != JsonValueKind.Null && tmp.Value.ValueKind != JsonValueKind.Undefined)
                {
                    existingObj = tmp.Value.Deserialize<WrappedEventPayload>(JsonUtility.GetDefaultOptions());
                    if (existingObj == null)
                    {
                        _logger.LogWarning($"StateUtility::EvaluateIdempotency: The object returned from the database was null or invalid and could not be deserialized into the WrappedEventPayload.");
                        throw new APIErrorsException(new Dictionary<string, string> { { "InvalidData", $"The object returned from the database was null or invalid and could not be deserialized into the WrappedEventPayload." } });
                    }
                }
                else
                    existingObj = null;

                var wrappedPayload = new WrappedEventPayload
                {
                    Status = WrappedEventPayloadStatus.ACTIVE,
                    Event = ToJsonElement(data),
                    AccountId = loyaltyAccountId,
                    NaturalKey = naturalKey,
                    MetaType = metaType
                };

                //Handle Idempotency if there was a previously saved event.
                if (existingObj != null)
                {
                    var eventTimes = EvaluateIdempotency(data, existingObj, modelDetails, reprocessEvent);
                    MigrateEngineState(existingObj, ref wrappedPayload);
                }
                else
                {
                    var timeOfEventValue = GetMetaDataValue(modelDetails.eventModel, TIME_OF_EVENT_META_KEY);
                    var requestTime = EventOccurrenceResolver.TryGet(data, timeOfEventValue);
                    wrappedPayload.TimeOfOccurrence = requestTime ?? DateTimeOffset.UtcNow;
                }
                return wrappedPayload;
            }
            catch (APIErrorsException ex)
            {
                _logger.LogError(ex);
                throw;
            }
        }

        public void MigrateEngineState(WrappedEventPayload existingObj, ref WrappedEventPayload newRequest)
        {
            newRequest.Id = existingObj.Id;
            //Map existing rule state over.
            newRequest.AppliedCampaigns = existingObj.AppliedCampaigns;
            newRequest.AppliedRuleSetIds = existingObj.AppliedRuleSetIds;
            newRequest.JourneyStates = existingObj.JourneyStates;
            newRequest.OutcomeStates = existingObj.OutcomeStates;
            newRequest.ProviderStates = existingObj.ProviderStates;

            newRequest.TimeOfOccurrence = existingObj.TimeOfOccurrence;
        }

        private JsonElement ToJsonElement(object obj)
        {
            string json = JsonSerializer.Serialize(obj, JsonUtility.GetDefaultOptions());
            using JsonDocument doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }

        private async static Task<List<PointAccountType>> GetPointAccountTypes(string tenantId)
        {
            return await PointAccountTypeCache.Instance.GetAllPointAccountTypes(tenantId) ?? new List<PointAccountType>();
        }
    }
}
