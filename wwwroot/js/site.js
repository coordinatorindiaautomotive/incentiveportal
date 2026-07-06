/* ================================================================
   SECURITY — CSRF Token Global AJAX Setup (Fix Issue 5)
   Automatically injects RequestVerificationToken header into every
   jQuery AJAX POST/PUT/PATCH/DELETE so MVC [ValidateAntiForgeryToken]
   controllers accept the request without per-call boilerplate.
   API endpoints with [IgnoreAntiforgeryToken] safely ignore the header.
 ================================================================ */
(function () {
    'use strict';
    var tokenEl = document.querySelector('meta[name="csrf-token"]')
                  || document.querySelector('input[name="__RequestVerificationToken"]');
    var token   = tokenEl
        ? (tokenEl.tagName === 'META' ? tokenEl.getAttribute('content') : tokenEl.value)
        : null;

    if (token && typeof $ !== 'undefined') {
        $.ajaxSetup({
            beforeSend: function (xhr, settings) {
                // Only add for state-changing methods; never on GET / HEAD / OPTIONS
                var method = (settings.type || 'GET').toUpperCase();
                if (['POST', 'PUT', 'PATCH', 'DELETE'].includes(method)) {
                    xhr.setRequestHeader('RequestVerificationToken', token);
                }
            }
        });
    }
})();

/* ================================================================
   SIDEBAR TOGGLE SYSTEM
   - Desktop (>900px):  collapses to icon-rail, persisted in localStorage
   - Mobile  (<=900px): slides in as full-width drawer with overlay
 ================================================================ */
(function () {
  var shell    = document.getElementById('appShell');
  var sidebar  = document.getElementById('appSidebar');
  var overlay  = document.getElementById('sidebarOverlay');
  var toggle   = document.getElementById('sidebarToggle');
  var icon     = document.getElementById('sidebarToggleIcon');

  if (!shell || !toggle) return;

  var MOBILE_BP = 900; // px — matches CSS breakpoint

  function isMobile() { return window.innerWidth <= MOBILE_BP; }

  /* ── Desktop: toggle icon-rail collapse ── */
  function setDesktopCollapsed(collapsed) {
    shell.classList.toggle('sidebar-collapsed', collapsed);
    icon.className = collapsed ? 'fa-solid fa-bars-staggered' : 'fa-solid fa-bars';
    try { localStorage.setItem('sidebar-collapsed', collapsed ? '1' : '0'); } catch(e) {}
  }

  /* ── Mobile: toggle drawer open/close ── */
  function setMobileOpen(open) {
    shell.classList.toggle('sidebar-open', open);
    if (overlay) overlay.classList.toggle('active', open);
    icon.className = open ? 'fa-solid fa-xmark' : 'fa-solid fa-bars';
    document.body.style.overflow = open ? 'hidden' : '';
  }

  /* ── Restore desktop state from localStorage ── */
  function restoreState() {
    if (!isMobile()) {
      var saved = '0';
      try { saved = localStorage.getItem('sidebar-collapsed') || '0'; } catch(e) {}
      setDesktopCollapsed(saved === '1');
    } else {
      // Always start mobile with sidebar closed
      setMobileOpen(false);
      shell.classList.remove('sidebar-collapsed');
    }
  }

  /* ── Main toggle handler ── */
  toggle.addEventListener('click', function () {
    if (isMobile()) {
      var isOpen = shell.classList.contains('sidebar-open');
      setMobileOpen(!isOpen);
    } else {
      var isCollapsed = shell.classList.contains('sidebar-collapsed');
      setDesktopCollapsed(!isCollapsed);
    }
  });

  /* ── Close drawer when overlay is clicked ── */
  if (overlay) {
    overlay.addEventListener('click', function () { setMobileOpen(false); });
  }

  /* ── Close drawer when a nav link is clicked (mobile UX) ── */
  /* NOTE: Exclude .nav-dropdown-trigger so toggling a submenu does NOT
     close the mobile sidebar — the user still needs to pick a child item. */
  if (sidebar) {
    sidebar.querySelectorAll('.nav-link:not(.nav-dropdown-trigger)').forEach(function (link) {
      link.addEventListener('click', function () {
        if (isMobile()) setMobileOpen(false);
      });
    });

    sidebar.querySelectorAll('.nav-dropdown-trigger').forEach(function (trigger) {
      trigger.addEventListener('click', function () {
        /* On desktop: auto-expand sidebar if it was collapsed */
        if (!isMobile() && shell.classList.contains('sidebar-collapsed')) {
          setDesktopCollapsed(false);
        }
      });
    });
  }

  /* ── Re-evaluate on resize (e.g. rotate phone) ── */
  var resizeTimer;
  window.addEventListener('resize', function () {
    clearTimeout(resizeTimer);
    resizeTimer = setTimeout(function () {
      if (!isMobile()) {
        // Clean up mobile open state
        shell.classList.remove('sidebar-open');
        if (overlay) overlay.classList.remove('active');
        document.body.style.overflow = '';
        icon.className = 'fa-solid fa-bars';
        // Restore desktop state
        var saved = '0';
        try { saved = localStorage.getItem('sidebar-collapsed') || '0'; } catch(e) {}
        setDesktopCollapsed(saved === '1');
      } else {
        shell.classList.remove('sidebar-collapsed');
        setMobileOpen(false);
      }
    }, 120);
  });

  /* ── Add data-tooltip to nav links for icon-rail tooltips ── */
  if (sidebar) {
    sidebar.querySelectorAll('.nav-link').forEach(function (link) {
      var label = link.querySelector('.nav-label');
      if (label) link.setAttribute('data-tooltip', label.textContent.trim());
    });
  }

  /* Initialise */
  restoreState();
})();

