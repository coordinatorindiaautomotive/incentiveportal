// ledger.js - Master Ledger Module
const LedgerModule = (function () {
    let _config = {
        getLedgerUrl: '',
        getHistoryUrl: '',
        getDiffUrl: '',
        exportExcelUrl: '',
        exportCsvUrl: ''
    };

    let table = null;

    function init(config) {
        _config = Object.assign(_config, config);
        initDataTable();
        bindEvents();
    }

    function initDataTable() {
        table = $('#ledgerDataTable').DataTable({
            serverSide: true,
            scrollX: true,
            ajax: {
                url: _config.getLedgerUrl,
                type: 'POST',
                data: function (d) {
                    d.filterPeriod = $('#filterPeriod').val();
                    d.filterStatus = $('#filterStatus').val();
                }
            },
            columns: [
                { 
                    data: null,
                    render: function (data, type, row) {
                        const months = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
                        if(row.incentiveMonth > 0 && row.incentiveMonth <= 12) {
                            return `<span class="fw-bold text-secondary" style="font-size: 0.85rem;">${months[row.incentiveMonth-1]}-${row.incentiveYear}</span>`;
                        }
                        return `<span class="fw-bold text-secondary" style="font-size: 0.85rem;">${row.periodLabel}</span>`;
                    }
                },
                { 
                    data: 'locationCode',
                    render: function (data) {
                        return `<span class="badge bg-light text-dark border">${data}</span>`;
                    }
                },
                { 
                    data: 'partyCode',
                    render: function (data) {
                        return `<div class="party-code-subtitle">${data}</div>`;
                    }
                },
                { 
                    data: 'partyName',
                    render: function (data) {
                        return `<div class="party-name-title text-truncate" style="max-width: 150px;" title="${data}">${data}</div>`;
                    }
                },
                { 
                    data: 'saleValue',
                    className: 'text-end',
                    render: function (data) {
                        return `<span class="val-currency">₹${Number(data).toLocaleString('en-IN', {maximumFractionDigits: 0})}</span>`;
                    }
                },
                { 
                    data: 'onBillDiscount',
                    className: 'text-end',
                    render: function (data) {
                        if(Number(data) === 0) return `<span class="text-muted small">-</span>`;
                        return `<span class="val-currency text-danger">₹${Number(data).toLocaleString('en-IN', {maximumFractionDigits: 0})}</span>`;
                    }
                },
                {
                    data: 'slabApplied',
                    className: 'text-end',
                    render: function (data) {
                        return `<span class="figma-badge figma-badge-neutral">${data}</span>`;
                    }
                },
                { 
                    data: 'grossIncentive',
                    className: 'text-end',
                    render: function (data) {
                        return `<span class="val-currency">₹${Number(data).toLocaleString('en-IN', {maximumFractionDigits: 0})}</span>`;
                    }
                },
                { 
                    data: 'tdsAmount',
                    className: 'text-end',
                    render: function (data, type, row) {
                        if(Number(data) === 0) return `<span class="text-muted small">-</span>`;
                        return `<span class="val-currency text-secondary" style="font-size: 0.8rem;">₹${Number(data).toLocaleString('en-IN', {maximumFractionDigits: 0})} <br><span style="font-size: 0.7rem; opacity: 0.7;">(${row.tdsPercent}%)</span></span>`;
                    }
                },
                { 
                    data: 'netTransferAmount',
                    className: 'text-end',
                    render: function (data, type, row) {
                        if (row.grossIncentive === 0) {
                            return `<span class="text-muted small">-</span>`;
                        }
                        return `<span class="val-currency-success">₹${Number(data).toLocaleString('en-IN', {maximumFractionDigits: 0})}</span>`;
                    }
                },
                { 
                    data: 'paymentStatus',
                    className: 'text-center',
                    render: function (data, type, row) {
                        if (row.grossIncentive === 0) {
                            return `<span class="text-muted small fst-italic">-</span>`;
                        }
                        let badgeClass = data === 'Paid' ? 'figma-badge-success' : data === 'Hold' ? 'figma-badge-neutral' : 'figma-badge-warning';
                        return `<span class="figma-badge ${badgeClass}">${data}</span>`;
                    }
                },
                { data: 'utrNumber' },
                { data: 'paymentDate' },
                { data: 'beneficiaryName' },
                { data: 'bankAccountNumber' },
                { data: 'ifsc' },
                { 
                    data: 'panNo',
                    render: function (data) {
                        return `<span class="badge bg-secondary">${data}</span>`;
                    }
                },
                { 
                    data: 'id',
                    className: 'text-center',
                    orderable: false,
                    render: function (data, type, row) {
                        return `
                            <div class="d-flex justify-content-center gap-2">
                                <button type="button" class="btn btn-sm btn-light open-versions-btn shadow-sm" 
                                    data-id="${data}" 
                                    data-party="${row.partyCode}" 
                                    data-name="${row.partyName}" 
                                    data-year="${row.incentiveYear}" 
                                    data-month="${row.incentiveMonth}" 
                                    style="border-radius: 8px; font-weight: 600; font-size: 0.75rem; color: var(--figma-text-muted);">
                                    <i class="fa-solid fa-clock-rotate-left text-primary"></i> v${row.versionNumber}
                                </button>
                            </div>
                        `;
                    }
                }
            ],
            order: [[0, 'desc']], // Order by Month desc on load
            pageLength: 10,
            lengthMenu: [10, 25, 50, 100],
            language: {
                processing: '<div class="spinner-border spinner-border-sm text-primary" role="status"></div> Loading Live Master Ledger...'
            }
        });
    }

    function bindEvents() {
        // Handle Filters Redirection
        $('#filterPeriod, #filterStatus').on('change', function () {
            table.ajax.reload();
        });

        // Reset Filters
        $('#resetFiltersBtn').on('click', function () {
            $('#filterPeriod').val('all');
            $('#filterStatus').val('all');
            table.search('').draw();
        });

        // High-performance exports
        $('#exportExcelBtn').on('click', function () {
            var period = $('#filterPeriod').val();
            var status = $('#filterStatus').val();
            var search = table.search();
            window.location.href = `${_config.exportExcelUrl}?filterPeriod=${period}&filterStatus=${status}&search=${encodeURIComponent(search)}`;
        });

        $('#exportCsvBtn').on('click', function () {
            var period = $('#filterPeriod').val();
            var status = $('#filterStatus').val();
            var search = table.search();
            window.location.href = `${_config.exportCsvUrl}?filterPeriod=${period}&filterStatus=${status}&search=${encodeURIComponent(search)}`;
        });

        // Single-click row redirects to detail page
        $('#ledgerDataTable tbody').on('click', 'tr', function (e) {
            if ($(e.target).closest('button').length) return;
            
            var rowData = table.row(this).data();
            if (rowData) {
                window.location.href = `/Ledger/Detail/${rowData.id}`;
            }
        });

        // 2. Open Version Auditing Drawer Modal
        $(document).on('click', '.open-versions-btn', function () {
            var id = $(this).data('id');
            var partyCode = $(this).data('party');
            var partyName = $(this).data('name');
            var year = $(this).data('year');
            var month = $(this).data('month');

            $('#modalSubtitleText').html(`Review version history for <strong>${partyName}</strong> (${partyCode}) · Month: ${month}/${year}`);
            
            // Reset Diff View
            $('#diffPlaceholderText').removeClass('d-none');
            $('#diffResultsPanel').addClass('d-none');
            $('#compareBtn').prop('disabled', true);

            // Fetch version history log
            $.get(_config.getHistoryUrl, { partyCode: partyCode, year: year, month: month }, function (history) {
                var tbody = $('#versionHistoryGrid tbody').empty();
                history.forEach(function (h) {
                    var isLatestLabel = h.isLatestVersion ? ' <span class="badge bg-success-light text-success fw-bold" style="font-size:0.6rem;">Latest</span>' : '';
                    tbody.append(`
                        <tr>
                            <td><strong>v${h.versionNumber}</strong>${isLatestLabel}</td>
                            <td><span class="val-currency">₹${Number(h.grossIncentive).toLocaleString()}</span></td>
                            <td><span class="figma-badge ${h.paymentStatus === 'Paid' ? 'figma-badge-success' : h.paymentStatus === 'Hold' ? 'figma-badge-neutral' : 'figma-badge-warning'}">${h.paymentStatus}</span></td>
                            <td><span class="d-block fw-semibold">${h.uploadedBy}</span><span class="d-block small text-muted" style="font-size:0.7rem;">${h.uploadedAt}</span></td>
                            <td class="text-center">
                                <input class="form-check-input version-checkbox" type="checkbox" value="${h.id}" data-ver="${h.versionNumber}">
                            </td>
                        </tr>
                    `);
                });

                var modal = new bootstrap.Modal(document.getElementById('versionHistoryModal'));
                modal.show();
            });
        });

        // Version Log checkbox limit (max 2)
        $(document).on('change', '.version-checkbox', function () {
            var checked = $('.version-checkbox:checked');
            if (checked.length > 2) {
                $(this).prop('checked', false);
                Swal.fire({ icon: 'warning', title: 'Limit Reached', text: 'You can select up to 2 versions for side-by-side comparison.', confirmButtonColor: 'var(--primary)' });
                return;
            }

            $('#compareBtn').prop('disabled', checked.length !== 2);
        });

        // 3. COMPARE SELECTED VERSIONS
        $('#compareBtn').on('click', function () {
            var checked = $('.version-checkbox:checked');
            if (checked.length !== 2) return;

            var v1 = $(checked[0]);
            var v2 = $(checked[1]);

            var currentId, compareId;
            if (v1.data('ver') > v2.data('ver')) {
                currentId = v1.val();
                compareId = v2.val();
                $('#diffHeadCurrent').text(`Version ${v1.data('ver')} (Newer)`);
                $('#diffHeadCompare').text(`Version ${v2.data('ver')} (Older)`);
            } else {
                currentId = v2.val();
                compareId = v1.val();
                $('#diffHeadCurrent').text(`Version ${v2.data('ver')} (Newer)`);
                $('#diffHeadCompare').text(`Version ${v1.data('ver')} (Older)`);
            }

            $.get(_config.getDiffUrl, { currentId: currentId, compareId: compareId }, function (res) {
                $('#diffRowSales .compare-val').text(`₹${Number(res.compare.saleValue).toLocaleString()}`);
                $('#diffRowSales .current-val').text(`₹${Number(res.current.saleValue).toLocaleString()}`);
                applyDiffColor('#diffRowSales', res.current.saleValue, res.compare.saleValue);

                $('#diffRowDiscount .compare-val').text(`₹${Number(res.compare.onBillDiscount).toLocaleString()}`);
                $('#diffRowDiscount .current-val').text(`₹${Number(res.current.onBillDiscount).toLocaleString()}`);
                applyDiffColor('#diffRowDiscount', res.compare.onBillDiscount, res.current.onBillDiscount);

                $('#diffRowAchievement .compare-val').text(`${Number(res.compare.achievementPercent).toFixed(1)}%`);
                $('#diffRowAchievement .current-val').text(`${Number(res.current.achievementPercent).toFixed(1)}%`);
                applyDiffColor('#diffRowAchievement', res.current.achievementPercent, res.compare.achievementPercent);

                $('#diffRowSlab .compare-val').text(res.compare.slabApplied);
                $('#diffRowSlab .current-val').text(res.current.slabApplied);
                if (res.compare.slabApplied !== res.current.slabApplied) {
                    $('#diffRowSlab td').addClass('text-warning');
                } else {
                    $('#diffRowSlab td').removeClass('text-warning text-success text-danger');
                }

                $('#diffRowGross .compare-val').text(`₹${Number(res.compare.grossIncentive).toLocaleString()}`);
                $('#diffRowGross .current-val').text(`₹${Number(res.current.grossIncentive).toLocaleString()}`);
                applyDiffColor('#diffRowGross', res.current.grossIncentive, res.compare.grossIncentive);

                $('#diffRowTds .compare-val').text(`₹${Number(res.compare.tdsAmount).toLocaleString()} (${res.compare.tdsPercent}%)`);
                $('#diffRowTds .current-val').text(`₹${Number(res.current.tdsAmount).toLocaleString()} (${res.current.tdsPercent}%)`);
                applyDiffColor('#diffRowTds', res.compare.tdsAmount, res.current.tdsAmount);

                $('#diffRowNet .compare-val').text(`₹${Number(res.compare.netTransferAmount).toLocaleString()}`);
                $('#diffRowNet .current-val').text(`₹${Number(res.current.netTransferAmount).toLocaleString()}`);
                applyDiffColor('#diffRowNet', res.current.netTransferAmount, res.compare.netTransferAmount);

                $('#diffPlaceholderText').addClass('d-none');
                $('#diffResultsPanel').removeClass('d-none');
            });
        });
    }

    function applyDiffColor(rowId, newVal, oldVal) {
        var row = $(rowId);
        row.find('.current-val, .compare-val').removeClass('text-success text-danger text-warning');
        
        if (newVal > oldVal) {
            row.find('.current-val').addClass('text-success fw-bold');
            row.find('.compare-val').addClass('text-danger');
        } else if (newVal < oldVal) {
            row.find('.current-val').addClass('text-danger fw-bold');
            row.find('.compare-val').addClass('text-success');
        }
    }

    return { init: init };
})();
