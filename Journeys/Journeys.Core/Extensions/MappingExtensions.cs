using Journeys.Core.Models;
using Journeys.Core.RulesEngine;
using Journeys.Core.Utility;
using Journeys.Core.RulesEngine.Journey;
using Journeys.Core.RulesEngine.Journey.Enums;
using Journeys.Core.Services;
using Journeys.DTO.Models;
using System.Text;
using static System.Net.Mime.MediaTypeNames;

namespace Journeys.Core.Extensions
{
    public static class MappingExtensions
    {
        #region loyalty account level mappings

        public static LoyaltyAccountDto? ToDto(this LoyaltyAccount account) =>
            account == null ? null : new LoyaltyAccountDto
            {
                ExtAccountId = account.ExtAccountId,
                Type = account.Type,
                ResettleASAP = account.ResettleASAP, //one way
                KnownExternalIds = account.KnownExternalIds,
                Tags = account.Tags?.Select(t => t.ToDto())?.ToList(),
                Journeys = account.Journeys?.Select(j => j.ToDto())?.ToList(),
                TenantId = account.TenantId,
                Id = account.Id,
                RuleState = account.RuleState?.ToDictionary(k => k.Key, v => new HistoricalRuleStateDto { Count = v.Value.Count, Value = v.Value.Value, FirstOccurrence = v.Value.FirstOccurrence })
            };

        public static LoyaltyAccount? FromDto(this LoyaltyAccountDto accountDto) =>
            accountDto == null ? null : new LoyaltyAccount(
                accountDto.ExtAccountId,
                accountDto.Type,
                string.Empty, //accountDto.Status,
                null, //lockLeaseKey
                null, //LockLeaseExpiration
                accountDto.Tags?.Select(t => t.FromDto()).ToList(),
                accountDto.Journeys?.Select(j => j.FromDto()).ToList(),
                accountDto.KnownExternalIds,
                accountDto.TenantId,
                accountDto.Id
            )
            {
                RuleState = accountDto.RuleState?.ToDictionary(k => k.Key, v => new HistoricalRuleState { Count = v.Value.Count, Value = v.Value.Value, FirstOccurrence = v.Value.FirstOccurrence }) ?? new Dictionary<string, HistoricalRuleState>()
            };

        public static TagDto? ToDto(this Tag tag) =>
            tag == null ? null : new TagDto
            {
                Name = tag.Name,
                EffectiveEndDate = tag.EffectiveEndDate,
                EffectiveEndTime = tag.EffectiveEndTime,
                EffectiveStartTime = tag.EffectiveStartTime,
                EntityId = tag.EntityId,
                EffectiveStartDate = tag.EffectiveStartDate,
                Type = tag.Type,
                Value = tag.Value,
                TtlSec = tag.TtlSec,
                Id = tag.Id,
                TenantId = tag.TenantId
            };

        public static Tag? FromDto(this TagDto tagDto) =>
            tagDto == null ? null : new Tag(
                tagDto.Type,
                tagDto.EntityId,
                tagDto.Name,
                tagDto.Value,
                tagDto.EffectiveStartDate,
                tagDto.EffectiveEndDate,
                tagDto.EffectiveStartTime,
                tagDto.EffectiveEndTime,
                tagDto.TtlSec,
                tagDto.TenantId,
                tagDto.Id
            );

        public static LoyaltyAccountJourneyDto? ToDto(this LoyaltyAccountJourney loyaltyAccountJourney) =>
            loyaltyAccountJourney == null ? null : new LoyaltyAccountJourneyDto
            {
                JourneyNodeIds = loyaltyAccountJourney.JourneyNodeIds,
                RootJourneyNodeId = loyaltyAccountJourney.RootJourneyNodeId
            };

        public static LoyaltyAccountJourney? FromDto(this LoyaltyAccountJourneyDto loyaltyAccountJourneyDto) =>
            loyaltyAccountJourneyDto == null ? null : new LoyaltyAccountJourney(
                loyaltyAccountJourneyDto.RootJourneyNodeId,
                loyaltyAccountJourneyDto.JourneyNodeIds,
                null //no id
            );