/* ================================================================
   SIDEBAR DROPDOWN ACCORDION — Persistent expanded-state fix
   Keeps the correct submenu open across page navigations.
   Uses localStorage key "sidebar-open-panel" to remember the last
   manually-expanded (or server-marked-active) collapse panel.
   ================================================================ */
(function () {
  'use strict';

  var STORAGE_KEY = 'sidebar-open-panel';

  // Helper to force display / hide styles to prevent CSS layout conflicts
  function syncStyle(el, show) {
    if (show) {
      el.style.setProperty('display', 'block', 'important');
      el.style.setProperty('height', 'auto', 'important');
      el.style.setProperty('visibility', 'visible', 'important');
      el.style.setProperty('opacity', '1', 'important');
    } else {
      el.style.setProperty('display', 'none', 'important');
      el.style.setProperty('height', '0px', 'important');
    }
  }

  // Collect all Bootstrap collapse panels inside the sidebar
  var collapseEls = document.querySelectorAll('.side-nav .collapse');
  if (!collapseEls.length) return;

  // Determine which panel Bootstrap has already opened via the server-rendered
  // "show" class (i.e. the currently-active controller's panel).
  var serverOpenId = null;
  collapseEls.forEach(function (el) {
    if (el.classList.contains('show')) {
      serverOpenId = el.id;
    }
  });

  // If the server pre-opened a panel (active route), persist it and honour it.
  if (serverOpenId) {
    try { localStorage.setItem(STORAGE_KEY, serverOpenId); } catch (e) {}
  }

  // Retrieve the persisted panel id (falls back to the server-open panel).
  var savedId = null;
  try { savedId = localStorage.getItem(STORAGE_KEY); } catch (e) {}

  // If a saved panel exists but Bootstrap hasn't opened it yet, open it now.
  if (savedId && !serverOpenId) {
    var savedEl = document.getElementById(savedId);
    if (savedEl && savedEl.classList.contains('collapse')) {
      // Use Bootstrap's Collapse API so aria-expanded syncs correctly.
      var bsCollapse = bootstrap.Collapse.getOrCreateInstance(savedEl, { toggle: false });
      bsCollapse.show();
    }
  }

  // Sync initial styling for pre-opened items
  collapseEls.forEach(function (el) {
    if (el.classList.contains('show') || (savedId && el.id === savedId)) {
      syncStyle(el, true);
    } else {
      syncStyle(el, false);
    }
  });

  // Listen to Bootstrap's collapse events on the sidebar to persist state and sync styles.
  collapseEls.forEach(function (el) {
    el.addEventListener('show.bs.collapse', function () {
      syncStyle(el, true);
      try { localStorage.setItem(STORAGE_KEY, el.id); } catch (e) {}
    });

    el.addEventListener('shown.bs.collapse', function () {
      syncStyle(el, true);
      try { localStorage.setItem(STORAGE_KEY, el.id); } catch (e) {}
    });

    el.addEventListener('hide.bs.collapse', function () {
      syncStyle(el, false);
    });

    el.addEventListener('hidden.bs.collapse', function () {
      syncStyle(el, false);
      // Only clear if this is the panel that was saved.
      try {
        var current = localStorage.getItem(STORAGE_KEY);
        if (current === el.id) localStorage.removeItem(STORAGE_KEY);
      } catch (e) {}
    });
  });
})();

