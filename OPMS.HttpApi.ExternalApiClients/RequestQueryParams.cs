using Refit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OPMS.HttpApi.ExternalApiClients
{
    public class RequestQueryParams
    {
        [AliasAs("offset")]
        public int Offset { get; set; }
    }
}
