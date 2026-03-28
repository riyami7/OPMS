# ============================================================
# Phase 3: Convert ExternalUnitId from int to Guid
# Run from: C:\Users\MSI-Laptop\source\repos\OperationalPlanMS
# Prereq: git checkout -b feature/guid-conversion
# ============================================================

$root = "OperationalPlanMS"
$ErrorActionPreference = "Stop"

function Replace-InFile($relativePath, $replacements) {
    $path = Join-Path $root $relativePath
    if (!(Test-Path $path)) { Write-Host "  SKIP (not found): $relativePath" -ForegroundColor DarkGray; return }
    $content = Get-Content $path -Raw -Encoding UTF8
    $changed = $false
    foreach ($r in $replacements) {
        $before = $content
        $content = $content -replace $r[0], $r[1]
        if ($content -ne $before) { $changed = $true }
    }
    if ($changed) {
        [System.IO.File]::WriteAllText((Resolve-Path $path).Path, $content, [System.Text.UTF8Encoding]::new($true))
        Write-Host "  OK: $relativePath" -ForegroundColor Green
    } else {
        Write-Host "  NO CHANGE: $relativePath" -ForegroundColor Yellow
    }
}

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host " Phase 3: Converting ExternalUnitId from int to Guid" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan

# ============================================================
# STEP 1: ExternalOrganizationalUnit Entity
# ============================================================
Write-Host "`n[1/8] ExternalOrganizationalUnit Entity..." -ForegroundColor Yellow

Replace-InFile "Models\Entities\ExternalOrganizationalUnit.cs" @(
    ,@('public int Id \{ get; set; \}', 'public Guid Id { get; set; }')
    ,@('public int\? ParentId \{ get; set; \}', 'public Guid? ParentId { get; set; }')
    ,@('public int TenantId \{ get; set; \}', 'public Guid TenantId { get; set; }')
    ,@('public bool IsRoot => !ParentId\.HasValue \|\| ParentId == 0;', 'public bool IsRoot => !ParentId.HasValue;')
)

# ============================================================
# STEP 2: Other Entities
# ============================================================
Write-Host "`n[2/8] Entities (ExternalUnitId)..." -ForegroundColor Yellow

$entityFiles = @(
    "Models\Entities\Initiative.cs",
    "Models\Entities\Project.cs",
    "Models\Entities\User.cs",
    "Models\Entities\SubObjective.cs",
    "Models\Entities\OrganizationalUnitSettings.cs",
    "Models\Entities\ProjectSupportingUnit.cs"
)
foreach ($file in $entityFiles) {
    Replace-InFile $file @(
        ,@('public int\? ExternalUnitId \{ get; set; \}', 'public Guid? ExternalUnitId { get; set; }')
    )
}

# ============================================================
# STEP 3: ViewModels
# ============================================================
Write-Host "`n[3/8] ViewModels..." -ForegroundColor Yellow

foreach ($file in @(
    "Models\ViewModels\InitiativeViewModel.cs",
    "Models\ViewModels\ProjectViewModels.cs",
    "Models\ViewModels\AdminViewModels.cs",
    "Models\ViewModels\VisionMissionViewModels.cs"
)) {
    Replace-InFile $file @(
        ,@('public int\? ExternalUnitId \{ get; set; \}', 'public Guid? ExternalUnitId { get; set; }')
    )
}

Replace-InFile "Models\ViewModels\ReportViewModel.cs" @(
    ,@('public int\? ExternalUnitId \{ get; set; \}', 'public Guid? ExternalUnitId { get; set; }')
    ,@('public int\? UnitId \{ get; set; \}', 'public Guid? UnitId { get; set; }')
)

# ============================================================
# STEP 4: Services
# ============================================================
Write-Host "`n[4/8] Services..." -ForegroundColor Yellow

