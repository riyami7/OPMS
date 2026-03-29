/**
 * reports-dashboard.js
 * ====================
 * Charts, unit filter, and drill-down for Reports/Index.
 *
 * Requires window.ReportsConfig:
 *   window.ReportsConfig = { selectedExternalUnitId: '' }
 * Requires Chart.js loaded before this script.
 */

(function () {
    'use strict';

    var config = window.ReportsConfig || {};
    var allUnits = [];
    var selectedExternalUnitId = config.selectedExternalUnitId || '';
    var chartDonut = null;
    var chartUnits = null;
    var chartLine = null;

    // ================================================================
    //  Init
    // ================================================================
    document.addEventListener('DOMContentLoaded', function () {
        loadOrganizationalUnits();
        loadCharts();
        document.getElementById('filterLevel1').addEventListener('change', onLevel1Change);
        document.getElementById('filterLevel2').addEventListener('change', onLevel2Change);
        document.getElementById('filterLevel3').addEventListener('change', onLevel3Change);
    });

    // ================================================================
    //  Charts via API
    // ================================================================
    function loadCharts() {
        var fiscalYearId = document.querySelector('[name="FiscalYearId"]')?.value || '';
        var externalUnitId = document.getElementById('ExternalUnitId')?.value || '';
        var url = '/Reports/GetChartData?fiscalYearId=' + fiscalYearId + '&externalUnitId=' + externalUnitId;

        fetch(url)
            .then(function (r) { return r.ok ? r.json() : Promise.reject(r.status); })
            .then(function (data) {
                renderDonut(data.donut);
                renderUnitsBar(data.units);
                renderMonthLine(data.monthly);
            })
            .catch(function (err) { console.error('Chart data error:', err); });
    }

    function renderDonut(d) {
        var ctx = document.getElementById('projectStatusChart');
        if (!ctx) return;
        if (chartDonut) { chartDonut.destroy(); }
        chartDonut = new Chart(ctx, {
            type: 'doughnut',
            data: {
                labels: ['مكتمل', 'قيد التنفيذ', 'متأخر', 'لم يبدأ'],
                datasets: [{
                    data: [d.completed, d.inProgress, d.delayed, d.notStarted],
                    backgroundColor: ['#0e7d5a', '#2D4A22', '#b91c1c', '#94a3b8'],
                    borderWidth: 0,
                    hoverOffset: 6
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: true,
                cutout: '65%',
                onClick: function (e, els) {
                    if (!els.length) return;
                    var s = ['completed', 'inprogress', 'delayed', 'notstarted'];
                    var l = ['مكتمل', 'قيد التنفيذ', 'متأخر', 'لم يبدأ'];
                    var c = ['#0e7d5a', '#2D4A22', '#b91c1c', '#94a3b8'];
                    var n = [d.completed, d.inProgress, d.delayed, d.notStarted];
                    var i = els[0].index;
                    window.showStatusDrillDown(s[i], l[i], c[i], n[i]);
                },
                plugins: {
                    legend: { position: 'bottom', labels: { font: { family: 'AlQabas', size: 11 }, padding: 10, usePointStyle: true } }
                }
            }
        });
    }

    function renderUnitsBar(units) {
        var ctx = document.getElementById('unitPerformanceChart');
        if (!ctx || !units || !units.length) return;
        if (chartUnits) { chartUnits.destroy(); }
        chartUnits = new Chart(ctx, {
            type: 'bar',
            data: {
                labels: units.map(function (u) { return u.label; }),
                datasets: [{
                    label: 'الإنجاز %',
                    data: units.map(function (u) { return u.value; }),
                    backgroundColor: units.map(function (u) { return u.color; }),
                    borderRadius: 5,
                    barThickness: 22
                }]
            },
            options: {
                indexAxis: 'y',
                responsive: true,
                maintainAspectRatio: true,
                onClick: function (e, els) {
                    if (!els.length) return;
                    var u = units[els[0].index];
                    window.showUnitDrillDown(u.label, u.unitId);
                },
                scales: {
                    x: { beginAtZero: true, max: 100, grid: { display: false }, ticks: { callback: function (v) { return v + '%'; }, font: { family: 'AlQabas' } } },
                    y: { grid: { display: false }, ticks: { font: { family: 'AlQabas', size: 11 } } }
                },
                plugins: {
                    legend: { display: false },
                    tooltip: { callbacks: { label: function (ctx) { return ctx.parsed.x + '%'; } } }
                }
            }
        });
    }

    function renderMonthLine(monthly) {
        var ctx = document.getElementById('progressLineChart');
        if (!ctx || !monthly || !monthly.length) return;
        if (chartLine) { chartLine.destroy(); }
        chartLine = new Chart(ctx, {
            type: 'line',
            data: {
                labels: monthly.map(function (m) { return m.label; }),
                datasets: [
                    {
                        label: 'الإنجاز الفعلي',
                        data: monthly.map(function (m) { return m.actual; }),
                        borderColor: '#2D4A22',
                        backgroundColor: 'rgba(26,58,92,0.07)',
                        borderWidth: 2.5,
                        tension: 0.4,
                        fill: true,
                        pointBackgroundColor: '#2D4A22',
                        pointRadius: 4
                    },
                    {
                        label: 'المخطط',
                        data: monthly.map(function (m) { return m.planned; }),
                        borderColor: '#c9a84c',
                        backgroundColor: 'transparent',
                        borderWidth: 2,
                        borderDash: [6, 3],
                        tension: 0.4,
                        pointBackgroundColor: '#c9a84c',
                        pointRadius: 3
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: true,
                scales: {
                    y: { beginAtZero: true, max: 100, ticks: { callback: function (v) { return v + '%'; }, font: { family: 'AlQabas' } }, grid: { color: '#f1f5f9' } },
                    x: { ticks: { font: { family: 'AlQabas' } }, grid: { display: false } }
                },
                plugins: {
                    legend: { position: 'top', labels: { font: { family: 'AlQabas', size: 11 }, usePointStyle: true } },
                    tooltip: { callbacks: { label: function (ctx) { return ctx.dataset.label + ': ' + ctx.parsed.y + '%'; } } }
                }
            }
        });
    }

    // ================================================================
    //  Unit Filter (3-level cascade)
    // ================================================================
    function loadOrganizationalUnits() {
        fetch('/api/OrganizationApi/units/all')
            .then(function (r) { return r.ok ? r.json() : Promise.reject(); })
            .then(function (data) {
                allUnits = data;
                populateFilterLevel1();
                if (selectedExternalUnitId) { restoreFilterSelection(selectedExternalUnitId); }
            })
            .catch(function () {
                document.getElementById('filterLevel1').innerHTML = '<option value="">-- فشل التحميل --</option>';
            });
    }

    function populateFilterLevel1() {
        var s = document.getElementById('filterLevel1');
        s.innerHTML = '<option value="">-- جميع الوحدات --</option>';
        allUnits.filter(function (u) { return u.code == '00001' && (!u.parentId || u.parentId === 0); }).forEach(function (u) {
            var o = document.createElement('option'); o.value = u.id; o.textContent = u.name; s.appendChild(o);
        });
    }

    function fillSelect(el, items, ph) {
        el.innerHTML = '<option value="">' + ph + '</option>';
        items.forEach(function (u) {
            var o = document.createElement('option'); o.value = u.id; o.textContent = u.name; el.appendChild(o);
        });
    }

    function onLevel1Change() {
        var id = this.value;
        var l2 = document.getElementById('filterLevel2');
        var l3 = document.getElementById('filterLevel3');
        l2.disabled = true;
        l3.innerHTML = '<option value="">-- اختر الثاني --</option>';
        l3.disabled = true;
        if (id) {
            var ch = allUnits.filter(function (u) { return u.parentId === id; });
            fillSelect(l2, ch, ch.length ? '-- اختر --' : '-- لا توجد فروع --');
            l2.disabled = !ch.length;
            updateSelectedUnit(id);
        } else {
            l2.innerHTML = '<option value="">-- اختر الأول --</option>';
            document.getElementById('ExternalUnitId').value = '';
            updateUnitDisplay('');
        }
    }

    function onLevel2Change() {
        var id = this.value;
        var l3 = document.getElementById('filterLevel3');
        l3.disabled = true;
        if (id) {
            var ch = allUnits.filter(function (u) { return u.parentId === id; });
            fillSelect(l3, ch, ch.length ? '-- اختر --' : '-- لا توجد أقسام --');
            l3.disabled = !ch.length;
            updateSelectedUnit(id);
        } else {
            l3.innerHTML = '<option value="">-- اختر الثاني --</option>';
            var l1v = document.getElementById('filterLevel1').value;
            if (l1v) { updateSelectedUnit(l1v); }
        }
    }

    function onLevel3Change() {
        var id = this.value;
        if (id) { updateSelectedUnit(id); }
        else {
            var l2v = document.getElementById('filterLevel2').value;
            if (l2v) { updateSelectedUnit(l2v); }
        }
    }

    function updateSelectedUnit(id) {
        document.getElementById('ExternalUnitId').value = id;
        var u = allUnits.find(function (x) { return x.id === id; });
        if (u) { updateUnitDisplay(u.name); }
    }

    function updateUnitDisplay(n) {
        var d = document.getElementById('selectedUnitDisplay');
        d.innerHTML = n
            ? '<span class="badge badge-approved" style="font-size:.82rem;padding:6px 12px;"><i class="bi bi-building me-1"></i>' + n + '</span>'
            : '';
    }

    function restoreFilterSelection(unitId) {
        var unit = allUnits.find(function (u) { return u.id === unitId; });
        if (!unit) return;
        var hierarchy = [], current = unit;
        while (current) {
            hierarchy.unshift(current);
            var pid = current.parentId;
            current = allUnits.find(function (u) { return u.id === pid; });
        }
        document.getElementById('filterLevel1').value = hierarchy[0].id;
        onLevel1Change.call(document.getElementById('filterLevel1'));
        setTimeout(function () {
            if (hierarchy.length >= 2) {
                document.getElementById('filterLevel2').value = hierarchy[1].id;
                onLevel2Change.call(document.getElementById('filterLevel2'));
                setTimeout(function () {
                    if (hierarchy.length >= 3) {
                        document.getElementById('filterLevel3').value = hierarchy[2].id;
                        updateSelectedUnit(hierarchy[2].id);
                    }
                }, 100);
            }
        }, 100);
    }

    // ================================================================
    //  Drill-Down modals
    // ================================================================
    window.showStatusDrillDown = function (status, label, color, count) {
        var modal = new bootstrap.Modal(document.getElementById('drillDownModal'));
        document.getElementById('drillDownLabel').innerHTML =
            '<span class="badge" style="background:' + color + '">' + count + '</span> ' + label;
        document.getElementById('drillDownLink').href = '/Projects?status=' + status;
        if (status === 'delayed') {
            document.getElementById('drillDownContent').innerHTML =
                document.getElementById('delayedDrillData').innerHTML;
        } else {
            document.getElementById('drillDownContent').innerHTML =
                '<div class="text-center py-4">' +
                '<span class="badge fs-1" style="background:' + color + ';padding:1rem 2rem;">' + count + '</span>' +
                '<p class="text-muted mt-3">اضغط "عرض الكل" للقائمة الكاملة</p></div>';
        }
        modal.show();
    };

    window.showUnitDrillDown = function (unitName, unitId) {
        var modal = new bootstrap.Modal(document.getElementById('drillDownModal'));
        document.getElementById('drillDownLabel').innerHTML = '<i class="bi bi-building me-1"></i> ' + unitName;
        document.getElementById('drillDownLink').href = '/Initiatives?ExternalUnitId=' + unitId;
        document.getElementById('drillDownContent').innerHTML =
            '<div class="row g-3 p-2">' +
            '<div class="col-6"><a href="/Initiatives?ExternalUnitId=' + unitId + '" class="card text-decoration-none h-100">' +
            '<div class="card-body text-center py-4"><i class="bi bi-lightning-charge text-warning" style="font-size:2rem;"></i>' +
            '<h6 class="mt-2 text-dark">المبادرات</h6></div></a></div>' +
            '<div class="col-6"><a href="/Projects?ExternalUnitId=' + unitId + '" class="card text-decoration-none h-100">' +
            '<div class="card-body text-center py-4"><i class="bi bi-folder text-success" style="font-size:2rem;"></i>' +
            '<h6 class="mt-2 text-dark">المشاريع</h6></div></a></div></div>';
        modal.show();
    };

})();