        // LoyaltyAccount Point Ledger mapping
        public static PointLedgerDto ToDto(this PointLedger ledger, bool capilatizecapitalizeType = false) =>
            ledger == null ? null : new PointLedgerDto
            {
                Id = ledger.Id,
                TenantId = ledger.TenantId,
                ModelId = ledger.ModelId,
                PointAccountTypeId = ledger.PointAccountTypeId,
                CurrentBalance = ledger.CurrentBalance,
                LifetimeTotal = ledger.LifetimeTotal,
                PageNumber = ledger.PageNumber,
                AccountId = ledger.AccountId,
                LedgerEntries = ledger.Entries?.Values?.SelectMany(e => e.Select(y => y.ToDto(capilatizecapitalizeType)))?.ToList()
            };

        public static PointLedger FromDto(this PointLedgerDto ledgerDto) =>
            ledgerDto == null ? null : new PointLedger(
                ledgerDto.AccountId,
                ledgerDto.PointAccountTypeId,
                ledgerDto.CurrentBalance,
                ledgerDto.LifetimeTotal,
                ledgerDto.PageNumber,
                //ledgerDto.LedgerEntries?.Select(e => e.FromDto())?.ToList(),
                ledgerDto.LedgerEntries
                    ?.Where(e => !string.IsNullOrEmpty(e?.EventType) && !string.IsNullOrEmpty(e?.EventId))
                    ?.GroupBy(e => EventKeyUtility.ToEventKey(e.EventType, e.EventId))
                    ?.ToDictionary(
                        group => group.Key,
                        group => group.Select(g => g.FromDto()).ToList()
                    ),
                ledgerDto.TenantId,
                ledgerDto.Id
            );

        // LoyaltyAccount Point Ledger Entry mapping
        public static LedgerEntryDto ToDto(this LedgerEntry entry, bool capilatizecapitalizeType = false) =>
            entry == null ? null : new LedgerEntryDto
            {
                Type = entry.Type,
                EntryId = entry.EntryId,
                EventType = (capilatizecapitalizeType) ? char.ToUpper(entry.EventType[0]) + entry.EventType.Substring(1) : entry.EventType,
                EventId = entry.EventId,
                OutcomeId = entry.OutcomeId,
                BurnDate = entry.BurnDate,
                EarnDate = entry.EarnDate,
                ExpirationDate = entry.ExpirationDate,
                PointsDeposited = entry.PointsDeposited,
                PointsWithdrawn = entry.PointsWithdrawn,
                SpendablePoints = entry.SpendablePoints,
                SpendReferences = entry.SpendReferences,
                Status = entry.Status,
                UserId = entry.UserId
            };

        public static LedgerEntry FromDto(this LedgerEntryDto entryDto) =>
            entryDto == null ? null : new LedgerEntry(
                entryDto.Type,
                entryDto.EntryId,
                entryDto.EventType,
                entryDto.EventId,
                entryDto.OutcomeId,
                entryDto.PointsDeposited,
                entryDto.PointsWithdrawn,
                entryDto.SpendablePoints,
                entryDto.ExpirationDate,
                entryDto.EarnDate,
                entryDto.BurnDate,
                entryDto.SpendReferences,
                entryDto.Status,
                entryDto.UserId
            );

        public static PointAccountTypeDto ToDto(this PointAccountType acctType) =>
            acctType == null ? null : new PointAccountTypeDto
            {
                ExtAccountId = acctType.ExtAccountId,
                Name = acctType.Name,
                Status = acctType.Status,
                PointSourceId = acctType.PointSourceId,
                LedgerType = acctType.LedgerType,
                PointsLifespanDays = acctType.PointsLifespanDays,
                PointsLifespanEndDate = acctType.PointsLifespanEndDate,
                ExpiresToPointAccountTypeId = acctType.ExpiresToPointAccountTypeId,
                IsSpendable = acctType.IsSpendable,
                Id = acctType.Id,
                TenantId = acctType.TenantId,
                RoundingOptionString = acctType.RoundingOption.ToString(),
                RoundingDecimalPlaces = acctType.RoundingDecimalPlaces
            };

        public static PointAccountType FromDto(this PointAccountTypeDto dto) =>
            dto == null ? null : new PointAccountType(
                dto.ExtAccountId,
                dto.Status,
                dto.Name,
                dto.PointSourceId,
                dto.LedgerType,
                dto.PointsLifespanDays,
                dto.PointsLifespanEndDate,
                dto.ExpiresToPointAccountTypeId,
                dto.IsSpendable,
                dto.RoundingOptionString,
                dto.RoundingDecimalPlaces,
                dto.TenantId,
                dto.Id
            );

