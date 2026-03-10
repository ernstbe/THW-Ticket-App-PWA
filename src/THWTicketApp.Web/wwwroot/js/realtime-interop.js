// Socket.IO real-time interop for Blazor
// Uses CDN-loaded socket.io-client

let socket = null;
let dotNetRef = null;

export function connect(serverUrl, token, objRef) {
    dotNetRef = objRef;

    if (socket && socket.connected) {
        socket.disconnect();
    }

    if (!window.io) {
        console.warn('Socket.IO client not loaded');
        return false;
    }

    try {
        socket = window.io(serverUrl, {
            query: { token: token },
            reconnection: true,
            reconnectionAttempts: 5,
            reconnectionDelay: 2000,
            timeout: 10000,
            transports: ['websocket', 'polling']
        });

        socket.on('connect', () => {
            if (dotNetRef) dotNetRef.invokeMethodAsync('OnConnected');
        });

        socket.on('disconnect', () => {
            if (dotNetRef) dotNetRef.invokeMethodAsync('OnDisconnected');
        });

        // Trudesk real-time events - must match socketEventConsts.js exactly
        const eventMap = {
            '$trudesk:tickets:created':            'ticketCreated',
            '$trudesk:tickets:update':             'ticketUpdated',
            '$trudesk:tickets:ui:status:update':   'statusUpdated',
            '$trudesk:tickets:assignee: update':   'assigneeUpdated',
            '$trudesk:tickets:ui:priority:update': 'priorityUpdated',
            '$trudesk:tickets:ui:type:update':     'typeUpdated',
            '$trudesk:tickets:ui:group:update':    'groupUpdated',
            '$trudesk:tickets:ui:tags:update':     'tagsUpdated',
            '$trudesk:tickets:comment_note:set':   'commentNoteAdded',
            '$trudesk:tickets:comment_note:remove':'commentNoteRemoved',
            '$trudesk:tickets:ui:attachments:update': 'attachmentsUpdated',
            '$trudesk:notifications:update':       'notificationUpdate'
        };

        Object.entries(eventMap).forEach(([socketEvent, friendlyName]) => {
            socket.on(socketEvent, (data) => {
                const ticketId = extractTicketId(data);
                if (dotNetRef) dotNetRef.invokeMethodAsync('OnTicketEvent', friendlyName, ticketId);
            });
        });

        return true;
    } catch (e) {
        console.error('Socket.IO connection error:', e);
        return false;
    }
}

export function disconnect() {
    if (socket) {
        socket.disconnect();
        socket = null;
    }
    dotNetRef = null;
}

export function isConnected() {
    return socket != null && socket.connected;
}

function extractTicketId(data) {
    try {
        if (!data) return '';
        if (typeof data === 'string') {
            const parsed = JSON.parse(data);
            data = parsed;
        }
        if (Array.isArray(data) && data.length > 0) data = data[0];
        if (data.ticket) {
            if (typeof data.ticket === 'string') return data.ticket;
            if (data.ticket._id) return data.ticket._id;
        }
        if (data._id) return data._id;
    } catch { }
    return '';
}
