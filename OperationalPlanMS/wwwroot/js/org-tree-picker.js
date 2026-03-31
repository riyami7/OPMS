/**
 * OrgTreePicker — مكوّن شجرة الوحدات التنظيمية (Dropdown Style)
 * الشجرة تُبنى فقط عند أول فتح للقائمة — الصفحة تفتح فوراً
 */
const OrgTreePicker = {

    _instances: {},
    _allUnits: null,

    async _loadUnits() {
        if (this._allUnits) return this._allUnits;
        try {
            var cached = sessionStorage.getItem('_orgUnitsCache');
            if (cached) {
                this._allUnits = JSON.parse(cached);
                return this._allUnits;
            }
            const res = await fetch('/api/OrganizationApi/units/all');
            this._allUnits = await res.json();
            try { sessionStorage.setItem('_orgUnitsCache', JSON.stringify(this._allUnits)); } catch(e) {}
            return this._allUnits;
        } catch (e) {
            console.error('Error loading org units:', e);
            return [];
        }
    },

    _isEmptyId(id) {
        return !id || id === '0' || id === 0 || id === '00000000-0000-0000-0000-000000000000' || id === null;
    },

    _buildTree(units, rootCode) {
        const roots = units.filter(u => u.code === rootCode && this._isEmptyId(u.parentId));
        if (roots.length === 0) {
            const fallbacks = units.filter(u => this._isEmptyId(u.parentId));
            if (fallbacks.length === 0) return [];
            return fallbacks.map(u => ({ ...u, children: this._getChildren(units, u.id) }));
        }
        return roots.map(u => ({ ...u, children: this._getChildren(units, u.id) }));
    },

    _getChildren(units, parentId) {
        return units
            .filter(u => u.parentId == parentId || u.parentId === parentId)
            .sort((a, b) => (a.name || '').localeCompare(b.name || '', 'ar'))
            .map(u => ({ ...u, children: this._getChildren(units, u.id) }));
    },

    _renderTree(nodes, level, instanceId) {
        if (!nodes || !nodes.length) return '';
        let html = '';
        for (const node of nodes) {
            const hasChildren = node.children && node.children.length > 0;
            const indent = level * 24;
            const isOpen = false;
            html += '<div class="otp-node" data-id="' + node.id + '" data-name="' + (node.name || '').replace(/"/g, '&quot;') + '" data-level="' + level + '">' +
                '<div class="otp-item" style="padding-right:' + (indent + 10) + 'px;" onclick="OrgTreePicker._select(\'' + instanceId + '\', \'' + node.id + '\', \'' + (node.name || '').replace(/'/g, "\\'").replace(/"/g, '&quot;') + '\')">' +
                (hasChildren
                    ? '<span class="otp-toggle" onclick="event.stopPropagation(); OrgTreePicker._toggle(this);"><span class="otp-arrow">›</span></span>'
                    : '<span class="otp-spacer"></span>') +
                '<span class="otp-name">' + (node.name || '') + '</span>' +
                '</div>' +
                (hasChildren
                    ? '<div class="otp-children" style="display:none;">' + this._renderTree(node.children, level + 1, instanceId) + '</div>'
                    : '') +
                '</div>';
        }
        return html;
    },

    _toggle(el) {
        var node = el.closest('.otp-node');
        var children = node.querySelector(':scope > .otp-children');
        var arrow = el.querySelector('.otp-arrow');
        if (children) {
            var isOpen = children.style.display !== 'none';
            children.style.display = isOpen ? 'none' : 'block';
            if (arrow) arrow.classList.toggle('open', !isOpen);
        }
    },

    _toggleDropdown(instanceId) {
        var inst = this._instances[instanceId];
        var container = document.getElementById(inst.containerId);
        var panel = container.querySelector('.otp-dropdown-panel');
        var isOpen = panel.style.display !== 'none';
        panel.style.display = isOpen ? 'none' : 'block';

        // بناء الشجرة عند أول فتح فقط
        if (!isOpen && inst._tree && !inst._treeBuilt) {
            var wrap = panel.querySelector('.otp-tree-wrap');
            wrap.innerHTML = this._renderTree(inst._tree, 0, instanceId);
            inst._treeBuilt = true;
            if (inst.selectedId) {
                this._expandToNode(container, inst.selectedId);
            }
        }

        if (!isOpen) {
            setTimeout(function() {
                var closeHandler = function(e) {
                    if (!container.contains(e.target)) {
                        panel.style.display = 'none';
                        document.removeEventListener('click', closeHandler);
                    }
                };
                document.addEventListener('click', closeHandler);
            }, 10);
            var searchInput = panel.querySelector('.otp-search input');
            if (searchInput) setTimeout(function() { searchInput.focus(); }, 50);
        }
    },

    _select(instanceId, id, name) {
        var inst = this._instances[instanceId];
        if (!inst) return;
        var container = document.getElementById(inst.containerId);
        container.querySelectorAll('.otp-item.selected').forEach(function(el) { el.classList.remove('selected'); });
        var node = container.querySelector('.otp-node[data-id="' + id + '"]');
        if (node) node.querySelector('.otp-item').classList.add('selected');
        document.getElementById(inst.hiddenInputId).value = id;
        if (inst.hiddenNameId) document.getElementById(inst.hiddenNameId).value = name;
        var trigger = container.querySelector('.otp-trigger');
        if (trigger) {
            trigger.innerHTML = '<span class="otp-trigger-text"><i class="bi bi-building me-2 text-success"></i>' + name + '</span>' +
                '<span class="otp-trigger-actions">' +
                '<button type="button" class="btn-otp-clear" onclick="event.stopPropagation(); OrgTreePicker._clear(\'' + instanceId + '\')"><i class="bi bi-x-lg"></i></button>' +
                '<i class="bi bi-chevron-down otp-chevron"></i></span>';
            trigger.classList.add('has-value');
        }
        var panel = container.querySelector('.otp-dropdown-panel');
        if (panel) panel.style.display = 'none';
        if (inst.onSelect) inst.onSelect(id, name);
    },

    _clear(instanceId) {
        var inst = this._instances[instanceId];
        if (!inst) return;
        var container = document.getElementById(inst.containerId);
        container.querySelectorAll('.otp-item.selected').forEach(function(el) { el.classList.remove('selected'); });
        document.getElementById(inst.hiddenInputId).value = '';
        if (inst.hiddenNameId) document.getElementById(inst.hiddenNameId).value = '';
        var trigger = container.querySelector('.otp-trigger');
        if (trigger) {
            trigger.innerHTML = '<span class="otp-trigger-text text-muted"><i class="bi bi-diagram-3 me-2"></i>اختر الوحدة التنظيمية...</span>' +
                '<i class="bi bi-chevron-down otp-chevron"></i>';
            trigger.classList.remove('has-value');
        }
        if (inst.onSelect) inst.onSelect('', '');
    },

    _expandToNode(container, nodeId) {
        var node = container.querySelector('.otp-node[data-id="' + nodeId + '"]');
        if (!node) return;
        var parent = node.parentElement;
        while (parent) {
            if (parent.classList && parent.classList.contains('otp-children')) {
                parent.style.display = 'block';
                var arrow = parent.parentElement.querySelector(':scope > .otp-item .otp-arrow');
                if (arrow) arrow.classList.add('open');
            }
            parent = parent.parentElement;
        }
        node.querySelector('.otp-item').classList.add('selected');
    },

    async init(config) {
        var instanceId = config.containerId;
        this._instances[instanceId] = config;
        config._loaded = false;
        config._treeBuilt = false;

        var container = document.getElementById(config.containerId);
        if (!container) return;

        if (config.selectedId) {
            await this._loadAndShowSelected(instanceId, container, config);
        } else {
            container.innerHTML = '<div class="otp-trigger" onclick="OrgTreePicker._lazyLoad(\'' + instanceId + '\')">' +
                '<span class="otp-trigger-text text-muted"><i class="bi bi-diagram-3 me-2"></i>اختر الوحدة التنظيمية...</span>' +
                '<i class="bi bi-chevron-down otp-chevron"></i></div>';
        }
    },

    async _loadAndShowSelected(instanceId, container, config) {
        var units = await this._loadUnits();
        if (!units || units.length === 0) {
            container.innerHTML = '<div class="otp-trigger disabled"><span class="text-danger"><i class="bi bi-exclamation-triangle me-1"></i>لا توجد بيانات</span></div>';
            return;
        }
        var selectedName = '';
        for (var i = 0; i < units.length; i++) {
            if (units[i].id === config.selectedId || units[i].id == config.selectedId) { selectedName = units[i].name || ''; break; }
        }
        var hasValue = config.selectedId && selectedName;
        config._tree = this._buildTree(units, config.rootCode || '00001');
        config._loaded = true;

        container.innerHTML = '<div class="otp-trigger ' + (hasValue ? 'has-value' : '') + '" onclick="OrgTreePicker._toggleDropdown(\'' + instanceId + '\')">' +
            (hasValue
                ? '<span class="otp-trigger-text"><i class="bi bi-building me-2 text-success"></i>' + selectedName + '</span>' +
                  '<span class="otp-trigger-actions"><button type="button" class="btn-otp-clear" onclick="event.stopPropagation(); OrgTreePicker._clear(\'' + instanceId + '\')"><i class="bi bi-x-lg"></i></button>' +
                  '<i class="bi bi-chevron-down otp-chevron"></i></span>'
                : '<span class="otp-trigger-text text-muted"><i class="bi bi-diagram-3 me-2"></i>اختر الوحدة التنظيمية...</span>' +
                  '<i class="bi bi-chevron-down otp-chevron"></i>') +
            '</div>' +
            '<div class="otp-dropdown-panel" style="display:none;">' +
            '<div class="otp-search"><input type="text" class="form-control form-control-sm" placeholder="بحث في الوحدات..." oninput="OrgTreePicker._search(\'' + instanceId + '\', this.value)" /></div>' +
            '<div class="otp-tree-wrap"></div></div>';
    },

    async _lazyLoad(instanceId) {
        var inst = this._instances[instanceId];
        if (!inst) return;
        if (inst._loaded) { this._toggleDropdown(instanceId); return; }
        var container = document.getElementById(inst.containerId);
        container.innerHTML = '<div class="otp-trigger disabled"><span class="text-muted"><div class="spinner-border spinner-border-sm me-2"></div>جاري التحميل...</span></div>';
        var units = await this._loadUnits();
        if (!units || units.length === 0) {
            container.innerHTML = '<div class="otp-trigger disabled"><span class="text-danger"><i class="bi bi-exclamation-triangle me-1"></i>لا توجد بيانات</span></div>';
            return;
        }
        inst._tree = this._buildTree(units, inst.rootCode || '00001');
        inst._loaded = true;
        inst._treeBuilt = false;
        container.innerHTML = '<div class="otp-trigger" onclick="OrgTreePicker._toggleDropdown(\'' + instanceId + '\')">' +
            '<span class="otp-trigger-text text-muted"><i class="bi bi-diagram-3 me-2"></i>اختر الوحدة التنظيمية...</span>' +
            '<i class="bi bi-chevron-down otp-chevron"></i></div>' +
            '<div class="otp-dropdown-panel" style="display:none;">' +
            '<div class="otp-search"><input type="text" class="form-control form-control-sm" placeholder="بحث في الوحدات..." oninput="OrgTreePicker._search(\'' + instanceId + '\', this.value)" /></div>' +
            '<div class="otp-tree-wrap"></div></div>';
        this._toggleDropdown(instanceId);
    },

    _search(instanceId, term) {
        var inst = this._instances[instanceId];
        var container = document.getElementById(inst.containerId);
        var nodes = container.querySelectorAll('.otp-node');
        var lowerTerm = term.trim().toLowerCase();
        if (!lowerTerm) {
            nodes.forEach(function(n) {
                n.style.display = '';
                var children = n.querySelector(':scope > .otp-children');
                if (children) children.style.display = 'none';
                var arrow = n.querySelector(':scope > .otp-item .otp-arrow');
                if (arrow) arrow.classList.toggle('open', false);
            });
            return;
        }
        nodes.forEach(function(n) {
            var name = (n.dataset.name || '').toLowerCase();
            var matches = name.includes(lowerTerm);
            var hasMatchingChild = Array.from(n.querySelectorAll('.otp-node'))
                .some(function(c) { return (c.dataset.name || '').toLowerCase().includes(lowerTerm); });
            if (matches || hasMatchingChild) {
                n.style.display = '';
                var children = n.querySelector(':scope > .otp-children');
                if (children) children.style.display = 'block';
                var arrow = n.querySelector(':scope > .otp-item .otp-arrow');
                if (arrow) arrow.classList.add('open');
            } else {
                n.style.display = 'none';
            }
        });
    }
};
