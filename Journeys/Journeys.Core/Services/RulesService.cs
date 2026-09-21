using Backend.Dto.Dynamic;
using Backend.Dto.Structures.Taxonomy;
using Journeys.Core.Caching;
using Journeys.Core.Extensions;
using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Interfaces.Services;
using Journeys.Core.Models;
using Journeys.Core.RulesEngine;
using Journeys.Core.RulesEngine.Comparitors;
using Journeys.Core.RulesEngine.Comparitors.Enums;
using Journeys.Core.RulesEngine.Engine;
using Journeys.Core.RulesEngine.Journey;
using Journeys.Core.RulesEngine.Journey.Enums;
using Journeys.Core.RulesEngine.Outcomes;
using Journeys.Core.RulesEngine.Providers;
using Journeys.Core.RulesEngine.Providers.Historical.State;
using Journeys.Core.RulesEngine.Rules;
using Journeys.Core.RulesEngine.Rules.Composite;
using Journeys.Core.Utility;
using Journeys.Core.Utility.StreamUtilities;
using Journeys.DTO.Exceptions;
using Journeys.DTO.Models;
using Journeys.DTO.Requests;
using Journeys.DTO.Responses;
using MassTransit;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using static MassTransit.ValidationResultExtensions;
using System.Text.Json;

namespace Journeys.Core.Services
{
    public class RulesService : IRulesService
    {
        const int FLUSH_CYCLE = 10;
        private const string WRAPPER_MODEL_META_KEY = "Wrapper";
        protected IJourneyAdapter journeyAdapter;
        //protected ILoyaltyAccountRuleStateAdapter ruleStateAdapter;
        protected ILoyaltyAccountService loyaltyAccountService;
        private readonly ICampaignService _campaignService;
        private readonly ITaxonomyDataAdapter _taxonomyDataAdapter;
        private readonly IHistoricalRuleStateTTLAdapter _historicalRuleStateTTLAdapter;
        private readonly IDynamicDataAdapter _dynamicDataAdapter;
        private readonly ModelCache _modelCache;

        private readonly object _loadedJourneysLock = new object();

        private readonly ILogger<RulesService> _logger;

        public RulesService(IJourneyAdapter journeyAdapter, ICampaignService campaignService, 
            //ILoyaltyAccountRuleStateAdapter ruleStateAdapter, 
            ILoyaltyAccountService loyaltyAccountService, 
            ILogger<RulesService> logger, ITaxonomyDataAdapter taxonomyDataAdapter,
            IDynamicDataAdapter dynamicDataAdapter, ModelCache modelCache,
            IHistoricalRuleStateTTLAdapter historicalRuleStateTTLAdapter)
        {
            this.journeyAdapter = journeyAdapter;
            //this.ruleStateAdapter = ruleStateAdapter;
            this.loyaltyAccountService = loyaltyAccountService;
            this._campaignService = campaignService;
            this._logger = logger;
            _taxonomyDataAdapter = taxonomyDataAdapter;
            _historicalRuleStateTTLAdapter = historicalRuleStateTTLAdapter;
            _dynamicDataAdapter = dynamicDataAdapter;
            _modelCache = modelCache;
        }

        public async Task InitState(string tenantId, Stream inputStream, Stream outputStream, CancellationToken token, int batchSize = 10)
        {
            using (var ndUtil = new NDJsonUtility(inputStream))
            {
                var reader = ndUtil.Read<InitStateEntry>(x => x.AccountXId, token);
                var tasks = new Dictionary<string, Task<Result<LoyaltyAccountDto>>>();
                int flushCycle = 0;
                await foreach (var line in reader)
                {
                    flushCycle++;
                    if(flushCycle >= FLUSH_CYCLE)
                    {
                        flushCycle = 0;
                        await outputStream.FlushAsync(token);
                    }
                    if (line.Value.IsSuccess)
                    {
                        if (line.Value.Value != null)
                        {
                            tasks.Add(line.Key, InitState(tenantId, line.Value.Value));
                        }
                    }
                    else
                    {
                        var output = JsonUtility.Serialize(line.Value, _logger) + "\r\n";
                        await outputStream.WriteAsync(Encoding.UTF8.GetBytes(output), 0, output.Length, token);
                    }

                    if (tasks.Values.Any(x => x.IsCompleted))
                    {
                        foreach (var t in tasks.Where(x => x.Value.IsCompleted))
                        {
                            if (t.Value == null)
                            {
                                var output = JsonUtility.Serialize(new KeyValuePair<string, Exception>(t.Key, new Exception("Task yielded null when processing state.")), _logger) + "\r\n";
                                await outputStream.WriteAsync(Encoding.UTF8.GetBytes(output), 0, output.Length, token);
                                tasks.Remove(t.Key);
                                continue;
                            }

                            if (t.Value.IsCompletedSuccessfully)
                            {
                                var output = JsonUtility.Serialize(t.Value.Result, _logger);
                                await outputStream.WriteAsync(Encoding.UTF8.GetBytes(output), 0, output.Length, token);
                                tasks.Remove(t.Key);
                                continue;
                            }
                            else
                            {
                                var output = JsonUtility.Serialize(new KeyValuePair<string, string>(t.Key, t.Value?.Exception?.Message ?? "Task was not completed successfully"), _logger) + "\r\n";
                                await outputStream.WriteAsync(Encoding.UTF8.GetBytes(output), 0, output.Length, token);
                                tasks.Remove(t.Key);
                                continue;
                            }
                        }
                    }

                    if (tasks.Count >= batchSize)
                    {
                        var result = await Task.WhenAny(tasks.Values);
                        var output = JsonUtility.Serialize(result, _logger) + "\r\n";
                        await outputStream.WriteAsync(Encoding.UTF8.GetBytes(output), 0, output.Length, token);

                        tasks.Remove(tasks.FirstOrDefault(x => x.Value == result).Key);
                    }
                }

                var remainingTasks = tasks.Values?.ToArray();
                if (remainingTasks != null)
                {
                    await Task.WhenAll(remainingTasks);
                    foreach (var t in tasks)
                    {
                        var output = JsonUtility.Serialize(t.Value.Result, _logger) + "\r\n";
                        await outputStream.WriteAsync(Encoding.UTF8.GetBytes(output), 0, output.Length, token);
                    }
                    await outputStream.FlushAsync(token);
                }
                else
                {
                    await outputStream.FlushAsync(token);
                    return;
                }
            }
        }

        public async Task<Result<LoyaltyAccountDto>> InitState(string tenantId, InitStateEntry entry)
        {
            var errors = new Dictionary<string, List<string>>();
            var addError = new Action<string, string>((string slug, string error) =>
            {
                if (!errors.ContainsKey(slug))
                {
                    errors.Add(slug, new List<string>());
                }
                errors[slug].Add(error);
            });
            Action<string, Exception> addErrorEx = null;
            addErrorEx = new Action<string, Exception>((string slug, Exception error) =>
            {
                if (error is AggregateException aggEx)
                {
                    foreach (var inner in aggEx.InnerExceptions)
                    {
                        addErrorEx(slug, inner);
                    }
                }
                else
                {
                    addError(slug, error.Message);
                }
            });
            
            LoyaltyAccountDto? account = null;
            //We need to handle the bulk of this... we are saving the same document repeatedly if multiple accounts exist.
            foreach(var tier in entry.JourneyStates)
            {
                foreach (var journey in tier.JourneyIds)
                {
                    try
                    {
                        account = await ManuallyEnterTier(tenantId, tier.CampaignId, journey, entry.AccountXId, CancellationToken.None, true);
                    }
                    catch (Exception ex)
                    {
                        var keySlug = "WARN_ManuallyEnterTier";
                        addErrorEx(keySlug, ex);
                        var errResult = Result<LoyaltyAccountDto>.Error($"XId: {entry.AccountXId} failed to Initialize State", JsonUtility.Serialize(errors, _logger));
                        return errResult;
                    }
                }
            }
            if(account == null)
                account = await loyaltyAccountService.GetLoyaltyAccountByExtIdAsync(tenantId, entry.AccountXId);

            if (account == null)
            {
                addError("FATAL_LoyaltyAccountDoesNotExist", $"No loyalty account with External Reference Id of \"{entry.AccountXId}\" was found.");
                return Result<LoyaltyAccountDto>.Error($"XId: {entry.AccountXId} failed to Initialize State", JsonUtility.Serialize(errors, _logger));
            }

            var pointTasks = new List<Task<PointLedgerDto>>();
            foreach (var point in entry.PointStates)
            {
                if (point.Balance == 0)
                    continue;
                var pointTask = await loyaltyAccountService.DepositPointsAsync(tenantId, new DTO.Requests.PointDespositRequest{
                    Amount = point.Balance,
                    LoyaltyAccountId = account.Id ?? throw new NullReferenceException($"Loyalty Account has an Id value of null for XId {entry.AccountXId}"),
                    DepositDate = DateTime.UtcNow,
                    EventId = $"{Guid.NewGuid()}",
                    EventType = "init",
                    PointAccountTypeId = point.PointAccountTypeId,
                    UserId = "System"
                });
            }
            await Task.WhenAll(pointTasks);

            if (!pointTasks.All(x => x.IsCompletedSuccessfully))
            {
                addError("WARN_PointInitialization", $"Not all points were awarded successfully. One or more tasks indicated a failure state.");
                return Result<LoyaltyAccountDto>.Error($"XId: {entry.AccountXId} failed to fully Initialize State", JsonUtility.Serialize(errors, _logger));
            }

            return Result<LoyaltyAccountDto>.Success(account);
        }

