"use strict";

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
    document.querySelectorAll('.user-item').forEach(item => item.classList.remove('active'));
    document.querySelector(`.user-item[data-userid='${partnerId}']`).classList.add('active');

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

        messageInput.disabled = false;
        sendButton.disabled = false;
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
connection.on("ReceiveMessage", (senderId, messageText, imageUrl) => {
    const activePartnerId = receiverIdInput.value;
    if (senderId.toString() === activePartnerId || senderId.toString() === currentUserId) {
        appendMessage(senderId, messageText, imageUrl);
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

connection.onclose(start);
start();