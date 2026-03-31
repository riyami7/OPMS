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
        OrgTreePicker.init({
            containerId: 'filterOrgTree',
            hiddenInputId: 'ExternalUnitId',
            selectedId: selectedExternalUnitId,
            rootCode: '00001'
        });
        loadCharts();
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
    //  Unit Filter — handled by OrgTreePicker
    // ================================================================

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
