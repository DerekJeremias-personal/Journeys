using Journeys.Core.Interfaces.DataStorage;
using Journeys.Core.Models;
using Journeys.Core.RulesEngine.Journey;
using Journeys.Core.Utility;
using Journeys.DTO.Exceptions;
using Journeys.DTO.Requests;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.DAL.Adapters
{
    public class CampaignAdapter : ReferenceableBaseAdapter<Campaign>, ICampaignAdapter
    {
        private const string CAMPAIGN_MODEL_ID = "eeae67ca-7bf9-4d2b-9131-83717b219a3a";

        public CampaignAdapter(IDynamicDataAdapter dynAdapter, IExternalReferenceAdapter extReferenceAdapter, ILogger<CampaignAdapter> logger) : base(dynAdapter, extReferenceAdapter, logger)
        {
        }


        public async Task<Campaign> FetchCampaignAsync(string tenantId, string campaignId, string status)
        {
            return await base.FetchEntityAsync(tenantId, campaignId, CAMPAIGN_MODEL_ID, status.ToLower());
        }

        public async Task<PagedResultSet<Campaign>> GetCampaignsByStatusAsync(string tenantId, string status, int pageSize, string? continuationToken = null)
        {
            return await base.FetchEntityByKeyAsync(tenantId, status.ToLower(), CAMPAIGN_MODEL_ID, pageSize, continuationToken);
        }

        public async Task<PagedResultSet<Campaign>> GetAllCampaignsAsync(string tenantId, int pageSize, string continuationToken = null)
        {
            return await base.GetAllEntitiesAsync(tenantId, CAMPAIGN_MODEL_ID, pageSize, continuationToken);
        }

        public async Task<PagedResultSet<Campaign>> GetCampaignsByFiltersAsync(string tenantId, GetCampaignsByFilterRequest req)
        {
            return await base.GetEntitiesByFiltersAsync(tenantId, CAMPAIGN_MODEL_ID, req.Query, req.Parameters, req.SortBy, req.SortOrder, req.PageSize, req.ContinuationToken, true);
        }

        public async Task<List<Campaign>> GetCampaignsAsync(string tenantId, List<string> ids, string status)
        {
            //Campaign entity requires a status indicator as a pk
            List<(string, Dictionary<string, string>)> idpks = ids
                .Select((string i) => (i, new Dictionary<string, string> { { "status", status.ToLower() } }))
                .ToList();
            return await base.GetManyEntitiesAsync(tenantId, CAMPAIGN_MODEL_ID, idpks);
        }

        public async Task<Campaign> UpsertCampaignAsync(string tenantId, Campaign campaign)
        {
            if (campaign == null) return null;

            if (string.IsNullOrWhiteSpace(campaign.Status))
            {
                throw new APIErrorsException(new Dictionary<string, string>
                {
                    { "status", "Status is required." }
                });
            }

            var normalizedStatus = campaign.Status.ToLower();
            campaign.Status = normalizedStatus;

            // Normalize ExtCampaignId
            if (string.IsNullOrWhiteSpace(campaign.ExtCampaignId) && !string.IsNullOrWhiteSpace(campaign.Name))
            {
                campaign.ExtCampaignId = campaign.Name.ToLower();
            }

            // Ensure journey IDs are set
            if (campaign.Journey != null)
            {
                EnsureJourneyIds(campaign.Journey);
                campaign.Journey.FillMissingRootNodeIds();
            }

            // Scenario A: New campaign (no Id)
            if (string.IsNullOrEmpty(campaign.Id))
            {
                campaign.Id = Guid.NewGuid().ToString();
                // New campaigns default to Draft if not specified
                if (normalizedStatus != CampaignStatusStrings.Draft.ToLower() &&
                    normalizedStatus != CampaignStatusStrings.Live.ToLower())
                {
                    campaign.Status = CampaignStatusStrings.Draft.ToLower();
                }

                // If creating as Draft, check if another Draft already exists for this ExtCampaignId
                if (normalizedStatus == CampaignStatusStrings.Draft.ToLower() && !string.IsNullOrWhiteSpace(campaign.ExtCampaignId))
                {
                    var existingDraftForExtId = await GetDraftCampaignByExtIdAsync(tenantId, campaign.ExtCampaignId);
                    if (existingDraftForExtId != null)
                    {
                        throw new APIErrorsException(new Dictionary<string, string>
                        {
                            { "status", $"A Draft version already exists for ExtCampaignId '{campaign.ExtCampaignId}'. Only one Draft is allowed per campaign. Update the existing Draft or delete it first." }
                        });
                    }
                }

                // If trying to create as Live, that's allowed (first deployment)
                if (normalizedStatus == CampaignStatusStrings.Live.ToLower())
                {
                    campaign.DeployedDate = DateTimeOffset.UtcNow;
                }
                return await base.UpsertEntityAsync(tenantId, CAMPAIGN_MODEL_ID, campaign, typeof(Campaign));
            }

            // Existing campaign - check current status
            var existingLive = await FetchCampaignAsync(tenantId, campaign.Id, CampaignStatusStrings.Live);
            var existingDraft = await FetchCampaignAsync(tenantId, campaign.Id, CampaignStatusStrings.Draft);
            var existingArchive = await FetchCampaignAsync(tenantId, campaign.Id, CampaignStatusStrings.Archive);
            var existingPaused = await FetchCampaignAsync(tenantId, campaign.Id, CampaignStatusStrings.Pause);

            Campaign existingCampaign = existingLive ?? existingDraft ?? existingArchive;

            // Scenario E: Invalid operations - Archive is immutable
            if (existingArchive != null)
            {
                throw new APIErrorsException(new Dictionary<string, string>
                {
                    { "status", "Cannot modify an archived campaign. Archived campaigns are immutable." }
                });
            }

            // Validate ExtCampaignId immutability
            if (existingCampaign != null && !string.IsNullOrWhiteSpace(existingCampaign.ExtCampaignId))
            {
                if (campaign.ExtCampaignId != existingCampaign.ExtCampaignId)
                {
                    throw new APIErrorsException(new Dictionary<string, string>
                    {
                        { "extCampaignId", $"ExtCampaignId is immutable. Cannot change from '{existingCampaign.ExtCampaignId}' to '{campaign.ExtCampaignId}'." }
                    });
                }
            }

            // Scenario B: Updating existing Draft (has Id, status = Draft)
            if (normalizedStatus == CampaignStatusStrings.Draft.ToLower())
            {
                if (existingLive != null)
                {
                    // Cannot update Live directly - must create new Draft
                    throw new APIErrorsException(new Dictionary<string, string>
                    {
                        { "status", "Cannot update a Live campaign. To modify a Live campaign, create a new Draft version with a new Id but the same ExtCampaignId." }
                    });
                }

                if (existingDraft != null)
                {
                    // Update existing Draft
                    // Preserve ExtCampaignId from existing
                    campaign.ExtCampaignId = existingDraft.ExtCampaignId;
                    // Preserve CreateDate
                    campaign.CreateDate = existingDraft.CreateDate;
                    return await base.UpsertEntityAsync(tenantId, CAMPAIGN_MODEL_ID, campaign, typeof(Campaign));
                }

                // Creating new Draft (shouldn't happen if Id exists, but handle it)
                return await base.UpsertEntityAsync(tenantId, CAMPAIGN_MODEL_ID, campaign, typeof(Campaign));
            }

            // Scenario C: Deploying Draft to Live (has Id, status = Draft → Live)
            if (normalizedStatus == CampaignStatusStrings.Live.ToLower())
            {
                if (existingLive != null && existingLive.Id == campaign.Id)
                {
                    // Trying to update Live directly
                    // For live campaigns, only start date and end date edits are allowed. So if none of these fields changed, discard Update.
                    if (CompareObjects(existingLive, campaign, new List<string>() { "StartDate", "EndDate" }))
                    {
                        throw new APIErrorsException(new Dictionary<string, string>
                        {
                            { "status", "Cannot update a Live campaign directly. Set status to 'draft' and create a new version to modify." }
                        });
                    }
                }


                // Check if another Draft exists for the same ExtCampaignId (single Draft enforcement)
                if (existingDraft!= null && !string.IsNullOrWhiteSpace(existingDraft.ExtCampaignId))
                {
                    var allDrafts = await GetCampaignsByStatusAsync(tenantId, CampaignStatusStrings.Draft, 1000);
                    var otherDrafts = allDrafts.Entities
                        .Where(c => c.ExtCampaignId == existingDraft.ExtCampaignId && c.Id != campaign.Id)
                        .ToList();

                    if (otherDrafts.Any())
                    {
                        throw new APIErrorsException(new Dictionary<string, string>
                        {
                            { "status", $"Cannot deploy. Another Draft version exists for ExtCampaignId '{existingDraft.ExtCampaignId}'. Only one Draft is allowed per campaign." }
                        });
                    }
                }

                // Check if Live version exists with same ExtCampaignId - archive it
                Campaign liveToArchive = null;
                if (!string.IsNullOrWhiteSpace(existingDraft?.ExtCampaignId))
                {
                    liveToArchive = await GetLiveCampaignByExtIdAsync(tenantId, existingDraft.ExtCampaignId);
                }

                // Archive existing Live version if it exists
                if (liveToArchive != null && liveToArchive.Id != campaign.Id)
                {
                    liveToArchive.ArchivedDate = DateTimeOffset.UtcNow;
                    liveToArchive.Status = CampaignStatusStrings.Live.ToLower(); //retain existing status, the new partition is what matters
                    var archivePartition = new Dictionary<string, string> { { "status", CampaignStatusStrings.Archive.ToLower() } };
                    await base.MoveEntityAsync(tenantId, CAMPAIGN_MODEL_ID, liveToArchive, archivePartition, typeof(Campaign));
                }

                // Move Draft to Live
                if (existingDraft != null)
                {
                    campaign.ExtCampaignId = existingDraft.ExtCampaignId;
                    campaign.CreateDate = existingDraft.CreateDate;
                    campaign.DeployedDate = DateTimeOffset.UtcNow;
                    campaign.ArchivedDate = null; // Clear if it was set
                    campaign.Status = CampaignStatusStrings.Draft.ToLower(); //retain existing status, the new partition is what matters
                    var livePartition = new Dictionary<string, string> { { "status", CampaignStatusStrings.Live.ToLower() } };
                    return await base.MoveEntityAsync(tenantId, CAMPAIGN_MODEL_ID, campaign, livePartition, typeof(Campaign));
                }

                // Move Paused to Live
                if(existingPaused != null)
                {
                    campaign.ExtCampaignId = existingPaused.ExtCampaignId;
                    campaign.CreateDate = existingPaused.CreateDate;
                    campaign.DeployedDate = DateTimeOffset.UtcNow;
                    campaign.ArchivedDate = null; // Clear if it was set
                    campaign.Status = CampaignStatusStrings.Pause.ToLower(); //retain existing status, the new partition is what matters
                    var livePartition = new Dictionary<string, string> { { "status", CampaignStatusStrings.Live.ToLower() } };
                    return await base.MoveEntityAsync(tenantId, CAMPAIGN_MODEL_ID, campaign, livePartition, typeof(Campaign));
                }
            }

            // Scenario D: Creating Draft from Live (has Id of Live campaign) - This should be handled by creating new Id
            // But if someone tries to update Live with Draft status, we handle it here
            if (existingLive != null && normalizedStatus == CampaignStatusStrings.Draft.ToLower())
            {
                throw new APIErrorsException(new Dictionary<string, string>
                {
                    { "status", "Cannot change a Live campaign to Draft. Create a new campaign with a new Id but the same ExtCampaignId to create a Draft version." }
                });
            }

            // Scenario E: Archiving a draft
            if(existingDraft != null && normalizedStatus == CampaignStatusStrings.Archive.ToLower() 
                && existingDraft.Id == campaign.Id)
            {
                existingDraft.ArchivedDate = DateTimeOffset.UtcNow;
                existingDraft.Status = CampaignStatusStrings.Draft.ToLower(); //retain existing status, the new partition is what matters
                var archivePartition = new Dictionary<string, string> { { "status", CampaignStatusStrings.Archive.ToLower() } };
                return await base.MoveEntityAsync(tenantId, CAMPAIGN_MODEL_ID, existingDraft, archivePartition, typeof(Campaign));
            }

            //Scenario F: Pause a live contract
            if (existingLive != null && normalizedStatus == CampaignStatusStrings.Pause.ToLower()
                && existingLive.Id == campaign.Id)
            {
                existingLive.ArchivedDate = DateTimeOffset.UtcNow;
                existingLive.Status = CampaignStatusStrings.Live.ToLower(); //retain existing status, the new partition is what matters
                var archivePartition = new Dictionary<string, string> { { "status", CampaignStatusStrings.Pause.ToLower() } };
                return await base.MoveEntityAsync(tenantId, CAMPAIGN_MODEL_ID, existingLive, archivePartition, typeof(Campaign));

            }


            // Default: just upsert (shouldn't normally reach here)
            return await base.UpsertEntityAsync(tenantId, CAMPAIGN_MODEL_ID, campaign, typeof(Campaign));
        }

        public async Task DeleteCampaignAsync(string tenantId, string campaignId, string status)
        {
            var normalizedStatus = status.ToLower();
            if (!string.Equals(normalizedStatus, CampaignStatusStrings.Draft.ToLower(), StringComparison.Ordinal))
            {
                throw new APIErrorsException(new Dictionary<string, string>
                {
                    { "deleteNotPermitted", "Only Draft campaigns may be deleted. Archive Live campaigns instead." }
                });
            }

            var existing = await FetchCampaignAsync(tenantId, campaignId, CampaignStatusStrings.Draft);
            CampaignDeleteGuard.ValidateCanHardDelete(existing);

            await base.DeleteEntityAsync(
                tenantId,
                CAMPAIGN_MODEL_ID,
                campaignId,
                new Dictionary<string, string>
                {
                    { "status",   status.ToLower() },
                    { "TenantId", tenantId         }
                }
            );
        }

        public async Task DeleteCampaignAsync(string tenantId, Campaign Campaign)
        {
            if (Campaign == null)
            {
                throw new APIErrorsException(new Dictionary<string, string>
                {
                    { "campaignNotFound", "Draft campaign not found." }
                });
            }

            await DeleteCampaignAsync(tenantId, Campaign.Id, Campaign.Status);
        }

        public async Task<List<Campaign>> GetCampaignVersionsByExtIdAsync(string tenantId, string extCampaignId)
        {
            // Query across all statuses to find all versions with the same ExtCampaignId
            var query = "c.extCampaignId = @extCampaignId";
            var parameters = new Dictionary<string, object> { { "@extCampaignId", extCampaignId.ToLower() } };

            var req = new GetCampaignsByFilterRequest
            {
                Query = query,
                Parameters = parameters,
                PageSize = 1000,
                ContinuationToken = null
            };

            var result = await GetCampaignsByFiltersAsync(tenantId, req);
            return result.Entities.ToList();
        }

        public async Task<PagedResultSet<Campaign>> GetArchivedCampaignsByDateRangeAsync(string tenantId, DateTimeOffset? fromDate, DateTimeOffset? toDate, int pageSize, string? continuationToken = null)
        {
            var queryParts = new List<string> { "c.status = @status" };
            var parameters = new Dictionary<string, object> { { "@status", CampaignStatusStrings.Archive.ToLower() } };

            if (fromDate.HasValue)
            {
                queryParts.Add("c.archivedDate >= @fromDate");
                parameters.Add("@fromDate", fromDate.Value);
            }

            if (toDate.HasValue)
            {
                queryParts.Add("c.archivedDate <= @toDate");
                parameters.Add("@toDate", toDate.Value);
            }

            var query = string.Join(" AND ", queryParts);
            var req = new GetCampaignsByFilterRequest
            {
                Query = query,
                Parameters = parameters,
                PageSize = pageSize,
                ContinuationToken = continuationToken,
                SortBy = "archivedDate",
                SortOrder = SortOrder.DESC
            };

            return await GetCampaignsByFiltersAsync(tenantId, req);
        }

        public async Task<Campaign> GetLiveCampaignByExtIdAsync(string tenantId, string extCampaignId)
        {
            var query = " c.extcampaignid = @extCampaignId AND c.status = @status";
            var parameters = new Dictionary<string, object>
            {
                { "@extCampaignId", extCampaignId.ToLower() },
                { "@status", CampaignStatusStrings.Live.ToLower() }
            };

            var req = new GetCampaignsByFilterRequest
            {
                Query = query,
                Parameters = parameters,
                PageSize = 1,
                ContinuationToken = null
            };

            var result = await GetCampaignsByFiltersAsync(tenantId, req);
            return result.Entities.FirstOrDefault();
        }

        public async Task<Campaign> GetDraftCampaignByExtIdAsync(string tenantId, string extCampaignId)
        {
            var query = "c.extCampaignId = @extCampaignId AND c.status = @status";
            var parameters = new Dictionary<string, object>
            {
                { "@extCampaignId", extCampaignId.ToLower() },
                { "@status", CampaignStatusStrings.Draft.ToLower() }
            };

            var req = new GetCampaignsByFilterRequest
            {
                Query = query,
                Parameters = parameters,
                PageSize = 1,
                ContinuationToken = null
            };

            var result = await GetCampaignsByFiltersAsync(tenantId, req);
            return result.Entities.FirstOrDefault();
        }

        private void EnsureJourneyIds(JourneyNode journey)
        {
            journey.Id ??= Guid.NewGuid().ToString();
            foreach (var j in journey.Children ?? new List<JourneyNode>())
            {
                EnsureJourneyIds(j);
            }
        }

        public bool CompareObjects<T>(T obj1, T obj2, List<string> ignoreProps) where T : class
        {
            if (obj1 == null || obj2 == null) return obj1 == obj2;

            // Get all public instance properties
            PropertyInfo[] properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (PropertyInfo prop in properties)
            {
                // Skip the two properties you want to ignore
                if (ignoreProps.Contains(prop.Name))
                    continue;

                object val1 = prop.GetValue(obj1);
                object val2 = prop.GetValue(obj2);

                // Check for value equality
                if (!Equals(val1, val2))
                    return false;
            }

            return true;
        }

    }
}
