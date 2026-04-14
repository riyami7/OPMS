using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using OperationalPlanMS.Data;
using OperationalPlanMS.Models.ExternalApi;

// Alias للتفريق بين الـ Entity والـ DTO
using ExternalUnitEntity = OperationalPlanMS.Models.Entities.ExternalOrganizationalUnit;

namespace OperationalPlanMS.Services
{
    /// <summary>
    /// خدمة التعامل مع API نظام الموارد البشرية الخارجي
    /// </summary>
    public interface IExternalApiService
    {
        // جلب من API الخارجي
        Task<List<ExternalOrganizationalUnitApi>> GetAllUnitsFromApiAsync();
        Task<List<ExternalEmployee>> GetAllEmployeesAsync();
        Task<List<EmployeeDto>> SearchEmployeesAsync(string searchTerm);

        // مزامنة مع قاعدة البيانات المحلية
        Task<SyncResult> SyncOrganizationalUnitsAsync();

        // جلب من قاعدة البيانات المحلية
        Task<List<OrganizationalUnitDto>> GetLocalUnitsAsync();
        Task<List<OrganizationalUnitDto>> GetLocalChildUnitsAsync(Guid parentId);

        // جلب موظف برقم الموظف
        Task<EmployeeDto?> GetEmployeeByNumberAsync(string empNumber);
        Task<byte[]?> GetEmployeePhotoAsync(string empNumber);


        void ClearCache();
    }

    /// <summary>
    /// نتيجة المزامنة
    /// </summary>
    public class SyncResult
    {
        public bool Success { get; set; }
        public int AddedCount { get; set; }
        public int UpdatedCount { get; set; }
        public int TotalCount { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime SyncedAt { get; set; } = DateTime.Now;
    }

    public class ExternalApiService : IExternalApiService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _cache;
        private readonly ILogger<ExternalApiService> _logger;
        private readonly AppDbContext _db;

        // OAuth2 Settings
        private readonly string _authServer;
        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _scope;

        // API Settings
        private readonly string _baseUrl;
        private readonly string _tenantId;

        // Cache keys
        private const string TOKEN_CACHE_KEY = "ExternalApi_AccessToken";
        private const string EMPLOYEES_CACHE_KEY = "ExternalApi_Employees";
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(30);

        public ExternalApiService(
            HttpClient httpClient,
            IConfiguration configuration,
            IMemoryCache cache,
            ILogger<ExternalApiService> logger,
            AppDbContext db)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _cache = cache;
            _logger = logger;
            _db = db;

            // OAuth2 Settings
            _authServer = _configuration["ExternalApi:AuthServer"] ?? "";
            _clientId = _configuration["ExternalApi:ClientId"] ?? "";
            _clientSecret = _configuration["ExternalApi:ClientSecret"] ?? "";
            _scope = _configuration["ExternalApi:Scope"] ?? "openid profile";

            // API Settings
            _baseUrl = _configuration["ExternalApi:BaseUrl"] ?? "";
            _tenantId = _configuration.GetValue<string>("ExternalApi:TenantId") ?? "1";
        }

        #region OAuth2 Authentication

        private async Task<string?> GetAccessTokenAsync()
        {
            if (_cache.TryGetValue(TOKEN_CACHE_KEY, out string? cachedToken) && !string.IsNullOrEmpty(cachedToken))
            {
                return cachedToken;
            }

            try
            {
                var tokenEndpoint = $"{_authServer}/connect/token";

                var requestBody = new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials",
                    ["client_id"] = _clientId,
                    ["client_secret"] = _clientSecret,
                    ["scope"] = _scope
                };

                var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
                {
                    Content = new FormUrlEncodedContent(requestBody)
                };

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (tokenResponse != null && !string.IsNullOrEmpty(tokenResponse.AccessToken))
                    {
                        var cacheTime = TimeSpan.FromSeconds(tokenResponse.ExpiresIn - 300);
                        if (cacheTime.TotalSeconds < 60) cacheTime = TimeSpan.FromSeconds(60);

                        _cache.Set(TOKEN_CACHE_KEY, tokenResponse.AccessToken, cacheTime);

                        _logger.LogInformation("Successfully obtained access token");
                        return tokenResponse.AccessToken;
                    }
                }

