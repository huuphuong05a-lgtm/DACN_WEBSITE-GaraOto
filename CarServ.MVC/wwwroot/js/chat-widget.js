document.addEventListener("DOMContentLoaded", function () {
    // 1. DOM Elements
    const toggleBtn = document.getElementById("chat-toggle-btn");
    const closeBtn = document.getElementById("chat-close-btn");
    const chatWindow = document.getElementById("chat-window");
    const sendBtn = document.getElementById("chat-send-btn");
    const input = document.getElementById("chat-input");
    const msgList = document.getElementById("chat-messages");

    // Nút Upload & Preview
    const imgUploadInput = document.getElementById("chat-image-upload");
    const previewContainer = document.getElementById("image-preview-container");
    const previewImg = document.getElementById("image-preview");

    // Biến lưu ảnh Base64 tạm thời
    let currentBase64 = null;

    // 2. Toggle Chat
    toggleBtn.onclick = () => chatWindow.style.display = "flex";
    closeBtn.onclick = () => chatWindow.style.display = "none";

    // 3. Load History
    loadHistory();
    async function loadHistory() {
        try {
            let res = await fetch("/Chat/GetHistory");
            let history = await res.json();
            if (history.length > 0) msgList.innerHTML = "";
            history.forEach(h => addBubble(h.message, h.isBot ? "bot" : "user"));
            msgList.scrollTop = msgList.scrollHeight;
        } catch { }
    }

    // 4. Xử lý khi chọn ảnh (Chỉ hiện Preview, CHƯA GỬI)
    // Lưu ý: Bạn cần thêm onclick="clearImage()" vào nút X đỏ trong HTML (như hướng dẫn trước)
    window.previewImage = function () {
        const file = imgUploadInput.files[0];
        if (!file) return;

        const reader = new FileReader();
        reader.onloadend = function () {
            currentBase64 = reader.result;
            previewImg.src = currentBase64;
            previewContainer.style.display = "block"; // Hiện khung preview lên
        }
        reader.readAsDataURL(file);
    };

    window.clearImage = function () {
        imgUploadInput.value = "";
        currentBase64 = null;
        previewContainer.style.display = "none"; // Ẩn khung preview
    };

    // 5. Gửi Tin Nhắn (Xử lý cả Text và Ảnh cùng lúc)
    async function sendMessage() {
        let msg = input.value.trim();

        // Nếu không có chữ và không có ảnh thì chặn
        if (!msg && !currentBase64) return;

        // --- HIỂN THỊ PHÍA USER ---
        let userHtml = msg;
        if (currentBase64) {
            userHtml += `<br><img src="${currentBase64}" style="max-width:150px; border-radius:8px; margin-top:5px; border:1px solid #ddd;">`;
        }
        addBubble(userHtml, "user");

        // --- RESET INPUT NGAY LẬP TỨC ---
        let imageToSend = currentBase64; // Lưu lại để gửi
        input.value = "";
        clearImage(); // Xóa preview và biến tạm

        // --- HIỂN THỊ LOADING ---
        let loadingId = addBubble('<i class="fas fa-ellipsis-h fa-fade"></i>', "bot", true);

        try {
            // --- GỌI API ---
            let res = await fetch("/Chat/SendMessage", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({
                    message: msg,
                    imageBase64: imageToSend // Gửi kèm ảnh nếu có
                })
            });

            let data = await res.json();

            // Xóa loading
            document.getElementById(loadingId)?.remove();

            // Hiển thị câu trả lời từ Bot
            let modelName = data.model || "System";
            let badgeHtml = `<div class="ai-source-badge">${modelName}</div>`;
            addBubble(data.reply + badgeHtml, "bot");

        } catch (err) {
            document.getElementById(loadingId)?.remove();
            addBubble("Lỗi kết nối server.", "bot error");
        }
    }

    // 6. Hàm tạo bong bóng chat
    function addBubble(html, type, isLoading = false) {
        let row = document.createElement("div");
        row.className = "chat-bubble-row " + (type.includes("user") ? "user" : "bot");
        if (isLoading) row.id = "loading-" + Date.now();

        let bubble = document.createElement("div");
        bubble.className = "chat-bubble " + (type.includes("error") ? "error" : "");
        bubble.innerHTML = html;

        row.appendChild(bubble);
        msgList.appendChild(row);
        msgList.scrollTop = msgList.scrollHeight;
        return row.id;
    }

    // 7. Sự kiện Gửi
    sendBtn.onclick = sendMessage;
    input.addEventListener("keypress", (e) => {
        if (e.key === "Enter") sendMessage();
    });
});