        public async Task<LoyaltyAccountDto> ManuallyExitTier(string tenantId, string campaignId, string journeyId, string loyaltyAccountXReference, CancellationToken token)
        {
            var campaignTask = _campaignService.FetchCampaignAsync(tenantId, campaignId, CampaignStatusStrings.Live);
            var accountTask = loyaltyAccountService.GetLoyaltyAccountByExtIdAsync(tenantId, loyaltyAccountXReference);

            await Task.WhenAll(campaignTask, accountTask);
            var campaignDto = campaignTask.Result;
            var account = accountTask.Result;

            if (account == null)
            {
                throw new APIErrorsException(new Dictionary<string, string> { { "NOTFOUND", $"Loyalty Account with External ID {loyaltyAccountXReference} not found." } });
            }

            if (campaignDto == null)
            {
                throw new APIErrorsException(new Dictionary<string, string> { { "NOTFOUND", $"Campaign with ID {campaignId} not found." } });
            }

            if (campaignDto.Journey == null)
            {
                throw new APIErrorsException(new Dictionary<string, string> { { "INVALID", $"Campaign with ID {campaignId} does not contain a Root Journey." } });
            }
            var rootJourneyNode = campaignDto.Journey.FromDto();
            if (rootJourneyNode == null || rootJourneyNode.Id == null)
            {
                throw new APIErrorsException(new Dictionary<string, string> { { "UNHANDLED", $"Unable to convert Root Journey Node from ccampaign with ID {campaignId} from DTO to Engine Object." } });
            }

            var targetNode = FindNode(rootJourneyNode, journeyId);
            if (targetNode == null || targetNode.Id == null)
            {
                throw new APIErrorsException(new Dictionary<string, string> { { "NOTFOUND", $"Journey with ID {journeyId} not found in Campaign with ID {campaignId}." } });
            }

            if (account.Journeys == null)
            {
                account.Journeys = new List<LoyaltyAccountJourneyDto>();
            }

            var journeyContainer = account.Journeys.SingleOrDefault(x => x.RootJourneyNodeId.Equals(rootJourneyNode.Id, StringComparison.InvariantCultureIgnoreCase));
            if (journeyContainer == null)
            {
                throw new APIErrorsException(new Dictionary<string, string> { { "DUPLICATE", $"Loyalty Account with External ID {loyaltyAccountXReference} is already not present in Journey with ID {journeyId}." } });
            }

            if (journeyContainer.JourneyNodeIds == null)
                journeyContainer.JourneyNodeIds = new List<string>();

            if (!journeyContainer.JourneyNodeIds.Contains(targetNode.Id))
            {
                throw new APIErrorsException(new Dictionary<string, string> { { "DUPLICATE", $"Loyalty Account with External ID {loyaltyAccountXReference} is already not present in Journey with ID {journeyId}." } });
            }

            journeyContainer.JourneyNodeIds.Remove(targetNode.Id);
            //Not in the campaign anymore, remove the rootNode binding.
            if (!journeyContainer.JourneyNodeIds.Any())
            {
                account.Journeys.Remove(journeyContainer);
            }

            var response = await loyaltyAccountService.UpsertLoyaltyAccountAsync(tenantId, account);
            return response;
        }
        public async Task<LoyaltyAccountDto> ManuallyEnterTier(string tenantId, string campaignId, string journeyId, string loyaltyAccountXReference, CancellationToken token, bool swapAssignment = false)
        {
            var campaignTask = _campaignService.FetchCampaignAsync(tenantId, campaignId, CampaignStatusStrings.Live);
            var accountTask = loyaltyAccountService.GetLoyaltyAccountByExtIdAsync(tenantId, loyaltyAccountXReference);

            await Task.WhenAll(campaignTask, accountTask);
            var campaignDto = campaignTask.Result;
            var account = accountTask.Result;
            
            if (account == null)
            {
                throw new APIErrorsException(new Dictionary<string, string> { { "NOTFOUND", $"Loyalty Account with External ID {loyaltyAccountXReference} not found." } });
            }

            if (campaignDto == null)
            {
                throw new APIErrorsException(new Dictionary<string, string> { { "NOTFOUND", $"Campaign with ID {campaignId} not found." } });
            }

            if (campaignDto.Journey == null)
            {
                throw new APIErrorsException(new Dictionary<string, string> { { "INVALID", $"Campaign with ID {campaignId} does not contain a Root Journey." } });
            }
            var rootJourneyNode = campaignDto.Journey.FromDto();
            if (rootJourneyNode == null || rootJourneyNode.Id == null)
            {
                throw new APIErrorsException(new Dictionary<string, string> { { "UNHANDLED", $"Unable to convert Root Journey Node from ccampaign with ID {campaignId} from DTO to Engine Object." } });
            }

            var targetNode = FindNode(rootJourneyNode, journeyId);
            if (targetNode == null || targetNode.Id == null)
            {
                throw new APIErrorsException(new Dictionary<string, string> { { "NOTFOUND", $"Journey with ID {journeyId} not found in Campaign with ID {campaignId}." } });
            }

            if (account.Journeys == null || swapAssignment)
            {
                account.Journeys = new List<LoyaltyAccountJourneyDto>();
            }

            var journeyContainer = account.Journeys.SingleOrDefault(x => x.RootJourneyNodeId.Equals(rootJourneyNode.Id, StringComparison.InvariantCultureIgnoreCase));
            if(journeyContainer == null)
            {
                journeyContainer = new LoyaltyAccountJourneyDto() { RootJourneyNodeId = rootJourneyNode.Id, JourneyNodeIds = new List<string>() };
                account.Journeys.Add(journeyContainer);
            }

            if(journeyContainer.JourneyNodeIds == null)
                journeyContainer.JourneyNodeIds = new List<string>();

            if (journeyContainer.JourneyNodeIds.Contains(targetNode.Id))
            {
                //TODO: Considered a hard failure here but rather decided to fail gracefully and return the account.
                //throw new APIErrorsException(new Dictionary<string, string> { { "DUPLICATE", $"Loyalty Account with External ID {loyaltyAccountXReference} is already in Journey with ID {journeyId}." } });
                return account;
            }

            journeyContainer.JourneyNodeIds.Add(targetNode.Id);
            var response = await loyaltyAccountService.UpsertLoyaltyAccountAsync(tenantId, account);
            return response;
        }