window.portalSetBusy = function (button, busyText) {
    if (!button) return;
    button.dataset.originalText = button.innerText;
    button.disabled = true;
    button.innerText = busyText || 'Working...';
};

window.portalClearBusy = function (button) {
    if (!button) return;
    button.disabled = false;
    button.innerText = button.dataset.originalText || button.innerText;
};

window.portalToast = function (message, type = 'info') {
    const icon = type === 'error' || type === 'danger' ? 'error' 
               : type === 'success' ? 'success' 
               : type === 'warning' ? 'warning' 
               : 'info';
               
    const Toast = Swal.mixin({
        toast: true,
        position: 'top-end',
        showConfirmButton: false,
        timer: 3000,
        timerProgressBar: true,
        didOpen: (toast) => {
            toast.addEventListener('mouseenter', Swal.stopTimer);
            toast.addEventListener('mouseleave', Swal.resumeTimer);
        }
    });
    Toast.fire({
        icon: icon,
        title: message
    });
};

// AJAX form submission using Lottie Loader inside SweetAlert2
$(document).on('submit', 'form[data-ajax="true"]', function (event) {
    event.preventDefault();
    const form = $(this);
    
    Swal.fire({
        title: 'Processing...',
        html: '<div style="display: flex; flex-direction: column; align-items: center; padding: 15px 0;"><div class="rounder" style="width:60px; height:60px; border:4px solid rgba(37, 99, 235, 0.1); border-left-color:#2563eb; border-radius:50%; animation:spin 1s linear infinite; position:relative;"><style>@keyframes spin{0%{transform:rotate(0deg);}100%{transform:rotate(360deg);}} .rounder::after{content:\'\'; position:absolute; top:5px; left:5px; right:5px; bottom:5px; border:4px solid rgba(6, 182, 212, 0.1); border-right-color:#06b6d4; border-radius:50%; animation:spin-reverse 1.5s linear infinite;} @keyframes spin-reverse{0%{transform:rotate(360deg);}100%{transform:rotate(0deg);}}</style></div><p style="margin-top: 20px; font-family:\'Poppins\', sans-serif; font-weight:600; color:#1E293B;">Executing request, please wait...</p></div>',
        showConfirmButton: false,
        allowOutsideClick: false
    });

    $.ajax({
        url: form.attr('action'),
        method: form.attr('method') || 'POST',
        data: form.serialize(),
        success: function (response) {
            Swal.fire({
                icon: 'success',
                title: 'Success!',
                text: response.message || 'Saved successfully.',
                showConfirmButton: false,
                timer: 1000
            }).then(() => {
                if (response.ok) location.reload();
            });
        },
        error: function (xhr) {
            Swal.fire({
                icon: 'error',
                title: 'Request Failed',
                text: xhr.responseJSON?.message || xhr.responseText || 'An error occurred.',
                confirmButtonColor: '#2563eb'
            });
        }
    });
});

/* ================================================================
   INACTIVITY AUTO-LOGOUT SYSTEM (10 Minutes)
   ================================================================ */
