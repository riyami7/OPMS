/**
 * chat-bubble.js — المساعد الذكي مع تصفية الصلاحيات
 *
 * التدفق:
 * 1. عند فتح المحادثة → يجلب chat-context من OPMS API (مصفّا حسب صلاحيات المستخدم)
 * 2. عند إرسال رسالة → يضيف السياق كـ system prompt مع الرسالة لـ riyamiai
 * 3. riyamiai يجيب بناءً على البيانات المصرح بها فقط
 */
(function () {
    'use strict';

    var C = Object.assign({
        apiBaseUrl: 'http://localhost:8000',
        apiToken: '',
        streaming: true,
        useRag: false,
        model: null,
        opmsApiBaseUrl: '',
        opmsApiKey: '',
        userId: 0,
        userRole: '',
        userName: ''
    }, window.CHAT_CONFIG || {});

    // DOM
    var fab = document.getElementById('chatFab'),
        win = document.getElementById('chatWin'),
        bd = document.getElementById('chatBd'),
        inp = document.getElementById('chatInp'),
        sndBtn = document.getElementById('chatSnd'),
        clrBtn = document.getElementById('chatClr'),
        minBtn = document.getElementById('chatMin'),
        stEl = document.getElementById('chatSt'),
        icoMsg = fab.querySelector('.aichat-fab__ico--msg'),
        icoX = fab.querySelector('.aichat-fab__ico--x');

    var open = false, busy = false;
    var cachedContext = null;  // يُحمّل مرة واحدة عند الفتح

    // ==================== Toggle ====================
    function toggle() {
        open = !open;
        win.style.display = open ? 'flex' : 'none';
        icoMsg.style.display = open ? 'none' : '';
        icoX.style.display = open ? '' : 'none';
        if (open) {
            inp.focus();
            scroll();
            if (!cachedContext) loadContext();  // جلب السياق عند أول فتح
        }
    }
    fab.onclick = toggle;
    minBtn.onclick = toggle;

    // ==================== Load Context (role-filtered) ====================
    async function loadContext() {
        try {
            status('يحمّل البيانات...');
            var base = C.opmsApiBaseUrl || window.location.origin;
            var url = base + '/api/data/chat-context?userId=' + C.userId + '&role=' + C.userRole;
            var res = await fetch(url, {
                headers: { 'X-API-Key': C.opmsApiKey }
            });
            if (res.ok) {
                var json = await res.json();
                cachedContext = json.data;
                status('متصل');
            } else {
                status('تعذر تحميل البيانات');
                console.warn('Chat context failed:', res.status);
            }
        } catch (e) {
            status('تعذر تحميل البيانات');
            console.error('Chat context error:', e);
        }
    }

    // ==================== Build System Prompt ====================
    function buildSystemPrompt() {
        if (!cachedContext) return '';

        var ctx = cachedContext;
        var lines = [];
        lines.push('أنت مساعد ذكي لنظام إدارة الخطط التشغيلية (OPMS).');
        lines.push('المستخدم الحالي: ' + ctx.userName + ' (الدور: ' + ctx.userRole + ')');
        lines.push('');
        lines.push('=== البيانات المتاحة (مصفّاة حسب صلاحيات المستخدم) ===');
        lines.push('إجمالي: ' + ctx.summary.totalInitiatives + ' مبادرات، ' + ctx.summary.totalProjects + ' مشاريع، ' + ctx.summary.totalSteps + ' خطوات');
        lines.push('مكتمل: ' + ctx.summary.completedProjects + ' مشروع | متأخر: ' + ctx.summary.delayedProjects + ' مشروع، ' + ctx.summary.delayedSteps + ' خطوة');
        lines.push('متوسط التقدم: ' + ctx.summary.averageProgress + '%');
        lines.push('');

        ctx.initiatives.forEach(function (ini) {
            lines.push('--- مبادرة: ' + ini.code + ' - ' + ini.name + ' ---');
            lines.push('  الوحدة: ' + (ini.unit || '-') + ' | المشرف: ' + (ini.supervisor || '-'));
            lines.push('  الحالة: ' + ini.status + ' | التقدم: ' + ini.progress + '%');
            if (ini.budget) lines.push('  الميزانية: ' + ini.budget.toLocaleString() + ' ر.ع');
            lines.push('');

            ini.projects.forEach(function (prj) {
                lines.push('  مشروع: ' + prj.code + ' - ' + prj.name);
                lines.push('    مدير المشروع: ' + (prj.manager || '-') + ' | الحالة: ' + prj.status + ' | التقدم: ' + prj.progress + '%' + (prj.isDelayed ? ' ⚠️ متأخر' : ''));

                prj.steps.forEach(function (st) {
                    lines.push('    خطوة ' + st.number + ': ' + st.name + ' → ' + st.status + ' (' + st.progress + '%)' + (st.isDelayed ? ' ⚠️' : '') + ' | المسؤول: ' + (st.assignedTo || '-'));
                });

                if (prj.kpIs && prj.kpIs.length) {
                    prj.kpIs.forEach(function (k) {
                        lines.push('    KPI: ' + k.name + ' | المستهدف: ' + (k.target || '-') + ' | الفعلي: ' + (k.actual || '-'));
                    });
                }
                lines.push('');
            });
        });

        lines.push('=== تعليمات ===');
        lines.push('- أجب بالعربية فقط');
        lines.push('- استخدم فقط البيانات أعلاه — لا تختلق معلومات غير موجودة');
        lines.push('- إذا سُئلت عن بيانات ليست في السياق، قل "هذه المعلومات غير متاحة لك حسب صلاحياتك"');
        lines.push('- كن موجزاً ومفيداً');

        return lines.join('\n');
    }

    // ==================== Send ====================
    function send() {
        var t = inp.value.trim();
        if (!t || busy) return;
        addMsg('u', t);
        inp.value = '';
        inp.style.height = 'auto';
        sndBtn.disabled = true;
        C.streaming ? doStream(t) : doSingle(t);
    }
    sndBtn.onclick = send;
    inp.onkeydown = function (e) { if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); send(); } };
    inp.oninput = function () {
        sndBtn.disabled = !inp.value.trim() || busy;
        inp.style.height = 'auto';
        inp.style.height = Math.min(inp.scrollHeight, 90) + 'px';
    };

    // ==================== Single Reply ====================
    async function doSingle(t) {
        busy = true; status('يكتب...'); var d = dots();
        try {
            var r = await api('/api/chat/single', t);
            rm(d);
            if (r.ok) { var j = await r.json(); addMsg('b', j.response || '—'); }
            else addMsg('b', errTxt(r.status), true);
        } catch (e) { rm(d); addMsg('b', 'تعذر الاتصال. تأكد من تشغيل الخدمة.', true); }
        finally { busy = false; status('متصل'); sndBtn.disabled = !inp.value.trim(); }
    }

    // ==================== Streaming Reply ====================
    async function doStream(t) {
        busy = true; status('يكتب...'); var d = dots();
        try {
            var r = await api('/api/chat/single/stream', t);
            rm(d);
            if (!r.ok) { addMsg('b', errTxt(r.status), true); return; }
            hideHi();
            var el = mkBubble('b', ''); bd.appendChild(el);
            var ct = el.querySelector('.aichat-m__t'), full = '';
            var rd = r.body.getReader(), dc = new TextDecoder();
            while (true) {
                var c = await rd.read(); if (c.done) break;
                var lines = dc.decode(c.value, { stream: true }).split('\n');
                for (var i = 0; i < lines.length; i++) {
                    var ln = lines[i].trim();
                    if (!ln.startsWith('data:')) continue;
                    try {
                        var ev = JSON.parse(ln.slice(5).trim());
                        if (ev.token) { full += ev.token; ct.textContent = full; scroll(); }
                        if (ev.error) ct.textContent = 'خطأ: ' + ev.error;
                    } catch (_) { }
                }
            }
        } catch (e) { rm(d); addMsg('b', 'تعذر الاتصال. تأكد من تشغيل الخدمة.', true); }
        finally { busy = false; status('متصل'); sndBtn.disabled = !inp.value.trim(); }
    }

    // ==================== API Call — مع System Prompt ====================
    function api(path, msg) {
        var h = { 'Content-Type': 'application/json' };
        if (C.apiToken) h['Authorization'] = 'Bearer ' + C.apiToken;

        // بناء الرسالة مع السياق المصفّا
        var systemPrompt = buildSystemPrompt();
        var fullMessage = systemPrompt
            ? '<<SYSTEM>>\n' + systemPrompt + '\n<</SYSTEM>>\n\nسؤال المستخدم: ' + msg
            : msg;

        var b = { message: fullMessage };
        if (C.model) b.model = C.model;
        if (C.useRag) b.use_rag = true;
        return fetch(C.apiBaseUrl + path, { method: 'POST', headers: h, body: JSON.stringify(b) });
    }

    function errTxt(c) {
        return c === 401 ? 'غير مصرح — تحقق من التوكن' : c === 503 ? 'الخدمة غير متاحة' : 'خطأ (' + c + ')';
    }

    // ==================== DOM Helpers ====================
    function addMsg(role, txt, err) {
        hideHi();
        var el = mkBubble(role, txt, err);
        bd.appendChild(el);
        scroll();
    }

    function mkBubble(role, txt, err) {
        var w = document.createElement('div');
        w.className = 'aichat-m aichat-m--' + role;
        var a = document.createElement('div');
        a.className = 'aichat-m__a';
        a.innerHTML = role === 'u'
            ? '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/></svg>'
            : '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><path d="M12 2a4 4 0 0 1 4 4v1a4 4 0 0 1-8 0V6a4 4 0 0 1 4-4z"/><rect x="5" y="12" width="14" height="8" rx="3"/><circle cx="9.5" cy="16" r="1" fill="currentColor" stroke="none"/><circle cx="14.5" cy="16" r="1" fill="currentColor" stroke="none"/></svg>';
        var b = document.createElement('div');
        b.className = 'aichat-m__t' + (err ? ' aichat-m__t--err' : '');
        b.textContent = txt;
        w.appendChild(a); w.appendChild(b);
        return w;
    }

    function dots() {
        hideHi();
        var w = document.createElement('div');
        w.className = 'aichat-m aichat-m--b'; w.id = 'aiDots';
        var a = document.createElement('div'); a.className = 'aichat-m__a';
        a.innerHTML = '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><path d="M12 2a4 4 0 0 1 4 4v1a4 4 0 0 1-8 0V6a4 4 0 0 1 4-4z"/><rect x="5" y="12" width="14" height="8" rx="3"/><circle cx="9.5" cy="16" r="1" fill="currentColor" stroke="none"/><circle cx="14.5" cy="16" r="1" fill="currentColor" stroke="none"/></svg>';
        var b = document.createElement('div'); b.className = 'aichat-m__t';
        b.innerHTML = '<div class="aichat-dots"><span></span><span></span><span></span></div>';
        w.appendChild(a); w.appendChild(b);
        bd.appendChild(w); scroll();
        return w;
    }

    function rm(el) { if (el && el.parentNode) el.remove(); }
    function hideHi() { var h = document.getElementById('chatHi'); if (h) h.remove(); }
    function scroll() { bd.scrollTop = bd.scrollHeight; }

    function status(t) {
        stEl.innerHTML = (t === 'متصل' ? '<span class="aichat__hd-dotg"></span>' : '') + t;
    }

    // ==================== Clear ====================
    clrBtn.onclick = function () {
        bd.innerHTML =
            '<div class="aichat__hi" id="chatHi">' +
            '<div class="aichat__hi-ava"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.4"><path d="M12 2a4 4 0 0 1 4 4v1a4 4 0 0 1-8 0V6a4 4 0 0 1 4-4z"/><rect x="5" y="12" width="14" height="8" rx="3"/><circle cx="9.5" cy="16" r="1" fill="currentColor" stroke="none"/><circle cx="14.5" cy="16" r="1" fill="currentColor" stroke="none"/></svg></div>' +
            '<div class="aichat__hi-t">\u0645\u0631\u062d\u0628\u0627\u064b\u060c \u0643\u064a\u0641 \u0623\u0642\u062f\u0631 \u0623\u0633\u0627\u0639\u062f\u0643\u061f</div>' +
            '<div class="aichat__hi-s">\u0627\u0633\u0623\u0644\u0646\u064a \u0639\u0646 \u0627\u0644\u0645\u0628\u0627\u062f\u0631\u0627\u062a\u060c \u0627\u0644\u0645\u0634\u0627\u0631\u064a\u0639\u060c \u0623\u0648 \u0623\u064a \u0634\u064a\u0621 \u0641\u064a \u0627\u0644\u0646\u0638\u0627\u0645</div>' +
            '<div class="aichat__chips">' +
            '<button class="aichat__chip" data-m="\u0645\u0627 \u0647\u064a \u0627\u0644\u0645\u0628\u0627\u062f\u0631\u0627\u062a \u0627\u0644\u062c\u0627\u0631\u064a\u0629\u061f">\uD83D\uDCCA \u0627\u0644\u0645\u0628\u0627\u062f\u0631\u0627\u062a \u0627\u0644\u062c\u0627\u0631\u064a\u0629</button>' +
            '<button class="aichat__chip" data-m="\u0623\u0639\u0637\u0646\u064a \u0645\u0644\u062e\u0635 \u062a\u0642\u062f\u0645 \u0627\u0644\u0645\u0634\u0627\u0631\u064a\u0639">\uD83D\uDCCB \u062a\u0642\u062f\u0645 \u0627\u0644\u0645\u0634\u0627\u0631\u064a\u0639</button>' +
            '<button class="aichat__chip" data-m="\u0645\u0627 \u0647\u064a \u0627\u0644\u062e\u0637\u0648\u0627\u062a \u0627\u0644\u0645\u062a\u0623\u062e\u0631\u0629\u061f">\u23F0 \u0627\u0644\u062e\u0637\u0648\u0627\u062a \u0627\u0644\u0645\u062a\u0623\u062e\u0631\u0629</button>' +
            '</div></div>';
        bindChips();
    };

    // ==================== Suggestion Chips ====================
    function bindChips() {
        bd.querySelectorAll('.aichat__chip').forEach(function (c) {
            c.onclick = function () { var m = this.getAttribute('data-m'); if (m) { inp.value = m; send(); } };
        });
    }
    bindChips();

})();