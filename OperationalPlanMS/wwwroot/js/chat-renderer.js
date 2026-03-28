/**
 * chat-renderer.js — Markdown + Chart.js rendering for OPMS Chat
 * 
 * Shared between /Chat page and _ChatWidget bubble.
 * Requires: marked.js (loaded before this script)
 * Optional: Chart.js (for inline charts)
 * 
 * Usage:
 *   ChatRenderer.renderMarkdown(text)     → returns sanitized HTML string
 *   ChatRenderer.processCharts(container) → finds ```chart blocks and renders Chart.js
 */
var ChatRenderer = (function () {
    'use strict';

    // ═══════════════════════════════════════════════════════════
    //  Markdown Configuration
    // ═══════════════════════════════════════════════════════════

    var isMarkedAvailable = typeof marked !== 'undefined';

    if (isMarkedAvailable) {
        marked.setOptions({
            breaks: true,       // Convert \n to <br>
            gfm: true,          // GitHub Flavored Markdown (tables, strikethrough)
            headerIds: false,   // Don't add IDs to headers
            mangle: false       // Don't mangle email addresses
        });
    }

    // ═══════════════════════════════════════════════════════════
    //  Simple HTML Sanitizer
    // ═══════════════════════════════════════════════════════════

    var ALLOWED_TAGS = [
        'p', 'br', 'strong', 'b', 'em', 'i', 'u', 's', 'del',
        'h1', 'h2', 'h3', 'h4', 'h5', 'h6',
        'ul', 'ol', 'li',
        'table', 'thead', 'tbody', 'tr', 'th', 'td',
        'blockquote', 'pre', 'code',
        'hr', 'a', 'span', 'div',
        'sup', 'sub'
    ];

    var ALLOWED_ATTRS = {
        'a': ['href', 'title'],
        'td': ['align'],
        'th': ['align'],
        'code': ['class'],
        'span': ['class'],
        'div': ['class', 'data-chart-config']
    };

    function sanitizeHtml(html) {
        var temp = document.createElement('div');
        temp.innerHTML = html;
        sanitizeNode(temp);
        return temp.innerHTML;
    }

    function sanitizeNode(node) {
        var children = Array.from(node.childNodes);
        for (var i = 0; i < children.length; i++) {
            var child = children[i];
            if (child.nodeType === 1) { // Element
                var tag = child.tagName.toLowerCase();
                if (ALLOWED_TAGS.indexOf(tag) === -1) {
                    // Replace disallowed tag with its text content
                    var text = document.createTextNode(child.textContent);
                    node.replaceChild(text, child);
                } else {
                    // Remove disallowed attributes
                    var attrs = Array.from(child.attributes);
                    for (var j = 0; j < attrs.length; j++) {
                        var attrName = attrs[j].name.toLowerCase();
                        var allowed = ALLOWED_ATTRS[tag] || [];
                        if (allowed.indexOf(attrName) === -1) {
                            child.removeAttribute(attrs[j].name);
                        }
                    }
                    // Sanitize href to prevent javascript: URLs
                    if (tag === 'a' && child.hasAttribute('href')) {
                        var href = child.getAttribute('href');
                        if (href && href.trim().toLowerCase().indexOf('javascript:') === 0) {
                            child.removeAttribute('href');
                        } else {
                            child.setAttribute('target', '_blank');
                            child.setAttribute('rel', 'noopener noreferrer');
                        }
                    }
                    sanitizeNode(child);
                }
            }
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  Markdown → HTML Renderer
    // ═══════════════════════════════════════════════════════════

    function renderMarkdown(text) {
        if (!text) return '';

        if (!isMarkedAvailable) {
            // Fallback: basic escaping with newlines
            return escapeHtml(text).replace(/\n/g, '<br>');
        }

        // Pre-process: extract ```chart blocks before markdown parsing
        var chartBlocks = [];
        text = text.replace(/```chart\s*\n([\s\S]*?)```/g, function (match, config) {
            var placeholder = '<!--CHART_PLACEHOLDER_' + chartBlocks.length + '-->';
            chartBlocks.push(config.trim());
            return placeholder;
        });

        // Parse markdown
        var html = marked.parse(text);

        // Sanitize
        html = sanitizeHtml(html);

        // Re-inject chart placeholders as data containers
        for (var i = 0; i < chartBlocks.length; i++) {
            var placeholder = '&lt;!--CHART_PLACEHOLDER_' + i + '--&gt;';
            // Also check non-escaped version
            var placeholder2 = '<!--CHART_PLACEHOLDER_' + i + '-->';
            var chartDiv = '<div class="chat-chart-container" data-chart-config="' +
                escapeAttr(chartBlocks[i]) + '"><canvas></canvas><div class="chat-chart-loading">جاري رسم الرسم البياني...</div></div>';
            html = html.replace(placeholder, chartDiv);
            html = html.replace(placeholder2, chartDiv);
        }

        return html;
    }

    // ═══════════════════════════════════════════════════════════
    //  Chart.js Processing
    // ═══════════════════════════════════════════════════════════

    function processCharts(container) {
        if (typeof Chart === 'undefined') return;

        var chartContainers = container.querySelectorAll('.chat-chart-container[data-chart-config]');

        for (var i = 0; i < chartContainers.length; i++) {
            var el = chartContainers[i];
            if (el.dataset.chartRendered) continue;
            el.dataset.chartRendered = 'true';

            var configStr = el.getAttribute('data-chart-config');
            var canvas = el.querySelector('canvas');
            var loadingEl = el.querySelector('.chat-chart-loading');

            try {
                var config = JSON.parse(configStr);

                // Apply RTL-friendly defaults
                config = applyChartDefaults(config);

                new Chart(canvas.getContext('2d'), config);

                if (loadingEl) loadingEl.style.display = 'none';
            } catch (e) {
                console.error('Chat chart error:', e);
                if (loadingEl) {
                    loadingEl.textContent = 'خطأ في رسم الرسم البياني';
                    loadingEl.style.color = '#b91c1c';
                }
            }
        }
    }

    function applyChartDefaults(config) {
        if (!config.options) config.options = {};
        if (!config.options.plugins) config.options.plugins = {};
        if (!config.options.plugins.legend) config.options.plugins.legend = {};

        // RTL-friendly defaults
        config.options.responsive = true;
        config.options.maintainAspectRatio = true;

        // Arabic-friendly font
        if (!config.options.plugins.legend.labels) config.options.plugins.legend.labels = {};
        config.options.plugins.legend.labels.font = { family: "'AlQabas', 'Segoe UI', sans-serif", size: 12 };

        // OPMS color palette if no colors specified
        var datasets = (config.data && config.data.datasets) || [];
        var opmsColors = [
            '#2D4A22', '#3A5C2E', '#4A7035', '#C9A84C',
            '#1B3A2A', '#5B8C4A', '#8FBC5A', '#E8D48C',
            '#6B8E23', '#DAA520', '#2E8B57', '#B8860B'
        ];
        var opmsBgColors = [
            'rgba(45,74,34,0.7)', 'rgba(58,92,46,0.7)', 'rgba(74,112,53,0.7)', 'rgba(201,168,76,0.7)',
            'rgba(27,58,42,0.7)', 'rgba(91,140,74,0.7)', 'rgba(143,188,90,0.7)', 'rgba(232,212,140,0.7)',
            'rgba(107,142,35,0.7)', 'rgba(218,165,32,0.7)', 'rgba(46,139,87,0.7)', 'rgba(184,134,11,0.7)'
        ];

        for (var i = 0; i < datasets.length; i++) {
            if (!datasets[i].backgroundColor) {
                if (config.type === 'pie' || config.type === 'doughnut' || config.type === 'polarArea') {
                    datasets[i].backgroundColor = opmsBgColors;
                    datasets[i].borderColor = opmsColors;
                } else {
                    datasets[i].backgroundColor = opmsBgColors[i % opmsBgColors.length];
                    datasets[i].borderColor = opmsColors[i % opmsColors.length];
                }
            }
            if (typeof datasets[i].borderWidth === 'undefined') {
                datasets[i].borderWidth = 1;
            }
        }

        return config;
    }

    // ═══════════════════════════════════════════════════════════
    //  Helpers
    // ═══════════════════════════════════════════════════════════

    function escapeHtml(text) {
        var div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    function escapeAttr(text) {
        return text
            .replace(/&/g, '&amp;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;');
    }

    // ═══════════════════════════════════════════════════════════
    //  Public API
    // ═══════════════════════════════════════════════════════════

    return {
        renderMarkdown: renderMarkdown,
        processCharts: processCharts,
        escapeHtml: escapeHtml
    };

})();