(function () {
    let inactivityTimer;
    let warningTimer;
    let countdownInterval;
    let isWarningActive = false;

    const timeoutLimit = 10 * 60 * 1000; // 10 minutes
    const warningLimit = 9 * 60 * 1000;  // 9 minutes (shows warning 1 min before)
    const countdownTime = 60;            // 60 seconds countdown

    function resetInactivityTimer() {
        if (isWarningActive) return;

        clearTimeout(inactivityTimer);
        clearTimeout(warningTimer);

        // Only track inactivity if user is logged in (logout form is present)
        const logoutForm = document.querySelector('form[action*="Logout"]');
        if (!logoutForm) return;

        warningTimer = setTimeout(showInactivityWarning, warningLimit);
        inactivityTimer = setTimeout(performAutoLogout, timeoutLimit);
    }

    function showInactivityWarning() {
        isWarningActive = true;
        let secondsLeft = countdownTime;

        // Pause main inactivity timer while warning is open
        clearTimeout(inactivityTimer);

        Swal.fire({
            title: 'Session Expiring',
            html: `Your session will expire in <strong id="session-countdown" style="color: #004C8F; font-size: 1.2em;">${secondsLeft}</strong> seconds due to inactivity.<br/><br/>Would you like to stay logged in?`,
            icon: 'warning',
            showCancelButton: true,
            confirmButtonText: 'Stay Logged In',
            cancelButtonText: 'Logout Now',
            confirmButtonColor: '#004C8F',
            cancelButtonColor: '#ef4444',
            allowOutsideClick: false,
            allowEscapeKey: false,
            didOpen: () => {
                countdownInterval = setInterval(() => {
                    secondsLeft--;
                    const countdownEl = document.getElementById('session-countdown');
                    if (countdownEl) {
                        countdownEl.textContent = secondsLeft;
                    }
                    if (secondsLeft <= 0) {
                        clearInterval(countdownInterval);
                        performAutoLogout();
                    }
                }, 1000);
            }
        }).then((result) => {
            isWarningActive = false;
            clearInterval(countdownInterval);
            if (result.isConfirmed) {
                // Ping the server to refresh sliding auth token
                fetch('/Home/Ping')
                    .then(() => {
                        resetInactivityTimer();
                        portalToast('Session extended successfully!', 'success');
                    })
                    .catch(() => {
                        resetInactivityTimer();
                    });
            } else {
                performAutoLogout();
            }
        });
    }

    function performAutoLogout() {
        const logoutForm = document.querySelector('form[action*="Logout"]');
        if (logoutForm) {
            logoutForm.submit();
        } else {
            window.location.href = '/Auth/Login';
        }
    }

    // Register active user events to reset timer on interaction
    const activityEvents = ['mousemove', 'mousedown', 'keypress', 'scroll', 'touchstart'];
    activityEvents.forEach(event => {
        document.addEventListener(event, resetInactivityTimer, true);
    });

    $(document).ready(function () {
        resetInactivityTimer();
    });
})();

// Global Client-Side File Upload Format Checker
$(document).ready(function() {
    $(document).on('change', 'input[type="file"]', function (e) {
        const accept = $(this).attr('accept');
        if (!accept) return;

        const files = this.files;
        if (!files || files.length === 0) return;

        const file = files[0];
        const fileName = file.name;
        const fileExt = '.' + fileName.split('.').pop().toLowerCase();

        const allowedExts = accept.split(',').map(x => x.trim().toLowerCase());
        
        if (allowedExts.indexOf(fileExt) === -1) {
            Swal.fire({
                icon: 'error',
                title: 'Incorrect File Format',
                html: `<p style="font-family:'Poppins', sans-serif; font-size: 0.9rem;">The selected file <strong>"${fileName}"</strong> is not in the correct format.</p>
                       <p style="font-family:'Poppins', sans-serif; font-size: 0.85rem; color: #64748b;">Please upload a file with the following extension(s): <strong>${accept}</strong></p>`,
                confirmButtonColor: '#2563eb',
                confirmButtonText: 'Understood'
            });

            // Cancel upload
            this.value = '';

            // Reset filename badges/elements across various pages
            $('#excelFileName').text("Click or Drag Excel file here to browse...").removeClass('fw-bold text-dark');
            $('#selectedFileInfo').text('').addClass('d-none');
            $('#selectedFileName').text('').addClass('d-none');
            $('#excelFileText').text('');
            
            // Disable process upload button for Outstanding Master
            $('#processUploadBtn').prop('disabled', true);
            
            e.preventDefault();
            return false;
        }
    });
});

