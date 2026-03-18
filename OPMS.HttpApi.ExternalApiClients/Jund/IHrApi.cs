using MOD.OPMS.HttpApi.ExternalApiClients.Jund.Dto;
using OPMS.HttpApi.ExternalApiClients.Jund.Dto;
using Refit;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace MOD.OPMS.HttpApi.ExternalApiClients.Jund
{
    public interface IJundApi
    {
        [Get("/person-by-service-id/{serviceId}")]
        Task<Employee> GetEmployeeAsync(string serviceId);

        [Get("/person-photo-by-service-id/{serviceId}")]
        Task<byte[]> GetEmployeePhotoAsync(string serviceId);

        [Get("/units-list")]
        Task<ExtendedPagedListResultDto<UnitTreeDto>> GetModUnitsAsync();
    }

}
