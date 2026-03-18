using Microsoft.Extensions.Configuration;
using OPMS.HttpApi.ExternalApiClients.Hr;
using OPMS.HttpApi.ExternalApiClients.Hr;
using OPMS.HttpApi.ExternalApiClients.Hr.Dto;
using Refit;
using System.Data;

namespace MOD.OPMS.HttpApi.ExternalApiClients.Hr

{
    public class HrClient : IHrClient
    {
        private IHrApi _hrApi;
        //private readonly OrganizationUnitManager _organizationUnitManager;


        public HrClient(
           HrAuthHeaderHandler authHeaderHandler,
           IConfiguration configuration
           //OrganizationUnitManager organizationUnitManager
           )
        {
            var baseUrl = configuration["ExternalApis:Hr:BaseUrl"];
            //_organizationUnitManager = organizationUnitManager;
            _hrApi = RestService.For<IHrApi>(new HttpClient(authHeaderHandler) {  BaseAddress = new Uri(baseUrl!), });
        }

        public async Task<Employee> GetEmployeeAsync(string serviceId)
        {

            Employee employee = await _hrApi.GetEmployeeAsync(serviceId);

            var photo = await _hrApi.GetEmployeePhotoAsync(serviceId);
            if (photo.Length > 0) employee.Photo = photo;
            return employee;
        }

    
    }
}

