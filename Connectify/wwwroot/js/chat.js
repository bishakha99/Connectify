"use strict";
let unreadCounts = {}; // To store { userId: count }

// --- SIGNALR CONNECTION ---
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/chatHub")
    .configureLogging(signalR.LogLevel.Information)
    .build();

// --- DOM ELEMENTS ---
const userList = document.getElementById("user-list");
const messageInput = document.getElementById("message-input");
const sendButton = document.getElementById("send-button");
const chatMessagesContainer = document.getElementById("chat-messages-container");
const mainChatArea = document.getElementById("main-chat-area");
const receiverIdInput = document.getElementById("receiver-id");
const currentUserId = document.getElementById("current-userid").value;
const welcomeMessage = document.getElementById("welcome-message");
const imageUploadInput = document.getElementById("image-upload");

// --- FUNCTIONS ---
function scrollToBottom() {
    chatMessagesContainer.scrollTop = chatMessagesContainer.scrollHeight;
}

function appendMessage(senderId, messageText, imageUrl) {
    const isSentByMe = senderId.toString() === currentUserId;
    const messageClass = isSentByMe ? 'sent' : 'received';
    const messageAlignment = isSentByMe ? 'd-flex justify-content-end' : 'd-flex justify-content-start';

    const messageDiv = document.createElement('div');
    messageDiv.className = `${messageAlignment} mb-2`;

    let messageContent = '';
    if (messageText) {
        messageContent = document.createTextNode(messageText).textContent;
    } else if (imageUrl) {
        messageContent = `<img src="${imageUrl}" class="chat-image" alt="User image" />`;
    }

    messageDiv.innerHTML = `
        <div class="chat-bubble ${messageClass}">
            ${messageContent}
            <div class="message-time">
                ${new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
            </div>
        </div>`;

    chatMessagesContainer.appendChild(messageDiv);
    scrollToBottom();
}

async function startChat(partnerId, partnerUsername) {
    // --- ADD THESE LINES ---
    unreadCounts[partnerId] = 0;
    updateUnreadBadge(partnerId, 0);
    // -----------------------

    // Visually mark the active user...
    document.querySelectorAll('.user-item').forEach(item => item.classList.remove('active'));
    document.querySelector(`.user-item[data-userid='${partnerId}']`).classList.add('active');
    // ...existing code...

    receiverIdInput.value = partnerId;

    welcomeMessage.classList.add('d-none');
    mainChatArea.classList.remove('d-none');
    mainChatArea.classList.add('d-flex');

    chatMessagesContainer.innerHTML = `
        <div class="spinner-container">
            <div class="spinner-border text-primary" role="status">
                <span class="visually-hidden">Loading...</span>
            </div>
        </div>`;

    messageInput.disabled = true;
    sendButton.disabled = true;

    const partnerStatus = document.getElementById(`status-${partnerId}`).classList.contains('status-online');
    document.getElementById('chat-with-header').innerHTML = `
        <div class="avatar">${partnerUsername.toUpperCase()[0]}</div>
        <div class="ms-3">
            <div>${partnerUsername}</div>
            <small class="text-muted">${partnerStatus ? 'Online' : 'Offline'}</small>
        </div>
    `;

    try {
        const response = await fetch(`/Chat/GetChatHistory?partnerId=${partnerId}`);
        if (!response.ok) throw new Error(`HTTP error! status: ${response.status}`);

        const historyHtml = await response.text();
        chatMessagesContainer.innerHTML = historyHtml;
        scrollToBottom();

        // --- ADD THIS LINE ---
        await connection.invoke("MarkMessagesAsRead", parseInt(partnerId));
        // ---------------------

        messageInput.disabled = false;
        sendButton.disabled = false;
        messageInput.disabled = false;
        messageInput.focus();
    } catch (error) {
        console.error('Error fetching chat history:', error);
        chatMessagesContainer.innerHTML = `<div class="text-center text-danger p-3">Could not load chat history.</div>`;
    }
}

async function sendMessage() {
    const receiverId = receiverIdInput.value;
    const messageText = messageInput.value.trim();

    if (receiverId && messageText) {
        try {
            await connection.invoke("SendMessage", parseInt(receiverId), messageText);
            messageInput.value = "";
        } catch (err) {
            console.error(err.toString());
        }
    }
}

