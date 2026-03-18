using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OPMS.HttpApi.ExternalApiClients.Hr.Dto
{
    public class UnitTreeDto
    {
        public Guid Id { get; set; }
        public Guid? ParentId { get; set; }
        public string Code { get; set; }
        public Guid? TenantId { get; set; }
        public string UnitName { get; set; }

    }
}
