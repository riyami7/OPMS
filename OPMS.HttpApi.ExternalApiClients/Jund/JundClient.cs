using Microsoft.Extensions.Configuration;
using OPMS.HttpApi.ExternalApiClients.Jund;
using OPMS.HttpApi.ExternalApiClients.Jund.Dto;
using Refit;
using System.Data;

namespace OPMS.HttpApi.ExternalApiClients.Jund
{
    public class JundClient : IJundClient
    {
        private IJundApi _jundApi;
        //private readonly OrganizationUnitManager _organizationUnitManager;


        public JundClient(
           JundAuthHeaderHandler authHeaderHandler,
           IConfiguration configuration
           //OrganizationUnitManager organizationUnitManager
           )
        {
            var baseUrl = configuration["ExternalApis:Jund:BaseUrl"];
            //_organizationUnitManager = organizationUnitManager;
            _jundApi = RestService.For<IJundApi>(new HttpClient(authHeaderHandler) {  BaseAddress = new Uri(baseUrl!), });
        }

        public async Task<Employee> GetEmployeeAsync(string serviceMilitaryId)
        {

            Employee employee = await _jundApi.GetEmployeeAsync(serviceMilitaryId);

            var photo = await _jundApi.GetEmployeePhotoAsync(serviceMilitaryId);
            if (photo.Length > 0) employee.Photo = photo;
            return employee;
        }

        public async Task <ExtendedPagedListResultDto <UnitTreeDto>>  GetModUnitsAsync (int pageNumber =1, int pageSize =20)
        {
            //ExtendedPagedListResultDto<UnitTreeDto> organizationUnit = await _jundApi.GetModUnitsAsync(pageNumber, pageSize);

            return  new ExtendedPagedListResultDto<UnitTreeDto>();
             
        }
    }
}