/* ================================================================
   THEME TOGGLE, SEARCH SUGGESTIONS & HELP MODAL (UI/UX Upgrades)
   ================================================================ */
$(document).ready(function() {
    // ── 1. Theme Management ──
    const themeBtn = $('#themeToggleBtn');
    const themeIcon = $('#themeToggleIcon');
    
    function applyTheme(theme) {
        if (theme === 'dark') {
            document.documentElement.setAttribute('data-theme', 'dark');
            document.body.classList.add('dark-theme');
            themeIcon.removeClass('fa-regular fa-moon').addClass('fa-solid fa-sun');
            themeBtn.attr('title', 'Toggle Light Mode');
        } else {
            document.documentElement.removeAttribute('data-theme');
            document.body.classList.remove('dark-theme');
            themeIcon.removeClass('fa-solid fa-sun').addClass('fa-regular fa-moon');
            themeBtn.attr('title', 'Toggle Dark Mode');
        }
    }

    // Initialize from localStorage
    const savedTheme = localStorage.getItem('theme') || 'light';
    applyTheme(savedTheme);

    // Toggle theme event click
    themeBtn.on('click', function() {
        const isDark = document.body.classList.contains('dark-theme');
        const nextTheme = isDark ? 'light' : 'dark';
        localStorage.setItem('theme', nextTheme);
        applyTheme(nextTheme);
        portalToast('Theme updated to ' + nextTheme + ' mode', 'success');
    });

    // ── 2. Topbar Search Suggest ──
    const searchInput = $('#topbarSearchInput');
    const searchDropdown = $('#searchDropdown');
    let searchTimeout = null;

    searchInput.on('input focus', function() {
        const query = $(this).val().trim();
        clearTimeout(searchTimeout);

        if (query.length < 2) {
            searchDropdown.empty().hide();
            return;
        }

        searchTimeout = setTimeout(function() {
            $.ajax({
                url: '/Home/Search',
                method: 'GET',
                data: { q: query },
                success: function(response) {
                    searchDropdown.empty();
                    
                    if (!response || response.length === 0) {
                        searchDropdown.append('<div style="padding: 8px 12px; font-size: 0.82rem; color: var(--muted);">No results found</div>');
                        searchDropdown.show();
                        return;
                    }

                    // Group results by category
                    const grouped = {};
                    response.forEach(item => {
                        if (!grouped[item.category]) {
                            grouped[item.category] = [];
                        }
                        grouped[item.category].push(item);
                    });

                    // Render groups
                    for (const cat in grouped) {
                        searchDropdown.append(`<div style="font-size: 0.65rem; font-weight: 700; text-transform: uppercase; color: var(--muted-2); letter-spacing: 0.05em; padding: 6px 12px 2px;">${cat}</div>`);
                        grouped[cat].forEach(item => {
                            const link = $(`<a href="${item.url}" class="dropdown-item py-1" style="border-radius: 4px; display: flex; align-items: center; gap: 8px; font-size: 0.82rem; color: var(--ink-2); text-decoration: none;">
                                <i class="fa-solid ${item.icon}" style="font-size: 0.8rem; width: 14px; text-align: center; color: var(--muted);"></i>
                                <span>${item.title}</span>
                            </a>`);
                            searchDropdown.append(link);
                        });
                    }
                    searchDropdown.show();
                },
                error: function() {
                    searchDropdown.empty().hide();
                }
            });
        }, 250);
    });

    // Close search list when clicking outside
    $(document).on('click', function(e) {
        if (!$(e.target).closest('.topbar-search-box').length) {
            searchDropdown.hide();
        }
    });

    // Focus search on Ctrl + /
    $(document).on('keydown', function(e) {
        if (e.ctrlKey && e.key === '/') {
            e.preventDefault();
            searchInput.focus().select();
        }
    });
});