        public static PointsDetailsDto ToDto(this PointsDetails details) =>
            details == null ? null : new PointsDetailsDto
            {

                Id = details.Id,
                TenantId = details.TenantId,
                LoyaltyAccountId = details.LoyaltyAccountId,
                EventId = details.EventId,
                EventType = details.EventType,
                Name = details.Name,
                Description = details.Description,
                Quantity = details.Quantity,
                //Etag = details.ETag
            };

        public static PointsDetails FromDto(this PointsDetailsDto dto) =>
            dto == null ? null : new PointsDetails(
                dto.LoyaltyAccountId,
                dto.EventId,
                dto.EventType,
                dto.Name,
                dto.Description,
                dto.Quantity,
                dto.TenantId,
                dto.Id
            );

        #endregion

        #region Campaign level mapping

        // Campaign mapping
        public static CampaignDto? ToDto(this Campaign campaign) =>
            campaign == null ? null : new CampaignDto
            {
                ExtCampaignId = campaign.ExtCampaignId,
                Name = campaign.Name,
                Status = campaign.Status,
                Events = campaign.Events,
                StartDate = campaign.StartDate,
                EndDate = campaign.EndDate,
                TenantId = campaign.TenantId,
                Segments = campaign.Segments?.Select(s => s.ToDto())?.ToList(),
                Journey = campaign.Journey?.ToDto(),
                Id = campaign.Id,
                DeployedDate = campaign.DeployedDate,
                ArchivedDate = campaign.ArchivedDate
            };

        public static Campaign? FromDto(this CampaignDto campaignDto) =>
            campaignDto == null ? null : new Campaign(
                campaignDto.ExtCampaignId,
                campaignDto.Status,
                campaignDto.Name,
                campaignDto.Events,
                campaignDto.StartDate,
                campaignDto.EndDate,
                campaignDto.Segments?.Select(s => s.FromDto())?.ToList() ?? new List<Segment>(),
                campaignDto.Journey?.FromDto(),
                campaignDto.TenantId,
                campaignDto.Id,
                campaignDto.DeployedDate,
                campaignDto.ArchivedDate
            );

        // Segment mapping
        public static SegmentDto? ToDto(this Segment segment) =>
            segment == null ? null : new SegmentDto
            {
                ExtSegmentId = segment.ExtSegmentId,
                Name = segment.Name,
                FileCreateDate = segment.FileCreateDate,
                Status = segment.Status,
                TargetFile = segment.TargetFile,
                TargetFolder = segment.TargetFolder,
                Type = segment.Type,
                Source = segment.Source?.ToDto(),
                Schedule = segment.Schedule?.ToDto()
            };

        public static Segment? FromDto(this SegmentDto segmentDto) =>
            segmentDto == null ? null : new Segment(
                segmentDto.ExtSegmentId,
                segmentDto.Name,
                segmentDto.Status,
                segmentDto.Type,
                segmentDto.TargetFolder,
                segmentDto.TargetFile,
                segmentDto.FileCreateDate,
                segmentDto.Source?.FromDto(),
                segmentDto.Schedule?.FromDto(),
                segmentDto.Id
            );

        // SegmentSource mapping
        public static SegmentSourceDto? ToDto(this SegmentSource segmentSource) =>
            segmentSource == null ? null : new SegmentSourceDto
            {
                ApiUrl = segmentSource.ApiUrl,
                LastRunDate = segmentSource.LastRunDate,
                LastRunDuration = segmentSource.LastRunDuration,
                Name = segmentSource.Name,
                Query = segmentSource.Query,
                SourceFile = segmentSource.SourceFile,
                SourceFolder = segmentSource.SourceFolder,
                Status = segmentSource.Status,
                Type = segmentSource.Type
            };

        public static SegmentSource? FromDto(this SegmentSourceDto segmentSourceDto) =>
            segmentSourceDto == null ? null : new SegmentSource(
                segmentSourceDto.Name,
                segmentSourceDto.Status,
                segmentSourceDto.Type,
                segmentSourceDto.SourceFile,
                segmentSourceDto.SourceFolder,
                segmentSourceDto.ApiUrl,
                segmentSourceDto.Query,
                segmentSourceDto.LastRunDate,
                segmentSourceDto.LastRunDuration
            );