        public async Task<RulesServiceResponse> ProcessRulesAsync(RulesServiceRequest request, CancellationToken token)
        {
            //var engineState = new RulesEngineState(ruleStateAdapter, request, request.CalculateOnly, loyaltyAccountService, request.EventId, request.EventType);
            var engineState = new RulesEngineState(request, request.CalculateOnly, loyaltyAccountService, request.EventId, request.EventType);
            try
            {
                var campaignIds = request.Campaigns == null
                    ? ""
                    : string.Join(",", request.Campaigns.Where(x => x != null).Select(x => x.Id));
                _logger.LogInformation(
                    "ProcessRules start: TenantId={TenantId}, PayloadModelId={PayloadModelId}, EventType={EventType}, EventId={EventId}, CampaignCount={CampaignCount}, CampaignIds={CampaignIds}",
                    engineState.TenantId,
                    request.PayloadModelId,
                    request.EventType,
                    request.EventId,
                    request.Campaigns?.Count ?? 0,
                    campaignIds);

                //TODO: Load globals from DB
                //request.Globals = new Dictionary<string, object>
                //{
                //    { "excludedPaymentTypes", new List<string> { "giftcard" } }
                //};

                engineState = await HydrateState(engineState, token);

                //TODO: This could be made async.
                //For ease of debugging, keeping it parallel for now is more helpful.
                foreach (var c in engineState.Campaigns)
                {
                    //Campaign has not started yet, do not evaluate the rules.
                    if (c.StartDate > DateTimeOffset.UtcNow)
                    {
                        _logger.LogDebug("ProcessRules skip campaign {CampaignId}: not started (StartDate {StartDate} > now).", c.Id, c.StartDate);
                        continue;
                    }

                    //Campaign has ended, do not evaluate the rules.
                    if (c.EndDate.HasValue && c.EndDate < DateTimeOffset.UtcNow)
                    {
                        _logger.LogDebug("ProcessRules skip campaign {CampaignId}: ended (EndDate {EndDate}).", c.Id, c.EndDate);
                        continue;
                    }

                    var journey = c.Journey;
                    if (journey != null)
                    {
                        if (String.IsNullOrEmpty(c.Id))
                        {
                            throw new NullReferenceException("Encountered a Campaign with a null Id");
                        }

                        engineState.CampaignsEvaluated.Add(c.Id);
                        engineState.CurrentCampaignId = c.Id;
                        engineState.ProcessingJourney = journey;

                        //Process the Journey.
                        engineState = await ProcessJourneyAsync(journey, engineState, token);

                        //Settle the account into the newly earned journey node(s)
                        // do this prior to saving the loyalty account (which happens in Finally)
                        //engineState = await SettleStateAsync(journey, engineState, token);
                    }
                }

                var evaluatedIds = string.Join(",", engineState.CampaignsEvaluated);
                var appliedRuleSets = string.Join(",", engineState.AppliedRuleSets);
                _logger.LogInformation(
                    "ProcessRules complete: TenantId={TenantId}, PayloadModelId={PayloadModelId}, CampaignsEvaluatedCount={EvaluatedCount}, CampaignsEvaluatedIds={EvaluatedIds}, AppliedRuleSetCount={RuleSetCount}, AppliedRuleSetIds={AppliedRuleSetIds}",
                    engineState.TenantId,
                    request.PayloadModelId,
                    engineState.CampaignsEvaluated.Count,
                    evaluatedIds,
                    engineState.AppliedRuleSets.Count,
                    appliedRuleSets);

                return new RulesServiceResponse(engineState);
            }
            finally
            {
                engineState.LoyaltyAccount.LockLeaseExpiration = null;
                engineState.LoyaltyAccount.LockLeaseKey = null;
                var accountSave = loyaltyAccountService.UpsertLoyaltyAccountAsync(engineState.TenantId, engineState.LoyaltyAccount.ToDto());
                var ttlSaves = Task.CompletedTask;
                if (_historicalRuleStateTTLAdapter != null && engineState.ModifiedHistoricalStateKeys?.Count > 0 && !string.IsNullOrEmpty(engineState.EventId))
                {
                    var upserts = new List<Task>();
                    foreach (var sk in engineState.ModifiedHistoricalStateKeys)
                    {
                        if (!engineState.LoadedState.TryGetValue(sk, out var s) || s is not SimpleState simple)
                            continue;
                        var entry = simple.ContributorTTLsList?.FirstOrDefault(t => string.Equals(t.DecayingEntityId, EventKeyUtility.ToEventKey(engineState.EventType, engineState.EventId), StringComparison.OrdinalIgnoreCase));
                        if (entry == null) continue;
                        upserts.Add(_historicalRuleStateTTLAdapter.UpsertEventTtlAsync(
                            engineState.TenantId, engineState.LoyaltyAccountId, sk,
                            engineState.EventType ?? "", engineState.EventId, entry.TTL, entry.ContributionValue));
                    }
                    if (upserts.Count > 0)
                        ttlSaves = Task.WhenAll(upserts);
                }
                await Task.WhenAll(accountSave, ttlSaves);
            }
        }

        public async Task<RulesServiceResponse> ResettleAccountJourneysAsync(RulesServiceRequest request, CancellationToken token)
        {
            //var engineState = new RulesEngineState(ruleStateAdapter, request, request.CalculateOnly, loyaltyAccountService);
            var engineState = new RulesEngineState(request, request.CalculateOnly, loyaltyAccountService);
            try
            {
                engineState = await HydrateState(engineState, token);

                //TODO: This could be made async.
                //For ease of debugging, keeping it parallel for now is more helpful.
                foreach (var c in engineState.Campaigns)
                {
                    var journey = c.Journey;
                    if (journey != null)
                    {
                        if (String.IsNullOrEmpty(c.Id))
                        {
                            throw new NullReferenceException("Encountered a Campaign with a null Id");
                        }

                        engineState.CampaignsEvaluated.Add(c.Id);
                        engineState.CurrentCampaignId = c.Id;
                        engineState.ProcessingJourney = journey;

                        //Settle the account into the newly earned journey node(s)
                        // do this prior to saving the loyalty account (which happens in Finally)
                        engineState = await SettleStateAsync(journey, engineState, token);
                    }
                }

                return new RulesServiceResponse(engineState);
            }
            finally
            {
                //Clear lock and save updated state
                engineState.LoyaltyAccount.LockLeaseExpiration = null;
                engineState.LoyaltyAccount.LockLeaseKey = null;
                await loyaltyAccountService.UpsertLoyaltyAccountAsync(engineState.TenantId, engineState.LoyaltyAccount.ToDto());
            }
        }

