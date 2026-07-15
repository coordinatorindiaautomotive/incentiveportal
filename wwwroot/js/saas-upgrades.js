/* ==========================================================================
   SAAS UPGRADES SCRIPTS — ThessBuddy Intelligent Enterprise OS
   ========================================================================== */

window.ThessBuddy = window.ThessBuddy || {};

ThessBuddy.UI = (function() {
    'use strict';

    function init() {
        bindEvents();
        refreshNotifBadge();
        setInterval(refreshNotifBadge, 120000);
        initTableResizers();
        
        // Relocate Modals to Body
        $('.modal').appendTo('body');
    }

    function bindEvents() {
        // Keyboard Shortcut Listener
        $(document).on('keydown', function(e) {
            if (e.ctrlKey && e.key === 'k') {
                e.preventDefault();
                $('#topbarSearchInput').focus().select().attr('placeholder', 'Type command (e.g. Helpdesk, Asset)...');
                if (typeof portalToast !== 'undefined') {
                    portalToast('Command Palette Activated. Type navigation shortcut.', 'info');
                }
            }
        });

        // Floating AI Copilot
        $('#floatingChatForm').on('submit', function(e) {
            e.preventDefault();
            const input = $('#floatingChatInput');
            const query = input.val().trim();
            if (!query) return;

            const chatBody = $('#floatingChatBody');
            chatBody.append(`<div class="p-2 mb-2 bg-primary text-white rounded text-end ms-auto" style="max-width: 85%;">${query}</div>`);
            input.val('');
            chatBody.scrollTop(chatBody[0].scrollHeight);

            $.ajax({
                url: '/AiCenter/CopilotChat',
                method: 'POST',
                data: { message: query },
                success: function(res) {
                    let formattedReply = res.reply
                        .replace(/\n/g, '<br/>')
                        .replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>')
                        .replace(/### (.*?)\n/g, '<strong>$1</strong><br/>')
                        .replace(/- (.*?)\n/g, '&bull; $1<br/>');

                    chatBody.append(`<div class="p-2 mb-2 bg-white rounded border text-secondary">${formattedReply}</div>`);
                    chatBody.scrollTop(chatBody[0].scrollHeight);
                },
                error: function() {
                    chatBody.append(`<div class="p-2 mb-2 bg-white rounded border text-danger">Failed to connect to ThessBuddy AI Engine. Check connection.</div>`);
                    chatBody.scrollTop(chatBody[0].scrollHeight);
                }
            });
        });
    }

    function refreshNotifBadge() {
        $.get('/NotificationCenter/GetUnreadCount', function(res) {
            const badge = $('#notifCountBadge');
            if (res && res.count > 0) {
                badge.text(res.count).removeClass('d-none');
            } else {
                badge.addClass('d-none');
            }
        }).fail(function() {
            // fail silently on unauth or network error
        });

        $.get('/NotificationCenter/GetNotificationsList', function(items) {
            const list = $('#notifDropdownList');
            if (!list.length) return;
            
            list.empty();
            
            if (items && items.length > 0) {
                items.forEach(n => {
                    const icon = n.notificationType === 'Workflow' ? 'fa-circle-check text-success' 
                               : n.notificationType === 'Escalation' ? 'fa-triangle-exclamation text-danger' 
                               : 'fa-info-circle text-primary';
                               
                    const el = $(`
                        <li class="dropdown-item p-2 mb-1 border-bottom" style="white-space:normal; cursor:pointer;" onclick="location.href='/NotificationCenter/Index'">
                            <div class="d-flex align-items-start gap-2">
                                <i class="fa-solid ${icon} pt-1"></i>
                                <div>
                                    <div class="fw-bold text-dark" style="font-size:0.75rem;">${n.title}</div>
                                    <div class="text-secondary" style="font-size:0.7rem;">${n.message}</div>
                                    <span class="text-muted" style="font-size:0.6rem;">${n.createdAt}</span>
                                </div>
                            </div>
                        </li>
                    `);
                    list.append(el);
                });
            } else {
                list.append('<li class="text-center py-3 text-muted">No new notifications</li>');
            }
        }).fail(function() {
            // fail silently
        });
    }

    function initTableResizers() {
        const tables = document.querySelectorAll('.custom-table');
        tables.forEach(table => {
            const cols = table.querySelectorAll('thead th');
            cols.forEach(col => {
                const resizer = document.createElement('div');
                resizer.classList.add('resizer');
                col.appendChild(resizer);
                
                let startX, startWidth;
                
                resizer.addEventListener('mousedown', function(e) {
                    startX = e.pageX;
                    startWidth = col.offsetWidth;
                    document.addEventListener('mousemove', mouseMoveHandler);
                    document.addEventListener('mouseup', mouseUpHandler);
                    e.preventDefault();
                });
                
                function mouseMoveHandler(e) {
                    const width = startWidth + (e.pageX - startX);
                    col.style.width = width + 'px';
                }
                
                function mouseUpHandler() {
                    document.removeEventListener('mousemove', mouseMoveHandler);
                    document.removeEventListener('mouseup', mouseUpHandler);
                }
            });
        });
    }

    function toggleFloatingChat() {
        const panel = $('#aiCopilotPanel');
        if (panel.hasClass('d-none')) {
            panel.removeClass('d-none');
            $('#floatingChatBody').scrollTop($('#floatingChatBody')[0].scrollHeight);
        } else {
            panel.addClass('d-none');
        }
    }

    return {
        init: init,
        refreshNotifBadge: refreshNotifBadge,
        toggleFloatingChat: toggleFloatingChat
    };
})();

// Legacy Global mappings for UI components that rely on inline onclick attributes
window.toggleFloatingChat = ThessBuddy.UI.toggleFloatingChat;
window.refreshNotifBadge = ThessBuddy.UI.refreshNotifBadge;

$(document).ready(function() {
    ThessBuddy.UI.init();
});
