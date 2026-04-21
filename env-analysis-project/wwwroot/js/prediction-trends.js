'use strict';

(function () {
    const routes = window.measurementResultRoutes || {};
    const bridge = window.trendPredictionBridge || null;

    const elements = {
        toggle: document.getElementById('trendModelToggle'),
        tableBody: document.getElementById('predictionTableBody'),
        tableSummary: document.getElementById('predictionTableSummary')
    };

    if (!bridge || !elements.toggle) {
        return;
    }

    let modelEnabled = false;
    let activeRequestKey = null;

    const normalizeType = (value) => (value || '').toString().trim().toLowerCase();

    const escapeHtml = (value) => {
        if (value === null || value === undefined) return '';
        return value
            .toString()
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;');
    };

    const formatNumericValue = (value) => {
        if (value === null || value === undefined) return '-';
        const number = Number(value);
        return Number.isFinite(number)
            ? number.toLocaleString(undefined, { maximumFractionDigits: 3 })
            : '-';
    };

    const unwrapApiResponse = (json) => {
        if (!json || typeof json !== 'object') return json;
        if (Object.prototype.hasOwnProperty.call(json, 'data')) return json.data;
        return json;
    };

    const setToggleBusy = (busy) => {
        if (!elements.toggle) return;
        elements.toggle.disabled = busy;
        elements.toggle.classList.toggle('opacity-60', busy);
        elements.toggle.classList.toggle('pointer-events-none', busy);
    };

    const updateToggleLabel = () => {
        if (!elements.toggle) return;
        elements.toggle.textContent = modelEnabled ? 'Model: On' : 'Model: Off';
        elements.toggle.classList.toggle('text-amber-600', modelEnabled);
        elements.toggle.classList.toggle('border-amber-300', modelEnabled);
    };

    const buildModelUrl = (selection) => {
        if (!routes.model || !selection) return null;
        const params = new URLSearchParams();
        const isWater = normalizeType(selection.activeType) === 'water';

        if (isWater) {
            const codes = (selection.selectedCodes || [])
                .filter(code => typeof code === 'string' && code.trim() !== '');
            if (!codes.length) return null;
            codes.forEach(code => params.append('codes', code));
        } else {
            if (!selection.selectedCode) return null;
            params.set('code', selection.selectedCode);
        }

        if (selection.filter?.startMonth) params.set('startMonth', selection.filter.startMonth);
        if (selection.filter?.endMonth) params.set('endMonth', selection.filter.endMonth);
        if (selection.filter?.sourceId != null) params.set('sourceId', selection.filter.sourceId.toString());
        return `${routes.model}?${params.toString()}`;
    };

    const buildModelTableItems = (payload, selection) => {
        const series = Array.isArray(payload?.series) ? payload.series : [];
        const isWater = normalizeType(selection.activeType) === 'water';
        const items = [];

        series.forEach((serie) => {
            const points = Array.isArray(serie?.points) ? serie.points : [];
            const baseName = serie?.parameterName || serie?.parameterCode || 'Model';
            const unit = serie?.unit || null;
            const standardValue = serie?.standardValue ?? null;
            const parameterName = isWater ? `${baseName} (Model)` : baseName;
            const sourceName = isWater ? null : 'Model (All sources)';

            points.forEach((point) => {
                const value = point?.value;
                if (value === null || value === undefined) return;
                const label = point?.label || point?.month || '';
                const month = point?.month || '';
                items.push({
                    label,
                    month,
                    value,
                    parameterName,
                    sourceName,
                    unit,
                    standardValue
                });
            });
        });

        return items;
    };

    const renderPredictionTable = (items) => {
        if (!elements.tableBody) return;
        const rows = Array.isArray(items) ? items : [];
        if (!rows.length) {
            elements.tableBody.innerHTML = `
                <tr>
                    <td colspan="5" class="px-3 py-5 text-center text-gray-400">
                        Turn on Model to see prediction values.
                    </td>
                </tr>`;
            if (elements.tableSummary) {
                elements.tableSummary.textContent = 'No prediction data';
            }
            return;
        }

        const sorted = rows
            .slice()
            .sort((a, b) => {
                const dateA = a.month || '';
                const dateB = b.month || '';
                if (dateA !== dateB) return dateA.localeCompare(dateB);
                return (a.parameterName || '').localeCompare(b.parameterName || '');
            });

        elements.tableBody.innerHTML = sorted.map(item => {
            const name = escapeHtml(item.parameterName || '-');
            const unit = item.unit ? escapeHtml(item.unit) : '-';
            const label = escapeHtml(item.label || '-');
            const value = formatNumericValue(item.value);
            const standard = formatNumericValue(item.standardValue);
            const highlight = item.standardValue != null && Number(item.value) > Number(item.standardValue);
            const rowClass = highlight ? 'bg-red-200' : '';
            return `
                <tr class="hover:bg-gray-50 transition ${rowClass}">
                    <td class="px-3 py-2">${label}</td>
                    <td class="px-3 py-2">${name}</td>
                    <td class="px-3 py-2">${value}</td>
                    <td class="px-3 py-2">${standard}</td>
                    <td class="px-3 py-2">${unit}</td>
                </tr>`;
        }).join('');

        if (elements.tableSummary) {
            elements.tableSummary.textContent = `Showing ${sorted.length} predicted points`;
        }
    };

    const clearModelOverlay = () => {
        bridge.clearModelOverlay();
        renderPredictionTable([]);
    };

    const loadModelPredictions = async (selection) => {
        const url = buildModelUrl(selection);
        if (!url) {
            clearModelOverlay();
            return;
        }

        const requestKey = url;
        activeRequestKey = requestKey;
        setToggleBusy(true);

        try {
            const res = await fetch(url, { credentials: 'same-origin' });
            if (!res.ok) {
                throw new Error('Failed to load prediction data.');
            }
            const json = await res.json();
            if (json?.success === false) throw new Error(json?.message || 'Failed to load prediction data.');
            const payload = unwrapApiResponse(json);
            if (activeRequestKey !== requestKey) return;

            const series = Array.isArray(payload?.series) ? payload.series : [];
            const modelPayload = {
                ...payload,
                series: series.map(item => ({
                    ...item,
                    isForecast: item?.isForecast ?? true
                }))
            };

            const modelTableItems = buildModelTableItems(modelPayload, selection);
            bridge.renderWithModelOverlay(modelPayload, modelTableItems);
            renderPredictionTable(modelTableItems);
        } catch (error) {
            console.error(error);
            if (modelEnabled) {
                alert(error.message || 'Failed to load prediction data.');
            }
            if (activeRequestKey === requestKey) {
                clearModelOverlay();
            }
        } finally {
            if (activeRequestKey === requestKey) {
                setToggleBusy(false);
                updateToggleLabel();
            }
        }
    };

    const handleToggleClick = () => {
        modelEnabled = !modelEnabled;
        updateToggleLabel();
        if (!modelEnabled) {
            clearModelOverlay();
            return;
        }
        const selection = bridge.getTrendSelection();
        loadModelPredictions(selection);
    };

    const handleTrendPayload = (event) => {
        if (!modelEnabled) return;
        const selection = event?.detail?.selection || bridge.getTrendSelection();
        const payload = event?.detail?.payload || null;
        if (!payload) {
            clearModelOverlay();
            return;
        }
        loadModelPredictions(selection);
    };

    elements.toggle.addEventListener('click', handleToggleClick);
    window.addEventListener('trend:payload', handleTrendPayload);
    updateToggleLabel();
})();