        public async Task<RulesEngineState> SettleStateAsync(JourneyNode journey, RulesEngineState engineState, CancellationToken token)
        {
            if (journey.RootNodeId == null || journey.Id == null)
            {
                throw new NullReferenceException("Journey.RootNodeId and Journey.Id are required to process a Journey.");
            }

            var stateManager = engineState.JourneyStateManager;
            var navigator = new JourneyNavigator(stateManager);
            var journeyStates = engineState.JourneyState;

            if (journeyStates.Journeys != null && journeyStates.Journeys.ContainsKey(journey.RootNodeId))
            {
                var nodeIds = journeyStates.Journeys[journey.RootNodeId].ToList();
                if (!nodeIds.Contains(journey.Id))
                {
                    // Process nodes that the account is actually in
                    var curNodes = nodeIds.ToList();
                    foreach (var id in curNodes)
                    {
                        var targetJourney = FindNode(journey, id);
                        if (targetJourney != null)
                        {
                            // Evaluate exit first
                            var exitResult = await navigator.EvaluateExitAsync(targetJourney, engineState, token);
                            if (exitResult.ShouldNavigate)
                            {
                                // Award exit outcomes
                                if (exitResult.NavigationOutcomes?.Any() ?? false)
                                {
                                    engineState.TryAddUpdateEarnedOutcomes(
                                        exitResult.NavigationOutcomes,
                                        NavigationType.Exit.ToString());
                                }

                                // Apply exit (remove node)
                                engineState.ApplyNavigationResult(exitResult, journey.RootNodeId);

                                // Re-settle because ejecting from a node requires re-evaluation from the top
                                return await SettleStateAsync(journey, engineState, token);
                            }

                            // Evaluate and apply transitions and entries for children
                            if (targetJourney.Children != null && targetJourney.Children.Any())
                            {
                                // Evaluate all transitions
                                var transitionResults = await navigator.EvaluateTransitionAsync(
                                    targetJourney, targetJourney.Children, engineState, token);
                                
                                foreach (var transitionResult in transitionResults)
                                {
                                    if (transitionResult.NavigationOutcomes?.Any() ?? false)
                                    {
                                        engineState.TryAddUpdateEarnedOutcomes(
                                            transitionResult.NavigationOutcomes,
                                            NavigationType.Transition.ToString());
                                    }
                                    engineState.ApplyNavigationResult(transitionResult, journey.RootNodeId);
                                }

                                // Evaluate and apply entries for children
                                foreach (var child in targetJourney.Children)
                                {
                                    var entryResult = await navigator.EvaluateEntryAsync(child, engineState, token);
                                    if (entryResult.ShouldNavigate)
                                    {
                                        if (entryResult.NavigationOutcomes?.Any() ?? false)
                                        {
                                            engineState.TryAddUpdateEarnedOutcomes(
                                                entryResult.NavigationOutcomes,
                                                NavigationType.Entry.ToString());
                                        }
                                        engineState.ApplyNavigationResult(entryResult, journey.RootNodeId);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                // Account not in journey - evaluate entry into root
                var entryResult = await navigator.EvaluateEntryAsync(journey, engineState, token);
                if (entryResult.ShouldNavigate)
                {
                    if (entryResult.NavigationOutcomes?.Any() ?? false)
                    {
                        engineState.TryAddUpdateEarnedOutcomes(
                            entryResult.NavigationOutcomes,
                            NavigationType.Entry.ToString());
                    }
                    engineState.ApplyNavigationResult(entryResult, journey.RootNodeId);

                    // Now evaluate transitions and entries for children
                    if (journey.Children != null && journey.Children.Any())
                    {
                        var transitionResults = await navigator.EvaluateTransitionAsync(
                            journey, journey.Children, engineState, token);
                        
                        foreach (var transitionResult in transitionResults)
                        {
                            if (transitionResult.NavigationOutcomes?.Any() ?? false)
                            {
                                engineState.TryAddUpdateEarnedOutcomes(
                                    transitionResult.NavigationOutcomes,
                                    NavigationType.Transition.ToString());
                            }
                            engineState.ApplyNavigationResult(transitionResult, journey.RootNodeId);
                        }

                        foreach (var child in journey.Children)
                        {
                            var childEntryResult = await navigator.EvaluateEntryAsync(child, engineState, token);
                            if (childEntryResult.ShouldNavigate)
                            {
                                if (childEntryResult.NavigationOutcomes?.Any() ?? false)
                                {
                                    engineState.TryAddUpdateEarnedOutcomes(
                                        childEntryResult.NavigationOutcomes,
                                        NavigationType.Entry.ToString());
                                }
                                engineState.ApplyNavigationResult(childEntryResult, journey.RootNodeId);
                            }
                        }
                    }
                }
            }

            return engineState;
        }

        /// <summary>
        /// Hydrates engine state for historical, taxonomy, and journey rules. All load I/O runs in parallel
        /// (expired TTLs, taxonomy fetches, journey loads, legacy provider batch); then we apply decay,
        /// delete expired TTLs in parallel, and merge taxonomy/journey results.
        /// </summary>
        protected async Task<RulesEngineState> HydrateState(RulesEngineState engineState, CancellationToken token)
        {
            if (engineState.Campaigns == null)
                return engineState;

            var Journeys = engineState.Campaigns
                .Select(x => x.Journey)
                .Where(x => x != null)
                .ToList();

            var historicalRules = engineState.Campaigns
                .Where(x => x.Journey != null)
                .SelectMany(campaign => campaign.Journey!.FlattenToRulesOfType<HistoricalRule>()
                    .Select(rule => (CampaignId: campaign.Id, Rule: rule)))
                .GroupBy(x => x.CampaignId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Rule).ToList());

            var taxonomyRules = engineState.Campaigns
                .Where(x => x.Journey != null)
                .SelectMany(x => x.Journey!.FlattenToRulesOfType<TaxonomicRule>())
                .ToList();

            var allLoadTasks = new List<Task>();
            var getExpiredTasks = new List<Task<(string key, List<HistoricalRuleEventTTL> expired)>>();
            var taxonomyLoadTasks = new List<Task<(TaxonomicRule rule, List<TaxonomyDto> taxs)>>();

            List<string> stateKeys = new List<string>();
            var now = DateTimeOffset.UtcNow;

            if (historicalRules != null && historicalRules.Any())
            {
                foreach (var rules in historicalRules)
                {
                    foreach (var r in rules.Value)
                    {
                        var value = (HistoricalRule)r;
                        var statekey = RuleBase.GetHistoricalStateKey(rules.Key, r.Id ?? r.HistoricalValueProvider?.Id ?? "");
                        if (!string.IsNullOrEmpty(statekey) && !stateKeys.Contains(statekey))
                        {
                            stateKeys.Add(statekey);
                        }
                    }
                }

                if (stateKeys.Count > 0)
                {
                    foreach (var sk in stateKeys)
                    {
                        var key = sk;
                        var t = _historicalRuleStateTTLAdapter.GetExpiredAsync(engineState.TenantId, engineState.LoyaltyAccountId, key, now)
                            .ContinueWith(t2 => (key, t2.IsCompletedSuccessfully ? t2.Result : new List<HistoricalRuleEventTTL>()), token);
                        getExpiredTasks.Add(t);
                        allLoadTasks.Add(t);
                    }
                }
            }

            if (taxonomyRules != null && taxonomyRules.Any())
            {
                var tenantId = engineState.TenantId;
                var adapter = _taxonomyDataAdapter;
                foreach (var rule in taxonomyRules)
                {
                    if (string.IsNullOrEmpty(rule.TaxonomyId) || rule.LeftProvider == null) continue;
                    var r = rule;
                    var taxTask = Task.Run(async () =>
                    {
                        var lstKeys = await r.GetTaxonomiesKeys(engineState, token) ?? new List<string>();
                        var taxs = (lstKeys.Count > 0) ? await adapter.GetManyTaxonomiesByXidAsync(tenantId, lstKeys) : null;
                        return (r, taxs);
                    }, token);
                    taxonomyLoadTasks.Add(taxTask);
                    allLoadTasks.Add(taxTask);
                }
            }

            if (Journeys != null && Journeys.Any())
            {
                var journeyTasks = Journeys
                    .Select(x =>
                        journeyAdapter
                            .LoadLoyaltyAccountJourneyState(engineState.TenantId, engineState.LoyaltyAccount)
                            .ContinueWith(async t =>
                            {
                                if (t.IsCompletedSuccessfully && t != null)
                                {
                                    var loyaltyAccountJourneyState = engineState.JourneyState;
                                    if (loyaltyAccountJourneyState.Journeys == null)
                                        loyaltyAccountJourneyState = new LoyaltyAccountJourneyState(engineState.LoyaltyAccountId, new List<LoyaltyAccountJourney>());

                                    foreach (var j in Journeys)
                                    {
                                        if (j == null || (string.IsNullOrEmpty(j.RootNodeId) && string.IsNullOrEmpty(j.Id))) continue;
                                        var rootNodeId = j.RootNodeId ?? j.Id;
                                        Dictionary<string, JourneyNode> startingStates;
                                        if (loyaltyAccountJourneyState.Journeys != null && loyaltyAccountJourneyState.Journeys.ContainsKey(rootNodeId))
                                        {
                                            var rootStates = loyaltyAccountJourneyState.Journeys[rootNodeId];
                                            if (rootStates == null)
                                                throw new InvalidDataException($"Invalid root Journey Id encountered in NavigateTo() for JourneyRoot {rootNodeId}");
                                            startingStates = rootStates
                                                ?.Select(x => new { JourneyId = x, JourneyNode = j.NavigateTo(x) })
                                                ?.ToDictionary(x => x.JourneyId, x => x.JourneyNode);
                                        }
                                        else
                                            startingStates = new Dictionary<string, JourneyNode> { { rootNodeId, j } };

                                        var invalidStates = startingStates.Where(x => x.Value == null).ToDictionary();
                                        if (invalidStates.Any())
                                            throw new InvalidDataException($"Invalid Id encountered in NavigateTo({string.Join(", ", invalidStates.Keys)}) for JourneyRoot {rootNodeId}");

                                        if (engineState.LoadedJourneys == null)
                                            engineState.LoadedJourneys = new Dictionary<string, JourneyNode>();

                                        if (loyaltyAccountJourneyState.Journeys != null && loyaltyAccountJourneyState.Journeys.ContainsKey(j.RootNodeId))
                                        {
                                            lock (_loadedJourneysLock)
                                            {
                                                foreach (var state in loyaltyAccountJourneyState.Journeys[j.RootNodeId])
                                                {
                                                    var node = FindNode(j, state);
                                                    if (engineState.LoadedJourneys.ContainsKey(state))
                                                        engineState.LoadedJourneys[state] = node;
                                                    else
                                                        engineState.LoadedJourneys.Add(state, node);
                                                }
                                            }
                                        }
                                    }
                                }
                            }, token)
                    )
                    .ToList();
                allLoadTasks.AddRange(journeyTasks);
            }

            try
            {
                await Task.WhenAll(allLoadTasks);
            }
            catch (Exception ex)
            {
                throw;
            }

            if (stateKeys != null && stateKeys.Count > 0 && getExpiredTasks.Count > 0)
            {
                // Option B: Explicit load of RuleState from persistence so decay is applied to the current
                // aggregate (not zeros). Ensures LoyaltyAccount.RuleState is correct before TimeToLive decay and save.
                try
                {
                    var storedAccountDto = await loyaltyAccountService.GetLoyaltyAccountAsync(engineState.TenantId, engineState.LoyaltyAccountId);
                    if (storedAccountDto?.RuleState != null && storedAccountDto.RuleState.Count > 0)
                    {
                        engineState.LoyaltyAccount.RuleState ??= new Dictionary<string, HistoricalRuleState>();
                        foreach (var stateKey in stateKeys)
                        {
                            if (storedAccountDto.RuleState.TryGetValue(stateKey, out var dtoState))
                            {
                                engineState.LoyaltyAccount.RuleState[stateKey] = new HistoricalRuleState
                                {
                                    Count = dtoState.Count,
                                    Value = dtoState.Value,
                                    FirstOccurrence = dtoState.FirstOccurrence
                                };
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "HydrateState: Failed to load RuleState from persistence for account {AccountId}; decay will apply to in-memory state only.", engineState.LoyaltyAccountId);
                }

                var expiredByKey = new Dictionary<string, List<HistoricalRuleEventTTL>>();
                foreach (var t in getExpiredTasks)
                {
                    if (t.IsCompletedSuccessfully)
                    {
                        var (key, expired) = t.Result;
                        expiredByKey[key] = expired ?? new List<HistoricalRuleEventTTL>();
                    }
                }

                engineState.LoyaltyAccount.RuleState ??= new Dictionary<string, HistoricalRuleState>();

                foreach (var stateKey in stateKeys)
                {
                    var expired = expiredByKey.GetValueOrDefault(stateKey) ?? new List<HistoricalRuleEventTTL>();
                    var decayCount = expired.Count;
                    var decayValue = expired.Sum(e => e.ContributionValue ?? 0);

                    HistoricalRuleState agg;
                    if (engineState.LoyaltyAccount.RuleState.TryGetValue(stateKey, out agg))
                    {
                        agg.Count = Math.Max(0, agg.Count - decayCount);
                        agg.Value = Math.Max(0, agg.Value - decayValue);
                    }
                    else
                        agg = new HistoricalRuleState { Count = 0, Value = 0 };

                    engineState.LoyaltyAccount.RuleState[stateKey] = agg;
                    engineState.LoadedState[stateKey] = new SimpleState(stateKey, agg.Count, agg.Value, agg.FirstOccurrence, new List<StateTTL>());
                }

                // Fetch existing TimeToLive for current event per state key (for idempotent delta updates when reprocessing same event)
                if (_historicalRuleStateTTLAdapter != null
                    && !string.IsNullOrEmpty(engineState.EventType)
                    && !string.IsNullOrEmpty(engineState.EventId))
                {
                    var existingTtlTasks = stateKeys
                        .Select(async sk =>
                        {
                            var existing = await _historicalRuleStateTTLAdapter.GetByEventAsync(
                                engineState.TenantId, engineState.LoyaltyAccountId, sk,
                                engineState.EventType, engineState.EventId);
                            return (sk, existing);
                        })
                        .ToList();
                    var existingTtlResults = await Task.WhenAll(existingTtlTasks);
                    foreach (var (sk, existing) in existingTtlResults)
                    {
                        if (existing != null)
                            engineState.ExistingEventTtlByStateKey[sk] = existing;
                    }
                }

                var stateKeysWithExpired = stateKeys.Where(sk => (expiredByKey.GetValueOrDefault(sk)?.Count ?? 0) > 0).ToList();
                if (stateKeysWithExpired.Count > 0)
                {
                    await loyaltyAccountService.UpsertLoyaltyAccountAsync(engineState.TenantId, engineState.LoyaltyAccount.ToDto());
                    var deleteTasks = stateKeysWithExpired
                        .Select(sk => _historicalRuleStateTTLAdapter.DeleteManyAsync(engineState.TenantId, engineState.LoyaltyAccountId, sk, expiredByKey[sk]));
                    await Task.WhenAll(deleteTasks);
                }
            }

            foreach (var t in taxonomyLoadTasks)
            {
                if (!t.IsCompletedSuccessfully || t.Result.taxs == null || t.Result.taxs.Count == 0) continue;
                var (rule, taxs) = t.Result;
                engineState.Taxonomies ??= new Dictionary<string, List<TaxonomyDto>>();
                if (engineState.Taxonomies.ContainsKey(rule.TaxonomyId))
                {
                    foreach (var tx in taxs)
                    {
                        var curtx = engineState.Taxonomies[rule.TaxonomyId].FirstOrDefault(x => x.Id == tx.Id);
                        if (curtx != null) curtx = tx;
                        else engineState.Taxonomies[rule.TaxonomyId].Add(tx);
                    }
                }
                else
                    engineState.Taxonomies.Add(rule.TaxonomyId, taxs);
            }

            return engineState;
        }

        protected JourneyNode? FindNode(JourneyNode journey, string targetId)
        {
            if (journey.Id != null && journey.Id.Equals(targetId, StringComparison.CurrentCultureIgnoreCase)) return journey;
            if (journey.Children != null)
                foreach (var c in journey.Children)
                {
                    var node = FindNode(c, targetId);
                    if (node == null) continue;
                    else
                        return node;
                }

            return null;
        }

        protected async Task<RulesEngineState> ProcessJourneyAsync(JourneyNode journey, RulesEngineState engineState, CancellationToken token)
        {
            if (journey.RootNodeId == null || journey.Id == null)
            {
                throw new NullReferenceException("Journey.RootNodeId and Journey.Id are required to process a Journey.");
            }

            var stateManager = engineState.JourneyStateManager;
            var journeyStates = engineState.JourneyState;

            // Navigate to the appropriate place in the journey if account is already in child nodes
            if (journeyStates.Journeys != null && journeyStates.Journeys.ContainsKey(journey.RootNodeId))
            {
                var nodeIds = journeyStates.Journeys[journey.RootNodeId].ToArray();
                // If the current node is not in the list, process the nodes that are in state
                if (nodeIds.Count() > 0 && !nodeIds.Contains(journey.Id))
                {
                    var badIds = new List<string>();
                    foreach (var id in nodeIds)
                    {
                        var targetJourney = FindNode(journey, id);
                        if (targetJourney == null)
                        {
                            // Bad data - log and clean up
                            badIds.Add(id);
                        }
                        else
                        {
                            // Process the node the account is actually in
                            await ProcessJourneyAsync(targetJourney, engineState, token);
                        }
                    }
                    // Cleanup bad data
                    foreach (var bad in badIds)
                    {
                        stateManager.RemoveNode(journey.RootNodeId, bad);
                    }
                    // Intentionally exit - we processed the nodes the account is in, not the root
                    return engineState;
                }
            }

            // Process the root journey node (Entry evaluation and navigation are handled by JourneyNode.ProcessAsync)
            await journey.ProcessAsync(engineState, token);

            if (engineState.CalculateOnly)
                return engineState;

            // Award outcomes for this journey
            var outcomes = engineState.EarnedOutcomes?
                .Values?
                .SelectMany(x => x)?
                .Where(x => x.IssuingJourneyNodeId?.Equals(journey.RootNodeId) ?? false)?
                .ToList();

            if (outcomes?.Any() ?? false)
            {
                // If this is a reprocessing of a previously received event,
                // remove any point entries for this event as they will be replaced/updated by AwardOutcomes
                foreach (var o in outcomes)
                {
                    var earnedOutcome = await o.IssuingOutcome.AwardOutcomeAsync(engineState, loyaltyAccountService, token);
                    if (earnedOutcome != null)
                    {
                        // Preserve context from calculated outcome; honor returned IsAwarded.
                        earnedOutcome.CampaignId = o.CampaignId;
                        earnedOutcome.RuleSetId = o.RuleSetId;
                        engineState.TryAddUpdateEarnedOutcomes(earnedOutcome, earnedOutcome.IssuingOutcome.Id);
                    }
                }
            }

            return engineState;
        }

        private JourneyNode JourneyNodeByJourneyNodeId(JourneyNode node, string nodeId)
        {
            foreach(var n in node.Children)
            {
                if (n.Id == nodeId) return n;
                var cn = JourneyNodeByJourneyNodeId(n, nodeId);
                if (cn != null) return cn;
            }
            return null;
        }

        #region Tier Move Helper Methods

        private (decimal? lowerBound, decimal? upperBound) ExtractJourneyNodeBounds(
            JourneyNode node, 
            string pointAccountTypeId)
        {
            if (node.NavigationCriteria == null || 
                !node.NavigationCriteria.ContainsKey(NavigationType.Transition))
                return (null, null);
            
            var transitionCriteria = node.NavigationCriteria[NavigationType.Transition] 
                as SimpleNavigationCriteria;
            if (transitionCriteria?.NavConstraint == null)
                return (null, null);
            
            var constraint = transitionCriteria.NavConstraint;
            
            // Handle AndRule with children
            if (constraint is AndRule andRule && andRule.Children != null)
            {
                decimal? lower = null;
                decimal? upper = null;
                
                foreach (var child in andRule.Children)
                {
                    if (child is NumericPropertyRule numRule)
                    {
                        // Check if this rule uses the correct point account type
                        if (numRule.LeftProvider is PointBalanceProvider pointProvider &&
                            pointProvider.PointAccountTypeId == pointAccountTypeId)
                        {
                            if (numRule.Evaluator is NumericEvaluation eval &&
                                numRule.RightProvider is ConstantValueProvider constProvider &&
                                constProvider.Value != null)
                            {
                                var value = constProvider.Value is JsonElement je && je.ValueKind == JsonValueKind.Number
                                    ? je.GetDecimal()
                                    : Convert.ToDecimal(constProvider.Value);
                                if (eval.Comparison == NumEvalType.GreaterThanOrEqual) // 3
                                    lower = value;
                                else if (eval.Comparison == NumEvalType.LessThanOrEqual) // 5
                                    upper = value;
                            }
                        }
                    }
                }
                
                return (lower, upper);
            }
            // Handle single NumericPropertyRule
            else if (constraint is NumericPropertyRule singleRule)
            {
                if (singleRule.LeftProvider is PointBalanceProvider pointProvider &&
                    pointProvider.PointAccountTypeId == pointAccountTypeId &&
                    singleRule.RightProvider is ConstantValueProvider constProvider &&
                    constProvider.Value != null)
                {
                    var singleValue = constProvider.Value is JsonElement sje && sje.ValueKind == JsonValueKind.Number
                        ? sje.GetDecimal()
                        : Convert.ToDecimal(constProvider.Value);
                    return (singleValue, null);
                }
            }
            
            return (null, null);
        }

        private decimal GetPointsPerDollarForPointAccount(
            JourneyNode journeyNode, 
            string pointAccountTypeId)
        {
            if (journeyNode.Rules == null) return 0;
            
            foreach (var ruleSet in journeyNode.Rules)
            {
                if (ruleSet.Outcomes == null) continue;
                
                foreach (var outcome in ruleSet.Outcomes)
                {
                    if (outcome is DepositPointsOutcome depositOutcome &&
                        depositOutcome.AffectedPointAccountTypeIds != null &&
                        depositOutcome.AffectedPointAccountTypeIds.Contains(pointAccountTypeId))
                    {
                        return depositOutcome.PointsPerDollar;
                    }
                }
            }
            
            return 0;
        }

        private async Task<decimal> GetYearToDateSpendTotal(
            string tenantId, 
            string loyaltyAccountId, 
            CancellationToken token)
        {
            var startOfYear = new DateTimeOffset(new DateTime(DateTime.UtcNow.Year, 1, 1), TimeSpan.Zero);
            
            // Get order model
            var orderModel = await _modelCache.GetOrLoadByName(tenantId, "order");
            if (orderModel == null)
                throw new Exception("Order model not found");
            
            // Get wrapper model
            if (orderModel.ModelMetaData == null || 
                !orderModel.ModelMetaData.ContainsKey(WRAPPER_MODEL_META_KEY))
                throw new Exception("Order model wrapper not configured");
            
            var wrapperModelId = orderModel.ModelMetaData[WRAPPER_MODEL_META_KEY];
            var wrapperModel = await _modelCache.GetOrLoadById(tenantId, wrapperModelId);
            
            // Query order events for this account since start of year
            var query = "c.accountid = @LoyaltyAccountId AND c.timeofoccurrence >= @StartOfYear";
            var parameters = new Dictionary<string, object>
            {
                { "@LoyaltyAccountId", loyaltyAccountId },
                { "@StartOfYear", startOfYear }
            };
            
            decimal totalSpend = 0;
            string continuationToken = null;
            bool isFirstPage = true;
            const string dealerSpendableId = "a246f62f-a9d8-4fe0-bd65-9e9d7f5881a5";
            
            // Page through results
            while (isFirstPage || !string.IsNullOrEmpty(continuationToken))
            {
                isFirstPage = false;
                
                var result = await _dynamicDataAdapter.QueryEntitiesAsync<WrappedEventPayload>(
                    tenantId, 
                    wrapperModel.ID, 
                    query, 
                    parameters, 
                    null, 
                    SortOrder.ASC, 
                    100, 
                    token, 
                    continuationToken, 
                    null, 
                    false);
                
                if (result?.Entities == null) break;
                
                // Sum pointsdeposited from outcome states for dealer_spendable
                foreach (var wrappedEvent in result.Entities)
                {
                    if (wrappedEvent.OutcomeStates == null) continue;
                    
                    // Filter for Order events with dealer_spendable points
                    var dealerSpendableOutcomes = wrappedEvent.OutcomeStates
                        .Where(o => o.IssuingEventType == "Order" &&
                                   o.PointAccountTypeId == dealerSpendableId &&
                                   o.PointsDeposited.HasValue)
                        .ToList();
                    
                    totalSpend += dealerSpendableOutcomes.Sum(o => o.PointsDeposited.Value);
                }
                
                continuationToken = result.ContinuationToken;
            }
            
            return totalSpend;
        }

        private (JourneyNode maxNode, int depth) FindMaxTierNode(JourneyNode rootNode, int currentDepth = 0)
        {
            if (rootNode.Children == null || rootNode.Children.Count == 0)
                return (rootNode, currentDepth);
            
            var maxDepth = currentDepth;
            JourneyNode deepestNode = rootNode;
            
            foreach (var child in rootNode.Children)
            {
                var (childNode, childDepth) = FindMaxTierNode(child, currentDepth + 1);
                if (childDepth > maxDepth)
                {
                    maxDepth = childDepth;
                    deepestNode = childNode;
                }
            }
            
            return (deepestNode, maxDepth);
        }

        private int GetNodeDepth(JourneyNode node, string targetId, int currentDepth)
        {
            if (node.Id == targetId) return currentDepth;
            if (node.Children == null) return -1;
            
            foreach (var child in node.Children)
            {
                var depth = GetNodeDepth(child, targetId, currentDepth + 1);
                if (depth >= 0) return depth;
            }
            
            return -1;
        }

        private int GetTierLevel(CampaignDto campaign, JourneyNode journeyNode)
        {
            // Simple depth-based tier level
            if (campaign.Journey == null) return 0;
            var rootNode = campaign.Journey.FromDto();
            return GetNodeDepth(rootNode, journeyNode?.Id, 0);
        }

        private List<string> ValidateTierMove(
            LoyaltyAccountDto account,
            CampaignDto currentCampaign,
            CampaignDto targetCampaign,
            JourneyNode currentJourneyNode,
            JourneyNode targetJourneyNode,
            decimal tierQualificationAmount,
            decimal dealerSpendableAmount)
        {
            var errors = new List<string>();

            // Determine whether this is a cross-tier-system move (different campaigns).
            // Depth-based tier levels are only comparable within the same campaign's journey tree.
            // When moving across campaigns (e.g. Public → Private), skip the level comparison
            var isSameTierSystem = currentCampaign != null &&
                                   targetCampaign != null &&
                                   string.Equals(currentCampaign.ExtCampaignId, targetCampaign.ExtCampaignId, StringComparison.OrdinalIgnoreCase);

            if (isSameTierSystem)
            {
                // Check tier demotion only within the same tier system
                var currentTierLevel = GetTierLevel(currentCampaign, currentJourneyNode);
                var targetTierLevel  = GetTierLevel(targetCampaign,  targetJourneyNode);
                if (targetTierLevel < currentTierLevel)
                {
                    errors.Add("Tier demotion is not allowed");
                }

                // Check same journey (only meaningful within same tier system)
                if (currentJourneyNode?.Id == targetJourneyNode?.Id)
                {
                    errors.Add("Account is already in the selected journey");
                }
            }

            // Check negative adjustments
            if (tierQualificationAmount < 0)
            {
                errors.Add("Tier qualification adjustment would result in negative value");
            }
            if (dealerSpendableAmount < 0)
            {
                errors.Add("Dealer spendable adjustment would result in negative value");
            }
            
            return errors;
        }

        private async Task<decimal> CalculateTierQualificationAmount(
            string tenantId,
            LoyaltyAccountDto currentAccount,
            CampaignDto currentCampaign,
            CampaignDto targetCampaign,
            JourneyNode targetJourneyNode,
            string tierQualificationPointAccountTypeId)
        {
            // Determine current tier (public/private)
            var wasPrivateTier = currentCampaign?.ExtCampaignId?.Contains("private", StringComparison.OrdinalIgnoreCase) ?? false;

            // Get current qualifying balance
            var currentLedgers = await loyaltyAccountService.GetLoyaltyAccountPointsAsync(
                tenantId, currentAccount.Id);
            var currentQualifyingBalance = currentLedgers
                ?.FirstOrDefault(l => l.PointAccountTypeId == tierQualificationPointAccountTypeId)
                ?.CurrentBalance ?? 0;

            // Handle Public → Private transition
            if (targetCampaign.ExtCampaignId?.Contains("private", StringComparison.OrdinalIgnoreCase) == true && !wasPrivateTier)
            {
                var publicCampaign = currentCampaign; // Assuming current is public
                var maxTierNode = FindMaxTierNode(publicCampaign.Journey.FromDto());
                var bounds = ExtractJourneyNodeBounds(maxTierNode.maxNode, tierQualificationPointAccountTypeId);
                var publicMaxQualifyingBalance = bounds.upperBound ?? 0;
                return publicMaxQualifyingBalance + 1 - currentQualifyingBalance;
            }

            // Handle Private → Private transition
            if (targetCampaign.ExtCampaignId?.Contains("private", StringComparison.OrdinalIgnoreCase) == true && wasPrivateTier)
            {
                // Need to find public campaign to get max tier
                // For now, use target journey bounds
                var bounds = ExtractJourneyNodeBounds(targetJourneyNode, tierQualificationPointAccountTypeId);
                var privateTargetQualifyingBalance = bounds.upperBound ?? 0;
                return currentQualifyingBalance > privateTargetQualifyingBalance 
                    ? 0 
                    : privateTargetQualifyingBalance;
            }
            
            // Standard case: Use lowerBound of target journey
            var targetBounds = ExtractJourneyNodeBounds(targetJourneyNode, tierQualificationPointAccountTypeId);
            var standardTargetQualifyingBalance = targetBounds.lowerBound ?? 0;
            return standardTargetQualifyingBalance - currentQualifyingBalance;
        }

        private async Task<decimal> CalculateDealerSpendableAmount(
            string tenantId,
            string loyaltyAccountId,
            JourneyNode currentJourneyNode,
            JourneyNode targetJourneyNode,
            string dealerSpendablePointAccountTypeId,
            CancellationToken token)
        {
            // Get earning rates
            var oldEarnRate = GetPointsPerDollarForPointAccount(currentJourneyNode, dealerSpendablePointAccountTypeId);
            var newEarnRate = GetPointsPerDollarForPointAccount(targetJourneyNode, dealerSpendablePointAccountTypeId);
            
            // Get YTD spend total (in points awarded)
            var spendTotal = await GetYearToDateSpendTotal(tenantId, loyaltyAccountId, token);
            
            // Calculate adjustment
            var leftPart = Math.Round(newEarnRate * spendTotal, 0);
            var rightPart = Math.Round(oldEarnRate * spendTotal, 0);
            
            return leftPart - rightPart;
        }

        public async Task<TierMovePreviewResponse> PreviewTierMoveAsync(
            string tenantId, 
            MoveTierRequest request, 
            CancellationToken cancellationToken)
        {
            // Validate request
            if (request == null || string.IsNullOrEmpty(tenantId))
                throw new APIErrorsException(new Dictionary<string, string> { { "INVALID", "Invalid request or tenant ID." } });
            
            if (string.IsNullOrEmpty(request.TargetCampaignId) || string.IsNullOrEmpty(request.TargetJourneyId))
                throw new APIErrorsException(new Dictionary<string, string> { { "INVALID", "Target campaign and journey IDs are required." } });
            
            // Fetch account
            LoyaltyAccountDto account = null;
            if (!string.IsNullOrEmpty(request.LoyaltyAccountId))
            {
                account = await loyaltyAccountService.GetLoyaltyAccountAsync(tenantId, request.LoyaltyAccountId);
            }
            else if (!string.IsNullOrEmpty(request.LoyaltyAccountXReference))
            {
                account = await loyaltyAccountService.GetLoyaltyAccountByExtIdAsync(tenantId, request.LoyaltyAccountXReference);
            }
            
            if (account == null)
                throw new APIErrorsException(new Dictionary<string, string> { { "NOTFOUND", "Loyalty account not found." } });
            
            // Fetch campaigns
            var targetCampaignTask = _campaignService.FetchCampaignAsync(tenantId, request.TargetCampaignId, CampaignStatusStrings.Live);
            await targetCampaignTask;
            var targetCampaign = targetCampaignTask.Result;
            
            if (targetCampaign == null)
                throw new APIErrorsException(new Dictionary<string, string> { { "NOTFOUND", $"Target campaign {request.TargetCampaignId} not found." } });
            
            if (targetCampaign.Journey == null)
                throw new APIErrorsException(new Dictionary<string, string> { { "INVALID", $"Target campaign {request.TargetCampaignId} does not contain a journey." } });
            
            var targetRootJourney = targetCampaign.Journey.FromDto();
            var targetJourneyNode = FindNode(targetRootJourney, request.TargetJourneyId);
            if (targetJourneyNode == null)
                throw new APIErrorsException(new Dictionary<string, string> { { "NOTFOUND", $"Target journey {request.TargetJourneyId} not found in campaign." } });
            
            // Find current campaign
            CampaignDto currentCampaign = null;
            JourneyNode currentJourneyNode = null;
            if (account.Journeys != null && account.Journeys.Any())
            {
                // Find campaign that matches the root journey node ID
                var currentRootJourneyId = account.Journeys.FirstOrDefault()?.RootJourneyNodeId;
                if (!string.IsNullOrEmpty(currentRootJourneyId))
                {
                    var allCampaigns = await _campaignService.GetCampaignsByStatusAsync(tenantId, CampaignStatusStrings.Live, 100, null);
                    currentCampaign = allCampaigns?.Entities?.FirstOrDefault(c => 
                        c.Journey?.Id?.Equals(currentRootJourneyId, StringComparison.OrdinalIgnoreCase) == true);
                    
                    if (currentCampaign != null && currentCampaign.Journey != null)
                    {
                        var currentRootJourney = currentCampaign.Journey.FromDto();
                        var currentJourneyIds = account.Journeys.FirstOrDefault(j => 
                            j.RootJourneyNodeId.Equals(currentRootJourneyId, StringComparison.OrdinalIgnoreCase))?.JourneyNodeIds;
                        if (currentJourneyIds != null && currentJourneyIds.Any())
                        {
                            var currentJourneyId = currentJourneyIds.Last(); // Get the deepest node
                            currentJourneyNode = FindNode(currentRootJourney, currentJourneyId);
                        }
                    }
                }
            }
            
            // Get point account types
            var pointAccountTypes = await LoyaltyAccountService.GetPointAccountTypes(tenantId);
            var dealerSpendableType = pointAccountTypes?.FirstOrDefault(x => x.IsSpendable == true);
            var tierQualificationType = pointAccountTypes?.FirstOrDefault(x => 
                x.Id == "9c61de1a-460d-4172-8781-acc04c783880");
            
            if (dealerSpendableType == null || tierQualificationType == null)
                throw new APIErrorsException(new Dictionary<string, string> { { "CONFIG", "Required point account types not found." } });
            
            // Get current balances
            var currentLedgers = await loyaltyAccountService.GetLoyaltyAccountPointsAsync(tenantId, account.Id);
            var currentTierQualificationBalance = currentLedgers
                ?.FirstOrDefault(l => l.PointAccountTypeId == tierQualificationType.Id)
                ?.CurrentBalance ?? 0;
            var currentDealerSpendableBalance = currentLedgers
                ?.FirstOrDefault(l => l.PointAccountTypeId == dealerSpendableType.Id)
                ?.CurrentBalance ?? 0;
            
            // Calculate adjustments
            var tierQualificationAmount = await CalculateTierQualificationAmount(
                tenantId, account, currentCampaign, targetCampaign, targetJourneyNode, tierQualificationType.Id);
            
            var dealerSpendableAmount = 0m;
            if (currentJourneyNode != null)
            {
                dealerSpendableAmount = await CalculateDealerSpendableAmount(
                    tenantId, account.Id, currentJourneyNode, targetJourneyNode, dealerSpendableType.Id, cancellationToken);
            }
            
            // Validate
            var validationErrors = ValidateTierMove(
                account, currentCampaign, targetCampaign, currentJourneyNode, targetJourneyNode,
                tierQualificationAmount, dealerSpendableAmount);
            
            return new TierMovePreviewResponse
            {
                TierQualificationAmount = tierQualificationAmount,
                DealerSpendableAmount = dealerSpendableAmount,
                CurrentTierQualificationBalance = currentTierQualificationBalance,
                CurrentDealerSpendableBalance = currentDealerSpendableBalance,
                ProjectedTierQualificationBalance = currentTierQualificationBalance + tierQualificationAmount,
                ProjectedDealerSpendableBalance = currentDealerSpendableBalance + dealerSpendableAmount,
                IsTierDemotion = validationErrors.Any(e => e.Contains("demotion", StringComparison.OrdinalIgnoreCase)),
                IsSameJourney = validationErrors.Any(e => e.Contains("already in", StringComparison.OrdinalIgnoreCase)),
                HasNegativeAdjustment = validationErrors.Any(e => e.Contains("negative", StringComparison.OrdinalIgnoreCase)),
                ValidationErrors = validationErrors
            };
        }

        public async Task<MoveTierResponse> MoveTierAsync(
            string tenantId, 
            MoveTierRequest request, 
            CancellationToken cancellationToken)
        {
            // Validate request
            if (request == null || string.IsNullOrEmpty(tenantId))
                throw new APIErrorsException(new Dictionary<string, string> { { "INVALID", "Invalid request or tenant ID." } });
            
            if (string.IsNullOrEmpty(request.TargetCampaignId) || string.IsNullOrEmpty(request.TargetJourneyId))
                throw new APIErrorsException(new Dictionary<string, string> { { "INVALID", "Target campaign and journey IDs are required." } });
            
            if (string.IsNullOrEmpty(request.Comment) || request.Comment.Length < 3)
                throw new APIErrorsException(new Dictionary<string, string> { { "INVALID", "Comment is required and must be at least 3 characters." } });
            
            // Fetch account
            LoyaltyAccountDto account = null;
            if (!string.IsNullOrEmpty(request.LoyaltyAccountId))
            {
                account = await loyaltyAccountService.GetLoyaltyAccountAsync(tenantId, request.LoyaltyAccountId);
            }
            else if (!string.IsNullOrEmpty(request.LoyaltyAccountXReference))
            {
                account = await loyaltyAccountService.GetLoyaltyAccountByExtIdAsync(tenantId, request.LoyaltyAccountXReference);
            }
            
            if (account == null)
                throw new APIErrorsException(new Dictionary<string, string> { { "NOTFOUND", "Loyalty account not found." } });
            
            // Fetch campaigns
            var targetCampaignTask = _campaignService.FetchCampaignAsync(tenantId, request.TargetCampaignId, CampaignStatusStrings.Live);
            await targetCampaignTask;
            var targetCampaign = targetCampaignTask.Result;
            
            if (targetCampaign == null)
                throw new APIErrorsException(new Dictionary<string, string> { { "NOTFOUND", $"Target campaign {request.TargetCampaignId} not found." } });
            
            if (targetCampaign.Journey == null)
                throw new APIErrorsException(new Dictionary<string, string> { { "INVALID", $"Target campaign {request.TargetCampaignId} does not contain a journey." } });
            
            var targetRootJourney = targetCampaign.Journey.FromDto();
            var targetJourneyNode = FindNode(targetRootJourney, request.TargetJourneyId);
            if (targetJourneyNode == null)
                throw new APIErrorsException(new Dictionary<string, string> { { "NOTFOUND", $"Target journey {request.TargetJourneyId} not found in campaign." } });
            
            // Find current campaign
            CampaignDto currentCampaign = null;
            JourneyNode currentJourneyNode = null;
            string currentJourneyId = null;
            if (account.Journeys != null && account.Journeys.Any())
            {
                var currentRootJourneyId = account.Journeys.FirstOrDefault()?.RootJourneyNodeId;
                if (!string.IsNullOrEmpty(currentRootJourneyId))
                {
                    var allCampaigns = await _campaignService.GetCampaignsByStatusAsync(tenantId, CampaignStatusStrings.Live, 100, null);
                    currentCampaign = allCampaigns?.Entities?.FirstOrDefault(c => 
                        c.Journey?.Id?.Equals(currentRootJourneyId, StringComparison.OrdinalIgnoreCase) == true);
                    
                    if (currentCampaign != null && currentCampaign.Journey != null)
                    {
                        var currentRootJourney = currentCampaign.Journey.FromDto();
                        var currentJourneyIds = account.Journeys.FirstOrDefault(j => 
                            j.RootJourneyNodeId.Equals(currentRootJourneyId, StringComparison.OrdinalIgnoreCase))?.JourneyNodeIds;
                        if (currentJourneyIds != null && currentJourneyIds.Any())
                        {
                            currentJourneyId = currentJourneyIds.Last();
                            currentJourneyNode = FindNode(currentRootJourney, currentJourneyId);
                        }
                    }
                }
            }
            
            // Get point account types
            var pointAccountTypes = await LoyaltyAccountService.GetPointAccountTypes(tenantId);
            var dealerSpendableType = pointAccountTypes?.FirstOrDefault(x => x.IsSpendable == true);
            var tierQualificationType = pointAccountTypes?.FirstOrDefault(x => 
                x.Id == "9c61de1a-460d-4172-8781-acc04c783880");
            
            if (dealerSpendableType == null || tierQualificationType == null)
                throw new APIErrorsException(new Dictionary<string, string> { { "CONFIG", "Required point account types not found." } });
            
            // Calculate adjustments
            var tierQualificationAmount = await CalculateTierQualificationAmount(
                tenantId, account, currentCampaign, targetCampaign, targetJourneyNode, tierQualificationType.Id);
            
            var dealerSpendableAmount = 0m;
            if (currentJourneyNode != null)
            {
                dealerSpendableAmount = await CalculateDealerSpendableAmount(
                    tenantId, account.Id, currentJourneyNode, targetJourneyNode, dealerSpendableType.Id, cancellationToken);
            }
            
            // Validate
            var validationErrors = ValidateTierMove(
                account, currentCampaign, targetCampaign, currentJourneyNode, targetJourneyNode,
                tierQualificationAmount, dealerSpendableAmount);
            
            if (validationErrors.Any())
            {
                throw new APIErrorsException(validationErrors.ToDictionary(e => "VALIDATION", e => e));
            }
            
            // Format comment
            var formattedComment = $"Journey_mgmt: {request.Comment}";
            
            // Set expiration date (1 year from now)
            var expirationDate = DateTimeOffset.UtcNow.AddYears(1);
            
            // Deposit tier qualification points
            if (tierQualificationAmount > 0)
            {
                await loyaltyAccountService.DepositPointsAsync(tenantId, new DTO.Requests.PointDespositRequest
                {
                    LoyaltyAccountId = account.Id,
                    PointAccountTypeId = tierQualificationType.Id,
                    Amount = tierQualificationAmount,
                    EventId = Guid.NewGuid().ToString(),
                    EventType = "Journey_mgmt",
                    UserId = request.AdminUserId,
                    DepositDate = DateTimeOffset.UtcNow
                });
            }
            
            // Deposit dealer spendable points
            if (dealerSpendableAmount > 0)
            {
                await loyaltyAccountService.DepositPointsAsync(tenantId, new DTO.Requests.PointDespositRequest
                {
                    LoyaltyAccountId = account.Id,
                    PointAccountTypeId = dealerSpendableType.Id,
                    Amount = dealerSpendableAmount,
                    EventId = Guid.NewGuid().ToString(),
                    EventType = "Journey_mgmt",
                    UserId = request.AdminUserId,
                    DepositDate = DateTimeOffset.UtcNow
                });
            }
            
            // Exit current journey (if exists) - NO DELAY
            if (currentCampaign != null && !string.IsNullOrEmpty(currentJourneyId))
            {
                await ManuallyExitTier(tenantId, currentCampaign.Id, currentJourneyId, 
                    request.LoyaltyAccountXReference ?? account.KnownExternalIds?.FirstOrDefault() ?? account.Id, 
                    cancellationToken);
            }
            
            // Enter new journey
            await ManuallyEnterTier(tenantId, request.TargetCampaignId, request.TargetJourneyId,
                request.LoyaltyAccountXReference ?? account.KnownExternalIds?.FirstOrDefault() ?? account.Id,
                cancellationToken);
            
            // Fetch updated account
            var updatedAccount = await loyaltyAccountService.GetLoyaltyAccountAsync(tenantId, account.Id);
            
            return new MoveTierResponse
            {
                LoyaltyAccount = updatedAccount,
                TierQualificationAmountDeposited = tierQualificationAmount,
                DealerSpendableAmountDeposited = dealerSpendableAmount,
                PreviousCampaignId = currentCampaign?.Id,
                PreviousJourneyId = currentJourneyId,
                NewCampaignId = targetCampaign.Id,
                NewJourneyId = targetJourneyNode.Id
            };
        }

        #endregion

    }
}
