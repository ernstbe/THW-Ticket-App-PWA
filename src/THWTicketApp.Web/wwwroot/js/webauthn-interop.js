// WebAuthn / Passkey interop for Blazor
// Used as local biometric gate to unlock stored credentials

export function isWebAuthnSupported() {
    return !!(window.PublicKeyCredential);
}

export async function isPasskeyRegistered() {
    const credId = localStorage.getItem('passkey_credential_id');
    return !!credId;
}

export async function registerPasskey(userId, userName) {
    try {
        const challenge = new Uint8Array(32);
        crypto.getRandomValues(challenge);

        const userIdBytes = new TextEncoder().encode(userId);

        const createOptions = {
            publicKey: {
                rp: {
                    name: "THW Ticket App",
                    id: window.location.hostname
                },
                user: {
                    id: userIdBytes,
                    name: userName,
                    displayName: userName
                },
                challenge: challenge,
                pubKeyCredParams: [
                    { alg: -7, type: "public-key" },   // ES256
                    { alg: -257, type: "public-key" }   // RS256
                ],
                authenticatorSelection: {
                    authenticatorAttachment: "platform",
                    userVerification: "required",
                    residentKey: "preferred"
                },
                timeout: 60000,
                attestation: "none"
            }
        };

        const credential = await navigator.credentials.create(createOptions);
        const credentialId = bufferToBase64(credential.rawId);

        localStorage.setItem('passkey_credential_id', credentialId);
        localStorage.setItem('passkey_user_id', userId);
        localStorage.setItem('passkey_user_name', userName);

        return credentialId;
    } catch (e) {
        console.error('Passkey registration failed:', e);
        return null;
    }
}

export async function authenticatePasskey() {
    try {
        const credentialId = localStorage.getItem('passkey_credential_id');
        if (!credentialId) return null;

        const challenge = new Uint8Array(32);
        crypto.getRandomValues(challenge);

        const getOptions = {
            publicKey: {
                challenge: challenge,
                rpId: window.location.hostname,
                allowCredentials: [{
                    id: base64ToBuffer(credentialId),
                    type: "public-key",
                    transports: ["internal"]
                }],
                userVerification: "required",
                timeout: 60000
            }
        };

        const assertion = await navigator.credentials.get(getOptions);
        // Authentication succeeded - return stored user info
        return {
            userId: localStorage.getItem('passkey_user_id'),
            userName: localStorage.getItem('passkey_user_name'),
            credentialId: credentialId
        };
    } catch (e) {
        console.error('Passkey authentication failed:', e);
        return null;
    }
}

export function removePasskey() {
    localStorage.removeItem('passkey_credential_id');
    localStorage.removeItem('passkey_user_id');
    localStorage.removeItem('passkey_user_name');
    return true;
}

function bufferToBase64(buffer) {
    const bytes = new Uint8Array(buffer);
    let binary = '';
    for (let i = 0; i < bytes.byteLength; i++) {
        binary += String.fromCharCode(bytes[i]);
    }
    return btoa(binary);
}

function base64ToBuffer(base64) {
    const binary = atob(base64);
    const bytes = new Uint8Array(binary.length);
    for (let i = 0; i < binary.length; i++) {
        bytes[i] = binary.charCodeAt(i);
    }
    return bytes.buffer;
}
