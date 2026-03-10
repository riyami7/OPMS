namespace OperationalPlanMS.Models.ExternalApi
{
    /// <summary>
    /// الوحدة التنظيمية من API الخارجي (HR System)
    /// </summary>
    public class ExternalOrganizationalUnit
    {
        public int Id { get; set; }
        public int? ParentId { get; set; }
        public int TenentId { get; set; }
        public string ArabicUnitName { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string? ArabicName { get; set; }
    }

    /// <summary>
    /// الموظف من API الخارجي (HR System)
    /// </summary>
    public class ExternalEmployee
    {
        public string EmpNumber { get; set; } = string.Empty;
        public string? Rank { get; set; }
        public string EmpName { get; set; } = string.Empty;
        public string? Position { get; set; }
        public string? CurrentUnit { get; set; }
        public string? BranchName { get; set; }
    }

    /// <summary>
    /// Response wrapper للـ API (ABP Framework style)
    /// </summary>
    public class ApiResponse<T>
    {
        public List<T> Items { get; set; } = new();
        public int TotalCount { get; set; }
    }

    /// <summary>
    /// DTO للعرض في Dropdown
    /// </summary>
    public class OrganizationalUnitDto
    {
        public int Id { get; set; }
        public int? ParentId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public int Level { get; set; }
        public List<OrganizationalUnitDto> Children { get; set; } = new();
    }

    /// <summary>
    /// DTO للموظف للعرض في Dropdown
    /// </summary>
    public class EmployeeDto
    {
        public string EmpNumber { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Rank { get; set; }
        public string? Position { get; set; }
        public string? Unit { get; set; }
        public string DisplayName => $"{Rank} {Name}".Trim();

    }


    



    }