                _logger.LogWarning("Failed to obtain access token. Status: {StatusCode}", response.StatusCode);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obtaining access token");
                return null;
            }
        }

        private async Task<HttpRequestMessage> CreateAuthorizedRequestAsync(HttpMethod method, string url)
        {
            var request = new HttpRequestMessage(method, url);

            var token = await GetAccessTokenAsync();
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            request.Headers.Add("__tenant", _tenantId);

            return request;
        }

        #endregion

        #region Organizational Units - API

        /// <summary>
        /// جلب الوحدات التنظيمية من API الخارجي
        /// </summary>
        public async Task<List<ExternalOrganizationalUnitApi>> GetAllUnitsFromApiAsync()
        {
            try
            {
                var url = $"{_baseUrl}/api/app/organizational-units/all";

                var request = await CreateAuthorizedRequestAsync(HttpMethod.Get, url);
                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                    List<ExternalOrganizationalUnitApi> units;

                    try
                    {
                        var apiResponse = JsonSerializer.Deserialize<AbpListResponse<ExternalOrganizationalUnitApi>>(json, options);
                        units = apiResponse?.Items ?? new List<ExternalOrganizationalUnitApi>();
                    }
                    catch
                    {
                        units = JsonSerializer.Deserialize<List<ExternalOrganizationalUnitApi>>(json, options) ?? new List<ExternalOrganizationalUnitApi>();
                    }

                    _logger.LogInformation("Successfully fetched {Count} organizational units from API", units.Count);
                    return units;
                }

                _logger.LogWarning("Failed to fetch organizational units. Status: {StatusCode}", response.StatusCode);
                return GetMockUnits();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching organizational units from external API");
                return GetMockUnits();
            }
        }

        #endregion

        #region Organizational Units - Sync

        /// <summary>
        /// مزامنة الوحدات التنظيمية مع قاعدة البيانات المحلية
        /// </summary>
        public async Task<SyncResult> SyncOrganizationalUnitsAsync()
        {
            var result = new SyncResult();

            try
            {
                var externalUnits = await GetAllUnitsFromApiAsync();

                if (externalUnits == null || externalUnits.Count == 0)
                {
                    result.ErrorMessage = "لم يتم جلب أي بيانات من API الخارجي";
                    return result;
                }

                var existingUnits = await _db.ExternalOrganizationalUnits.ToListAsync();
                var existingIds = existingUnits.Select(u => u.Id).ToHashSet();

                foreach (var extUnit in externalUnits)
                {
                    var existing = existingUnits.FirstOrDefault(u => u.Id == extUnit.Id);

                    if (existing == null)
                    {
                        var newUnit = new ExternalUnitEntity
                        {
                            Id = extUnit.Id,
                            ParentId = extUnit.ParentId,
                            Code = extUnit.Code ?? "",
                            ArabicName = extUnit.ArabicName,
                            ArabicUnitName = extUnit.ArabicUnitName ?? "",
                            IsActive = true,
                            LastSyncAt = DateTime.Now
                        };

                        _db.ExternalOrganizationalUnits.Add(newUnit);
                        result.AddedCount++;
                    }
                    else
                    {
                        existing.ParentId = extUnit.ParentId;
                        existing.Code = extUnit.Code ?? existing.Code;
                        existing.ArabicName = extUnit.ArabicName ?? existing.ArabicName;
                        existing.ArabicUnitName = extUnit.ArabicUnitName ?? existing.ArabicUnitName;
                        existing.LastSyncAt = DateTime.Now;
                        existing.IsActive = true;

                        result.UpdatedCount++;
                    }
                }

                await _db.SaveChangesAsync();

                result.Success = true;
                result.TotalCount = externalUnits.Count;
                result.SyncedAt = DateTime.Now;

                _logger.LogInformation("Sync completed: {Added} added, {Updated} updated",
                    result.AddedCount, result.UpdatedCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing organizational units");
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        #endregion

        #region Organizational Units - Local Database

        /// <summary>
        /// جلب الوحدات التنظيمية من قاعدة البيانات المحلية
        /// </summary>
        public async Task<List<OrganizationalUnitDto>> GetLocalUnitsAsync()
        {
            var units = await _db.ExternalOrganizationalUnits
                .Where(u => u.IsActive)
                .OrderBy(u => u.ArabicName)
                .Select(u => new OrganizationalUnitDto
                {
                    Id = u.Id,
                    ParentId = u.ParentId,
                    Name = u.ArabicName ?? u.ArabicUnitName ?? "",
                    Code = u.Code
                })
                .ToListAsync();

            return units;
        }

        /// <summary>
        /// جلب الوحدات الفرعية لوحدة معينة
        /// </summary>
        public async Task<List<OrganizationalUnitDto>> GetLocalChildUnitsAsync(Guid parentId)
        {
            var units = await _db.ExternalOrganizationalUnits
                .Where(u => u.IsActive && u.ParentId == parentId)
                .OrderBy(u => u.ArabicName)
                .Select(u => new OrganizationalUnitDto
                {
                    Id = u.Id,
                    ParentId = u.ParentId,
                    Name = u.ArabicName ?? u.ArabicUnitName ?? "",
                    Code = u.Code
                })
                .ToListAsync();

            return units;
        }

        #endregion

        #region Employees

        public async Task<List<ExternalEmployee>> GetAllEmployeesAsync()
        {

            return await Task.FromResult(GetMockEmployees());

            //if (_cache.TryGetValue(EMPLOYEES_CACHE_KEY, out List<ExternalEmployee>? cachedEmployees) && cachedEmployees != null)
            //{
            //    return cachedEmployees;
            //}

            //try
            //{
            //    var url = $"{_baseUrl}/api/app/employees/all";

            //    var request = await CreateAuthorizedRequestAsync(HttpMethod.Get, url);
            //    var response = await _httpClient.SendAsync(request);

            //    if (response.IsSuccessStatusCode)
            //    {
            //        var json = await response.Content.ReadAsStringAsync();
            //        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            //        List<ExternalEmployee> employees;

            //        try
            //        {
            //            var apiResponse = JsonSerializer.Deserialize<AbpListResponse<ExternalEmployee>>(json, options);
            //            employees = apiResponse?.Items ?? new List<ExternalEmployee>();
            //        }
            //        catch
            //        {
            //            employees = JsonSerializer.Deserialize<List<ExternalEmployee>>(json, options) ?? new List<ExternalEmployee>();
            //        }

            //        _cache.Set(EMPLOYEES_CACHE_KEY, employees, _cacheExpiration);
            //        _logger.LogInformation("Successfully fetched {Count} employees", employees.Count);

            //        return employees;
            //    }

            //    _logger.LogWarning("Failed to fetch employees. Status: {StatusCode}", response.StatusCode);
            //    return GetMockEmployees();
            //}
            //catch (Exception ex)
            //{
            //    _logger.LogError(ex, "Error fetching employees from external API");
            //    return GetMockEmployees();
            //}
        }

        public async Task<List<EmployeeDto>> SearchEmployeesAsync(string searchTerm)
        {
            var allEmployees = await GetAllEmployeesAsync();

            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return allEmployees
                    .Take(50)
                    .Select(MapToEmployeeDto)
                    .ToList();
            }

            return allEmployees
                .Where(e => e.EmpName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                            e.EmpNumber.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                            (e.Position?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false) ||
                            (e.Rank?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false))
                .Take(50)
                .Select(MapToEmployeeDto)
                .OrderBy(e => e.Name)
                .ToList();
        }

        /// <summary>
        /// جلب موظف برقم الموظف بالضبط
        /// </summary>
        public async Task<EmployeeDto?> GetEmployeeByNumberAsync(string empNumber)
        {
            if (string.IsNullOrWhiteSpace(empNumber))
            {
                return null;
            }

            var allEmployees = await GetAllEmployeesAsync();
            var employee = allEmployees.FirstOrDefault(e =>
                e.EmpNumber.Equals(empNumber, StringComparison.OrdinalIgnoreCase));

            if (employee == null)
            {
                _logger.LogWarning("Employee not found with number: {EmpNumber}", empNumber);
                return null;
            }

            return MapToEmployeeDto(employee);
        }

        private EmployeeDto MapToEmployeeDto(ExternalEmployee e) => new()
        {
            EmpNumber = e.EmpNumber,
            Name = e.EmpName,
            Rank = e.Rank,
            Position = e.Position,
            Unit = e.CurrentUnit
        };


        public async Task<byte[]?> GetEmployeePhotoAsync(string empNumber)
        {
            if (string.IsNullOrWhiteSpace(empNumber)) return null;
            try
            {
                var url = $"{_baseUrl}/person-photo-by-military-service-id/{empNumber}";
                var request = await CreateAuthorizedRequestAsync(HttpMethod.Get, url);
                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var photoBytes = await response.Content.ReadAsByteArrayAsync();
                    if (photoBytes.Length > 0) return photoBytes;
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "خطأ في جلب صورة الموظف: {EmpNumber}", empNumber);
                return null;
            }
        }

        #endregion

        #region Mock Data

        private List<ExternalOrganizationalUnitApi> GetMockUnits()
        {
            _logger.LogInformation("Using mock organizational units data");

            var t = Guid.Parse("00000000-0000-0000-0000-000000000001"); // Tenant
            return new List<ExternalOrganizationalUnitApi>
            {
                // المستوى 1
                new() { Id = Guid.Parse("10000000-0000-0000-0000-000000000001"), ParentId = null, TenentId = t, ArabicUnitName = "الإدارة العامة", Code = "001", ArabicName = "الإدارة العامة" },
                new() { Id = Guid.Parse("10000000-0000-0000-0000-000000000002"), ParentId = null, TenentId = t, ArabicUnitName = "الشؤون المالية", Code = "002", ArabicName = "الشؤون المالية" },
                new() { Id = Guid.Parse("10000000-0000-0000-0000-000000000003"), ParentId = null, TenentId = t, ArabicUnitName = "الموارد البشرية", Code = "003", ArabicName = "الموارد البشرية" },

                // المستوى 2
                new() { Id = Guid.Parse("20000000-0000-0000-0000-000000000004"), ParentId = Guid.Parse("10000000-0000-0000-0000-000000000001"), TenentId = t, ArabicUnitName = "مكتب المدير العام", Code = "001-01", ArabicName = "مكتب المدير العام" },
                new() { Id = Guid.Parse("20000000-0000-0000-0000-000000000005"), ParentId = Guid.Parse("10000000-0000-0000-0000-000000000001"), TenentId = t, ArabicUnitName = "إدارة التخطيط", Code = "001-02", ArabicName = "إدارة التخطيط" },
                new() { Id = Guid.Parse("20000000-0000-0000-0000-000000000006"), ParentId = Guid.Parse("10000000-0000-0000-0000-000000000002"), TenentId = t, ArabicUnitName = "قسم المحاسبة", Code = "002-01", ArabicName = "قسم المحاسبة" },
                new() { Id = Guid.Parse("20000000-0000-0000-0000-000000000007"), ParentId = Guid.Parse("10000000-0000-0000-0000-000000000002"), TenentId = t, ArabicUnitName = "قسم الميزانية", Code = "002-02", ArabicName = "قسم الميزانية" },
                new() { Id = Guid.Parse("20000000-0000-0000-0000-000000000008"), ParentId = Guid.Parse("10000000-0000-0000-0000-000000000003"), TenentId = t, ArabicUnitName = "قسم التوظيف", Code = "003-01", ArabicName = "قسم التوظيف" },
                new() { Id = Guid.Parse("20000000-0000-0000-0000-000000000009"), ParentId = Guid.Parse("10000000-0000-0000-0000-000000000003"), TenentId = t, ArabicUnitName = "قسم التدريب", Code = "003-02", ArabicName = "قسم التدريب" },

                // المستوى 3
                new() { Id = Guid.Parse("30000000-0000-0000-0000-000000000010"), ParentId = Guid.Parse("20000000-0000-0000-0000-000000000005"), TenentId = t, ArabicUnitName = "وحدة المتابعة", Code = "001-02-01", ArabicName = "وحدة المتابعة" },
                new() { Id = Guid.Parse("30000000-0000-0000-0000-000000000011"), ParentId = Guid.Parse("20000000-0000-0000-0000-000000000005"), TenentId = t, ArabicUnitName = "وحدة التقييم", Code = "001-02-02", ArabicName = "وحدة التقييم" },
                new() { Id = Guid.Parse("30000000-0000-0000-0000-000000000012"), ParentId = Guid.Parse("20000000-0000-0000-0000-000000000006"), TenentId = t, ArabicUnitName = "شعبة الرواتب", Code = "002-01-01", ArabicName = "شعبة الرواتب" },
                new() { Id = Guid.Parse("30000000-0000-0000-0000-000000000013"), ParentId = Guid.Parse("20000000-0000-0000-0000-000000000008"), TenentId = t, ArabicUnitName = "شعبة الاستقطاب", Code = "003-01-01", ArabicName = "شعبة الاستقطاب" },
            };
        }

        private List<ExternalEmployee> GetMockEmployees()
        {
            _logger.LogInformation("Using mock employees data");

            return new List<ExternalEmployee>
            {
                new() { EmpNumber = "C1-1001", Rank = "مدير", EmpName = "أحمد محمد العامري", Position = "مدير عام", CurrentUnit = "الإدارة العامة", BranchName = "الرئيسي" },
                new() { EmpNumber = "C1-1002", Rank = "رئيس قسم", EmpName = "سالم علي البلوشي", Position = "رئيس قسم التخطيط", CurrentUnit = "إدارة التخطيط", BranchName = "الرئيسي" },
                new() { EmpNumber = "C1-1003", Rank = "موظف", EmpName = "خالد سعيد الحارثي", Position = "محلل مالي", CurrentUnit = "قسم المحاسبة", BranchName = "الرئيسي" },
                new() { EmpNumber = "C1-1004", Rank = "موظف", EmpName = "فاطمة أحمد الهنائي", Position = "أخصائي موارد بشرية", CurrentUnit = "قسم التوظيف", BranchName = "الرئيسي" },
                new() { EmpNumber = "C1-1005", Rank = "رئيس شعبة", EmpName = "محمد سالم الكندي", Position = "رئيس شعبة الرواتب", CurrentUnit = "شعبة الرواتب", BranchName = "الرئيسي" },
                new() { EmpNumber = "C1-1006", Rank = "موظف", EmpName = "عائشة خميس المعمري", Position = "منسق تدريب", CurrentUnit = "قسم التدريب", BranchName = "الرئيسي" },
                new() { EmpNumber = "C1-1007", Rank = "موظف", EmpName = "يوسف حمد الراشدي", Position = "محلل ميزانية", CurrentUnit = "قسم الميزانية", BranchName = "الرئيسي" },
                new() { EmpNumber = "C1-1008", Rank = "رئيس وحدة", EmpName = "مريم سعود البوسعيدي", Position = "رئيس وحدة المتابعة", CurrentUnit = "وحدة المتابعة", BranchName = "الرئيسي" },
            };
        }

        #endregion

        public void ClearCache()
        {
            _cache.Remove(TOKEN_CACHE_KEY);
            _cache.Remove(EMPLOYEES_CACHE_KEY);
            _logger.LogInformation("External API cache cleared");
        }
    }

    #region Response Models

    public class TokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string TokenType { get; set; } = "Bearer";
        public int ExpiresIn { get; set; } = 3600;
        public string? RefreshToken { get; set; }
        public string? Scope { get; set; }
    }

    public class AbpListResponse<T>
    {
        public List<T> Items { get; set; } = new();
        public int TotalCount { get; set; }
    }

    /// <summary>
    /// الوحدة التنظيمية من API (DTO)
    /// </summary>
    public class ExternalOrganizationalUnitApi
    {
        public Guid Id { get; set; }
        public Guid? ParentId { get; set; }
        public Guid TenentId { get; set; }
        public string? Code { get; set; }
        public string? ArabicName { get; set; }
        public string? ArabicUnitName { get; set; }
    }

    #endregion
}
