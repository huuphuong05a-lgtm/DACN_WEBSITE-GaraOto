document.addEventListener("DOMContentLoaded", function () {
    // 1. DOM Elements
    const toggleBtn = document.getElementById("chat-toggle-btn");
    const closeBtn = document.getElementById("chat-close-btn");
    const chatWindow = document.getElementById("chat-window");
    const sendBtn = document.getElementById("chat-send-btn");
    const input = document.getElementById("chat-input");
    const msgList = document.getElementById("chat-messages");
    const suggestionsContainer = document.getElementById("chat-suggestions");

    // Nút Upload & Preview
    const imgUploadInput = document.getElementById("chat-image-upload");
    const previewContainer = document.getElementById("image-preview-container");
    const previewImg = document.getElementById("image-preview");

    // Biến lưu ảnh Base64 tạm thời
    let currentBase64 = null;

    // 2. Toggle Chat
    toggleBtn.onclick = () => {
        chatWindow.style.display = "flex";
        msgList.scrollTop = msgList.scrollHeight;
        checkSuggestionsVisibility();
    };
    closeBtn.onclick = () => chatWindow.style.display = "none";

    // 3. Load History
    loadHistory();
    async function loadHistory() {
        try {
            let res = await fetch("/Chat/GetHistory");
            let history = await res.json();
            if (history.length > 0) msgList.innerHTML = "";
            history.forEach(h => {
                if (h.isBot) {
                    addBubble(formatMessageText(h.message), "bot");
                } else {
                    addBubble(escapeHtml(h.message), "user");
                }
            });
            msgList.scrollTop = msgList.scrollHeight;
            checkSuggestionsVisibility();
        } catch { }
    }

    // Toggle static suggestions based on message history
    function checkSuggestionsVisibility() {
        const bubbles = msgList.getElementsByClassName("chat-bubble-row");
        if (bubbles.length <= 2) {
            suggestionsContainer.style.display = "flex";
        } else {
            suggestionsContainer.style.display = "none";
        }
    }

    // Set up click handlers for static suggestions
    document.querySelectorAll(".chat-suggest-btn").forEach(btn => {
        btn.onclick = function() {
            let text = this.getAttribute("data-text");
            if (text) {
                sendDirectMessage(text);
            }
        };
    });

    // 4. Preview Image
    window.previewImage = function () {
        const file = imgUploadInput.files[0];
        if (!file) return;
        if (file.size > 2 * 1024 * 1024) {
            addBubble("Ảnh quá lớn. Vui lòng chọn ảnh dưới 2MB.", "bot error");
            imgUploadInput.value = "";
            return;
        }

        const reader = new FileReader();
        reader.onloadend = function () {
            currentBase64 = reader.result;
            previewImg.src = currentBase64;
            previewContainer.style.display = "block";
        }
        reader.readAsDataURL(file);
    };

    window.clearImage = function () {
        imgUploadInput.value = "";
        currentBase64 = null;
        previewContainer.style.display = "none";
    };

    // 5. Send message direct
    async function sendDirectMessage(text) {
        input.value = text;
        await sendMessage();
    }

    // Bind to window to allow dynamic suggestions to send messages
    window.sendDynamicChoice = function(text, btnElement) {
        const container = btnElement.closest(".chat-dynamic-suggestions");
        if (container) {
            container.remove();
        }
        sendDirectMessage(text);
    };

    // 6. Gửi Tin Nhắn
    async function sendMessage() {
        let msg = input.value.trim();

        if (!msg && !currentBase64) return;

        let userHtml = escapeHtml(msg);
        if (currentBase64) {
            userHtml += `<br><img src="${currentBase64}" style="max-width:150px; border-radius:12px; margin-top:5px; border:1px solid #ddd;">`;
        }
        addBubble(userHtml, "user");

        let imageToSend = currentBase64;
        input.value = "";
        clearImage();
        suggestionsContainer.style.display = "none"; // Ẩn khi bắt đầu trò chuyện

        let loadingId = addTypingIndicator();

        try {
            let res = await fetch("/Chat/SendMessage", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({
                    message: msg,
                    imageBase64: imageToSend
                })
            });

            let data = await res.json();

            document.getElementById(loadingId)?.remove();

            let replyText = data.reply || "Dạ hệ thống chưa có phản hồi, mình thử lại giúp em nhé.";
            let formattedHtml = formatMessageText(replyText);
            
            addBubble(formattedHtml, "bot");

            // Tạo các gợi ý động sau câu trả lời
            handleDynamicSuggestions(replyText);

        } catch (err) {
            document.getElementById(loadingId)?.remove();
            addBubble("Lỗi kết nối server.", "bot error");
        }
    }

    // Show typing loader
    function addTypingIndicator() {
        let rowId = "loading-" + Date.now();
        let row = document.createElement("div");
        row.className = "chat-bubble-row bot";
        row.id = rowId;

        let avatarWrap = document.createElement("div");
        avatarWrap.className = "chat-avatar-container-bubble";
        avatarWrap.innerHTML = `<img src="/img/bot-avatar.png" alt="Bot Avatar" class="chat-bot-avatar-bubble">`;
        row.appendChild(avatarWrap);

        let bubble = document.createElement("div");
        bubble.className = "chat-bubble typing-bubble shadow-sm";
        bubble.innerHTML = `
            <div class="chat-loading-dots">
                <span class="dot-text">🤖 Đang kiểm tra thông tin</span>
                <span class="dots">
                    <span></span>
                    <span></span>
                    <span></span>
                </span>
            </div>
        `;
        row.appendChild(bubble);
        msgList.appendChild(row);
        msgList.scrollTop = msgList.scrollHeight;
        return rowId;
    }

    // 7. Hàm tạo bong bóng chat
    function addBubble(html, type) {
        let row = document.createElement("div");
        row.className = "chat-bubble-row " + (type.includes("user") ? "user" : "bot");

        if (type.includes("bot")) {
            let avatarWrap = document.createElement("div");
            avatarWrap.className = "chat-avatar-container-bubble";
            avatarWrap.innerHTML = `<img src="/img/bot-avatar.png" alt="Bot Avatar" class="chat-bot-avatar-bubble">`;
            row.appendChild(avatarWrap);
        }

        let bubble = document.createElement("div");
        bubble.className = "chat-bubble " + (type.includes("error") ? "error" : "");
        bubble.innerHTML = html;

        row.appendChild(bubble);
        msgList.appendChild(row);
        msgList.scrollTop = msgList.scrollHeight;
        return row;
    }

    // 8. Dynamic Suggestions logic
    function handleDynamicSuggestions(replyText) {
        let suggestions = [];

        // Check for times in format: 08:00, 09:00, 14:00, etc.
        let timeRegex = /(\b\d{2}:\d{2}\b)/g;
        let matches = replyText.match(timeRegex);
        if (matches) {
            let allowedHours = ["08:00", "09:00", "10:00", "11:00", "13:00", "14:00", "15:00", "16:00", "17:00"];
            let uniqueMatches = [...new Set(matches)];
            
            uniqueMatches.forEach(h => {
                if (allowedHours.includes(h)) {
                    suggestions.push(h);
                }
            });
        }

        // Check if reply text relates to services
        let serviceKeywords = ["bảo dưỡng", "dịch vụ", "thay dầu", "phanh", "khắc phục", "sửa chữa", "điều hòa", "giá", "chi phí"];
        let hasServiceKeywords = serviceKeywords.some(kw => replyText.toLowerCase().includes(kw));

        if (hasServiceKeywords && suggestions.length === 0) {
            suggestions = ["Thay dầu", "Kiểm tra phanh", "Vệ sinh điều hòa", "Bảo dưỡng định kỳ"];
        }

        // If we have suggestions, render them
        if (suggestions.length > 0) {
            let suggestionRow = document.createElement("div");
            suggestionRow.className = "chat-dynamic-suggestions";
            
            suggestions.forEach(item => {
                let btn = document.createElement("button");
                btn.className = "chat-dynamic-pill";
                btn.innerHTML = item;
                btn.onclick = function() {
                    window.sendDynamicChoice(item, this);
                };
                suggestionRow.appendChild(btn);
            });
            
            msgList.appendChild(suggestionRow);
            msgList.scrollTop = msgList.scrollHeight;
        }
    }

    // Helper formatting function for bullet points and emoji headers
    function formatMessageText(text) {
        if (!text) return "";

        let escaped = escapeHtml(text);
        
        // Convert bold markdown **text** to <strong>text</strong>
        escaped = escaped.replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>');
        
        let lines = escaped.split("\n");
        let htmlLines = [];

        for (let i = 0; i < lines.length; i++) {
            let line = lines[i].trim();
            if (line.length === 0) {
                htmlLines.push('<div class="chat-line-spacer"></div>');
                continue;
            }

            // Detect time slots: • 08:00 - 09:00 or - 08:00 - 09:00
            let timeSlotMatch = line.match(/^([•\-\*]\s*)?(\d{2}:\d{2}\s*-\s*\d{2}:\d{2})$/);
            let bulletMatch = line.match(/^([•\-\*])\s*(.+)$/);

            if (timeSlotMatch) {
                let slot = timeSlotMatch[2];
                htmlLines.push(`<div class="chat-time-badge" onclick="window.sendDynamicChoice('${slot}', this)"><i class="far fa-clock me-1"></i> ${slot}</div>`);
            } else if (bulletMatch) {
                let content = bulletMatch[2];
                htmlLines.push(`<div class="chat-list-bullet"><span class="bullet-dot">•</span> ${content}</div>`);
            } else {
                if (line.startsWith("📅") || line.startsWith("🟢") || line.startsWith("👉")) {
                    htmlLines.push(`<div class="chat-content-header">${line}</div>`);
                } else {
                    htmlLines.push(`<div class="chat-content-text">${line}</div>`);
                }
            }
        }

        return htmlLines.join("");
    }

    function escapeHtml(value) {
        return value
            .replaceAll("&", "&amp;")
            .replaceAll("<", "&lt;")
            .replaceAll(">", "&gt;")
            .replaceAll('"', "&quot;")
            .replaceAll("'", "&#039;");
    }

    // 9. Send Events
    sendBtn.onclick = sendMessage;
    input.addEventListener("keypress", (e) => {
        if (e.key === "Enter") sendMessage();
    });
});
