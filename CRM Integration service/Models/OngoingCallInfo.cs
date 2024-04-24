using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRM_Integration_service.Models
{
    class OngoingCallInfo
    {
        public string CallRef { get; set; }        
        public string LoginName { get; set; }
        public string CallingNumber { get; set; }
        public string CallDiretion { get; set; }
        public DateTime CallStart { get; set; }
        public DateTime CallEnd { get; set; }
    }
}