// ── 3. Help & Support Modal ──
window.showHelpModal = function() {
    Swal.fire({
        title: 'ThessBuddy Help & Support',
        html: `
            <div style="text-align: left; font-family: 'Inter', sans-serif; font-size: 0.85rem; line-height: 1.5; color: var(--ink-2);">
                <p>Welcome to <strong>ThessBuddy</strong>, Powered by ThessSystems.</p>
                <p>This portal is configured for secure genuine parts distribution, sales incentive calculations, cash books, and branch analytics.</p>
                <div style="background: var(--canvas); border-radius: 8px; padding: 12px; margin-top: 15px; border: 1px solid var(--line);">
                    <p style="margin-bottom: 6px; font-weight: 700; color: var(--ink);"><i class="fa-solid fa-circle-info text-primary me-1"></i> Technical Assistance</p>
                    <p style="margin: 2px 0;"><strong>Support Email:</strong> <a href="mailto:support@thessbuddy.com" style="color: var(--primary);">support@thessbuddy.com</a></p>
                    <p style="margin: 2px 0;"><strong>Support Hotline:</strong> +91 (1800) 555-0199 (24/7)</p>
                </div>
            </div>
        `,
        icon: 'info',
        confirmButtonText: 'Dismiss',
        confirmButtonColor: '#1b63f2',
        background: 'var(--panel)',
        color: 'var(--ink)'
    });
};

// ── 4. Global Excel/CSV Exporter Utilities ──
window.exportTableToCSV = function(tableSelector, filename) {
    var table = document.querySelector(tableSelector);
    if (!table) return;
    
    var csv = [];
    var rows = table.querySelectorAll("tr");
    
    for (var i = 0; i < rows.length; i++) {
        if (rows[i].style.display === "none") continue;
        
        var row = [], cols = rows[i].querySelectorAll("td, th");
        for (var j = 0; j < cols.length; j++) {
            if (cols[j].querySelector("button") || cols[j].querySelector("input[type='checkbox']") || cols[j].classList.contains("no-export")) {
                continue;
            }
            
            var text = cols[j].innerText || cols[j].textContent;
            text = text.replace(/₹/g, "").trim();
            text = text.replace(/"/g, '""');
            if (text.indexOf(",") > -1 || text.indexOf("\n") > -1 || text.indexOf('"') > -1) {
                text = '"' + text + '"';
            }
            row.push(text);
        }
        if (row.length > 0) {
            csv.push(row.join(","));
        }
    }
    
    var csvContent = "\uFEFF" + csv.join("\n");
    var blob = new Blob([csvContent], { type: "text/csv;charset=utf-8;" });
    var link = document.createElement("a");
    var url = URL.createObjectURL(blob);
    link.setAttribute("href", url);
    link.setAttribute("download", filename || "export.csv");
    link.style.visibility = "hidden";
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
};

window.exportDataTableToCSV = function(tableSelector, filename) {
    var dt = $(tableSelector).DataTable();
    if (!dt) return;
    
    var csv = [];
    
    // Get headers
    var headers = [];
    $(tableSelector + " thead th").each(function() {
        var text = $(this).text().trim();
        if (text && text !== "Actions" && !$(this).hasClass("no-export")) {
            headers.push('"' + text.replace(/"/g, '""') + '"');
        }
    });
    csv.push(headers.join(","));
    
    // Get all filtered data rows
    var data = dt.rows({ search: 'applied' }).data();
    for (var i = 0; i < data.length; i++) {
        var row = [];
        var rowData = data[i];
        for (var j = 0; j < rowData.length; j++) {
            var th = $(tableSelector + " thead th").eq(j);
            if (th.text().trim() === "Actions" || th.hasClass("no-export")) {
                continue;
            }
            
            var cellHTML = rowData[j];
            var cellText = $("<div>").html(cellHTML).text().trim();
            cellText = cellText.replace(/₹/g, "").trim();
            cellText = cellText.replace(/"/g, '""');
            row.push('"' + cellText + '"');
        }
        csv.push(row.join(","));
    }
    
    var csvContent = "\uFEFF" + csv.join("\n");
    var blob = new Blob([csvContent], { type: "text/csv;charset=utf-8;" });
    var link = document.createElement("a");
    var url = URL.createObjectURL(blob);
    link.setAttribute("href", url);
    link.setAttribute("download", filename || "export.csv");
    link.style.visibility = "hidden";
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
};



