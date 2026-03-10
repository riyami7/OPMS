/**
 * Organization Cascade Dropdowns
 * للتعامل مع الهيكل التنظيمي الهرمي
 */

const OrganizationApi = {
    // Base URL للـ API
    baseUrl: '/api/OrganizationApi',

    /**
     * جلب الوحدات الجذرية (المستوى الأول)
     */
    async getRootUnits() {
        const response = await fetch(`${this.baseUrl}/units/root`);
        return await response.json();
    },

    /**
     * جلب الوحدات الفرعية
     */
    async getChildUnits(parentId) {
        const response = await fetch(`${this.baseUrl}/units/children/${parentId}`);
        return await response.json();
    },

    /**
     * جلب جميع الوحدات
     */
    async getAllUnits() {
        const response = await fetch(`${this.baseUrl}/units/all`);
        return await response.json();
    },

    /**
     * البحث عن موظفين
     */
    async searchEmployees(term) {
        const response = await fetch(`${this.baseUrl}/employees/search?term=${encodeURIComponent(term)}`);
        return await response.json();
    },

    /**
     * جلب جميع الموظفين
     */
    async getAllEmployees() {
        const response = await fetch(`${this.baseUrl}/employees/all`);
        return await response.json();
    }
};

/**
 * تهيئة Cascade Dropdowns للهيكل التنظيمي
 * @param {Object} config - إعدادات الـ Dropdowns
 */
function initOrganizationCascade(config) {
    const {
        level1Selector,     // #level1
        level2Selector,     // #level2
        level3Selector,     // #level3
        onSelectionChange   // callback عند تغيير الاختيار
    } = config;

    const level1 = document.querySelector(level1Selector);
    const level2 = document.querySelector(level2Selector);
    const level3 = document.querySelector(level3Selector);

    // تحميل المستوى الأول
    loadLevel1();

    // عند تغيير المستوى الأول
    level1?.addEventListener('change', async function() {
        const parentId = this.value;
        
        // مسح المستويات التالية
        clearDropdown(level2, 'اختر...');
        clearDropdown(level3, 'اختر...');
        
        if (parentId) {
            await loadChildUnits(level2, parentId);
        }

        if (onSelectionChange) {
            onSelectionChange({ level: 1, value: parentId });
        }
    });

    // عند تغيير المستوى الثاني
    level2?.addEventListener('change', async function() {
        const parentId = this.value;
        
        // مسح المستوى الثالث
        clearDropdown(level3, 'اختر...');
        
        if (parentId) {
            await loadChildUnits(level3, parentId);
        }

        if (onSelectionChange) {
            onSelectionChange({ level: 2, value: parentId });
        }
    });

    // عند تغيير المستوى الثالث
    level3?.addEventListener('change', function() {
        if (onSelectionChange) {
            onSelectionChange({ level: 3, value: this.value });
        }
    });

    // دوال مساعدة
    async function loadLevel1() {
        try {
            const units = await OrganizationApi.getRootUnits();
            populateDropdown(level1, units, 'اختر المستوى الأول...');
        } catch (error) {
            console.error('Error loading level 1:', error);
        }
    }

    async function loadChildUnits(dropdown, parentId) {
        try {
            dropdown.disabled = true;
            const units = await OrganizationApi.getChildUnits(parentId);
            populateDropdown(dropdown, units, 'اختر...');
            dropdown.disabled = false;
        } catch (error) {
            console.error('Error loading child units:', error);
            dropdown.disabled = false;
        }
    }
}

/**
 * تعبئة Dropdown
 */
function populateDropdown(dropdown, items, placeholder = 'اختر...') {
    if (!dropdown) return;

    dropdown.innerHTML = `<option value="">${placeholder}</option>`;
    
    items.forEach(item => {
        const option = document.createElement('option');
        option.value = item.id;
        option.textContent = item.name;
        option.dataset.code = item.code || '';
        dropdown.appendChild(option);
    });
}

/**
 * مسح Dropdown
 */
function clearDropdown(dropdown, placeholder = 'اختر...') {
    if (!dropdown) return;
    dropdown.innerHTML = `<option value="">${placeholder}</option>`;
}

/**
 * تهيئة البحث عن موظفين مع Select2
 * @param {string} selector - محدد العنصر
 * @param {Object} options - خيارات إضافية
 */
