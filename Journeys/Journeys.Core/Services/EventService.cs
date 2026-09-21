

using Azure;
using Backend.Dto.Dynamic;
using Backend.Dto.Interfaces;
using Backend.Dto.Structures.Model;
using Backend.Dto.Utilities;
using Journeys.Core.Caching;
using Journeys.Core.Exceptions;
using Journeys.Core.Extensions;
using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Interfaces.Entities;
using Journeys.Core.Interfaces.Services;
using Journeys.Core.JsonConverters;
using Journeys.Core.Models;
using Journeys.Core.RulesEngine.Engine;
using Journeys.Core.RulesEngine.Outcomes;
using Journeys.Core.RulesEngine.Providers;
using Journeys.Core.RulesEngine.Utility;
using Journeys.Core.Utility;
using Journeys.Core.Utility.StreamUtilities;
using Journeys.DTO.Exceptions;
using Journeys.DTO.Interfaces;
using Journeys.DTO.Models;
using Journeys.DTO.Models.RulesEngine;
using Journeys.DTO.Requests;
using Journeys.DTO.Responses;
using MassTransit;
using MassTransit.Contracts.JobService;
using MassTransit.Serialization;
using MassTransit.Transports;
using Microsoft.Azure.Amqp.Framing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Dynamic;
using System.Globalization;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Xml.Linq;
using static MassTransit.AzureServiceBusTransport.Topology.PublishEndpointBrokerTopologyBuilder;
using static System.Runtime.InteropServices.JavaScript.JSType;
using ConcurrencyException = Journeys.Core.Exceptions.ConcurrencyException;


namespace Journeys.Core.Services
{
    public class EventService : IEventService
    {
        private static DateTimeFormatInfo invoiceDateFormat = new DateTimeFormatInfo()
        {
            FullDateTimePattern = "yyyyMMdd",
            LongTimePattern = "yyyyMMdd",
            MonthDayPattern = "yyyyMMdd",
            ShortTimePattern = "yyyyMMdd",
            YearMonthPattern = "yyyyMMdd",
            ShortDatePattern = "yyyyMMdd",
            LongDatePattern = "yyyyMMdd",
            DateSeparator = "",
            TimeSeparator = ""
        };

        const int FLUSH_CYCLE = 10;

        private const string ACCOUNT_EXT_ID_META_KEY = "AccountXIdSymbol";
        private const string NATURAL_KEY_SYMBOLS_META_KEY = "NaturalKeySymbols";
        private const string WRAPPER_MODEL_META_KEY = "Wrapper";
        private const string TIME_OF_EVENT_META_KEY = "TimeOfOccurrence";
        private const string IS_LOYALTY_ACCOUNT_EVENT = "IsLoyaltyAccount";
        private const string EVENT_SYMBOL_KEY = "event";

        public static string EVENT_TYPE_SYMBOL_KEY = "eventtype";

        private static PathValueProvider TypeProvider = new PathValueProvider("type");
        private static PathValueProvider EventTypeProvider = new PathValueProvider("eventtype");
        private static PathValueProvider AccountDetailsProvider = new PathValueProvider("AccountDetails");
        private static PathValueProvider NaturalKeyProvider = new PathValueProvider("naturalkey");
        private static PathValueProvider EventModelIdProvider = new PathValueProvider("modelId");
        private static PathValueProvider IdempotencyProvider = new PathValueProvider("version");
        private static PathValueProvider CalculateOnlyProvider = new PathValueProvider("calculateOnly");
        private static PathValueProvider PayloadProvider = new PathValueProvider("event");
        private static PathValueProvider IdProvider = new PathValueProvider("id");


        //private readonly IEventAdapterFactory _adapterFactory;
        private readonly ILogicalEntityAdapter _logicalEntityAdapter;
        private readonly ILoyaltyAccountService _loyaltyAccountService;
        private readonly ICampaignService _campaignService;
        private readonly IRulesService _rulesService;
        private readonly IDynamicDataAdapter _dynamicDataAdapter;
        private readonly ModelCache _modelCache;
        private readonly IDynamicExternalReferenceAdapter _dynamicExtRefAdapter;
        private readonly StateUtility _stateUtility;
        private readonly ILogger<EventService> _logger;
        private readonly ITaxonomyDataAdapter _taxonomyDataAdapter;
        private readonly ITenantDataAdapter _tenantDataAdapter;
        private readonly INotificationService? _notificationService;
        private readonly IConfiguration? _configuration;

        public EventService(//IEventAdapterFactory adapterFactory,
                ILoyaltyAccountService loyaltyAccountService, ICampaignService campaignService,
                ILogicalEntityAdapter logicalEntityAdapter, IRulesService rulesService,
                ModelCache modelCache, IDynamicDataAdapter dynamicDataAdapter,
                IDynamicExternalReferenceAdapter dynamicAdapter, StateUtility stateUtility,
                //IPublishEndpoint publishEndpoint, 
                ILogger<EventService> logger, ITaxonomyDataAdapter taxonomyDataAdapter,
                ITenantDataAdapter tenantDataAdapter,
                INotificationService? notificationService = null,
                IConfiguration? configuration = null)
        {
            //_adapterFactory = adapterFactory; ;
            _loyaltyAccountService = loyaltyAccountService;
            _campaignService = campaignService;
            _logicalEntityAdapter = logicalEntityAdapter;
            _rulesService = rulesService;
            _modelCache = modelCache;
            _dynamicDataAdapter = dynamicDataAdapter;
            _dynamicExtRefAdapter = dynamicAdapter;
            _stateUtility = stateUtility;
            //_publishEndpoint = publishEndpoint;
            _logger = logger;
            _taxonomyDataAdapter = taxonomyDataAdapter;
            _tenantDataAdapter = tenantDataAdapter;
            _notificationService = notificationService;
            _configuration = configuration;
        }

        //Could be ready from Azure Vault...or config, etc.
        //private static readonly Dictionary<string, Guid> ModelDictionary = new()
        //{
        //    { "Journeys.Models.Order", new Guid("a6edbbc5-bf43-4c57-b2f1-e015b9efaf03") }, //Order model id
        //    { "OptIn", Guid.NewGuid() }, //example
        //    // Add additional event types and corresponding model IDs as needed
        //};

        public async Task<EventPayloadResponseDto> GetAccountEntity(string tenantId, string modelName, string id, CancellationToken token, bool includeChildModels)
        {
            var model = await _modelCache.GetOrLoadByName(tenantId, modelName);
            var entities = await _dynamicDataAdapter.GetEntitiesByPKAsync<WrappedEventPayload>(tenantId, id, ModelUtility.GetContainerModelID(model), 1);
            var entity = entities?.Entities?.FirstOrDefault();
            if (entity == null)
            {
                throw new APIErrorsException(new Dictionary<string, string> { { "NOT_FOUND", $"Event with ID {id} not found in model {modelName} for tenant {tenantId}." } });
            }

            var response = new EventPayloadResponseDto
            {
                TenantId = tenantId,
                LoyaltyAccountId = entity.AccountId,
                TimeOfOccurrence = entity.TimeOfOccurrence,
                LastProcessed = entity.LastProcessed,
                Event = entity.Event,
                AppliedCampaigns = entity.AppliedCampaigns,
                AppliedRuleSetIds = entity.AppliedRuleSetIds,
                ProviderStates = entity?.ProviderStates ?? new Dictionary<string, ProviderStateBaseDto>(),
                OutcomeStates = entity?.OutcomeStates ?? new List<OutcomeStateBaseDto>(),
                JourneyStates = entity?.JourneyStates ?? new Dictionary<string, JourneyStateDto>()
            };
            return response;
        }

