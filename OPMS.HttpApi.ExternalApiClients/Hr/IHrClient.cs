
using OPMS.HttpApi.ExternalApiClients.Hr.Dto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace OPMS.HttpApi.ExternalApiClients.Hr
{
    public interface IHrClient 
    {
        Task<Employee> GetEmployeeAsync(string serviceMilitaryId);
    }
}
