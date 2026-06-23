(function () {
    // ─── INIT SESSION ────────────────────────────────────────────────────────
    let sessionId = localStorage.getItem("chat_session_id");
    if (!sessionId) {
        sessionId = 'sess_' + Math.random().toString(36).substr(2, 9) + Date.now().toString(36);
        localStorage.setItem("chat_session_id", sessionId);
    }

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

    const chatBtn = document.getElementById("ai-chat-btn");
    const chatPanel = document.getElementById("ai-chat-panel");
    const chatClose = document.getElementById("ai-chat-close");
    const chatClear = document.getElementById("ai-chat-clear");
    const chatInput = document.getElementById("ai-chat-input");
    const chatSend = document.getElementById("ai-chat-send");
    const chatMessages = document.getElementById("ai-chat-messages");

    let isHistoryLoaded = false;

    // ─── ACTIONS ─────────────────────────────────────────────────────────────
    chatBtn.addEventListener("click", () => {
        chatPanel.classList.toggle("active");
        if (chatPanel.classList.contains("active")) {
            chatInput.focus();
            if (!isHistoryLoaded) {
                loadHistory();
            }
        }
    });

    chatClose.addEventListener("click", () => {
        chatPanel.classList.remove("active");
    });

    chatClear.addEventListener("click", () => {
        if (confirm("Bạn có chắc chắn muốn xóa lịch sử cuộc trò chuyện và bắt đầu phiên mới?")) {
            sessionId = 'sess_' + Math.random().toString(36).substr(2, 9) + Date.now().toString(36);
            localStorage.setItem("chat_session_id", sessionId);
            chatMessages.innerHTML = `
                <div class="message-bubble message-ai">
                    Hội thoại đã được làm mới. Tôi có thể hỗ trợ gì cho bạn ngay bây giờ?
                </div>
            `;
            isHistoryLoaded = true;
        }
    });

    chatInput.addEventListener("keypress", (e) => {
        if (e.key === "Enter") {
            sendMessage();
        }
    });

    chatSend.addEventListener("click", sendMessage);

    // ─── LOAD HISTORY ────────────────────────────────────────────────────────
    async function loadHistory() {
        try {
            const res = await fetch(`/Chat/History?sessionId=${sessionId}`);
            if (res.ok) {
                const data = await res.json();
                if (data && data.messages && data.messages.length > 0) {
                    chatMessages.innerHTML = "";
                    data.messages.forEach(msg => {
                        // ignore system prompt messages
                        if (msg.role === "user") {
                            appendMessageBubble("user", msg.content);
                        } else if (msg.role === "assistant") {
                            appendMessageBubble("ai", msg.content);
                        }
                    });
                }
            }
            isHistoryLoaded = true;
        } catch (ex) {
            console.error("Lỗi khi tải lịch sử chat:", ex);
        }
    }

    // ─── SEND MESSAGE ────────────────────────────────────────────────────────
    async function sendMessage() {
        const text = chatInput.value.trim();
        if (!text) return;

        chatInput.value = "";
        appendMessageBubble("user", text);

        // Show typing indicator
        const typingId = showTypingIndicator();

        try {
            const res = await fetch("/Chat/Message", {
                method: "POST",
                headers: {
                    "Content-Type": "application/json"
                },
                body: JSON.stringify({
                    sessionId: sessionId,
                    message: text
                })
            });

            removeTypingIndicator(typingId);

            if (res.ok) {
                const apiResult = await res.json();
                if (apiResult && apiResult.success && apiResult.data) {
                    const data = apiResult.data;
                    appendMessageBubble("ai", data.reply, data.suggestedItems, data.orderLink);
                } else {
                    const msg = apiResult?.message || "Đã xảy ra lỗi không mong muốn.";
                    appendMessageBubble("ai", "Lỗi: " + msg);
                }
            } else {
                appendMessageBubble("ai", "Dịch vụ AI đang gặp sự cố. Vui lòng thử lại sau.");
            }
        } catch (ex) {
            removeTypingIndicator(typingId);
            appendMessageBubble("ai", "Lỗi kết nối tới máy chủ.");
        }
    }

    // ─── HELPERS ─────────────────────────────────────────────────────────────
    function appendMessageBubble(role, text, suggestedItems = null, orderLink = null) {
        const bubble = document.createElement("div");
        bubble.className = `message-bubble ${role === "user" ? "message-user" : "message-ai"}`;
        
        // Format simple markdown-like elements
        let formattedText = escapeHtml(text)
            .replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>')
            .replace(/\*(.*?)\*/g, '<em>$1</em>')
            .replace(/\n/g, '<br/>');

        // Parse tables if present in markdown format
        if (formattedText.includes('|')) {
            formattedText = parseMarkdownTable(formattedText);
        }

        bubble.innerHTML = formattedText;

        // If AI reply has suggested products
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

        // If AI suggestions trigger checkout/draft creation link
        if (orderLink) {
            const btn = document.createElement("a");
            btn.className = "ai-draft-order-btn";
            btn.href = orderLink;
            btn.innerHTML = `<i class="bi bi-lightning-charge-fill me-1"></i> Bấm để Đặt Hàng Nháp`;
            bubble.appendChild(btn);
        }

        chatMessages.appendChild(bubble);
        chatMessages.scrollTop = chatMessages.scrollHeight;
    }

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
                if (!inTable) {
                    inTable = true;
                }
                // Skip separator row: | --- | --- |
                if (line.includes('---') || line.includes('-:-')) {
                    return;
                }
                const cells = line.split('|').map(c => c.trim()).filter((c, idx, arr) => idx > 0 && idx < arr.length - 1);
                const tag = htmlTable.includes('<tbody>') ? 'td' : 'th';
                
                let row = '<tr>';
                cells.forEach(cell => {
                    row += `<${tag}>${cell}</${tag}>`;
                });
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