        public async Task<PagedResultSetResponse<EventPayloadResponseDto>> QueryAsync(string tenantId, string modelName, string query, Dictionary<string, object> parameters, string sortBy, SortOrder sortOrder, int pageSize, string? continuationToken, CancellationToken token, bool includeChildModels = false)
        {
            try
            {
                var model = await _modelCache.GetOrLoadByName(tenantId, modelName);
                if (model == null)
                {
                    throw new APIErrorsException(new Dictionary<string, string> { { "InvalidModel", $"{modelName} was unable to be loaded or found." } });
                }

                if (model.ModelMetaData == null || !model.ModelMetaData.ContainsKey(WRAPPER_MODEL_META_KEY) || !model.ModelMetaData.ContainsKey(NATURAL_KEY_SYMBOLS_META_KEY))
                {
                    throw new APIErrorsException(new Dictionary<string, string> { { "InvalidModel", $"{modelName} is not correctly configured to process within the Rules Engine, please create the necessary RuleState wrapper and set the {WRAPPER_MODEL_META_KEY} Meta-Data property with the Wrapper Id on the model. Also set the {NATURAL_KEY_SYMBOLS_META_KEY} property with the array of Symbols to be used for the natural key generation." } });
                }

                var wrapperModel = await _modelCache.GetOrLoadById(tenantId, model.ModelMetaData[WRAPPER_MODEL_META_KEY]);

                var result = await _dynamicDataAdapter.QueryEntitiesAsync<WrappedEventPayload>(tenantId, wrapperModel.ID, query, parameters, sortBy, sortOrder, pageSize, token, continuationToken, null, includeChildModels);
                var response = new PagedResultSetResponse<EventPayloadResponseDto>();

                response.Entities = result?.Entities?.Select(entity => new EventPayloadResponseDto
                {
                    TenantId = tenantId,
                    LoyaltyAccountId = entity.AccountId,
                    EventNaturalKey = entity.NaturalKey,
                    Event = entity.Event,
                    ProcessedEventModelId = model.ID,
                    AppliedCampaigns = entity.AppliedCampaigns,
                    AppliedRuleSetIds = entity.AppliedRuleSetIds,
                    ProviderStates = entity?.ProviderStates ?? new Dictionary<string, ProviderStateBaseDto>(),
                    OutcomeStates = entity?.OutcomeStates ?? new List<OutcomeStateBaseDto>(),
                    JourneyStates = entity?.JourneyStates ?? new Dictionary<string, JourneyStateDto>()
                })?.ToList() ?? new List<EventPayloadResponseDto>();
                response.ContinuationToken = result.ContinuationToken;
                response.Count = result.Count;
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing QueryAsync for tenant {TenantId} and model {ModelName}.", tenantId, modelName);
                throw;
            }
        }

        public async Task ProcessBulk(string tenantId, string modelName, Stream inputStream, Stream outputStream, CancellationToken token, int batchSize = 5)
        {
            using (var csvUtil = new CSVUtility(inputStream))
            {
                var reader = csvUtil.Read(0, token);
                var tasks = new Dictionary<string, Task<Result<EventPayloadResponseDto>>>();
                int flushCycle = 0;
                int lineNumber = 0;
                await foreach (var line in reader)
                {
                    try
                    {
                        if (lineNumber == 0)
                        {
                            lineNumber++;
                            continue;
                        }
                        lineNumber++;

                        flushCycle++;
                        if (flushCycle >= FLUSH_CYCLE)
                        {
                            flushCycle = 0;
                            await outputStream.FlushAsync(token);
                        }

                        if (line.IsSuccess)
                        {
                            if (line.Value != null)
                            {
                                try
                                {
                                    var task = ProcessCSVOrderObject(tenantId, line.Value, token);
                                    tasks.Add(line.Value[0], task);
                                }
                                catch (Exception ex)
                                {
                                    var entityJson = JsonUtility.Serialize(line.Value, _logger);
                                    var error = Result<EventPayloadResponseDto>.Error(ex.Message, entityJson);
                                    var output = JsonUtility.Serialize(error, _logger) + "\r\n";
                                    var outputBytes = Encoding.UTF8.GetBytes(output);
                                    await outputStream.WriteAsync(outputBytes, 0, outputBytes.Length, token);
                                }
                            }
                        }
                        else
                        {
                            var output = JsonUtility.Serialize(line, _logger) + "\r\n";
                            var outputBytes = Encoding.UTF8.GetBytes(output);
                            await outputStream.WriteAsync(outputBytes, 0, outputBytes.Length, token);
                        }

                        if (tasks.Count >= batchSize)
                        {
                            var result = await Task.WhenAny(tasks.Values);
                            var output = JsonUtility.Serialize(result, _logger) + "\r\n";
                            var outputBytes = Encoding.UTF8.GetBytes(output);
                            await outputStream.WriteAsync(outputBytes, 0, outputBytes.Length, token);

                            tasks.Remove(tasks.FirstOrDefault(x => x.Value == result).Key);
                        }
                    }
                    catch (Exception ex)
                    {
                        var output = JsonUtility.Serialize(Result<EventPayloadResponseDto>.Error(ex.Message, JsonUtility.Serialize(line, _logger)), _logger) + "\r\n";
                        var outputBytes = Encoding.UTF8.GetBytes(output);
                        await outputStream.WriteAsync(outputBytes, 0, outputBytes.Length, token);
                    }
                }

                await Task.WhenAll(tasks.Values);
                foreach (var result in tasks)
                {
                    var output = JsonUtility.Serialize(result, _logger) + "\r\n";
                    var outputBytes = Encoding.UTF8.GetBytes(output);
                    await outputStream.WriteAsync(outputBytes, 0, outputBytes.Length, token);
                }
                await outputStream.FlushAsync(token);
            }
        }

        public async Task<EventPayloadResponseDto> ProcessEventAsync(string tenantId, string modelName, JsonElement jsonData, CancellationToken? token = null, bool reprocessEvent = false, string? campaignId = null)
        {
            var data = DynamicHelper.Import(jsonData);
            string accountId = null;
            try
            {
                var modelDetails = await _stateUtility.LoadEventModels(tenantId, modelName);
                var loyaltyAccountEntity = await _stateUtility.LoadLoyaltyAccountAsync(tenantId, data, modelDetails);
                accountId = loyaltyAccountEntity?.Id;

                var wrappedPayload = await _stateUtility.GenerateAndHydrateEventWrapper(tenantId, loyaltyAccountEntity.Id, data, modelDetails, reprocessEvent);

                return await ProcessEventInternalAsync(tenantId, modelDetails, loyaltyAccountEntity, wrappedPayload, data, jsonData, token, reprocessEvent, campaignId);
            }
            catch (APIErrorsException ex)
            {
                ex.AccountId = accountId;
                throw ex;
            }
            catch (Exception ex)
            {
                throw;
            }
        }


