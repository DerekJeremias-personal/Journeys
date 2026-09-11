using Journeys.Core.RulesEngine.Engine;
using Journeys.DTO.Models;
using Journeys.DTO.Requests;
using Journeys.DTO.Responses;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.Core.Interfaces.Services
{
    public interface IRulesService
    {
        Task<RulesServiceResponse> ProcessRulesAsync(RulesServiceRequest request, CancellationToken token);
        Task<RulesServiceResponse> ResettleAccountJourneysAsync(RulesServiceRequest request, CancellationToken token);
        Task<LoyaltyAccountDto> ManuallyEnterTier(string tenantId, string campaignId, string journeyId, string loyaltyAccountXReference, CancellationToken token, bool swapAssignment = false);
        Task<LoyaltyAccountDto> ManuallyExitTier(string tenantId, string campaignId, string journeyId, string loyaltyAccountXReference, CancellationToken token);
        Task InitState(string tenantId, Stream inputStream, Stream outputStream, CancellationToken token, int batchSize = 10);
        Task<TierMovePreviewResponse> PreviewTierMoveAsync(string tenantId, MoveTierRequest request, CancellationToken cancellationToken);
        Task<MoveTierResponse> MoveTierAsync(string tenantId, MoveTierRequest request, CancellationToken cancellationToken);
    }
}
