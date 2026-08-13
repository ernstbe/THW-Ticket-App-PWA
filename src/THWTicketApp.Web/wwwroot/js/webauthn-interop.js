// WebAuthn / Passkey interop for Blazor.
//
// Thin wrappers around navigator.credentials.create()/get() — all
// challenge generation and verification happens server-side now (#205).
// This module only converts between the server's base64url-encoded JSON
// options and the ArrayBuffers the WebAuthn API needs, then converts the
// browser's response back to base64url JSON for the C# side to POST on
// to the server's verify endpoint unchanged.

export function isWebAuthnSupported() {
    return !!(window.PublicKeyCredential);
}

export async function createCredential(optionsJson) {
    const options = typeof optionsJson === 'string' ? JSON.parse(optionsJson) : optionsJson;

    const publicKey = {
        ...options,
        challenge: base64urlToBuffer(options.challenge),
        user: {
            ...options.user,
            id: base64urlToBuffer(options.user.id)
        },
        excludeCredentials: (options.excludeCredentials || []).map(c => ({
            ...c,
            id: base64urlToBuffer(c.id)
        }))
    };

    const credential = await navigator.credentials.create({ publicKey });
    if (!credential) return null;

    return JSON.stringify({
        id: credential.id,
        rawId: bufferToBase64url(credential.rawId),
        type: credential.type,
        response: {
            clientDataJSON: bufferToBase64url(credential.response.clientDataJSON),
            attestationObject: bufferToBase64url(credential.response.attestationObject),
            transports: credential.response.getTransports ? credential.response.getTransports() : []
        },
        clientExtensionResults: credential.getClientExtensionResults ? credential.getClientExtensionResults() : {},
        authenticatorAttachment: credential.authenticatorAttachment || null
    });
}

export async function getAssertion(optionsJson) {
    const options = typeof optionsJson === 'string' ? JSON.parse(optionsJson) : optionsJson;

    const publicKey = {
        ...options,
        challenge: base64urlToBuffer(options.challenge),
        allowCredentials: (options.allowCredentials || []).map(c => ({
            ...c,
            id: base64urlToBuffer(c.id)
        }))
    };

    const assertion = await navigator.credentials.get({ publicKey });
    if (!assertion) return null;

    return JSON.stringify({
        id: assertion.id,
        rawId: bufferToBase64url(assertion.rawId),
        type: assertion.type,
        response: {
            clientDataJSON: bufferToBase64url(assertion.response.clientDataJSON),
            authenticatorData: bufferToBase64url(assertion.response.authenticatorData),
            signature: bufferToBase64url(assertion.response.signature),
            userHandle: assertion.response.userHandle ? bufferToBase64url(assertion.response.userHandle) : null
        },
        clientExtensionResults: assertion.getClientExtensionResults ? assertion.getClientExtensionResults() : {},
        authenticatorAttachment: assertion.authenticatorAttachment || null
    });
}

function base64urlToBuffer(base64url) {
    const base64 = base64url.replace(/-/g, '+').replace(/_/g, '/');
    const padded = base64 + '='.repeat((4 - (base64.length % 4)) % 4);
    const binary = atob(padded);
    const bytes = new Uint8Array(binary.length);
    for (let i = 0; i < binary.length; i++) {
        bytes[i] = binary.charCodeAt(i);
    }
    return bytes.buffer;
}

function bufferToBase64url(buffer) {
    const bytes = new Uint8Array(buffer);
    let binary = '';
    for (let i = 0; i < bytes.byteLength; i++) {
        binary += String.fromCharCode(bytes[i]);
    }
    return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}
