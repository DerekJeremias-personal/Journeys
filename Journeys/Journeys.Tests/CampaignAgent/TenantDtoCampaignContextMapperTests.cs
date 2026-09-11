using Backend.Dto.Structures.Tenant;
using Journeys.API.CampaignAgent;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Journeys.Tests.CampaignAgent;

public class TenantDtoCampaignContextMapperTests
{
    [Fact]
    public void Map_includes_campaignTestAccountExtIds_from_tenant_dto()
    {
        var dto = new TenantDto
        {
            Name = "primo",
            CampaignTestAccountExtIds = ["test_exp_01", "Test_Exp_02"]
        };

        var slice = TenantDtoCampaignContextMapper.Map(dto, 8000, 8000, NullLogger.Instance);

        Assert.NotNull(slice.CampaignTestAccountExtIds);
        Assert.Equal(2, slice.CampaignTestAccountExtIds.Count);
        Assert.Contains("test_exp_01", slice.CampaignTestAccountExtIds, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildCacheVersion_changes_when_allowlist_changes()
    {
        var baseDto = new TenantDto { Name = "primo", UpdatedAt = DateTimeOffset.UtcNow };
        var withIds = new TenantDto
        {
            Name = "primo",
            UpdatedAt = baseDto.UpdatedAt,
            CampaignTestAccountExtIds = ["a"]
        };

        var v1 = TenantDtoCampaignContextMapper.BuildCacheVersion(baseDto);
        var v2 = TenantDtoCampaignContextMapper.BuildCacheVersion(withIds);

        Assert.NotEqual(v1, v2);
    }
}
