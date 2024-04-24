using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM_Integration_service.Models
{
    public partial class JsonData
    {
        [JsonProperty("call_id")]
        public string CallId { get; set; }

        [JsonProperty("dest_tel")]
        public string DestTel { get; set; }

        [JsonProperty("source_key")]
        public string SourceKey { get; set; }

        [JsonProperty("source_tel")]
        public string SourceTel { get; set; }

        [JsonProperty("started_at")]
        public DateTime StartedAt { get; set; }

        [JsonProperty("agent_email")]
        public string AgentEmail { get; set; }

        [JsonProperty("agent_customfield")]
        public AgentCustomfield AgentCustomfield { get; set; }
    }
    public partial class AgentCustomfield
    {
        [JsonProperty("fieldvalue")]
        public string Fieldvalue { get; set; }

        [JsonProperty("customfield")]
        public string Customfield { get; set; }
    }
}