        // Schedule mapping
        public static ScheduleDto? ToDto(this Schedule schedule) =>
            schedule == null ? null : new ScheduleDto
            {
                Frequency = schedule.Frequency,
                FrequencyUnit = schedule.FrequencyUnit,
                LastRunDate = schedule.LastRunDate,
                LastRunDuration = schedule.LastRunDuration,
                Name = schedule.Name,
                NextRunDate = schedule.NextRunDate,
                StartDate = schedule.StartDate,
                Status = schedule.Status,
                Type = schedule.Type
            };

        public static Schedule? FromDto(this ScheduleDto scheduleDto) =>
            scheduleDto == null ? null : new Schedule(
                scheduleDto.Name,
                scheduleDto.Status,
                scheduleDto.Type,
                scheduleDto.StartDate,
                scheduleDto.EndDate,
                scheduleDto.Frequency,
                scheduleDto.FrequencyUnit,
                scheduleDto.LastRunDate,
                scheduleDto.LastRunDuration,
                scheduleDto.NextRunDate,
                scheduleDto.Id
            );

        // Journey mapping
        public static JourneyDto? ToDto(this JourneyNode journey) =>
            journey == null ? null : new JourneyDto
            {
                Name = journey.Name,
                Rules = journey.Rules.Select(r => r.ToDto()).ToList(),
                Id = journey.Id,
                RootNodeId = journey.RootNodeId,
                Navigation = journey.Navigation,
                Children = journey.Children?.Select(j => j.ToDto())?.ToList(),
            };

        public static JourneyNode? FromDto(this JourneyDto journeyDto)
        {
            if (journeyDto == null)
                return null;
            var node = MapJourneyDtoToNode(journeyDto);
            node.FillMissingRootNodeIds();
            return node;
        }

        private static JourneyNode MapJourneyDtoToNode(JourneyDto journeyDto) =>
            new JourneyNode(
                name: journeyDto.Name,
                rules: journeyDto.Rules?.Select(r => r.FromDto())?.ToList(),
                id: journeyDto.Id,
                rootNodeId: journeyDto.RootNodeId,
                navigation: journeyDto.Navigation,
                children: journeyDto.Children?.Select(MapJourneyDtoToNode)?.ToList());

        public static NavigationTypeDto ToDto(this NavigationType modelEnum)
        {
            return (NavigationTypeDto)modelEnum;
        }

        public static NavigationType FromDto(this NavigationTypeDto dtoEnum)
        {
            return (NavigationType)dtoEnum;
        }

        // RuleSet mapping
        public static RuleSetDto? ToDto(this RuleSet ruleset) =>
            ruleset == null ? null : new RuleSetDto
            {
                Name = ruleset.Name,
                RootRuleDiscriminator = ruleset.RootRuleDiscriminator,
                RuleJsonElement = ruleset.RuleJsonElement,
                OutcomesJsonElement = ruleset.OutcomesJsonElement,
                Id = ruleset.Id
            };

        public static RuleSet? FromDto(this RuleSetDto rulesetDto) =>
            rulesetDto == null ? null : new RuleSet(
                rulesetDto.Name,
                rulesetDto.RuleJsonElement,
                rulesetDto.RootRuleDiscriminator,
                rulesetDto.OutcomesJsonElement,
                rulesetDto.Id
            );

        #endregion

        // AdminAudit mapping
        public static AdminAuditDto? ToDto(this AdminAudit audit)
        {
            var jsonBytes = Convert.FromBase64String(audit.ActionJSON);
            var jsonString = Encoding.UTF8.GetString(jsonBytes);
            return audit == null ? null : new AdminAuditDto
            {
                Action = audit.Action,
                ActionJSON = jsonString,
                ActionPrettyPrint = audit.ActionPrettyPrint,
                ActionType = audit.ActionType,
                AdminUserId = audit.AdminUserId,
                Comment = audit.Comment,
                LoyaltyMemberId = audit.LoyaltyMemberId,
                TimeOfOccurrence = audit.TimeOfOccurrence,
                //TimeOfOccurrenceYYYYMM = audit.TimeOfOccurrenceYYYYMM,
                Id = audit.Id,
                TenantId = audit.TenantId
            };
        }

        public static AdminAudit? FromDto(this AdminAuditDto auditDto)
        {
            var base64String = Convert.ToBase64String(Encoding.UTF8.GetBytes(auditDto.ActionJSON));
            return auditDto == null ? null : new AdminAudit(
                auditDto.Action,
                auditDto.ActionType,
                auditDto.LoyaltyMemberId,
                auditDto.AdminUserId,
                auditDto.TimeOfOccurrence,
                auditDto.Comment,
                base64String,
                auditDto.ActionPrettyPrint,
                auditDto.TenantId,
                auditDto.Id
            );
        }