// --- SIGNALR EVENT HANDLERS ---
connection.on("ReceiveMessage", (senderId, senderUsername, messageText, imageUrl) => {
    const activePartnerId = receiverIdInput.value;
    const isMyOwnMessage = senderId.toString() === currentUserId;

    // If the message is from the active chat OR it's my own message just sent
    if (senderId.toString() === activePartnerId || isMyOwnMessage) {
        appendMessage(senderId, messageText, imageUrl);
        if (!isMyOwnMessage) {
            // If it's a message from my active partner, mark it as read immediately
            connection.invoke("MarkMessagesAsRead", parseInt(senderId));
        }
    } else {
        // Message from an inactive chat
        // 1. Increment unread count
        unreadCounts[senderId] = (unreadCounts[senderId] || 0) + 1;

        // 2. Update the user list UI
        updateUnreadBadge(senderId, unreadCounts[senderId]);

        // 3. Show a "toast" notification
        showToastNotification(senderUsername, messageText || "Sent you an image");
    }
});

connection.on("UserStatusChanged", (userId, isOnline) => {
    const statusBadge = document.getElementById(`status-${userId}`);
    if (statusBadge) {
        statusBadge.className = `status-indicator ${isOnline ? "status-online" : "status-offline"}`;
    }

    const activePartnerId = receiverIdInput.value;
    if (userId.toString() === activePartnerId) {
        const headerStatus = document.querySelector('#chat-with-header small');
        if (headerStatus) {
            headerStatus.textContent = isOnline ? 'Online' : 'Offline';
        }
    }
});

// --- EVENT LISTENERS ---
userList.addEventListener('click', (event) => {
    const userItem = event.target.closest('.user-item');
    if (userItem) {
        const userId = userItem.dataset.userid;
        const username = userItem.dataset.username;
        startChat(userId, username);
    }
});

sendButton.addEventListener('click', sendMessage);
messageInput.addEventListener('keypress', (event) => {
    if (event.key === "Enter") {
        event.preventDefault();
        sendMessage();
    }
});

imageUploadInput.addEventListener('change', async (event) => {
    const file = event.target.files[0];
    const receiverId = receiverIdInput.value;
    if (!file || !receiverId) return;

    const token = document.querySelector('input[name="__RequestVerificationToken"]').value;
    const formData = new FormData();
    formData.append('file', file);

    try {
        const response = await fetch('/Chat/UploadImage', {
            method: 'POST',
            body: formData,
            headers: { 'RequestVerificationToken': token }
        });
        if (!response.ok) {
            const error = await response.json();
            throw new Error(error.message || 'Image upload failed.');
        }
        const result = await response.json();
        await connection.invoke("SendImageMessage", parseInt(receiverId), result.imageUrl);
    } catch (err) {
        console.error(err);
        alert(err.message);
    }
    event.target.value = '';
});

// --- START CONNECTION ---
async function start() {
    try {
        await connection.start();
        console.log("SignalR Connected.");
    } catch (err) {
        console.log(err);
        setTimeout(start, 5000);
    }
}

connection.on("MessagesHaveBeenRead", (readerId) => {
    const activePartnerId = receiverIdInput.value;

    // Check if the user who read the messages is the one we are currently chatting with
    if (readerId.toString() === activePartnerId) {
        // Find all 'sent' messages in the current chat that don't have a seen icon yet
        const sentMessages = document.querySelectorAll('.chat-bubble.sent:not(.seen)');
        sentMessages.forEach(msg => {
            // Add the 'seen' class to mark it as processed
            msg.classList.add('seen');
            // Append the seen icon
            const seenIcon = document.createElement('i');
            seenIcon.className = 'bi bi-check2-all seen-icon';
            msg.querySelector('.message-time').appendChild(seenIcon);
        });
    }
});
function updateUnreadBadge(userId, count) {
    const userItem = document.querySelector(`.user-item[data-userid='${userId}']`);
    if (!userItem) return;

    let badge = userItem.querySelector('.unread-badge');
    if (!badge) {
        badge = document.createElement('span');
        badge.className = 'badge bg-danger rounded-pill ms-auto unread-badge';
        // Find where to insert it. E.g., before the status indicator
        const statusIndicator = userItem.querySelector('.status-indicator');
        userItem.insertBefore(badge, statusIndicator);
    }

    if (count > 0) {
        badge.textContent = count;
        badge.style.display = 'block';
    } else {
        badge.style.display = 'none';
    }
}

function showToastNotification(title, body) {
    // A simple, non-library toast implementation
    const toastContainer = document.createElement('div');
    toastContainer.className = 'toast-notification';
    toastContainer.innerHTML = `
        <div class="toast-title">${title}</div>
        <div class="toast-body">${body}</div>
    `;
    document.body.appendChild(toastContainer);

    setTimeout(() => {
        toastContainer.classList.add('show');
    }, 100);

    setTimeout(() => {
        toastContainer.classList.remove('show');
        setTimeout(() => {
            document.body.removeChild(toastContainer);
        }, 500);
    }, 4000);
}
connection.onclose(start);
start();