        public async Task<LoyaltyAccountDto> ResettleAccountAsync(string tenantId, string accountId, CancellationToken? token = null)
        {
            try
            {
                if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(accountId)) return null;

                var loyaltyAccountEntity = await _stateUtility.GetLoyaltyAccountAsync(tenantId, accountId);
                var res = await ResettleAccountInternalAsync(tenantId, loyaltyAccountEntity);
                return loyaltyAccountEntity.ToDto();
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public async Task<LoyaltyAccountDto> ResettleAccountByXidAsync(string tenantId, string extRefId, CancellationToken? token = null)
        {
            try
            {
                if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(extRefId)) return null;

                var loyaltyAccountEntity = await _stateUtility.GetLoyaltyAccountByXidAsync(tenantId, extRefId);
                var res = await ResettleAccountInternalAsync(tenantId, loyaltyAccountEntity);
                return loyaltyAccountEntity.ToDto();
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public async Task<LoyaltyAccountDto> ResettleAccountAsync(string tenantId, LoyaltyAccountDto account, CancellationToken? token = null)
        {
            try
            {
                if (string.IsNullOrEmpty(tenantId) || account?.Id == null) return account;

                var res = await ResettleAccountInternalAsync(tenantId, account.FromDto());
                return res?.State?.LoyaltyAccount?.ToDto() ?? account;
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public async Task<ReconcileAccountResponse> ReconcileLoyaltyAccountEventsByFileAsync(string tenantId, string modelName, JsonElement jsonData, CancellationToken? token = null, bool reprocessEvent = false)
        {
            reprocessEvent = true;
            var modelDetails = await _stateUtility.LoadEventModels(tenantId, modelName);

            List<(IDynamicEntity, JsonElement)> nodes = new List<(IDynamicEntity, JsonElement)>();
            LoyaltyAccount loyaltyAccountEntity = null;
            if (jsonData.ValueKind == JsonValueKind.Array)
            {
                foreach (var line in jsonData.EnumerateArray())
                {
                    var dataLine = DynamicHelper.Import(line);
                    nodes.Add((dataLine, line));
                    if (loyaltyAccountEntity == null)
                        loyaltyAccountEntity = await _stateUtility.LoadLoyaltyAccountAsync(tenantId, dataLine, modelDetails);
                }
            }
            else
            {
                var data = DynamicHelper.Import(jsonData);
                nodes.Add((data, jsonData));
                loyaltyAccountEntity = await _stateUtility.LoadLoyaltyAccountAsync(tenantId, data, modelDetails);
            }

            if (loyaltyAccountEntity?.ExtAccountId == null || nodes.Count == 0) return null;

            ReconcileAccountResponse response = new ReconcileAccountResponse
            {
                LoyaltyAccountId = loyaltyAccountEntity.Id,
                ExternalId = loyaltyAccountEntity.ExtAccountId,
                RunDate = DateTime.UtcNow,
                ModelName = modelName,
                TotalEvents = 0,
                TotalEventsUnprocessed = 0,
                TotalEventsReprocessed = 0,
                TotalEventsToBeReprocessed = 0,
                ReconciliationDetails = new List<ReconciliationDetail>()
            };

            if (loyaltyAccountEntity == null)
                throw new ArgumentNullException(nameof(loyaltyAccountEntity), "ReconcileLoyaltyAccountEventsByXidAsync: The Account must be provided.");

            //get point account types
            var pointAccountTypes = await LoyaltyAccountService.GetPointAccountTypes(tenantId) ?? new List<PointAccountType>();
            if (pointAccountTypes?.Count == 0)
            {
                throw new ConfigurationException($"ReconcileLoyaltyAccountEventsAsync: No Point Account Types configured.");
            }

            //use spendable
            var patId = pointAccountTypes.FirstOrDefault(x => x.IsSpendable ?? false)?.Id;
            if (patId == null)
            {
                throw new ConfigurationException($"ReconcileLoyaltyAccountEventsAsync: No spendable account defined.");
            }

            //Fetch point entry ..do this by calling the function to ensure points are in a correct state
            loyaltyAccountEntity.PointLedgers ??= await _loyaltyAccountService.BringLoyaltyAccountPointsCurrentInternalAsync(tenantId, loyaltyAccountEntity);
            var entries = loyaltyAccountEntity.PointLedgers?.FirstOrDefault(x => x.PointAccountTypeId == patId)?.Entries;

            if (modelDetails.eventModel.ModelMetaData == null ||
                !modelDetails.eventModel.ModelMetaData.ContainsKey(WRAPPER_MODEL_META_KEY) ||
                !modelDetails.eventModel.ModelMetaData.ContainsKey(NATURAL_KEY_SYMBOLS_META_KEY))
            {
                throw new APIErrorsException(new Dictionary<string, string> { { "InvalidSchema", $"ReconcileLoyaltyAccountEventsAsync: {modelName} is not correctly configured to process within the Rules Engine, please create the necessary RuleState wrapper and set the {WRAPPER_MODEL_META_KEY} Meta-Data property with the Wrapper Id on the schema. Also set the {NATURAL_KEY_SYMBOLS_META_KEY} property with the array of Symbols to be used for the natural key generation." } });
            }

            var timeOfEventValue = StateUtility.GetMetaDataValue(modelDetails.eventModel, TIME_OF_EVENT_META_KEY);
            if (!string.IsNullOrEmpty(timeOfEventValue) &&
                !modelDetails.eventModel.Attributes.Any(x => x.Symbol == timeOfEventValue))
            {
                //ToDo: log this
            }
            var TimeOfOccurrenceProvider = new PathValueProvider(timeOfEventValue);

            foreach (var node in nodes)
            {
                //Provide report output
                var reconDetail = new ReconciliationDetail
                {
                    EventKey = loyaltyAccountEntity.ExtAccountId,
                    EventDate = TimeOfOccurrenceProvider.GetValue<DateTime>(node.Item1)
                };
                response.ReconciliationDetails.Add(reconDetail);
                response.TotalEvents += 1;

                string metaType = TypeProvider.GetValue<string>(node.Item1) ?? modelDetails.eventModel.Name;
                if (string.IsNullOrEmpty(metaType))
                {
                    //ToDo: log this

                    reconDetail.Errors = new Dictionary<string, string> { { "Malformed Event", "No Event Type." } };
                    continue;
                }
                reconDetail.EventType = metaType;

                //Find entry entry
                var entryKey = EventKeyUtility.ToEventKey(metaType, loyaltyAccountEntity.ExtAccountId);
                var entry = (entries.ContainsKey(entryKey)) ? entries[entryKey] : null;

                reconDetail.EventProcessedDate = entry?.FirstOrDefault()?.EarnDate;

                if ((!entry?.Any() ?? true))// || !reconDetail.OutcomesAwarded)
                    response.TotalEventsToBeReprocessed += 1;

                //Re-run if asked, and the event is unprocessed
                if (reprocessEvent && (!entry?.Any() ?? true))
                {
                    var wrappedPayload = await _stateUtility.GenerateAndHydrateEventWrapper(tenantId, loyaltyAccountEntity.Id, node.Item1, modelDetails, reprocessEvent);
                    var eventResult = await ProcessEventInternalAsync(tenantId, modelDetails, loyaltyAccountEntity, wrappedPayload, node.Item1, node.Item2, token, true);
                    if (eventResult.Errors?.Count > 0)
                    {
                        //Reprocessing had errors
                        reconDetail.Errors = eventResult.Errors;
                        reconDetail.SetUnprocessedStatus();
                    }
                    else
                    {
                        reconDetail.SetProcessedStatus();
                        response.TotalEventsReprocessed += 1;
                    }

                    reconDetail.OutcomesAwarded = eventResult?.OutcomeStates?
                                                        .Any(x => x.IsAwarded || (x.IssuingOutcomeId?.Contains("Awarded", StringComparison.InvariantCultureIgnoreCase) ?? false)) ?? false;

                    reconDetail.PointAward = (reconDetail.OutcomesAwarded) ?
                                                    eventResult.OutcomeStates
                                                        .Where(x => x.IsAwarded || (x.IssuingOutcomeId?.Contains("Awarded", StringComparison.InvariantCultureIgnoreCase) ?? false))
                                                        ?.Where(x => x.PointAccountTypeId?.Equals(patId, StringComparison.InvariantCultureIgnoreCase) ?? false)
                                                        ?.Sum(x => x.PointsDeposited) ?? 0 : 0;

                    var journey = eventResult.JourneyStates?.Values?.FirstOrDefault();
                    if (journey?.NodeMemberships?.Count > 0)
                        reconDetail.JourneyNodeAtEvent = journey.NodeMemberships.First();
                    else
                        reconDetail.JourneyNodeAtEvent = "Unknown";
                }

            }
            return response;

            //ReconcileAccountRequest request = new ReconcileAccountRequest
            //{
            //    ExternalId = loyaltyAccountEntity.ExtAccountId,
            //    Reprocess = reprocessEvent
            //};
            //return await ReconcileLoyaltyAccountEventsAsync(tenantId, modelName, request, token);
        }
        
        public async Task<ReconcileAccountResponse> ReconcileLoyaltyAccountEventsByXidAsync(string tenantId, string schemaName, JsonElement jsonData, CancellationToken? token = null, bool reprocessEvent = false)
        {
            if (jsonData.TryGetProperty("xid", out var xidElement))
            {
                var data = DynamicHelper.Import(jsonData);
                var xid = data.GetValue("xid");
                if (xid == null) return null;

                ReconcileAccountRequest request = new ReconcileAccountRequest
                {
                    ExternalId = xid.ToString(),
                    Reprocess = true //reprocessEvent
                };
                return await ReconcileLoyaltyAccountEventsAsync(tenantId, schemaName, request, token);
            }
            return null;
        }

        public async Task<ReconcileAccountResponse> ReconcileLoyaltyAccountEventsAsync(string tenantId, string modelName, ReconcileAccountRequest request, CancellationToken? token = null)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
                throw new ArgumentNullException(nameof(tenantId), "ReconcileLoyaltyAccountEventsAsync: The TenantId must be provided.");

            if (string.IsNullOrEmpty(request?.AccountId) && string.IsNullOrEmpty(request.ExternalId))
                throw new ArgumentNullException(nameof(tenantId), "ReconcileLoyaltyAccountEventsAsync: The External Id or Loyalty Account Id must be provided.");

            ReconcileAccountResponse response = new ReconcileAccountResponse
            {
                LoyaltyAccountId = request.AccountId,
                ExternalId = request.ExternalId,
                RunDate = DateTime.UtcNow,
                ModelName = modelName,
                TotalEvents = 0,
                TotalEventsUnprocessed = 0,
                TotalEventsReprocessed = 0,
                TotalEventsToBeReprocessed = 0,
                ReconciliationDetails = new List<ReconciliationDetail>()
            };

            try
            {

                //Get account
                var accountDto = (!string.IsNullOrEmpty(request.AccountId)) ?
                    await this._loyaltyAccountService.GetLoyaltyAccountAsync(tenantId, request.AccountId)
                    : await _loyaltyAccountService.GetLoyaltyAccountByExtIdAsync(tenantId, request.ExternalId);

                if (accountDto == null)
                    throw new ArgumentNullException(nameof(accountDto), "ReconcileLoyaltyAccountEventsAsync: The Account must be provided.");

                var account = accountDto.FromDto();

                //get point account types
                var pointAccountTypes = await LoyaltyAccountService.GetPointAccountTypes(tenantId) ?? new List<PointAccountType>();
                if (pointAccountTypes?.Count == 0)
                {
                    throw new ConfigurationException($"ReconcileLoyaltyAccountEventsAsync: No Point Account Types configured.");
                }
                response.LoyaltyAccountId ??= account.Id;
                response.ExternalId ??= account.KnownExternalIds?.FirstOrDefault();

                //use spendable
                var patId = pointAccountTypes.FirstOrDefault(x => x.IsSpendable ?? false)?.Id;
                if (patId == null)
                {
                    throw new ConfigurationException($"ReconcileLoyaltyAccountEventsAsync: No spendable account defined.");
                }

                //Fetch point entry ..do this by calling the function to ensure points are in a correct state
                account.PointLedgers ??= await _loyaltyAccountService.BringLoyaltyAccountPointsCurrentInternalAsync(tenantId, account);
                var entries = account.PointLedgers?.FirstOrDefault(x => x.PointAccountTypeId == patId)?.Entries;

                //setup
                var modelDetails = await _stateUtility.LoadEventModels(tenantId, modelName);
                if (modelDetails.eventModel == null || modelDetails.wrapperModel == null)
                {
                    throw new ConfigurationException($"ReconcileLoyaltyAccountEventsAsync: No event model found.");
                }

                if (modelDetails.eventModel.ModelMetaData == null ||
                    !modelDetails.eventModel.ModelMetaData.ContainsKey(WRAPPER_MODEL_META_KEY) ||
                    !modelDetails.eventModel.ModelMetaData.ContainsKey(NATURAL_KEY_SYMBOLS_META_KEY))
                {
                    throw new APIErrorsException(new Dictionary<string, string> { { "InvalidModel", $"ReconcileLoyaltyAccountEventsAsync: {modelName} is not correctly configured to process within the Rules Engine, please create the necessary RuleState wrapper and set the {WRAPPER_MODEL_META_KEY} Meta-Data property with the Wrapper Id on the model. Also set the {NATURAL_KEY_SYMBOLS_META_KEY} property with the array of Symbols to be used for the natural key generation." } });
                }

                var query = "c.accountid = @LoyaltyAccountId";
                var parms = new Dictionary<string, object> { { "@LoyaltyAccountId", account.Id } };

                var timeOfEventValue = StateUtility.GetMetaDataValue(modelDetails.eventModel, TIME_OF_EVENT_META_KEY);
                if (!string.IsNullOrEmpty(timeOfEventValue) &&
                    !modelDetails.eventModel.Attributes.Any(x => x.Symbol == timeOfEventValue))
                {
                    //ToDo: log this
                }
                var TimeOfOccurrenceProvider = new PathValueProvider(timeOfEventValue);
                var pageSize = 100;
                string continuationToken = null;
                WrappedEventPayload lastEvent = null;
                bool bfirst = true;

                //Page through events (as provided by the model name)
                while (bfirst || !string.IsNullOrEmpty(continuationToken))
                {
                    bfirst = false;
                    var result = await _dynamicDataAdapter.QueryEntitiesAsync<WrappedEventPayload>(tenantId, modelDetails.wrapperModel.ID, query, parms, null, SortOrder.ASC, pageSize, token ?? default(CancellationToken), continuationToken, null, false);
                    if (result?.Entities == null) break;

                    foreach (var wrappedEvent in result.Entities)
                    {
                        //Provide report output
                        var reconDetail = new ReconciliationDetail
                        {
                            EventKey = wrappedEvent.NaturalKey,
                            EventDate = TimeOfOccurrenceProvider.GetValue<DateTime>(wrappedEvent.Event),
                            OutcomesAwarded = wrappedEvent.OutcomeStates.Any()
                        };
                        response.ReconciliationDetails.Add(reconDetail);
                        response.TotalEvents += 1;

                        string metaType = TypeProvider.GetValue<string>(wrappedEvent.Event) ?? modelDetails.eventModel.Name;
                        if (string.IsNullOrEmpty(metaType))
                        {
                            //ToDo: log this

                            reconDetail.Errors = new Dictionary<string, string>
                            {
                                { "Malformed Event", "No Event Type." }
                            };
                            continue;
                        }
                        reconDetail.EventType = metaType;
                        wrappedEvent.MetaType = metaType;

                        //Find entry entry
                        var entryKey = EventKeyUtility.ToEventKey(metaType, wrappedEvent.NaturalKey);
                        var entry = (entries.ContainsKey(entryKey)) ? entries[entryKey] : null;

                        reconDetail.EventProcessedDate = entry?.FirstOrDefault()?.EarnDate;

                        reconDetail.OutcomesAwarded = wrappedEvent?.OutcomeStates?
                                                            .Any(x => x.IsAwarded || (x.IssuingOutcomeId?.Contains("Awarded", StringComparison.InvariantCultureIgnoreCase) ?? false)) ?? false;

                        reconDetail.PointAward = (reconDetail.OutcomesAwarded) ?
                                                        wrappedEvent.OutcomeStates
                                                            .Where(x => x.IsAwarded || (x.IssuingOutcomeId?.Contains("Awarded", StringComparison.InvariantCultureIgnoreCase) ?? false))
                                                            ?.Where(x => x.PointAccountTypeId?.Equals(patId, StringComparison.InvariantCultureIgnoreCase) ?? false)
                                                            ?.Sum(x => x.PointsDeposited) ?? 0 : 0;

                        if ((!entry?.Any() ?? true) || !reconDetail.OutcomesAwarded) response.TotalEventsToBeReprocessed += 1;

                        var journey = wrappedEvent.JourneyStates?.Values?.FirstOrDefault();
                        if (journey?.NodeMemberships?.Count > 0)
                            reconDetail.JourneyNodeAtEvent = journey.NodeMemberships.First();
                        else
                            reconDetail.JourneyNodeAtEvent = "Unknown";

                        //Re-run if asked, and the event is unprocessed
                        if ((request.Reprocess ?? false) && ((!entry?.Any() ?? true) || !reconDetail.OutcomesAwarded))
                        {
                            if (wrappedEvent?.Event == null)
                            {
                                reconDetail.Errors = new Dictionary<string, string>
                                {
                                    { "Malformed Event", "No Event Data." }
                                };
                                continue;
                            }

                            var data = DynamicHelper.Import(wrappedEvent.Event);
                            var eventResult = await ProcessEventInternalAsync(tenantId, modelDetails, account, wrappedEvent, data, wrappedEvent.Event, token, true);
                            if (eventResult.Errors?.Count > 0)
                            {
                                //Reprocessing had errors
                                reconDetail.Errors = eventResult.Errors;
                                reconDetail.SetUnprocessedStatus();
                            }
                            else
                            {
                                reconDetail.SetProcessedStatus();
                                response.TotalEventsReprocessed += 1;
                            }

                            reconDetail.OutcomesAwarded = eventResult?.OutcomeStates?
                                                                .Any(x => x.IsAwarded || (x.IssuingOutcomeId?.Contains("Awarded", StringComparison.InvariantCultureIgnoreCase) ?? false)) ?? false;

                            reconDetail.PointAward = (reconDetail.OutcomesAwarded) ?
                                                            eventResult.OutcomeStates
                                                                .Where(x => x.IsAwarded || (x.IssuingOutcomeId?.Contains("Awarded", StringComparison.InvariantCultureIgnoreCase) ?? false))
                                                                ?.Where(x => x.PointAccountTypeId?.Equals(patId, StringComparison.InvariantCultureIgnoreCase) ?? false)
                                                                ?.Sum(x => x.PointsDeposited) ?? 0 : 0;

                            journey = eventResult.JourneyStates?.Values?.FirstOrDefault();
                            if (journey?.NodeMemberships?.Count > 0)
                                reconDetail.JourneyNodeAtEvent = journey.NodeMemberships.First();
                            else
                                reconDetail.JourneyNodeAtEvent = "Unknown";
                        }
                    }

                    continuationToken = result?.ContinuationToken;
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                throw;
            }
            return response;
        }

        public async Task<EventPayloadResponseDto> SaveEventPayload(string tenantId, string modelName, EventPayloadResponseDto dto)
        {
            try
            {
                var modelDetails = await _stateUtility.LoadEventModels(tenantId, modelName);

                WrappedEventPayload payload = new WrappedEventPayload
                {
                    AccountId = dto.LoyaltyAccountId,
                    AppliedCampaigns = dto.AppliedCampaigns,
                    AppliedRuleSetIds = dto.AppliedRuleSetIds,
                    //Id = dto.,
                    JourneyStates = dto.JourneyStates,
                    NaturalKey = dto.EventNaturalKey,
                    LastProcessed = dto.LastProcessed,
                    MetaType = modelName,
                    Event = dto.Event,
                    OutcomeStates = dto.OutcomeStates,
                    ProviderStates = dto.ProviderStates,
                    //Status = dto.
                    TimeOfOccurrence = dto.TimeOfOccurrence
                };

                var postSave = await _dynamicExtRefAdapter.UpsertEntityAsync(tenantId, modelDetails.wrapperModel.ID, payload, dto.EventNaturalKey, modelName);

                return dto;
            }
            catch (Exception ex)
            {
                throw;
            }
            return null;
        }

        protected async Task<EventPayloadResponseDto> ProcessEventInternalAsync(string tenantId,
            (ModelDto eventModel, ModelDto wrapperModel) modelDetails,
            LoyaltyAccount loyaltyAccountEntity,
            WrappedEventPayload wrappedPayload,
            IDynamicEntity data,
            JsonElement jsonData,
            CancellationToken? token = null,
            bool reprocessEvent = false,
            string? draftVerificationCampaignId = null)
        {
            var errors = new Dictionary<string, string>();
            try
            {
                var isLoyaltyAccountEvent = _stateUtility.IsLoyaltyAccountEvent(modelDetails);

                #region Set Event Data Values...
                //Inject the event type into the payload for processing.
                if (string.IsNullOrEmpty(wrappedPayload.MetaType))
                {
                    throw new APIErrorsException(new Dictionary<string, string> { { "Invalid Payload", $"{wrappedPayload.MetaType} is not correctly configured in the eventModel meta-data." } });
                }
                EventTypeProvider.SetValue(ref data, wrappedPayload.MetaType);

                //We found all necessary Models.
                //Inject the ModelId into the payload for processing.
                //Because these use ref, we cannot use them in the header for an async method.
                EventModelIdProvider.SetValue(ref data, modelDetails.eventModel.ID);

                if (!_stateUtility.IsLoyaltyAccountEvent(modelDetails))
                {
                    //Inject the NaturalKey into the payload
                    NaturalKeyProvider.SetValue(ref data, wrappedPayload.NaturalKey);
                }

                #endregion

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                //Use the Model to enumerate the Elements and apply constraint validation on the JSON
                errors.Update(
                    data.ValidateWithModel(modelDetails.eventModel)
                );

                if (errors.Count > 0)
                {
                    throw new APIErrorsException(errors);
                }

                #region Handle Loyalty Account Events...
                if (_stateUtility.IsLoyaltyAccountEvent(modelDetails) ) //&& string.IsNullOrWhiteSpace(wrappedPayload.Event.GetRawText()))
                {
                    //If this is a LoyaltyAccount event, we need to set the AccountId property on the payload.
                    //This handles first time account creation prior to engine processing.
                    var detailsEle = AccountDetailsProvider.GetValue<DynamicEntity>(data);
                    if (detailsEle != null)
                    {
                        wrappedPayload.Event = this.ToJsonElement(detailsEle);
                    }
                    else
                    {
                        //if there isn't an AccountDetails property, then assume this account object is unwrapped
                        wrappedPayload.Event = jsonData;
                    }
                }
                else
                {
                    //Save the current wrappedEvent.
                    wrappedPayload.Event = ToJsonElement(data);
                }
                #endregion

                //TODO: presave can come back with an error:
                //{"code":"unexpected_error","type":"unhandled_exception","message":"Invalid cast from 'DateTime' to 'Decimal'.","rawMessage":null,"stackTrace":null}"
                var preSave = await _dynamicExtRefAdapter.UpsertEntityAsync(tenantId: tenantId, entity: wrappedPayload, modelId: modelDetails.wrapperModel.ID, xId: wrappedPayload.NaturalKey, type: wrappedPayload.MetaType);
                try
                {
                    if (preSave is JsonElement elem && elem.ValueKind == JsonValueKind.Object && elem.TryGetProperty("code", out var codeProp))
                    {
                        var errorCode = codeProp.GetString();
                        var errorMessage = elem.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : null;
                        throw new Exception($"EventService::ProcessEventAsync - Error saving pre-loyalty: {errorMessage}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during pre-save of event payload.");
                    throw;
                }

                wrappedPayload.Id = IdProvider.GetValue<string>(preSave) ?? throw new Exception("Upsert failed to yield a valid Id");

                //Ensure the Id property is correctly set on the event payload.
                IdProvider.SetValue(ref data, wrappedPayload.Id);

                //First, verify this account is in good standing
                bool bOk = await _loyaltyAccountService.EnsureAccountInValidStateAsync(tenantId, loyaltyAccountEntity);
                if (!bOk && !isLoyaltyAccountEvent) throw new APIErrorsException(new Dictionary<string, string> { { "error", "Loyalty Account is in an invalid state." } });

                List<Campaign> campaignsToRun = null;
                LoyaltyAccount loyaltyAccount = null;
                if (bOk)
                {
                    #region Hydrate Campaigns...
                    //Populate the LoyaltyAccount Account Object.
                    loyaltyAccount = await PopulateLoyaltyAccountAsync(tenantId, loyaltyAccountEntity.Id, modelDetails.eventModel.ID, loyaltyAccountEntity);
                    if (loyaltyAccount == default(LoyaltyAccount))
                    {
                        errors.Add("loyaltyAccountId", "LoyaltyAccount or Account not found");
                        throw new APIErrorsException(errors);
                    }

                    if (!string.IsNullOrWhiteSpace(draftVerificationCampaignId))
                    {
                        campaignsToRun = await DraftVerificationCampaignSelector.ResolveExclusiveDraftCampaignsAsync(
                            tenantId,
                            draftVerificationCampaignId,
                            loyaltyAccount.ExtAccountId ?? loyaltyAccountEntity.ExtAccountId ?? string.Empty,
                            modelDetails.eventModel.ID,
                            _campaignService,
                            _tenantDataAdapter,
                            token ?? CancellationToken.None);

                        _logger.LogInformation(
                            "ProcessEvent draft verification: TenantId={TenantId}, CampaignId={CampaignId}, AccountExtId={AccountExtId}, EventPayloadModelId={EventPayloadModelId}",
                            tenantId,
                            draftVerificationCampaignId,
                            loyaltyAccount.ExtAccountId,
                            modelDetails.eventModel.ID);
                    }
                    else
                    {
                        //Load Campaigns to Run
                        campaignsToRun = new List<Campaign>(loyaltyAccount.Campaigns);

                        //Fetch event related campaigns
                        var eventCampaigns = await GetEventCampaignsAsync(tenantId, modelDetails.eventModel.ID, 100) ?? new PagedResultSetResponse<Campaign>();

                        //TODO: Fetch them, this is bad beyond pilot.
                        if (!string.IsNullOrEmpty(eventCampaigns?.ContinuationToken))
                            throw new NotImplementedException($"Multiple page campaign fetch has not yet been implemented, Temporary campaign count exceeded for loyaltyAccount: {loyaltyAccount.Id}");

                        //If there are campaigns from the event, add them to the list to run.
                        if (eventCampaigns != null && eventCampaigns.Entities != null && eventCampaigns.Entities.Any())
                            campaignsToRun.AddRange(eventCampaigns.Entities);

                        var tagCampaignCount = loyaltyAccount.Campaigns?.Count ?? 0;
                        var liveEventMatchCount = eventCampaigns?.Entities?.Count ?? 0;
                        _logger.LogInformation(
                            "ProcessEvent campaigns: TenantId={TenantId}, EventPayloadModelId={EventPayloadModelId}, FromAccountTags={TagCampaignCount}, FromLiveEventsList={LiveEventMatchCount}, TotalMerged={TotalMerged}",
                            tenantId,
                            modelDetails.eventModel.ID,
                            tagCampaignCount,
                            liveEventMatchCount,
                            campaignsToRun.Count);
                    }
                    #endregion
                }

                //Determine calculate only
                var calcOnly = CalculateOnlyProvider.GetValue<bool?>(data) ?? false;

                if (campaignsToRun?.Count > 0)
                {
                    if (jsonData.ValueKind == JsonValueKind.Null)
                        throw new NullReferenceException("Provided wrappedEvent was null or unable to be parsed.");
                    jsonData = ToJsonElement(data);
                    var ruleResponse = await ProcessCampaignsAsync(tenantId, modelDetails.eventModel.ID, jsonData, loyaltyAccount, campaignsToRun, calcOnly, wrappedPayload.NaturalKey, wrappedPayload.MetaType);
                    if (ruleResponse == null)
                    {
                        errors.Add("ProcessRulesAsync", "Rules Service yielded null when processing the request.");
                        throw new APIErrorsException(errors);
                    }

                    wrappedPayload.Status = WrappedEventPayloadStatus.STABLE;
                    wrappedPayload.AppliedCampaigns = ruleResponse?.State?.CampaignsEvaluated;
                    wrappedPayload.AppliedRuleSetIds = ruleResponse?.State?.AppliedRuleSets;
                    wrappedPayload.ProviderStates = ruleResponse?.State?.LoadedState?.ToDictionary(x => x.Key, x => new ProviderStateBaseDto(x.Value.Id, x.Value.Kind)) ?? new Dictionary<string, ProviderStateBaseDto>();
                    wrappedPayload.JourneyStates = ruleResponse?.State?.JourneyState?.Journeys?.ToDictionary(x => x.Key, x => new JourneyStateDto(x.Value));


                    var lstOutcomeDtos = TransformOutcomes(ruleResponse?.State?.EarnedOutcomes?.Values?.SelectMany(x => x)?.ToList());
                    wrappedPayload.OutcomeStates = lstOutcomeDtos ?? new List<OutcomeStateBaseDto>();

                    //Save the final wrappedEvent.
                    var postSave = await _dynamicExtRefAdapter.UpsertEntityAsync(tenantId: tenantId, entity: wrappedPayload, modelId: modelDetails.wrapperModel.ID, xId: wrappedPayload.NaturalKey);

                    var response = new EventPayloadResponseDto
                    {
                        Event = wrappedPayload.Event,
                        AppliedCampaigns = ruleResponse?.State?.CampaignsEvaluated,
                        AppliedRuleSetIds = ruleResponse?.State?.AppliedRuleSets,
                        ProviderStates = ruleResponse?.State?.LoadedState?.ToDictionary(x => x.Key, x => new ProviderStateBaseDto(x.Value.Id, x.Value.Kind)) ?? new Dictionary<string, ProviderStateBaseDto>(),
                        OutcomeStates = lstOutcomeDtos ?? new List<OutcomeStateBaseDto>(),
                        JourneyStates = ruleResponse?.State?.JourneyState?.Journeys?.ToDictionary(x => x.Key, x => new JourneyStateDto(x.Value)),
                        TimeOfOccurrence = wrappedPayload.TimeOfOccurrence,
                        LastProcessed = wrappedPayload.LastProcessed,
                        Errors = errors,
                        TenantId = tenantId,
                        LoyaltyAccountId = loyaltyAccount.Id,
                        EventNaturalKey = wrappedPayload.NaturalKey,
                        ProcessedEventModelId = modelDetails.eventModel.ID
                    };

                    //Publish notification job
                    //await _publishEndpoint.Publish<SubmitJob<ProcessedEventDto>>(new
                    //{
                    //    JobId = NewId.NextGuid(),
                    //    Job = new ProcessedEventDto
                    //    {
                    //        TenantId = tenantId,
                    //        ProcessedEvent = response
                    //    }
                    //});

                    return response;
                }

                var noCampaignResponse = new EventPayloadResponseDto
                {
                    TenantId = tenantId,
                    Event = wrappedPayload.Event,
                    Errors = errors,
                    LoyaltyAccountId = loyaltyAccountEntity?.Id ?? throw new Exception("Fatal Error: Engine processing completed with null Loyalty Account or Loyalty Account Id"),
                    EventNaturalKey = wrappedPayload.NaturalKey,
                    ProcessedEventModelId = modelDetails.eventModel.ID
                };

                //Publish notification job
                //await _publishEndpoint.Publish<SubmitJob<ProcessedEventDto>>(new
                //{
                //    JobId = NewId.NextGuid(),
                //    Job = new ProcessedEventDto
                //    {
                //        TenantId = tenantId,
                //        ProcessedEvent = noCampaignResponse
                //    }
                //});

                return noCampaignResponse;
            }
            catch (Exception ex)
            {
                throw;
            }
        }


        private List<OutcomeStateBaseDto> TransformOutcomes(List<OutcomeResult>? outcomes)
        {
            if (outcomes == null) return new List<OutcomeStateBaseDto>();

            var lstOutcomeDtos = new List<OutcomeStateBaseDto>();

            foreach (var outcome in outcomes)
            {
                if (outcome.IssuingOutcome is PointOutcomeBase pointOutcome)
                {
                    // Handle point outcomes - flatten PointResults into separate DTOs per PointAccountType
                    if (pointOutcome.PointResults != null)
                    {
                        foreach (var pointResult in pointOutcome.PointResults)
                        {
                            if (pointResult.LedgerEntries != null && pointResult.LedgerEntries.Any())
                            {
                                // Aggregate points for this PointAccountType
                                lstOutcomeDtos.Add(new OutcomeStateBaseDto
                                {
                                    IssuingOutcomeId = $"{outcome.IssuingOutcomeId}|{pointResult.PointAccountTypeId}",
                                    IssuingOutcomeKind = outcome.IssuingOutcomeKind,
                                    IssuingEventId = outcome.IssuingEventId,
                                    IssuingEventType = outcome.IssuingEventType,
                                    PointAccountTypeId = pointResult.PointAccountTypeId,
                                    PointsDeposited = pointResult.LedgerEntries.Sum(x => x.PointsDeposited),
                                    PointsWithdrawn = pointResult.LedgerEntries.Sum(x => x.PointsWithdrawn),
                                    CampaignId = outcome.CampaignId,
                                    RuleSetId = outcome.RuleSetId,
                                    IsAwarded = outcome.IsAwarded
                                });
                            }
                        }
                    }
                }
                else
                {
                    // Handle non-point outcomes
                    lstOutcomeDtos.Add(new OutcomeStateBaseDto
                    {
                        IssuingOutcomeId = outcome.IssuingOutcomeId,
                        IssuingOutcomeKind = outcome.IssuingOutcomeKind,
                        IssuingEventId = outcome.IssuingEventId,
                        IssuingEventType = outcome.IssuingEventType,
                        PointAccountTypeId = outcome.PointAccountTypeId,
                        PointsDeposited = null,
                        PointsWithdrawn = null,
                        CampaignId = outcome.CampaignId,
                        RuleSetId = outcome.RuleSetId,
                        IsAwarded = outcome.IsAwarded
                    });
                }
            }

            return lstOutcomeDtos;
        }

        private JsonElement ToJsonElement(string json)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            using (var doc = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true }))
            {
                return doc.RootElement.Clone();
            }
        }

        private async Task<Result<EventPayloadResponseDto>> ProcessCSVOrderObject(string tenantId, string[]? fields, CancellationToken token)
        {
            try
            {
                if (fields == null || fields.Length != 14)
                {
                    throw new ArgumentException($"Invalid number of fields in the CSV order object, expected 14, found {fields?.Length.ToString() ?? "null"}.");
                }

                if (fields.Any(x => x != null && x.Contains("\"")))
                {

                }

                if (!DateTime.TryParse(fields[13], out var loadDate))
                {
                    throw new APIErrorsException(new Dictionary<string, string> { { "InvalidLoadDate", $"Load date is not in the correct format: {fields[13]}" } });
                }

                if (!DateTime.TryParseExact(
                    fields[7],
                    "yyyyMMdd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var invoiceDate
                ))
                {
                    throw new APIErrorsException(new Dictionary<string, string> { { "InvalidInvoiceDate", $"Invoice date is not in the correct format: {fields[7]}" } });
                }

                var lineJSON = @"{
                    ""ItemNumber"" : """ + fields[8] + @""",
                    ""value"" : " + fields[10] + @",
                    ""quantity"" : " + fields[9] + @",
                    ""desc"" : """ + fields[11] + @""",
                    ""desc2"" : """ + fields[12] + @""",
                    ""loadDate"" : """ + loadDate + @"""
                }";

                var orderJSON = @"
                {
                    ""distributorCode"" : """ + fields[1] + @""",
                    ""distributor"" : """ + fields[2] + @""",
                    ""sfoqId"" : """ + fields[3] + @""",
                    ""ddr"" : """ + fields[4] + @""",
                    ""dealerName"" : """ + fields[5] + @""",
                    ""invoiceNumber"" : """ + fields[0] + @""",
                    ""invoiceDate"" : """ + invoiceDate + @""",
                    ""Items"" : [" + lineJSON + @"],
                    ""calculateOnly"" : false
                }";

                
                var result = await ProcessEventAsync(tenantId, "order", ToJsonElement(orderJSON), token);
                return Result<EventPayloadResponseDto>.Success(result);
            }
            catch (Exception ex)
            {
                return Result<EventPayloadResponseDto>.Error(ex.Message, JsonUtility.Serialize(fields, _logger));
            }
        }

        private async Task<PagedResultSetResponse<Campaign>> GetEventCampaignsAsync(string tenantId, string eventType, int pageSize, string? continuationToken = null)
        {
            PagedResultSetResponse<Campaign> campaigns = new PagedResultSetResponse<Campaign>();
            try
            {
                var res = await _campaignService.GetCampaignsByStatusAsync(tenantId, CampaignStatusStrings.Live, pageSize, continuationToken);
                if (res == null || res.Count == 0) return campaigns;

                campaigns.Count = res.Count;
                campaigns.ContinuationToken = res.ContinuationToken;
                var matched = res.Entities?
                    .Where(x => CampaignEventMatching.MatchesEventPayloadModel(x.Events, eventType))
                    .ToList();
                campaigns.Entities = matched?
                    .Select(x => x.FromDto())?
                    .ToList();
                if ((matched?.Count ?? 0) == 0 && res.Count > 0)
                {
                    _logger.LogDebug(
                        "No Live campaigns reference EventPayloadModelId {EventPayloadModelId} in Events for tenant {TenantId} (Live count={LiveCount}).",
                        eventType,
                        tenantId,
                        res.Count);
                }
            }
            catch (Exception ex)
            {
                //Log this
                throw;
            }
            return campaigns ?? new PagedResultSetResponse<Campaign>();
        }

        private async Task<PagedResultSetResponse<Campaign>> GetLiveCampaignsPageAsync(string tenantId, int pageSize, string? continuationToken = null)
        {
            PagedResultSetResponse<Campaign> campaigns = new PagedResultSetResponse<Campaign>();
            try
            {
                var res = await _campaignService.GetCampaignsByStatusAsync(tenantId, CampaignStatusStrings.Live, pageSize, continuationToken);
                if (res == null || res.Count == 0) return campaigns;

                campaigns.Count = res.Count;
                campaigns.ContinuationToken = res.ContinuationToken;
                campaigns.Entities = res.Entities?
                                        .Select(x => x.FromDto())?
                                        .ToList();
            }
            catch (Exception ex)
            {
                //Log this
                throw;
            }
            return campaigns ?? new PagedResultSetResponse<Campaign>();
        }

        private async Task<LoyaltyAccount?> PopulateLoyaltyAccountAsync(string tenantId, string loyaltyAccountId, string modelId, LoyaltyAccount? loyaltyAccount = null)
        {
            if (loyaltyAccount == null)
            {
                var loyaltyAccountDto = await _loyaltyAccountService.GetLoyaltyAccountAsync(tenantId, loyaltyAccountId);
                if (loyaltyAccountDto == null)
                {
                    //try look up by ext id
                    loyaltyAccountDto = await _loyaltyAccountService.GetLoyaltyAccountByExtIdAsync(tenantId, loyaltyAccountId);
                }
                loyaltyAccount = loyaltyAccountDto?.FromDto();
            }
            if (loyaltyAccount != null)
            {
                //fetch tags
                var tags = await _loyaltyAccountService.GetLoyaltyAccountTagsAsync(tenantId, loyaltyAccount.Id, "Campaign", 100) ?? new PagedResultSet<TagDto>();
                if (!string.IsNullOrEmpty(tags?.ContinuationToken)) throw new Exception($"Maximum number of Tags has been exceeded for loyaltyAccount: {loyaltyAccount.Id}");

                var moretags = (!string.IsNullOrEmpty(loyaltyAccount.ExtAccountId) ?
                                await _loyaltyAccountService.GetLoyaltyAccountTagsAsync(tenantId, loyaltyAccount.ExtAccountId, "Campaign", 100) : null) ?? new PagedResultSet<TagDto>();
                if (!string.IsNullOrEmpty(moretags?.ContinuationToken)) throw new Exception($"Maximum number of Tags has been exceeded for loyaltyAccount: {loyaltyAccount.ExtAccountId}");

                tags.AddRange(moretags);
                if (tags.Entities?.Count > 0)
                {
                    loyaltyAccount.Tags = tags.Entities.Select(t => t.FromDto()).ToList();
                    var campaignIds = tags.Entities.Select(t => t.Value).ToList();
                    if (campaignIds?.Count() > 0)
                    {
                        //Here we're passing the modelId of the event Entity in as the Campaign Type
                        //So this will return all campaigns whose Type property matches the provided event entity model id
                        var campaignSet = await _campaignService.GetManyCampaignsAsync(tenantId, campaignIds);
                        if (campaignSet?.Count > 0)
                        {
                            loyaltyAccount.Campaigns = campaignSet.Select(c => c.FromDto()).ToList();
                        }
                    }
                }
                return loyaltyAccount;
            }
            return default;
        }

        internal async Task<RulesServiceResponse> ProcessCampaignsAsync(string tenantId, string modelId, JsonElement jsonData, LoyaltyAccount loyaltyAccount, List<Campaign> campaignsToRun, bool calcOnly, string eventId = null, string eventType = null)
        {
            RulesServiceResponse ruleResponse = null;
            var numretries = 3;
            while (numretries > 0)
            {
                try
                {
                    //Try to lock the account
                    loyaltyAccount = await _loyaltyAccountService.TryLockAccount(tenantId, loyaltyAccount, Guid.NewGuid().ToString(), 500);
                    if (loyaltyAccount == null) throw new Exception($"Concurrency failure trying to lock account: {loyaltyAccount.Id}");

                    loyaltyAccount.PointLedgers = await _loyaltyAccountService.BringLoyaltyAccountPointsCurrentInternalAsync(tenantId, loyaltyAccount);

                    //Run rules
                    var ruleRequest = new RulesServiceRequest(modelId, jsonData, loyaltyAccount, campaignsToRun, null, calcOnly, eventId, eventType);
                    AttachNotificationPort(ruleRequest);
                    ruleResponse = await _rulesService.ProcessRulesAsync(ruleRequest, default);

                    break;
                }
                catch (LockAcquisitionException laex)
                {
                    _logger.LogError(laex);
                    --numretries;
                }
                catch (ConcurrencyException cex)
                {
                    _logger.LogError(cex);
                    --numretries;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex);
                    throw;
                }
            }
            if (numretries <= 0) throw new Exception($"Concurrency failure while processing account: {loyaltyAccount.Id}");

            return ruleResponse;
        }

