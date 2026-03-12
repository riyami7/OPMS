/**
 * safe-dom.js — دوال آمنة للتعامل مع DOM
 * تمنع XSS عبر استخدام textContent بدل innerHTML
 * 
 * الاستخدام:
 *   safePopulateSelect(selectElement, items, { valueProp: 'id', textProp: 'name', placeholder: '-- اختر --' });
 *   safeClearSelect(selectElement, '-- اختر --');
 *   safeSetText(element, text);
 */

/**
 * تعبئة select بطريقة آمنة بدون innerHTML
 * @param {HTMLSelectElement} select - عنصر الـ select
 * @param {Array} items - مصفوفة البيانات
 * @param {Object} options - خيارات التعبئة
 * @param {string} options.valueProp - اسم الخاصية للقيمة (افتراضي: 'id')
 * @param {string} options.textProp - اسم الخاصية للنص (افتراضي: 'name')
 * @param {string} [options.placeholder] - نص الخيار الأول الفارغ
 * @param {string} [options.dataProp] - اسم خاصية data attribute إضافية
 * @param {string} [options.dataAttr] - اسم الـ data attribute (مثل 'data-name')
 * @param {string|number} [options.selectedValue] - القيمة المختارة مسبقاً
 */
function safePopulateSelect(select, items, options = {}) {
    if (!select) return;

    const valueProp = options.valueProp || 'id';
    const textProp = options.textProp || 'name';

    // تفريغ القائمة
    select.length = 0;

    // إضافة placeholder
    if (options.placeholder !== undefined) {
        const placeholderOpt = document.createElement('option');
        placeholderOpt.value = '';
        placeholderOpt.textContent = options.placeholder;
        select.appendChild(placeholderOpt);
    }

    // إضافة العناصر
    if (items && items.length > 0) {
        items.forEach(item => {
            const opt = document.createElement('option');
            opt.value = item[valueProp];
            opt.textContent = item[textProp];

            if (options.dataAttr && options.dataProp) {
                opt.setAttribute(options.dataAttr, item[options.dataProp]);
            }

            if (options.selectedValue !== undefined &&
                String(item[valueProp]) === String(options.selectedValue)) {
                opt.selected = true;
            }

            select.appendChild(opt);
        });
    }
}

/**
 * تفريغ select مع placeholder
 */
function safeClearSelect(select, placeholder) {
    if (!select) return;
    select.length = 0;
    if (placeholder) {
        const opt = document.createElement('option');
        opt.value = '';
        opt.textContent = placeholder;
        select.appendChild(opt);
    }
}

/**
 * تعيين نص بطريقة آمنة
 */
function safeSetText(element, text) {
    if (!element) return;
    element.textContent = text || '';
}

/**
 * إنشاء عنصر HTML بطريقة آمنة
 * @param {string} tag - نوع العنصر
 * @param {Object} attrs - الخصائص
 * @param {string} text - النص الداخلي
 * @returns {HTMLElement}
 */
function safeCreateElement(tag, attrs = {}, text = '') {
    const el = document.createElement(tag);
    Object.entries(attrs).forEach(([key, value]) => {
        if (key === 'className') {
            el.className = value;
        } else {
            el.setAttribute(key, value);
        }
    });
    if (text) el.textContent = text;
    return el;
}