Replace-InFile "Services\InitiativeService.cs" @(
    @('int\? fiscalYearId, int\? externalUnitId,', 'int? fiscalYearId, Guid? externalUnitId,')
    @('Task<string\?> GetUnitNameAsync\(int externalUnitId\)', 'Task<string?> GetUnitNameAsync(Guid externalUnitId)')
    @('public async Task<string\?> GetUnitNameAsync\(int externalUnitId\)', 'public async Task<string?> GetUnitNameAsync(Guid externalUnitId)')
    @('private async Task<List<int>> GetUnitAndChildrenIdsAsync\(int unitId\)', 'private async Task<List<Guid>> GetUnitAndChildrenIdsAsync(Guid unitId)')
    @('var result = new List<int> \{ unitId \};', 'var result = new List<Guid> { unitId };')
)

Replace-InFile "Services\ProjectService.cs" @(
    @('int\? initiativeId, int\? externalUnitId,', 'int? initiativeId, Guid? externalUnitId,')
    @('Task<object> GetSubObjectivesByUnitAsync\(int\? externalUnitId\)', 'Task<object> GetSubObjectivesByUnitAsync(Guid? externalUnitId)')
    @('Task<string\?> GetUnitNameAsync\(int externalUnitId\)', 'Task<string?> GetUnitNameAsync(Guid externalUnitId)')
    @('public async Task<object> GetSubObjectivesByUnitAsync\(int\? externalUnitId\)', 'public async Task<object> GetSubObjectivesByUnitAsync(Guid? externalUnitId)')
    @('public async Task<string\?> GetUnitNameAsync\(int externalUnitId\)', 'public async Task<string?> GetUnitNameAsync(Guid externalUnitId)')
    @('private async Task<List<int>> GetUnitAndChildrenIdsAsync\(int unitId\)', 'private async Task<List<Guid>> GetUnitAndChildrenIdsAsync(Guid unitId)')
    @('var result = new List<int> \{ unitId \};', 'var result = new List<Guid> { unitId };')
)

Replace-InFile "Services\ExternalApiService.cs" @(
    @('Task<List<OrganizationalUnitDto>> GetLocalChildUnitsAsync\(int parentId\)', 'Task<List<OrganizationalUnitDto>> GetLocalChildUnitsAsync(Guid parentId)')
    @('public async Task<List<OrganizationalUnitDto>> GetLocalChildUnitsAsync\(int parentId\)', 'public async Task<List<OrganizationalUnitDto>> GetLocalChildUnitsAsync(Guid parentId)')
    @('private readonly int _tenantId;', 'private readonly string _tenantId;')
)

# ============================================================
# STEP 5: Controllers
# ============================================================
Write-Host "`n[5/8] Controllers..." -ForegroundColor Yellow

Replace-InFile "Controllers\InitiativesController.cs" @(
    ,@('int\? externalUnitId\)', 'Guid? externalUnitId)')
)

Replace-InFile "Controllers\ProjectsController.cs" @(
    @('int\? externalUnitId\)', 'Guid? externalUnitId)')
    @('public async Task<IActionResult> GetSubObjectivesByUnit\(int\? externalUnitId\)', 'public async Task<IActionResult> GetSubObjectivesByUnit(Guid? externalUnitId)')
)

Replace-InFile "Controllers\ReportsController.cs" @(
    @('int\? fiscalYearId, int\? externalUnitId\)', 'int? fiscalYearId, Guid? externalUnitId)')
    @('int\? fiscalYearId = null, int\? externalUnitId = null\)', 'int? fiscalYearId = null, Guid? externalUnitId = null)')
    @('private async Task<List<int>> GetUnitAndChildrenIds\(int unitId\)', 'private async Task<List<Guid>> GetUnitAndChildrenIds(Guid unitId)')
    @('var result = new List<int> \{ unitId \};', 'var result = new List<Guid> { unitId };')
    @('List<int>\? unitIds = null;', 'List<Guid>? unitIds = null;')
)

