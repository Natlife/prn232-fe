(function () {
    const STORAGE_KEY_SESSION = "chat_session_id";
    const STORAGE_KEY_OPEN = "chat_panel_open";
    const STORAGE_KEY_MESSAGES = "chat_messages_cache";

    let sessionId = localStorage.getItem(STORAGE_KEY_SESSION);
    if (!sessionId) {
        sessionId = "sess_" + Math.random().toString(36).slice(2, 11) + Date.now().toString(36);
        localStorage.setItem(STORAGE_KEY_SESSION, sessionId);
    }

    function loadCachedMessages() {
        try {
            const raw = sessionStorage.getItem(STORAGE_KEY_MESSAGES);
            return raw ? JSON.parse(raw) : [];
        } catch {
            return [];
        }
    }

    function saveCachedMessages(messages) {
        try {
            sessionStorage.setItem(
                STORAGE_KEY_MESSAGES,
                JSON.stringify(messages.slice(-60))
            );
        } catch {
            // Ignore storage quota issues.
        }
    }

    function clearCachedMessages() {
        sessionStorage.removeItem(STORAGE_KEY_MESSAGES);
    }

    let messages = loadCachedMessages();

    const widgetHtml = `
    <div id="ai-chat-widget">
        <button id="ai-chat-btn" class="ai-chat-btn" title="Trò chuyện với trợ lý AI">
            <i class="bi bi-robot"></i>
        </button>

        <div id="ai-chat-panel" class="ai-chat-panel">
            <div class="ai-chat-header">
                <div class="ai-chat-title">
                    <i class="bi bi-cpu fs-5"></i>
                    <div>
                        <h6>Trợ lý AI</h6>
                        <span>Car Sales Management</span>
                    </div>
                </div>
                <div class="ai-chat-actions">
                    <button id="ai-chat-clear" title="Làm mới hội thoại" class="me-2">
                        <i class="bi bi-arrow-clockwise"></i>
                    </button>
                    <button id="ai-chat-close" title="Đóng">
                        <i class="bi bi-x-lg"></i>
                    </button>
                </div>
            </div>

            <div id="ai-chat-messages" class="ai-chat-messages">
                <div class="message-bubble message-ai">
                    Xin chào! Tôi có thể hỗ trợ bạn tìm xe, phụ tùng, dịch vụ và hướng dẫn đặt cọc hoặc mua đứt ngay trong hệ thống.
                </div>
            </div>

            <div class="ai-chat-input-area">
                <input type="text" id="ai-chat-input" placeholder="Nhập tin nhắn..." autocomplete="off" />
                <button id="ai-chat-send" class="ai-chat-send-btn" aria-label="Gửi tin nhắn">
                    <i class="bi bi-send-fill"></i>
                </button>
            </div>
        </div>
    </div>`;

    document.body.insertAdjacentHTML("beforeend", widgetHtml);

    const chatBtn = document.getElementById("ai-chat-btn");
    const chatPanel = document.getElementById("ai-chat-panel");
    const chatClose = document.getElementById("ai-chat-close");
    const chatClear = document.getElementById("ai-chat-clear");
    const chatInput = document.getElementById("ai-chat-input");
    const chatSend = document.getElementById("ai-chat-send");
    const chatMessages = document.getElementById("ai-chat-messages");

    function restoreMessagesFromCache() {
        if (!messages.length) {
            return;
        }

        chatMessages.innerHTML = "";
        messages.forEach((message) => {
            renderBubble(
                message.role,
                message.text,
                message.orderLink || null,
                message.action || null
            );
        });
        chatMessages.scrollTop = chatMessages.scrollHeight;
    }

    const chatWasOpen = localStorage.getItem(STORAGE_KEY_OPEN) === "true";
    if (chatWasOpen) {
        chatPanel.style.transition = "none";
        chatPanel.classList.add("active");
        requestAnimationFrame(() => requestAnimationFrame(() => {
            chatPanel.style.transition = "";
        }));

        if (messages.length > 0) {
            restoreMessagesFromCache();
        }
    }

    chatBtn.addEventListener("click", () => {
        const isNowOpen = chatPanel.classList.toggle("active");
        localStorage.setItem(STORAGE_KEY_OPEN, isNowOpen ? "true" : "false");

        if (!isNowOpen) {
            return;
        }

        chatInput.focus();
        if (messages.length > 0) {
            restoreMessagesFromCache();
            return;
        }

        fetchHistoryFromServer();
    });

    chatClose.addEventListener("click", () => {
        chatPanel.classList.remove("active");
        localStorage.setItem(STORAGE_KEY_OPEN, "false");
    });

    chatClear.addEventListener("click", () => {
        if (!confirm("Bạn có chắc muốn xóa hội thoại hiện tại và bắt đầu phiên mới?")) {
            return;
        }

        sessionId = "sess_" + Math.random().toString(36).slice(2, 11) + Date.now().toString(36);
        localStorage.setItem(STORAGE_KEY_SESSION, sessionId);

        messages = [];
        clearCachedMessages();
        chatMessages.innerHTML = `
            <div class="message-bubble message-ai">
                Hội thoại đã được làm mới. Bạn cần mình tư vấn xe, combo, đặt cọc hay mua đứt sản phẩm nào?
            </div>
        `;
    });

    chatInput.addEventListener("keypress", (event) => {
        if (event.key === "Enter") {
            sendMessage();
        }
    });

    chatSend.addEventListener("click", sendMessage);

    document.addEventListener("click", (event) => {
        const orderButton = event.target.closest("a.ai-draft-order-btn");
        if (!orderButton) {
            return;
        }

        const href = orderButton.getAttribute("href");
        if (!href) {
            return;
        }

        if (href.includes("/ComboOrder/Confirm")) {
            event.preventDefault();
            const actionType = orderButton.dataset.actionType || "";
            const resolvedHref = normalizeComboConfirmLink(href, actionType);
            if (resolvedHref) {
                orderButton.setAttribute("href", resolvedHref);
                window.location.href = resolvedHref;
            }
            return;
        }

        if (!href.includes("/Cars/Details/")) {
            return;
        }

        const actionType = orderButton.dataset.actionType || "";
        const bubbleText = orderButton.closest(".message-bubble")?.innerText || "";
        const resolvedHref = appendChatActionToCarLink(href, bubbleText, actionType);
        if (resolvedHref !== href) {
            orderButton.setAttribute("href", resolvedHref);
        }
    });

    async function fetchHistoryFromServer() {
        try {
            const response = await fetch(`/Chat/History?sessionId=${sessionId}`);
            if (!response.ok) {
                return;
            }

            const data = await response.json();
            if (!data?.messages?.length) {
                return;
            }

            messages = [];
            chatMessages.innerHTML = "";

            data.messages.forEach((message) => {
                if (message.role === "user") {
                    pushAndRender("user", message.content);
                    return;
                }

                if (message.role === "assistant") {
                    pushAndRender("ai", message.content);
                }
            });
        } catch (error) {
            console.error("Lỗi khi tải lịch sử chat:", error);
        }
    }

    async function sendMessage() {
        const text = chatInput.value.trim();
        if (!text) {
            return;
        }

        chatInput.value = "";
        pushAndRender("user", text);

        const typingId = showTypingIndicator();

        try {
            const response = await fetch("/Chat/Message", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ sessionId, message: text })
            });

            removeTypingIndicator(typingId);

            if (!response.ok) {
                pushAndRender("ai", "Dịch vụ AI đang gặp sự cố. Vui lòng thử lại sau.");
                return;
            }

            const apiResult = await response.json();
            if (apiResult?.success && apiResult.data) {
                const data = apiResult.data;
                pushAndRender("ai", data.reply, data.orderLink, data.action);
                return;
            }

            pushAndRender("ai", `Lỗi: ${apiResult?.message || "Đã xảy ra lỗi không mong muốn."}`);
        } catch (error) {
            removeTypingIndicator(typingId);
            pushAndRender("ai", "Không thể kết nối tới máy chủ.");
        }
    }

    function pushAndRender(role, text, orderLink = null, action = null) {
        messages.push({
            role,
            text,
            orderLink: orderLink || null,
            action: action || null
        });
        saveCachedMessages(messages);
        renderBubble(role, text, orderLink, action);
    }

    function renderBubble(role, text, orderLink = null, action = null) {
        const bubble = document.createElement("div");
        bubble.className = `message-bubble ${role === "user" ? "message-user" : "message-ai"}`;

        let formattedText = escapeHtml(text)
            .replace(/\*\*(.*?)\*\*/g, "<strong>$1</strong>")
            .replace(/\*(.*?)\*/g, "<em>$1</em>")
            .replace(/!\[(.*?)\]\((.*?)\)/g, '<img src="$2" alt="$1" class="img-fluid rounded my-2 d-block chat-inline-image" />')
            .replace(/\n/g, "<br/>");

        if (formattedText.includes("|")) {
            formattedText = parseMarkdownTable(formattedText);
        }

        bubble.innerHTML = formattedText;

        const actionUrl = action?.url || orderLink;
        if (actionUrl) {
            const button = document.createElement("a");
            button.className = "ai-draft-order-btn";
            button.href = actionUrl;

            if (action) {
                button.dataset.actionType = action.type || "";
                button.dataset.targetType = action.targetType || "";
                button.dataset.actionLabel = action.label || "";
            }

            button.innerHTML = buildActionButtonLabel(actionUrl, action);
            bubble.appendChild(button);
        }

        chatMessages.appendChild(bubble);
        chatMessages.scrollTop = chatMessages.scrollHeight;
    }

    function buildActionButtonLabel(actionUrl, action) {
        if (action?.targetType === "combo") {
            if (action.type === "deposit") {
                return '<i class="bi bi-cash-coin me-1"></i> Đặt cọc combo';
            }
            if (action.type === "buyout") {
                return '<i class="bi bi-cart-check me-1"></i> Mua đứt combo';
            }
            return '<i class="bi bi-bag-check me-1"></i> Xem combo và xác nhận';
        }

        if (action?.targetType === "car") {
            if (action.type === "deposit") {
                return `<i class="bi bi-cash-stack me-1"></i> ${escapeHtml(action.label || "Đặt cọc xe")}`;
            }
            if (action.type === "buyout") {
                return `<i class="bi bi-cart-check me-1"></i> ${escapeHtml(action.label || "Mua đứt xe")}`;
            }
            return `<i class="bi bi-car-front-fill me-1"></i> ${escapeHtml(action.label || "Xem xe và tiếp tục")}`;
        }

        if (actionUrl.includes("/Cars/Details/")) {
            return '<i class="bi bi-car-front-fill me-1"></i> Xem xe và tiếp tục';
        }

        return '<i class="bi bi-bag-check me-1"></i> Xem đơn và tiếp tục';
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
            </div>`;
        chatMessages.appendChild(bubble);
        chatMessages.scrollTop = chatMessages.scrollHeight;
        return typingId;
    }

    function removeTypingIndicator(id) {
        document.getElementById(id)?.remove();
    }

    function escapeHtml(value) {
        return String(value || "")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#039;");
    }

    function parseMarkdownTable(text) {
        const lines = text.split("<br/>");
        let inTable = false;
        let htmlTable = '<div class="chat-table-wrap"><table class="table table-sm table-bordered mt-2 chat-table">';
        const outputLines = [];

        lines.forEach((line) => {
            if (line.trim().startsWith("|")) {
                if (!inTable) {
                    inTable = true;
                }

                if (line.includes("---") || line.includes("-:-")) {
                    return;
                }

                const cells = line
                    .split("|")
                    .map((cell) => cell.trim())
                    .filter((cell, index, array) => index > 0 && index < array.length - 1);

                const tag = htmlTable.includes("<tbody>") ? "td" : "th";
                let row = "<tr>";
                cells.forEach((cell) => {
                    row += `<${tag}>${cell}</${tag}>`;
                });
                row += "</tr>";

                if (tag === "th") {
                    htmlTable += `<thead>${row}</thead><tbody>`;
                } else {
                    htmlTable += row;
                }

                return;
            }

            if (inTable) {
                htmlTable += "</tbody></table></div>";
                outputLines.push(htmlTable);
                htmlTable = '<div class="chat-table-wrap"><table class="table table-sm table-bordered mt-2 chat-table">';
                inTable = false;
            }

            outputLines.push(line);
        });

        if (inTable) {
            htmlTable += "</tbody></table></div>";
            outputLines.push(htmlTable);
        }

        return outputLines.join("<br/>");
    }

    function appendChatActionToCarLink(href, bubbleText, actionType = "") {
        const url = new URL(href, window.location.origin);
        if (!url.pathname.includes("/Cars/Details/") || url.searchParams.has("chatAction")) {
            return url.pathname + url.search + url.hash;
        }

        if (actionType === "deposit" || actionType === "buyout") {
            url.searchParams.set("chatAction", actionType);
            return url.pathname + url.search + url.hash;
        }

        const normalized = (bubbleText || "").toLowerCase();
        if (
            normalized.includes("đặt cọc") ||
            normalized.includes("dat coc") ||
            normalized.includes("cọc xe") ||
            normalized.includes("coc xe")
        ) {
            url.searchParams.set("chatAction", "deposit");
        } else if (
            normalized.includes("mua đứt") ||
            normalized.includes("mua dut") ||
            normalized.includes("thanh toán đứt") ||
            normalized.includes("thanh toan dut")
        ) {
            url.searchParams.set("chatAction", "buyout");
        }

        return url.pathname + url.search + url.hash;
    }

    function normalizeComboConfirmLink(href, actionType = "") {
        const url = new URL(href, window.location.origin);
        if (!url.pathname.includes("/ComboOrder/Confirm")) {
            return url.pathname + url.search + url.hash;
        }

        const rawDraftMatch = href.match(/[?&]draft=([^&#]*)/i);
        if (!rawDraftMatch || !rawDraftMatch[1]) {
            return url.pathname + url.search + url.hash;
        }

        const rawDraft = rawDraftMatch[1].replace(/ /g, "+");
        let decodedDraft = rawDraft;

        try {
            decodedDraft = decodeURIComponent(rawDraft.replace(/\+/g, "%2B"));
        } catch {
            decodedDraft = rawDraft;
        }

        url.searchParams.set("draft", decodedDraft);

        if ((actionType === "deposit" || actionType === "buyout") && !url.searchParams.get("type")) {
            url.searchParams.set("type", actionType);
        }

        return url.pathname + url.search + url.hash;
    }
})();
