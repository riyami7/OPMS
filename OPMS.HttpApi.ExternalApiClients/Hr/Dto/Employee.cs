using System;

namespace OPMS.HttpApi.ExternalApiClients.Hr.Dto
{
    public class Employee
    {
    
        public string ServiceNumber { get; set; }
        public string RankArabic { get; set; }
        public string CurrentUnitAr { get; set; }
        public string EmpNameAr { get; set; }
        public string PositionAr { get; set; }

        public byte[] Photo { get; set; }

    }
}
