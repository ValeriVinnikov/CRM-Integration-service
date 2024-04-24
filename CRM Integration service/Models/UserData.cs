using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM_Integration_service.Models
{
  public partial class UserData
  {
    [JsonProperty("loginName")]
    public string LoginName { get; set; }

    [JsonProperty("userName")]
    public string UserName { get; set; }

    [JsonProperty("extension")]
    public string Extension { get; set; }
  }
}