Replace-InFile "Controllers\StrategicPlanningController.cs" @(
    ,@('int\? ExternalUnitId, string\? ExternalUnitName', 'Guid? ExternalUnitId, string? ExternalUnitName')
)

# ============================================================
# STEP 6: Views - parseInt() removal
# ============================================================
Write-Host "`n[6/8] Views (parseInt removal)..." -ForegroundColor Yellow

Replace-InFile "Views\Initiatives\Index.cshtml" @(
    @('var id = parseInt\(this\.value\)', 'var id = this.value')
    @('restoreFilterSelection\(parseInt\(selectedExternalUnitId\)\)', 'restoreFilterSelection(selectedExternalUnitId)')
)

Replace-InFile "Views\Projects\Index.cshtml" @(
    @('var id = parseInt\(this\.value\)', 'var id = this.value')
    @('restoreFilterSelection\(parseInt\(selectedExternalUnitId\)\)', 'restoreFilterSelection(selectedExternalUnitId)')
)

foreach ($file in @(
    "Views\StrategicPlanning\EditSubObjective.cshtml",
    "Views\StrategicPlanning\EditUnitSettings.cshtml",
    "Views\StrategicPlanning\Index.cshtml",
    "Views\StrategicPlanning\VisionMission.cshtml",
    "Views\Initiatives\Edit.cshtml",
    "Views\Initiatives\Create.cshtml"
)) {
    Replace-InFile $file @(
        ,@('const parentId = parseInt\(this\.value\);', 'const parentId = this.value;')
    )
}

Replace-InFile "Views\Initiatives\Details.cshtml" @(
    ,@('loadUnitHierarchy\(parseInt\(externalUnitIdEl\.value\)\)', 'loadUnitHierarchy(externalUnitIdEl.value)')
)

# ============================================================
# STEP 7: JavaScript files
# ============================================================
Write-Host "`n[7/8] JavaScript files..." -ForegroundColor Yellow

Replace-InFile "wwwroot\js\project-form.js" @(
    @('savedExternalUnitId: number', 'savedExternalUnitId: string')
    @('if \(savedExternalUnitId > 0\)', 'if (savedExternalUnitId)')
    @('unitId = parseInt\(level3\.value\);', 'unitId = level3.value;')
    @('unitId = parseInt\(level2\.value\);', 'unitId = level2.value;')
    @('unitId = parseInt\(level1\.value\);', 'unitId = level1.value;')
)

Replace-InFile "wwwroot\js\reports-dashboard.js" @(
    @('restoreFilterSelection\(parseInt\(selectedExternalUnitId\)\)', 'restoreFilterSelection(selectedExternalUnitId)')
    @('var id = parseInt\(this\.value\)', 'var id = this.value')
)

