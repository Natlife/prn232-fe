(function () {
    const STORAGE_KEY_SESSION   = "chat_session_id";
    const STORAGE_KEY_OPEN      = "chat_panel_open";
    const STORAGE_KEY_MESSAGES  = "chat_messages_cache";  // sessionStorage – clears on tab close

    // ─── INIT SESSION ────────────────────────────────────────────────────────
    let sessionId = localStorage.getItem(STORAGE_KEY_SESSION);
    if (!sessionId) {
        sessionId = 'sess_' + Math.random().toString(36).substr(2, 9) + Date.now().toString(36);
        localStorage.setItem(STORAGE_KEY_SESSION, sessionId);
    }

    // ─── MESSAGE CACHE (sessionStorage) ──────────────────────────────────────
    // Format: [{role, text, suggestedItems, orderLink}]
    // Uses sessionStorage so data survives page navigation within same tab,
    // but is cleared when user closes the tab/browser.
    function loadCachedMessages() {
        try {
            const raw = sessionStorage.getItem(STORAGE_KEY_MESSAGES);
            return raw ? JSON.parse(raw) : null;
        } catch { return null; }
    }

    function saveCachedMessages(messages) {
        try {
            // Keep last 60 messages max to avoid storage bloat
            const trimmed = messages.slice(-60);
            sessionStorage.setItem(STORAGE_KEY_MESSAGES, JSON.stringify(trimmed));
        } catch { /* sessionStorage full – ignore */ }
    }

    function clearCachedMessages() {
        sessionStorage.removeItem(STORAGE_KEY_MESSAGES);
    }

    // In-memory message array (source of truth for this page load)
    let _messages = loadCachedMessages() || [];

    // ─── DOM INJECTION ───────────────────────────────────────────────────────
    const widgetHtml = `
    <div id="ai-chat-widget">
        <!-- Floating Toggle Button -->
        <button id="ai-chat-btn" class="ai-chat-btn" title="Trò chuyện với trợ lý AI">
            <i class="bi bi-robot"></i>
        </button>

        <!-- Chat Panel -->
        <div id="ai-chat-panel" class="ai-chat-panel">
            <!-- Header -->
            <div class="ai-chat-header">
                <div class="ai-chat-title">
                    <i class="bi bi-cpu fs-5"></i>
                    <div>
                        <h6>Trợ lý ảo AI</h6>
                        <span>Showroom Group7</span>
                    </div>
                </div>
                <div class="ai-chat-actions">
                    <button id="ai-chat-clear" title="Xóa hội thoại, làm mới" class="me-2">
                        <i class="bi bi-trash"></i>
                    </button>
                    <button id="ai-chat-close" title="Đóng">
                        <i class="bi bi-x-lg"></i>
                    </button>
                </div>
            </div>

            <!-- Messages Area -->
            <div id="ai-chat-messages" class="ai-chat-messages">
                <div class="message-bubble message-ai">
                    Xin chào! Tôi là trợ lý ảo AI của Showroom Group7. Tôi có thể giúp bạn tìm kiếm thông tin về ô tô, phụ tùng thay thế hoặc các gói dịch vụ bảo dưỡng phù hợp. Hãy hỏi tôi bất cứ điều gì nhé!
                </div>
            </div>

            <!-- Input Area -->
            <div class="ai-chat-input-area">
                <input type="text" id="ai-chat-input" placeholder="Nhập tin nhắn..." autocomplete="off" />
                <button id="ai-chat-send" class="ai-chat-send-btn">
                    <i class="bi bi-send-fill"></i>
                </button>
            </div>
        </div>
    </div>
    `;

    // Append to body
    document.body.insertAdjacentHTML('beforeend', widgetHtml);

    const chatBtn      = document.getElementById("ai-chat-btn");
    const chatPanel    = document.getElementById("ai-chat-panel");
    const chatClose    = document.getElementById("ai-chat-close");
    const chatClear    = document.getElementById("ai-chat-clear");
    const chatInput    = document.getElementById("ai-chat-input");
    const chatSend     = document.getElementById("ai-chat-send");
    const chatMessages = document.getElementById("ai-chat-messages");

    // ─── RESTORE MESSAGES FROM CACHE ─────────────────────────────────────────
    function restoreMessagesFromCache() {
        if (_messages.length === 0) return;
        chatMessages.innerHTML = ""; // clear default greeting
        _messages.forEach(msg => {
            renderBubble(msg.role, msg.text, msg.suggestedItems || null, msg.orderLink || null, /*save=*/false);
        });
        chatMessages.scrollTop = chatMessages.scrollHeight;
    }

    // ─── RESTORE PANEL STATE (open/close) ────────────────────────────────────
    const chatWasOpen = localStorage.getItem(STORAGE_KEY_OPEN) === "true";
    if (chatWasOpen) {
        // Show instantly, no animation flash on page load
        chatPanel.style.transition = "none";
        chatPanel.classList.add("active");
        requestAnimationFrame(() => requestAnimationFrame(() => {
            chatPanel.style.transition = "";
        }));
        // Restore messages immediately from sessionStorage cache
        restoreMessagesFromCache();
    }

    // ─── ACTIONS ─────────────────────────────────────────────────────────────
    chatBtn.addEventListener("click", () => {
        const isNowOpen = chatPanel.classList.toggle("active");
        localStorage.setItem(STORAGE_KEY_OPEN, isNowOpen ? "true" : "false");
        if (isNowOpen) {
            chatInput.focus();
            // If no cached messages, try fetching from server
            if (_messages.length === 0) {
                fetchHistoryFromServer();
            } else {
                restoreMessagesFromCache();
            }
        }
    });

    chatClose.addEventListener("click", () => {
        chatPanel.classList.remove("active");
        localStorage.setItem(STORAGE_KEY_OPEN, "false");
    });

    chatClear.addEventListener("click", () => {
        if (confirm("Bạn có chắc chắn muốn xóa lịch sử cuộc trò chuyện và bắt đầu phiên mới?")) {
            // New session
            sessionId = 'sess_' + Math.random().toString(36).substr(2, 9) + Date.now().toString(36);
            localStorage.setItem(STORAGE_KEY_SESSION, sessionId);
            // Clear cache
            _messages = [];
            clearCachedMessages();
            // Reset UI
            chatMessages.innerHTML = `
                <div class="message-bubble message-ai">
                    Hội thoại đã được làm mới. Tôi có thể hỗ trợ gì cho bạn ngay bây giờ?
                </div>
            `;
        }
    });

    chatInput.addEventListener("keypress", (e) => {
        if (e.key === "Enter") sendMessage();
    });
    chatSend.addEventListener("click", sendMessage);

    // ─── FETCH HISTORY FROM SERVER (fallback only) ───────────────────────────
    async function fetchHistoryFromServer() {
        try {
            const res = await fetch(`/Chat/History?sessionId=${sessionId}`);
            if (!res.ok) return;
            const data = await res.json();
            if (data && data.messages && data.messages.length > 0) {
                chatMessages.innerHTML = "";
                _messages = [];
                data.messages.forEach(msg => {
                    if (msg.role === "user") {
                        pushAndRender("user", msg.content);
                    } else if (msg.role === "assistant") {
                        pushAndRender("ai", msg.content);
                    }
                });
                chatMessages.scrollTop = chatMessages.scrollHeight;
            }
        } catch (ex) {
            console.error("Lỗi khi tải lịch sử chat:", ex);
        }
    }

    // ─── SEND MESSAGE ────────────────────────────────────────────────────────
    async function sendMessage() {
        const text = chatInput.value.trim();
        if (!text) return;

        chatInput.value = "";
        pushAndRender("user", text);

        const typingId = showTypingIndicator();

        try {
            const res = await fetch("/Chat/Message", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ sessionId, message: text })
            });

            removeTypingIndicator(typingId);

            if (res.ok) {
                const apiResult = await res.json();
                if (apiResult && apiResult.success && apiResult.data) {
                    const data = apiResult.data;
                    pushAndRender("ai", data.reply, data.suggestedItems, data.orderLink);
                } else {
                    const msg = apiResult?.message || "Đã xảy ra lỗi không mong muốn.";
                    pushAndRender("ai", "Lỗi: " + msg);
                }
            } else {
                pushAndRender("ai", "Dịch vụ AI đang gặp sự cố. Vui lòng thử lại sau.");
            }
        } catch (ex) {
            removeTypingIndicator(typingId);
            pushAndRender("ai", "Lỗi kết nối tới máy chủ.");
        }
    }

    // ─── CORE: Push to cache + render ────────────────────────────────────────
    function pushAndRender(role, text, suggestedItems = null, orderLink = null) {
        _messages.push({ role, text, suggestedItems: suggestedItems || null, orderLink: orderLink || null });
        saveCachedMessages(_messages);
        renderBubble(role, text, suggestedItems, orderLink, /*save=*/false);
    }

    // ─── RENDER BUBBLE ───────────────────────────────────────────────────────
    function renderBubble(role, text, suggestedItems = null, orderLink = null) {
        const bubble = document.createElement("div");
        bubble.className = `message-bubble ${role === "user" ? "message-user" : "message-ai"}`;

        // Format markdown-like elements
        let formattedText = escapeHtml(text)
            .replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>')
            .replace(/\*(.*?)\*/g, '<em>$1</em>')
            .replace(/!\[(.*?)\]\((.*?)\)/g, '<img src="$2" alt="$1" class="img-fluid rounded my-2 d-block" style="max-height:180px; width:auto; object-fit:cover; border: 1px solid #ddd;" />')
            .replace(/\n/g, '<br/>');

        // Parse tables
        if (formattedText.includes('|')) {
            formattedText = parseMarkdownTable(formattedText);
        }

        bubble.innerHTML = formattedText;

        // Suggested product cards
        if (suggestedItems && suggestedItems.length > 0) {
            const container = document.createElement("div");
            container.className = "ai-suggested-container";

            suggestedItems.forEach(item => {
                const a = document.createElement("a");
                a.className = "ai-suggested-item";
                a.href = item.detailUrl;
                a.target = "_blank";

                const imgHtml = item.imageUrl
                    ? `<img src="${item.imageUrl}" alt="${item.name}" />`
                    : `<div class="d-flex align-items-center justify-content-center bg-white border text-secondary" style="width:40px;height:40px;border-radius:6px;"><i class="bi bi-box"></i></div>`;

                a.innerHTML = `
                    ${imgHtml}
                    <div class="ai-suggested-item-info">
                        <div class="ai-suggested-item-name">${item.name}</div>
                        <div class="ai-suggested-item-price">${formatVnd(item.price)}</div>
                    </div>
                    <i class="bi bi-chevron-right text-muted small"></i>
                `;
                container.appendChild(a);
            });
            bubble.appendChild(container);
        }

        // Order / deposit button
        if (orderLink) {
            const btn = document.createElement("a");
            btn.className = "ai-draft-order-btn";
            btn.href = orderLink;
            if (orderLink.includes("/Cars/Details/")) {
                btn.innerHTML = `<i class="bi bi-car-front-fill me-1"></i> Bấm để Đặt Cọc / Mua Đứt Xe`;
            } else {
                btn.innerHTML = `<i class="bi bi-cart-fill me-1"></i> Bấm để Xem &amp; Xác Nhận Đơn Hàng`;
            }
            bubble.appendChild(btn);
        }

        chatMessages.appendChild(bubble);
        chatMessages.scrollTop = chatMessages.scrollHeight;
    }

    // ─── TYPING INDICATOR ────────────────────────────────────────────────────
    function showTypingIndicator() {
        const bubble = document.createElement("div");
        const typingId = "typing_" + Date.now();
        bubble.id = typingId;
        bubble.className = "message-bubble message-ai";
        bubble.innerHTML = `
            <div class="typing-indicator">
                <div class="typing-dot"></div>
                <div class="typing-dot"></div>
                <div class="typing-dot"></div>
            </div>
        `;
        chatMessages.appendChild(bubble);
        chatMessages.scrollTop = chatMessages.scrollHeight;
        return typingId;
    }

    function removeTypingIndicator(id) {
        const el = document.getElementById(id);
        if (el) el.remove();
    }

    // ─── UTILITIES ───────────────────────────────────────────────────────────
    function escapeHtml(str) {
        return str
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#039;");
    }

    function formatVnd(val) {
        return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(val);
    }

    function parseMarkdownTable(text) {
        const lines = text.split('<br/>');
        let inTable = false;
        let htmlTable = '<table class="table table-sm table-bordered mt-2" style="font-size:12px;">';
        let outputLines = [];

        lines.forEach(line => {
            if (line.trim().startsWith('|')) {
                if (!inTable) inTable = true;
                if (line.includes('---') || line.includes('-:-')) return;

                const cells = line.split('|').map(c => c.trim()).filter((c, idx, arr) => idx > 0 && idx < arr.length - 1);
                const tag = htmlTable.includes('<tbody>') ? 'td' : 'th';

                let row = '<tr>';
                cells.forEach(cell => { row += `<${tag}>${cell}</${tag}>`; });
                row += '</tr>';

                if (tag === 'th') {
                    htmlTable += `<thead>${row}</thead><tbody>`;
                } else {
                    htmlTable += row;
                }
            } else {
                if (inTable) {
                    htmlTable += '</tbody></table>';
                    outputLines.push(htmlTable);
                    htmlTable = '<table class="table table-sm table-bordered mt-2" style="font-size:12px;">';
                    inTable = false;
                }
                outputLines.push(line);
            }
        });

        if (inTable) {
            htmlTable += '</tbody></table>';
            outputLines.push(htmlTable);
        }

        return outputLines.join('<br/>');
    }
})();