        private async Task<RulesServiceResponse> ResettleAccountInternalAsync(string tenantId, LoyaltyAccount loyaltyAccount)
        {
            RulesServiceResponse ruleResponse = null;

            //Fetch event related campaigns
            var campaignsToRun = await GetLiveCampaignsPageAsync(tenantId, 100) ?? new PagedResultSetResponse<Campaign>();

            //TODO: Fetch them, this is bad beyond pilot.
            if (!string.IsNullOrEmpty(campaignsToRun?.ContinuationToken))
                throw new NotImplementedException($"Multiple page campaign fetch has not yet been implemented, Temporary campaign count exceeded for loyaltyAccount: {loyaltyAccount.Id}");

            //If there are campaigns from the event, add them to the list to run.
            if ((campaignsToRun?.Entities?.Count ?? 0) == 0)
                return ruleResponse;

            var numretries = 3;
            while (numretries > 0)
            {
                try
                {
                    //Try to lock the account
                    loyaltyAccount = await _loyaltyAccountService.TryLockAccount(tenantId, loyaltyAccount, Guid.NewGuid().ToString(), 500);
                    if (loyaltyAccount == null) throw new Exception($"Concurrency failure trying to lock account: {loyaltyAccount.Id}");

                    //Run rules
                    var ruleRequest = new RulesServiceRequest(null, null, loyaltyAccount, campaignsToRun.Entities, null, false);
                    AttachNotificationPort(ruleRequest);
                    ruleResponse = await _rulesService.ResettleAccountJourneysAsync(ruleRequest, default);

                    break;
                }
                catch (LockAcquisitionException laex)
                {
                    _logger.LogError(laex);
                    --numretries;
                }
                catch (ConcurrencyException cex)
                {
                    _logger.LogError(cex);
                    --numretries;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex);
                    throw;
                }
            }
            if (numretries <= 0) throw new Exception($"Concurrency failure while processing account: {loyaltyAccount.Id}");

            return ruleResponse;
        }


        private bool TryGetCaseInsensitiveProperty(JsonElement data, string propertyName, out JsonElement propertyValue)
        {
            propertyValue = default;

            foreach (var property in data.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    propertyValue = property.Value;
                    return true;
                }
            }

            return false;
        }

        private void AttachNotificationPort(RulesServiceRequest request)
        {
            request.NotificationService = _notificationService;
            request.TreatNotificationSendThrowAsFalse =
                _configuration?.GetValue<bool>("Journeys:NotificationOutcome:TreatSendThrowAsFalse") ?? false;
        }

        private JsonElement ToJsonElement(object obj)
        {
            string json = JsonSerializer.Serialize(obj, JsonUtility.GetDefaultOptions());
            using JsonDocument doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }

    }

    class UnpackedEntity
    {
        public UnpackedEntity(ProcessEventResponse responseObj)
        {
            responseObject = responseObj;
        }
        public ProcessEventResponse responseObject { get; set; }
        public string modelId { get; set; }
        public string? eventType { get; set; }
        public object? entity { get; set; }
    }
}