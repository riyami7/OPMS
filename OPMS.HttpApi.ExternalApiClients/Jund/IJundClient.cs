using OPMS.HttpApi.ExternalApiClients.Jund.Dto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace OPMS.HttpApi.ExternalApiClients.Jund
{
    public interface IJundClient 
    {
        Task<Employee> GetEmployeeAsync(string serviceMilitaryId);
        Task<ExtendedPagedListResultDto<UnitTreeDto>> GetModUnitsAsync(int pageNumber = 1, int pageSize = 20);
    }
}
