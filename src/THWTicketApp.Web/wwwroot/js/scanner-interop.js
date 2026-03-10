// QR/Barcode scanner using browser camera and BarcodeDetector API
let stream = null;
let scanning = false;
let dotNetRef = null;
let animationFrameId = null;

export async function startScanner(videoElementId, objRef) {
    dotNetRef = objRef;
    const video = document.getElementById(videoElementId);
    if (!video) return false;

    try {
        stream = await navigator.mediaDevices.getUserMedia({
            video: { facingMode: 'environment' }
        });
        video.srcObject = stream;
        await video.play();
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
        return false;
    }
}

export function stopScanner() {
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
