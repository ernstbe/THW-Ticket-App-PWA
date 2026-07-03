// QR/Barcode scanner using browser camera and BarcodeDetector API
let stream = null;
let scanning = false;
let dotNetRef = null;
let animationFrameId = null;
// Bumped by every startScanner() and stopScanner() call. A getUserMedia() that
// is still pending when the component is disposed (or a new scan starts) checks
// this after it resolves: if the token moved, the acquisition is stale and must
// be stopped immediately instead of arming a disposed component and leaking the
// live camera track.
let startToken = 0;

export async function startScanner(videoElementId, objRef) {
    const myToken = ++startToken;
    dotNetRef = objRef;
    const video = document.getElementById(videoElementId);
    if (!video) return false;

    let localStream = null;
    try {
        localStream = await navigator.mediaDevices.getUserMedia({
            video: { facingMode: 'environment' }
        });

        // Disposed / superseded while getUserMedia was pending — don't arm,
        // just release the camera we just opened.
        if (myToken !== startToken) {
            localStream.getTracks().forEach(t => t.stop());
            return false;
        }

        stream = localStream;
        video.srcObject = stream;
        await video.play();

        // play() also awaits; re-check we weren't superseded meanwhile.
        if (myToken !== startToken) {
            stopStream();
            return false;
        }

        scanning = true;

        // Use BarcodeDetector if available, else fallback to manual input
        if ('BarcodeDetector' in window) {
            const detector = new BarcodeDetector({ formats: ['qr_code', 'code_128', 'ean_13', 'ean_8'] });
            const scan = async () => {
                if (!scanning) return;
                try {
                    const barcodes = await detector.detect(video);
                    if (barcodes.length > 0) {
                        const value = barcodes[0].rawValue;
                        if (dotNetRef) dotNetRef.invokeMethodAsync('OnBarcodeDetected', value);
                        scanning = false;
                        stopStream();
                        return;
                    }
                } catch { }
                animationFrameId = requestAnimationFrame(scan);
            };
            animationFrameId = requestAnimationFrame(scan);
        }
        return true;
    } catch (e) {
        console.error('Camera access error:', e);
        // getUserMedia may have already opened the camera before a later step
        // (e.g. video.play()) rejected. Stop whatever we acquired so the camera
        // indicator light doesn't stay on until a full page reload.
        if (localStream) {
            localStream.getTracks().forEach(t => t.stop());
            if (stream === localStream) stream = null;
        }
        scanning = false;
        return false;
    }
}

export function stopScanner() {
    // Supersede any in-flight startScanner() so its pending getUserMedia releases
    // the camera instead of re-arming this now-stopped component.
    startToken++;
    scanning = false;
    if (animationFrameId) cancelAnimationFrame(animationFrameId);
    stopStream();
}

function stopStream() {
    if (stream) {
        stream.getTracks().forEach(t => t.stop());
        stream = null;
    }
}

export function isBarcodeDetectorSupported() {
    return 'BarcodeDetector' in window;
}