        //External Reference
        public static ExternalReferenceDto? ToDto(this ExternalReference xref) =>
            xref == null ? null : new ExternalReferenceDto
            {
                MapFromId = xref.MapFromId,
                MapToId = xref.MapToId,
                Status = xref.Status,
                Type = xref.Type,
                Id = xref.Id,
                TenantId = xref.TenantId,
                Etag = xref.ETag
            };

        public static ExternalReference? FromDto(this ExternalReferenceDto xref) =>
            xref == null ? null : new ExternalReference(xref.Type, xref.Status, xref.MapFromId, xref.MapToId, xref.TenantId, xref.Id, null);


        // DashboardRole mapping
        public static DashboardRoleDto? ToDto(this DashboardRole dashboardRole) =>
             dashboardRole == null ? null : new DashboardRoleDto
             {
                 RoleId = dashboardRole.RoleId,
                 DashboardId = dashboardRole.DashboardId,
                 CreateDate = dashboardRole.CreateDate ?? DateTimeOffset.UtcNow,
                 LastUpdated = dashboardRole.LastUpdated ?? DateTimeOffset.UtcNow,
                 Id = dashboardRole.Id,
                 TenantId= dashboardRole.TenantId
             };
        public static DashboardRole? FromDto(this DashboardRoleDto dashboardRoleDto) =>
            dashboardRoleDto == null ? null : new DashboardRole(
                dashboardRoleDto.RoleId,        // roleId
                dashboardRoleDto.DashboardId,   // dashboardId
                dashboardRoleDto.TenantId,            // id
                dashboardRoleDto.Id,
                dashboardRoleDto.CreateDate,    // createDate
                dashboardRoleDto.LastUpdated    // lastUpdated
            );

        public static FileIngestionSummaryDto? ToDto(this FileIngestionSummary model) =>
           model == null ? null : new FileIngestionSummaryDto
           {
               Id = model.Id,
               TenantId = model.TenantId,
               FileName = model.FileName,
               DirectoryLoaded = model.DirectoryLoaded,
               TotalRows = model.TotalRows,
               TotalSuccess = model.TotalSuccess,
               TotalErrors = model.TotalErrors,
               DateFileDropped = model.DateFileDropped,
               DateFileProcessed = model.DateFileProcessed,
               CompletionTime = model.CompletionTime,
               FileSize = model.FileSize,
               Source = model.Source,
               ReferenceId = model.ReferenceId,
               FileType = model.FileType,
               Status = model.Status,
               Checked = model.Checked
           };

        public static FileIngestionSummary? FromDto(this FileIngestionSummaryDto model) =>
        model == null ? null : new FileIngestionSummary(
            model.TenantId,
            model.Id,
            model.FileName,
            model.DirectoryLoaded,
            model.TotalRows,
            model.TotalSuccess,
            model.TotalErrors,
            model.DateFileDropped,
            model.DateFileProcessed,
            model.CompletionTime,
            model.FileSize,
            model.Source,
            model.ReferenceId,
            model.FileType,
            model.Status,
            model.Checked
      );

        // Warehouse Config mapping
        public static WarehouseConfigDto? ToDto(this WarehouseConfig warehouserConfig) =>
             warehouserConfig == null ? null : new WarehouseConfigDto
             {
                 ServicePrincipalName = warehouserConfig.ServicePrincipalName,
                 KeyVaultSecretAppId = warehouserConfig.KeyVaultSecretAppId,
                 KeyVaultSecretPassword = warehouserConfig.KeyVaultSecretPassword,
                 SchemaFullName = warehouserConfig.SchemaFullName,
                 WorkSpaceFolder = warehouserConfig.WorkSpaceFolder,
                 TenantId = warehouserConfig.TenantId,
                 Id = warehouserConfig.Id
             };
        public static WarehouseConfig? FromDto(this WarehouseConfigDto warehouseConfigDto) =>
            warehouseConfigDto == null ? null : new WarehouseConfig(
                warehouseConfigDto.ServicePrincipalName,
                warehouseConfigDto.KeyVaultSecretAppId, 
                warehouseConfigDto.KeyVaultSecretPassword,
                warehouseConfigDto.SchemaFullName,
                warehouseConfigDto.WorkSpaceFolder,
                warehouseConfigDto.TenantId,       
                warehouseConfigDto.Id
            );

    }

}
