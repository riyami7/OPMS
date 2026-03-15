/**
 * project-form.js
 * ================
 * Shared JavaScript for Project Create & Edit forms.
 *
 * Requires window.ProjectFormConfig to be set BEFORE this script loads:
 *   window.ProjectFormConfig = {
 *       requirementIndex: number,
 *       kpiIndex: number,
 *       supportingEntities: array,
 *       existingYearTargets: array,
 *       savedExternalUnitId: number,
 *       savedSubObjectiveIds: array  // [] for Create
 *   }
 */

(function () {
    'use strict';

    const config = window.ProjectFormConfig || {};
    let requirementIndex = config.requirementIndex || 0;
    let kpiIndex = config.kpiIndex || 0;
    let supportingEntities = config.supportingEntities || [];
    let allUnitsCache = [];

    const existingYearTargets = config.existingYearTargets || [];
    const savedExternalUnitId = config.savedExternalUnitId || 0;
    const savedSubObjectiveIds = config.savedSubObjectiveIds || [];

    // ================================================================
    //  DOMContentLoaded
    // ================================================================
    document.addEventListener('DOMContentLoaded', async function () {
        await loadOrganizationalUnits();
        renderSupportingEntities();
        checkMultiYear();
    });

    // ================================================================
    //  الهيكل التنظيمي — Organizational Structure
    // ================================================================
    async function loadOrganizationalUnits() {
        try {
            const response = await fetch('/api/OrganizationApi/units/all');
            if (response.ok) {
                allUnitsCache = await response.json();

                if (allUnitsCache.length === 0) {
                    document.getElementById('ExternalLevel1').innerHTML =
                        '<option value="">-- لا توجد بيانات، يرجى المزامنة --</option>';
                    return;
                }

                populateLevel1();

                if (savedExternalUnitId > 0) {
                    await restoreSelection(savedExternalUnitId);
                }
            } else {
                console.error('Failed to load units');
                document.getElementById('ExternalLevel1').innerHTML =
                    '<option value="">-- فشل تحميل البيانات --</option>';
            }
        } catch (error) {
            console.error('Error loading units:', error);
            document.getElementById('ExternalLevel1').innerHTML =
                '<option value="">-- خطأ في الاتصال --</option>';
        }
    }

    async function restoreSelection(unitId) {
        const unit = allUnitsCache.find(u => u.id == unitId);
        if (!unit) return;

        const path = [];
        let current = unit;
        while (current) {
            path.unshift(current);
            current = current.parentId ? allUnitsCache.find(u => u.id == current.parentId) : null;
        }

        if (path.length >= 1) {
            document.getElementById('ExternalLevel1').value = path[0].id;
            loadLevel2(path[0].id);
        }
        if (path.length >= 2) {
            setTimeout(() => {
                document.getElementById('ExternalLevel2').value = path[1].id;
                loadLevel3(path[1].id);
            }, 100);
        }
        if (path.length >= 3) {
            setTimeout(() => {
                document.getElementById('ExternalLevel3').value = path[2].id;
                updateSelectedUnit();
            }, 200);
        }

        // تحميل الأهداف الفرعية مع القيم المحفوظة
        if (savedSubObjectiveIds.length > 0) {
            setTimeout(() => {
                const unitId = document.getElementById('ExternalUnitId').value;
                if (unitId) {
                    loadSubObjectives(unitId, savedSubObjectiveIds);
                }
            }, 300);
        }
    }

    function populateLevel1() {
        const level1 = document.getElementById('ExternalLevel1');
        const rootUnits = allUnitsCache.filter(u => !u.parentId || u.parentId === 0);

        level1.innerHTML = '<option value="">-- اختر --</option>';
        rootUnits.forEach(u => {
            level1.innerHTML += `<option value="${u.id}" data-name="${u.name}">${u.name}</option>`;
        });

        populateSupportingLevel1();
    }

    document.getElementById('ExternalLevel1')?.addEventListener('change', function () {
        const level2 = document.getElementById('ExternalLevel2');
        const level3 = document.getElementById('ExternalLevel3');
        level2.innerHTML = '<option value="">-- اختر --</option>';
        level2.disabled = true;
        level3.innerHTML = '<option value="">-- اختر --</option>';
        level3.disabled = true;

        if (this.value) {
            loadLevel2(this.value);
        }
        updateSelectedUnit();
    });

    function loadLevel2(parentId) {
        const level2 = document.getElementById('ExternalLevel2');
        const children = allUnitsCache.filter(u => u.parentId == parentId);

        if (children.length > 0) {
            level2.innerHTML = '<option value="">-- اختر --</option>';
            children.forEach(u => {
                level2.innerHTML += `<option value="${u.id}" data-name="${u.name}">${u.name}</option>`;
            });
            level2.disabled = false;
        }
    }

    document.getElementById('ExternalLevel2')?.addEventListener('change', function () {
        const level3 = document.getElementById('ExternalLevel3');
        level3.innerHTML = '<option value="">-- اختر --</option>';
        level3.disabled = true;

        if (this.value) {
            loadLevel3(this.value);
        }
        updateSelectedUnit();
    });

    function loadLevel3(parentId) {
        const level3 = document.getElementById('ExternalLevel3');
        const children = allUnitsCache.filter(u => u.parentId == parentId);

        if (children.length > 0) {
            level3.innerHTML = '<option value="">-- اختر --</option>';
            children.forEach(u => {
                level3.innerHTML += `<option value="${u.id}" data-name="${u.name}">${u.name}</option>`;
            });
            level3.disabled = false;
        }
    }

    document.getElementById('ExternalLevel3')?.addEventListener('change', function () {
        updateSelectedUnit();
    });

    function updateSelectedUnit() {
        const level3 = document.getElementById('ExternalLevel3');
        const level2 = document.getElementById('ExternalLevel2');
        const level1 = document.getElementById('ExternalLevel1');

        let selectedId = '';
        let selectedName = '';

        if (level3.value) {
            selectedId = level3.value;
            selectedName = level3.options[level3.selectedIndex]?.dataset.name || '';
        } else if (level2.value) {
            selectedId = level2.value;
            selectedName = level2.options[level2.selectedIndex]?.dataset.name || '';
        } else if (level1.value) {
            selectedId = level1.value;
            selectedName = level1.options[level1.selectedIndex]?.dataset.name || '';
        }

        document.getElementById('ExternalUnitId').value = selectedId;
        document.getElementById('ExternalUnitName').value = selectedName;
        loadSubObjectives(selectedId);
    }

    async function loadSubObjectives(unitId, selectedValues) {
        selectedValues = selectedValues || [];
        const container = document.getElementById('SubObjectivesContainer');
        if (!container) return;

        if (!unitId) {
            container.innerHTML = '<small class="text-muted">-- اختر الوحدة أولاً --</small>';
            return;
        }

        container.innerHTML = '<div class="d-flex align-items-center gap-2 py-1"><div class="spinner-border spinner-border-sm text-primary"></div><small class="text-muted">جاري التحميل...</small></div>';

        try {
            const response = await fetch(`/Projects/GetSubObjectivesByUnit?externalUnitId=${encodeURIComponent(unitId)}`);
            if (response.ok) {
                const objectives = await response.json();
                container.innerHTML = '';

                if (objectives.length === 0) {
                    container.innerHTML = '<small class="text-muted">-- لا توجد أهداف فرعية لهذه الوحدة --</small>';
                    return;
                }

                objectives.forEach(obj => {
                    const isChecked = Array.isArray(selectedValues) && selectedValues.includes(obj.id);
                    const item = document.createElement('div');
                    item.className = 'form-check py-1';
                    item.innerHTML =
                        '<input class="form-check-input" type="checkbox" name="SubObjectiveIds" value="' + obj.id + '" id="so_' + obj.id + '"' + (isChecked ? ' checked' : '') + '>' +
                        '<label class="form-check-label" for="so_' + obj.id + '" style="cursor:pointer;">' + obj.nameAr + '</label>';
                    container.appendChild(item);
                });
            }
        } catch (error) {
            console.error('Error loading sub objectives:', error);
            container.innerHTML = '<small class="text-danger">خطأ في التحميل</small>';
        }
    }

    // ================================================================
    //  مدير المشروع — Project Manager Search
    // ================================================================
    let searchTimeout;
    const projectManagerSearch = document.getElementById('projectManagerSearch');
    const projectManagerResults = document.getElementById('projectManagerResults');

    projectManagerSearch?.addEventListener('input', function () {
        clearTimeout(searchTimeout);
        const term = this.value.trim();
        if (term.length < 2) {
            projectManagerResults.classList.remove('show');
            return;
        }
        searchTimeout = setTimeout(() => searchEmployees(term), 300);
    });

    async function searchEmployees(term) {
        try {
            const response = await fetch(`/api/OrganizationApi/employees/search?term=${encodeURIComponent(term)}`);
            if (response.ok) {
                const employees = await response.json();
                showEmployeeResults(employees);
            }
        } catch (error) {
            console.error('Error:', error);
        }
    }

    function showEmployeeResults(employees) {
        if (employees.length === 0) {
            projectManagerResults.innerHTML = '<div class="p-2 text-muted">لا توجد نتائج</div>';
        } else {
            projectManagerResults.innerHTML = employees.map(emp => `
                <div class="item" onclick="selectProjectManager('${emp.empNumber}', '${emp.name}', '${emp.rank || ''}')">
                    <div class="emp-name">${emp.rank || ''} ${emp.name}</div>
                    <div class="emp-info">${emp.empNumber} - ${emp.position || ''}</div>
                </div>
            `).join('');
        }
        projectManagerResults.classList.add('show');
    }

    window.selectProjectManager = function (empNumber, name, rank) {
        document.getElementById('ProjectManagerEmpNumber').value = empNumber;
        document.getElementById('ProjectManagerName').value = name;
        document.getElementById('ProjectManagerRank').value = rank;

        document.getElementById('projectManagerSearch').style.display = 'none';
        document.getElementById('projectManagerDisplay').innerHTML = `
            <div class="selected-employee">
                <div>
                    <div class="fw-bold">${rank} ${name}</div>
                    <small class="text-muted">${empNumber}</small>
                </div>
                <span class="remove-btn" onclick="clearProjectManager()"><i class="bi bi-x-circle"></i></span>
            </div>
        `;
        document.getElementById('projectManagerDisplay').style.display = 'block';
        projectManagerResults.classList.remove('show');
    };

    window.clearProjectManager = function () {
        document.getElementById('ProjectManagerEmpNumber').value = '';
        document.getElementById('ProjectManagerName').value = '';
        document.getElementById('ProjectManagerRank').value = '';
        document.getElementById('projectManagerSearch').value = '';
        document.getElementById('projectManagerSearch').style.display = 'block';
        document.getElementById('projectManagerDisplay').style.display = 'none';
    };

    document.addEventListener('click', (e) => {
        if (!e.target.closest('#projectManagerSearch') && !e.target.closest('#projectManagerResults')) {
            projectManagerResults?.classList.remove('show');
        }
    });

    // ================================================================
    //  الجهات المساندة — Supporting Entities
    // ================================================================
    window.showAddSupportingEntity = function () {
        document.getElementById('addSupportingEntitySection').style.display = 'block';
    };

    window.cancelAddSupportingEntity = function () {
        document.getElementById('addSupportingEntitySection').style.display = 'none';
    };

    function populateSupportingLevel1() {
        const select = document.getElementById('supportingLevel1');
        const rootUnits = allUnitsCache.filter(u => !u.parentId || u.parentId === 0);
        select.innerHTML = '<option value="">-- اختر --</option>';
        rootUnits.forEach(u => {
            select.innerHTML += `<option value="${u.id}" data-name="${u.name}">${u.name}</option>`;
        });
    }

    window.loadSupportingLevel2 = function () {
        const level1 = document.getElementById('supportingLevel1');
        const level2 = document.getElementById('supportingLevel2');
        const level3 = document.getElementById('supportingLevel3');
        level2.innerHTML = '<option value="">-- اختر --</option>';
        level2.disabled = true;
        level3.innerHTML = '<option value="">-- اختر --</option>';
        level3.disabled = true;

        if (level1.value) {
            const children = allUnitsCache.filter(u => u.parentId == level1.value);
            if (children.length > 0) {
                children.forEach(u => {
                    level2.innerHTML += `<option value="${u.id}" data-name="${u.name}">${u.name}</option>`;
                });
                level2.disabled = false;
            }
        }
    };

    window.loadSupportingLevel3 = function () {
        const level2 = document.getElementById('supportingLevel2');
        const level3 = document.getElementById('supportingLevel3');
        level3.innerHTML = '<option value="">-- اختر --</option>';
        level3.disabled = true;

        if (level2.value) {
            const children = allUnitsCache.filter(u => u.parentId == level2.value);
            if (children.length > 0) {
                children.forEach(u => {
                    level3.innerHTML += `<option value="${u.id}" data-name="${u.name}">${u.name}</option>`;
                });
                level3.disabled = false;
            }
        }
    };

    window.addSupportingEntity = function () {
        const level3 = document.getElementById('supportingLevel3');
        const level2 = document.getElementById('supportingLevel2');
        const level1 = document.getElementById('supportingLevel1');

        let unitId, unitName;
        if (level3.value) {
            unitId = parseInt(level3.value);
            unitName = level3.options[level3.selectedIndex]?.dataset.name || '';
        } else if (level2.value) {
            unitId = parseInt(level2.value);
            unitName = level2.options[level2.selectedIndex]?.dataset.name || '';
        } else if (level1.value) {
            unitId = parseInt(level1.value);
            unitName = level1.options[level1.selectedIndex]?.dataset.name || '';
        } else {
            alert('يرجى اختيار جهة');
            return;
        }

        if (supportingEntities.find(e => e.externalUnitId == unitId)) {
            alert('هذه الجهة مضافة مسبقاً');
            return;
        }

        supportingEntities.push({
            externalUnitId: unitId,
            unitName: unitName,
            representativeEmpNumber: null,
            representativeName: null,
            representativeRank: null
        });

        renderSupportingEntities();
        window.cancelAddSupportingEntity();
    };

    window.removeSupportingEntity = function (unitId) {
        supportingEntities = supportingEntities.filter(e => e.externalUnitId != unitId);
        renderSupportingEntities();
    };

    function renderSupportingEntities() {
        const container = document.getElementById('supportingEntitiesContainer');
        const inputsContainer = document.getElementById('supportingEntitiesInputs');
        const noEntities = document.getElementById('noSupportingEntities');

        if (supportingEntities.length === 0) {
            container.innerHTML = '';
            inputsContainer.innerHTML = '';
            noEntities.style.display = 'block';
            return;
        }

        noEntities.style.display = 'none';

        container.innerHTML = supportingEntities.map((entity, index) => `
            <div class="supporting-entity-item" id="entity_${entity.externalUnitId}">
                <div class="entity-header">
                    <span class="entity-name"><i class="bi bi-building me-1"></i>${entity.unitName}</span>
                    <button type="button" class="btn btn-sm btn-outline-danger" onclick="removeSupportingEntity(${entity.externalUnitId})">
                        <i class="bi bi-trash"></i>
                    </button>
                </div>
                <div class="row align-items-end">
                    <div class="col">
                        <label class="form-label small">ممثل الجهة (اختياري)</label>
                        <div class="position-relative">
                            <div id="repDisplay_${entity.externalUnitId}" style="display: ${entity.representativeEmpNumber ? 'block' : 'none'};">
                                ${entity.representativeEmpNumber ? `
                                    <div class="selected-employee">
                                        <div>
                                            <div class="fw-bold">${entity.representativeRank || ''} ${entity.representativeName || ''}</div>
                                            <small class="text-muted">${entity.representativeEmpNumber}</small>
                                        </div>
                                        <span class="remove-btn" onclick="clearEntityRep(${entity.externalUnitId})"><i class="bi bi-x-circle"></i></span>
                                    </div>
                                ` : ''}
                            </div>
                            <input type="text" id="repSearch_${entity.externalUnitId}" class="form-control form-control-sm"
                                   placeholder="ابحث برقم الموظف..." oninput="searchEntityRep(${entity.externalUnitId}, this.value)"
                                   style="display: ${entity.representativeEmpNumber ? 'none' : 'block'};" />
                            <div id="repResults_${entity.externalUnitId}" class="search-dropdown"></div>
                        </div>
                    </div>
                </div>
            </div>
        `).join('');

        inputsContainer.innerHTML = supportingEntities.map((entity, index) => `
            <input type="hidden" name="SupportingEntitiesWithReps[${index}].ExternalUnitId" value="${entity.externalUnitId || ''}" />
            <input type="hidden" name="SupportingEntitiesWithReps[${index}].UnitName" value="${entity.unitName}" />
            <input type="hidden" name="SupportingEntitiesWithReps[${index}].RepresentativeEmpNumber" value="${entity.representativeEmpNumber || ''}" />
            <input type="hidden" name="SupportingEntitiesWithReps[${index}].RepresentativeName" value="${entity.representativeName || ''}" />
            <input type="hidden" name="SupportingEntitiesWithReps[${index}].RepresentativeRank" value="${entity.representativeRank || ''}" />
        `).join('');
    }

    let repSearchTimeout;
    window.searchEntityRep = function (unitId, term) {
        clearTimeout(repSearchTimeout);
        const resultsDiv = document.getElementById(`repResults_${unitId}`);
        if (term.length < 2) {
            resultsDiv.classList.remove('show');
            return;
        }

        repSearchTimeout = setTimeout(async () => {
            try {
                const response = await fetch(`/api/OrganizationApi/employees/search?term=${encodeURIComponent(term)}`);
                if (response.ok) {
                    const employees = await response.json();
                    resultsDiv.innerHTML = employees.length === 0
                        ? '<div class="p-2 text-muted">لا توجد نتائج</div>'
                        : employees.map(emp => `
                                <div class="item" onclick="selectEntityRep(${unitId}, '${emp.empNumber}', '${emp.name}', '${emp.rank || ''}')">
                                    <div class="emp-name">${emp.rank || ''} ${emp.name}</div>
                                    <div class="emp-info">${emp.empNumber}</div>
                                </div>
                            `).join('');
                    resultsDiv.classList.add('show');
                }
            } catch (error) {
                console.error('Error:', error);
            }
        }, 300);
    };

    window.selectEntityRep = function (unitId, empNumber, name, rank) {
        const entity = supportingEntities.find(e => e.externalUnitId == unitId);
        if (entity) {
            entity.representativeEmpNumber = empNumber;
            entity.representativeName = name;
            entity.representativeRank = rank;
        }
        renderSupportingEntities();
    };

    window.clearEntityRep = function (unitId) {
        const entity = supportingEntities.find(e => e.externalUnitId == unitId);
        if (entity) {
            entity.representativeEmpNumber = null;
            entity.representativeName = null;
            entity.representativeRank = null;
        }
        renderSupportingEntities();
    };

    // ================================================================
    //  متطلبات التنفيذ — Requirements
    // ================================================================
    window.addRequirement = function () {
        const container = document.getElementById('requirementsContainer');
        const div = document.createElement('div');
        div.className = 'input-group mb-2 requirement-item';
        div.innerHTML = `
            <span class="input-group-text">${requirementIndex + 1}</span>
            <input type="text" name="Requirements[${requirementIndex}]" class="form-control" />
            <button type="button" class="btn btn-outline-danger" onclick="removeRequirement(this)"><i class="bi bi-trash"></i></button>
        `;
        container.appendChild(div);
        requirementIndex++;
    };

    window.removeRequirement = function (btn) {
        btn.closest('.requirement-item').remove();
        reindexRequirements();
    };

    function reindexRequirements() {
        const items = document.querySelectorAll('#requirementsContainer .requirement-item');
        items.forEach((item, index) => {
            item.querySelector('.input-group-text').textContent = index + 1;
            item.querySelector('input').name = `Requirements[${index}]`;
        });
        requirementIndex = items.length;
    }

    // ================================================================
    //  مؤشرات الأداء — KPIs
    // ================================================================
    window.addKPI = function () {
        const container = document.getElementById('kpisContainer');
        const div = document.createElement('div');
        div.className = 'card mb-2 kpi-item';
        div.innerHTML = `
            <div class="card-body py-2">
                <div class="row g-2 align-items-center">
                    <div class="col-md-6"><input type="text" name="KPIItems[${kpiIndex}].KPIText" class="form-control form-control-sm" placeholder="نص المؤشر" /></div>
                    <div class="col-md-2"><input type="text" name="KPIItems[${kpiIndex}].TargetValue" class="form-control form-control-sm" placeholder="المستهدف" /></div>
                    <div class="col-md-2"><input type="text" name="KPIItems[${kpiIndex}].ActualValue" class="form-control form-control-sm" placeholder="الفعلي" /></div>
                    <div class="col-md-2 text-end"><button type="button" class="btn btn-sm btn-outline-danger" onclick="removeKPI(this)"><i class="bi bi-trash"></i></button></div>
                </div>
            </div>
        `;
        container.appendChild(div);
        kpiIndex++;
    };

    window.removeKPI = function (btn) {
        btn.closest('.kpi-item').remove();
        reindexKPIs();
    };

    function reindexKPIs() {
        const items = document.querySelectorAll('#kpisContainer .kpi-item');
        items.forEach((item, index) => {
            const inputs = item.querySelectorAll('input');
            inputs[0].name = `KPIItems[${index}].KPIText`;
            inputs[1].name = `KPIItems[${index}].TargetValue`;
            inputs[2].name = `KPIItems[${index}].ActualValue`;
        });
        kpiIndex = items.length;
    }

    // ================================================================
    //  نسب السنوات — Multi-Year Targets
    // ================================================================
    window.checkMultiYear = function () {
        const startDate = document.getElementById('plannedStartDate').value;
        const endDate = document.getElementById('plannedEndDate').value;
        const multiYearCard = document.getElementById('multiYearCard');

        if (!startDate || !endDate) {
            multiYearCard.style.display = 'none';
            return;
        }

        const startYear = new Date(startDate).getFullYear();
        const endYear = new Date(endDate).getFullYear();
        const yearCount = endYear - startYear + 1;

        if (yearCount > 1) {
            multiYearCard.style.display = 'block';
            document.getElementById('yearCount').textContent = yearCount + ' سنوات';
            generateYearTargets(startYear, endYear);
        } else {
            multiYearCard.style.display = 'none';
        }
    };

    function generateYearTargets(startYear, endYear) {
        const container = document.getElementById('yearTargetsContainer');
        let html = '';
        for (let year = startYear; year <= endYear; year++) {
            const existing = existingYearTargets.find(t => t.year === year);
            const value = existing ? existing.targetPercentage : '';
            html += `
                <div class="mb-2">
                    <div class="input-group input-group-sm">
                        <span class="input-group-text">${year}</span>
                        <input type="hidden" name="YearTargets[${year - startYear}].Year" value="${year}" />
                        <input type="number" name="YearTargets[${year - startYear}].TargetPercentage" class="form-control year-percentage"
                               value="${value}" min="0" max="100" step="0.01" placeholder="%" onchange="calculateTotalPercentage()" oninput="calculateTotalPercentage()" />
                        <span class="input-group-text">%</span>
                    </div>
                </div>
            `;
        }
        container.innerHTML = html;
        window.calculateTotalPercentage();
    }

    window.calculateTotalPercentage = function () {
        const inputs = document.querySelectorAll('.year-percentage');
        let total = 0;
        inputs.forEach(input => { total += parseFloat(input.value) || 0; });
        const badge = document.getElementById('totalYearPercentage');
        badge.textContent = total.toFixed(1) + '%';
        badge.className = Math.abs(total - 100) < 0.1 ? 'badge bg-success' : 'badge bg-danger';
    };

})();