function initEmployeeSearch(selector, options = {}) {
    const element = document.querySelector(selector);
    if (!element) return;

    // إذا كان Select2 متاحاً
    if (typeof $ !== 'undefined' && $.fn.select2) {
        $(selector).select2({
            dir: 'rtl',
            language: 'ar',
            placeholder: options.placeholder || 'ابحث عن موظف...',
            allowClear: true,
            minimumInputLength: 0,
            ajax: {
                url: '/api/OrganizationApi/employees/search',
                dataType: 'json',
                delay: 300,
                data: function(params) {
                    return { term: params.term || '' };
                },
                processResults: function(data) {
                    return {
                        results: data.map(emp => ({
                            id: emp.empNumber,
                            text: emp.displayName || `${emp.rank || ''} ${emp.name}`.trim(),
                            employee: emp
                        }))
                    };
                }
            },
            templateResult: formatEmployee,
            templateSelection: formatEmployeeSelection
        });
    } else {
        // Fallback بدون Select2
        initSimpleEmployeeSearch(selector, options);
    }
}

/**
 * تنسيق عرض الموظف في القائمة
 */
function formatEmployee(employee) {
    if (!employee.employee) return employee.text;

    const emp = employee.employee;
    return $(`
        <div class="employee-option">
            <div class="employee-name">${emp.rank || ''} ${emp.name}</div>
            <small class="text-muted">${emp.position || ''} - ${emp.unit || ''}</small>
        </div>
    `);
}

/**
 * تنسيق عرض الموظف المختار
 */
function formatEmployeeSelection(employee) {
    return employee.text || employee.id;
}

/**
 * بحث بسيط بدون Select2
 */
async function initSimpleEmployeeSearch(selector, options = {}) {
    const container = document.querySelector(selector)?.parentElement;
    if (!container) return;

    const input = document.createElement('input');
    input.type = 'text';
    input.className = 'form-control';
    input.placeholder = options.placeholder || 'ابحث عن موظف...';

    const dropdown = document.createElement('div');
    dropdown.className = 'employee-search-dropdown';
    dropdown.style.cssText = 'position:absolute;width:100%;max-height:200px;overflow-y:auto;background:white;border:1px solid #ddd;border-radius:4px;display:none;z-index:1000;';

    const hiddenInput = document.querySelector(selector);
    hiddenInput.type = 'hidden';

    container.style.position = 'relative';
    container.insertBefore(input, hiddenInput);
    container.appendChild(dropdown);

    let timeout;
    input.addEventListener('input', function() {
        clearTimeout(timeout);
        timeout = setTimeout(async () => {
            const term = this.value;
            if (term.length < 1) {
                dropdown.style.display = 'none';
                return;
            }

            const employees = await OrganizationApi.searchEmployees(term);
            showEmployeeDropdown(dropdown, employees, hiddenInput, input);
        }, 300);
    });

    function showEmployeeDropdown(dropdown, employees, hiddenInput, input) {
        dropdown.innerHTML = '';
        
        if (employees.length === 0) {
            dropdown.innerHTML = '<div class="p-2 text-muted">لا توجد نتائج</div>';
            dropdown.style.display = 'block';
            return;
        }

        employees.forEach(emp => {
            const item = document.createElement('div');
            item.className = 'p-2 border-bottom cursor-pointer';
            item.style.cursor = 'pointer';
            item.innerHTML = `
                <div>${emp.rank || ''} ${emp.empName}</div>
                <small class="text-muted">${emp.position || ''}</small>
            `;
            item.addEventListener('click', () => {
                hiddenInput.value = emp.empNumber;
                input.value = `${emp.rank || ''} ${emp.empName}`.trim();
                dropdown.style.display = 'none';
            });
            dropdown.appendChild(item);
        });

        dropdown.style.display = 'block';
    }

    // إخفاء القائمة عند النقر خارجها
    document.addEventListener('click', (e) => {
        if (!container.contains(e.target)) {
            dropdown.style.display = 'none';
        }
    });
}


// ========== مثال على الاستخدام ==========
/*
// في صفحة إضافة المشروع:
document.addEventListener('DOMContentLoaded', function() {
    // تهيئة Cascade للهيكل التنظيمي
    initOrganizationCascade({
        level1Selector: '#Level1UnitId',
        level2Selector: '#Level2UnitId',
        level3Selector: '#Level3UnitId',
        onSelectionChange: function(data) {
            console.log('Selection changed:', data);
        }
    });

    // تهيئة البحث عن مدير المشروع
    initEmployeeSearch('#ProjectManagerId', {
        placeholder: 'ابحث عن مدير المشروع...'
    });
});
*/