# ============================================================
# STEP 8: Summary
# ============================================================
Write-Host "`n============================================================" -ForegroundColor Cyan
Write-Host " Automated replacements complete!" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host " MANUAL CHANGES STILL NEEDED (10 items):" -ForegroundColor Red
Write-Host ""
Write-Host " A) ProjectViewModels.cs ~line 552 (SupportingEntityDisplayItem):" -ForegroundColor White
Write-Host "    public int Id { get; set; }" -ForegroundColor DarkGray
Write-Host "    -->  public string Id { get; set; } = string.Empty;" -ForegroundColor DarkGray
Write-Host ""
Write-Host " B) ProjectService.cs ~line 153:" -ForegroundColor White
Write-Host "    Id = s.SupportingEntityId > 0 ? s.SupportingEntity!.Id : (s.ExternalUnitId ?? 0)," -ForegroundColor DarkGray
Write-Host '    -->  Id = s.SupportingEntityId > 0 ? s.SupportingEntity!.Id.ToString() : (s.ExternalUnitId?.ToString() ?? ""),' -ForegroundColor DarkGray
Write-Host ""
Write-Host " C) ReportsController.cs ~line 276:" -ForegroundColor White
Write-Host "    unitId = u.ExternalUnitId ?? u.UnitId ?? 0" -ForegroundColor DarkGray
Write-Host '    -->  unitId = (u.ExternalUnitId ?? u.UnitId)?.ToString() ?? ""' -ForegroundColor DarkGray
Write-Host ""
Write-Host " D) Reports/Index.cshtml ~line 400:" -ForegroundColor White
Write-Host "    var uid = unit.ExternalUnitId ?? unit.UnitId ?? 0;" -ForegroundColor DarkGray
Write-Host '    -->  var uid = (unit.ExternalUnitId ?? unit.UnitId)?.ToString() ?? "";' -ForegroundColor DarkGray
Write-Host ""
Write-Host " E) ExternalApiService.cs bottom ~line 522 (local DTO):" -ForegroundColor White
Write-Host "    public int Id --> public Guid Id" -ForegroundColor DarkGray
Write-Host "    public int? ParentId --> public Guid? ParentId" -ForegroundColor DarkGray
Write-Host "    public int TenentId --> public Guid TenentId" -ForegroundColor DarkGray
Write-Host ""
Write-Host " F) ExternalApiService.cs GetMockUnits() - see MOCK_DATA_GUIDE.md" -ForegroundColor White
Write-Host ""
Write-Host " G) ExternalApiService.cs ~line 93:" -ForegroundColor White
Write-Host '    _tenantId = _configuration.GetValue<int>("ExternalApi:TenantId", 1);' -ForegroundColor DarkGray
Write-Host '    -->  _tenantId = _configuration.GetValue<string>("ExternalApi:TenantId") ?? "1";' -ForegroundColor DarkGray
Write-Host ""
Write-Host " H) 5 Views - savedExternalUnitId Razor lines:" -ForegroundColor White
Write-Host "    Projects/Create.cshtml ~line 50 and Projects/Edit.cshtml ~line 70:" -ForegroundColor DarkGray
Write-Host "      savedExternalUnitId: @(Model.ExternalUnitId ?? 0)," -ForegroundColor DarkGray
Write-Host "      --> savedExternalUnitId: '@(Model.ExternalUnitId?.ToString() ?? `"`")'," -ForegroundColor DarkGray
Write-Host "    EditSubObjective ~114, EditUnitSettings ~133, Initiatives/Edit ~237:" -ForegroundColor DarkGray
Write-Host "      const savedExternalUnitId = @(Model.ExternalUnitId?.ToString() ?? `"null`");" -ForegroundColor DarkGray
Write-Host "      --> const savedExternalUnitId = '@(Model.ExternalUnitId?.ToString() ?? `"`")';" -ForegroundColor DarkGray
Write-Host ""
Write-Host " I) project-form.js ~line 36:" -ForegroundColor White
Write-Host "    const savedExternalUnitId = config.savedExternalUnitId || 0;" -ForegroundColor DarkGray
Write-Host "    --> const savedExternalUnitId = config.savedExternalUnitId || '';" -ForegroundColor DarkGray
Write-Host ""
Write-Host " J) project-form.js renderSupportingEntities - quote Guid in onclick:" -ForegroundColor White
Write-Host '    removeRep(${entity.externalUnitId},  -->  removeRep(' + "'" + '${entity.externalUnitId}' + "'" + ',' -ForegroundColor DarkGray
Write-Host '    removeSupportingEntity(${e..})  -->  same pattern' -ForegroundColor DarkGray
Write-Host '    searchEntityRep(${e..},  -->  same pattern' -ForegroundColor DarkGray
Write-Host ""
Write-Host " K) appsettings.json:" -ForegroundColor White
Write-Host '    "TenantId": 1  -->  "TenantId": "00000000-0000-0000-0000-000000000001"' -ForegroundColor DarkGray
Write-Host ""
Write-Host " THEN: dotnet build | Add-Migration | Update-Database | dotnet test" -ForegroundColor Yellow
Write-Host ""
