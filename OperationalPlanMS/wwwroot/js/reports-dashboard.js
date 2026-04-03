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
        loadSunburst();
        loadGantt();
        loadHeatmap();
    });

    // ================================================================
    //  Charts via API
    // ================================================================
    function loadCharts() {
        var externalUnitId = document.getElementById('ExternalUnitId')?.value || '';
        var url = '/Reports/GetChartData?externalUnitId=' + externalUnitId;

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
    //  Sunburst (ECharts)
    // ================================================================
    var sunburstInstance = null;
    var sunburstData = null;
    var currentColorMode = 'status';

    var statusColors = {
        completed: '#0e7d5a',
        inprogress: '#2D4A22',
        delayed: '#b91c1c',
        notstarted: '#94a3b8'
    };

    function loadSunburst() {
        var el = document.getElementById('sunburstChart');
        if (!el || typeof echarts === 'undefined') return;

        var externalUnitId = document.getElementById('ExternalUnitId')?.value || '';
        var url = '/Reports/GetSunburstData?externalUnitId=' + externalUnitId;

        fetch(url)
            .then(function (r) { return r.ok ? r.json() : Promise.reject(r.status); })
            .then(function (data) {
                sunburstData = data;
                renderSunburst(data);
            })
            .catch(function (err) { console.error('Sunburst data error:', err); });
    }

    function getItemColor(item, mode) {
        if (item.status) {
            if (mode === 'progress') {
                var p = item.progress || 0;
                if (p >= 100) return '#0e7d5a';
                if (p >= 70) return '#3A5C2E';
                if (p >= 40) return '#C9A84C';
                if (p > 0) return '#b45309';
                return '#94a3b8';
            }
            return statusColors[item.status] || '#94a3b8';
        }
        return null;
    }

    function applyColors(nodes, mode) {
        if (!nodes) return;
        nodes.forEach(function (node) {
            if (node.status) {
                node.itemStyle = { color: getItemColor(node, mode) };
            }
            if (node.children) {
                applyColors(node.children, mode);
            }
        });
    }

    function renderSunburst(data) {
        var el = document.getElementById('sunburstChart');
        if (!el || !data || !data.length) return;

        if (sunburstInstance) { sunburstInstance.dispose(); }
        sunburstInstance = echarts.init(el);

        var treeData = JSON.parse(JSON.stringify(data));
        applyColors(treeData, currentColorMode);

        var option = {
            tooltip: {
                trigger: 'item',
                formatter: function (p) {
                    var d = p.data || {};
                    var tip = '<strong style="font-size:13px;">' + (d.name || '') + '</strong>';
                    if (d.progress !== undefined) {
                        tip += '<br/>الإنجاز: <strong>' + d.progress + '%</strong>';
                    }
                    if (d.status) {
                        var labels = { completed: 'مكتمل', inprogress: 'قيد التنفيذ', delayed: 'متأخر', notstarted: 'لم يبدأ' };
                        var colors = { completed: '#0e7d5a', inprogress: '#2D4A22', delayed: '#b91c1c', notstarted: '#94a3b8' };
                        tip += '<br/><span style="display:inline-block;width:8px;height:8px;border-radius:50%;background:' + (colors[d.status] || '#999') + ';margin-left:4px;"></span> ' + (labels[d.status] || '');
                    }
                    if (d.children) {
                        tip += '<br/>المشاريع: ' + d.children.length;
                    }
                    return tip;
                },
                textStyle: { fontFamily: 'AlQabas', fontSize: 12 },
                padding: [10, 14],
                borderRadius: 8
            },
            series: [{
                type: 'sunburst',
                data: treeData,
                radius: ['15%', '90%'],
                sort: undefined,
                emphasis: {
                    focus: 'ancestor',
                    itemStyle: { shadowBlur: 10, shadowColor: 'rgba(0,0,0,0.3)' }
                },
                levels: [
                    {},
                    {
                        r0: '15%', r: '50%',
                        itemStyle: { borderWidth: 3, borderColor: '#fff', borderRadius: 4 },
                        label: {
                            rotate: 0,
                            fontSize: 12,
                            fontFamily: 'AlQabas',
                            fontWeight: 'bold',
                            color: '#fff',
                            minAngle: 25,
                            align: 'center',
                            overflow: 'truncate',
                            ellipsis: '…',
                            width: 90
                        }
                    },
                    {
                        r0: '50%', r: '90%',
                        itemStyle: { borderWidth: 2, borderColor: '#fff', borderRadius: 3 },
                        label: {
                            rotate: 'tangential',
                            fontSize: 10,
                            fontFamily: 'AlQabas',
                            color: '#fff',
                            minAngle: 20,
                            overflow: 'truncate',
                            ellipsis: '…',
                            width: 70
                        }
                    }
                ]
            }]
        };

        sunburstInstance.setOption(option);

        sunburstInstance.on('click', function (params) {
            var d = params.data || {};
            if (d.id) {
                window.location.href = '/Projects/Details/' + d.id;
            } else if (d.children && d.children.length) {
                // Drill down — show back button
                var btn = document.getElementById('sunburstBackBtn');
                if (btn) btn.style.display = 'inline-block';
            }
        });

        // Click on center to go back
        sunburstInstance.on('mouseup', function (params) {
            if (!params.data) {
                var btn = document.getElementById('sunburstBackBtn');
                if (btn) btn.style.display = 'none';
            }
        });

        window.addEventListener('resize', function () {
            if (sunburstInstance) sunburstInstance.resize();
        });
    }

    window.resetSunburst = function () {
        if (sunburstData) {
            renderSunburst(sunburstData);
            var btn = document.getElementById('sunburstBackBtn');
            if (btn) btn.style.display = 'none';
        }
    };

    window.setSunburstColor = function (mode) {
        currentColorMode = mode;
        var btns = document.querySelectorAll('.btn-group .btn');
        btns.forEach(function (b) { b.classList.remove('active'); });
        event.target.classList.add('active');
        if (sunburstData) renderSunburst(sunburstData);
    };

    // ================================================================
    //  Gantt Timeline (ECharts)
    // ================================================================
    function loadGantt() {
        var el = document.getElementById('ganttChart');
        if (!el || typeof echarts === 'undefined') return;

        var externalUnitId = document.getElementById('ExternalUnitId')?.value || '';
        fetch('/Reports/GetGanttData?externalUnitId=' + externalUnitId)
            .then(function (r) { return r.ok ? r.json() : Promise.reject(r.status); })
            .then(function (data) { renderGantt(data, el); })
            .catch(function (err) { console.error('Gantt error:', err); });
    }

    function renderGantt(data, el) {
        if (!data || !data.length) {
            el.innerHTML = '<div style="text-align:center;padding:3rem;color:#999;">لا توجد مشاريع بتواريخ مخطط لها</div>';
            return;
        }

        var statusColors = {
            completed: { bg: '#d1fae5', fill: '#0e7d5a' },
            inprogress: { bg: '#e8f0e0', fill: '#2D4A22' },
            delayed: { bg: '#fee2e2', fill: '#b91c1c' },
            notstarted: { bg: '#f1f5f9', fill: '#94a3b8' }
        };
        var statusLabels = { completed: 'مكتمل', inprogress: 'قيد التنفيذ', delayed: 'متأخر', notstarted: 'لم يبدأ' };

        var categories = data.map(function (d) { return d.name; });
        var chartHeight = Math.max(280, data.length * 44 + 80);
        el.style.height = chartHeight + 'px';

        var chart = echarts.init(el);
        var today = new Date().getTime();

        // شريط الخلفية (المدة الكاملة)
        var bgBars = data.map(function (d, i) {
            var c = statusColors[d.status] || statusColors.notstarted;
            return {
                value: [i, new Date(d.plannedStart).getTime(), new Date(d.plannedEnd).getTime(), d.progress, d.status],
                itemStyle: { color: c.bg, borderColor: c.fill, borderWidth: 1, borderRadius: 4 }
            };
        });

        // شريط الإنجاز
        var fillBars = data.map(function (d, i) {
            var c = statusColors[d.status] || statusColors.notstarted;
            var start = new Date(d.plannedStart).getTime();
            var end = new Date(d.plannedEnd).getTime();
            var progressEnd = start + ((end - start) * d.progress / 100);
            return {
                value: [i, start, progressEnd, d.progress, d.status],
                itemStyle: { color: c.fill, borderRadius: 4 }
            };
        });

        var renderBar = function (params, api) {
            var catIdx = api.value(0);
            var start = api.coord([api.value(1), catIdx]);
            var end = api.coord([api.value(2), catIdx]);
            var height = api.size([0, 1])[1] * 0.55;
            return {
                type: 'rect',
                shape: { x: start[0], y: start[1] - height / 2, width: Math.max(end[0] - start[0], 2), height: height },
                style: api.style()
            };
        };

        // حساب حدود المحور
        var allStarts = data.map(function (d) { return new Date(d.plannedStart).getTime(); });
        var allEnds = data.map(function (d) { return new Date(d.plannedEnd).getTime(); });
        var xMin = Math.min.apply(null, allStarts);
        var xMax = Math.max.apply(null, allEnds);
        var padding = (xMax - xMin) * 0.05;

        var option = {
            tooltip: {
                formatter: function (p) {
                    if (!p.value) return '';
                    var d = data[p.value[0]];
                    if (!d) return '';
                    var c = statusColors[d.status] || statusColors.notstarted;
                    return '<strong>' + d.name + '</strong><br/>'
                        + 'البداية: ' + d.plannedStart + '<br/>'
                        + 'النهاية: ' + d.plannedEnd + '<br/>'
                        + 'الإنجاز: <strong>' + d.progress + '%</strong><br/>'
                        + '<span style="color:' + c.fill + ';">●</span> ' + (statusLabels[d.status] || '');
                },
                textStyle: { fontFamily: 'AlQabas', fontSize: 12 }
            },
            grid: { left: '20%', right: '5%', top: '4%', bottom: '8%' },
            xAxis: {
                type: 'time',
                min: xMin - padding,
                max: xMax + padding,
                axisLabel: { fontFamily: 'AlQabas', fontSize: 10 }
            },
            yAxis: {
                type: 'category',
                data: categories,
                inverse: true,
                axisLabel: { fontFamily: 'AlQabas', fontSize: 11, width: 150, overflow: 'truncate' }
            },
            series: [
                {
                    type: 'custom',
                    renderItem: renderBar,
                    encode: { x: [1, 2], y: 0 },
                    data: bgBars,
                    z: 1
                },
                {
                    type: 'custom',
                    renderItem: renderBar,
                    encode: { x: [1, 2], y: 0 },
                    data: fillBars,
                    z: 2,
                    label: {
                        show: true,
                        position: 'insideRight',
                        formatter: function (p) { return p.value[3] > 0 ? p.value[3] + '%' : ''; },
                        fontSize: 10,
                        fontFamily: 'AlQabas',
                        fontWeight: 'bold',
                        color: '#fff'
                    },
                    markLine: {
                        silent: true,
                        symbol: 'none',
                        lineStyle: { color: '#C9A84C', width: 2, type: 'dashed' },
                        data: [{ xAxis: today }],
                        label: { show: false }
                    }
                }
            ]
        };

        chart.setOption(option);

        chart.on('click', function (params) {
            if (params.value) {
                var d = data[params.value[0]];
                if (d && d.id) window.location.href = '/Projects/Details/' + d.id;
            }
        });

        window.addEventListener('resize', function () { chart.resize(); });
    }

    // ================================================================
    //  Heatmap (ECharts)
    // ================================================================
    function loadHeatmap() {
        var el = document.getElementById('heatmapChart');
        if (!el || typeof echarts === 'undefined') return;

        var externalUnitId = document.getElementById('ExternalUnitId')?.value || '';
        fetch('/Reports/GetHeatmapData?externalUnitId=' + externalUnitId)
            .then(function (r) { return r.ok ? r.json() : Promise.reject(r.status); })
            .then(function (data) { renderHeatmap(data, el); })
            .catch(function (err) { console.error('Heatmap error:', err); });
    }

    function renderHeatmap(data, el) {
        if (!data || !data.rows || !data.rows.length) {
            el.innerHTML = '<div style="text-align:center;padding:3rem;color:#999;">لا توجد بيانات نشاط شهري</div>';
            return;
        }

        var chartHeight = Math.max(250, data.rows.length * 45 + 100);
        el.style.height = chartHeight + 'px';

        var chart = echarts.init(el);

        var initiatives = data.rows.map(function (r) { return r.name; });
        var maxVal = 0;
        var heatData = [];

        data.rows.forEach(function (row, yIdx) {
            row.data.forEach(function (val, xIdx) {
                heatData.push([xIdx, yIdx, val]);
                if (val > maxVal) maxVal = val;
            });
        });

        if (maxVal === 0) maxVal = 1;

        var option = {
            tooltip: {
                formatter: function (p) {
                    var month = data.months[p.value[0]];
                    var initiative = initiatives[p.value[1]];
                    var count = p.value[2];
                    return '<strong>' + initiative + '</strong><br/>'
                        + month + ': <strong>' + count + '</strong> خطوة';
                },
                textStyle: { fontFamily: 'AlQabas', fontSize: 12 }
            },
            grid: { left: '20%', right: '6%', top: '8%', bottom: '15%' },
            xAxis: {
                type: 'category',
                data: data.months,
                splitArea: { show: true },
                axisLabel: { fontFamily: 'AlQabas', fontSize: 10 }
            },
            yAxis: {
                type: 'category',
                data: initiatives,
                inverse: true,
                axisLabel: { fontFamily: 'AlQabas', fontSize: 11, width: 130, overflow: 'truncate' }
            },
            visualMap: {
                min: 0,
                max: maxVal,
                calculable: false,
                orient: 'horizontal',
                left: 'center',
                bottom: '0%',
                inRange: {
                    color: ['#f1f5f9', '#a7f3d0', '#3A5C2E', '#1B3A2A']
                },
                textStyle: { fontFamily: 'AlQabas', fontSize: 10 }
            },
            series: [{
                type: 'heatmap',
                data: heatData,
                label: {
                    show: true,
                    fontSize: 11,
                    fontFamily: 'AlQabas',
                    formatter: function (p) { return p.value[2] > 0 ? p.value[2] : ''; }
                },
                emphasis: {
                    itemStyle: { shadowBlur: 6, shadowColor: 'rgba(0,0,0,0.3)' }
                },
                itemStyle: { borderColor: '#fff', borderWidth: 2, borderRadius: 3 }
            }]
        };

        chart.setOption(option);
        window.addEventListener('resize', function () { chart.resize(); });
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
