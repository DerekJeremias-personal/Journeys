using Journeys.Core.Utility;
using Journeys.DTO.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Journeys.Tests.JSONTests
{
    public class AdminAuditJSON
    {
        private readonly ILogger<AdminAuditJSON> _logger;
        public AdminAuditJSON() {
            _logger = LoggerFactoryProvider.CreateLogger<AdminAuditJSON>();
        }

        [Fact]
        public void DeserializeTest()
        {
            var json = @"{  ""loyaltyMemberId"": ""c17e0671-d4d4-43b1-848d-76489bf04883"",  ""adminUserId"": ""test@bishoplabs.com"",  ""actionType"": ""Point Adjustment"",  ""action"": ""Deposit"",  ""comment"": ""Initial testing of the feature"" }";
            var obj = JsonUtility.Deserialize<AdminAuditDto>(json, _logger);

        }
    }
}
